using System;
using System.Buffers;
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

    /// <summary>
    /// ASCII whitespace (Infra: tab, LF, FF, CR, space) — the only characters the "initial" insertion
    /// mode ignores. Not <c>char.IsWhiteSpace</c>: U+00A0, U+000B and U+FEFF are ordinary characters
    /// there, and end the mode.
    /// </summary>
    private static readonly SearchValues<char> AsciiWhitespace = SearchValues.Create("\t\n\f\r ");

    /// <remarks>
    /// <paramref name="html"/> is the already-decoded input stream. A byte order mark belongs to the
    /// input byte stream (HTML §13.2.3) and the Encoding Standard's decode strips it before
    /// tokenization, so it must not still be at the start of the string: a U+FEFF there is an
    /// ordinary character, which ends the "initial" insertion mode and makes a DOCTYPE after it a
    /// late one (no DocumentType). <c>File.ReadAllText</c> and <c>StreamReader</c> strip it;
    /// <c>Encoding.UTF8.GetString</c> does not.
    /// </remarks>
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

        // HTML §13.2.6.4.1: the parser starts in the "initial" insertion mode, and only a DOCTYPE
        // token seen there becomes the document's DocumentType. Comments and ASCII whitespace keep it
        // there; anything else ends it, and every later mode ("before html" onward, and the fragment
        // parsing algorithm, which never enters "initial") ignores a DOCTYPE as a parse error. A
        // DocumentType node is the only document-mode signal this component emits, so a DOCTYPE
        // accepted after content turned a quirks-mode page into a standards-mode one for every
        // consumer of the tree.
        var initialInsertionMode = true;

        foreach (var token in new HtmlTokenizer().Tokenize(html))
        {
            // Decided here, ahead of the switch, rather than inside the per-type cases: the character
            // case `continue`s past whitespace it drops before the head exists — testing Unicode
            // whitespace, so U+00A0 with it — and a U+00A0 must still end the initial mode.
            var inInitialInsertionMode = initialInsertionMode;
            if (initialInsertionMode && !StaysInInitialInsertionMode(token))
                initialInsertionMode = false;

            switch (token.Type)
            {
                case TokenType.Doctype:
                    // A DOCTYPE after the initial insertion mode is a parse error and ignored: no
                    // node, quirks mode (which, with no mode property, is just that absence). This
                    // used to insert the first named DOCTYPE before <html> wherever it appeared, so
                    // `<p>x</p><!DOCTYPE html>` read as standards where Chromium gives
                    // document.doctype === null. The mode flag also covers what the old
                    // `DocumentType is null` guard did: the first DOCTYPE ends the mode, so a second
                    // one is never in it, and the document's own children were removed above. A
                    // nameless DOCTYPE (`<!DOCTYPE>`) ends the mode too, so the named one after it is
                    // ignored; that the nameless token creates no node itself is a separate gap.
                    if (inInitialInsertionMode && !string.IsNullOrWhiteSpace(token.Name))
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

                    if (tag.Equals("title", StringComparison.OrdinalIgnoreCase) && !IsInTemplate(openElements))
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

                    InsertionPoint(parent).AppendChild(element);
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
                        InsertionPoint(parent).AppendChild(text);
                    }
                    break;
                }

                case TokenType.Comment:
                {
                    var parent = !bodyOpened && openElements.Count > 0 && ReferenceEquals(openElements.Peek(), body)
                        ? head
                        : openElements.Count > 0 ? openElements.Peek() : body;
                    InsertionPoint(parent).AppendChild(document.CreateComment(token.Data ?? string.Empty));
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
        // A template context parsed the input into the wrapper template's contents, not into its
        // child list, so that is where the fragment's nodes are.
        foreach (var child in InsertionPoint(context).ChildNodes.ToArray())
            fragment.AppendChild(child);
        return new HtmlFragmentParseResult(fragment, result.Diagnostics);
    }

    /// <summary>
    /// Where a node inserted into <paramref name="parent"/> actually goes (HTML §13.2.6.1, "the
    /// appropriate place for inserting a node"). A <c>&lt;template&gt;</c> takes no children of its
    /// own: everything between its tags belongs to its template contents (§4.12.3), so insertions
    /// are redirected into that fragment.
    /// </summary>
    private static DomNode InsertionPoint(DomElement parent) => parent.TemplateContents ?? (DomNode)parent;

    /// <summary>Whether any element still open is a <c>&lt;template&gt;</c>.</summary>
    /// <remarks>
    /// Asked only by the branches that answer "where does this go" with the document's head rather
    /// than the current insertion point. Inside a template those would pull content back out of the
    /// inert fragment the Standard just put it in — and, for a <c>&lt;title&gt;</c>, make markup
    /// that renders nothing the document's title.
    /// </remarks>
    private static bool IsInTemplate(Stack<DomElement> openElements)
    {
        foreach (var element in openElements)
        {
            if (element.LocalName.Equals("template", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
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

    /// <summary>Whether <paramref name="token"/> leaves the parser in HTML §13.2.6.4.1's "initial" insertion mode.</summary>
    /// <remarks>
    /// Only a comment or ASCII whitespace does. A DOCTYPE is processed there and then moves the
    /// parser to "before html", so it ends the mode too, nameless or not; so does any tag and any
    /// other character. The spec emits one character token per code point, where this tokenizer
    /// emits a whole run up to the next <c>&lt;</c>, but a DOCTYPE can only follow the whole run, so
    /// "is anything in the run not ASCII whitespace" gives the same answer for it. CR stays in the
    /// set even though input stream preprocessing turns literal CRs into LF: <c>&amp;#13;</c> still
    /// decodes to U+000D. A <c>&lt;?xml ...?&gt;</c> produces no token here at all (the spec's bogus
    /// comment would be a comment token), so it keeps the mode either way.
    /// <para>
    /// The answer is only as good as the token stream, and two tokenizer departures reach it. Whitespace
    /// spelled as a character reference the tokenizer's decoder leaves literal is text here, so it ends
    /// the mode where the Standard's ignored whitespace would not: <c>&amp;Tab;</c> and
    /// <c>&amp;NewLine;</c> (named references outside the platform's table, which stays the platform's)
    /// and a numeric reference with no semicolon (<c>&amp;#32</c>). And the end tag open state accepts
    /// any letter where the Standard wants an ASCII alpha, so <c>&lt;/é&gt;</c> is an end tag, which ends
    /// the mode, rather than the bogus comment that would keep it. A DOCTYPE after either is ignored.
    /// </para>
    /// </remarks>
    private static bool StaysInInitialInsertionMode(HtmlToken token) => token.Type switch
    {
        TokenType.Comment => true,
        TokenType.Character => !token.Data.AsSpan().ContainsAnyExcept(AsciiWhitespace),
        _ => false,
    };

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

    /// <remarks>
    /// Every wrapper opens with <c>&lt;html&gt;</c>, a start tag, which takes <see cref="ParseDocument"/>
    /// out of the "initial" insertion mode before the caller's markup is reached. That is what the
    /// HTML fragment parsing algorithm does too — it resets the insertion mode from the context
    /// element and never starts in "initial" — so a DOCTYPE in fragment input creates no node, not
    /// even in the synthetic document. A new wrapper must keep a start tag ahead of the input.
    /// <para>
    /// Nothing follows the input. The HTML fragment parsing algorithm (§13.4) tokenizes the input and
    /// nothing else, so the input's end is the end of the stream; this builder closes whatever is
    /// still open at end of input, so closing tags after it inserted nothing. What they did do was
    /// become part of any input left unfinished. In a div, an unterminated script, style or noscript
    /// read <c>&lt;/div&gt;&lt;/body&gt;&lt;/html&gt;</c> as its text, and so did an unterminated
    /// comment; <c>&lt;a href=x</c>, which end of input drops (eof-in-tag), became an element whose
    /// href was <c>x&lt;/div</c>. Once title, textarea, xmp, iframe, noembed,
    /// noframes and plaintext read as text too, every innerHTML or document.write with an unclosed
    /// one would have shown the wrapper's tags in the page, and a <c>plaintext</c> context — which no
    /// end tag leaves — always would.
    /// </para>
    /// <para>
    /// Input that contains the context element's own end tag still closes the wrapper early and loses
    /// what follows (<c>textarea.innerHTML = "a&lt;/textarea&gt;b"</c>). §13.4 sets the tokenizer state
    /// from the context element and has no appropriate end tag in the fragment case; that needs a
    /// parser seeded from the context rather than a string wrapper (roadmap D6).
    /// </para>
    /// </remarks>
    private static string BuildFragmentDocument(string contextTag, string html) => contextTag switch
    {
        "html" => $"<html>{html}",
        "head" => $"<html><head>{html}",
        "body" => $"<html><head></head><body>{html}",
        "table" => $"<html><head></head><body><table>{html}",
        "thead" or "tbody" or "tfoot" => $"<html><head></head><body><table><{contextTag}>{html}",
        "tr" => $"<html><head></head><body><table><tbody><tr>{html}",
        "td" or "th" => $"<html><head></head><body><table><tbody><tr><{contextTag}>{html}",
        "colgroup" => $"<html><head></head><body><table><colgroup>{html}",
        "caption" => $"<html><head></head><body><table><caption>{html}",
        "select" => $"<html><head></head><body><select>{html}",
        "template" => $"<html><head></head><body><template>{html}",
        _ => $"<html><head></head><body><{contextTag}>{html}"
    };
}
