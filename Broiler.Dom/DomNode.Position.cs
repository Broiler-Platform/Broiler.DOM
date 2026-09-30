using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Broiler.Dom;

// Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=5; Fingerprint=TBF
// Broiler-Falsified-If: two disconnected trees are ordered one way by one comparison and the other way by a later one while both roots are alive, including comparisons made on different threads
// Broiler-Human:        PENDING
public abstract partial class DomNode
{
    // Broiler-AI:           Origin=AI; IP=None; Security=High; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: a root that is still alive is issued a second, different order key, so the same two trees compare one way and later the other
    // Broiler-Human:        PENDING
    private static readonly ConditionalWeakTable<DomNode, object> DisconnectedOrderKeys = new();
    private static long _lastDisconnectedOrderKey;

    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: two threads asking at once for the key of the same new root receive different keys
    // Broiler-Human:        PENDING
    private static long DisconnectedOrderKey(DomNode root) =>
        (long)DisconnectedOrderKeys.GetValue(root, static _ => Interlocked.Increment(ref _lastDisconnectedOrderKey));

    /// <summary>
    /// Compares the position of this node relative to <paramref name="other"/> in document order,
    /// returning a bitmask of <see cref="DomDocumentPosition"/> flags.
    /// </summary>
    /// <remarks>
    /// Nodes in different trees are <see cref="DomDocumentPosition.Disconnected"/> |
    /// <see cref="DomDocumentPosition.ImplementationSpecific"/> plus a Preceding or Following bit. The
    /// spec only requires that bit to be consistent, so it comes from a sequence number each root gets
    /// on its first such comparison: two trees order the same way for as long as both are alive.
    /// </remarks>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s4.4; IP=Low; Security=High; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: for nodes of two different trees, a.CompareDocumentPosition(b) and b.CompareDocumentPosition(a) both carry Following, or both carry Preceding
    // Broiler-Human:        PENDING
    public DomDocumentPosition CompareDocumentPosition(DomNode other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (ReferenceEquals(this, other))
            return DomDocumentPosition.None;

        var ancestors = InclusiveAncestors().ToArray();
        var otherAncestors = other.InclusiveAncestors().ToArray();
        if (!ReferenceEquals(ancestors[^1], otherAncestors[^1]))
        {
            return DomDocumentPosition.Disconnected | DomDocumentPosition.ImplementationSpecific |
                (DisconnectedOrderKey(otherAncestors[^1]) < DisconnectedOrderKey(ancestors[^1])
                    ? DomDocumentPosition.Preceding
                    : DomDocumentPosition.Following);
        }

        if (ancestors.IndexOfReference(other) >= 0)
            return DomDocumentPosition.Contains | DomDocumentPosition.Preceding;
        if (otherAncestors.IndexOfReference(this) >= 0)
            return DomDocumentPosition.ContainedBy | DomDocumentPosition.Following;

        // Neither contains the other, so walking down from the shared root the two ancestor chains
        // must diverge before either ends. The diverging nodes are siblings; their order is the answer.
        var depth = 1;
        while (ReferenceEquals(ancestors[^(depth + 1)], otherAncestors[^(depth + 1)]))
            depth++;
        var siblings = ancestors[^depth]._children;
        return siblings.IndexOf(otherAncestors[^(depth + 1)]) < siblings.IndexOf(ancestors[^(depth + 1)])
            ? DomDocumentPosition.Preceding
            : DomDocumentPosition.Following;
    }
}
