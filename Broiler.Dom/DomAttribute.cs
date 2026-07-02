namespace Broiler.Dom;

public readonly record struct DomAttribute(DomName Name, string Value)
{
    public string QualifiedName => Name.QualifiedName;

    public string LocalName => Name.LocalName;

    public string? NamespaceUri => Name.NamespaceUri;
}
