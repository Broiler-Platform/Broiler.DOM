using System.Net;
using System.Text;

namespace Broiler.Dom.Html;

public enum HtmlSerializationNodeKind
{
    Element,
    Text,
    Comment,
    Fragment,
    DocumentRoot,
    DocumentType
}

public sealed record HtmlSerializationAdapter<TNode>(
    Func<TNode, HtmlSerializationNodeKind> GetKind,
    Func<TNode, string> GetName,
    Func<TNode, IEnumerable<TNode>> GetChildren,
    Func<TNode, IEnumerable<KeyValuePair<string, string>>> GetAttributes,
    Func<TNode, IEnumerable<KeyValuePair<string, string>>> GetStyles,
    Func<TNode, string?> GetText,
    Func<TNode, string?> GetRawInnerHtml);

public sealed record HtmlSerializationOptions(
    bool IncludeHtmlDoctype = false,
    int MaximumDepth = 100_000,
    bool EncodeTextNodes = true,
    bool NewLineAfterDoctype = false);

/// <summary>Deterministic HTML serialization shared by canonical and compatibility DOM surfaces.</summary>
public static class HtmlSerializer
{
    public static readonly IReadOnlySet<string> VoidElements =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "area", "base", "br", "col", "embed", "hr", "img", "input",
            "link", "meta", "param", "source", "track", "wbr"
        };

    /// <summary>
    /// Returns <c>true</c> when <paramref name="property"/> is a CSS shorthand
    /// that, if emitted after its longhands, would reset those longhands to
    /// initial values (e.g. <c>margin</c> resets <c>margin-left</c>).
    /// </summary>
    public static bool IsShorthandProperty(string property) =>
        property switch
        {
            "margin" or "padding" or "border" or "background"
                or "font" or "list-style" or "outline" => true,
            _ => false,
        };

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

    public static string Serialize(DomNode node, HtmlSerializationOptions? options = null) =>
        Serialize(node, CanonicalAdapter, options);

    public static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    // A pending unit of serialization work. Either a node to open (Closing is
    // null) or a literal close-tag string to append once a node's children have
    // all been written. Kept on an explicit heap stack so serialization does not
    // recurse on the .NET call stack: a legitimately deep DOM (e.g. hundreds of
    // nested shadow hosts, WPT shadow-dom/build-deep-detached-shadow-then-append-
    // text.html) would otherwise overflow the stack. Depth is still tracked so a
    // runaway/cyclic structure is bounded by MaximumDepth instead of exhausting
    // memory.
    private readonly struct PendingNode<TNode>(TNode node, string? closing, int depth)
    {
        public readonly TNode Node = node;
        public readonly string? Closing = closing;
        public readonly int Depth = depth;
    }

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
                builder.Append(options.EncodeTextNodes ? Encode(textData) : textData);
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
                for (var index = children.Length - 1; index >= 0; index--)
                    stack.Push(new PendingNode<TNode>(children[index], null, nodeDepth + 1));
                continue;
            }

            if (adapter.GetText(node) is { Length: > 0 } text)
            {
                builder.Append(tagName is "script" or "style" ? text : Encode(text));
            }
            else if (adapter.GetRawInnerHtml(node) is { Length: > 0 } rawInnerHtml)
            {
                builder.Append(rawInnerHtml);
            }

            builder.Append("</").Append(tagName).Append('>');
        }
    }

    private static void PushChildren<TNode>(
        Stack<PendingNode<TNode>> stack,
        IEnumerable<TNode> children,
        int depth)
    {
        // Reverse so the first child pops first (document order).
        var buffer = children as IReadOnlyList<TNode> ?? children.ToArray();
        for (var index = buffer.Count - 1; index >= 0; index--)
            stack.Push(new PendingNode<TNode>(buffer[index], null, depth));
    }

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
        GetChildren: static node => node.ChildNodes,
        GetAttributes: static node => node is DomElement element
            ? element.Attributes.Values.Select(static attribute =>
                new KeyValuePair<string, string>(attribute.QualifiedName, attribute.Value))
            : [],
        GetStyles: static _ => [],
        GetText: static node => node is DomCharacterData data ? data.Data : null,
        GetRawInnerHtml: static _ => null);
}
