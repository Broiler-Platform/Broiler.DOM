using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Broiler.Dom;

// Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s5.5; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
// Broiler-Falsified-If: ExtractContents or DeleteContents detaches from the tree a node that lies wholly outside the range's two boundary points
// Broiler-Human:        PENDING
public class DomRange : IDisposable
{
    private bool _disposed;
    private DomNode _startContainer;
    private int _startOffset;
    private DomNode _endContainer;
    private int _endOffset;

    private readonly bool _tracksMutations;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a range built with this constructor does not adjust its start offset when a child before that offset is removed from the start container
    // Broiler-Human:        PENDING
    public DomRange(DomNode root)
        : this(root, trackMutations: true)
    {
    }

    /// <summary>
    /// Constructs a range, optionally opting out of the document mutation subscription
    /// (DOM "removing steps"). A host that keeps its own weak range registry and drives
    /// boundary adjustment itself — so a script-abandoned range is not kept alive by the
    /// document's event — passes <paramref name="trackMutations"/> = <c>false</c> and calls
    /// <see cref="NotifyNodeRemoved"/> when it removes a child. The default (public) ctor
    /// tracks mutations, so a standalone range self-adjusts.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: with mutation tracking on, a boundary moved into a node of a document other than Root's keeps an offset past that container's child count after a child there is removed
    // Broiler-Human:        PENDING
    protected DomRange(DomNode root, bool trackMutations)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        _startContainer = root;
        _endContainer = root;
        _tracksMutations = trackMutations;
        if (trackMutations)
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
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a range whose start and end are the same container at equal offsets reports not collapsed, or two distinct containers report collapsed
    // Broiler-Human:        PENDING
    public bool Collapsed =>
        ReferenceEquals(_startContainer, _endContainer) && _startOffset == _endOffset;

    /// <summary>
    /// Sets the range's start boundary (DOM Standard §4.3 "set the start of a range").
    /// If the new start is after the current end, or is in a different tree, the range
    /// collapses onto the new start.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s5.5; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a start set after the current end, or in a different tree, leaves the end before the start instead of collapsing the range onto the new start
    // Broiler-Human:        PENDING
    public void SetStart(DomNode container, int offset)
    {
        ValidateBoundary(container, offset);

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
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s5.5; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an end set before the current start, or in a different tree, leaves the start after the end instead of collapsing the range onto the new end
    // Broiler-Human:        PENDING
    public void SetEnd(DomNode container, int offset)
    {
        ValidateBoundary(container, offset);

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
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a point whose container has a different root from the other point is reported not after, so SetStart leaves the range spanning two trees
    // Broiler-Human:        PENDING
    private static bool IsAfter(DomNode container, int offset, DomNode other, int otherOffset) =>
        !ReferenceEquals(container.GetRootNode(), other.GetRootNode()) ||
        CompareBoundaryPoints(container, offset, other, otherOffset) > 0;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a point whose container has a different root from the other point is reported not before, so SetEnd leaves the range spanning two trees
    // Broiler-Human:        PENDING
    private static bool IsBefore(DomNode container, int offset, DomNode other, int otherOffset) =>
        !ReferenceEquals(container.GetRootNode(), other.GetRootNode()) ||
        CompareBoundaryPoints(container, offset, other, otherOffset) < 0;

    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s5.2; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a point (A, i) compared with a point inside the child of A at index i returns 1 instead of -1
    // Broiler-Human:        PENDING
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

        var common = ResolveCommonAncestor(containerA, containerB);
        var childA = containerA;
        var childB = containerB;
        while (!ReferenceEquals(childA.ParentNode, common))
            childA = childA.ParentNode!;
        while (!ReferenceEquals(childB.ParentNode, common))
            childB = childB.ParentNode!;
        return common.ChildNodes.IndexOfReference(childA)
            .CompareTo(common.ChildNodes.IndexOfReference(childB));
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: after Root is adopted into another document, Dispose leaves the range subscribed to the original document's Mutated event, so its boundaries still move on removals there
    // Broiler-Human:        PENDING
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_tracksMutations)
            Root.OwnerDocument.Mutated -= OnMutation;
    }

