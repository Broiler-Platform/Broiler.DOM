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
    int MaximumDepth = 1024,
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

    private static void Append<TNode>(
        TNode node,
        HtmlSerializationAdapter<TNode> adapter,
        HtmlSerializationOptions options,
        StringBuilder builder,
        int depth)
    {
        if (depth > options.MaximumDepth)
            throw new InvalidOperationException($"Maximum HTML serialization depth ({options.MaximumDepth}) exceeded.");

        var kind = adapter.GetKind(node);
        if (kind == HtmlSerializationNodeKind.Text)
        {
            var text = adapter.GetText(node) ?? string.Empty;
            builder.Append(options.EncodeTextNodes ? Encode(text) : text);
            return;
        }

        if (kind == HtmlSerializationNodeKind.Comment)
        {
            builder.Append("<!--").Append(adapter.GetText(node) ?? string.Empty).Append("-->");
            return;
        }

        if (kind is HtmlSerializationNodeKind.Fragment or HtmlSerializationNodeKind.DocumentRoot)
        {
            foreach (var child in adapter.GetChildren(node))
                Append(child, adapter, options, builder, depth + 1);
            return;
        }

        if (kind == HtmlSerializationNodeKind.DocumentType)
        {
            var name = adapter.GetName(node);
            builder.Append("<!DOCTYPE ").Append(string.IsNullOrWhiteSpace(name) ? "html" : name).Append('>');
            return;
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
            return;

        var children = adapter.GetChildren(node).ToArray();
        if (children.Length > 0)
        {
            foreach (var child in children)
                Append(child, adapter, options, builder, depth + 1);
        }
        else if (adapter.GetText(node) is { Length: > 0 } text)
        {
            builder.Append(tagName is "script" or "style" ? text : Encode(text));
        }
        else if (adapter.GetRawInnerHtml(node) is { Length: > 0 } rawInnerHtml)
        {
            builder.Append(rawInnerHtml);
        }

        builder.Append("</").Append(tagName).Append('>');
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
