using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Broiler.Dom;

public abstract partial class DomNode
{
    private readonly List<DomNode> _children = [];
    private readonly ReadOnlyCollection<DomNode> _childNodes;
    private DomDocument? _ownerDocument;

    protected DomNode(DomNodeType nodeType, DomDocument? ownerDocument)
    {
        NodeType = nodeType;
        _ownerDocument = ownerDocument;
        _childNodes = _children.AsReadOnly();
    }

    public DomNodeType NodeType { get; }

    /// <summary>
    /// The character data of a text/comment node (DOM <c>nodeValue</c>); <c>null</c> for
    /// element and document nodes. Lets a consumer read a node's text through the canonical
    /// type without depending on the concrete node class — e.g. a host that represents text
    /// with its own <see cref="DomNodeType.Text"/> node subtype can override this.
    /// </summary>
    public virtual string? NodeValue => null;

    public void Remove() => ParentNode?.RemoveChild(this);

    public virtual DomDocument OwnerDocument =>
        _ownerDocument ?? throw new InvalidOperationException("The document node owns itself.");

    public DomNode? ParentNode { get; private set; }

    public IReadOnlyList<DomNode> ChildNodes => _childNodes;

    public DomNode ReplaceChild(DomNode node, DomNode child)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(child);

        if (!ReferenceEquals(child.ParentNode, this))
            throw DomException.NotFound("The node to replace is not a child of this node.");

        if (ReferenceEquals(node, child))
            return child;

