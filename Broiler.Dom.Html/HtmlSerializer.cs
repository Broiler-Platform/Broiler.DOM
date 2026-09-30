using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Broiler.Dom.Html;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public enum HtmlSerializationNodeKind
{
    Element,
    Text,
    Comment,
    Fragment,
    DocumentRoot,
    DocumentType
}

// Broiler-AI:           Origin=AI; IP=None; Security=High; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: an adapter's GetRawInnerHtml or GetName hands back character data taken from the tree, and it reaches the output as markup without passing Encode
// Broiler-Human:        PENDING
public sealed record HtmlSerializationAdapter<TNode>(
    Func<TNode, HtmlSerializationNodeKind> GetKind,
    Func<TNode, string> GetName,
    Func<TNode, IEnumerable<TNode>> GetChildren,
    Func<TNode, IEnumerable<KeyValuePair<string, string>>> GetAttributes,
    Func<TNode, IEnumerable<KeyValuePair<string, string>>> GetStyles,
    Func<TNode, string?> GetText,
    Func<TNode, string?> GetRawInnerHtml);

// Broiler-AI:           Origin=AI; IP=None; Security=High; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: a default-constructed options value has EncodeTextNodes false, so text nodes are written without escaping
// Broiler-Human:        PENDING
public sealed record HtmlSerializationOptions(
    bool IncludeHtmlDoctype = false,
    int MaximumDepth = 100_000,
    bool EncodeTextNodes = true,
    bool NewLineAfterDoctype = false);

/// <summary>Deterministic HTML serialization shared by canonical and compatibility DOM surfaces.</summary>
// Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.3; IP=Low; Security=High; Resources=4; Fingerprint=TBF
// Broiler-Falsified-If: a text child of an SVG-namespace style element built through the DOM, such as <img src=x onerror=f()>, is written unescaped, so a spec parser reading the output again creates an img element
// Broiler-Human:        PENDING
public static class HtmlSerializer
{
    /// <remarks>
    /// <c>frame</c> is one of the tags HTML §"HTML fragment serialisation algorithm" names
    /// alongside the void elements as taking no end tag, and it can hold no children to close
    /// around (see <c>HtmlDocumentParser.VoidElements</c>). Emitting <c>&lt;/frame&gt;</c> put an
    /// end tag in the markup that re-parsing has to discard.
    /// </remarks>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an element named keygen, bgsound or basefont is written with its children and an end tag, where HTML fragment serialization treats it as void
    // Broiler-Human:        PENDING
    public static readonly IReadOnlySet<string> VoidElements =
        HtmlElementNames.VoidElements;

