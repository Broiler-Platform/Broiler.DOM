using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.Dom;

// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
// Broiler-Falsified-If: a script-built subtree nested about 20,000 elements deep ends the process with a stack overflow when imported deeply or connected to the document
// Broiler-Human:        PENDING
public sealed class DomDocument : DomNode
{
    private readonly Dictionary<string, HashSet<DomElement>> _elementsById = new(StringComparer.Ordinal);

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public DomDocument() : base(DomNodeType.Document, null)
    {
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public override DomDocument OwnerDocument => this;

    public ulong Version { get; private set; }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a document whose doctype follows a comment child returns null from DocumentType
    // Broiler-Human:        PENDING
    public DomDocumentType? DocumentType => ChildNodes.OfType<DomDocumentType>().FirstOrDefault();

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event Action<DomMutationRecord>? Mutated;

    /// <summary>
    /// Raised before nodes leave this document's tree, while they are still connected and still
    /// where they were: the moment a host runs what has to happen first. A browser takes focus from
    /// a focused element that is going there, and fires its <c>blur</c> and <c>focusout</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A handler can run script, and script can change the tree. A removal checks again afterwards:
    /// <see cref="DomNode.RemoveChild"/> throws a <c>NotFoundError</c>, as Chromium does, when the node
    /// is no longer a child of the node removing it, and an insertion that was moving a node throws
    /// when its reference node has gone. A replace-all removes whatever children there are once the
    /// handlers have run.
    /// </para>
    /// <para>
    /// Raised only for connected nodes: what a host has to do before a removal concerns nodes in a
    /// document, a focused element always is one, and a detached tree has nothing to announce.
    /// <c>moveBefore</c> raises nothing, because it never disconnects what it moves.
    /// </para>
    /// </remarks>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s4.2.3; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a handler sees the node already detached or its parent changed, or a removal from a detached tree raises it
    // Broiler-Human:        PENDING
    public event Action<DomRemoval>? Removing;

    internal bool HasRemovingHandlers => Removing is not null;

    internal void RaiseRemoving(DomRemoval removal) => Removing?.Invoke(removal);

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public DomDocumentType CreateDocumentType(string name, string publicId = "", string systemId = "") => new(this, name, publicId, systemId);

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public DomText CreateTextNode(string data) => new(this, data);

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public DomComment CreateComment(string data) => new(this, data);

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public DomDocumentFragment CreateDocumentFragment() => new(this);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a document whose first child is a doctype or comment returns null although an element child follows
    // Broiler-Human:        PENDING
    public DomElement? DocumentElement => ChildNodes.OfType<DomElement>().FirstOrDefault();

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: Head returns an element outside the HTML namespace, such as an SVG element named head placed before the HTML head
    // Broiler-Human:        PENDING
    public DomElement? Head => DocumentElement?.ChildNodes.OfType<DomElement>().FirstOrDefault(static element => string.Equals(element.LocalName, "head", StringComparison.OrdinalIgnoreCase));

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a document element whose first body-or-frameset child is a frameset makes Body return null instead of that frameset
    // Broiler-Human:        PENDING
    public DomElement? Body => DocumentElement?.ChildNodes.OfType<DomElement>().FirstOrDefault(static element => string.Equals(element.LocalName, "body", StringComparison.OrdinalIgnoreCase));

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: adopting a shadow root moves it to another document while its host stays in the old one, where the DOM throws HierarchyRequestError
    // Broiler-Human:        PENDING
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

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: a deep import of a detached chain about 20,000 elements deep ends the process with a stack overflow in the recursive ImportNode calls
    // Broiler-Human:        PENDING
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
            DomNode.CopyTemplateContents(node, clone, child => ImportNode(child, true));
        }

