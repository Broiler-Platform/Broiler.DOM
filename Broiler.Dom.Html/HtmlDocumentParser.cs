using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
/// Caller-supplied switches for a parse whose answer the markup alone does not give.
/// </summary>
/// <param name="AllowDeclarativeShadowRoots">
/// Whether a <c>&lt;template shadowrootmode&gt;</c> attaches a shadow root to its intended parent
/// (HTML §13.2.6.4.4) rather than staying an ordinary template. Defaults to <c>false</c>.
/// </param>
/// <remarks>
/// The Standard gates declarative shadow roots on the document's "allow declarative shadow roots"
/// flag, which is set by the entry point and not by the markup: navigation and
/// <c>DOMParser.parseFromString</c> set it, <c>setHTMLUnsafe</c> sets it, and <c>innerHTML</c>
/// deliberately does not — that is the whole point of the "unsafe" in the other name. This parser
/// sees none of those contexts, so the caller that does supplies the answer, and the default is the
/// conservative one: markup of unknown provenance does not silently grow shadow trees.
/// </remarks>
public sealed record HtmlParseOptions(bool AllowDeclarativeShadowRoots = false);

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
    public static HtmlDocumentParseResult ParseDocument(string html, DomDocument? document = null) =>
        ParseDocument(html, document, null);

    /// <inheritdoc cref="ParseDocument(string, DomDocument)"/>
    /// <param name="html">The already-decoded input stream.</param>
    /// <param name="document">The document to build into, or <see langword="null"/> for a new one.</param>
    /// <param name="options">
    /// Switches the markup does not answer, such as whether declarative shadow roots are allowed.
    /// <see langword="null"/> takes the defaults.
    /// </param>
    public static HtmlDocumentParseResult ParseDocument(string html, DomDocument? document, HtmlParseOptions? options) =>
        ParseDocument(html, document, options, declarativeShadowRootFloor: 0);

    /// <param name="declarativeShadowRootFloor">
    /// How many elements must already be open before a <c>&lt;template shadowrootmode&gt;</c> may
    /// attach a shadow root — the open-element depth the caller's own markup starts at. HTML
    /// §13.2.6.4.4 refuses to attach when the adjusted current node is the topmost element in the
    /// stack of open elements, which keeps the root of a parse from acquiring a shadow root; for a
    /// document that is the html element, and for a fragment it is the context element, which the
    /// string wrapper opens before the caller's markup is reached.
    /// </param>
    /// <inheritdoc cref="ParseDocument(string, DomDocument, HtmlParseOptions)"/>
    private static HtmlDocumentParseResult ParseDocument(
        string html,
        DomDocument? document,
        HtmlParseOptions? options,
        int declarativeShadowRootFloor)
    {
        ArgumentNullException.ThrowIfNull(html);
        options ??= DefaultOptions;
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

        // The template elements whose contents are a declarative shadow root rather than their own
        // fragment. Parse-local by construction: these templates are never in the tree, so the map
        // dies with the parse, and a template that reaches a caller always carries its own
        // TemplateContents.
        var shadowContents = new Dictionary<DomElement, DomShadowRoot>();
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

                    var insertionTarget = TableElements.Contains(parent.LocalName) && !TableChildElements.Contains(tag)
                        ? FosterParent(openElements, body)
                        : InsertionPoint(parent, shadowContents);

                    if (options.AllowDeclarativeShadowRoots && !token.SelfClosing &&
                        openElements.Count > declarativeShadowRootFloor &&
                        TryAttachDeclarativeShadowRoot(parent, token, diagnostics, out var declarativeShadow))
                    {
                        // HTML §13.2.6.4.4 inserts this template into the stack of open elements
                        // only — never into the tree — and makes its template contents the shadow
                        // root, so everything up to </template> is parsed straight into the shadow
                        // tree and the template itself is gone once the end tag pops it.
                        shadowContents[element] = declarativeShadow;
                        openElements.Push(element);
                        break;
                    }

                    insertionTarget.AppendChild(element);
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
                    var textTarget = TableElements.Contains(parent.LocalName) && !string.IsNullOrWhiteSpace(token.Data)
                        ? FosterParent(openElements, body)
                        : InsertionPoint(parent, shadowContents);

                    var text = document.CreateTextNode(token.Data);
                    if (ReferenceEquals(textTarget, root))
                    {
                        // "after head" whitespace goes where the spec's insertion point is — after
                        // the head — but this builder creates the body up front, so appending to
                        // the html element would put it after the body instead.
                        root.InsertBefore(text, body);
                    }
                    else
                    {
                        textTarget.AppendChild(text);
                    }
                    break;
                }

                case TokenType.Comment:
                {
                    var parent = !bodyOpened && openElements.Count > 0 && ReferenceEquals(openElements.Peek(), body)
                        ? head
                        : openElements.Count > 0 ? openElements.Peek() : body;
                    InsertionPoint(parent, shadowContents).AppendChild(document.CreateComment(token.Data ?? string.Empty));
                    break;
                }

                case TokenType.EndOfFile:
                    break;
            }
        }

        return new HtmlDocumentParseResult(document, title.Trim(), diagnostics);
    }

    public static HtmlFragmentParseResult ParseFragment(string html, string contextTagName) =>
        ParseFragment(html, contextTagName, null);

    /// <inheritdoc cref="ParseFragment(string, string)"/>
    /// <param name="html">The fragment's markup.</param>
    /// <param name="contextTagName">The context element's tag name.</param>
    /// <param name="options">
    /// Switches the markup does not answer. Declarative shadow roots belong to
    /// <c>setHTMLUnsafe</c> on this path and not to <c>innerHTML</c>, so a caller implementing the
    /// former passes them in and a caller implementing the latter does not.
    /// </param>
    public static HtmlFragmentParseResult ParseFragment(string html, string contextTagName, HtmlParseOptions? options)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentException.ThrowIfNullOrWhiteSpace(contextTagName);
        if (VoidElements.Contains(contextTagName))
            return new HtmlFragmentParseResult(new DomDocument().CreateDocumentFragment(), []);

        var (wrapper, contextDepth) = BuildFragmentDocument(contextTagName.ToLowerInvariant(), html);
        var result = ParseDocument(wrapper, null, options, contextDepth);
        var context = FindContextElement(result.Document, contextTagName) ?? result.Document.Body ?? result.Document.DocumentElement!;
        var fragment = result.Document.CreateDocumentFragment();
        // A template context parsed the input into the wrapper template's contents, not into its
        // child list, so that is where the fragment's nodes are.
        var parsed = context.TemplateContents ?? (DomNode)context;
        foreach (var child in parsed.ChildNodes.ToArray())
            fragment.AppendChild(child);
        return new HtmlFragmentParseResult(fragment, result.Diagnostics);
    }

    /// <summary>
    /// Where a node inserted into <paramref name="parent"/> actually goes (HTML §13.2.6.1, "the
    /// appropriate place for inserting a node"). A <c>&lt;template&gt;</c> takes no children of its
    /// own: everything between its tags belongs to its template contents (§4.12.3), so insertions
    /// are redirected into that fragment — or, for a template that declared a shadow root, into
    /// the shadow root that became its contents.
    /// </summary>
    private static DomNode InsertionPoint(
        DomElement parent,
        Dictionary<DomElement, DomShadowRoot> shadowContents) =>
        shadowContents.TryGetValue(parent, out var shadow)
            ? shadow
            : parent.TemplateContents ?? (DomNode)parent;

    private static readonly HtmlParseOptions DefaultOptions = new();

    /// <summary>
    /// Attribute values that put <c>shadowrootmode</c> in a state other than "none", and the
    /// encapsulation mode each one asks for (HTML §4.12.3).
    /// </summary>
    private static readonly Dictionary<string, DomShadowRootMode> ShadowRootModes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["open"] = DomShadowRootMode.Open,
            ["closed"] = DomShadowRootMode.Closed,
        };

    /// <summary>
    /// Attaches a shadow root to <paramref name="intendedParent"/> when <paramref name="token"/> is
    /// a <c>&lt;template&gt;</c> start tag declaring one (HTML §13.2.6.4.4, the "in head" insertion
    /// mode's template start tag), and reports it as the template's contents.
    /// </summary>
    /// <remarks>
    /// All four <c>shadowroot*</c> attributes are read, not just the mode: the other three are the
    /// options <c>attachShadow</c> takes, and an author who wrote them meant them. The three
    /// besides the mode are HTML boolean attributes, so presence is the value.
    /// <para>
    /// Attaching can legitimately fail — the intended parent may be an element that hosts no shadow
    /// tree, or may already host one, which is how the Standard makes only the first declarative
    /// shadow root on a host win. Both are parse errors and neither is fatal: the template stays an
    /// ordinary, inert template and a diagnostic records why, because a document is not worth
    /// refusing over one template a browser would simply leave alone.
    /// </para>
    /// </remarks>
    private static bool TryAttachDeclarativeShadowRoot(
        DomElement intendedParent,
        HtmlToken token,
        List<HtmlParseDiagnostic> diagnostics,
        [NotNullWhen(true)] out DomShadowRoot? shadowRoot)
    {
        shadowRoot = null;
        if (!string.Equals(token.Name, "template", StringComparison.OrdinalIgnoreCase) ||
            !token.Attributes.TryGetValue("shadowrootmode", out var declaredMode) ||
            !ShadowRootModes.TryGetValue(declaredMode?.Trim() ?? string.Empty, out var mode))
        {
            return false;
        }

        try
        {
            shadowRoot = intendedParent.AttachShadow(
                mode,
                delegatesFocus: token.Attributes.ContainsKey("shadowrootdelegatesfocus"),
                slotAssignment: DomSlotAssignmentMode.Named,
                clonable: token.Attributes.ContainsKey("shadowrootclonable"),
                serializable: token.Attributes.ContainsKey("shadowrootserializable"));
            return true;
        }
        catch (DomException exception)
        {
            diagnostics.Add(new HtmlParseDiagnostic(
                $"A <template shadowrootmode=\"{declaredMode}\"> inside <{intendedParent.LocalName}> " +
                $"stays an ordinary template: {exception.Message}"));
            return false;
        }
    }

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

    /// <remarks>
    /// HTML §13.2.6.1 fosters into the last table's <em>parent node</em>, which need not be an
    /// element: a table written inside a <c>&lt;template&gt;</c> hangs off the contents fragment,
    /// and one inside a declarative shadow root hangs off the shadow root. Narrowing the parent to
    /// <see cref="DomElement"/> turned both into the fallback, so
    /// <c>&lt;template&gt;&lt;table&gt;&lt;div&gt;</c> put the div in the body — markup the
    /// template model exists to keep inert, back in the document tree where every query here sees
    /// it again. The reverse case, a template <em>below</em> the table, needs nothing: a template
    /// is then the current node, which is not a table, so nothing is fostered at all.
    /// </remarks>
    private static DomNode FosterParent(Stack<DomElement> openElements, DomElement body)
    {
        foreach (var element in openElements)
        {
            if (element.LocalName.Equals("table", StringComparison.OrdinalIgnoreCase))
                return element.ParentNode ?? body;
        }
        return body;
    }

    private static DomElement? FindContextElement(DomDocument document, string contextTagName) =>
        document
            .Descendants()
            .OfType<DomElement>()
            .FirstOrDefault(element => element.LocalName.Equals(contextTagName, StringComparison.OrdinalIgnoreCase));

    /// <remarks>
    /// Every wrapper opens with <c>&lt;html&gt;</c>, a start tag, which takes
    /// <see cref="ParseDocument(string, DomDocument)"/> out of the "initial" insertion mode before
    /// the caller's markup is reached. That is what the
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
    /// <returns>
    /// The wrapper document, and how many elements the prefix leaves open when the caller's markup
    /// is reached — the body this builder pushes up front, plus each element the prefix opens.
    /// <c>html</c>, <c>head</c> and <c>body</c> start tags open nothing: those three elements exist
    /// from the start of every parse, so their tags only carry attributes across. The count is what
    /// tells the tree builder where the caller's markup begins, which is the only thing that
    /// distinguishes the context element from an element the input opened itself.
    /// </returns>
    private static (string Wrapper, int ContextDepth) BuildFragmentDocument(string contextTag, string html) => contextTag switch
    {
        "html" => ($"<html>{html}", 1),
        "head" => ($"<html><head>{html}", 1),
        "body" => ($"<html><head></head><body>{html}", 1),
        "table" => ($"<html><head></head><body><table>{html}", 2),
        "thead" or "tbody" or "tfoot" => ($"<html><head></head><body><table><{contextTag}>{html}", 3),
        "tr" => ($"<html><head></head><body><table><tbody><tr>{html}", 4),
        "td" or "th" => ($"<html><head></head><body><table><tbody><tr><{contextTag}>{html}", 5),
        "colgroup" => ($"<html><head></head><body><table><colgroup>{html}", 3),
        "caption" => ($"<html><head></head><body><table><caption>{html}", 3),
        "select" => ($"<html><head></head><body><select>{html}", 2),
        "template" => ($"<html><head></head><body><template>{html}", 2),
        _ => ($"<html><head></head><body><{contextTag}>{html}", 2)
    };
}
