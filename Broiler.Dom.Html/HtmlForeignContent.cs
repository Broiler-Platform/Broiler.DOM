using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Broiler.Dom.Html;

/// <summary>
/// What the HTML tree builder needs to build SVG and MathML: the name tables of HTML §13.2.6.1 and
/// §13.2.6.5 ("adjust SVG attributes", "adjust MathML attributes", "adjust foreign attributes", the
/// SVG tag name table and the start tags that break out of foreign content), and the integration
/// point tests of the tree construction dispatcher (§13.2.6).
/// </summary>
/// <remarks>
/// The tokenizer lowercases every tag and attribute name, so each table maps the lowercase name to
/// the one the element carries. A name missing from a table keeps its lowercase spelling, which is
/// also what a browser does: <c>&lt;svg&gt;&lt;myElement&gt;</c> is an SVG <c>myelement</c>.
/// </remarks>
// Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.2.6.5; IP=Low; Security=High; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: an <svg><foreignobject> start tag yields an element whose local name is foreignobject, or an <svg viewbox> attribute one named viewbox
// Broiler-Human:        PENDING
internal static class HtmlForeignContent
{
    /// <summary>
    /// The start tags that end foreign content (§13.2.6.5): each is a parse error there, pops the
    /// open SVG and MathML elements up to an HTML element or an integration point, and is then
    /// processed as HTML. <c>font</c> joins them only with a <c>color</c>, <c>face</c> or
    /// <c>size</c> attribute (<see cref="IsBreakout"/>).
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.2.6.5; IP=None; Security=High; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an <svg><img> start tag inserts an SVG element named img instead of closing the svg and inserting an HTML img after it
    // Broiler-Human:        PENDING
    private static readonly FrozenSet<string> BreakoutStartTags = new[]
    {
        "b", "big", "blockquote", "body", "br", "center", "code", "dd", "div", "dl", "dt", "em", "embed",
        "h1", "h2", "h3", "h4", "h5", "h6", "head", "hr", "i", "img", "li", "listing", "menu", "meta",
        "nobr", "ol", "p", "pre", "ruby", "s", "small", "span", "strong", "strike", "sub", "sup", "table",
        "tt", "u", "ul", "var",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The SVG element names that are not all lowercase (§13.2.6.5, the tag name table).</summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.2.6.5; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a parsed <svg><lineargradient> or <svg><feGaussianBlur> element has a local name other than linearGradient or feGaussianBlur
    // Broiler-Human:        PENDING
    private static readonly FrozenDictionary<string, string> SvgElementNames = CaseTable(
        "altGlyph", "altGlyphDef", "altGlyphItem", "animateColor", "animateMotion", "animateTransform",
        "clipPath", "feBlend", "feColorMatrix", "feComponentTransfer", "feComposite", "feConvolveMatrix",
        "feDiffuseLighting", "feDisplacementMap", "feDistantLight", "feDropShadow", "feFlood", "feFuncA",
        "feFuncB", "feFuncG", "feFuncR", "feGaussianBlur", "feImage", "feMerge", "feMergeNode",
        "feMorphology", "feOffset", "fePointLight", "feSpecularLighting", "feSpotLight", "feTile",
        "feTurbulence", "foreignObject", "glyphRef", "linearGradient", "radialGradient", "textPath");

    /// <summary>The SVG attribute names that are not all lowercase (§13.2.6.1, "adjust SVG attributes").</summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.2.6.1; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a parsed <svg viewbox> or <svg preserveaspectratio> attribute keeps its lowercase name
    // Broiler-Human:        PENDING
    private static readonly FrozenDictionary<string, string> SvgAttributeNames = CaseTable(
        "attributeName", "attributeType", "baseFrequency", "baseProfile", "calcMode", "clipPathUnits",
        "diffuseConstant", "edgeMode", "filterUnits", "glyphRef", "gradientTransform", "gradientUnits",
        "kernelMatrix", "kernelUnitLength", "keyPoints", "keySplines", "keyTimes", "lengthAdjust",
        "limitingConeAngle", "markerHeight", "markerUnits", "markerWidth", "maskContentUnits", "maskUnits",
        "numOctaves", "pathLength", "patternContentUnits", "patternTransform", "patternUnits", "pointsAtX",
        "pointsAtY", "pointsAtZ", "preserveAlpha", "preserveAspectRatio", "primitiveUnits", "refX", "refY",
        "repeatCount", "repeatDur", "requiredExtensions", "requiredFeatures", "specularConstant",
        "specularExponent", "spreadMethod", "startOffset", "stdDeviation", "stitchTiles", "surfaceScale",
        "systemLanguage", "tableValues", "targetX", "targetY", "textLength", "viewBox", "viewTarget",
        "xChannelSelector", "yChannelSelector", "zoomAndPan");

    /// <summary>
    /// The attributes of SVG and MathML elements that are created in a namespace (§13.2.6.1, "adjust
    /// foreign attributes"): the prefix, the local name and the namespace each one gets.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.2.6.1; IP=None; Security=High; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a parsed <svg><use xlink:href> attribute is in no namespace, so GetAttributeNS with the XLink namespace and href misses it
    // Broiler-Human:        PENDING
    private static readonly FrozenDictionary<string, (string? Prefix, string LocalName, string Namespace)> ForeignAttributes =
        new Dictionary<string, (string?, string, string)>(StringComparer.Ordinal)
        {
            ["xlink:actuate"] = ("xlink", "actuate", DomNamespaces.XLink),
            ["xlink:arcrole"] = ("xlink", "arcrole", DomNamespaces.XLink),
            ["xlink:href"] = ("xlink", "href", DomNamespaces.XLink),
            ["xlink:role"] = ("xlink", "role", DomNamespaces.XLink),
            ["xlink:show"] = ("xlink", "show", DomNamespaces.XLink),
            ["xlink:title"] = ("xlink", "title", DomNamespaces.XLink),
            ["xlink:type"] = ("xlink", "type", DomNamespaces.XLink),
            ["xml:lang"] = ("xml", "lang", DomNamespaces.Xml),
            ["xml:space"] = ("xml", "space", DomNamespaces.Xml),
            ["xmlns"] = (null, "xmlns", DomNamespaces.Xmlns),
            ["xmlns:xlink"] = ("xmlns", "xlink", DomNamespaces.Xmlns),
        }.ToFrozenDictionary(StringComparer.Ordinal);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a camel-case name is keyed by itself rather than by its lowercase spelling, so the tokenizer's lowercase name finds no entry
    // Broiler-Human:        PENDING
    private static FrozenDictionary<string, string> CaseTable(params string[] names)
    {
        var table = new Dictionary<string, string>(names.Length, StringComparer.Ordinal);
        foreach (var name in names)
            table[name.ToLowerInvariant()] = name;
        return table.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>Whether <paramref name="element"/> is in the HTML namespace.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=High; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an element in no namespace, or an SVG element, is reported as an HTML element
    // Broiler-Human:        PENDING
    internal static bool IsHtml(DomElement element) =>
        string.Equals(element.NamespaceUri, DomNamespaces.Html, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="element"/> is an HTML element named <paramref name="localName"/>.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=High; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an SVG element whose local name is table is reported as an HTML table
    // Broiler-Human:        PENDING
    internal static bool IsHtml(DomElement element, string localName) =>
        IsHtml(element) && string.Equals(element.LocalName, localName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="element"/> is in the SVG or the MathML namespace.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=High; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an HTML element, or one in no namespace, is reported as an SVG or MathML element
    // Broiler-Human:        PENDING
    internal static bool IsSvgOrMathMl(DomElement element) =>
        string.Equals(element.NamespaceUri, DomNamespaces.Svg, StringComparison.Ordinal) ||
        string.Equals(element.NamespaceUri, DomNamespaces.MathMl, StringComparison.Ordinal);

    /// <summary>
    /// Whether <paramref name="element"/> is a MathML text integration point (§13.2.6): a MathML
    /// <c>mi</c>, <c>mo</c>, <c>mn</c>, <c>ms</c> or <c>mtext</c>, whose text and start tags (other
    /// than <c>mglyph</c> and <c>malignmark</c>) are HTML content.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.2.6; IP=None; Security=High; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an HTML element named mi, outside any math, is reported as a MathML text integration point
    // Broiler-Human:        PENDING
    internal static bool IsMathMlTextIntegrationPoint(DomElement element) =>
        string.Equals(element.NamespaceUri, DomNamespaces.MathMl, StringComparison.Ordinal) &&
        element.LocalName is "mi" or "mo" or "mn" or "ms" or "mtext";

    /// <summary>
    /// Whether <paramref name="element"/> is an HTML integration point (§13.2.6): an SVG
    /// <c>foreignObject</c>, <c>desc</c> or <c>title</c>, or a MathML <c>annotation-xml</c> whose
    /// <c>encoding</c> is <c>text/html</c> or <c>application/xhtml+xml</c> in any ASCII case. Its
    /// text and start tags are HTML content.
    /// </summary>
    /// <remarks>
    /// The Standard decides an <c>annotation-xml</c> from the start tag that created it; the element's
    /// attribute is that start tag's until a script changes it, and no script runs during a parse.
    /// </remarks>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.2.6; IP=None; Security=High; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a MathML annotation-xml with no encoding attribute is reported as an HTML integration point, so a <div> inside it stays HTML instead of breaking out of the math
    // Broiler-Human:        PENDING
    internal static bool IsHtmlIntegrationPoint(DomElement element)
    {
        if (string.Equals(element.NamespaceUri, DomNamespaces.Svg, StringComparison.Ordinal))
            return element.LocalName is "foreignObject" or "desc" or "title";

        return string.Equals(element.NamespaceUri, DomNamespaces.MathMl, StringComparison.Ordinal) &&
               element.LocalName == "annotation-xml" &&
               element.GetAttribute("encoding") is { } encoding &&
               (encoding.Equals("text/html", StringComparison.OrdinalIgnoreCase) ||
                encoding.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The SVG and MathML elements in the "special" category and in the default scope (HTML
    /// §13.2.4.2, "has an element in scope"): the MathML text integration points and
    /// <c>annotation-xml</c>, and the SVG <c>foreignObject</c>, <c>desc</c> and <c>title</c>.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.2.4.2; IP=None; Security=High; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an SVG g or a MathML mrow is reported as a scope boundary, so a </div> outside the svg does not close a div that encloses it
    // Broiler-Human:        PENDING
    internal static bool IsForeignScopeBoundary(DomElement element)
    {
        if (string.Equals(element.NamespaceUri, DomNamespaces.Svg, StringComparison.Ordinal))
            return element.LocalName is "foreignObject" or "desc" or "title";

        return string.Equals(element.NamespaceUri, DomNamespaces.MathMl, StringComparison.Ordinal) &&
               element.LocalName is "mi" or "mo" or "mn" or "ms" or "mtext" or "annotation-xml";
    }

    /// <summary>
    /// Whether a start tag met in foreign content ends it (§13.2.6.5): one of the names in
    /// <see cref="BreakoutStartTags"/>, or a <c>font</c> with a <c>color</c>, <c>face</c> or
    /// <c>size</c> attribute.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.2.6.5; IP=None; Security=High; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an <svg><font> start tag with no attributes closes the svg, or one with a color attribute is inserted as an SVG font element
    // Broiler-Human:        PENDING
    internal static bool IsBreakout(HtmlToken token)
    {
        var name = token.Name ?? string.Empty;
        if (BreakoutStartTags.Contains(name))
            return true;

        return name == "font" &&
               (token.Attributes.ContainsKey("color") || token.Attributes.ContainsKey("face") || token.Attributes.ContainsKey("size"));
    }

    /// <summary>
    /// The element a start tag creates in <paramref name="namespaceUri"/> (§13.2.6.1, "insert a
    /// foreign element", with the adjustments its callers make first): an SVG tag name takes its
    /// camel case, an SVG or MathML attribute name its case, and the <c>xlink:</c>, <c>xml:</c> and
    /// <c>xmlns</c> attributes their namespace. The name is the element's whole local name and is
    /// never split at a colon, as the HTML parser never splits one.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.2.6.1; IP=Low; Security=High; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a foreign start tag named a:b:c throws, or yields an element with a prefix, instead of an element whose local name is a:b:c
    // Broiler-Human:        PENDING
    internal static DomElement CreateElement(DomDocument document, HtmlToken token, string namespaceUri)
    {
        var name = token.Name ?? throw new InvalidOperationException("A start tag must have a name.");
        var svg = string.Equals(namespaceUri, DomNamespaces.Svg, StringComparison.Ordinal);
        if (svg && SvgElementNames.TryGetValue(name, out var adjustedName))
            name = adjustedName;

        var element = document.CreateElement(DomName.CreateLocal(namespaceUri, name));
        foreach (var (attributeName, value) in token.Attributes)
        {
            if (ForeignAttributes.TryGetValue(attributeName, out var foreign))
            {
                element.SetAttributeNS(
                    foreign.Namespace,
                    foreign.Prefix is null ? foreign.LocalName : $"{foreign.Prefix}:{foreign.LocalName}",
                    value);
                continue;
            }

            var adjusted = svg
                ? SvgAttributeNames.GetValueOrDefault(attributeName, attributeName)
                : attributeName == "definitionurl" ? "definitionURL" : attributeName;
            // Set by its exact name: SetAttribute(string, string) would lowercase viewBox again, and
            // SetAttributeNS would split a foreign foo:bar at its colon, where the parser keeps one
            // attribute named foo:bar in no namespace.
            element.SetAttribute(DomName.CreateLocal(adjusted), value);
        }

        return element;
    }
}
