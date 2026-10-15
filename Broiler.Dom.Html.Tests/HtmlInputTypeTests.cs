using Broiler.Dom;
using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// <see cref="HtmlFormQueries.GetInputType"/> maps an input's <c>type</c> attribute to its state
/// keyword (HTML §4.10.5), as the <c>type</c> IDL attribute does: the keyword it matches ASCII
/// case-insensitively, else <c>text</c>.
/// </summary>
/// <remarks>
/// The renderer kept three type lists of its own and HtmlBridge another, each lowercasing the raw
/// attribute. None of them mapped an invalid type to the Text state, so <c>type="foo"</c> was neither
/// a text field nor anything else.
/// </remarks>
public sealed class HtmlInputTypeTests
{
    private static string TypeOf(string? type)
    {
        var input = new DomDocument().CreateElement("input");
        if (type is not null)
            input.SetAttribute("type", type);
        return HtmlFormQueries.GetInputType(input);
    }

    [Theory(Timeout = 600000)]
    [InlineData("hidden")]
    [InlineData("text")]
    [InlineData("search")]
    [InlineData("tel")]
    [InlineData("url")]
    [InlineData("email")]
    [InlineData("password")]
    [InlineData("date")]
    [InlineData("month")]
    [InlineData("week")]
    [InlineData("time")]
    [InlineData("datetime-local")]
    [InlineData("number")]
    [InlineData("range")]
    [InlineData("color")]
    [InlineData("checkbox")]
    [InlineData("radio")]
    [InlineData("file")]
    [InlineData("submit")]
    [InlineData("image")]
    [InlineData("reset")]
    [InlineData("button")]
    public void Every_Keyword_Is_Its_Own_State(string keyword) =>
        Assert.Equal(keyword, TypeOf(keyword));

    [Theory(Timeout = 600000)]
    [InlineData("SUBMIT", "submit")]
    [InlineData("CheckBox", "checkbox")]
    [InlineData("Datetime-Local", "datetime-local")]
    public void Keywords_Match_Ascii_Case_Insensitively(string type, string expected) =>
        Assert.Equal(expected, TypeOf(type));

    [Theory(Timeout = 600000)]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("foo")]
    [InlineData(" checkbox ")]
    [InlineData("datetime")]
    [InlineData("checKbox")]
    public void A_Missing_Or_Invalid_Type_Is_Text(string? type) =>
        Assert.Equal("text", TypeOf(type));
}
