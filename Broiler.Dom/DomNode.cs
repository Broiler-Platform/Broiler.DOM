using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Broiler.Dom;

public abstract class DomNode
{
    private readonly List<DomNode> _children = [];
    private readonly ReadOnlyCollection<DomNode> _childNodes;
    private DomDocument? _ownerDocument;

    protected DomNode(DomNodeType nodeType, DomDocument? ownerDocument)
    {
        NodeType = nodeType;
        _ownerDocument = ownerDocument;
        _childNodes = _children.AsReadOnly();
    }

    public DomNodeType NodeType { get; }

    /// <summary>
    /// The character data of a text/comment node (DOM <c>nodeValue</c>); <c>null</c> for
    /// element and document nodes. Lets a consumer read a node's text through the canonical
    /// type without depending on the concrete node class — e.g. a host that represents text
    /// with its own <see cref="DomNodeType.Text"/> node subtype can override this.
    /// </summary>
    public virtual string? NodeValue => null;

    public void Remove() => ParentNode?.RemoveChild(this);

    public virtual DomDocument OwnerDocument =>
        _ownerDocument ?? throw new InvalidOperationException("The document node owns itself.");

    public DomNode? ParentNode { get; private set; }

    public IReadOnlyList<DomNode> ChildNodes => _childNodes;

    public DomNode ReplaceChild(DomNode node, DomNode child)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(child);

        if (!ReferenceEquals(child.ParentNode, this))
            throw DomException.NotFound("The node to replace is not a child of this node.");

        if (ReferenceEquals(node, child))
            return child;