    /// <summary>
    /// HTML elements whose character-data children serialize literally (not
    /// HTML-escaped) per the HTML Standard section 13.3 "Serialising HTML
    /// fragments" - the raw text elements (script, style), the legacy ones the
    /// tokenizer also reads as RAWTEXT or PLAINTEXT (xmp, iframe, noembed,
    /// noframes, plaintext), and noscript with scripting enabled. The escapable
    /// raw text elements, title and textarea, are NOT here: their text is
    /// escaped, and the tokenizer's RCDATA state decodes it back. Single source
    /// of truth for both this serializer's leaf-text branch and compatibility
    /// adapters (e.g. the HtmlBridge serializer).
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=High; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: the set contains title, textarea or another name the tokenizer does not read as RAWTEXT or PLAINTEXT, so literal text under it re-parses as markup or decoded references
    // Broiler-Human:        PENDING
    public static readonly IReadOnlySet<string> RawTextElements =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "script", "style", "xmp", "iframe",
            "noembed", "noframes", "noscript", "plaintext"
        };

    /// <summary>
    /// Whether <paramref name="tagName"/> is an element whose text content is
    /// serialized literally rather than HTML-escaped (see <see cref="RawTextElements"/>).
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=High; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: IsRawTextElement returns true for textarea, so textarea text holding </textarea><img src=x onerror=f()> is written unescaped and re-parses as an img element
    // Broiler-Human:        PENDING
    public static bool IsRawTextElement(string tagName) => RawTextElements.Contains(tagName);

    /// <summary>
    /// Returns <c>true</c> when <paramref name="property"/> is a CSS shorthand
    /// that, if emitted after its longhands, would reset those longhands to
    /// initial values (e.g. <c>margin</c> resets <c>margin-left</c>).
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: IsShorthandProperty returns false for flex, a shorthand that resets flex-grow when it is emitted after it
    // Broiler-Human:        PENDING
    public static bool IsShorthandProperty(string property) =>
        property switch
        {
            "margin" or "padding" or "border" or "background"
                or "font" or "list-style" or "outline" => true,
            _ => false,
        };

    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.3; IP=Low; Security=High; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a call with null options writes the less-than signs and ampersands of a text node unescaped
    // Broiler-Human:        PENDING
    public static string Serialize<TNode>(
        TNode node,
        HtmlSerializationAdapter<TNode> adapter,
        HtmlSerializationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        options ??= new HtmlSerializationOptions();
        var builder = new StringBuilder();
        if (options.IncludeHtmlDoctype)
        {
            builder.Append("<!DOCTYPE html>");
            if (options.NewLineAfterDoctype)
                builder.AppendLine();
        }
        Append(node, adapter, options, builder, 0);
        return builder.ToString();
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=High; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a DomText child of a div whose data is </div><img src=x onerror=f()> is written without its less-than signs escaped
    // Broiler-Human:        PENDING
    public static string Serialize(DomNode node, HtmlSerializationOptions? options = null) =>
        Serialize(node, CanonicalAdapter, options);

    // Broiler-AI:           Origin=AI; IP=None; Security=High; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a double quote inside an attribute value is written as a literal quote character, ending the attribute early when the output is parsed again
    // Broiler-Human:        PENDING
    public static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    // A pending unit of serialization work. Either a node to open (Closing is
    // null) or a literal close-tag string to append once a node's children have
    // all been written. Kept on an explicit heap stack so serialization does not
    // recurse on the .NET call stack: a legitimately deep DOM (e.g. hundreds of
    // nested shadow hosts, WPT shadow-dom/build-deep-detached-shadow-then-append-
    // text.html) would otherwise overflow the stack. Depth is still tracked so a
    // runaway/cyclic structure is bounded by MaximumDepth instead of exhausting
    // memory.
    // Broiler-AI:           Origin=AI; IP=None; Security=High; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an entry created without the rawText argument, such as the root or a fragment's child, carries RawText true, so its text is written unescaped
    // Broiler-Human:        PENDING
    private readonly struct PendingNode<TNode>(TNode node, string? closing, int depth, bool rawText = false)
    {
        public readonly TNode Node = node;
        public readonly string? Closing = closing;
        public readonly int Depth = depth;
        public readonly bool RawText = rawText;
    }

    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.3; IP=Low; Security=High; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a text child of an SVG-namespace style element built through the DOM, such as <img src=x onerror=f()>, is written unescaped, so a spec parser reading the output again creates an img element
    // Broiler-Human:        PENDING
    private static void Append<TNode>(
        TNode root,
        HtmlSerializationAdapter<TNode> adapter,
        HtmlSerializationOptions options,
        StringBuilder builder,
        int depth)
    {
        var stack = new Stack<PendingNode<TNode>>();
        stack.Push(new PendingNode<TNode>(root, null, depth));

        while (stack.Count > 0)
        {
            var pending = stack.Pop();
            if (pending.Closing is not null)
            {
                builder.Append(pending.Closing);
                continue;
            }

            var node = pending.Node;
            var nodeDepth = pending.Depth;
            if (nodeDepth > options.MaximumDepth)
                throw new InvalidOperationException($"Maximum HTML serialization depth ({options.MaximumDepth}) exceeded.");

            var kind = adapter.GetKind(node);
            if (kind == HtmlSerializationNodeKind.Text)
            {
                var textData = adapter.GetText(node) ?? string.Empty;
                builder.Append(options.EncodeTextNodes && !pending.RawText ? Encode(textData) : textData);
                continue;
            }

            if (kind == HtmlSerializationNodeKind.Comment)
            {
                builder.Append("<!--").Append(adapter.GetText(node) ?? string.Empty).Append("-->");
                continue;
            }

            if (kind is HtmlSerializationNodeKind.Fragment or HtmlSerializationNodeKind.DocumentRoot)
            {
                PushChildren(stack, adapter.GetChildren(node), nodeDepth + 1);
                continue;
            }

            if (kind == HtmlSerializationNodeKind.DocumentType)
            {
                var name = adapter.GetName(node);
                builder.Append("<!DOCTYPE ").Append(string.IsNullOrWhiteSpace(name) ? "html" : name).Append('>');
                continue;
            }

            var tagName = adapter.GetName(node).ToLowerInvariant();
            builder.Append('<').Append(tagName);
            foreach (var (name, value) in adapter.GetAttributes(node))
                builder.Append(' ').Append(name).Append("=\"").Append(Encode(value)).Append('"');

            var styles = adapter.GetStyles(node).ToArray();
            if (styles.Length > 0)
            {
                builder.Append(" style=\"");
                for (var index = 0; index < styles.Length; index++)
                {
                    if (index > 0)
                        builder.Append("; ");
                    builder.Append(styles[index].Key).Append(": ").Append(Encode(styles[index].Value));
                }
                builder.Append('"');
            }

            builder.Append('>');
            if (VoidElements.Contains(tagName))
                continue;

            var children = adapter.GetChildren(node).ToArray();
            if (children.Length > 0)
            {
                // Defer the close tag until after every child is written, then
                // push the children so they pop (and serialize) in document order.
                stack.Push(new PendingNode<TNode>(default!, $"</{tagName}>", nodeDepth));
                PushChildren(stack, children, nodeDepth + 1, IsRawTextElement(tagName));
                continue;
            }

            if (adapter.GetText(node) is { Length: > 0 } text)
            {
                builder.Append(IsRawTextElement(tagName) ? text : Encode(text));
            }
            else if (adapter.GetRawInnerHtml(node) is { Length: > 0 } rawInnerHtml)
            {
                builder.Append(rawInnerHtml);
            }

            builder.Append("</").Append(tagName).Append('>');
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a text child of an element that is not raw text is pushed with rawText true, so its data is written unescaped
    // Broiler-Human:        PENDING
    private static void PushChildren<TNode>(
        Stack<PendingNode<TNode>> stack,
        IEnumerable<TNode> children,
        int depth,
        bool rawText = false)
    {
        // Reverse so the first child pops first (document order).
        var buffer = children as IReadOnlyList<TNode> ?? children.ToArray();
        for (var index = buffer.Count - 1; index >= 0; index--)
            stack.Push(new PendingNode<TNode>(buffer[index], null, depth, rawText));
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: the canonical adapter returns a non-null GetRawInnerHtml value for some DomNode, so that string is appended to the output with no escaping
    // Broiler-Human:        PENDING
    private static readonly HtmlSerializationAdapter<DomNode> CanonicalAdapter = new(
        GetKind: static node => node switch
        {
            DomText => HtmlSerializationNodeKind.Text,
            DomComment => HtmlSerializationNodeKind.Comment,
            DomDocumentFragment => HtmlSerializationNodeKind.Fragment,
            DomDocument => HtmlSerializationNodeKind.DocumentRoot,
            DomDocumentType => HtmlSerializationNodeKind.DocumentType,
            _ => HtmlSerializationNodeKind.Element
        },
        GetName: static node => node switch
        {
            DomElement element => element.TagName,
            DomDocumentType doctype => doctype.Name,
            _ => string.Empty
        },
        // A template serializes its template contents (HTML §13.3): those are the nodes that were
        // written between its tags. Reading ChildNodes here would round-trip every parsed <template>
        // as an empty one. The converse is the part worth knowing: a template assembled through the
        // node API keeps its children on the element, and those are not what comes back out.
        GetChildren: static node => node is DomElement { TemplateContents: { } contents }
            ? contents.ChildNodes
            : node.ChildNodes,
        GetAttributes: static node => node is DomElement element
            ? element.Attributes.Values.Select(static attribute =>
                new KeyValuePair<string, string>(attribute.QualifiedName, attribute.Value))
            : [],
        GetStyles: static _ => [],
        GetText: static node => node is DomCharacterData data ? data.Data : null,
        GetRawInnerHtml: static _ => null);
}