    /// <summary>
    /// Applies the DOM "removing steps" for a child removed from <paramref name="parent"/>
    /// at <paramref name="index"/>. For hosts constructed with <c>trackMutations: false</c>
    /// that drive range adjustment from their own mutation path instead of the document event.
    /// </summary>
    public void NotifyNodeRemoved(DomNode parent, DomNode removed, int index) =>
        AdjustForRemoval(parent, removed, index);

    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s4.2.3; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: inserting a child before a boundary offset in that boundary's container, or shortening a Text node's data below a boundary offset in it, leaves that offset unchanged
    // Broiler-Human:        PENDING
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

    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s4.2.3; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a removal adjusts only the start boundary, leaving an end boundary inside the removed subtree pointing into the detached node
    // Broiler-Human:        PENDING
    private void AdjustForRemoval(DomNode parent, DomNode removed, int index)
    {
        AdjustBoundary(ref _startContainer, ref _startOffset, parent, removed, index);
        AdjustBoundary(ref _endContainer, ref _endOffset, parent, removed, index);
    }

    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s4.2.3; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a boundary in the parent at an offset equal to the removed child's index is decremented, or a boundary inside the removed subtree is not moved to the parent at that index
    // Broiler-Human:        PENDING
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

    // ---- Selection helpers (DOM Standard §4.5) ---------------------------------

    /// <summary>
    /// The deepest node that is an inclusive ancestor of both boundary points
    /// (DOM Standard <c>commonAncestorContainer</c>).
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: for boundaries in two sibling subtrees it returns a node that is not an inclusive ancestor of both containers, or a shallower shared ancestor than the deepest one
    // Broiler-Human:        PENDING
    public DomNode CommonAncestorContainer => ResolveCommonAncestor(_startContainer, _endContainer);

