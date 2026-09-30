using System;
using System.Collections.Generic;
using System.Linq;


namespace Broiler.Dom;

// Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s6.3; IP=None; Security=Low; Resources=0; Fingerprint=TBF
// Broiler-Falsified-If: a member's bit differs from the DOM Standard's NodeFilter constant of the same name (Element 0x1, Text 0x4, Comment 0x80, Document 0x100, DocumentFragment 0x400), so a script-supplied mask selects another node kind
// Broiler-Human:        PENDING
[Flags]
public enum DomWhatToShow : uint
{
    None = 0,
    Element = 0x1,
    Text = 0x4,
    Comment = 0x80,
    Document = 0x100,
    DocumentFragment = 0x400,
    All = uint.MaxValue
}

// Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s6.3; IP=None; Security=Low; Resources=0; Fingerprint=TBF
// Broiler-Falsified-If: a member's value differs from the NodeFilter constants 1 (accept), 2 (reject) and 3 (skip), so a numeric filter return cast by the script binding is read as another verdict
// Broiler-Human:        PENDING
public enum DomFilterResult
{
    Accept = 1,
    Reject = 2,
    Skip = 3
}

// Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s6.2; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
// Broiler-Falsified-If: nextSibling() on a walker whose current node has been removed from its tree throws NullReferenceException, because the null parent reaches Evaluate, instead of returning null
// Broiler-Human:        PENDING
public sealed class DomTreeWalker
{
    private readonly Func<DomNode, DomFilterResult>? _filter;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a null root is accepted and the first traversal call throws NullReferenceException instead of the constructor throwing ArgumentNullException
    // Broiler-Human:        PENDING
    public DomTreeWalker(DomNode root, DomWhatToShow whatToShow = DomWhatToShow.All, Func<DomNode, DomFilterResult>? filter = null)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        CurrentNode = root;
        WhatToShow = whatToShow;
        _filter = filter;
    }

    public DomNode Root { get; }

    public DomWhatToShow WhatToShow { get; }

    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s6.2; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: assigning a node that is neither the root nor its descendant throws NotFound, although the DOM Standard's currentNode setter accepts any node
    // Broiler-Human:        PENDING
    public DomNode CurrentNode
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!ReferenceEquals(value, Root) && !value.IsDescendantOf(Root))
                throw DomException.NotFound("The current node must be within the TreeWalker root.");
            field = value;
        }
    }

    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s6.2; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: starting from a current node inside the root, the walk returns or makes current an accepted node above the root instead of stopping at the root
    // Broiler-Human:        PENDING
    public DomNode? ParentNode()
    {
        if (ReferenceEquals(CurrentNode, Root))
            return null;

        for (var node = CurrentNode.ParentNode; node is not null; node = node.ParentNode)
        {
            if (Evaluate(node) == DomFilterResult.Accept)
                return CurrentNode = node;
            if (ReferenceEquals(node, Root))
                break;
        }
        return null;
    }

    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s6; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a filter that calls this walker's nextNode() re-entrantly is run to completion instead of being refused with InvalidStateError, so the nested call moves CurrentNode mid-traversal
    // Broiler-Human:        PENDING
    private DomFilterResult Evaluate(DomNode node)
    {
        if ((WhatToShow & ShowFlag(node.NodeType)) == 0)
            return DomFilterResult.Skip;
        return _filter?.Invoke(node) ?? DomFilterResult.Accept;
    }

    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s6; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a DocumentType node maps to no flag, so a traversal with DomWhatToShow.All never returns the doctype although node type 10 corresponds to bit 0x200
    // Broiler-Human:        PENDING
    internal static DomWhatToShow ShowFlag(DomNodeType nodeType) => nodeType switch
    {
        DomNodeType.Element => DomWhatToShow.Element,
        DomNodeType.Text => DomWhatToShow.Text,
        DomNodeType.Comment => DomWhatToShow.Comment,
        DomNodeType.Document => DomWhatToShow.Document,
        DomNodeType.DocumentFragment => DomWhatToShow.DocumentFragment,
        _ => DomWhatToShow.None
    };

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: the call passes forward: false, so firstChild() returns the last acceptable child instead of the first
    // Broiler-Human:        PENDING
    public DomNode? FirstChild() => TraverseChildren(forward: true);

    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s6.2; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: when a skipped child's descendants are all skipped or rejected, an acceptable later sibling is not found and null is returned, because the walk never climbs back out of the skipped subtree
    // Broiler-Human:        PENDING
    private DomNode? TraverseChildren(bool forward)
    {
        var node = forward ? CurrentNode.FirstChild : CurrentNode.LastChild;
        while (node is not null)
        {
            var result = Evaluate(node);
            if (result == DomFilterResult.Accept)
                return CurrentNode = node;
            if (result == DomFilterResult.Skip)
            {
                var child = forward ? node.FirstChild : node.LastChild;
                if (child is not null)
                {
                    node = child;
                    continue;
                }
            }
            node = forward ? node.NextSibling : node.PreviousSibling;
        }
        return null;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: the call passes forward: true, so lastChild() returns the first acceptable child instead of the last
    // Broiler-Human:        PENDING
    public DomNode? LastChild() => TraverseChildren(forward: false);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: the call passes forward: false, so nextSibling() walks toward preceding siblings instead of following ones
    // Broiler-Human:        PENDING
    public DomNode? NextSibling() => TraverseSiblings(forward: true);

    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s6.2; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: when a skipped sibling's descendants are all skipped or rejected, the siblings after it are never visited and null is returned, because the inner loop never climbs back out of the skipped subtree
    // Broiler-Human:        PENDING
    private DomNode? TraverseSiblings(bool forward)
    {
        var node = CurrentNode;
        while (!ReferenceEquals(node, Root))
        {
            var sibling = forward ? node.NextSibling : node.PreviousSibling;
            while (sibling is not null)
            {
                var result = Evaluate(sibling);
                if (result == DomFilterResult.Accept)
                    return CurrentNode = sibling;
                if (result == DomFilterResult.Skip)
                {
                    var child = forward ? sibling.FirstChild : sibling.LastChild;
                    if (child is not null)
                    {
                        sibling = child;
                        continue;
                    }
                }
                sibling = forward ? sibling.NextSibling : sibling.PreviousSibling;
            }

            node = node.ParentNode!;
            if (Evaluate(node) == DomFilterResult.Accept)
                return null;
        }
        return null;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: the call passes forward: true, so previousSibling() walks toward following siblings instead of preceding ones
    // Broiler-Human:        PENDING
    public DomNode? PreviousSibling() => TraverseSiblings(forward: false);

    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s6.2; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: when the filter rejects the current node (including the root of a fresh walker), nextNode() skips that node's descendants and can return null, where the DOM Standard starts from an accept result and descends into them
    // Broiler-Human:        PENDING
    public DomNode? NextNode()
    {
        var node = CurrentNode;
        while (true)
        {
            if (Evaluate(node) != DomFilterResult.Reject && node.FirstChild is not null)
            {
                node = node.FirstChild;
            }
            else
            {
                while (node.NextSibling is null)
                {
                    if (node.ParentNode is null || ReferenceEquals(node, Root))
                        return null;
                    node = node.ParentNode;
                }
                if (ReferenceEquals(node, Root))
                    return null;
                node = node.NextSibling;
            }

            var result = Evaluate(node);
            if (result == DomFilterResult.Accept)
                return CurrentNode = node;
        }
    }

    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s6.2; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: starting from a current node inside the root, the walk returns a node that lies outside the root instead of stopping at the root
    // Broiler-Human:        PENDING
    public DomNode? PreviousNode()
    {
        var node = CurrentNode;
        while (!ReferenceEquals(node, Root))
        {
            if (node.PreviousSibling is not null)
            {
                node = node.PreviousSibling;
                while (Evaluate(node) != DomFilterResult.Reject && node.LastChild is not null)
                    node = node.LastChild;
            }
            else if (node.ParentNode is not null)
            {
                node = node.ParentNode;
            }
            else
            {
                return null;
            }

            if (Evaluate(node) == DomFilterResult.Accept)
                return CurrentNode = node;
        }
        return null;
    }
}

// Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s6.1; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
// Broiler-Falsified-If: after the reference node is removed while its previous sibling has children, the next nextNode() returns those children again although they were already returned
// Broiler-Human:        PENDING
public sealed class DomNodeIterator : IDisposable
{
    private int _lastKnownIndex = -1;
    private readonly Func<DomNode, DomFilterResult>? _filter;
    private readonly Dictionary<DomNode, int> _snapshotIndices = new(ReferenceEqualityComparer.Instance);
    private DomNode[]? _snapshot;
    private ulong _snapshotVersion;
    private bool _disposed;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: an iterator that is never disposed stays reachable from its document's Mutated event, with its snapshot array and index map, for the lifetime of the document
    // Broiler-Human:        PENDING
    public DomNodeIterator(DomNode root, DomWhatToShow whatToShow = DomWhatToShow.All, Func<DomNode, DomFilterResult>? filter = null)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        WhatToShow = whatToShow;
        ReferenceNode = root;
        PointerBeforeReferenceNode = true;
        root.OwnerDocument.Mutated += OnMutation;
        _filter = filter;
    }

    public DomNode Root { get; }

    public DomWhatToShow WhatToShow { get; }

    public DomNode ReferenceNode { get; private set; }

    public bool PointerBeforeReferenceNode { get; private set; }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: after the root is adopted into another document, Dispose() unsubscribes from the new owner document and leaves OnMutation attached to the document it subscribed to at construction
    // Broiler-Human:        PENDING
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Root.OwnerDocument.Mutated -= OnMutation;
        _snapshot = null;
        _snapshotIndices.Clear();
    }

    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s6.1; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: when the removed reference node's previous sibling has children, the reference becomes that sibling itself instead of its last inclusive descendant
    // Broiler-Human:        PENDING
    private void OnMutation(DomMutationRecord mutation)
    {
        if (mutation.Type != DomMutationType.ChildList ||
            mutation.RemovedNodes is not { Count: > 0 })
        {
            return;
        }

        foreach (var removed in mutation.RemovedNodes)
        {
            if (!ReferenceEquals(ReferenceNode, removed) && !ReferenceNode.IsDescendantOf(removed))
                continue;
            if (!ReferenceEquals(mutation.Target, Root) && !mutation.Target.IsDescendantOf(Root))
                continue;

            if (PointerBeforeReferenceNode && mutation.NextSibling is not null)
            {
                ReferenceNode = mutation.NextSibling;
                return;
            }

            ReferenceNode = mutation.PreviousSibling ?? mutation.Target;
            PointerBeforeReferenceNode = false;
        }
    }

    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s6.1; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: with no mutation between calls, nextNode() returns a node that precedes the reference node in tree order, or returns the reference node again while the pointer is after it
    // Broiler-Human:        PENDING
    public DomNode? NextNode()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var nodes = GetSnapshot();
        var index = SnapshotIndexOf(ReferenceNode);
        if (index < 0)
            index = _lastKnownIndex >= 0 ? Math.Min(_lastKnownIndex, nodes.Length - 1) : -1;
        var start = PointerBeforeReferenceNode ? index : index + 1;
        for (var i = Math.Max(start, 0); i < nodes.Length;)
        {
            var candidate = nodes[i];
            _lastKnownIndex = i;
            var result = Evaluate(candidate);
            nodes = GetSnapshot();
            if (result == DomFilterResult.Accept)
            {
                ReferenceNode = candidate;
                PointerBeforeReferenceNode = false;
                var currentIndex = SnapshotIndexOf(candidate);
                _lastKnownIndex = currentIndex >= 0 ? currentIndex : i;
                return ReferenceNode;
            }

            var newIndex = SnapshotIndexOf(candidate);
            if (newIndex < 0)
                continue;
            i = newIndex + 1;
        }
        return null;
    }

    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s6.1; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: with no mutation between calls, previousNode() returns a node that follows the reference node in tree order, or returns the reference node again while the pointer is before it
    // Broiler-Human:        PENDING
    public DomNode? PreviousNode()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var nodes = GetSnapshot();
        var index = SnapshotIndexOf(ReferenceNode);
        if (index < 0)
            index = _lastKnownIndex >= 0 ? Math.Min(_lastKnownIndex, nodes.Length) : 0;
        var start = PointerBeforeReferenceNode ? index - 1 : index;
        for (var i = Math.Min(start, nodes.Length - 1); i >= 0;)
        {
            var candidate = nodes[i];
            _lastKnownIndex = i;
            var result = Evaluate(candidate);
            nodes = GetSnapshot();
            if (result == DomFilterResult.Accept)
            {
                ReferenceNode = candidate;
                PointerBeforeReferenceNode = true;
                var currentIndex = SnapshotIndexOf(candidate);
                _lastKnownIndex = currentIndex >= 0 ? currentIndex : i;
                return ReferenceNode;
            }

            var newIndex = SnapshotIndexOf(candidate);
            i = newIndex >= 0 ? newIndex - 1 : Math.Min(i - 1, nodes.Length - 1);
        }
        return null;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: a child-list change inside the root that does not advance Root.TreeVersion keeps the old snapshot in use, so a removed node is returned or an inserted node is skipped
    // Broiler-Human:        PENDING
    private DomNode[] GetSnapshot()
    {
        if (_snapshot is null || _snapshotVersion != Root.TreeVersion)
        {
            _snapshot = Root.InclusiveDescendants().ToArray();
            _snapshotVersion = Root.TreeVersion;
            _snapshotIndices.Clear();
            for (var index = 0; index < _snapshot.Length; index++)
                _snapshotIndices.Add(_snapshot[index], index);
        }
        return _snapshot;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a node absent from the current snapshot yields an index other than -1
    // Broiler-Human:        PENDING
    private int SnapshotIndexOf(DomNode node) => _snapshotIndices.GetValueOrDefault(node, -1);

    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s6; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a filter that calls this iterator's nextNode() re-entrantly is run to completion instead of being refused with InvalidStateError, so the nested call moves ReferenceNode mid-traversal
    // Broiler-Human:        PENDING
    private DomFilterResult Evaluate(DomNode node)
    {
        if ((WhatToShow & DomTreeWalker.ShowFlag(node.NodeType)) == 0)
            return DomFilterResult.Skip;
        return _filter?.Invoke(node) ?? DomFilterResult.Accept;
    }
}
