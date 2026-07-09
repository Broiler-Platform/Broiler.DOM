using System;
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
}
