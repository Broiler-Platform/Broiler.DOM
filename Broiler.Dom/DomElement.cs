using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Broiler.Dom;

// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
// Broiler-Falsified-If: an attribute write or removal leaves the document id index disagreeing with the id attribute, so GetElementById returns an element whose Id is not the requested value
// Broiler-Human:        PENDING
public class DomElement : DomNode
{
    private readonly Dictionary<(string? NamespaceUri, string LocalName), DomAttribute> _attributes = [];
    private readonly ReadOnlyDictionary<(string? NamespaceUri, string LocalName), DomAttribute> _readOnlyAttributes;

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    internal DomElement(DomDocument ownerDocument, DomName name)
        : this(ownerDocument, name, DomNodeType.Element)
    {
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an HTML template element is created with null TemplateContents, or a non-template element with a fragment
    // Broiler-Human:        PENDING
    protected DomElement(DomDocument ownerDocument, DomName name, DomNodeType nodeType)
        : base(nodeType, ownerDocument)
    {
        Name = name;
        _readOnlyAttributes = _attributes.AsReadOnly();
        TemplateContents = IsHtmlTemplateName(name) ? new DomDocumentFragment(ownerDocument) : null;
    }

    /// <summary>
    /// An HTML <c>&lt;template&gt;</c> element's <em>template contents</em> (HTML §4.12.3): the
    /// <see cref="DomDocumentFragment"/> holding what was written between its tags. Null for every
    /// other element, and deliberately absent from <see cref="DomNode.ChildNodes"/> — a template's
    /// contents are inert, so no walk over the document tree reaches them.
    /// </summary>
    /// <remarks>
    /// Created with the element, not on demand: the HTML tree builder needs somewhere to redirect
    /// insertions the moment the start tag is seen (§13.2.6.1), and the fragment's identity is
    /// observable, so asking a template for its contents twice must give the same node.
    /// <para>
    /// This is the second HTML detail the kernel carries by name, beside the host element list in
    /// <see cref="AttachShadow"/>, and for the same reason: it is part of a node's data model, not a
    /// projection over it. Kept outside, as a table keyed on the element beside the tree, it could
    /// not travel with the node — and every query in the layers above would still be walking into
    /// markup the Standard calls inert, each needing its own template guard.
    /// </para>
    /// </remarks>
    public DomDocumentFragment? TemplateContents { get; }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an SVG or MathML element with local name template is treated as an HTML template
    // Broiler-Human:        PENDING
    private static bool IsHtmlTemplateName(DomName name) =>
        string.Equals(name.NamespaceUri, DomNamespaces.Html, StringComparison.Ordinal) &&
        string.Equals(name.LocalName, "template", StringComparison.Ordinal);

    public DomName Name { get; private set; }

    public string LocalName => Name.LocalName;

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public string TagName => Name.QualifiedName;

    /// <summary>The namespace prefix of the element's qualified name, or <c>null</c>.</summary>
    public string? Prefix => Name.Prefix;

    public string? NamespaceUri => Name.NamespaceUri;

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected void SetName(DomName name) => Name = name;

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: an empty-string namespace misses an attribute stored with no namespace
    // Broiler-Human:        PENDING
    public bool RemoveAttributeNS(string? namespaceUri, string localName) =>
    RemoveAttributeCore(NormalizeNamespace(namespaceUri), localName);

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a prefixed qualified name with no namespace is stored instead of throwing a DomException named NamespaceError
    // Broiler-Human:        PENDING
    public void SetAttributeNS(string? namespaceUri, string qualifiedName, string value) =>
    SetAttributeCore(new DomName(namespaceUri, qualifiedName), value);

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public IReadOnlyDictionary<(string? NamespaceUri, string LocalName), DomAttribute> Attributes =>
        _readOnlyAttributes;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: setting Id to null leaves an id attribute in place or the element still findable by GetElementById
    // Broiler-Human:        PENDING
    public string? Id
    {
        get => GetAttribute("id");
        set
        {
            if (value is null)
                RemoveAttribute("id");
            else
                SetAttribute("id", value);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: setting ClassName to null leaves a class attribute on the element
    // Broiler-Human:        PENDING
    public string? ClassName
    {
        get => GetAttribute("class");
        set
        {
            if (value is null)
                RemoveAttribute("class");
            else
                SetAttribute("class", value);
        }
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public bool HasAttribute(string qualifiedName) => GetAttribute(qualifiedName) is not null;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: GetAttribute with a mixed-case name misses an attribute that SetAttribute stored under the same name
    // Broiler-Human:        PENDING
    public string? GetAttribute(string qualifiedName)
    {
        var key = (NamespaceUri: (string?)null, LocalName: qualifiedName.ToLowerInvariant());
        return _attributes.TryGetValue(key, out var attribute) ? attribute.Value : null;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: an empty-string namespace misses an attribute stored with no namespace
    // Broiler-Human:        PENDING
    public string? GetAttributeNS(string? namespaceUri, string localName)
    {
        var key = (NormalizeNamespace(namespaceUri), localName);
        return _attributes.TryGetValue(key, out var attribute) ? attribute.Value : null;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a name containing U+212A KELVIN SIGN is folded to ASCII k, so onclic followed by that sign is stored as the onclick attribute
    // Broiler-Human:        PENDING
    public void SetAttribute(string qualifiedName, string value) =>
        // Per DOM, Element.setAttribute() does NO namespace splitting: the whole
        // qualified name is the attribute's local name (no namespace). This also
        // keys identically to GetAttribute/RemoveAttribute below, and avoids
        // throwing on prefixed names like SVG's "xlink:href".
        SetAttributeCore(DomName.CreateLocal(qualifiedName.ToLowerInvariant()), value);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: RemoveAttribute with a mixed-case name leaves in place the attribute SetAttribute stored for it
    // Broiler-Human:        PENDING
    public bool RemoveAttribute(string qualifiedName) =>
        RemoveAttributeCore(null, qualifiedName.ToLowerInvariant());

    /// <summary>
    /// Attempts to retrieve an attribute by its qualified name using ASCII case-insensitive comparison.
    /// Yields <c>string.Empty</c> when absent.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an attribute whose qualified name differs only in ASCII case from the query is not found
    // Broiler-Human:        PENDING
    public bool TryGetAttributeByQualifiedName(string qualifiedName, out string value)
    {
        ArgumentNullException.ThrowIfNull(qualifiedName);

        foreach (var attribute in _attributes.Values)
        {
            if (string.Equals(attribute.QualifiedName, qualifiedName, StringComparison.OrdinalIgnoreCase))
            {
                value = attribute.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    /// <summary>
    /// Retrieves an attribute value by qualified name using ASCII case-insensitive comparison,
    /// or <c>null</c> if absent.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public string? GetAttributeByQualifiedName(string qualifiedName) =>
        TryGetAttributeByQualifiedName(qualifiedName, out var value) ? value : null;

    /// <summary>
    /// Returns <c>true</c> if an attribute with the given qualified name exists (case-insensitive).
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public bool HasAttributeByQualifiedName(string qualifiedName) =>
        TryGetAttributeByQualifiedName(qualifiedName, out _);

    /// <summary>
    /// Sets an attribute by qualified name. If an attribute with the same qualified name
    /// already exists (case-insensitive), its value is updated in place preserving its namespace;
    /// otherwise a no-namespace attribute is created.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: updating an existing namespaced attribute by its qualified name adds a second no-namespace attribute instead of changing it
    // Broiler-Human:        PENDING
    public void SetAttributeByQualifiedName(string qualifiedName, string value)
    {
        ArgumentNullException.ThrowIfNull(qualifiedName);
        ArgumentNullException.ThrowIfNull(value);

        DomAttribute? existing = null;
        foreach (var attribute in _attributes.Values)
        {
            if (string.Equals(attribute.QualifiedName, qualifiedName, StringComparison.OrdinalIgnoreCase))
            {
                existing = attribute;
                break;
            }
        }

        if (existing is { } found)
            SetAttributeCore(found.Name, value);
        else
            SetAttribute(qualifiedName, value);
    }

    /// <summary>
    /// Removes the attribute matching the given qualified name (case-insensitive).
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: removing by a qualified name that matches a namespaced attribute returns true while the attribute stays
    // Broiler-Human:        PENDING
    public bool RemoveAttributeByQualifiedName(string qualifiedName)
    {
        ArgumentNullException.ThrowIfNull(qualifiedName);

        DomAttribute? existing = null;
        foreach (var attribute in _attributes.Values)
        {
            if (string.Equals(attribute.QualifiedName, qualifiedName, StringComparison.OrdinalIgnoreCase))
            {
                existing = attribute;
                break;
            }
        }

        return existing is { } found && RemoveAttributeCore(found.NamespaceUri, found.LocalName);
    }

    /// <summary>
    /// Looks up an attribute by namespace URI and local name, yielding its qualified name and value.
    /// The namespace URI is normalized (empty string ≡ null).
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: a lookup with an empty-string namespace misses an attribute stored with no namespace
    // Broiler-Human:        PENDING
    public bool TryGetAttributeNS(string? namespaceUri, string localName, out string qualifiedName, out string value)
    {
        ArgumentNullException.ThrowIfNull(localName);

        var ns = NormalizeNamespace(namespaceUri);
        if (_attributes.TryGetValue((ns, localName), out var attribute))
        {
            qualifiedName = attribute.QualifiedName;
            value = attribute.Value;
            return true;
        }

        qualifiedName = string.Empty;
        value = string.Empty;
        return false;
    }

    /// <summary>
    /// Enumerates the qualified names of all attributes present on this element.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a namespaced attribute is enumerated by its local name instead of its prefixed qualified name
    // Broiler-Human:        PENDING
    public IEnumerable<string> AttributeQualifiedNames
    {
        get
        {
            foreach (var attribute in _attributes.Values)
                yield return attribute.QualifiedName;
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a shallow clone shares an attribute store with its source, so an attribute write on one shows on the other
    // Broiler-Human:        PENDING
    internal override DomNode CloneShallow(DomDocument ownerDocument)
    {
        var clone = new DomElement(ownerDocument, Name);
        foreach (var attribute in _attributes.Values)
            clone._attributes.Add((attribute.NamespaceUri, attribute.LocalName), attribute);
        return clone;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a no-namespace attribute whose local name is ID in upper case updates the document id index, so GetElementById returns an element whose Id is null
    // Broiler-Human:        PENDING
    private void SetAttributeCore(DomName name, string value)
    {
        var key = (name.NamespaceUri, name.LocalName);
        var oldValue = _attributes.TryGetValue(key, out var oldAttribute)
            ? oldAttribute.Value
            : null;

        if (string.Equals(oldValue, value, StringComparison.Ordinal) &&
            oldAttribute.Name == name)
        {
            return;
        }

        _attributes[key] = new DomAttribute(name, value);
        if (name.NamespaceUri is null && string.Equals(name.LocalName, "id", StringComparison.OrdinalIgnoreCase))
            OwnerDocument.UpdateElementId(this, oldValue, value);

        MarkChanged();
        OwnerDocument.PublishMutation(new DomMutationRecord(
            DomMutationType.Attributes,
            this,
            AttributeName: name.QualifiedName,
            AttributeNamespace: name.NamespaceUri,
            OldValue: oldValue,
            NewValue: value));
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: removing a no-namespace attribute named ID in upper case drops the element from the id index while its id attribute still holds that value
    // Broiler-Human:        PENDING
    private bool RemoveAttributeCore(string? namespaceUri, string localName)
    {
        var key = (namespaceUri, localName);
        if (!_attributes.Remove(key, out var removed))
            return false;

        if (namespaceUri is null && string.Equals(localName, "id", StringComparison.OrdinalIgnoreCase))
            OwnerDocument.UpdateElementId(this, removed.Value, null);

        MarkChanged();
        OwnerDocument.PublishMutation(new DomMutationRecord(
            DomMutationType.Attributes,
            this,
            AttributeName: removed.QualifiedName,
            AttributeNamespace: removed.NamespaceUri,
            OldValue: removed.Value));
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an empty-string namespace is kept as the empty string, so it keys differently from no namespace
    // Broiler-Human:        PENDING
    private static string? NormalizeNamespace(string? namespaceUri) =>
        string.IsNullOrEmpty(namespaceUri) ? null : namespaceUri;

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: the set admits a local name that differs in case from a listed name, such as an HTML-namespace element named DIV
    // Broiler-Human:        PENDING
    private static readonly HashSet<string> AllowedShadowHostTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "article", "aside", "blockquote", "body", "div", "footer",
        "h1", "h2", "h3", "h4", "h5", "h6",
        "header", "main", "nav", "p", "section", "span",
    };

    private DomShadowRoot? _shadowRoot;

    /// <summary>
    /// Returns the open shadow root attached to this element, or null if there is none
    /// or if it was attached in closed mode.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an element whose shadow root was attached in closed mode returns that root from ShadowRoot
    // Broiler-Human:        PENDING
    public DomShadowRoot? ShadowRoot => _shadowRoot?.Mode == DomShadowRootMode.Open ? _shadowRoot : null;

    /// <summary>
    /// Returns the shadow root attached to this element regardless of mode.
    /// Intended for engine and bridge internal use.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public DomShadowRoot? InternalShadowRoot => _shadowRoot;

    /// <summary>
    /// Attaches a shadow root to this element (DOM §4.2.1.3).
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: an element outside the HTML namespace whose local name is on the host list, such as an SVG element named div, is given a shadow root instead of a DomException named NotSupportedError
    // Broiler-Human:        PENDING
    public DomShadowRoot AttachShadow(
        DomShadowRootMode mode,
        bool delegatesFocus = false,
        DomSlotAssignmentMode slotAssignment = DomSlotAssignmentMode.Named,
        bool clonable = false,
        bool serializable = false)
    {
        if (!AllowedShadowHostTags.Contains(LocalName) && !DomNameValidation.IsValidCustomElementName(LocalName))
        {
            throw DomException.NotSupported(
                $"Failed to execute 'attachShadow' on 'Element': This element ('{LocalName}') does not support attachShadow.");
        }

        if (_shadowRoot != null)
        {
            throw DomException.NotSupported(
                "Failed to execute 'attachShadow' on 'Element': Shadow root cannot be created on a host which already hosts a shadow tree.");
        }

        var shadowRoot = new DomShadowRoot(this, mode, delegatesFocus, slotAssignment, clonable, serializable);
        _shadowRoot = shadowRoot;
        return shadowRoot;
    }
}
