using Xunit;

namespace Broiler.Dom.Html.Tests;

public sealed class HtmlTableOperationsTests
{
    [Fact(Timeout = 600000)]
    public void Caption_Create_Get_And_Delete_Work_Correctly()
    {
        var doc = new DomDocument();
        var table = doc.CreateElement("table");
        doc.AppendChild(table);

        Assert.Null(HtmlTableOperations.GetCaption(table));

        var cap1 = HtmlTableOperations.CreateCaption(table);
        Assert.NotNull(cap1);
        Assert.Equal("caption", cap1.TagName);
        Assert.Same(cap1, table.FirstChild);

        var cap2 = HtmlTableOperations.CreateCaption(table);
        Assert.Same(cap1, cap2);
        Assert.Same(cap1, HtmlTableOperations.GetCaption(table));

        HtmlTableOperations.DeleteCaption(table);
        Assert.Null(HtmlTableOperations.GetCaption(table));
        Assert.Null(table.FirstChild);

        // DeleteCaption when none exists is safe
        HtmlTableOperations.DeleteCaption(table);
    }

    [Fact(Timeout = 600000)]
    public void Section_Create_Get_And_Delete_Work_Correctly()
    {
        var doc = new DomDocument();
        var table = doc.CreateElement("table");
        doc.AppendChild(table);

        Assert.Null(HtmlTableOperations.GetTHead(table));
        Assert.Null(HtmlTableOperations.GetTFoot(table));
        Assert.Empty(HtmlTableOperations.GetTableBodies(table));

        var thead = HtmlTableOperations.CreateTHead(table);
        Assert.Equal("thead", thead.TagName);
        Assert.Same(thead, HtmlTableOperations.GetTHead(table));

        // Calling again returns the existing thead
        Assert.Same(thead, HtmlTableOperations.CreateTHead(table));

        var tfoot = HtmlTableOperations.CreateTFoot(table);
        Assert.Equal("tfoot", tfoot.TagName);
        Assert.Same(tfoot, HtmlTableOperations.GetTFoot(table));

        var tbody1 = HtmlTableOperations.CreateSection(table, "tbody");
        var tbody2 = doc.CreateElement("tbody");
        table.AppendChild(tbody2);

        var bodies = HtmlTableOperations.GetTableBodies(table);
        Assert.Equal(2, bodies.Count);
        Assert.Same(tbody1, bodies[0]);
        Assert.Same(tbody2, bodies[1]);

        HtmlTableOperations.DeleteTHead(table);
        Assert.Null(HtmlTableOperations.GetTHead(table));

        HtmlTableOperations.DeleteTFoot(table);
        Assert.Null(HtmlTableOperations.GetTFoot(table));
    }

    [Fact(Timeout = 600000)]
    public void InsertRow_On_Empty_Table_Creates_TBody()
    {
        var doc = new DomDocument();
        var table = doc.CreateElement("table");
        doc.AppendChild(table);

        var tr = HtmlTableOperations.InsertRow(table, -1);
        Assert.NotNull(tr);
        Assert.Equal("tr", tr.TagName);

        var tbody = table.FirstChild as DomElement;
        Assert.NotNull(tbody);
        Assert.Equal("tbody", tbody.TagName);
        Assert.Same(tbody, tr.ParentElement);
    }

