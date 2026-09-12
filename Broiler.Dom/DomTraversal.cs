using System;
using System.Collections.Generic;
using System.Linq;


namespace Broiler.Dom;

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

public enum DomFilterResult
{
    Accept = 1,
    Reject = 2,
    Skip = 3
}

public sealed class DomTreeWalker
{
    private readonly Func<DomNode, DomFilterResult>? _filter;

    public DomTreeWalker(DomNode root, DomWhatToShow whatToShow = DomWhatToShow.All, Func<DomNode, DomFilterResult>? filter = null)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        CurrentNode = root;
        WhatToShow = whatToShow;
        _filter = filter;
    }

    public DomNode Root { get; }

    public DomWhatToShow WhatToShow { get; }

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

    private DomFilterResult Evaluate(DomNode node)
    {
        if ((WhatToShow & ShowFlag(node.NodeType)) == 0)
            return DomFilterResult.Skip;
        return _filter?.Invoke(node) ?? DomFilterResult.Accept;
    }

    internal static DomWhatToShow ShowFlag(DomNodeType nodeType) => nodeType switch
    {
        DomNodeType.Element => DomWhatToShow.Element,
        DomNodeType.Text => DomWhatToShow.Text,
        DomNodeType.Comment => DomWhatToShow.Comment,
        DomNodeType.Document => DomWhatToShow.Document,
        DomNodeType.DocumentFragment => DomWhatToShow.DocumentFragment,
        _ => DomWhatToShow.None
    };

    public DomNode? FirstChild() => TraverseChildren(forward: true);

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

    public DomNode? LastChild() => TraverseChildren(forward: false);

    public DomNode? NextSibling() => TraverseSiblings(forward: true);

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

    public DomNode? PreviousSibling() => TraverseSiblings(forward: false);

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

public sealed class DomNodeIterator : IDisposable
{
    private int _lastKnownIndex = -1;
    private readonly Func<DomNode, DomFilterResult>? _filter;
    private readonly Dictionary<DomNode, int> _snapshotIndices = new(ReferenceEqualityComparer.Instance);
    private DomNode[]? _snapshot;
    private ulong _snapshotVersion;
    private bool _disposed;

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

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Root.OwnerDocument.Mutated -= OnMutation;
        _snapshot = null;
        _snapshotIndices.Clear();
    }

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

    private int SnapshotIndexOf(DomNode node) => _snapshotIndices.GetValueOrDefault(node, -1);

    private DomFilterResult Evaluate(DomNode node)
    {
        if ((WhatToShow & DomTreeWalker.ShowFlag(node.NodeType)) == 0)
            return DomFilterResult.Skip;
        return _filter?.Invoke(node) ?? DomFilterResult.Accept;
    }
}
