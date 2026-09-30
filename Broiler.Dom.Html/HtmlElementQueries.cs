using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Broiler.Dom.Html;

/// <summary>
/// HTML-semantic element tree queries over the canonical DOM — the ordered
/// collections that the HTML specification defines for particular elements
/// (a table's rows, a form's listed controls). Pure tree walks with no host,
/// layout, or JavaScript coupling.
/// </summary>
/// <remarks>
/// Promoted from the HtmlBridge glue layer (its <c>CollectTableRows</c> /
/// <c>CollectFormControls</c> helpers), which now call these canonical queries.
/// </remarks>
// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
// Broiler-Falsified-If: a form holding about 10,000 nested elements overflows the stack in the control walk and ends the process
// Broiler-Human:        PENDING
public static class HtmlElementQueries
{
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Human:        PENDING
    private static IEnumerable<DomElement> ChildElements(DomNode node) =>
        node.ChildNodes.OfType<DomElement>();

    /// <summary>
    /// Collects a table's <c>&lt;tr&gt;</c> elements in <c>HTMLTableElement.rows</c> order:
    /// (1) rows inside <c>&lt;thead&gt;</c>, (2) direct <c>&lt;tr&gt;</c> children and rows
    /// inside <c>&lt;tbody&gt;</c> in tree order, then (3) rows inside <c>&lt;tfoot&gt;</c>.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.1; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a <tr> inside a <tfoot> that precedes the <tbody> is listed ahead of the tbody's rows
    // Broiler-Human:        PENDING
    public static List<DomElement> CollectTableRows(DomElement table)
    {
        var rows = new List<DomElement>();
        // 1. All tr children of thead elements (in tree order)
        foreach (var child in ChildElements(table))
        {
            if (string.Equals(child.TagName, "thead", StringComparison.OrdinalIgnoreCase))
                foreach (var c in ChildElements(child))
                    if (string.Equals(c.TagName, "tr", StringComparison.OrdinalIgnoreCase))
                        rows.Add(c);
        }
        // 2. Direct tr children of the table, or tr children of tbody elements (in tree order)
        foreach (var child in ChildElements(table))
        {
            var ctag = child.TagName.ToLowerInvariant();
            if (ctag == "tr")
                rows.Add(child);
            else if (ctag == "tbody")
                foreach (var c in ChildElements(child))
                    if (string.Equals(c.TagName, "tr", StringComparison.OrdinalIgnoreCase))
                        rows.Add(c);
        }
        // 3. All tr children of tfoot elements (in tree order)
        foreach (var child in ChildElements(table))
        {
            if (string.Equals(child.TagName, "tfoot", StringComparison.OrdinalIgnoreCase))
                foreach (var c in ChildElements(child))
                    if (string.Equals(c.TagName, "tr", StringComparison.OrdinalIgnoreCase))
                        rows.Add(c);
        }
        return rows;
    }

    /// <summary>
    /// Collects the form control elements (<c>input</c>, <c>select</c>, <c>textarea</c>,
    /// <c>button</c>) in a form's subtree, in document order.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: an input inside a nested div is listed after a select that follows that div in document order
    // Broiler-Human:        PENDING
    public static List<DomElement> CollectFormControls(DomElement form)
    {
        var controls = new List<DomElement>();
        CollectFormControlsRecursive(form, controls);
        return controls;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: a form holding about 10,000 nested elements overflows the stack and ends the process
    // Broiler-Human:        PENDING
    private static void CollectFormControlsRecursive(DomElement parent, List<DomElement> controls)
    {
        foreach (var child in ChildElements(parent))
        {
            var ctag = child.TagName.ToLowerInvariant();
            if (ctag == "input" || ctag == "select" || ctag == "textarea" || ctag == "button")
                controls.Add(child);
            CollectFormControlsRecursive(child, controls);
        }
    }

    /// <summary>
    /// The filled fraction (0..1) of a <c>&lt;progress&gt;</c> or <c>&lt;meter&gt;</c> element,
    /// from its <c>value</c>/<c>max</c> (and, for <c>meter</c>, <c>min</c>) content attributes.
    /// <c>min</c> defaults to 0 and <c>max</c> to 1; a <c>max</c> at or below <c>min</c> is
    /// treated as <c>min + 1</c>, and the result is clamped to the range.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a value, min or max spelled NaN or Infinity yields NaN instead of a ratio clamped to 0..1
    // Broiler-Human:        PENDING
    public static double ResolveProgressLikeValueRatio(DomElement element, string tag)
    {
        var min = tag == "meter" ? ReadNumericAttribute(element, "min", 0) : 0;
        var max = ReadNumericAttribute(element, "max", 1);
        if (max <= min)
            max = min + 1;

        var value = ReadNumericAttribute(element, "value", min);
        return Math.Clamp((value - min) / (max - min), 0, 1);
    }

    /// <summary>
    /// Reads a numeric content attribute, returning <paramref name="fallback"/> when the
    /// attribute is absent, blank, or not a valid number.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an attribute value that is not a valid floating-point number, such as NaN or Infinity, is returned as a number instead of the fallback
    // Broiler-Human:        PENDING
    public static double ReadNumericAttribute(DomElement element, string attributeName, double fallback)
    {
        if (element.TryGetAttributeByQualifiedName(attributeName, out var raw))
        {
            return string.IsNullOrWhiteSpace(raw)
                ? fallback
                : double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : fallback;
        }

        return fallback;
    }
}
