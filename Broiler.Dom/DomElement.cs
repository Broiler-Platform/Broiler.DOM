using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Broiler.Dom;

public class DomElement : DomNode
{
    private readonly Dictionary<(string? NamespaceUri, string LocalName), DomAttribute> _attributes = [];
    private readonly ReadOnlyDictionary<(string? NamespaceUri, string LocalName), DomAttribute> _readOnlyAttributes;

    internal DomElement(DomDocument ownerDocument, DomName name)
        : this(ownerDocument, name, DomNodeType.Element)
    {
    }

    protected DomElement(DomDocument ownerDocument, DomName name, DomNodeType nodeType)
        : base(nodeType, ownerDocument)
    {
        Name = name;
        _readOnlyAttributes = _attributes.AsReadOnly();
    }

    public DomName Name { get; private set; }

    public string LocalName => Name.LocalName;

    public string TagName => Name.QualifiedName;

    /// <summary>The namespace prefix of the element's qualified name, or <c>null</c>.</summary>
    public string? Prefix => Name.Prefix;

    public string? NamespaceUri => Name.NamespaceUri;

    protected void SetName(DomName name) => Name = name;

    public bool RemoveAttributeNS(string? namespaceUri, string localName) =>
    RemoveAttributeCore(NormalizeNamespace(namespaceUri), localName);

    public void SetAttributeNS(string? namespaceUri, string qualifiedName, string value) =>
    SetAttributeCore(new DomName(namespaceUri, qualifiedName), value);

    public IReadOnlyDictionary<(string? NamespaceUri, string LocalName), DomAttribute> Attributes =>
        _readOnlyAttributes;

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

    public bool HasAttribute(string qualifiedName) => GetAttribute(qualifiedName) is not null;

    public string? GetAttribute(string qualifiedName)
    {
        var key = (NamespaceUri: (string?)null, LocalName: qualifiedName.ToLowerInvariant());
        return _attributes.TryGetValue(key, out var attribute) ? attribute.Value : null;
    }

    public string? GetAttributeNS(string? namespaceUri, string localName)
    {
        var key = (NormalizeNamespace(namespaceUri), localName);
        return _attributes.TryGetValue(key, out var attribute) ? attribute.Value : null;
    }

    public void SetAttribute(string qualifiedName, string value) =>
        // Per DOM, Element.setAttribute() does NO namespace splitting: the whole
        // qualified name is the attribute's local name (no namespace). This also
        // keys identically to GetAttribute/RemoveAttribute below, and avoids
        // throwing on prefixed names like SVG's "xlink:href".
        SetAttributeCore(DomName.CreateLocal(qualifiedName.ToLowerInvariant()), value);

    public bool RemoveAttribute(string qualifiedName) =>
        RemoveAttributeCore(null, qualifiedName.ToLowerInvariant());

    /// <summary>
    /// Attempts to retrieve an attribute by its qualified name using ASCII case-insensitive comparison.
    /// Yields <c>string.Empty</c> when absent.
    /// </summary>
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
    public string? GetAttributeByQualifiedName(string qualifiedName) =>
        TryGetAttributeByQualifiedName(qualifiedName, out var value) ? value : null;

    /// <summary>
    /// Returns <c>true</c> if an attribute with the given qualified name exists (case-insensitive).
    /// </summary>
    public bool HasAttributeByQualifiedName(string qualifiedName) =>
        TryGetAttributeByQualifiedName(qualifiedName, out _);

    /// <summary>
    /// Sets an attribute by qualified name. If an attribute with the same qualified name
    /// already exists (case-insensitive), its value is updated in place preserving its namespace;
    /// otherwise a no-namespace attribute is created.
    /// </summary>
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
    public IEnumerable<string> AttributeQualifiedNames
    {
        get
        {
            foreach (var attribute in _attributes.Values)
                yield return attribute.QualifiedName;
        }
    }

    internal override DomNode CloneShallow(DomDocument ownerDocument)
    {
        var clone = new DomElement(ownerDocument, Name);
        foreach (var attribute in _attributes.Values)
            clone._attributes.Add((attribute.NamespaceUri, attribute.LocalName), attribute);
        return clone;
    }

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

    private static string? NormalizeNamespace(string? namespaceUri) =>
        string.IsNullOrEmpty(namespaceUri) ? null : namespaceUri;

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
    public DomShadowRoot? ShadowRoot => _shadowRoot?.Mode == DomShadowRootMode.Open ? _shadowRoot : null;

    /// <summary>
    /// Returns the shadow root attached to this element regardless of mode.
    /// Intended for engine and bridge internal use.
    /// </summary>
    public DomShadowRoot? InternalShadowRoot => _shadowRoot;

    /// <summary>
    /// Attaches a shadow root to this element (DOM §4.2.1.3).
    /// </summary>
    public DomShadowRoot AttachShadow(
        DomShadowRootMode mode,
        bool delegatesFocus = false,
        DomSlotAssignmentMode slotAssignment = DomSlotAssignmentMode.Named)
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

        var shadowRoot = new DomShadowRoot(this, mode, delegatesFocus, slotAssignment);
        _shadowRoot = shadowRoot;
        return shadowRoot;
    }
}
