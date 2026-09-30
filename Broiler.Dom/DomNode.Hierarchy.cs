using System.Linq;

namespace Broiler.Dom;

// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
// Broiler-Falsified-If: a node these checks accept becomes its own ancestor through a shadow-host link, as when a shadow host is appended to its own shadow root, and the connectedness walk that follows never returns
// Broiler-Human:        PENDING
public abstract partial class DomNode
{
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: called on a DomText, DomComment or DomDocumentType node it returns instead of throwing HierarchyRequestError
    // Broiler-Human:        PENDING
    private void EnsureCanHaveChildren()
    {
        if (this is DomText or DomComment or DomDocumentType)
            throw DomException.HierarchyRequest($"{NodeType} nodes cannot have children.");
    }

    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s4.2.3; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a template element appended to its own template contents passes, because only parent links are walked and not the host link a host-including ancestor check follows
    // Broiler-Human:        PENDING
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
