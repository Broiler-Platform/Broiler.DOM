using System.Linq;

namespace Broiler.Dom;

public abstract partial class DomNode
{
    private void EnsureCanHaveChildren()
    {
        if (this is DomText or DomComment or DomDocumentType)
            throw DomException.HierarchyRequest($"{NodeType} nodes cannot have children.");
    }

    internal void EnsurePreInsertValidity(DomNode node, DomNode? referenceNode, DomNode? replacedChild = null)
    {
        EnsureCanHaveChildren();

        if (ReferenceEquals(node, this) || InclusiveAncestors().Contains(node))
            throw DomException.HierarchyRequest("A node cannot be inserted into itself or one of its descendants.");

        if (node is DomDocument)
            throw DomException.HierarchyRequest("A document cannot be inserted into another node.");

        if (this is not DomDocument document)
            return;

        if (ReferenceEquals(referenceNode, node))
            referenceNode = node.NextSibling;

        var candidates = node is DomDocumentFragment
            ? node.ChildNodes
            : [node];

        if (candidates.Any(static candidate => candidate is DomText))
            throw DomException.HierarchyRequest("Text nodes cannot be direct children of a document.");

        // Validate the resulting child order before removal, adoption, or notifications.
        // Exclude both a moved node and a replaced child when checking document uniqueness.
        var children = document._children
            .Where(child => !ReferenceEquals(child, node) && !ReferenceEquals(child, replacedChild))
            .ToList();
        var insertionIndex = referenceNode is null ? children.Count : children.IndexOf(referenceNode);
        children.InsertRange(insertionIndex, candidates);

        if (children.Count(static child => child is DomElement) > 1 ||
            children.Count(static child => child is DomDocumentType) > 1)
            throw DomException.HierarchyRequest("A document can contain only one element and one document type.");

        var elementIndex = children.FindIndex(static child => child is DomElement);
        var doctypeIndex = children.FindIndex(static child => child is DomDocumentType);
        if (elementIndex >= 0 && doctypeIndex > elementIndex)
            throw DomException.HierarchyRequest("A document type must precede the document element.");
    }
}
