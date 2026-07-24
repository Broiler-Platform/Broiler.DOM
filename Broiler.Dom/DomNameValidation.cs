using System.Text.RegularExpressions;

namespace Broiler.Dom;

/// <summary>
/// Spec-defined DOM element / qualified-name validation (XML 1.0 §2.3 and
/// Namespaces in XML). Throws <see cref="DomException"/> with the appropriate
/// error name (<c>InvalidCharacterError</c> / <c>NamespaceError</c>).
/// </summary>
/// <remarks>
/// Promoted from <c>Broiler.HtmlBridge</c>, which previously carried its own copy
/// of these regexes and rules. The bridge now only marshals the thrown
/// <see cref="DomException"/> into a JavaScript <c>DOMException</c>; the algorithm
/// itself is canonical DOM data-model logic independent of JavaScript identity.
/// </remarks>
public static partial class DomNameValidation
{
    /// <summary>
    /// Valid XML Name: a Unicode letter or underscore, followed by Unicode
    /// letters, digits, hyphens, underscores, or dots (XML 1.0 §2.3). Uses
    /// Unicode categories so non-ASCII names such as U+212A (Kelvin sign) are
    /// accepted. Colons are NOT allowed here (see the qualified-name pattern).
    /// </summary>
    [GeneratedRegex(@"^[\p{L}_][\p{L}\p{N}_.\-]*$", RegexOptions.Compiled)]
    private static partial Regex ValidXmlNamePatternRegex();

    /// <summary>
    /// Valid XML QName: either a simple name or <c>prefix:localName</c> where both
    /// parts are valid XML names (a single optional colon).
    /// </summary>
    [GeneratedRegex(@"^[\p{L}_][\p{L}\p{N}_.\-]*(?::[\p{L}_][\p{L}\p{N}_.\-]*)?$", RegexOptions.Compiled)]
    private static partial Regex ValidXmlQualifiedNamePatternRegex();

    private static readonly Regex ValidXmlNamePattern = ValidXmlNamePatternRegex();
    private static readonly Regex ValidXmlQualifiedNamePattern = ValidXmlQualifiedNamePatternRegex();

    /// <summary>
    /// Validates an element/doctype name per the XML spec.
    /// Throws a <see cref="DomException"/> with <c>InvalidCharacterError</c> for invalid names.
    /// </summary>
    public static void ValidateElementName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Contains('\0') || !ValidXmlNamePattern.IsMatch(name))
        {
            throw DomException.InvalidCharacter(
                $"Failed to execute 'createElement': The tag name provided ('{name}') is not a valid name.");
        }
    }

    /// <summary>
    /// Validates a qualified name and namespace per the Namespaces in XML spec.
    /// Throws a <see cref="DomException"/> with <c>NamespaceError</c> for namespace
    /// violations, or <c>InvalidCharacterError</c> for invalid name characters.
    /// </summary>
    public static void ValidateQualifiedName(string qualifiedName, string? ns)
    {
        // Empty prefix (e.g. ":div") is a NamespaceError.
        if (!string.IsNullOrEmpty(qualifiedName) && qualifiedName.StartsWith(':'))
        {
            throw DomException.Namespace(
                $"Failed to execute 'createElementNS': The qualified name provided ('{qualifiedName}') has an empty prefix.");
        }

        // Trailing colon (e.g. "a:") — empty local name is a NamespaceError.
        if (!string.IsNullOrEmpty(qualifiedName) && qualifiedName.EndsWith(':'))
        {
            throw DomException.Namespace(
                $"Failed to execute 'createElementNS': The qualified name provided ('{qualifiedName}') has an empty local name.");
        }

        // Validate the name characters (allows one optional colon for prefix:localName).
        if (string.IsNullOrEmpty(qualifiedName) || !ValidXmlQualifiedNamePattern.IsMatch(qualifiedName))
        {
            throw DomException.InvalidCharacter(
                $"Failed to execute 'createElementNS': The qualified name provided ('{qualifiedName}') is not a valid name.");
        }

        var colonIndex = qualifiedName.IndexOf(':');
        if (colonIndex < 0)
            return;

        // Prefixed name: the namespace must not be empty.
        if (string.IsNullOrEmpty(ns))
        {
            throw DomException.Namespace(
                $"Failed to execute 'createElementNS': The namespace URI provided is empty for qualified name '{qualifiedName}'.");
        }

        var prefix = qualifiedName[..colonIndex];

        // The "xml" prefix must use the XML namespace.
        if (prefix == "xml" && ns != DomNamespaces.Xml)
        {
            throw DomException.Namespace(
                "Failed to execute 'createElementNS': The namespace URI for prefix 'xml' is invalid.");
        }

        // The "xmlns" prefix must use the XMLNS namespace.
        if (prefix == "xmlns" && ns != DomNamespaces.Xmlns)
        {
            throw DomException.Namespace(
                "Failed to execute 'createElementNS': The namespace URI for prefix 'xmlns' is invalid.");
        }

        // The XMLNS namespace may only be used with the "xmlns" prefix.
        if (prefix != "xmlns" && ns == DomNamespaces.Xmlns)
        {
            throw DomException.Namespace(
                "Failed to execute 'createElementNS': The XMLNS namespace URI may only be used with prefix 'xmlns'.");
        }
    }
}
