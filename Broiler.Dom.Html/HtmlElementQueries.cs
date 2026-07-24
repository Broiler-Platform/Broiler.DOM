using System.Collections.Generic;
using System.Linq;
using Broiler.Dom;

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
public static class HtmlElementQueries
{
    private static IEnumerable<DomElement> ChildElements(DomNode node) =>
        node.ChildNodes.OfType<DomElement>();

    /// <summary>
    /// Collects a table's <c>&lt;tr&gt;</c> elements in <c>HTMLTableElement.rows</c> order:
    /// (1) rows inside <c>&lt;thead&gt;</c>, (2) direct <c>&lt;tr&gt;</c> children and rows
    /// inside <c>&lt;tbody&gt;</c> in tree order, then (3) rows inside <c>&lt;tfoot&gt;</c>.
    /// </summary>
    public static List<DomElement> CollectTableRows(DomElement table)
    {
        var rows = new List<DomElement>();
        // 1. All tr children of thead elements (in tree order)
        foreach (var child in ChildElements(table))
        {
            if (string.Equals(child.TagName, "thead", System.StringComparison.OrdinalIgnoreCase))
                foreach (var c in ChildElements(child))
                    if (string.Equals(c.TagName, "tr", System.StringComparison.OrdinalIgnoreCase))
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
                    if (string.Equals(c.TagName, "tr", System.StringComparison.OrdinalIgnoreCase))
                        rows.Add(c);
        }
        // 3. All tr children of tfoot elements (in tree order)
        foreach (var child in ChildElements(table))
        {
            if (string.Equals(child.TagName, "tfoot", System.StringComparison.OrdinalIgnoreCase))
                foreach (var c in ChildElements(child))
                    if (string.Equals(c.TagName, "tr", System.StringComparison.OrdinalIgnoreCase))
                        rows.Add(c);
        }
        return rows;
    }

    /// <summary>
    /// Collects the form control elements (<c>input</c>, <c>select</c>, <c>textarea</c>,
    /// <c>button</c>) in a form's subtree, in document order.
    /// </summary>
    public static List<DomElement> CollectFormControls(DomElement form)
    {
        var controls = new List<DomElement>();
        CollectFormControlsRecursive(form, controls);
        return controls;
    }

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
}
