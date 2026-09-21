using System.Linq;
using Broiler.Dom;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// HTML §13.2.6.4.4: a <c>&lt;template&gt;</c> start tag whose <c>shadowrootmode</c> is not in the
/// none state attaches a shadow root to its intended parent and becomes that shadow root's
/// contents, so the markup between its tags is parsed straight into the shadow tree and the
/// template never reaches the document.
/// </summary>
public sealed class HtmlDeclarativeShadowRootTests
{
    private static readonly HtmlParseOptions Allowed = new(AllowDeclarativeShadowRoots: true);

    private static DomElement ParseHost(string html, HtmlParseOptions? options = null) =>
        HtmlDocumentParser.ParseDocument(html, null, options)
            .Document.Body!.ChildNodes.OfType<DomElement>().Single();

    [Fact(Timeout = 600000)]
    public void A_Declarative_Template_Becomes_A_Shadow_Root_On_Its_Parent()
    {
        var host = ParseHost(
            "<body><div><template shadowrootmode=open><p>shadow</p></template><p>light</p></div></body>",
            Allowed);

        var shadow = host.ShadowRoot;
        Assert.NotNull(shadow);
        Assert.Same(host, shadow!.Host);
        Assert.Equal(DomShadowRootMode.Open, shadow.Mode);
        Assert.Equal("shadow", shadow.ChildNodes.OfType<DomElement>().Single().TextContent);

        // The template itself is never inserted: the host keeps only its light children.
        Assert.Equal("light", host.ChildNodes.OfType<DomElement>().Single().TextContent);
        Assert.DoesNotContain(host.ChildNodes.OfType<DomElement>(), child => child.LocalName == "template");
    }

    [Fact(Timeout = 600000)]
    public void A_Closed_Mode_Root_Is_Attached_But_Not_Exposed_As_ShadowRoot()
    {
        var host = ParseHost("<body><div><template shadowrootmode=closed>x</template></div></body>", Allowed);

        Assert.Null(host.ShadowRoot);
        Assert.NotNull(host.InternalShadowRoot);
        Assert.Equal(DomShadowRootMode.Closed, host.InternalShadowRoot!.Mode);
        Assert.Equal("x", host.InternalShadowRoot.TextContent);
    }

    [Fact(Timeout = 600000)]
    public void All_Four_ShadowRoot_Attributes_Are_Read()
    {
        var host = ParseHost(
            "<body><div><template shadowrootmode=open shadowrootdelegatesfocus " +
            "shadowrootclonable shadowrootserializable></template></div></body>",
            Allowed);

        var shadow = host.ShadowRoot!;
        Assert.True(shadow.DelegatesFocus);
        Assert.True(shadow.Clonable);
        Assert.True(shadow.Serializable);
    }

    [Fact(Timeout = 600000)]
    public void The_Three_Boolean_Attributes_Default_To_False_When_Absent()
    {
        var host = ParseHost("<body><div><template shadowrootmode=open></template></div></body>", Allowed);

        var shadow = host.ShadowRoot!;
        Assert.False(shadow.DelegatesFocus);
        Assert.False(shadow.Clonable);
        Assert.False(shadow.Serializable);
        Assert.Equal(DomSlotAssignmentMode.Named, shadow.SlotAssignment);
    }

    [Theory(Timeout = 600000)]
    [InlineData("OPEN", DomShadowRootMode.Open)]
    [InlineData(" closed ", DomShadowRootMode.Closed)]
    public void The_Mode_Attribute_Is_Matched_Case_Insensitively_And_Trimmed(string mode, DomShadowRootMode expected)
    {
        var host = ParseHost($"<body><div><template shadowrootmode=\"{mode}\"></template></div></body>", Allowed);

        Assert.Equal(expected, host.InternalShadowRoot!.Mode);
    }

