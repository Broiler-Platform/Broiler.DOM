using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Broiler.Dom;

namespace Broiler.Dom.Html;

/// <summary>
/// Queries on parsed HTML DOM trees and raw HTML markup streams for document-level metadata,
/// including document base URL (<c>&lt;base href&gt;</c>) and doctype detection.
/// </summary>
public static class HtmlDocumentQueries
{
    private const string AsciiWhitespace = "\t\n\f\r ";

    /// <summary>
    /// Finds the effective document base URL in a DOM tree: the <c>href</c> attribute of the
    /// first <c>&lt;base&gt;</c> element in tree order that carries a non-whitespace value, trimmed.
    /// Returns <see langword="null"/> if none is found (HTML §4.2.3).
    /// </summary>
    /// <remarks>
    /// This agrees with the <see cref="GetEffectiveBaseHref(string)"/> overload about a
    /// <c>&lt;base&gt;</c> inside a <c>&lt;template&gt;</c> without a guard of its own: the parser
    /// puts template children in the template's contents fragment (HTML §4.12.3), which is not in
    /// the tree, so a descendant walk never reaches one. The token overload cannot rely on that —
    /// it never builds a tree — which is why it counts template depth itself.
    /// </remarks>
    public static string? GetEffectiveBaseHref(DomNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        foreach (var element in root.InclusiveDescendants().OfType<DomElement>())
        {
            if (string.Equals(element.TagName, "base", StringComparison.OrdinalIgnoreCase) &&
                element.GetAttributeByQualifiedName("href") is { } href &&
                !string.IsNullOrWhiteSpace(href))
            {
                return href.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to find the effective document base URL in a DOM tree.
    /// </summary>
    public static bool TryGetEffectiveBaseHref(DomNode root, [NotNullWhen(true)] out string? baseHref)
    {
        baseHref = GetEffectiveBaseHref(root);
        return baseHref is not null;
    }

    /// <summary>
    /// Finds the effective document base URL in raw HTML source by tokenizing the markup: the
    /// <c>href</c> of the first <c>&lt;base&gt;</c> start tag outside of any <c>&lt;template&gt;</c>
    /// that carries a non-whitespace value, trimmed. Returns <see langword="null"/> if none is found.
    /// </summary>
    public static string? GetEffectiveBaseHref(string html)
    {
        if (string.IsNullOrEmpty(html) || !html.Contains("<base", StringComparison.OrdinalIgnoreCase))
            return null;

        var templateDepth = 0;
        foreach (var token in new HtmlTokenizer().Tokenize(html))
        {
            if (token.Type == TokenType.EndTag)
            {
                if (templateDepth > 0 && string.Equals(token.Name, "template", StringComparison.OrdinalIgnoreCase))
                    templateDepth--;
                continue;
            }

            if (token.Type != TokenType.StartTag)
                continue;

            if (string.Equals(token.Name, "template", StringComparison.OrdinalIgnoreCase))
            {
                if (!token.SelfClosing)
                    templateDepth++;
                continue;
            }

            if (templateDepth == 0 &&
                string.Equals(token.Name, "base", StringComparison.OrdinalIgnoreCase) &&
                token.Attributes.TryGetValue("href", out var href) &&
                !string.IsNullOrWhiteSpace(href))
            {
                return href.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to find the effective document base URL in raw HTML source.
    /// </summary>
    public static bool TryGetEffectiveBaseHref(string html, [NotNullWhen(true)] out string? baseHref)
    {
        baseHref = GetEffectiveBaseHref(html);
        return baseHref is not null;
    }

    /// <summary>
    /// Determines whether the HTML source begins with a standards-mode DOCTYPE: the first token
    /// that is neither a comment nor ASCII whitespace is a DOCTYPE named <c>html</c> (HTML §13.2.6.4.1).
    /// </summary>
    public static bool HasHtmlDoctype(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return false;

        foreach (var token in new HtmlTokenizer().Tokenize(html))
        {
            if (token.Type == TokenType.Comment ||
                (token.Type == TokenType.Character && token.Data.AsSpan().Trim(AsciiWhitespace).IsEmpty))
            {
                continue;
            }

            return token.Type == TokenType.Doctype && string.Equals(token.Name, "html", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
