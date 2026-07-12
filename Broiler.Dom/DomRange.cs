using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.Dom;

public sealed class DomRange : IDisposable
{
    private bool _disposed;
    private DomNode _startContainer;
    private int _startOffset;
    private DomNode _endContainer;
    private int _endOffset;

    public DomRange(DomNode root)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        _startContainer = root;
        _endContainer = root;
        root.OwnerDocument.Mutated += OnMutation;
    }

    public DomNode Root { get; }

    /// <summary>The node in which the range starts.</summary>
    public DomNode StartContainer => _startContainer;

    /// <summary>The offset of the range's start within <see cref="StartContainer"/>.</summary>
    public int StartOffset => _startOffset;

    /// <summary>The node in which the range ends.</summary>
    public DomNode EndContainer => _endContainer;

    /// <summary>The offset of the range's end within <see cref="EndContainer"/>.</summary>
    public int EndOffset => _endOffset;

    /// <summary>Whether the range is collapsed — its two boundary points are equal.</summary>
    public bool Collapsed =>
        ReferenceEquals(_startContainer, _endContainer) && _startOffset == _endOffset;

    /// <summary>
    /// Sets the range's start boundary (DOM Standard §4.3 "set the start of a range").
    /// If the new start is after the current end, or is in a different tree, the range
    /// collapses onto the new start.
    /// </summary>
    public void SetStart(DomNode container, int offset)
    {
        ArgumentNullException.ThrowIfNull(container);
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Range offset must be non-negative.");

        var collapse = IsAfter(container, offset, _endContainer, _endOffset);
        _startContainer = container;
        _startOffset = offset;
        if (collapse)
        {
            _endContainer = container;
            _endOffset = offset;
        }
    }

    /// <summary>
    /// Sets the range's end boundary (DOM Standard §4.3 "set the end of a range").
    /// If the new end is before the current start, or is in a different tree, the range
    /// collapses onto the new end.
    /// </summary>
    public void SetEnd(DomNode container, int offset)
    {
        ArgumentNullException.ThrowIfNull(container);
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Range offset must be non-negative.");

        var collapse = IsBefore(container, offset, _startContainer, _startOffset);
        _endContainer = container;
        _endOffset = offset;
        if (collapse)
        {
            _startContainer = container;
            _startOffset = offset;
        }
    }

    // A boundary point in a different tree is treated as "after"/"before" so the
    // range collapses (DOM: "or root is not equal to this's root").
    private static bool IsAfter(DomNode container, int offset, DomNode other, int otherOffset) =>
        !ReferenceEquals(container.GetRootNode(), other.GetRootNode()) ||
        CompareBoundaryPoints(container, offset, other, otherOffset) > 0;

    private static bool IsBefore(DomNode container, int offset, DomNode other, int otherOffset) =>
        !ReferenceEquals(container.GetRootNode(), other.GetRootNode()) ||
        CompareBoundaryPoints(container, offset, other, otherOffset) < 0;

    public static int CompareBoundaryPoints(
        DomNode containerA,
        int offsetA,
        DomNode containerB,
        int offsetB)
    {
        if (!ReferenceEquals(containerA.GetRootNode(), containerB.GetRootNode()))
            throw DomException.WrongDocument("Boundary points belong to different trees.");
        if (ReferenceEquals(containerA, containerB))
            return offsetA.CompareTo(offsetB);

        if (containerB.IsDescendantOf(containerA))
        {
            var child = containerB;
            while (!ReferenceEquals(child.ParentNode, containerA))
                child = child.ParentNode!;
            return offsetA <= containerA.ChildNodes.IndexOfReference(child) ? -1 : 1;
        }

        if (containerA.IsDescendantOf(containerB))
            return -CompareBoundaryPoints(containerB, offsetB, containerA, offsetA);

        var common = FindCommonAncestor(containerA, containerB);
        var childA = containerA;
        var childB = containerB;
        while (!ReferenceEquals(childA.ParentNode, common))
            childA = childA.ParentNode!;
        while (!ReferenceEquals(childB.ParentNode, common))
            childB = childB.ParentNode!;
        return common.ChildNodes.IndexOfReference(childA)
            .CompareTo(common.ChildNodes.IndexOfReference(childB));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Root.OwnerDocument.Mutated -= OnMutation;
    }

    private void OnMutation(DomMutationRecord mutation)
    {
        if (mutation.Type != DomMutationType.ChildList ||
            mutation.RemovedNodes is not { Count: > 0 })
        {
            return;
        }

        var index = mutation.PreviousSibling is null
            ? 0
            : mutation.Target.ChildNodes.IndexOfReference(mutation.PreviousSibling) + 1;
        foreach (var removed in mutation.RemovedNodes)
            AdjustForRemoval(mutation.Target, removed, index);
    }

    private void AdjustForRemoval(DomNode parent, DomNode removed, int index)
    {
        AdjustBoundary(ref _startContainer, ref _startOffset, parent, removed, index);
        AdjustBoundary(ref _endContainer, ref _endOffset, parent, removed, index);
    }

    private static void AdjustBoundary(ref DomNode container, ref int offset, DomNode parent, DomNode removed, int index)
    {
        if (ReferenceEquals(container, removed) || container.IsDescendantOf(removed))
        {
            container = parent;
            offset = index;
        }
        else if (ReferenceEquals(container, parent) && offset > index)
        {
            offset--;
        }
    }

    private static DomNode FindCommonAncestor(DomNode first, DomNode second)
    {
        var ancestors = first.InclusiveAncestors().ToHashSet();
        return second.InclusiveAncestors().First(ancestors.Contains);
    }

    // ---- Selection helpers (DOM Standard §4.5) ---------------------------------

    /// <summary>
    /// The deepest node that is an inclusive ancestor of both boundary points
    /// (DOM Standard <c>commonAncestorContainer</c>).
    /// </summary>
    public DomNode CommonAncestorContainer
    {
        get
        {
            var container = _startContainer;
            while (!IsInclusiveAncestor(container, _endContainer))
                container = container.ParentNode!;
            return container;
        }
    }

    /// <summary>Collapses the range onto one of its boundary points (DOM Standard §4.5 "collapse").</summary>
    public void Collapse(bool toStart)
    {
        if (toStart)
        {
            _endContainer = _startContainer;
            _endOffset = _startOffset;
        }
        else
        {
            _startContainer = _endContainer;
            _startOffset = _endOffset;
        }
    }

    /// <summary>Selects <paramref name="node"/> — its boundaries become the points around it (DOM Standard §4.5 "select").</summary>
    public void SelectNode(DomNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var parent = node.ParentNode
            ?? throw DomException.InvalidNodeType("The node to select has no parent.");
        var index = IndexOf(node);
        SetStart(parent, index);
        SetEnd(parent, index + 1);
    }

    /// <summary>Selects the contents of <paramref name="node"/> (DOM Standard §4.5 "select node contents").</summary>
    public void SelectNodeContents(DomNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node is DomDocumentType)
            throw DomException.InvalidNodeType("A doctype's contents cannot be selected.");
        SetStart(node, 0);
        SetEnd(node, NodeLength(node));
    }

    // ---- Content operations (DOM Standard §4.5) --------------------------------

    /// <summary>
    /// Removes the range's contents from the tree and returns them in a document
    /// fragment (DOM Standard §4.5 "extract"). Partially selected text and element
    /// boundaries are split so the fragment holds exactly the selected content.
    /// </summary>
    public DomDocumentFragment ExtractContents()
    {
        var fragment = _startContainer.OwnerDocument.CreateDocumentFragment();
        if (Collapsed)
            return fragment;

        var originalStartNode = _startContainer;
        var originalStartOffset = _startOffset;
        var originalEndNode = _endContainer;
        var originalEndOffset = _endOffset;

        // Same character-data container: split out the selected substring.
        if (ReferenceEquals(originalStartNode, originalEndNode) && originalStartNode is DomCharacterData sameData)
        {
            var clone = (DomCharacterData)sameData.CloneNode(false);
            clone.Data = Substring(sameData, originalStartOffset, originalEndOffset - originalStartOffset);
            fragment.AppendChild(clone);
            sameData.Data = sameData.Data.Remove(originalStartOffset, originalEndOffset - originalStartOffset);
            // "replace data" collapses the range onto the start offset; OnMutation only
            // adjusts for ChildList removals, so do it explicitly here.
            _endContainer = sameData;
            _endOffset = originalStartOffset;
            return fragment;
        }

        var commonAncestor = ResolveCommonAncestor(originalStartNode, originalEndNode);
        var firstPartial = IsInclusiveAncestor(originalStartNode, originalEndNode)
            ? null
            : commonAncestor.ChildNodes.FirstOrDefault(IsPartiallyContained);
        var lastPartial = IsInclusiveAncestor(originalEndNode, originalStartNode)
            ? null
            : commonAncestor.ChildNodes.LastOrDefault(IsPartiallyContained);
        var containedChildren = CollectContainedChildren(commonAncestor);

        var (newNode, newOffset) = CollapsePointAfterRemoval(originalStartNode, originalStartOffset, originalEndNode);

        if (firstPartial is DomCharacterData startData)
        {
            // First partially contained child is character data — necessarily the start node.
            var clone = (DomCharacterData)startData.CloneNode(false);
            clone.Data = Substring(startData, originalStartOffset, NodeLength(startData) - originalStartOffset);
            fragment.AppendChild(clone);
            startData.Data = startData.Data.Remove(originalStartOffset);
        }
        else if (firstPartial is not null)
        {
            var clone = firstPartial.CloneNode(false);
            fragment.AppendChild(clone);
            using var subrange = new DomRange(Root);
            subrange.SetStart(originalStartNode, originalStartOffset);
            subrange.SetEnd(firstPartial, NodeLength(firstPartial));
            clone.AppendChild(subrange.ExtractContents());
        }

        foreach (var child in containedChildren)
            fragment.AppendChild(child);

        if (lastPartial is DomCharacterData endData)
        {
            var clone = (DomCharacterData)endData.CloneNode(false);
            clone.Data = Substring(endData, 0, originalEndOffset);
            fragment.AppendChild(clone);
            endData.Data = endData.Data.Remove(0, originalEndOffset);
        }
        else if (lastPartial is not null)
        {
            var clone = lastPartial.CloneNode(false);
            fragment.AppendChild(clone);
            using var subrange = new DomRange(Root);
            subrange.SetStart(lastPartial, 0);
            subrange.SetEnd(originalEndNode, originalEndOffset);
            clone.AppendChild(subrange.ExtractContents());
        }

        _startContainer = newNode;
        _startOffset = newOffset;
        _endContainer = newNode;
        _endOffset = newOffset;
        return fragment;
    }

    /// <summary>
    /// Returns a document fragment holding a copy of the range's contents, leaving
    /// the tree unchanged (DOM Standard §4.5 "clone the contents").
    /// </summary>
    public DomDocumentFragment CloneContents()
    {
        var fragment = _startContainer.OwnerDocument.CreateDocumentFragment();
        if (Collapsed)
            return fragment;

        var originalStartNode = _startContainer;
        var originalStartOffset = _startOffset;
        var originalEndNode = _endContainer;
        var originalEndOffset = _endOffset;

        if (ReferenceEquals(originalStartNode, originalEndNode) && originalStartNode is DomCharacterData sameData)
        {
            var clone = (DomCharacterData)sameData.CloneNode(false);
            clone.Data = Substring(sameData, originalStartOffset, originalEndOffset - originalStartOffset);
            fragment.AppendChild(clone);
            return fragment;
        }

        var commonAncestor = ResolveCommonAncestor(originalStartNode, originalEndNode);
        var firstPartial = IsInclusiveAncestor(originalStartNode, originalEndNode)
            ? null
            : commonAncestor.ChildNodes.FirstOrDefault(IsPartiallyContained);
        var lastPartial = IsInclusiveAncestor(originalEndNode, originalStartNode)
            ? null
            : commonAncestor.ChildNodes.LastOrDefault(IsPartiallyContained);
        var containedChildren = CollectContainedChildren(commonAncestor);

        if (firstPartial is DomCharacterData startData)
        {
            var clone = (DomCharacterData)startData.CloneNode(false);
            clone.Data = Substring(startData, originalStartOffset, NodeLength(startData) - originalStartOffset);
            fragment.AppendChild(clone);
        }
        else if (firstPartial is not null)
        {
            var clone = firstPartial.CloneNode(false);
            fragment.AppendChild(clone);
            using var subrange = new DomRange(Root);
            subrange.SetStart(originalStartNode, originalStartOffset);
            subrange.SetEnd(firstPartial, NodeLength(firstPartial));
            clone.AppendChild(subrange.CloneContents());
        }

        foreach (var child in containedChildren)
            fragment.AppendChild(child.CloneNode(true));

        if (lastPartial is DomCharacterData endData)
        {
            var clone = (DomCharacterData)endData.CloneNode(false);
            clone.Data = Substring(endData, 0, originalEndOffset);
            fragment.AppendChild(clone);
        }
        else if (lastPartial is not null)
        {
            var clone = lastPartial.CloneNode(false);
            fragment.AppendChild(clone);
            using var subrange = new DomRange(Root);
            subrange.SetStart(lastPartial, 0);
            subrange.SetEnd(originalEndNode, originalEndOffset);
            clone.AppendChild(subrange.CloneContents());
        }

        return fragment;
    }

    /// <summary>Removes the range's contents from the tree (DOM Standard §4.5 "delete the contents").</summary>
    public void DeleteContents()
    {
        if (Collapsed)
            return;

        var originalStartNode = _startContainer;
        var originalStartOffset = _startOffset;
        var originalEndNode = _endContainer;
        var originalEndOffset = _endOffset;

        if (ReferenceEquals(originalStartNode, originalEndNode) && originalStartNode is DomCharacterData sameData)
        {
            sameData.Data = sameData.Data.Remove(originalStartOffset, originalEndOffset - originalStartOffset);
            _endContainer = sameData;
            _endOffset = originalStartOffset;
            return;
        }

        // Nodes to remove: those contained in the range whose parent is not itself contained.
        var nodesToRemove = new List<DomNode>();
        foreach (var node in ResolveCommonAncestor(originalStartNode, originalEndNode).InclusiveDescendants())
        {
            if (IsContained(node) && !(node.ParentNode is { } parent && IsContained(parent)))
                nodesToRemove.Add(node);
        }

        var (newNode, newOffset) = CollapsePointAfterRemoval(originalStartNode, originalStartOffset, originalEndNode);

        if (originalStartNode is DomCharacterData startData)
            startData.Data = startData.Data.Remove(originalStartOffset);

        foreach (var node in nodesToRemove)
            node.ParentNode?.RemoveChild(node);

        if (originalEndNode is DomCharacterData endData)
            endData.Data = endData.Data.Remove(0, originalEndOffset);

        _startContainer = newNode;
        _startOffset = newOffset;
        _endContainer = newNode;
        _endOffset = newOffset;
    }

    /// <summary>
    /// Inserts <paramref name="node"/> at the range's start boundary (DOM Standard §4.5
    /// "insert"). A text start boundary is split so the node lands at the offset.
    /// </summary>
    public void InsertNode(DomNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var startNode = _startContainer;
        if (startNode is DomComment ||
            (startNode is DomText && startNode.ParentNode is null) ||
            ReferenceEquals(node, startNode))
        {
            throw DomException.HierarchyRequest("The node cannot be inserted at the range's start.");
        }

        DomNode? referenceNode = startNode is DomText
            ? startNode
            : (_startOffset < startNode.ChildNodes.Count ? startNode.ChildNodes[_startOffset] : null);
        var parent = referenceNode is null ? startNode : referenceNode.ParentNode!;

        // Ensure pre-insert validity (DOM §4.2) before the text split mutates the tree,
        // so a rejected insert leaves no split node behind.
        if (parent.InclusiveAncestors().Contains(node))
            throw DomException.HierarchyRequest("A node cannot be inserted into itself or one of its descendants.");

        if (startNode is DomText startText)
            referenceNode = SplitText(startText, _startOffset);

        if (ReferenceEquals(node, referenceNode))
            referenceNode = referenceNode.NextSibling;

        node.ParentNode?.RemoveChild(node);

        var newOffset = referenceNode is null ? NodeLength(parent) : IndexOf(referenceNode);
        newOffset += node is DomDocumentFragment ? NodeLength(node) : 1;

        parent.InsertBefore(node, referenceNode);

        if (Collapsed)
        {
            _endContainer = parent;
            _endOffset = newOffset;
        }
    }

    /// <summary>
    /// Extracts the range's contents, wraps them in <paramref name="newParent"/>, and
    /// re-inserts them (DOM Standard §4.5 "surround contents"). Throws if the range
    /// partially selects a non-text node.
    /// </summary>
    public void SurroundContents(DomNode newParent)
    {
        ArgumentNullException.ThrowIfNull(newParent);

        // A range that partially contains a non-CharacterData node cannot be surrounded.
        foreach (var node in CommonAncestorContainer.InclusiveDescendants())
        {
            if (node is not DomCharacterData && IsPartiallyContained(node))
                throw DomException.InvalidState("The range partially selects a non-text node.");
        }

        if (newParent is DomDocument or DomDocumentType or DomDocumentFragment)
            throw DomException.InvalidNodeType("The surrounding node must not be a document, doctype, or fragment.");

        var fragment = ExtractContents();

        while (newParent.FirstChild is { } child)
            newParent.RemoveChild(child);

        InsertNode(newParent);
        newParent.AppendChild(fragment);
        SelectNode(newParent);
    }

    // ---- §4.5 primitives -------------------------------------------------------

    private static int NodeLength(DomNode node) => node switch
    {
        DomCharacterData data => data.Data.Length,
        DomDocumentType => 0,
        _ => node.ChildNodes.Count,
    };

    private static bool IsInclusiveAncestor(DomNode ancestor, DomNode node) =>
        ReferenceEquals(ancestor, node) || node.IsDescendantOf(ancestor);

    private static int IndexOf(DomNode node) =>
        node.ParentNode!.ChildNodes.IndexOfReference(node);

    private static string Substring(DomCharacterData data, int offset, int count) =>
        data.Data.Substring(offset, count);

    private DomNode ResolveCommonAncestor(DomNode start, DomNode end)
    {
        var container = start;
        while (!IsInclusiveAncestor(container, end))
            container = container.ParentNode!;
        return container;
    }

    /// <summary>True when <paramref name="node"/> is fully contained by the range (DOM Standard "contained").</summary>
    private bool IsContained(DomNode node)
    {
        if (!ReferenceEquals(node.GetRootNode(), _startContainer.GetRootNode()))
            return false;
        return CompareBoundaryPoints(node, 0, _startContainer, _startOffset) > 0
            && CompareBoundaryPoints(node, NodeLength(node), _endContainer, _endOffset) < 0;
    }

    /// <summary>True when <paramref name="node"/> is partially contained (DOM Standard "partially contained").</summary>
    private bool IsPartiallyContained(DomNode node) =>
        IsInclusiveAncestor(node, _startContainer) != IsInclusiveAncestor(node, _endContainer);

    private List<DomNode> CollectContainedChildren(DomNode commonAncestor)
    {
        var children = new List<DomNode>();
        foreach (var child in commonAncestor.ChildNodes)
        {
            if (!IsContained(child))
                continue;
            if (child is DomDocumentType)
                throw DomException.HierarchyRequest("A doctype cannot be extracted from a range.");
            children.Add(child);
        }
        return children;
    }

    // The point the range collapses onto after its contents are removed (extract/delete).
    private (DomNode Node, int Offset) CollapsePointAfterRemoval(DomNode startNode, int startOffset, DomNode endNode)
    {
        if (IsInclusiveAncestor(startNode, endNode))
            return (startNode, startOffset);

        var reference = startNode;
        while (reference.ParentNode is { } parent && !IsInclusiveAncestor(parent, endNode))
            reference = parent;
        return (reference.ParentNode!, IndexOf(reference) + 1);
    }

    // DOM Standard §4.9 "split" of a text node at an offset, returning the new trailing node.
    private static DomText SplitText(DomText node, int offset)
    {
        var newData = node.Data.Substring(offset);
        var newNode = node.OwnerDocument.CreateTextNode(newData);
        node.ParentNode?.InsertBefore(newNode, node.NextSibling);
        node.Data = node.Data.Remove(offset);
        return newNode;
    }
}
