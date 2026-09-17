using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.Dom.Html;

public sealed record HtmlParseDiagnostic(string Message, int? SourceOffset = null);

public sealed record HtmlDocumentParseResult(
    DomDocument Document,
    string Title,
    IReadOnlyList<HtmlParseDiagnostic> Diagnostics);

public sealed record HtmlFragmentParseResult(
    DomDocumentFragment Fragment,
    IReadOnlyList<HtmlParseDiagnostic> Diagnostics);

/// <summary>
/// Shared HTML tree builder for the supported WHATWG-aligned subset.
/// Document and fragment parsing use the same token stream and insertion rules.
/// </summary>
public sealed class HtmlDocumentParser
{
    /// <remarks>
    /// <c>frame</c> is here even though it is not one of the spec's "void elements": HTML
    /// §"the in frameset insertion mode" inserts a <c>frame</c> element and *immediately pops it*
    /// off the stack of open elements, so it can never take children either. Without it a
    /// <c>&lt;frameset&gt;</c>'s second frame parsed as a child of its first, the frameset saw one
    /// cell instead of two, and every frame after the first painted nothing —
    /// <c>DomParser.LayoutFramesetChildren</c> lays out the cells it is given.
    /// </remarks>
    private static readonly IReadOnlySet<string> VoidElements = HtmlElementNames.VoidElements;

