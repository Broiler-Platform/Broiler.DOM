using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Broiler.Dom;

namespace Broiler.Dom.Html;

/// <summary>
/// Queries on parsed HTML DOM trees and raw HTML markup streams for document-level metadata,
/// including document base URL (<c>&lt;base href&gt;</c>) and doctype detection.
/// </summary>
// Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=TBF
// Broiler-Falsified-If: a <base href> inside a <template>, as a DOM element or between <template> and </template> in raw source, is returned as the document's base URL
// Broiler-Human:        PENDING
public static class HtmlDocumentQueries
{
    // Broiler-AI:           Origin=AI; IP=None; Security=High; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: the constant holds a character beyond tab, LF, FF, CR and space, so source starting with U+00A0 or U+000B before <!DOCTYPE html> reports an html doctype
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.2.3; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: a <base> with a non-blank href is passed over in favour of one later in tree order
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: it returns false while GetEffectiveBaseHref finds a base, or true with a null baseHref
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.2.3; IP=Low; Security=High; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a <base href> between a <template> start tag and its </template> in the raw source is returned as the base URL
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: source whose only <base href> sits between <template> and </template> returns true
    // Broiler-Human:        PENDING
    public static bool TryGetEffectiveBaseHref(string html, [NotNullWhen(true)] out string? baseHref)
    {
        baseHref = GetEffectiveBaseHref(html);
        return baseHref is not null;
    }

    /// <summary>
    /// Determines whether the HTML source begins with a standards-mode DOCTYPE: the first token
    /// that is neither a comment nor ASCII whitespace is a DOCTYPE named <c>html</c> (HTML §13.2.6.4.1).
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.2.6.4.1; IP=Low; Security=High; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: source whose first token besides comments and ASCII whitespace is text, such as U+00A0, followed by <!DOCTYPE html> returns true
    // Broiler-Human:        PENDING
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