    [Fact(Timeout = 600000)]
    public void InsertRow_And_DeleteRow_With_Spec_Ordering()
    {
        var doc = new DomDocument();
        var table = doc.CreateElement("table");
        doc.AppendChild(table);

        var thead = HtmlTableOperations.CreateTHead(table);
        var r1 = HtmlTableOperations.InsertSectionRow(thead);
        r1.SetAttribute("id", "head-r1");

        var tbody = HtmlTableOperations.CreateSection(table, "tbody");
        var r2 = HtmlTableOperations.InsertSectionRow(tbody);
        r2.SetAttribute("id", "body-r1");

        var tfoot = HtmlTableOperations.CreateTFoot(table);
        var r3 = HtmlTableOperations.InsertSectionRow(tfoot);
        r3.SetAttribute("id", "foot-r1");

        // CollectTableRows: thead -> tbody -> tfoot
        var rows = HtmlElementQueries.CollectTableRows(table);
        Assert.Equal(3, rows.Count);
        Assert.Equal("head-r1", rows[0].GetAttribute("id"));
        Assert.Equal("body-r1", rows[1].GetAttribute("id"));
        Assert.Equal("foot-r1", rows[2].GetAttribute("id"));

        Assert.Equal(0, HtmlTableOperations.GetRowIndex(r1));
        Assert.Equal(1, HtmlTableOperations.GetRowIndex(r2));
        Assert.Equal(2, HtmlTableOperations.GetRowIndex(r3));

        // Section row indices
        Assert.Equal(0, HtmlTableOperations.GetSectionRowIndex(r1));
        Assert.Equal(0, HtmlTableOperations.GetSectionRowIndex(r2));
        Assert.Equal(0, HtmlTableOperations.GetSectionRowIndex(r3));

        // Insert at index 1 (before r2 in tbody)
        var inserted = HtmlTableOperations.InsertRow(table, 1);
        inserted.SetAttribute("id", "inserted");
        Assert.Same(tbody, inserted.ParentElement);
        Assert.Equal(1, HtmlTableOperations.GetRowIndex(inserted));
        Assert.Equal(2, HtmlTableOperations.GetRowIndex(r2));

        // DeleteRow with negative index (-1 deletes last row: foot-r1)
        HtmlTableOperations.DeleteRow(table, -1);
        var remaining = HtmlElementQueries.CollectTableRows(table);
        Assert.Equal(3, remaining.Count);
        Assert.DoesNotContain(r3, remaining);

        // DeleteRow out of bounds throws DomException with IndexSizeError
        var ex = Assert.Throws<DomException>(() => HtmlTableOperations.DeleteRow(table, 10));
        Assert.Equal("IndexSizeError", ex.Name);

        var exNeg = Assert.Throws<DomException>(() => HtmlTableOperations.DeleteRow(table, -10));
        Assert.Equal("IndexSizeError", exNeg.Name);
    }

    [Fact(Timeout = 600000)]
    public void Cell_Operations_Insert_And_Delete_Properly()
    {
        var doc = new DomDocument();
        var tr = doc.CreateElement("tr");

        Assert.True(HtmlTableOperations.IsTableCell(doc.CreateElement("td")));
        Assert.True(HtmlTableOperations.IsTableCell(doc.CreateElement("th")));
        Assert.False(HtmlTableOperations.IsTableCell(doc.CreateElement("div")));

        var td1 = HtmlTableOperations.InsertCell(tr, -1);
        td1.SetAttribute("id", "c1");
        var td2 = HtmlTableOperations.InsertCell(tr, -1);
        td2.SetAttribute("id", "c2");

        var cells = HtmlTableOperations.GetRowCells(tr);
        Assert.Equal(2, cells.Count);
        Assert.Same(td1, cells[0]);
        Assert.Same(td2, cells[1]);

        // Insert at index 0
        var td0 = HtmlTableOperations.InsertCell(tr, 0);
        td0.SetAttribute("id", "c0");
        Assert.Equal(3, HtmlTableOperations.GetRowCells(tr).Count);
        Assert.Same(td0, HtmlTableOperations.GetRowCells(tr)[0]);

        // DeleteCell with negative index (-1 deletes c2)
        HtmlTableOperations.DeleteCell(tr, -1);
        cells = HtmlTableOperations.GetRowCells(tr);
        Assert.Equal(2, cells.Count);
        Assert.Same(td0, cells[0]);
        Assert.Same(td1, cells[1]);

        // Out of bounds DeleteCell
        var ex = Assert.Throws<DomException>(() => HtmlTableOperations.DeleteCell(tr, 5));
        Assert.Equal("IndexSizeError", ex.Name);

        // Out of bounds InsertCell
        var exIns = Assert.Throws<DomException>(() => HtmlTableOperations.InsertCell(tr, 10));
        Assert.Equal("IndexSizeError", exIns.Name);
    }
}