        var reference = child.NextSibling;
        if (ReferenceEquals(reference, node))
            reference = node.NextSibling;
        EnsurePreInsertValidity(node, reference, child);
        RemoveChild(child);
        InsertBefore(node, reference);
        return child;
    }

    public DomNode? PreviousSibling
    {
        get
        {
            if (ParentNode is null)
                return null;

            var index = ParentNode._children.IndexOf(this);
            return index > 0 ? ParentNode._children[index - 1] : null;
        }
    }

    public DomNode? NextSibling
    {
        get
        {
            if (ParentNode is null)
                return null;

            var index = ParentNode._children.IndexOf(this);
            return index >= 0 && index + 1 < ParentNode._children.Count
                ? ParentNode._children[index + 1]
                : null;
        }
    }

    public DomNode? FirstChild => _children.Count == 0 ? null : _children[0];

    public DomNode? LastChild => _children.Count == 0 ? null : _children[^1];

    /// <summary>The DOM <c>parentElement</c>: the parent when it is an element, otherwise <c>null</c>
    /// (a document or fragment parent, or no parent at all).</summary>
    public DomElement? ParentElement => ParentNode as DomElement;

    // The element-traversal members below are the DOM's ParentNode (children, firstElementChild,
    // lastElementChild, childElementCount) and NonDocumentTypeChildNode (previous/nextElementSibling)
    // mixins. They are declared once here rather than per node class: on a node type the spec does
    // not give them to, the result is simply empty or null.

    /// <summary>
    /// The DOM <c>children</c>: this node's element children in tree order, as a snapshot taken when
    /// read. Text, comment and doctype children are skipped.
    /// </summary>
    public IReadOnlyList<DomElement> ChildElements => _children.OfType<DomElement>().ToArray();

    /// <summary>The DOM <c>firstElementChild</c>: the first child that is an element.</summary>
    public DomElement? FirstElementChild
    {
        get
        {
            foreach (var child in _children)
            {
                if (child is DomElement element)
                    return element;
            }
            return null;
        }
    }

    /// <summary>The DOM <c>lastElementChild</c>: the last child that is an element.</summary>
    public DomElement? LastElementChild
    {
        get
        {
            for (var index = _children.Count - 1; index >= 0; index--)
            {
                if (_children[index] is DomElement element)
                    return element;
            }
            return null;
        }
    }

    /// <summary>The DOM <c>childElementCount</c>: the number of children that are elements.</summary>
    public int ChildElementCount
    {
        get
        {
            var count = 0;
            foreach (var child in _children)
            {
                if (child is DomElement)
                    count++;
            }
            return count;
        }
    }

    /// <summary>The DOM <c>previousElementSibling</c>: the nearest preceding sibling that is an element.</summary>
    public DomElement? PreviousElementSibling
    {
        get
        {
            var siblings = ParentNode?._children;
            if (siblings is null)
                return null;

            for (var index = siblings.IndexOf(this) - 1; index >= 0; index--)
            {
                if (siblings[index] is DomElement element)
                    return element;
            }
            return null;
        }
    }

    /// <summary>The DOM <c>nextElementSibling</c>: the nearest following sibling that is an element.</summary>
    public DomElement? NextElementSibling
    {
        get
        {
            var siblings = ParentNode?._children;
            var index = siblings?.IndexOf(this) ?? -1;
            if (index < 0)
                return null;

            for (index++; index < siblings!.Count; index++)
            {
                if (siblings[index] is DomElement element)
                    return element;
            }
            return null;
        }
    }

    public bool IsConnected => GetRootNode(composed: true) is DomDocument;

    public ulong TreeVersion { get; private set; }

    public DomNode AppendChild(DomNode node) => InsertBefore(node, null);

    public DomNode InsertBefore(DomNode node, DomNode? referenceNode)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (referenceNode is not null && !ReferenceEquals(referenceNode.ParentNode, this))
            throw DomException.NotFound("The reference node is not a child of this node.");

        if (ReferenceEquals(node, referenceNode))
            return node;

        EnsurePreInsertValidity(node, referenceNode);

        if (node is DomDocumentFragment fragment)
        {
            var fragmentChildren = fragment._children.ToArray();
            foreach (var child in fragmentChildren)
                InsertBefore(child, referenceNode);
            return fragment;
        }

        var targetDocument = this is DomDocument document ? document : OwnerDocument;
        if (!ReferenceEquals(node.OwnerDocument, targetDocument))
            targetDocument.AdoptNode(node);

        if (ReferenceEquals(node.ParentNode, this) && ReferenceEquals(node.NextSibling, referenceNode))
            return node;

        node.ParentNode?.RemoveChild(node);

        var index = referenceNode is null ? _children.Count : _children.IndexOf(referenceNode);
        var previousSibling = index > 0 ? _children[index - 1] : null;
        _children.Insert(index, node);
        node.ParentNode = this;

        if (IsConnected)
            targetDocument.IndexConnectedSubtree(node);

        MarkChanged();
        targetDocument.PublishMutation(new DomMutationRecord(
            DomMutationType.ChildList,
            this,
            AddedNodes: [node],
            PreviousSibling: previousSibling,
            NextSibling: referenceNode));
        return node;
    }

    /// <summary>
    /// The DOM <c>Node.moveBefore()</c> operation: moves <paramref name="node"/> into this parent
    /// before <paramref name="referenceNode"/> <em>atomically</em>, without the
    /// remove-then-insert pair that <see cref="InsertBefore"/> performs.
    /// <para>
    /// The distinction is observable. A removal disconnects the node — an <c>&lt;iframe&gt;</c>
    /// reloads, focus is lost, animations restart, and a render-blocking element stops blocking.
    /// A move never disconnects it: because the spec requires both parents to share a
    /// shadow-including root, the node's connectedness cannot change, so the document's id index
    /// stays valid and is deliberately not churned.
    /// </para>
    /// <para>
    /// DIAGNOSTIC NOTE (WPT issue #1491, problem 27):
    /// <c>dom/nodes/moveBefore/preserve-render-blocking-style.html</c> moves a render-blocking
    /// <c>&lt;style&gt;</c> and asserts the styles survive. With no <c>moveBefore</c> at all the
    /// script threw, the document was never styled, and the test rendered white against
    /// Chromium's 100% green.
    /// </para>
    /// </summary>
    public DomNode MoveBefore(DomNode node, DomNode? referenceNode)
    {
        ArgumentNullException.ThrowIfNull(node);

        // Per spec: if the reference IS the node being moved, the move targets the slot after it,
        // which makes moveBefore(n, n) a no-op rather than an error.
        if (ReferenceEquals(referenceNode, node))
            referenceNode = node.NextSibling;

        EnsurePreMoveValidity(node, referenceNode);

        var oldParent = node.ParentNode!;
        if (ReferenceEquals(oldParent, this) && ReferenceEquals(node.NextSibling, referenceNode))
            return node;

        var document = this is DomDocument self ? self : OwnerDocument;

        // Detach from the old parent's child list WITHOUT RemoveChild: that would unindex the
        // subtree and publish a removal record, i.e. exactly the disconnection this operation
        // exists to avoid.
        var oldIndex = oldParent._children.IndexOf(node);
        var oldPreviousSibling = oldIndex > 0 ? oldParent._children[oldIndex - 1] : null;
        var oldNextSibling = oldIndex + 1 < oldParent._children.Count
            ? oldParent._children[oldIndex + 1]
            : null;
        oldParent._children.RemoveAt(oldIndex);

        var index = referenceNode is null ? _children.Count : _children.IndexOf(referenceNode);
        var previousSibling = index > 0 ? _children[index - 1] : null;
        _children.Insert(index, node);
        node.ParentNode = this;

        // No Index/UnindexConnectedSubtree pair: connectedness is invariant across a move, so the
        // id index the two would tear down and rebuild is already correct.
        oldParent.MarkChanged();
        if (!ReferenceEquals(oldParent, this))
            MarkChanged();

        // Observers still see the move as a removal from the old parent and an insertion into the
        // new one — the spec queues both records; only the disconnection is skipped.
        document.PublishMutation(new DomMutationRecord(
            DomMutationType.ChildList,
            oldParent,
            RemovedNodes: [node],
            PreviousSibling: oldPreviousSibling,
            NextSibling: oldNextSibling));
        document.PublishMutation(new DomMutationRecord(
            DomMutationType.ChildList,
            this,
            AddedNodes: [node],
            PreviousSibling: previousSibling,
            NextSibling: referenceNode));
        return node;
    }

    /// <summary>
    /// The DOM "ensure pre-move validity" steps. Stricter than pre-insert validity in two ways
    /// that matter: the node must <em>already be in the tree</em> (a move has nothing to preserve
    /// otherwise), and it must share this parent's shadow-including root — moving across roots
    /// would change connectedness, which a move is defined never to do.
    /// </summary>
    private void EnsurePreMoveValidity(DomNode node, DomNode? referenceNode)
    {
        if (this is not (DomDocument or DomDocumentFragment or DomElement))
            throw DomException.HierarchyRequest($"{NodeType} nodes cannot be a moveBefore parent.");

        if (ReferenceEquals(node, this) || InclusiveAncestors().Contains(node))
            throw DomException.HierarchyRequest("A node cannot be moved into itself or one of its descendants.");

        // Only Element and CharacterData are movable; a fragment has no single identity to
        // preserve, and a doctype/document cannot be repositioned this way.
        if (node is not (DomElement or DomCharacterData))
            throw DomException.HierarchyRequest($"{node.NodeType} nodes cannot be moved with moveBefore.");

        if (node.ParentNode is null)
            throw DomException.HierarchyRequest("The node to move must already be in the tree.");

        if (referenceNode is not null && !ReferenceEquals(referenceNode.ParentNode, this))
            throw DomException.NotFound("The reference node is not a child of this node.");

        if (!ReferenceEquals(node.GetRootNode(), GetRootNode()))
            throw DomException.HierarchyRequest("The node to move must share this node's root.");

        if (this is DomDocument && node is DomText)
            throw DomException.HierarchyRequest("Text nodes cannot be direct children of a document.");
    }

    public DomNode RemoveChild(DomNode child)
    {
        ArgumentNullException.ThrowIfNull(child);

        var index = _children.IndexOf(child);
        if (index < 0)
            throw DomException.NotFound("The node to remove is not a child of this node.");

        var previousSibling = index > 0 ? _children[index - 1] : null;
        var nextSibling = index + 1 < _children.Count ? _children[index + 1] : null;
        var document = this is DomDocument owner ? owner : OwnerDocument;

        if (child.IsConnected)
            document.UnindexConnectedSubtree(child);

        _children.RemoveAt(index);
        child.ParentNode = null;
        MarkChanged();
        document.PublishMutation(new DomMutationRecord(
            DomMutationType.ChildList,
            this,
            RemovedNodes: [child],
            PreviousSibling: previousSibling,
            NextSibling: nextSibling));
        return child;
    }

    public DomNode CloneNode(bool deep = false)
    {
        var clone = CloneShallow(OwnerDocument);
        if (deep)
        {
            foreach (var child in _children)
                clone.AppendChild(child.CloneNode(true));
            CopyTemplateContents(this, clone, static child => child.CloneNode(true));
        }

        return clone;
    }

    /// <summary>
    /// Copies a template's contents (HTML §4.12.3's cloning steps) from <paramref name="source"/>
    /// onto <paramref name="clone"/>, using <paramref name="copyChild"/> to produce each copy.
    /// Does nothing when either node is not an HTML <c>&lt;template&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Template contents are not children, so the deep-copy child walks in <see cref="CloneNode"/>
    /// and <see cref="DomDocument.ImportNode"/> do not reach them on their own, and a copy that
    /// skipped them would hand back an empty template. The two differ only in how a child is
    /// copied — cloned into the same document, or imported into another — which is the parameter.
    /// </remarks>
    internal static void CopyTemplateContents(DomNode source, DomNode clone, Func<DomNode, DomNode> copyChild)
    {
        if (source is not DomElement { TemplateContents: { } contents } ||
            clone is not DomElement { TemplateContents: { } target })
        {
            return;
        }

        foreach (var child in contents.ChildNodes)
            target.AppendChild(copyChild(child));
    }

    /// <summary>
    /// The DOM <c>Node.isEqualNode()</c> operation (§4.4): two nodes are equal when they have the
    /// same node type and type-specific identity, and equal children in order. DocumentType nodes
    /// compare name/publicId/systemId; character-data nodes compare their data; elements compare
    /// namespace + qualified name + attribute set (unordered, by namespace/local-name/value) + child
    /// list; document/fragment nodes compare their child list. This is the neutral tree algorithm the
    /// script bridge's <c>isEqualNode</c> binding delegates to.
    /// </summary>
    public bool IsEqualNode(DomNode? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        if (NodeType != other.NodeType)
            return false;

        switch (this)
        {
            case DomDocumentType thisDocType when other is DomDocumentType otherDocType:
                return string.Equals(thisDocType.Name, otherDocType.Name, StringComparison.Ordinal)
                    && string.Equals(thisDocType.PublicId, otherDocType.PublicId, StringComparison.Ordinal)
                    && string.Equals(thisDocType.SystemId, otherDocType.SystemId, StringComparison.Ordinal);

            case DomCharacterData thisData when other is DomCharacterData otherData:
                return string.Equals(thisData.Data, otherData.Data, StringComparison.Ordinal);

            case DomElement thisEl when other is DomElement otherEl:
                if (!string.Equals(thisEl.TagName, otherEl.TagName, StringComparison.Ordinal)
                    || !string.Equals(thisEl.NamespaceUri, otherEl.NamespaceUri, StringComparison.Ordinal)
                    || !AttributesEqual(thisEl, otherEl))
                {
                    return false;
                }
                break;
        }

        if (_children.Count != other._children.Count)
            return false;
        for (var index = 0; index < _children.Count; index++)
        {
            if (!_children[index].IsEqualNode(other._children[index]))
                return false;
        }
        return true;
    }

    private static bool AttributesEqual(DomElement first, DomElement second)
    {
        if (first.Attributes.Count != second.Attributes.Count)
            return false;
        foreach (var (key, attribute) in first.Attributes)
        {
            if (!second.Attributes.TryGetValue(key, out var other)
                || !string.Equals(attribute.Value, other.Value, StringComparison.Ordinal)
                || !string.Equals(attribute.QualifiedName, other.QualifiedName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return true;
    }

    public DomNode GetRootNode(bool composed = false)
    {
        DomNode current = this;
        while (true)
        {
            if (current.ParentNode is not null)
                current = current.ParentNode;
            else if (composed && current is DomShadowRoot shadowRoot && shadowRoot.Host is not null)
                current = shadowRoot.Host;
            else
                break;
        }
        return current;
    }

    public IEnumerable<DomNode> Descendants()
    {
        // Snapshot each level: Descendants() is enumerated lazily by long-lived
        // consumers (querySelectorAll, getElementsByTagName, tree walkers) while
        // script — or anchor/style reflection on the bridge — mutates _children
        // between MoveNext calls. Iterating the live list then throws "Collection
        // was modified" and aborts the walk (WPT issue #1143). Snapshotting is the
        // same defensive idiom the bridge uses for its Children walks.
        foreach (var child in _children.ToArray())
        {
            yield return child;
            foreach (var descendant in child.Descendants())
                yield return descendant;
        }
    }

    public IEnumerable<DomNode> InclusiveDescendants()
    {
        yield return this;
        foreach (var descendant in Descendants())
            yield return descendant;
    }

    /// <summary>
    /// The node's <c>textContent</c> (DOM Standard §4.4): a character-data node's own
    /// <see cref="DomCharacterData.Data"/>, otherwise the concatenated data of every
    /// <see cref="DomNodeType.Text"/> descendant in tree order (comments and processing
    /// instructions contribute nothing). Never <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Promoted from the HtmlBridge, which aggregated descendant text itself; the
    /// traversal is pure DOM data-model logic.
    /// <para>
    /// Setting it follows the same section: a character-data node takes the value as its
    /// data (<see langword="null"/> as the empty string); an element or fragment has every
    /// child replaced by one text node holding the value, or by nothing when the value is
    /// <see langword="null"/> or empty; a document or doctype ignores the write.
    /// </para>
    /// </remarks>
    [AllowNull]
    public string TextContent
    {
        get
        {
            if (this is DomCharacterData characterData)
                return characterData.Data;

            var builder = new StringBuilder();
            AppendDescendantText(this, builder);
            return builder.ToString();
        }
        set
        {
            switch (this)
            {
                case DomCharacterData characterData:
                    characterData.Data = value ?? string.Empty;
                    return;
                case DomDocument or DomDocumentType:
                    return;
            }

            ReplaceAllChildren(string.IsNullOrEmpty(value) ? null : OwnerDocument.CreateTextNode(value));
        }
    }

    /// <summary>
    /// The DOM "replace all" algorithm (§4.2.3): removes every child, inserts <paramref name="node"/>
    /// when given, and publishes <em>one</em> child-list record carrying both lists, as the spec
    /// queues one. That record's removed nodes all sat at index 0 with no siblings left, which is
    /// the shape live ranges and node iterators already resolve exactly as per-child removal would.
    /// </summary>
    private void ReplaceAllChildren(DomNode? node)
    {
        if (_children.Count == 0 && (node is null || (node is DomDocumentFragment frag && frag.ChildNodes.Count == 0)))
            return;

        var document = this is DomDocument doc ? doc : OwnerDocument;
        var removed = _children.ToArray();
        foreach (var child in removed)
        {
            if (child.IsConnected)
                document.UnindexConnectedSubtree(child);
            child.ParentNode = null;
        }
        _children.Clear();

        DomNode[]? added = null;
        if (node is DomDocumentFragment fragment)
        {
            var fragChildren = fragment._children.ToArray();
            fragment._children.Clear();
            foreach (var child in fragChildren)
            {
                _children.Add(child);
                child.ParentNode = this;
                if (IsConnected)
                    document.IndexConnectedSubtree(child);
            }
            added = fragChildren.Length == 0 ? null : fragChildren;
        }
        else if (node is not null)
        {
            _children.Add(node);
            node.ParentNode = this;
            if (IsConnected)
                document.IndexConnectedSubtree(node);
            added = [node];
        }

        MarkChanged();
        document.PublishMutation(new DomMutationRecord(
            DomMutationType.ChildList,
            this,
            AddedNodes: added,
            RemovedNodes: removed.Length == 0 ? null : removed));
    }

    private static void AppendDescendantText(DomNode node, StringBuilder builder)
    {
        if (node.NodeType == DomNodeType.Text)
        {
            builder.Append(node is DomCharacterData data ? data.Data : node.NodeValue ?? string.Empty);
            return;
        }

        // Snapshot for the same reason Descendants() does: a lazy consumer may mutate
        // the child list mid-walk (WPT issue #1143).
        foreach (var child in node._children.ToArray())
            AppendDescendantText(child, builder);
    }

    public IEnumerable<DomNode> InclusiveAncestors()
    {
        for (DomNode? current = this; current is not null; current = current.ParentNode)
            yield return current;
    }

    public bool IsDescendantOf(DomNode ancestor)
    {
        ArgumentNullException.ThrowIfNull(ancestor);
        for (var current = ParentNode; current is not null; current = current.ParentNode)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
        }
        return false;
    }

    public bool Contains(DomNode? other) =>
        other is not null && (ReferenceEquals(this, other) || other.IsDescendantOf(this));

    /// <summary>
    /// The nearest common inclusive ancestor of this node and <paramref name="other"/> — the deepest
    /// node that is an inclusive ancestor of both — or <c>null</c> when they belong to different trees
    /// (or <paramref name="other"/> is <c>null</c>). "Inclusive" means a node is its own ancestor, so
    /// if one node is an ancestor of the other, that node is returned. Unlike
    /// <see cref="DomRange.CommonAncestorContainer"/> (which requires two boundary points in one tree
    /// and throws otherwise), this is a null-tolerant node-level query for arbitrary node pairs.
    /// </summary>
    public DomNode? CommonAncestorWith(DomNode? other)
    {
        if (other is null)
            return null;
        var ancestors = InclusiveAncestors().ToHashSet();
        foreach (var ancestor in other.InclusiveAncestors())
        {
            if (ancestors.Contains(ancestor))
                return ancestor;
        }
        return null;
    }

    public void Normalize()
    {
        for (var index = 0; index < _children.Count;)
        {
            if (_children[index] is DomText text)
            {
                // DOM §4.4 "normalize": concatenate the node's contiguous exclusive Text siblings'
                // data and set the node's data ONCE, so a characterData observer sees a single
                // record per contiguous text run (not one per merged sibling). The following Text
                // siblings are then removed. (Previously `text.Data += next.Data` per sibling, which
                // published one CharacterData record for each merge step.)
                System.Text.StringBuilder? merged = null;
                while (index + 1 < _children.Count && _children[index + 1] is DomText next)
                {
                    merged ??= new System.Text.StringBuilder(text.Data);
                    merged.Append(next.Data);
                    RemoveChild(next);
                }

                if (merged is not null)
                    text.Data = merged.ToString();

                if (text.Data.Length == 0)
                {
                    RemoveChild(text);
                    continue;
                }
            }
            else
            {
                _children[index].Normalize();
            }

            index++;
        }
    }

    internal abstract DomNode CloneShallow(DomDocument ownerDocument);

    /// <remarks>
    /// Template contents travel with the template (HTML §4.12.3's adopting steps), and a child walk
    /// does not reach them — the same reason <see cref="CopyTemplateContents"/> exists for the two
    /// copying paths. Without this, adopting a template left its contents, and everything inside
    /// them, owned by the document they came from.
    /// </remarks>
    internal void SetOwnerDocument(DomDocument document)
    {
        _ownerDocument = document;
        if (this is DomElement { TemplateContents: { } contents })
            contents.SetOwnerDocument(document);
        foreach (var child in _children)
            child.SetOwnerDocument(document);
    }

    protected void MarkChanged()
    {
        for (DomNode? current = this; current is not null; current = current.ParentNode)
            current.TreeVersion++;
    }

}
