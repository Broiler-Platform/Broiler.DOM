using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.Dom;

public sealed class DomDocument : DomNode
{
    private readonly Dictionary<string, HashSet<DomElement>> _elementsById = new(StringComparer.Ordinal);

    public DomDocument() : base(DomNodeType.Document, null)
    {
    }

    public override DomDocument OwnerDocument => this;

    public ulong Version { get; private set; }

    public event Action<DomMutationRecord>? Mutated;

    public DomNode AdoptNode(DomNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node is DomDocument)
            throw DomException.HierarchyRequest("A document cannot be adopted.");

        var oldDocument = node.OwnerDocument;
        node.ParentNode?.RemoveChild(node);

        if (ReferenceEquals(oldDocument, this))
            return node;

        node.SetOwnerDocument(this);
        PublishMutation(new DomMutationRecord(
            DomMutationType.Adoption,
            node,
            OldDocument: oldDocument,
            NewDocument: this));
        return node;
    }

    public DomNode ImportNode(DomNode node, bool deep = false)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node is DomDocument)
            throw DomException.HierarchyRequest("A document cannot be imported.");

        var clone = node.CloneShallow(this);
        if (deep)
        {
            foreach (var child in node.ChildNodes)
                clone.AppendChild(ImportNode(child, true));
        }

        return clone;
    }

    internal override DomNode CloneShallow(DomDocument ownerDocument) =>
        throw new InvalidOperationException("Document cloning is not supported by the Phase 1 kernel.");

    internal void PublishMutation(DomMutationRecord mutation)
    {
        Version++;
        Mutated?.Invoke(mutation);
    }

    internal void UpdateElementId(DomElement element, string? oldId, string? newId)
    {
        if (!element.IsConnected)
            return;

        if (!string.IsNullOrEmpty(oldId) && _elementsById.TryGetValue(oldId, out var oldSet))
        {
            oldSet.Remove(element);
            if (oldSet.Count == 0)
                _elementsById.Remove(oldId);
        }

        if (!string.IsNullOrEmpty(newId))
        {
            if (!_elementsById.TryGetValue(newId, out var newSet))
            {
                newSet = [];
                _elementsById.Add(newId, newSet);
            }

            newSet.Add(element);
        }
    }

    internal void IndexConnectedSubtree(DomNode node)
    {
        foreach (var element in node.InclusiveDescendants().OfType<DomElement>())
            UpdateElementId(element, null, element.Id);
    }

    internal void UnindexConnectedSubtree(DomNode node)
    {
        foreach (var element in node.InclusiveDescendants().OfType<DomElement>())
            UpdateElementId(element, element.Id, null);
    }
}