    [Fact(Timeout = 600000)]
    public void Without_The_Option_A_Declarative_Template_Stays_An_Ordinary_Template()
    {
        var host = ParseHost("<body><div><template shadowrootmode=open><p>x</p></template></div></body>");

        Assert.Null(host.InternalShadowRoot);
        var template = host.ChildNodes.OfType<DomElement>().Single();
        Assert.Equal("template", template.LocalName);
        Assert.Equal("open", template.GetAttributeByQualifiedName("shadowrootmode"));
        Assert.Equal("x", template.TemplateContents!.TextContent);
    }

    [Theory(Timeout = 600000)]
    [InlineData("")]
    [InlineData("none")]
    [InlineData("auto")]
    public void An_Unrecognised_Mode_Leaves_An_Ordinary_Template(string mode)
    {
        var host = ParseHost($"<body><div><template shadowrootmode=\"{mode}\"><p>x</p></template></div></body>", Allowed);

        Assert.Null(host.InternalShadowRoot);
        Assert.Equal("template", host.ChildNodes.OfType<DomElement>().Single().LocalName);
    }

    [Fact(Timeout = 600000)]
    public void A_Template_Without_The_Attribute_Is_Untouched()
    {
        var host = ParseHost("<body><div><template><p>x</p></template></div></body>", Allowed);

        Assert.Null(host.InternalShadowRoot);
        Assert.Equal("x", host.ChildNodes.OfType<DomElement>().Single().TemplateContents!.TextContent);
    }

    [Fact(Timeout = 600000)]
    public void A_Host_That_Cannot_Take_A_Shadow_Root_Keeps_The_Template_And_Reports_It()
    {
        var result = HtmlDocumentParser.ParseDocument(
            "<body><table><template shadowrootmode=open><p>x</p></template></table></body>", null, Allowed);

        var table = result.Document.GetElementsByTagName("table").Single();
        Assert.Null(table.InternalShadowRoot);
        Assert.Equal("template", table.ChildNodes.OfType<DomElement>().Single().LocalName);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("table", diagnostic.Message);
        Assert.Contains("attachShadow", diagnostic.Message);
    }

    [Fact(Timeout = 600000)]
    public void Only_The_First_Declarative_Template_On_A_Host_Wins()
    {
        var result = HtmlDocumentParser.ParseDocument(
            "<body><div><template shadowrootmode=open>first</template>" +
            "<template shadowrootmode=open>second</template></div></body>",
            null,
            Allowed);

        var host = result.Document.Body!.ChildNodes.OfType<DomElement>().Single();
        Assert.Equal("first", host.ShadowRoot!.TextContent);

        var leftover = host.ChildNodes.OfType<DomElement>().Single();
        Assert.Equal("template", leftover.LocalName);
        Assert.Equal("second", leftover.TemplateContents!.TextContent);
        Assert.Single(result.Diagnostics);
    }

    [Fact(Timeout = 600000)]
    public void A_Declarative_Template_Nests_Inside_A_Shadow_Tree()
    {
        var host = ParseHost(
            "<body><div><template shadowrootmode=open><span>" +
            "<template shadowrootmode=closed>deep</template></span></template></div></body>",
            Allowed);

        var inner = host.ShadowRoot!.ChildNodes.OfType<DomElement>().Single();
        Assert.Equal("span", inner.LocalName);
        Assert.Equal("deep", inner.InternalShadowRoot!.TextContent);
        Assert.Empty(inner.ChildNodes);
    }

    [Fact(Timeout = 600000)]
    public void A_Self_Closing_Declarative_Template_Attaches_Nothing()
    {
        var host = ParseHost("<body><div><template shadowrootmode=open/></div></body>", Allowed);

        Assert.Null(host.InternalShadowRoot);
    }

    [Fact(Timeout = 600000)]
    public void A_Declarative_Template_Is_Not_Serialized_With_Its_Host()
    {
        var host = ParseHost("<body><div><template shadowrootmode=open><p>x</p></template>light</div></body>", Allowed);

        Assert.Equal("<div>light</div>", HtmlSerializer.Serialize(host));
    }

