using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.Dom;

// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
// Broiler-Falsified-If: a node is moved in without the pre-insert checks and ends up as its own ancestor, as when an element's parent is passed to that element's ReplaceChildren
// Broiler-Human:        PENDING
public abstract partial class DomNode
{
    /// <summary>
    /// Inserts the specified nodes just before this node in its parent's children.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: the params overload leaves the tree different from what the sequence overload leaves given the same nodes
    // Broiler-Human:        PENDING
    public void Before(params DomNode[] nodes) => Before((IEnumerable<DomNode>)nodes);

    /// <summary>
    /// Inserts the specified nodes just before this node in its parent's children.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: x.Before(b, p), where p is x's previous sibling, leaves b and p anywhere other than directly before x in that order
    // Broiler-Human:        PENDING
    public void Before(IEnumerable<DomNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var parent = ParentNode;
        if (parent is null)
            return;

        var nodesList = nodes.Where(static n => n is not null).ToList();
        if (nodesList.Count == 0)
            return;

        var viablePreviousSibling = PreviousSibling;
        while (viablePreviousSibling is not null && nodesList.Contains(viablePreviousSibling))
            viablePreviousSibling = viablePreviousSibling.PreviousSibling;

        var fragment = parent.ConvertNodesToFragment(nodesList);
        var reference = viablePreviousSibling is not null ? viablePreviousSibling.NextSibling : parent.FirstChild;
        parent.InsertBefore(fragment, reference);
    }

    /// <summary>
    /// Inserts the specified nodes just after this node in its parent's children.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: the params overload leaves the tree different from what the sequence overload leaves given the same nodes
    // Broiler-Human:        PENDING
    public void After(params DomNode[] nodes) => After((IEnumerable<DomNode>)nodes);

    /// <summary>
    /// Inserts the specified nodes just after this node in its parent's children.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: x.After(n, b), where n is x's next sibling, leaves n and b anywhere other than directly after x in that order
    // Broiler-Human:        PENDING
    public void After(IEnumerable<DomNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var parent = ParentNode;
        if (parent is null)
            return;

        var nodesList = nodes.Where(static n => n is not null).ToList();
        if (nodesList.Count == 0)
            return;

        var viableNextSibling = NextSibling;
        while (viableNextSibling is not null && nodesList.Contains(viableNextSibling))
            viableNextSibling = viableNextSibling.NextSibling;

        var fragment = parent.ConvertNodesToFragment(nodesList);
        parent.InsertBefore(fragment, viableNextSibling);
    }

    /// <summary>
    /// Replaces this node with the specified nodes in its parent's children.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: the params overload leaves the tree different from what the sequence overload leaves given the same nodes
    // Broiler-Human:        PENDING
    public void ReplaceWith(params DomNode[] nodes) => ReplaceWith((IEnumerable<DomNode>)nodes);

    /// <summary>
    /// Replaces this node with the specified nodes in its parent's children.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: a ReplaceWith call that throws HierarchyRequestError, such as a doctype replaced by an element in a document that already has one, has already removed this node from its parent
    // Broiler-Human:        PENDING
    public void ReplaceWith(IEnumerable<DomNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var parent = ParentNode;
        if (parent is null)
            return;

        var nodesList = nodes.Where(static n => n is not null).ToList();
        var viableNextSibling = NextSibling;
        while (viableNextSibling is not null && nodesList.Contains(viableNextSibling))
            viableNextSibling = viableNextSibling.NextSibling;

        var fragment = parent.ConvertNodesToFragment(nodesList);
        if (ReferenceEquals(ParentNode, parent))
            parent.RemoveChild(this);

        parent.InsertBefore(fragment, viableNextSibling);
    }

    /// <summary>
    /// Inserts the specified nodes before the first child of this node.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: the params overload leaves the tree different from what the sequence overload leaves given the same nodes
    // Broiler-Human:        PENDING
    public void Prepend(params DomNode[] nodes) => Prepend((IEnumerable<DomNode>)nodes);

    /// <summary>
    /// Inserts the specified nodes before the first child of this node.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s4.2.6; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: p.Prepend(a, b) leaves a and b anywhere other than ahead of p's former first child, in argument order
    // Broiler-Human:        PENDING
    public void Prepend(IEnumerable<DomNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var nodesList = nodes.Where(static n => n is not null).ToList();
        if (nodesList.Count == 0)
            return;

        var fragment = ConvertNodesToFragment(nodesList);
        InsertBefore(fragment, FirstChild);
    }

    /// <summary>
    /// Inserts the specified nodes after the last child of this node.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: the params overload leaves the tree different from what the sequence overload leaves given the same nodes
    // Broiler-Human:        PENDING
    public void Append(params DomNode[] nodes) => Append((IEnumerable<DomNode>)nodes);

    /// <summary>
    /// Inserts the specified nodes after the last child of this node.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s4.2.6; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: p.Append(a, b) leaves a and b anywhere other than after p's former last child, in argument order
    // Broiler-Human:        PENDING
    public void Append(IEnumerable<DomNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var nodesList = nodes.Where(static n => n is not null).ToList();
        if (nodesList.Count == 0)
            return;

        var fragment = ConvertNodesToFragment(nodesList);
        InsertBefore(fragment, null);
    }

    /// <summary>
    /// Replaces all children of this node with the specified nodes.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: the params overload leaves the tree different from what the sequence overload leaves given the same nodes
    // Broiler-Human:        PENDING
    public void ReplaceChildren(params DomNode[] nodes) => ReplaceChildren((IEnumerable<DomNode>)nodes);

    /// <summary>
    /// Replaces all children of this node with the specified nodes.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s4.2.6; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: an element's own parent passed to its ReplaceChildren is accepted without HierarchyRequestError, leaving the two nodes each other's parent so the next parent-chain walk never ends
    // Broiler-Human:        PENDING
    public void ReplaceChildren(IEnumerable<DomNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var nodesList = nodes.Where(static n => n is not null).ToList();
        var fragment = ConvertNodesToFragment(nodesList);
        ReplaceAllChildren(fragment);
    }

    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s4.2.6; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: called on a DomDocument it reads OwnerDocument and throws instead of creating the fragment in that document
    // Broiler-Human:        PENDING
    private DomDocumentFragment ConvertNodesToFragment(IEnumerable<DomNode> nodes)
    {
        var targetDocument = this is DomDocument doc ? doc : OwnerDocument;
        var fragment = targetDocument.CreateDocumentFragment();
        foreach (var node in nodes)
        {
            if (node is not null)
                fragment.AppendChild(node);
        }
        return fragment;
    }
}
