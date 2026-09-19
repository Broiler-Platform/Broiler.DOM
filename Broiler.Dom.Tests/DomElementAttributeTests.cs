using System;
using System.Linq;

namespace Broiler.Dom.Tests;

public sealed class DomElementAttributeTests
{
    [Fact(Timeout = 600000)]
    public void TryGetAttributeByQualifiedName_Matches_CaseInsensitive()
    {
        var doc = new DomDocument();
        var el = doc.CreateElement("svg");
        el.SetAttribute("viewBox", "0 0 100 100");

        Assert.True(el.TryGetAttributeByQualifiedName("viewBox", out var v1));
        Assert.Equal("0 0 100 100", v1);

        Assert.True(el.TryGetAttributeByQualifiedName("VIEWBOX", out var v2));
        Assert.Equal("0 0 100 100", v2);

        Assert.True(el.TryGetAttributeByQualifiedName("viewbox", out var v3));
        Assert.Equal("0 0 100 100", v3);

        Assert.False(el.TryGetAttributeByQualifiedName("nonexistent", out var absent));
        Assert.Equal(string.Empty, absent);
    }

    [Fact(Timeout = 600000)]
    public void GetAttributeByQualifiedName_And_HasAttributeByQualifiedName()
    {
        var doc = new DomDocument();
        var el = doc.CreateElement("div");
        el.SetAttribute("data-state", "active");

        Assert.True(el.HasAttributeByQualifiedName("DATA-STATE"));
        Assert.False(el.HasAttributeByQualifiedName("data-missing"));

        Assert.Equal("active", el.GetAttributeByQualifiedName("DATA-STATE"));
        Assert.Null(el.GetAttributeByQualifiedName("data-missing"));
    }

    [Fact(Timeout = 600000)]
    public void SetAttributeByQualifiedName_Updates_Existing_Namespaced_Attribute_Preserving_Namespace()
    {
        var doc = new DomDocument();
        var el = doc.CreateElementNS(DomNamespaces.Svg, "use");
        const string xlinkNs = "http://www.w3.org/1999/xlink";
        el.SetAttributeNS(xlinkNs, "xlink:href", "#initial");

        el.SetAttributeByQualifiedName("XLINK:HREF", "#updated");

        Assert.Equal("#updated", el.GetAttributeNS(xlinkNs, "href"));
        Assert.True(el.TryGetAttributeNS(xlinkNs, "href", out var qName, out var val));
        Assert.Equal("xlink:href", qName);
        Assert.Equal("#updated", val);
    }

    [Fact(Timeout = 600000)]
    public void SetAttributeByQualifiedName_Updates_Existing_Unprefixed_Colon_Attribute()
    {
        var doc = new DomDocument();
        var el = doc.CreateElement("div");
        // setAttribute stores literally with no namespace
        el.SetAttribute("xlink:href", "#first");

        el.SetAttributeByQualifiedName("XLINK:HREF", "#second");

        Assert.Equal("#second", el.GetAttribute("xlink:href"));
        Assert.Null(el.GetAttributeNS("http://www.w3.org/1999/xlink", "href"));
    }

    [Fact(Timeout = 600000)]
    public void SetAttributeByQualifiedName_Creates_New_Attribute_When_Absent()
    {
        var doc = new DomDocument();
        var el = doc.CreateElement("div");

        el.SetAttributeByQualifiedName("title", "hello");

        Assert.Equal("hello", el.GetAttribute("title"));
        Assert.True(el.HasAttributeByQualifiedName("TITLE"));
    }

    [Fact(Timeout = 600000)]
    public void RemoveAttributeByQualifiedName_Removes_CaseInsensitively()
    {
        var doc = new DomDocument();
        var el = doc.CreateElementNS(DomNamespaces.Svg, "use");
        const string xlinkNs = "http://www.w3.org/1999/xlink";
        el.SetAttributeNS(xlinkNs, "xlink:href", "#target");

        Assert.True(el.RemoveAttributeByQualifiedName("XLINK:HREF"));
        Assert.False(el.HasAttributeByQualifiedName("xlink:href"));
        Assert.Null(el.GetAttributeNS(xlinkNs, "href"));
        Assert.False(el.RemoveAttributeByQualifiedName("XLINK:HREF"));
    }

    [Fact(Timeout = 600000)]
    public void TryGetAttributeNS_Resolves_QualifiedName_And_Value()
    {
        var doc = new DomDocument();
        var el = doc.CreateElement("div");
        const string customNs = "https://example.com/ns";
        el.SetAttributeNS(customNs, "p:custom", "42");
        el.SetAttribute("localOnly", "100");

        Assert.True(el.TryGetAttributeNS(customNs, "custom", out var qn1, out var v1));
        Assert.Equal("p:custom", qn1);
        Assert.Equal("42", v1);

        // Normalized null or empty string for no namespace
        Assert.True(el.TryGetAttributeNS(null, "localonly", out var qn2, out var v2));
        Assert.Equal("localonly", qn2);
        Assert.Equal("100", v2);

        Assert.True(el.TryGetAttributeNS(string.Empty, "localonly", out var qn3, out var v3));
        Assert.Equal("localonly", qn3);
        Assert.Equal("100", v3);

        Assert.False(el.TryGetAttributeNS(customNs, "missing", out var qn4, out var v4));
        Assert.Equal(string.Empty, qn4);
        Assert.Equal(string.Empty, v4);
    }

    [Fact(Timeout = 600000)]
    public void AttributeQualifiedNames_Enumerates_All_Attributes()
    {
        var doc = new DomDocument();
        var el = doc.CreateElement("div");
        el.SetAttribute("id", "main");
        el.SetAttributeNS("http://www.w3.org/1999/xlink", "xlink:href", "#icon");

        var names = el.AttributeQualifiedNames.ToList();

        Assert.Contains("id", names);
        Assert.Contains("xlink:href", names);
        Assert.Equal(2, names.Count);
    }
}
