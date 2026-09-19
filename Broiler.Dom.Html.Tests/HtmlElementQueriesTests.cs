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
}