        return clone;
    }

    /// <summary>
    /// Creates an HTML-namespace element whose local name is <paramref name="localName"/>
    /// lowercased. Per DOM, <c>createElement</c> takes a local name, so the whole name is
    /// kept verbatim — a ':' in it is an ordinary character, not a prefix separator, and
    /// never an error. That is also what the HTML parser needs: its tokeniser accepts any
    /// character but whitespace, '/' and '&gt;' in a tag name, so markup as unremarkable as
    /// <c>&lt;x::y&gt;</c> — or a resource mistakenly fed to the parser as markup — would
    /// otherwise throw here and take down the whole page. Prefix splitting and
    /// namespace validation belong to <see cref="CreateElementNS"/>.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: a name containing U+212A KELVIN SIGN is folded to ASCII k, so LIN followed by that sign yields an element whose local name is link
    // Broiler-Human:        PENDING
    public DomElement CreateElement(string localName) =>
        new(this, DomName.CreateLocal(DomNamespaces.Html, localName.ToLowerInvariant()));

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: a prefixed qualified name with no namespace yields an element instead of a DomException named NamespaceError
    // Broiler-Human:        PENDING
    public DomElement CreateElementNS(string? namespaceUri, string qualifiedName) =>
        new(this, new DomName(namespaceUri, qualifiedName));

    internal override DomNode CloneShallow(DomDocument ownerDocument) =>
        throw new InvalidOperationException("Document cloning is not supported by the Phase 1 kernel.");

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a subscriber observes a record while Version still holds its value from before the mutation
    // Broiler-Human:        PENDING
    internal void PublishMutation(DomMutationRecord mutation)
    {
        Version++;
        Mutated?.Invoke(mutation);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: after a connected element's id changes, GetElementById still returns it for the old id
    // Broiler-Human:        PENDING
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

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: connecting a detached chain of 50,000 nested elements ends the process with a stack overflow in the nested InclusiveDescendants iterators
    // Broiler-Human:        PENDING
    internal void IndexConnectedSubtree(DomNode node)
    {
        foreach (var element in node.InclusiveDescendants().OfType<DomElement>())
            UpdateElementId(element, null, element.Id);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: an element in the shadow tree of a removed host keeps its id entry, so once its id changes and it moves into the document GetElementById returns it for the old id
    // Broiler-Human:        PENDING
    internal void UnindexConnectedSubtree(DomNode node)
    {
        foreach (var element in node.InclusiveDescendants().OfType<DomElement>())
            UpdateElementId(element, element.Id, null);
    }

    /// <summary>
    /// Returns the element in this document's tree whose id is <paramref name="id"/>, in tree
    /// order, or <see langword="null"/> (DOM <c>getElementById</c>).
    /// </summary>
    /// <remarks>
    /// The index behind this is keyed by shadow-including connectedness, because that is what
    /// decides when an id is added and removed, so it also holds elements inside a shadow tree.
    /// <c>getElementById</c> searches this document's own descendants, which a shadow tree is not
    /// one of — hence the root check on the single-candidate path, where the tree walk that the
    /// ambiguous path takes would already have answered correctly. The two used to disagree: with
    /// one element carrying an id the encapsulated one came back, with two it did not.
    /// </remarks>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: with a single indexed candidate inside a shadow tree, GetElementById returns that element instead of null
    // Broiler-Human:        PENDING
    public DomElement? GetElementById(string id)
    {
        if (!_elementsById.TryGetValue(id, out var candidates) || candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
        {
            var only = candidates.First();
            return ReferenceEquals(only.GetRootNode(), this) ? only : null;
        }

        return Descendants().OfType<DomElement>().FirstOrDefault(candidates.Contains);
    }

    /// <summary>
    /// Returns the document's element descendants whose qualified name matches
    /// <paramref name="qualifiedName"/> in tree order (DOM Standard
    /// <c>getElementsByTagName</c>). The special value <c>"*"</c> matches every
    /// element; matching is otherwise ASCII case-insensitive.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: a lowercase query matches an SVG element whose qualified name differs in case, such as foreignObject
    // Broiler-Human:        PENDING
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
