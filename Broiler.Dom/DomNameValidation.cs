using System;
using System.Collections.Generic;
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
// Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=TBF
// Broiler-Falsified-If: a tag name holding a space followed by more text, such as img onerror, passes ValidateElementName and the name carries attribute text into serialized markup
// Broiler-Human:        PENDING
public static partial class DomNameValidation
{
    /// <summary>
    /// Valid XML Name: a Unicode letter or underscore, followed by Unicode
    /// letters, digits, hyphens, underscores, or dots (XML 1.0 §2.3). Uses
    /// Unicode categories so non-ASCII names such as U+212A (Kelvin sign) are
    /// accepted. Colons are NOT allowed here (see the qualified-name pattern).
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a name that ends in one line feed, such as div followed by a line feed, matches, because $ also matches before a final newline where \z would not
    // Broiler-Human:        PENDING
    [GeneratedRegex(@"^[\p{L}_][\p{L}\p{N}_.\-]*$", RegexOptions.Compiled)]
    private static partial Regex ValidXmlNamePatternRegex();

    /// <summary>
    /// Valid XML QName: either a simple name or <c>prefix:localName</c> where both
    /// parts are valid XML names (a single optional colon).
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a qualified name that ends in one line feed, such as a:b followed by a line feed, matches, because $ also matches before a final newline where \z would not
    // Broiler-Human:        PENDING
    [GeneratedRegex(@"^[\p{L}_][\p{L}\p{N}_.\-]*(?::[\p{L}_][\p{L}\p{N}_.\-]*)?$", RegexOptions.Compiled)]
    private static partial Regex ValidXmlQualifiedNamePatternRegex();

    // Broiler-AI:           Origin=AI; IP=None; Security=High; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: ValidateElementName tests names against an instance other than the pattern ValidXmlNamePatternRegex declares, so it accepts a name that pattern refuses
    // Broiler-Human:        PENDING
    private static readonly Regex ValidXmlNamePattern = ValidXmlNamePatternRegex();
    // Broiler-AI:           Origin=AI; IP=None; Security=High; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: ValidateQualifiedName tests names against an instance other than the pattern ValidXmlQualifiedNamePatternRegex declares, so it accepts a name that pattern refuses
    // Broiler-Human:        PENDING
    private static readonly Regex ValidXmlQualifiedNamePattern = ValidXmlQualifiedNamePatternRegex();

    /// <summary>
    /// Validates an element/doctype name per the XML spec.
    /// Throws a <see cref="DomException"/> with <c>InvalidCharacterError</c> for invalid names.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a name with an interior space, such as img src, returns without InvalidCharacterError
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an unprefixed name such as foo passes in the XMLNS namespace without NamespaceError, because the method returns before its xmlns checks when the name has no colon
    // Broiler-Human:        PENDING
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

    /// <summary>
    /// A valid custom element name (HTML §4.13.1): starts with an ASCII lower alpha, contains a
    /// hyphen, and holds no upper-case letters.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a name that ends in one line feed, such as my-element followed by a line feed, matches, because $ also matches before a final newline where \z would not
    // Broiler-Human:        PENDING
    [GeneratedRegex(@"^[a-z][-._0-9a-z]*-[-._0-9a-z]*$", RegexOptions.Compiled)]
    private static partial Regex ValidCustomElementNamePatternRegex();

    // Broiler-AI:           Origin=AI; IP=None; Security=High; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: one of the eight reserved hyphenated names, such as annotation-xml or missing-glyph, is absent, so IsValidCustomElementName reports it valid
    // Broiler-Human:        PENDING
    private static readonly HashSet<string> ReservedCustomElementNames = new(StringComparer.Ordinal)
    {
        "annotation-xml", "color-profile", "font-face", "font-face-src", "font-face-uri",
        "font-face-format", "font-face-name", "missing-glyph",
    };

    /// <summary>
    /// Determines whether the specified string is a valid custom element name per HTML §4.13.1.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a name holding an upper-case ASCII letter, such as x-Foo, is reported valid
    // Broiler-Human:        PENDING
    public static bool IsValidCustomElementName(string name) =>
        !string.IsNullOrEmpty(name) && !ReservedCustomElementNames.Contains(name) && ValidCustomElementNamePatternRegex().IsMatch(name);
}