    /// <summary>Collapses the range onto one of its boundary points (DOM Standard §4.5 "collapse").</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: Collapse(true) moves the start onto the end instead of the end onto the start
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s5.5; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: selecting a node leaves boundaries other than (parent, index) and (parent, index + 1), or a parentless node is selected instead of raising InvalidNodeTypeError
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s5.5; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: selecting a Text node's contents ends at offset 0 instead of its data length, or a doctype's contents are selected instead of raising InvalidNodeTypeError
    // Broiler-Human:        PENDING
    public void SelectNodeContents(DomNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node is DomDocumentType)
            throw DomException.InvalidNodeType("A doctype's contents cannot be selected.");
        SetStart(node, 0);
        SetEnd(node, NodeLength(node));
    }

    /// <summary>
    /// Returns the text the range covers — the DOM Standard §4.5 stringifier
    /// (<c>Range.toString()</c>). It concatenates, in tree order, the selected portion of the
    /// start Text node, the full data of every Text node fully contained in the range, and the
    /// selected portion of the end Text node; non-Text nodes contribute nothing. A range within a
    /// single Text node returns that node's selected substring. An empty or collapsed range, or one
    /// whose boundaries touch only non-Text content, returns the empty string.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s5.5; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: when the end Text node's data has been shortened below the end offset, its text is appended twice, once as a contained node and once as the end slice
    // Broiler-Human:        PENDING
    public override string ToString()
    {
        // step 2: start and end are the same Text node — return the selected substring.
        if (ReferenceEquals(_startContainer, _endContainer) && _startContainer is DomText onlyText)
            return Slice(onlyText.Data, _startOffset, _endOffset);

        var sb = new StringBuilder();

        // step 3: the start node is a Text node — append from the start offset to its end.
        if (_startContainer is DomText startText)
            sb.Append(Slice(startText.Data, _startOffset, startText.Data.Length));

        // step 4: append, in tree order, the data of every Text node fully contained in the range.
        foreach (var node in ResolveCommonAncestor(_startContainer, _endContainer).InclusiveDescendants())
            if (node is DomText containedText && IsContained(node))
                sb.Append(containedText.Data);

        // step 5: the end node is a Text node — append from its start to the end offset.
        if (_endContainer is DomText endText)
            sb.Append(Slice(endText.Data, 0, _endOffset));

        return sb.ToString();
    }

    // Clamped half-open substring [from, to). The stringifier tolerates a boundary offset that a
    // later character-data mutation may have left past the node's current data length.
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a from offset beyond the data length throws instead of clamping to an empty string
    // Broiler-Human:        PENDING
    private static string Slice(string data, int from, int to)
    {
        from = Math.Clamp(from, 0, data.Length);
        to = Math.Clamp(to, from, data.Length);
        return data.Substring(from, to - from);
    }

    // ---- Content operations (DOM Standard §4.5) --------------------------------

    /// <summary>
    /// Removes the range's contents from the tree and returns them in a fragment
    /// (DOM Standard §4.5 "extract"). Partially selected text and element boundaries
    /// are split so the fragment holds exactly the selected content. The returned node
    /// is a <see cref="DomDocumentFragment"/> by default; a host can substitute its own
    /// fragment kind via <see cref="CreateResultFragment"/>.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s5.5; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: when the end Text node's data has been shortened below the end offset, the contained children are moved into a fragment that is never returned before string.Substring throws, so they leave the document
    // Broiler-Human:        PENDING
    public DomNode ExtractContents()
    {
        var fragment = CreateResultFragment();
        if (Collapsed)
            return fragment;

        var originalStartNode = _startContainer;
        var originalStartOffset = _startOffset;
        var originalEndNode = _endContainer;
        var originalEndOffset = _endOffset;

        // Same character-data container: split out the selected substring.
        if (ReferenceEquals(originalStartNode, originalEndNode) && originalStartNode is DomCharacterData sameData)
        {
            var clone = (DomCharacterData)CloneForRange(sameData, false);
            clone.Data = Substring(sameData, originalStartOffset, originalEndOffset - originalStartOffset);
            fragment.AppendChild(clone);
            sameData.Data = sameData.Data.Remove(originalStartOffset, originalEndOffset - originalStartOffset);
            // "replace data" collapses the range onto the start offset; OnMutation only
            // adjusts for ChildList removals, so do it explicitly here.
            _endContainer = sameData;
            _endOffset = originalStartOffset;
            return fragment;
        }

        var (firstPartial, lastPartial, containedChildren) = ClassifyContents(originalStartNode, originalEndNode);

        var (newNode, newOffset) = CollapsePointAfterRemoval(originalStartNode, originalStartOffset, originalEndNode);

        if (firstPartial is DomCharacterData startData)
        {
            // First partially contained child is character data — necessarily the start node.
            var clone = (DomCharacterData)CloneForRange(startData, false);
            clone.Data = Substring(startData, originalStartOffset, NodeLength(startData) - originalStartOffset);
            fragment.AppendChild(clone);
            startData.Data = startData.Data.Remove(originalStartOffset);
        }
        else if (firstPartial is not null)
        {
            var clone = CloneForRange(firstPartial, false);
            fragment.AppendChild(clone);
            using var subrange = CreateSubRange(Root);
            subrange.SetStart(originalStartNode, originalStartOffset);
            subrange.SetEnd(firstPartial, NodeLength(firstPartial));
            MoveChildrenInto(clone, subrange.ExtractContents());
        }

        foreach (var child in containedChildren)
            fragment.AppendChild(child);

        if (lastPartial is DomCharacterData endData)
        {
            var clone = (DomCharacterData)CloneForRange(endData, false);
            clone.Data = Substring(endData, 0, originalEndOffset);
            fragment.AppendChild(clone);
            endData.Data = endData.Data.Remove(0, originalEndOffset);
        }
        else if (lastPartial is not null)
        {
            var clone = CloneForRange(lastPartial, false);
            fragment.AppendChild(clone);
            using var subrange = CreateSubRange(Root);
            subrange.SetStart(lastPartial, 0);
            subrange.SetEnd(originalEndNode, originalEndOffset);
            MoveChildrenInto(clone, subrange.ExtractContents());
        }

        _startContainer = newNode;
        _startOffset = newOffset;
        _endContainer = newNode;
        _endOffset = newOffset;
        return fragment;
    }

    /// <summary>
    /// Returns a fragment holding a copy of the range's contents, leaving the tree
    /// unchanged (DOM Standard §4.5 "clone the contents"). The returned node is a
    /// <see cref="DomDocumentFragment"/> by default; a host can substitute its own
    /// fragment kind via <see cref="CreateResultFragment"/>.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s5.5; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: cloning a range changes the source tree, such as moving a contained child out of it or shortening a boundary Text node
    // Broiler-Human:        PENDING
    public DomNode CloneContents()
    {
        var fragment = CreateResultFragment();
        if (Collapsed)
            return fragment;

        var originalStartNode = _startContainer;
        var originalStartOffset = _startOffset;
        var originalEndNode = _endContainer;
        var originalEndOffset = _endOffset;

        if (ReferenceEquals(originalStartNode, originalEndNode) && originalStartNode is DomCharacterData sameData)
        {
            var clone = (DomCharacterData)CloneForRange(sameData, false);
            clone.Data = Substring(sameData, originalStartOffset, originalEndOffset - originalStartOffset);
            fragment.AppendChild(clone);
            return fragment;
        }

        var (firstPartial, lastPartial, containedChildren) = ClassifyContents(originalStartNode, originalEndNode);

        if (firstPartial is DomCharacterData startData)
        {
            var clone = (DomCharacterData)CloneForRange(startData, false);
            clone.Data = Substring(startData, originalStartOffset, NodeLength(startData) - originalStartOffset);
            fragment.AppendChild(clone);
        }
        else if (firstPartial is not null)
        {
            var clone = CloneForRange(firstPartial, false);
            fragment.AppendChild(clone);
            using var subrange = CreateSubRange(Root);
            subrange.SetStart(originalStartNode, originalStartOffset);
            subrange.SetEnd(firstPartial, NodeLength(firstPartial));
            MoveChildrenInto(clone, subrange.CloneContents());
        }

        foreach (var child in containedChildren)
            fragment.AppendChild(CloneForRange(child, true));

        if (lastPartial is DomCharacterData endData)
        {
            var clone = (DomCharacterData)CloneForRange(endData, false);
            clone.Data = Substring(endData, 0, originalEndOffset);
            fragment.AppendChild(clone);
        }
        else if (lastPartial is not null)
        {
            var clone = CloneForRange(lastPartial, false);
            fragment.AppendChild(clone);
            using var subrange = CreateSubRange(Root);
            subrange.SetStart(lastPartial, 0);
            subrange.SetEnd(originalEndNode, originalEndOffset);
            MoveChildrenInto(clone, subrange.CloneContents());
        }

        return fragment;
    }

    /// <summary>Removes the range's contents from the tree (DOM Standard §4.5 "delete the contents").</summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s5.5; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: a partially contained end Text node is detached from the tree when its data has been shortened below the end offset
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s5.5; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: inserting into a non-collapsed range whose end is (parent, k) with k above the insertion index leaves the end at k, so the range no longer covers its last selected child
    // Broiler-Human:        PENDING
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
        parent.EnsurePreInsertValidity(node, referenceNode);

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
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s5.5; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: a range that partially contains a Comment or an element is surrounded instead of raising InvalidStateError, or newParent keeps a child it had before the call
    // Broiler-Human:        PENDING
    public void SurroundContents(DomNode newParent)
    {
        ArgumentNullException.ThrowIfNull(newParent);

        // A range that partially contains a non-Text node cannot be surrounded. Only Text is
        // exempt (DOM Standard "Text node") — a Comment is a non-Text node, so a partially
        // contained comment boundary throws (e.g. Acid3 test 11).
        foreach (var node in CommonAncestorContainer.InclusiveDescendants())
        {
            if (node is not DomText && IsPartiallyContained(node))
                throw DomException.InvalidState("The range partially selects a non-text node.");
        }

        if (newParent is DomDocument or DomDocumentType or DomDocumentFragment)
            throw DomException.InvalidNodeType("The surrounding node must not be a document, doctype, or fragment.");

        var fragment = ExtractContents();

        while (newParent.FirstChild is { } child)
            newParent.RemoveChild(child);

        InsertNode(newParent);
        MoveChildrenInto(newParent, fragment);
        SelectNode(newParent);
    }

    // ---- Node-creation seams ---------------------------------------------------
    //
    // The content operations mint three kinds of node — a result fragment, node clones,
    // and split-off text — and may recurse through sub-ranges. Each is a protected virtual
    // so a host (e.g. the HtmlBridge) can substitute its own representation: a custom
    // fragment kind, a clone that carries host-side runtime state (form-control value,
    // scroll, dialog/shadow, live inline style), or node-registry bookkeeping. The defaults
    // are the plain canonical DOM behaviour.

    /// <summary>Creates the fragment that receives a content operation's result.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: the default result fragment already has children, so an extraction result carries nodes that were never in the range
    // Broiler-Human:        PENDING
    protected virtual DomNode CreateResultFragment() =>
        _startContainer.OwnerDocument.CreateDocumentFragment();

    /// <summary>Clones <paramref name="node"/> (shallow or deep) for a content operation.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: a deep clone shares a child node with the source subtree instead of copying it, or a shallow clone copies children
    // Broiler-Human:        PENDING
    protected virtual DomNode CloneForRange(DomNode node, bool deep) => node.CloneNode(deep);

    /// <summary>Creates a text node (used when splitting a text boundary in <see cref="InsertNode"/>).</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: the created Text node belongs to a document other than the start container's, so the split tail is adopted on insertion
    // Broiler-Human:        PENDING
    protected virtual DomText CreateTextForRange(string data) =>
        _startContainer.OwnerDocument.CreateTextNode(data);

    /// <summary>Creates a sub-range used to recurse into a partially contained boundary child.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: the default sub-range is rooted somewhere other than the given root or does not track removals while the outer operation mutates the tree
    // Broiler-Human:        PENDING
    protected virtual DomRange CreateSubRange(DomNode root) => new(root);

    // ---- §4.5 primitives -------------------------------------------------------

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a Comment or Text node's length is taken as its child count instead of its data length, or a doctype reports a length above 0
    // Broiler-Human:        PENDING
    private static int NodeLength(DomNode node) => node switch
    {
        DomCharacterData data => data.Data.Length,
        DomDocumentType => 0,
        _ => node.ChildNodes.Count,
    };

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a node is not reported as its own inclusive ancestor
    // Broiler-Human:        PENDING
    private static bool IsInclusiveAncestor(DomNode ancestor, DomNode node) =>
        ReferenceEquals(ancestor, node) || node.IsDescendantOf(ancestor);

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: the index of a different earlier child is returned because children are matched by value rather than by reference
    // Broiler-Human:        PENDING
    private static int IndexOf(DomNode node) =>
        node.ParentNode!.ChildNodes.IndexOfReference(node);

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an offset past the Text node's current data, left there by an earlier data change, throws ArgumentOutOfRangeException from string.Substring rather than a DomException
    // Broiler-Human:        PENDING
    private static string Substring(DomCharacterData data, int offset, int count) =>
        data.Data.Substring(offset, count);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: boundaries in two different trees return a node instead of raising WrongDocumentError
    // Broiler-Human:        PENDING
    private static DomNode ResolveCommonAncestor(DomNode start, DomNode end) =>
        start.CommonAncestorWith(end)
            ?? throw DomException.WrongDocument("Boundary points belong to different trees.");

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an offset of -1, or one greater than the container's length, is accepted, or a doctype is accepted as a container
    // Broiler-Human:        PENDING
    private static void ValidateBoundary(DomNode container, int offset)
    {
        ArgumentNullException.ThrowIfNull(container);
        if (container is DomDocumentType)
            throw DomException.InvalidNodeType("A range boundary cannot be a doctype.");
        if (offset < 0 || offset > NodeLength(container))
            throw DomException.IndexSize("Range offset must be within the container's length.");
    }

    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s5.5; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: when the start container is an inclusive ancestor of the end container a first partially contained child is still returned
    // Broiler-Human:        PENDING
    private (DomNode? FirstPartial, DomNode? LastPartial, List<DomNode> ContainedChildren)
        ClassifyContents(DomNode start, DomNode end)
    {
        var commonAncestor = ResolveCommonAncestor(start, end);
        var firstPartial = IsInclusiveAncestor(start, end)
            ? null
            : commonAncestor.ChildNodes.FirstOrDefault(IsPartiallyContained);
        var lastPartial = IsInclusiveAncestor(end, start)
            ? null
            : commonAncestor.ChildNodes.LastOrDefault(IsPartiallyContained);
        return (firstPartial, lastPartial, CollectContainedChildren(commonAncestor));
    }

    /// <summary>True when <paramref name="node"/> is fully contained by the range (DOM Standard "contained").</summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s5.5; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: the start container with a start offset of 0 is reported contained
    // Broiler-Human:        PENDING
    private bool IsContained(DomNode node)
    {
        if (!ReferenceEquals(node.GetRootNode(), _startContainer.GetRootNode()))
            return false;
        return CompareBoundaryPoints(node, 0, _startContainer, _startOffset) > 0
            && CompareBoundaryPoints(node, NodeLength(node), _endContainer, _endOffset) < 0;
    }

    /// <summary>True when <paramref name="node"/> is partially contained (DOM Standard "partially contained").</summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s5.5; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: the common ancestor container, an inclusive ancestor of both boundaries, is reported partially contained
    // Broiler-Human:        PENDING
    private bool IsPartiallyContained(DomNode node) =>
        IsInclusiveAncestor(node, _startContainer) != IsInclusiveAncestor(node, _endContainer);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: a doctype child fully inside the range is returned instead of raising HierarchyRequestError before any node is moved
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s5.5; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: after removing contents that start inside one child of the common ancestor, the range collapses somewhere other than just after that child in the common ancestor
    // Broiler-Human:        PENDING
    private (DomNode Node, int Offset) CollapsePointAfterRemoval(DomNode startNode, int startOffset, DomNode endNode)
    {
        if (IsInclusiveAncestor(startNode, endNode))
            return (startNode, startOffset);

        var reference = startNode;
        while (reference.ParentNode is { } parent && !IsInclusiveAncestor(parent, endNode))
            reference = parent;
        return (reference.ParentNode!, IndexOf(reference) + 1);
    }

    // Moves the children of a content-operation result fragment into <paramref name="target"/>.
    // Done child-by-child rather than AppendChild(fragment) so it works whether the fragment is
    // a canonical DomDocumentFragment (default) or a host-supplied container element (which has
    // no "append moves children" semantics).
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: children reach the target in an order different from their order in the fragment, or one is left behind in the fragment
    // Broiler-Human:        PENDING
    private static void MoveChildrenInto(DomNode target, DomNode fragment)
    {
        foreach (var child in fragment.ChildNodes.ToArray())
            target.AppendChild(child);
    }

    // DOM Standard §4.9 "split" of a text node at an offset, returning the new trailing node.
    // Uses the text seam so a host registers the split-off node in its registry, and applies the
    // spec's live-range "split" steps to THIS range so its boundaries follow the split.
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s4.11; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: after a split at offset k, a boundary in the split node at an offset above k stays on the original node instead of moving into the new node at offset minus k
    // Broiler-Human:        PENDING
    private DomText SplitText(DomText node, int offset)
    {
        var parent = node.ParentNode;
        var nodeIndex = parent is null ? -1 : IndexOf(node);
        var newNode = CreateTextForRange(node.Data.Substring(offset));
        parent?.InsertBefore(newNode, node.NextSibling);
        node.Data = node.Data.Remove(offset);

        // Range boundaries beyond the split point move into the new trailing node; a boundary
        // immediately after the split node in the parent shifts past the inserted node.
        if (ReferenceEquals(_startContainer, node) && _startOffset > offset)
        {
            _startContainer = newNode;
            _startOffset -= offset;
        }
        if (ReferenceEquals(_endContainer, node) && _endOffset > offset)
        {
            _endContainer = newNode;
            _endOffset -= offset;
        }
        if (parent is not null)
        {
            if (ReferenceEquals(_startContainer, parent) && _startOffset == nodeIndex + 1)
                _startOffset++;
            if (ReferenceEquals(_endContainer, parent) && _endOffset == nodeIndex + 1)
                _endOffset++;
        }
        return newNode;
    }
}
