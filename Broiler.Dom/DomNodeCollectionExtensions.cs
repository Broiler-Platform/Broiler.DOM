using System.Collections.Generic;

namespace Broiler.Dom;

public static class DomNodeCollectionExtensions
{
    /// <summary>Index of <paramref name="target"/> in <paramref name="nodes"/> by reference equality,
    /// or -1 if absent. Public so bridge/host consumers can reuse the canonical reference-index scan
    /// (e.g. <c>DomBridge.ChildIndexOf</c>) instead of re-implementing the loop.</summary>
    public static int IndexOfReference(this IReadOnlyList<DomNode> nodes, DomNode target)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            if (ReferenceEquals(nodes[index], target))
                return index;
        }
        return -1;
    }
}
