using Xunit;

namespace Broiler.Dom.Html.Tests;

public sealed class HtmlElementQueriesTests
{
    [Fact(Timeout = 600000)]
    public void ReadNumericAttribute_Queries_QualifiedName_CaseInsensitively()
    {
        var doc = new DomDocument();
        var el = doc.CreateElement("progress");
        el.SetAttribute("MAX", "200");

        var result = HtmlElementQueries.ReadNumericAttribute(el, "max", 100);
        Assert.Equal(200, result);

        var fallback = HtmlElementQueries.ReadNumericAttribute(el, "min", 0);
        Assert.Equal(0, fallback);
    }

    [Theory(Timeout = 600000)]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e400")]
    [InlineData("abc")]
    [InlineData("  ")]
    public void ReadNumericAttribute_Takes_The_Fallback_For_A_Value_That_Is_Not_A_Finite_Number(string raw)
    {
        var doc = new DomDocument();
        var el = doc.CreateElement("meter");
        el.SetAttribute("value", raw);

        Assert.Equal(7, HtmlElementQueries.ReadNumericAttribute(el, "value", 7));
    }

    [Theory(Timeout = 600000)]
    [InlineData("meter", "NaN", null, null, 0)]
    [InlineData("progress", "Infinity", null, null, 0)]
    [InlineData("meter", "0.5", null, "NaN", 0.5)]
    [InlineData("meter", "5", "Infinity", "10", 0.5)]
    [InlineData("progress", "3", null, "4", 0.75)]
    [InlineData("progress", "9", null, "4", 1)]
    [InlineData("meter", "1e308", "-1e308", "1e308", 0)]
    public void ResolveProgressLikeValueRatio_Is_Always_A_Number_In_Range(string tag, string value, string? min, string? max, double expected)
    {
        var doc = new DomDocument();
        var el = doc.CreateElement(tag);
        el.SetAttribute("value", value);
        if (min is not null)
            el.SetAttribute("min", min);
        if (max is not null)
            el.SetAttribute("max", max);

        Assert.Equal(expected, HtmlElementQueries.ResolveProgressLikeValueRatio(el, tag), 10);
    }
}
