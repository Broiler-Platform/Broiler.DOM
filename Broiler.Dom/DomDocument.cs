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

    public DomDocumentType? DocumentType => ChildNodes.OfType<DomDocumentType>().FirstOrDefault();

    public event Action<DomMutationRecord>? Mutated;

    public DomDocumentType CreateDocumentType(string name, string publicId = "", string systemId = "") => new(this, name, publicId, systemId);

    public DomText CreateTextNode(string data) => new(this, data);

    public DomComment CreateComment(string data) => new(this, data);

    public DomDocumentFragment CreateDocumentFragment() => new(this);

    public DomElement? DocumentElement => ChildNodes.OfType<DomElement>().FirstOrDefault();

    public DomElement? Body => DocumentElement?.ChildNodes.OfType<DomElement>().FirstOrDefault(static element => string.Equals(element.LocalName, "body", StringComparison.OrdinalIgnoreCase));

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

    public DomElement CreateElement(string localName) => new(this, new DomName(DomNamespaces.Html, localName.ToLowerInvariant()));

    public DomElement CreateElementNS(string? namespaceUri, string qualifiedName) =>
        new(this, new DomName(namespaceUri, qualifiedName));

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

    public DomElement? GetElementById(string id)
    {
        if (!_elementsById.TryGetValue(id, out var candidates) || candidates.Count == 0)
            return null;

        return Descendants().OfType<DomElement>().FirstOrDefault(candidates.Contains);
    }

    /// <summary>
    /// Returns the document's element descendants whose qualified name matches
    /// <paramref name="qualifiedName"/> in tree order (DOM Standard
    /// <c>getElementsByTagName</c>). The special value <c>"*"</c> matches every
    /// element; matching is otherwise ASCII case-insensitive.
    /// </summary>
    public IReadOnlyList<DomElement> GetElementsByTagName(string qualifiedName)
    {
        ArgumentNullException.ThrowIfNull(qualifiedName);

        var matchAll = qualifiedName == "*";
        var result = new List<DomElement>();
        foreach (var element in Descendants().OfType<DomElement>())
        {
            if (matchAll || string.Equals(element.TagName, qualifiedName, StringComparison.OrdinalIgnoreCase))
                result.Add(element);
        }

        return result;
    }
}
