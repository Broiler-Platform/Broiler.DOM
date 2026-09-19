using Xunit;

namespace Broiler.Dom.Html.Tests;

public sealed class HtmlSelectQueriesTests
{
    [Fact(Timeout = 600000)]
    public void GetOptions_Collects_Options_Recursively_Through_Optgroups()
    {
        var doc = new DomDocument();
        var select = doc.CreateElement("select");
        var opt1 = doc.CreateElement("option");
        opt1.SetAttribute("value", "1");
        select.AppendChild(opt1);

        var group = doc.CreateElement("optgroup");
        var opt2 = doc.CreateElement("option");
        opt2.SetAttribute("value", "2");
        group.AppendChild(opt2);
        select.AppendChild(group);

        var opt3 = doc.CreateElement("option");
        opt3.SetAttribute("value", "3");
        select.AppendChild(opt3);

        var options = HtmlSelectQueries.GetOptions(select);
        Assert.Equal(3, options.Count);
        Assert.Same(opt1, options[0]);
        Assert.Same(opt2, options[1]);
        Assert.Same(opt3, options[2]);
    }

    [Fact(Timeout = 600000)]
    public void GetOptionText_Strips_And_Collapses_Whitespace_And_Skips_Script()
    {
        var doc = new DomDocument();
        var option = doc.CreateElement("option");

        // " \n  Two \t\n words\u00a0here \n "
        option.AppendChild(doc.CreateTextNode(" \n  Two \t\n words\u00a0here \n "));
        Assert.Equal("Two words\u00a0here", HtmlSelectQueries.GetOptionText(option));

        // Subtree with nested text and script
        var option2 = doc.CreateElement("option");
        option2.AppendChild(doc.CreateTextNode("Hello "));
        var script = doc.CreateElement("script");
        script.AppendChild(doc.CreateTextNode("ignored code"));
        option2.AppendChild(script);
        var span = doc.CreateElement("span");
        span.AppendChild(doc.CreateTextNode(" World "));
        option2.AppendChild(span);

        Assert.Equal("Hello World", HtmlSelectQueries.GetOptionText(option2));
    }

    [Fact(Timeout = 600000)]
    public void GetOptionValue_Falls_Back_To_Option_Text()
    {
        var doc = new DomDocument();
        var opt1 = doc.CreateElement("option");
        opt1.SetAttribute("value", "custom-val");
        opt1.AppendChild(doc.CreateTextNode("  Label  "));
        Assert.Equal("custom-val", HtmlSelectQueries.GetOptionValue(opt1));

        var opt2 = doc.CreateElement("option");
        opt2.AppendChild(doc.CreateTextNode("  Fallback Label  "));
        Assert.Equal("Fallback Label", HtmlSelectQueries.GetOptionValue(opt2));
    }

    [Fact(Timeout = 600000)]
    public void Selection_And_Value_Resolution_Work_Correctly()
    {
        var doc = new DomDocument();
        var select = doc.CreateElement("select");

        // Empty select
        Assert.Equal(-1, HtmlSelectQueries.ResolveSelectedIndex(select));
        Assert.Equal(string.Empty, HtmlSelectQueries.ResolveSelectValue(select));

        var opt1 = doc.CreateElement("option");
        opt1.SetAttribute("value", "a");
        var opt2 = doc.CreateElement("option");
        opt2.SetAttribute("value", "b");
        opt2.SetAttribute("selected", "");
        var opt3 = doc.CreateElement("option");
        opt3.SetAttribute("value", "c");

        select.AppendChild(opt1);
        select.AppendChild(opt2);
        select.AppendChild(opt3);

        // Option 2 has "selected" attribute
        Assert.Equal(1, HtmlSelectQueries.ResolveSelectedIndex(select));
        Assert.Equal("b", HtmlSelectQueries.ResolveSelectValue(select));

        // Dirty index overrides attribute
        Assert.Equal(2, HtmlSelectQueries.ResolveSelectedIndex(select, dirtyIndex: 2));
        Assert.Equal("c", HtmlSelectQueries.ResolveSelectValue(select, dirtyIndex: 2));

        // Invalid dirty index falls back to -1
        Assert.Equal(-1, HtmlSelectQueries.ResolveSelectedIndex(select, dirtyIndex: 99));

        // FindOptionIndexByValue
        Assert.Equal(0, HtmlSelectQueries.FindOptionIndexByValue(select, "a"));
        Assert.Equal(2, HtmlSelectQueries.FindOptionIndexByValue(select, "c"));
        Assert.Equal(-1, HtmlSelectQueries.FindOptionIndexByValue(select, "nonexistent"));
    }

    [Fact(Timeout = 600000)]
    public void AddOption_And_GetSize_Work_Correctly()
    {
        var doc = new DomDocument();
        var select = doc.CreateElement("select");

        var opt1 = doc.CreateElement("option");
        var opt2 = doc.CreateElement("option");
        var opt3 = doc.CreateElement("option");

        HtmlSelectQueries.AddOption(select, opt1);
        HtmlSelectQueries.AddOption(select, opt3);
        // Insert opt2 before opt3
        HtmlSelectQueries.AddOption(select, opt2, opt3);

        var options = HtmlSelectQueries.GetOptions(select);
        Assert.Equal(3, options.Count);
        Assert.Same(opt1, options[0]);
        Assert.Same(opt2, options[1]);
        Assert.Same(opt3, options[2]);

        // Moving an existing option
        HtmlSelectQueries.AddOption(select, opt1, opt2); // opt1 before opt2
        Assert.Same(opt1, select.ChildNodes[0]);

        // Size attribute
        Assert.Equal(0, HtmlSelectQueries.GetSize(select));
        select.SetAttribute("size", "4");
        Assert.Equal(4, HtmlSelectQueries.GetSize(select));
        select.SetAttribute("size", "-2");
        Assert.Equal(0, HtmlSelectQueries.GetSize(select));
        select.SetAttribute("size", "invalid");
        Assert.Equal(0, HtmlSelectQueries.GetSize(select));
    }
}
