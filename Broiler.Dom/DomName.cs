namespace Broiler.Dom;

public readonly record struct DomName
{
    public DomName(string? namespaceUri, string qualifiedName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName))
            throw new ArgumentException("A qualified name is required.", nameof(qualifiedName));

        var separator = qualifiedName.IndexOf(':');
        if (separator != qualifiedName.LastIndexOf(':') ||
            separator == 0 ||
            separator == qualifiedName.Length - 1)
        {
            throw DomException.Namespace($"'{qualifiedName}' is not a valid qualified name.");
        }

        NamespaceUri = string.IsNullOrEmpty(namespaceUri) ? null : namespaceUri;
        Prefix = separator < 0 ? null : qualifiedName[..separator];
        LocalName = separator < 0 ? qualifiedName : qualifiedName[(separator + 1)..];

        if (Prefix is not null && NamespaceUri is null)
            throw DomException.Namespace("A prefixed name requires a namespace URI.");

        if (string.Equals(Prefix, "xml", StringComparison.Ordinal) &&
            !string.Equals(NamespaceUri, DomNamespaces.Xml, StringComparison.Ordinal))
        {
            throw DomException.Namespace("The xml prefix requires the XML namespace.");
        }

        if ((string.Equals(qualifiedName, "xmlns", StringComparison.Ordinal) ||
             string.Equals(Prefix, "xmlns", StringComparison.Ordinal)) &&
            !string.Equals(NamespaceUri, DomNamespaces.Xmlns, StringComparison.Ordinal))
        {
            throw DomException.Namespace("The xmlns name requires the XMLNS namespace.");
        }

        QualifiedName = qualifiedName;
    }

    public string? NamespaceUri { get; }

    public string? Prefix { get; }

    public string LocalName { get; }

    public string QualifiedName { get; }
}

public static class DomNamespaces
{
    public const string Html = "http://www.w3.org/1999/xhtml";
    public const string Svg = "http://www.w3.org/2000/svg";
    public const string MathMl = "http://www.w3.org/1998/Math/MathML";
    public const string Xml = "http://www.w3.org/XML/1998/namespace";
    public const string Xmlns = "http://www.w3.org/2000/xmlns/";
}