    private static readonly HashSet<string> StructuralTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "html", "head", "body"
    };

    private static readonly HashSet<string> HeadMetadataElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "style", "link", "meta", "base", "script", "noscript", "title"
    };

    private static readonly HashSet<string> PClosers = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "blockquote", "details", "dialog",
        "dd", "div", "dl", "dt", "fieldset", "figcaption", "figure",
        "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "header",
        "hgroup", "hr", "li", "main", "nav", "ol", "p", "pre", "section",
        "table", "ul"
    };

    private static readonly HashSet<string> TableElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "table", "thead", "tbody", "tfoot", "tr"
    };

    private static readonly HashSet<string> TableChildElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "caption", "colgroup", "col", "thead", "tbody", "tfoot", "tr",
        "td", "th", "style", "script", "template"
    };

    public static HtmlDocumentParseResult ParseDocument(string html, DomDocument? document = null)
    {
        ArgumentNullException.ThrowIfNull(html);
        document ??= new DomDocument();
        foreach (var child in document.ChildNodes.ToArray())
            document.RemoveChild(child);

        var root = document.CreateElement("html");
        var head = document.CreateElement("head");
        var body = document.CreateElement("body");
        document.AppendChild(root);
        root.AppendChild(head);
        root.AppendChild(body);

        var openElements = new Stack<DomElement>();
        openElements.Push(body);
        var diagnostics = new List<HtmlParseDiagnostic>();
        var title = string.Empty;
        var inTitle = false;
        var bodyOpened = false;

        // The document's head and body exist from the start here, so these two flags stand in for
        // the insertion modes that decide where inter-element whitespace belongs (HTML §13.2.6.4):
        // "before html"/"before head" ignore it, "in head" keeps it in the head, and "after head"
        // puts it in the html element, between head and body.
        var headOpened = false;
        var headClosed = false;

        foreach (var token in new HtmlTokenizer().Tokenize(html))
        {
            switch (token.Type)
            {
                case TokenType.Doctype:
                    if (document.DocumentType is null && !string.IsNullOrWhiteSpace(token.Name))
                    {
                        var doctype = document.CreateDocumentType(token.Name, token.PublicId, token.SystemId);
                        document.InsertBefore(doctype, root);
                    }
                    break;

                case TokenType.StartTag:
                {
                    var tag = token.Name ?? string.Empty;
                    if (string.IsNullOrEmpty(tag))
                        break;

                    if (StructuralTags.Contains(tag))
                    {
                        var target = tag.Equals("html", StringComparison.OrdinalIgnoreCase)
                            ? root
                            : tag.Equals("head", StringComparison.OrdinalIgnoreCase) ? head : body;
                        if (tag.Equals("head", StringComparison.OrdinalIgnoreCase))
                            headOpened = true;
                        if (tag.Equals("body", StringComparison.OrdinalIgnoreCase))
                            bodyOpened = true;
                        CopyAttributes(target, token);
                        break;
                    }

                    if (tag.Equals("title", StringComparison.OrdinalIgnoreCase))
                    {
                        inTitle = true;
                        headOpened = true;
                        var titleElement = CreateElement(document, token);
                        head.AppendChild(titleElement);
                        openElements.Push(titleElement);
                        break;
                    }

                    if (!bodyOpened && HeadMetadataElements.Contains(tag))
                    {
                        headOpened = true;
                        var metadata = CreateElement(document, token);
                        head.AppendChild(metadata);
                        if (!VoidElements.Contains(tag) && !token.SelfClosing)
                            openElements.Push(metadata);
                        break;
                    }

                    bodyOpened = true;
                    AutoCloseCurrent(openElements, tag);
                    var element = CreateElement(document, token);
                    var parent = openElements.Count > 0 ? openElements.Peek() : body;

                    if (tag.Equals("tr", StringComparison.OrdinalIgnoreCase) &&
                        parent.LocalName.Equals("table", StringComparison.OrdinalIgnoreCase))
                    {
                        var tbody = document.CreateElement("tbody");
                        parent.AppendChild(tbody);
                        openElements.Push(tbody);
                        parent = tbody;
                    }

                    if (TableElements.Contains(parent.LocalName) && !TableChildElements.Contains(tag))
                        parent = FosterParent(openElements, body);

                    parent.AppendChild(element);
                    if (!VoidElements.Contains(tag) && !token.SelfClosing)
                        openElements.Push(element);
                    break;
                }

                case TokenType.EndTag:
                {
                    var tag = token.Name ?? string.Empty;
                    if (tag.Equals("title", StringComparison.OrdinalIgnoreCase))
                    {
                        inTitle = false;
                        if (openElements.Count > 0 &&
                            openElements.Peek().LocalName.Equals("title", StringComparison.OrdinalIgnoreCase))
                        {
                            openElements.Pop();
                        }
                        break;
                    }

                    if (tag.Equals("head", StringComparison.OrdinalIgnoreCase))
                        headClosed = true;

                    if (StructuralTags.Contains(tag) || VoidElements.Contains(tag))
                        break;

                    PopToTag(openElements, tag);
                    break;
                }

                case TokenType.Character:
                {
                    if (string.IsNullOrEmpty(token.Data))
                        break;

                    if (inTitle)
                        title += token.Data;

                    var parent = openElements.Count > 0 ? openElements.Peek() : body;
                    if (!bodyOpened && ReferenceEquals(parent, body))
                    {
                        // HTML tree construction ("in head" / "after head"): leading
                        // whitespace before the body is ignored (kept in the head as
                        // non-rendering text), but the first non-whitespace character
                        // opens the body and is inserted there. Previously *all*
                        // pre-body text was redirected to the head, so a document
                        // without an explicit <body> that began with text — extremely
                        // common in WPT reftests ("Test passes if …") — silently
                        // dropped that text from the rendered output.
                        if (string.IsNullOrWhiteSpace(token.Data))
                        {
                            // Where the whitespace belongs depends on how far the document has got
                            // (HTML §13.2.6.4). Before the head exists it is dropped; between
                            // </head> and the body it belongs to the html element; inside the head
                            // it stays there. All of it used to land in the head, which put the
                            // newline after the doctype — and the one before <body> — inside it.
                            if (!headOpened)
                                continue;

                            parent = headClosed ? root : head;
                        }
                        else
                        {
                            bodyOpened = true;
                        }
                    }
                    if (TableElements.Contains(parent.LocalName) && !string.IsNullOrWhiteSpace(token.Data))
                        parent = FosterParent(openElements, body);

                    var text = document.CreateTextNode(token.Data);
                    if (ReferenceEquals(parent, root))
                    {
                        // "after head" whitespace goes where the spec's insertion point is — after
                        // the head — but this builder creates the body up front, so appending to
                        // the html element would put it after the body instead.
                        root.InsertBefore(text, body);
                    }
                    else
                    {
                        parent.AppendChild(text);
                    }
                    break;
                }

                case TokenType.Comment:
                {
                    var parent = !bodyOpened && openElements.Count > 0 && ReferenceEquals(openElements.Peek(), body)
                        ? head
                        : openElements.Count > 0 ? openElements.Peek() : body;
                    parent.AppendChild(document.CreateComment(token.Data ?? string.Empty));
                    break;
                }

                case TokenType.EndOfFile:
                    break;
            }
        }

        return new HtmlDocumentParseResult(document, title.Trim(), diagnostics);
    }

    public static HtmlFragmentParseResult ParseFragment(string html, string contextTagName)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentException.ThrowIfNullOrWhiteSpace(contextTagName);
        if (VoidElements.Contains(contextTagName))
            return new HtmlFragmentParseResult(new DomDocument().CreateDocumentFragment(), []);

        var wrapper = BuildFragmentDocument(contextTagName.ToLowerInvariant(), html);
        var result = ParseDocument(wrapper);
        var context = FindContextElement(result.Document, contextTagName) ?? result.Document.Body ?? result.Document.DocumentElement!;
        var fragment = result.Document.CreateDocumentFragment();
        foreach (var child in context.ChildNodes.ToArray())
            fragment.AppendChild(child);
        return new HtmlFragmentParseResult(fragment, result.Diagnostics);
    }

    private static DomElement CreateElement(DomDocument document, HtmlToken token)
    {
        var element = document.CreateElement(token.Name ?? throw new InvalidOperationException("A start tag must have a name."));
        CopyAttributes(element, token);
        return element;
    }

    private static void CopyAttributes(DomElement element, HtmlToken token)
    {
        foreach (var (name, value) in token.Attributes)
            element.SetAttribute(name, value);
    }

    private static void AutoCloseCurrent(Stack<DomElement> openElements, string incomingTag)
    {
        if (openElements.Count == 0)
            return;

        var current = openElements.Peek().LocalName;
        var close =
            current.Equals("p", StringComparison.OrdinalIgnoreCase) && PClosers.Contains(incomingTag) ||
            current.Equals("li", StringComparison.OrdinalIgnoreCase) && incomingTag.Equals("li", StringComparison.OrdinalIgnoreCase) ||
            (current is "dd" or "dt") && incomingTag is "dd" or "dt" ||
            (current is "td" or "th") && incomingTag is "td" or "th" or "tr" ||
            current.Equals("tr", StringComparison.OrdinalIgnoreCase) && incomingTag.Equals("tr", StringComparison.OrdinalIgnoreCase) ||
            (current is "thead" or "tbody" or "tfoot") && incomingTag is "thead" or "tbody" or "tfoot" ||
            current.Equals("option", StringComparison.OrdinalIgnoreCase) && incomingTag is "option" or "optgroup" ||
            current.Equals("optgroup", StringComparison.OrdinalIgnoreCase) && incomingTag.Equals("optgroup", StringComparison.OrdinalIgnoreCase);
        if (close)
            openElements.Pop();
    }

    private static void PopToTag(Stack<DomElement> openElements, string tag)
    {
        while (openElements.Count > 1)
        {
            if (openElements.Pop().LocalName.Equals(tag, StringComparison.OrdinalIgnoreCase))
                return;
        }
    }

    private static DomElement FosterParent(Stack<DomElement> openElements, DomElement body)
    {
        foreach (var element in openElements)
        {
            if (element.LocalName.Equals("table", StringComparison.OrdinalIgnoreCase))
                return element.ParentNode as DomElement ?? body;
        }
        return body;
    }

    private static DomElement? FindContextElement(DomDocument document, string contextTagName) =>
        document
            .Descendants()
            .OfType<DomElement>()
            .FirstOrDefault(element => element.LocalName.Equals(contextTagName, StringComparison.OrdinalIgnoreCase));

    private static string BuildFragmentDocument(string contextTag, string html) => contextTag switch
    {
        "html" => $"<html>{html}</html>",
        "head" => $"<html><head>{html}</head><body></body></html>",
        "body" => $"<html><head></head><body>{html}</body></html>",
        "table" => $"<html><head></head><body><table>{html}</table></body></html>",
        "thead" or "tbody" or "tfoot" => $"<html><head></head><body><table><{contextTag}>{html}</{contextTag}></table></body></html>",
        "tr" => $"<html><head></head><body><table><tbody><tr>{html}</tr></tbody></table></body></html>",
        "td" or "th" => $"<html><head></head><body><table><tbody><tr><{contextTag}>{html}</{contextTag}></tr></tbody></table></body></html>",
        "colgroup" => $"<html><head></head><body><table><colgroup>{html}</colgroup></table></body></html>",
        "caption" => $"<html><head></head><body><table><caption>{html}</caption></table></body></html>",
        "select" => $"<html><head></head><body><select>{html}</select></body></html>",
        "template" => $"<html><head></head><body><template>{html}</template></body></html>",
        _ => $"<html><head></head><body><{contextTag}>{html}</{contextTag}></body></html>"
    };
}
