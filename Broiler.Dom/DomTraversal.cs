using System;


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

public sealed class DomTreeWalker
{
    public DomTreeWalker(DomNode root, DomWhatToShow whatToShow = DomWhatToShow.All)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        CurrentNode = root;
        WhatToShow = whatToShow;
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
}

public sealed class DomNodeIterator : IDisposable
{
    private bool _disposed;

    public DomNodeIterator(DomNode root, DomWhatToShow whatToShow = DomWhatToShow.All)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        WhatToShow = whatToShow;
        ReferenceNode = root;
        PointerBeforeReferenceNode = true;
        root.OwnerDocument.Mutated += OnMutation;
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
}
