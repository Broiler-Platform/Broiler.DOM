using System;
using System.Diagnostics.CodeAnalysis;
using Broiler.Dom;

namespace Broiler.Dom.Html;

/// <summary>
/// Helper operations for HTML fragment parsing, including context tag normalisation,
/// void element guards, and fragment creation.
/// </summary>
public static class HtmlFragmentParsing
{
    /// <summary>
    /// Normalises a fragment parsing context tag name. Internal pseudo-tags starting with
    /// <c>#</c> (such as <c>#shadow-root</c>) are mapped to <c>"div"</c>, and regular tag
    /// names are trimmed and converted to lower case.
    /// </summary>
    public static string NormalizeContextTag(string? contextTag)
    {
        if (string.IsNullOrWhiteSpace(contextTag))
            return "div";

        var trimmed = contextTag.Trim();
        if (trimmed.StartsWith('#'))
            return "div";

        return trimmed.ToLowerInvariant();
    }

    /// <summary>
    /// Determines whether an element with the given tag name can host parsed child fragments.
    /// Void elements (such as <c>&lt;img&gt;</c> or <c>&lt;input&gt;</c>) cannot have children and
    /// therefore cannot host an innerHTML fragment.
    /// </summary>
    public static bool CanHostFragment(string? contextTag)
    {
        if (string.IsNullOrWhiteSpace(contextTag))
            return false;

        var normalized = NormalizeContextTag(contextTag);
        return !HtmlElementNames.VoidElements.Contains(normalized);
    }

    /// <summary>
    /// Attempts to parse an HTML fragment within the context of a tag name. Returns
    /// <see langword="false"/> if the context tag cannot host child content.
    /// </summary>
    public static bool TryBuildFragment(string? contextTag, string? html, [NotNullWhen(true)] out DomDocumentFragment? fragment) =>
        TryBuildFragment(contextTag, html, null, out fragment);

    /// <inheritdoc cref="TryBuildFragment(string, string, out DomDocumentFragment)"/>
    /// <param name="contextTag">The context element's tag name.</param>
    /// <param name="html">The fragment's markup.</param>
    /// <param name="options">
    /// Switches the markup does not answer. Declarative shadow roots belong to <c>setHTMLUnsafe</c>
    /// and not to <c>innerHTML</c>, which is why the overload without this parameter leaves them off.
    /// </param>
    /// <param name="fragment">The parsed fragment, when this returns <see langword="true"/>.</param>
    public static bool TryBuildFragment(
        string? contextTag,
        string? html,
        HtmlParseOptions? options,
        [NotNullWhen(true)] out DomDocumentFragment? fragment)
    {
        fragment = null;
        if (!CanHostFragment(contextTag))
            return false;

        var tag = NormalizeContextTag(contextTag);
        fragment = HtmlDocumentParser.ParseFragment(html ?? string.Empty, tag, options).Fragment;
        return true;
    }

    /// <summary>
    /// Attempts to parse an HTML fragment within the context of an existing <see cref="DomElement"/>.
    /// Returns <see langword="false"/> if the element is a void element.
    /// </summary>
    public static bool TryBuildFragment(DomElement contextElement, string? html, [NotNullWhen(true)] out DomDocumentFragment? fragment) =>
        TryBuildFragment(contextElement, html, null, out fragment);

    /// <inheritdoc cref="TryBuildFragment(DomElement, string, out DomDocumentFragment)"/>
    /// <param name="contextElement">The element the fragment is parsed in the context of.</param>
    /// <param name="html">The fragment's markup.</param>
    /// <param name="options">Switches the markup does not answer.</param>
    /// <param name="fragment">The parsed fragment, when this returns <see langword="true"/>.</param>
    public static bool TryBuildFragment(
        DomElement contextElement,
        string? html,
        HtmlParseOptions? options,
        [NotNullWhen(true)] out DomDocumentFragment? fragment)
    {
        ArgumentNullException.ThrowIfNull(contextElement);
        return TryBuildFragment(contextElement.TagName, html, options, out fragment);
    }
}
