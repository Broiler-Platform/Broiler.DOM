using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.Dom.Html;

/// <summary>
/// Pure HTML table DOM operations (HTMLTableElement, HTMLTableSectionElement, and
/// HTMLTableRowElement). Manages table structure, row and cell insertion, deletion, and
/// indices in tree order with no JavaScript or host dependencies.
/// </summary>
public static class HtmlTableOperations
{
    // -------- Caption Operations --------

    /// <summary>Returns the table's first child &lt;caption&gt; element, or null.</summary>
    public static DomElement? GetCaption(DomElement table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table.ChildNodes.OfType<DomElement>()
            .FirstOrDefault(c => string.Equals(c.TagName, "caption", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the table's existing &lt;caption&gt;, or creates a new one at the start of the table.
    /// </summary>
    public static DomElement CreateCaption(DomElement table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var existing = GetCaption(table);
        if (existing != null)
            return existing;

        var caption = table.OwnerDocument.CreateElement("caption");
        table.InsertBefore(caption, table.FirstChild);
        return caption;
    }

    /// <summary>Removes the table's first child &lt;caption&gt; element, if present.</summary>
    public static void DeleteCaption(DomElement table)
    {
        ArgumentNullException.ThrowIfNull(table);
        GetCaption(table)?.Remove();
    }

    // -------- Section Operations (thead, tfoot, tbody) --------

    /// <summary>Returns the table's first child &lt;thead&gt; element, or null.</summary>
    public static DomElement? GetTHead(DomElement table) => GetSection(table, "thead");

    /// <summary>
    /// Returns the table's existing &lt;thead&gt;, or creates a new one appended to the table.
    /// </summary>
    public static DomElement CreateTHead(DomElement table) => CreateSection(table, "thead");

    /// <summary>Removes the table's first child &lt;thead&gt; element, if present.</summary>
    public static void DeleteTHead(DomElement table) => DeleteSection(table, "thead");

    /// <summary>Returns the table's first child &lt;tfoot&gt; element, or null.</summary>
    public static DomElement? GetTFoot(DomElement table) => GetSection(table, "tfoot");

    /// <summary>
    /// Returns the table's existing &lt;tfoot&gt;, or creates a new one appended to the table.
    /// </summary>
    public static DomElement CreateTFoot(DomElement table) => CreateSection(table, "tfoot");

    /// <summary>Removes the table's first child &lt;tfoot&gt; element, if present.</summary>
    public static void DeleteTFoot(DomElement table) => DeleteSection(table, "tfoot");

    /// <summary>Returns the table's first child element matching <paramref name="sectionTag"/>, or null.</summary>
    public static DomElement? GetSection(DomElement table, string sectionTag)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(sectionTag);
        return table.ChildNodes.OfType<DomElement>()
            .FirstOrDefault(c => string.Equals(c.TagName, sectionTag, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the table's first child element matching <paramref name="sectionTag"/>, or creates a new one.
    /// </summary>
    public static DomElement CreateSection(DomElement table, string sectionTag)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(sectionTag);
        var existing = GetSection(table, sectionTag);
        if (existing != null)
            return existing;

        var section = table.OwnerDocument.CreateElement(sectionTag.ToLowerInvariant());
        table.AppendChild(section);
        return section;
    }

    /// <summary>Removes the table's first child element matching <paramref name="sectionTag"/>, if present.</summary>
    public static void DeleteSection(DomElement table, string sectionTag)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(sectionTag);
        GetSection(table, sectionTag)?.Remove();
    }

    /// <summary>Returns all direct child &lt;tbody&gt; elements of the table.</summary>
    public static IReadOnlyList<DomElement> GetTableBodies(DomElement table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table.ChildNodes.OfType<DomElement>()
            .Where(c => string.Equals(c.TagName, "tbody", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    // -------- Table Row Operations --------

    /// <summary>
    /// Inserts a new &lt;tr&gt; into the table per HTMLTableElement.insertRow().
    /// When index is -1 or equal to rows.Count, appends to the last section (or creates a tbody).
    /// </summary>
    public static DomElement InsertRow(DomElement table, int index = -1)
    {
        ArgumentNullException.ThrowIfNull(table);
        var allRows = HtmlElementQueries.CollectTableRows(table);
        if (index < -1 || index > allRows.Count)
            throw DomException.IndexSize($"Index {index} is out of bounds (rows count is {allRows.Count}).");

        var tr = table.OwnerDocument.CreateElement("tr");

        if (allRows.Count == 0 || index == -1 || index == allRows.Count)
        {
            DomElement? lastSection = null;
            for (var i = table.ChildNodes.Count - 1; i >= 0; i--)
            {
                if (table.ChildNodes[i] is not DomElement childElement)
                    continue;
                var ctag = childElement.TagName.ToLowerInvariant();
                if (ctag is "thead" or "tbody" or "tfoot")
                {
                    lastSection = childElement;
                    break;
                }
            }

            if (lastSection == null && allRows.Count == 0)
            {
                var tbody = table.OwnerDocument.CreateElement("tbody");
                table.AppendChild(tbody);
                lastSection = tbody;
            }

            if (lastSection != null)
                lastSection.AppendChild(tr);
            else
                table.AppendChild(tr);
        }
        else
        {
            var refRow = allRows[index];
            var parent = refRow.ParentElement ?? table;
            parent.InsertBefore(tr, refRow);
        }

        return tr;
    }

    /// <summary>
    /// Deletes the row at <paramref name="index"/> per HTMLTableElement.deleteRow().
    /// Negative indices count from the end of the rows collection.
    /// </summary>
    public static void DeleteRow(DomElement table, int index)
    {
        ArgumentNullException.ThrowIfNull(table);
        var allRows = HtmlElementQueries.CollectTableRows(table);
        if (index < 0)
            index = allRows.Count + index;

        if (index < 0 || index >= allRows.Count)
            throw DomException.IndexSize($"Index {index} is out of bounds (rows count is {allRows.Count}).");

        allRows[index].Remove();
    }

    // -------- Section Row Operations --------

    /// <summary>Returns direct child &lt;tr&gt; elements of a section (or table).</summary>
    public static IReadOnlyList<DomElement> GetSectionRows(DomElement section)
    {
        ArgumentNullException.ThrowIfNull(section);
        return section.ChildNodes.OfType<DomElement>()
            .Where(c => string.Equals(c.TagName, "tr", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Inserts a new &lt;tr&gt; into <paramref name="section"/> per HTMLTableSectionElement.insertRow().
    /// </summary>
    public static DomElement InsertSectionRow(DomElement section, int index = -1)
    {
        ArgumentNullException.ThrowIfNull(section);
        var trRows = GetSectionRows(section);
        if (index < -1 || index > trRows.Count)
            throw DomException.IndexSize($"Index {index} is out of bounds (rows count is {trRows.Count}).");

        var tr = section.OwnerDocument.CreateElement("tr");
        if (index == -1 || index == trRows.Count)
            section.AppendChild(tr);
        else
            section.InsertBefore(tr, trRows[index]);

        return tr;
    }

    // -------- Row and Cell Operations --------

    /// <summary>
    /// Resolves the 0-based index of <paramref name="row"/> in its enclosing table's rows collection,
    /// or -1 if the row is not in a table.
    /// </summary>
    public static int GetRowIndex(DomElement row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var tableEl = row.ParentElement;
        if (tableEl != null && (string.Equals(tableEl.TagName, "thead", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(tableEl.TagName, "tbody", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(tableEl.TagName, "tfoot", StringComparison.OrdinalIgnoreCase)))
        {
            tableEl = tableEl.ParentElement;
        }

        if (tableEl == null || !string.Equals(tableEl.TagName, "table", StringComparison.OrdinalIgnoreCase))
            return -1;

        var rows = HtmlElementQueries.CollectTableRows(tableEl);
        return rows.IndexOf(row);
    }

    /// <summary>
    /// Resolves the 0-based index of <paramref name="row"/> among &lt;tr&gt; siblings in its parent
    /// section or table, or -1 if unparented.
    /// </summary>
    public static int GetSectionRowIndex(DomElement row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var section = row.ParentElement;
        if (section == null)
            return -1;

        var idx = 0;
        foreach (var c in section.ChildNodes.OfType<DomElement>())
        {
            if (ReferenceEquals(c, row))
                return idx;
            if (string.Equals(c.TagName, "tr", StringComparison.OrdinalIgnoreCase))
                idx++;
        }

        return -1;
    }

    /// <summary>Returns whether <paramref name="element"/> is a table cell (&lt;td&gt; or &lt;th&gt;).</summary>
    public static bool IsTableCell(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var tag = element.TagName;
        return string.Equals(tag, "td", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "th", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns direct child cell elements (&lt;td&gt; and &lt;th&gt;) of <paramref name="row"/>.</summary>
    public static IReadOnlyList<DomElement> GetRowCells(DomElement row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.ChildNodes.OfType<DomElement>().Where(IsTableCell).ToList();
    }

    /// <summary>
    /// Inserts a new &lt;td&gt; cell into <paramref name="row"/> per HTMLTableRowElement.insertCell().
    /// </summary>
    public static DomElement InsertCell(DomElement row, int index = -1)
    {
        ArgumentNullException.ThrowIfNull(row);
        var cells = GetRowCells(row);
        if (index < -1 || index > cells.Count)
            throw DomException.IndexSize($"Index {index} is out of bounds (cells count is {cells.Count}).");

        var td = row.OwnerDocument.CreateElement("td");
        if (index == -1 || index == cells.Count)
            row.AppendChild(td);
        else
            row.InsertBefore(td, cells[index]);

        return td;
    }

    /// <summary>
    /// Deletes the cell at <paramref name="index"/> per HTMLTableRowElement.deleteCell().
    /// Negative indices count from the end of the cells collection.
    /// </summary>
    public static void DeleteCell(DomElement row, int index)
    {
        ArgumentNullException.ThrowIfNull(row);
        var cells = GetRowCells(row);
        if (index < 0)
            index = cells.Count + index;

        if (index < 0 || index >= cells.Count)
            throw DomException.IndexSize($"Index {index} is out of bounds (cells count is {cells.Count}).");

        cells[index].Remove();
    }
}
