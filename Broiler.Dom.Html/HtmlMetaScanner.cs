using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Dom;

namespace Broiler.Dom.Html;

/// <summary>
/// Scanners for document-level metadata declared in HTML <c>&lt;meta&gt;</c> tags,
/// including pre-parse token scans (<c>http-equiv</c>, CSP, refresh) and parsed DOM
/// color-scheme discovery.
/// </summary>
public static class HtmlMetaScanner
{
    /// <summary>
    /// Finds the <c>content</c> attribute of the first <c>&lt;meta http-equiv="..."&gt;</c> tag
    /// in <paramref name="html"/> matching <paramref name="headerName"/> (case-insensitive).
    /// If <paramref name="trimHeader"/> is false (the default), the attribute value must match without trimming
    /// (e.g. CSP requires exact header name matching with no leading or trailing whitespace).
    /// Returns <see langword="null"/> if none is present or if its <c>content</c> is whitespace.
    /// </summary>
    public static string? FindHttpEquivContent(string html, string headerName, bool trimHeader = false)
    {
        ArgumentNullException.ThrowIfNull(headerName);
        if (string.IsNullOrWhiteSpace(html))
            return null;

        foreach (var token in new HtmlTokenizer().Tokenize(html))
        {
            if (token.Type != TokenType.StartTag ||
                !string.Equals(token.Name, "meta", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!token.Attributes.TryGetValue("http-equiv", out var httpEquiv))
                continue;

            var matchValue = trimHeader ? httpEquiv?.Trim() : httpEquiv;
            if (!string.Equals(matchValue, headerName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (token.Attributes.TryGetValue("content", out var content) &&
                !string.IsNullOrWhiteSpace(content))
            {
                return content;
            }
        }

        return null;
    }

    /// <summary>
    /// Enumerates the <c>content</c> attribute values of all <c>&lt;meta http-equiv="..."&gt;</c>
    /// tags in <paramref name="html"/> matching <paramref name="headerName"/> (case-insensitive).
    /// </summary>
    public static IEnumerable<string> FindAllHttpEquivContents(string html, string headerName, bool trimHeader = false)
    {
        ArgumentNullException.ThrowIfNull(headerName);
        if (string.IsNullOrWhiteSpace(html))
            yield break;

        foreach (var token in new HtmlTokenizer().Tokenize(html))
        {
            if (token.Type != TokenType.StartTag ||
                !string.Equals(token.Name, "meta", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!token.Attributes.TryGetValue("http-equiv", out var httpEquiv))
                continue;

            var matchValue = trimHeader ? httpEquiv?.Trim() : httpEquiv;
            if (!string.Equals(matchValue, headerName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (token.Attributes.TryGetValue("content", out var content) &&
                !string.IsNullOrWhiteSpace(content))
            {
                yield return content;
            }
        }
    }

    /// <summary>
    /// Returns the directive string (<c>content</c> value) of the first
    /// <c>&lt;meta http-equiv="Content-Security-Policy" content="..."&gt;</c> tag in <paramref name="html"/>,
    /// or <see langword="null"/> when none is present. Does not trim the header name attribute, per CSP §8.1.1.
    /// </summary>
    public static string? FindCspPolicyContent(string html) =>
        FindHttpEquivContent(html, "Content-Security-Policy", trimHeader: false);

    /// <summary>
    /// Returns the <c>content</c> value of the first
    /// <c>&lt;meta http-equiv="refresh" content="..."&gt;</c> tag in <paramref name="html"/>,
    /// or <see langword="null"/> when none is present. Trims the header name attribute per HTML refresh steps.
    /// </summary>
    public static string? FindMetaRefreshContent(string html) =>
        FindHttpEquivContent(html, "refresh", trimHeader: true);

    /// <summary>
    /// Finds the effective document <c>color-scheme</c> value from the first valid
    /// <c>&lt;meta name="color-scheme" content="..."&gt;</c> element in the document tree.
    /// Ignores elements inside shadow trees per HTML §4.2.5.3.
    /// </summary>
    public static string? FindMetaColorScheme(DomNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        foreach (var element in root.InclusiveDescendants().OfType<DomElement>())
        {
            if (!string.Equals(element.TagName, "meta", StringComparison.OrdinalIgnoreCase))
                continue;

            var name = element.GetAttributeByQualifiedName("name");
            if (!string.Equals(name?.Trim(), "color-scheme", StringComparison.OrdinalIgnoreCase))
                continue;

            if (IsInShadowTree(element))
                continue;

            var content = element.GetAttributeByQualifiedName("content");
            if (IsValidColorSchemeValue(content))
            {
                return content!.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Whether <paramref name="content"/> is a valid CSS <c>&lt;'color-scheme'&gt;</c> value:
    /// a whitespace-separated list of CSS identifiers (<c>normal</c>, <c>light</c>, <c>dark</c>,
    /// <c>only</c>, or a custom ident per CSS Color Adjust §2).
    /// </summary>
    public static bool IsValidColorSchemeValue(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var tokens = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return false;

        foreach (var token in tokens)
        {
            foreach (var ch in token)
            {
                if (!(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch > 0x7F))
                    return false;
            }
        }

        return true;
    }

    /// <remarks>
    /// Two shapes of shadow root reach this. <see cref="DomShadowRoot"/> is the canonical one, which
    /// the HTML parser now produces for <c>&lt;template shadowrootmode&gt;</c>; the
    /// <c>#shadow-root</c> element is the synthetic sentinel a compatibility layer puts in the tree
    /// in its place. A scan rooted at the document reaches neither — a shadow root is not a child of
    /// its host — but one rooted inside a shadow tree walks up into it, and that is the case
    /// HTML §4.2.5.3 is about.
    /// </remarks>
    private static bool IsInShadowTree(DomNode node)
    {
        for (var current = node.ParentNode; current != null; current = current.ParentNode)
        {
            if (current is DomShadowRoot)
                return true;

            if (current is DomElement el && string.Equals(el.TagName, "#shadow-root", StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
