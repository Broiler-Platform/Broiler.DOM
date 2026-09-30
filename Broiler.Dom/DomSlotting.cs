using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.Dom;

/// <summary>
/// Canonical slot and slottable assignment algorithms (DOM §4.2.2.3).
/// </summary>
// Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s4.2.2.3; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
// Broiler-Falsified-If: a light-tree child is reported assigned to a slot of a shadow tree other than its parent host's own shadow root
// Broiler-Human:        PENDING
public static class DomSlotting
{
    /// <summary>
    /// Checks whether a &lt;slot&gt; element accepts a given slottable node based on their slot names.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s4.2.2.3; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a slot whose name attribute differs from the node's slot attribute only in letter case accepts the node
    // Broiler-Human:        PENDING
    public static bool SlotAcceptsNode(DomElement slot, DomNode node)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(node);

        var slotName = slot.GetAttribute("name");
        var nodeSlot = (node as DomElement)?.GetAttribute("slot");
        return string.IsNullOrEmpty(slotName)
            ? string.IsNullOrEmpty(nodeSlot)
            : string.Equals(slotName, nodeSlot, StringComparison.Ordinal);
    }

    /// <summary>
    /// Finds the slot in the host's shadow root assigned to <paramref name="node"/>, or <c>null</c>.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s4.2.2.3; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: in a shadow root created with Manual slot assignment, a host child whose slot attribute matches a slot's name is returned as assigned without any manual assignment
    // Broiler-Human:        PENDING
    public static DomElement? FindAssignedSlot(DomNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.ParentNode is not DomElement host || host.InternalShadowRoot is null)
            return null;

        var shadowRoot = host.InternalShadowRoot;
        foreach (var element in shadowRoot.Descendants().OfType<DomElement>())
        {
            if (string.Equals(element.LocalName, "slot", StringComparison.OrdinalIgnoreCase) &&
                SlotAcceptsNode(element, node))
            {
                return element;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the nodes assigned to <paramref name="slot"/>, optionally flattening nested slots
    /// and falling back to slot children if no nodes are assigned.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s4.2.2.3; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: the default slot's assigned nodes include a Comment child of the host, which is not slottable
    // Broiler-Human:        PENDING
    public static IReadOnlyList<DomNode> GetAssignedNodes(DomElement slot, bool flatten = false)
    {
        ArgumentNullException.ThrowIfNull(slot);
        if (!string.Equals(slot.LocalName, "slot", StringComparison.OrdinalIgnoreCase))
            return Array.Empty<DomNode>();

        if (slot.GetRootNode(composed: false) is not DomShadowRoot shadowRoot || shadowRoot.Host is null)
            return Array.Empty<DomNode>();

        var host = shadowRoot.Host;
        var assigned = new List<DomNode>();

        foreach (var child in host.ChildNodes)
        {
            if (FindAssignedSlot(child) == slot)
            {
                if (flatten && child is DomElement childSlot &&
                    string.Equals(childSlot.LocalName, "slot", StringComparison.OrdinalIgnoreCase))
                {
                    assigned.AddRange(GetAssignedNodes(childSlot, flatten: true));
                }
                else
                {
                    assigned.Add(child);
                }
            }
        }

        if (assigned.Count == 0 && flatten)
        {
            foreach (var fallbackChild in slot.ChildNodes)
            {
                if (fallbackChild is DomElement childSlot &&
                    string.Equals(childSlot.LocalName, "slot", StringComparison.OrdinalIgnoreCase))
                {
                    assigned.AddRange(GetAssignedNodes(childSlot, flatten: true));
                }
                else
                {
                    assigned.Add(fallbackChild);
                }
            }
        }

        return assigned;
    }
}
