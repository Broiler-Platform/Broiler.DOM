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
        DomSlotAssignmentMode slotAssignment = DomSlotAssignmentMode.Named)
        : base(host.OwnerDocument)
    {
        ArgumentNullException.ThrowIfNull(host);
        Host = host;
        Mode = mode;
        DelegatesFocus = delegatesFocus;
        SlotAssignment = slotAssignment;
    }

    /// <summary>The element hosting this shadow root.</summary>
    public DomElement Host { get; }

    /// <summary>The encapsulation mode: Open or Closed.</summary>
    public DomShadowRootMode Mode { get; }

    /// <summary>Whether focus delegates to the first focusable child.</summary>
    public bool DelegatesFocus { get; }

    /// <summary>The slot assignment mode: Named or Manual.</summary>
    public DomSlotAssignmentMode SlotAssignment { get; }

    internal override DomNode CloneShallow(DomDocument ownerDocument) =>
        throw DomException.NotSupported("ShadowRoot cannot be cloned.");
}
