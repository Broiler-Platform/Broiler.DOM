using System;

namespace Broiler.Dom;

/// <summary>
/// The nodes about to leave a document's tree, as <see cref="DomDocument.Removing"/> announces them:
/// <see cref="Root"/> and everything under it, or, when <see cref="ChildrenOnly"/>, only what is under it.
/// </summary>
/// <remarks>
/// The children-only form is the "replace all" a <c>textContent</c> write or <c>replaceChildren()</c>
/// performs: the parent stays, everything in it goes, and it is announced once rather than child by
/// child.
/// </remarks>
// Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s4.2.3; IP=Low; Security=Medium; Resources=2; Fingerprint=TBF
// Broiler-Falsified-If: a children-only removal claims its root, or a removal misses a node inside a shadow tree whose host it removes
// Broiler-Human:        PENDING
public readonly record struct DomRemoval(DomNode Root, bool ChildrenOnly)
{
    /// <summary>
    /// Whether <paramref name="node"/> goes with this removal: whether it is a shadow-including
    /// inclusive descendant of <see cref="Root"/>, and not <see cref="Root"/> itself when only its
    /// children go.
    /// </summary>
    /// <remarks>
    /// Shadow-including, because a host takes its shadow tree with it: an element focused inside a
    /// shadow root leaves the document when its host does.
    /// </remarks>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: Removes answers true for the root of a children-only removal, or false for an element in the shadow tree of a removed host
    // Broiler-Human:        PENDING
    public bool Removes(DomNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        for (DomNode? current = node; current is not null;
             current = current.ParentNode ?? (current as DomShadowRoot)?.Host)
        {
            if (ReferenceEquals(current, Root))
                return !ChildrenOnly || !ReferenceEquals(node, Root);
        }

        return false;
    }
}
