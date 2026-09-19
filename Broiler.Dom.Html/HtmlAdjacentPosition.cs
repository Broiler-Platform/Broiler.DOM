using System;
using Broiler.Dom;

namespace Broiler.Dom.Html;

/// <summary>
/// The relative insertion position for DOM <c>insertAdjacentElement</c>,
/// <c>insertAdjacentText</c>, and <c>insertAdjacentHTML</c>.
/// </summary>
public enum HtmlAdjacentPosition
{
    BeforeBegin,
    AfterBegin,
    BeforeEnd,
    AfterEnd
}

/// <summary>
/// Parses insertion position keywords and resolves insertion targets and parsing contexts
/// per DOM §4.2.4 (Element insertAdjacentElement, insertAdjacentText, insertAdjacentHTML).
/// </summary>
public static class HtmlAdjacentPositionResolver
{
    /// <summary>
    /// Attempts to parse a case-insensitive position keyword into <see cref="HtmlAdjacentPosition"/>.
    /// </summary>
    public static bool TryParse(string? position, out HtmlAdjacentPosition result)
    {
        if (position is not null)
        {
            var trimmed = position.Trim();
            if (string.Equals(trimmed, "beforebegin", StringComparison.OrdinalIgnoreCase))
            {
                result = HtmlAdjacentPosition.BeforeBegin;
                return true;
            }
            if (string.Equals(trimmed, "afterbegin", StringComparison.OrdinalIgnoreCase))
            {
                result = HtmlAdjacentPosition.AfterBegin;
                return true;
            }
            if (string.Equals(trimmed, "beforeend", StringComparison.OrdinalIgnoreCase))
            {
                result = HtmlAdjacentPosition.BeforeEnd;
                return true;
            }
            if (string.Equals(trimmed, "afterend", StringComparison.OrdinalIgnoreCase))
            {
                result = HtmlAdjacentPosition.AfterEnd;
                return true;
            }
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Parses a position keyword into <see cref="HtmlAdjacentPosition"/>, or throws
    /// a <see cref="DomException"/> (<c>SyntaxError</c>) if invalid.
    /// </summary>
    public static HtmlAdjacentPosition Parse(string? position)
    {
        if (!TryParse(position, out var result))
            throw DomException.Syntax($"'{position}' is not a valid insertion position.");

        return result;
    }

    /// <summary>
    /// Resolves the <paramref name="position"/> relative to <paramref name="element"/> into
    /// the target (parent, insertionIndex) pair. Throws <see cref="DomException"/>
    /// (<c>NoModificationAllowedError</c>) when <c>BeforeBegin</c> or <c>AfterEnd</c> has no parent.
    /// </summary>
    public static (DomElement Parent, int Index) ResolveTarget(DomElement element, HtmlAdjacentPosition position)
    {
        ArgumentNullException.ThrowIfNull(element);
        switch (position)
        {
            case HtmlAdjacentPosition.BeforeBegin:
            {
                var parent = element.ParentElement
                    ?? throw DomException.NoModificationAllowed("Cannot insert adjacent content without a parent node.");
                return (parent, parent.ChildNodes.IndexOfReference(element));
            }
            case HtmlAdjacentPosition.AfterBegin:
                return (element, 0);
            case HtmlAdjacentPosition.BeforeEnd:
                return (element, element.ChildNodes.Count);
            case HtmlAdjacentPosition.AfterEnd:
            {
                var parent = element.ParentElement
                    ?? throw DomException.NoModificationAllowed("Cannot insert adjacent content without a parent node.");
                return (parent, parent.ChildNodes.IndexOfReference(element) + 1);
            }
            default:
                throw DomException.Syntax($"Invalid adjacent position '{position}'.");
        }
    }

    /// <summary>
    /// Resolves the target (parent, insertionIndex) pair from a position keyword.
    /// </summary>
    public static (DomElement Parent, int Index) ResolveTarget(DomElement element, string? position) =>
        ResolveTarget(element, Parse(position));

    /// <summary>
    /// Resolves the context element used to parse adjacent HTML. For <c>BeforeBegin</c> and
    /// <c>AfterEnd</c>, this is the element's parent element. Throws <see cref="DomException"/>
    /// (<c>NoModificationAllowedError</c>) if there is no parent element.
    /// </summary>
    public static DomElement ResolveParsingContext(DomElement element, HtmlAdjacentPosition position)
    {
        ArgumentNullException.ThrowIfNull(element);
        switch (position)
        {
            case HtmlAdjacentPosition.BeforeBegin:
            case HtmlAdjacentPosition.AfterEnd:
                return element.ParentElement
                    ?? throw DomException.NoModificationAllowed("Cannot insert adjacent HTML without a parent node.");
            default:
                return element;
        }
    }

    /// <summary>
    /// Resolves the context element used to parse adjacent HTML from a position keyword.
    /// </summary>
    public static DomElement ResolveParsingContext(DomElement element, string? position) =>
        ResolveParsingContext(element, Parse(position));
}
