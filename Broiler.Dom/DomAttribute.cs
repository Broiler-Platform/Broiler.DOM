namespace Broiler.Dom;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public readonly record struct DomAttribute(DomName Name, string Value)
{
    public string QualifiedName => Name.QualifiedName;

    public string LocalName => Name.LocalName;

    public string? NamespaceUri => Name.NamespaceUri;
}
