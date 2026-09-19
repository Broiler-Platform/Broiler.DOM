using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.Dom;

public abstract partial class DomNode
{
    /// <summary>
    /// Inserts the specified nodes just before this node in its parent's children.
    /// </summary>
    public void Before(params DomNode[] nodes) => Before((IEnumerable<DomNode>)nodes);

    /// <summary>
    /// Inserts the specified nodes just before this node in its parent's children.
    /// </summary>
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
    public void After(params DomNode[] nodes) => After((IEnumerable<DomNode>)nodes);

    /// <summary>
    /// Inserts the specified nodes just after this node in its parent's children.
    /// </summary>
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
    public void ReplaceWith(params DomNode[] nodes) => ReplaceWith((IEnumerable<DomNode>)nodes);

    /// <summary>
    /// Replaces this node with the specified nodes in its parent's children.
    /// </summary>
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
    public void Prepend(params DomNode[] nodes) => Prepend((IEnumerable<DomNode>)nodes);

    /// <summary>
    /// Inserts the specified nodes before the first child of this node.
    /// </summary>
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
    public void Append(params DomNode[] nodes) => Append((IEnumerable<DomNode>)nodes);

    /// <summary>
    /// Inserts the specified nodes after the last child of this node.
    /// </summary>
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
    public void ReplaceChildren(params DomNode[] nodes) => ReplaceChildren((IEnumerable<DomNode>)nodes);

    /// <summary>
    /// Replaces all children of this node with the specified nodes.
    /// </summary>
    public void ReplaceChildren(IEnumerable<DomNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var nodesList = nodes.Where(static n => n is not null).ToList();
        var fragment = ConvertNodesToFragment(nodesList);
        ReplaceAllChildren(fragment);
    }

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