        var reference = child.NextSibling;
        RemoveChild(child);
        InsertBefore(node, reference);
        return child;
    }

    public DomNode? PreviousSibling
    {
        get
        {
            if (ParentNode is null)
                return null;

            var index = ParentNode._children.IndexOf(this);
            return index > 0 ? ParentNode._children[index - 1] : null;
        }
    }

    public DomNode? NextSibling
    {
        get
        {
            if (ParentNode is null)
                return null;

            var index = ParentNode._children.IndexOf(this);
            return index >= 0 && index + 1 < ParentNode._children.Count
                ? ParentNode._children[index + 1]
                : null;
        }
    }

    public DomNode? FirstChild => _children.Count == 0 ? null : _children[0];

    public DomNode? LastChild => _children.Count == 0 ? null : _children[^1];

    public bool IsConnected => GetRootNode() is DomDocument;

    public ulong TreeVersion { get; private set; }

    public DomNode AppendChild(DomNode node) => InsertBefore(node, null);

    public DomNode InsertBefore(DomNode node, DomNode? referenceNode)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (referenceNode is not null && !ReferenceEquals(referenceNode.ParentNode, this))
            throw DomException.NotFound("The reference node is not a child of this node.");

        if (ReferenceEquals(node, referenceNode))
            return node;

        EnsureCanHaveChildren();
        EnsurePreInsertValidity(node, referenceNode);

        if (node is DomDocumentFragment fragment)
        {
            var fragmentChildren = fragment._children.ToArray();
            foreach (var child in fragmentChildren)
                InsertBefore(child, referenceNode);
            return fragment;
        }

        var targetDocument = this is DomDocument document ? document : OwnerDocument;
        if (!ReferenceEquals(node.OwnerDocument, targetDocument))
            targetDocument.AdoptNode(node);

        if (ReferenceEquals(node.ParentNode, this) && ReferenceEquals(node.NextSibling, referenceNode))
            return node;

        node.ParentNode?.RemoveChild(node);

        var index = referenceNode is null ? _children.Count : _children.IndexOf(referenceNode);
        var previousSibling = index > 0 ? _children[index - 1] : null;
        _children.Insert(index, node);
        node.ParentNode = this;

        if (IsConnected)
            targetDocument.IndexConnectedSubtree(node);

        MarkChanged();
        targetDocument.PublishMutation(new DomMutationRecord(
            DomMutationType.ChildList,
            this,
            AddedNodes: [node],
            PreviousSibling: previousSibling,
            NextSibling: referenceNode));
        return node;
    }

    public DomNode RemoveChild(DomNode child)
    {
        ArgumentNullException.ThrowIfNull(child);

        var index = _children.IndexOf(child);
        if (index < 0)
            throw DomException.NotFound("The node to remove is not a child of this node.");

        var previousSibling = index > 0 ? _children[index - 1] : null;
        var nextSibling = index + 1 < _children.Count ? _children[index + 1] : null;
        var document = this is DomDocument owner ? owner : OwnerDocument;

        if (child.IsConnected)
            document.UnindexConnectedSubtree(child);

        _children.RemoveAt(index);
        child.ParentNode = null;
        MarkChanged();
        document.PublishMutation(new DomMutationRecord(
            DomMutationType.ChildList,
            this,
            RemovedNodes: [child],
            PreviousSibling: previousSibling,
            NextSibling: nextSibling));
        return child;
    }

    public DomNode CloneNode(bool deep = false)
    {
        var clone = CloneShallow(OwnerDocument);
        if (deep)
        {
            foreach (var child in _children)
                clone.AppendChild(child.CloneNode(true));
        }

        return clone;
    }

    /// <summary>
    /// The DOM <c>Node.isEqualNode()</c> operation (§4.4): two nodes are equal when they have the
    /// same node type and type-specific identity, and equal children in order. DocumentType nodes
    /// compare name/publicId/systemId; character-data nodes compare their data; elements compare
    /// namespace + qualified name + attribute set (unordered, by namespace/local-name/value) + child
    /// list; document/fragment nodes compare their child list. This is the neutral tree algorithm the
    /// script bridge's <c>isEqualNode</c> binding delegates to.
    /// </summary>
    public bool IsEqualNode(DomNode? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        if (NodeType != other.NodeType)
            return false;

        switch (this)
        {
            case DomDocumentType thisDocType when other is DomDocumentType otherDocType:
                return string.Equals(thisDocType.Name, otherDocType.Name, StringComparison.Ordinal)
                    && string.Equals(thisDocType.PublicId, otherDocType.PublicId, StringComparison.Ordinal)
                    && string.Equals(thisDocType.SystemId, otherDocType.SystemId, StringComparison.Ordinal);

            case DomCharacterData thisData when other is DomCharacterData otherData:
                return string.Equals(thisData.Data, otherData.Data, StringComparison.Ordinal);

            case DomElement thisEl when other is DomElement otherEl:
                if (!string.Equals(thisEl.TagName, otherEl.TagName, StringComparison.Ordinal)
                    || !string.Equals(thisEl.NamespaceUri, otherEl.NamespaceUri, StringComparison.Ordinal)
                    || !AttributesEqual(thisEl, otherEl))
                {
                    return false;
                }
                break;
        }

        if (_children.Count != other._children.Count)
            return false;
        for (var index = 0; index < _children.Count; index++)
        {
            if (!_children[index].IsEqualNode(other._children[index]))
                return false;
        }
        return true;
    }

    private static bool AttributesEqual(DomElement first, DomElement second)
    {
        if (first.Attributes.Count != second.Attributes.Count)
            return false;
        foreach (var (key, attribute) in first.Attributes)
        {
            if (!second.Attributes.TryGetValue(key, out var other)
                || !string.Equals(attribute.Value, other.Value, StringComparison.Ordinal)
                || !string.Equals(attribute.QualifiedName, other.QualifiedName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return true;
    }

    public DomNode GetRootNode()
    {
        DomNode current = this;
        while (current.ParentNode is not null)
            current = current.ParentNode;
        return current;
    }

    public IEnumerable<DomNode> Descendants()
    {
        // Snapshot each level: Descendants() is enumerated lazily by long-lived
        // consumers (querySelectorAll, getElementsByTagName, tree walkers) while
        // script — or anchor/style reflection on the bridge — mutates _children
        // between MoveNext calls. Iterating the live list then throws "Collection
        // was modified" and aborts the walk (WPT issue #1143). Snapshotting is the
        // same defensive idiom the bridge uses for its Children walks.
        foreach (var child in _children.ToArray())
        {
            yield return child;
            foreach (var descendant in child.Descendants())
                yield return descendant;
        }
    }

    public IEnumerable<DomNode> InclusiveDescendants()
    {
        yield return this;
        foreach (var descendant in Descendants())
            yield return descendant;
    }

    public IEnumerable<DomNode> InclusiveAncestors()
    {
        for (DomNode? current = this; current is not null; current = current.ParentNode)
            yield return current;
    }

    public bool IsDescendantOf(DomNode ancestor)
    {
        ArgumentNullException.ThrowIfNull(ancestor);
        for (var current = ParentNode; current is not null; current = current.ParentNode)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
        }
        return false;
    }

    /// <summary>
    /// The nearest common inclusive ancestor of this node and <paramref name="other"/> — the deepest
    /// node that is an inclusive ancestor of both — or <c>null</c> when they belong to different trees
    /// (or <paramref name="other"/> is <c>null</c>). "Inclusive" means a node is its own ancestor, so
    /// if one node is an ancestor of the other, that node is returned. Unlike
    /// <see cref="DomRange.CommonAncestorContainer"/> (which requires two boundary points in one tree
    /// and throws otherwise), this is a null-tolerant node-level query for arbitrary node pairs.
    /// </summary>
    public DomNode? CommonAncestorWith(DomNode? other)
    {
        if (other is null)
            return null;
        var ancestors = InclusiveAncestors().ToHashSet();
        foreach (var ancestor in other.InclusiveAncestors())
        {
            if (ancestors.Contains(ancestor))
                return ancestor;
        }
        return null;
    }

    public void Normalize()
    {
        for (var index = 0; index < _children.Count;)
        {
            if (_children[index] is DomText text)
            {
                // DOM §4.4 "normalize": concatenate the node's contiguous exclusive Text siblings'
                // data and set the node's data ONCE, so a characterData observer sees a single
                // record per contiguous text run (not one per merged sibling). The following Text
                // siblings are then removed. (Previously `text.Data += next.Data` per sibling, which
                // published one CharacterData record for each merge step.)
                System.Text.StringBuilder? merged = null;
                while (index + 1 < _children.Count && _children[index + 1] is DomText next)
                {
                    merged ??= new System.Text.StringBuilder(text.Data);
                    merged.Append(next.Data);
                    RemoveChild(next);
                }

                if (merged is not null)
                    text.Data = merged.ToString();

                if (text.Data.Length == 0)
                {
                    RemoveChild(text);
                    continue;
                }
            }
            else
            {
                _children[index].Normalize();
            }

            index++;
        }
    }

    internal abstract DomNode CloneShallow(DomDocument ownerDocument);

    internal void SetOwnerDocument(DomDocument document)
    {
        _ownerDocument = document;
        foreach (var child in _children)
            child.SetOwnerDocument(document);
    }

    protected void MarkChanged()
    {
        for (DomNode? current = this; current is not null; current = current.ParentNode)
            current.TreeVersion++;
    }

    private void EnsureCanHaveChildren()
    {
        if (this is DomText or DomComment or DomDocumentType)
            throw DomException.HierarchyRequest($"{NodeType} nodes cannot have children.");
    }

    private void EnsurePreInsertValidity(DomNode node, DomNode? referenceNode)
    {
        if (ReferenceEquals(node, this) || InclusiveAncestors().Contains(node))
            throw DomException.HierarchyRequest("A node cannot be inserted into itself or one of its descendants.");

        if (node is DomDocument)
            throw DomException.HierarchyRequest("A document cannot be inserted into another node.");

        if (this is not DomDocument document)
            return;

        var candidates = node is DomDocumentFragment
            ? node.ChildNodes
            : [node];

        if (candidates.Any(static candidate => candidate is DomText))
            throw DomException.HierarchyRequest("Text nodes cannot be direct children of a document.");

        if (candidates.Count(static candidate => candidate is DomElement) > 1 ||
            candidates.Count(static candidate => candidate is DomDocumentType) > 1)
        {
            throw DomException.HierarchyRequest("A document can contain only one element and one document type.");
        }

        var replacedOrMoved = ReferenceEquals(node.ParentNode, document) ? node : null;
        var existingElement = document._children
            .FirstOrDefault(child => child is DomElement && !ReferenceEquals(child, replacedOrMoved));
        var existingDoctype = document._children
            .FirstOrDefault(child => child is DomDocumentType && !ReferenceEquals(child, replacedOrMoved));

        if (existingElement is not null && candidates.Any(static candidate => candidate is DomElement))
            throw DomException.HierarchyRequest("The document already has a document element.");

        if (existingDoctype is not null && candidates.Any(static candidate => candidate is DomDocumentType))
            throw DomException.HierarchyRequest("The document already has a document type.");

        if (candidates.Any(static candidate => candidate is DomDocumentType))
        {
            var elementIndex = document._children.FindIndex(static child => child is DomElement);
            var insertionIndex = referenceNode is null ? document._children.Count : document._children.IndexOf(referenceNode);
            if (elementIndex >= 0 && insertionIndex > elementIndex)
                throw DomException.HierarchyRequest("A document type must precede the document element.");
        }

        if (candidates.Any(static candidate => candidate is DomElement))
        {
            var doctypeIndex = document._children.FindIndex(static child => child is DomDocumentType);
            var insertionIndex = referenceNode is null ? document._children.Count : document._children.IndexOf(referenceNode);
            if (doctypeIndex >= 0 && insertionIndex <= doctypeIndex)
                throw DomException.HierarchyRequest("The document element must follow the document type.");
        }
    }

}

public static class DomNodeCollectionExtensions
{
    /// <summary>Index of <paramref name="target"/> in <paramref name="nodes"/> by reference equality,
    /// or -1 if absent. Public so bridge/host consumers can reuse the canonical reference-index scan
    /// (e.g. <c>DomBridge.ChildIndexOf</c>) instead of re-implementing the loop.</summary>
    public static int IndexOfReference(this IReadOnlyList<DomNode> nodes, DomNode target)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            if (ReferenceEquals(nodes[index], target))
                return index;
        }
        return -1;
    }
}
