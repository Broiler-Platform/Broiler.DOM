using System;

namespace Broiler.Dom;

/// <summary>
/// Specifies the mode of a shadow root (DOM §4.2.2).
/// </summary>
public enum DomShadowRootMode
{
    Open,
    Closed,
}

/// <summary>
/// Specifies how slots are assigned within a shadow root (DOM §4.2.2).
/// </summary>
public enum DomSlotAssignmentMode
{
    Named,
    Manual,
}

/// <summary>
/// Represents a shadow root node (DOM §4.2.2). ShadowRoot nodes are DocumentFragment nodes
/// that are associated with a host element and encapsulate a DOM subtree.
/// </summary>
public class DomShadowRoot : DomDocumentFragment
{
    internal DomShadowRoot(
        DomElement host,
        DomShadowRootMode mode,
        bool delegatesFocus = false,
        DomSlotAssignmentMode slotAssignment = DomSlotAssignmentMode.Named,
        bool clonable = false,
        bool serializable = false)
        : base(host.OwnerDocument)
    {
        ArgumentNullException.ThrowIfNull(host);
        Host = host;
        Mode = mode;
        DelegatesFocus = delegatesFocus;
        SlotAssignment = slotAssignment;
        Clonable = clonable;
        Serializable = serializable;
    }

    /// <summary>The element hosting this shadow root.</summary>
    public DomElement Host { get; }

    /// <summary>The encapsulation mode: Open or Closed.</summary>
    public DomShadowRootMode Mode { get; }

    /// <summary>Whether focus delegates to the first focusable child.</summary>
    public bool DelegatesFocus { get; }

    /// <summary>The slot assignment mode: Named or Manual.</summary>
    public DomSlotAssignmentMode SlotAssignment { get; }

    /// <summary>
    /// Whether a clone of the host is meant to carry a clone of this shadow tree.
    /// </summary>
    /// <remarks>
    /// Recorded as given at attach time, from <c>attachShadow</c>'s <c>clonable</c> option or an
    /// HTML parser's <c>shadowrootclonable</c> attribute. This kernel's own cloning does not act on
    /// it yet: <see cref="CloneShallow"/> refuses, so cloning a host never reaches a shadow root
    /// either way. It is kept because it is what the markup said, and re-deriving it would mean
    /// parsing the document again.
    /// </remarks>
    public bool Clonable { get; }

    /// <summary>
    /// Whether this shadow tree is meant to be included by a serializer that was asked for
    /// serializable shadow roots (DOM <c>serializable</c>, HTML <c>shadowrootserializable</c>).
    /// </summary>
    /// <remarks>
    /// Recorded as given at attach time. Nothing here serializes shadow trees — <c>HtmlSerializer</c>
    /// walks the light tree only — so this is the parser's record of the author's intent for
    /// whoever implements <c>getHTML</c>, not a switch inside this component.
    /// </remarks>
    public bool Serializable { get; }

    internal override DomNode CloneShallow(DomDocument ownerDocument) =>
        throw DomException.NotSupported("ShadowRoot cannot be cloned.");
}