    [Fact(Timeout = 600000)]
    public void A_Fragment_Does_Not_Give_Its_Context_Element_A_Shadow_Root()
    {
        // HTML §13.2.6.4.4 refuses to attach when the adjusted current node is the topmost element
        // in the stack of open elements, which in a fragment is the context element. Attaching there
        // would hang the content off a wrapper the caller never sees, and the fragment would arrive
        // empty.
        var result = HtmlDocumentParser.ParseFragment(
            "<template shadowrootmode=open><p>x</p></template>", "div", Allowed);

        var template = result.Fragment.ChildNodes.OfType<DomElement>().Single();
        Assert.Equal("template", template.LocalName);
        Assert.Equal("x", template.TemplateContents!.TextContent);
    }

    [Fact(Timeout = 600000)]
    public void A_Fragment_Attaches_A_Shadow_Root_Below_The_Context_Element()
    {
        var result = HtmlDocumentParser.ParseFragment(
            "<section><template shadowrootmode=open><p>x</p></template></section>", "div", Allowed);

        var section = result.Fragment.ChildNodes.OfType<DomElement>().Single();
        Assert.Equal("x", section.ShadowRoot!.TextContent);
        Assert.Empty(section.ChildNodes);
    }

    [Fact(Timeout = 600000)]
    public void TryBuildFragment_Leaves_Declarative_Shadow_Roots_Off_By_Default()
    {
        Assert.True(HtmlFragmentParsing.TryBuildFragment(
            "div", "<section><template shadowrootmode=open>x</template></section>", out var innerHtml));
        Assert.Null(innerHtml!.ChildNodes.OfType<DomElement>().Single().InternalShadowRoot);

        Assert.True(HtmlFragmentParsing.TryBuildFragment(
            "div", "<section><template shadowrootmode=open>x</template></section>", Allowed, out var unsafeHtml));
        Assert.Equal("x", unsafeHtml!.ChildNodes.OfType<DomElement>().Single().ShadowRoot!.TextContent);
    }

    [Fact(Timeout = 600000)]
    public void TryBuildFragment_From_A_Context_Element_Takes_The_Same_Option()
    {
        var document = new DomDocument();
        var context = document.CreateElement("div");

        Assert.True(HtmlFragmentParsing.TryBuildFragment(
            context, "<section><template shadowrootmode=closed>x</template></section>", Allowed, out var fragment));

        var section = fragment!.ChildNodes.OfType<DomElement>().Single();
        Assert.Equal(DomShadowRootMode.Closed, section.InternalShadowRoot!.Mode);
    }

    [Fact(Timeout = 600000)]
    public void A_Meta_Color_Scheme_Inside_A_Declarative_Shadow_Tree_Is_Ignored()
    {
        var host = ParseHost(
            "<body><div><template shadowrootmode=open>" +
            "<meta name=\"color-scheme\" content=\"dark\"></template></div></body>",
            Allowed);

        Assert.Null(HtmlMetaScanner.FindMetaColorScheme(host.OwnerDocument));
        Assert.Null(HtmlMetaScanner.FindMetaColorScheme(host.ShadowRoot!));
    }

    [Fact(Timeout = 600000)]
    public void GetElementById_Does_Not_Reach_Into_A_Declarative_Shadow_Tree()
    {
        var result = HtmlDocumentParser.ParseDocument(
            "<body><div><template shadowrootmode=open><span id=inshadow></span></template></div>" +
            "<span id=light></span></body>",
            null,
            Allowed);

        Assert.Null(result.Document.GetElementById("inshadow"));
        Assert.NotNull(result.Document.GetElementById("light"));
        Assert.DoesNotContain(result.Document.GetElementsByTagName("span"), element => element.Id == "inshadow");
    }
}
