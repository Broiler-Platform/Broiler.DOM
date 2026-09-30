using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.Dom.Html;

/// <summary>
/// Pure HTML table DOM operations (HTMLTableElement, HTMLTableSectionElement, and
/// HTMLTableRowElement). Manages table structure, row and cell insertion, deletion, and
/// indices in tree order with no JavaScript or host dependencies.
/// </summary>
// Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
// Broiler-Falsified-If: insertRow() on a table whose only child is an empty thead puts the new row inside that thead instead of in a new tbody
// Broiler-Human:        PENDING
public static class HtmlTableOperations
{
    // -------- Caption Operations --------

    /// <summary>Returns the table's first child &lt;caption&gt; element, or null.</summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.1; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a caption nested inside a tbody, rather than a child of the table, is returned as the table's caption
    // Broiler-Human:        PENDING
    public static DomElement? GetCaption(DomElement table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table.ChildNodes.OfType<DomElement>()
            .FirstOrDefault(c => string.Equals(c.TagName, "caption", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the table's existing &lt;caption&gt;, or creates a new one at the start of the table.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.1; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a table that already has a caption child is given a second caption, or the new caption is not inserted as the table's first child
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.1; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a caption that is not a child of the table, or a later caption child, is removed instead of the first caption child
    // Broiler-Human:        PENDING
    public static void DeleteCaption(DomElement table)
    {
        ArgumentNullException.ThrowIfNull(table);
        GetCaption(table)?.Remove();
    }

    // -------- Section Operations (thead, tfoot, tbody) --------

    /// <summary>Returns the table's first child &lt;thead&gt; element, or null.</summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.1; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a thead nested inside a tbody is returned as the table's thead
    // Broiler-Human:        PENDING
    public static DomElement? GetTHead(DomElement table) => GetSection(table, "thead");

    /// <summary>
    /// Returns the table's existing &lt;thead&gt;, or creates a new one appended to the table.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.1; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a new thead for a table that already has a tbody is appended after it instead of inserted before the first child that is neither a caption nor a colgroup
    // Broiler-Human:        PENDING
    public static DomElement CreateTHead(DomElement table) => CreateSection(table, "thead");

    /// <summary>Removes the table's first child &lt;thead&gt; element, if present.</summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.1; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a thead other than the table's first thead child is removed
    // Broiler-Human:        PENDING
    public static void DeleteTHead(DomElement table) => DeleteSection(table, "thead");

    /// <summary>Returns the table's first child &lt;tfoot&gt; element, or null.</summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.1; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a tfoot nested inside a tbody is returned as the table's tfoot
    // Broiler-Human:        PENDING
    public static DomElement? GetTFoot(DomElement table) => GetSection(table, "tfoot");

    /// <summary>
    /// Returns the table's existing &lt;tfoot&gt;, or creates a new one appended to the table.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.1; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a table that already has a tfoot child is given a second tfoot
    // Broiler-Human:        PENDING
    public static DomElement CreateTFoot(DomElement table) => CreateSection(table, "tfoot");

    /// <summary>Removes the table's first child &lt;tfoot&gt; element, if present.</summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.1; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a tfoot other than the table's first tfoot child is removed
    // Broiler-Human:        PENDING
    public static void DeleteTFoot(DomElement table) => DeleteSection(table, "tfoot");

    /// <summary>Returns the table's first child element matching <paramref name="sectionTag"/>, or null.</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an element matching sectionTag that is a grandchild rather than a child of the table is returned
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a second section is created when the table already has a child whose tag name matches sectionTag in another ASCII case
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an element matching sectionTag other than the table's first such child is removed
    // Broiler-Human:        PENDING
    public static void DeleteSection(DomElement table, string sectionTag)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(sectionTag);
        GetSection(table, sectionTag)?.Remove();
    }

    /// <summary>Returns all direct child &lt;tbody&gt; elements of the table.</summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.1; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a tbody nested inside a thead or another tbody is included in the table's bodies
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.1; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: insertRow() on a table whose only child is an empty thead puts the new row inside that thead instead of in a new tbody
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.1; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: deleteRow(-2) on a table with three rows removes a row instead of throwing IndexSizeError
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.5; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a tr nested inside a cell of the section is included in the section's rows
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.5; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: insertRow(-2) on a table section inserts a row instead of throwing IndexSizeError
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.8; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a tr whose parent tbody sits in a div rather than a table returns an index other than -1
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.8; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a tr whose parent is a div, not a table or a table section, returns 0 instead of -1
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an element named tr or caption is reported as a table cell
    // Broiler-Human:        PENDING
    public static bool IsTableCell(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var tag = element.TagName;
        return string.Equals(tag, "td", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "th", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns direct child cell elements (&lt;td&gt; and &lt;th&gt;) of <paramref name="row"/>.</summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.8; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a td nested inside another cell of the row is included in the row's cells
    // Broiler-Human:        PENDING
    public static IReadOnlyList<DomElement> GetRowCells(DomElement row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.ChildNodes.OfType<DomElement>().Where(IsTableCell).ToList();
    }

    /// <summary>
    /// Inserts a new &lt;td&gt; cell into <paramref name="row"/> per HTMLTableRowElement.insertCell().
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.8; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: insertCell(-2), or an index above the cell count, inserts a cell instead of throwing IndexSizeError
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.9.8; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: deleteCell(-2) on a row of three cells removes a cell instead of throwing IndexSizeError
    // Broiler-Human:        PENDING
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
