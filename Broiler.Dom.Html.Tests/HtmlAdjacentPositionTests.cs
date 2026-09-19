using Xunit;

namespace Broiler.Dom.Html.Tests;

public sealed class HtmlAdjacentPositionTests
{
    [Theory(Timeout = 600000)]
    [InlineData("beforebegin", HtmlAdjacentPosition.BeforeBegin)]
    [InlineData("BEFOREBEGIN", HtmlAdjacentPosition.BeforeBegin)]
    [InlineData("  beforebegin  ", HtmlAdjacentPosition.BeforeBegin)]
    [InlineData("afterbegin", HtmlAdjacentPosition.AfterBegin)]
    [InlineData("AfterBegin", HtmlAdjacentPosition.AfterBegin)]
    [InlineData("beforeend", HtmlAdjacentPosition.BeforeEnd)]
    [InlineData("BeforeEnd", HtmlAdjacentPosition.BeforeEnd)]
    [InlineData("afterend", HtmlAdjacentPosition.AfterEnd)]
    [InlineData("AFTEREND", HtmlAdjacentPosition.AfterEnd)]
    public void TryParse_And_Parse_Succeed_For_Valid_Keywords(string input, HtmlAdjacentPosition expected)
    {
        Assert.True(HtmlAdjacentPositionResolver.TryParse(input, out var result));
        Assert.Equal(expected, result);
        Assert.Equal(expected, HtmlAdjacentPositionResolver.Parse(input));
    }

    [Theory(Timeout = 600000)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("middle")]
    [InlineData("before")]
    [InlineData(null)]
    public void TryParse_Fails_And_Parse_Throws_For_Invalid_Keywords(string? input)
    {
        Assert.False(HtmlAdjacentPositionResolver.TryParse(input, out _));
        var ex = Assert.Throws<DomException>(() => HtmlAdjacentPositionResolver.Parse(input));
        Assert.Equal("SyntaxError", ex.Name);
    }

    [Fact(Timeout = 600000)]
    public void ResolveTarget_Works_For_All_Positions_With_Parent()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var child1 = doc.CreateElement("span");
        var target = doc.CreateElement("p");
        var child2 = doc.CreateElement("b");

        parent.AppendChild(child1);
        parent.AppendChild(target);
        parent.AppendChild(child2);

        // BeforeBegin: target's parent, index of target (1)
        var (pBefore, idxBefore) = HtmlAdjacentPositionResolver.ResolveTarget(target, HtmlAdjacentPosition.BeforeBegin);
        Assert.Same(parent, pBefore);
        Assert.Equal(1, idxBefore);

        // AfterBegin: target itself, index 0
        var (pAfterBegin, idxAfterBegin) = HtmlAdjacentPositionResolver.ResolveTarget(target, HtmlAdjacentPosition.AfterBegin);
        Assert.Same(target, pAfterBegin);
        Assert.Equal(0, idxAfterBegin);

        // BeforeEnd: target itself, index equals target's child count (0)
        var (pBeforeEnd, idxBeforeEnd) = HtmlAdjacentPositionResolver.ResolveTarget(target, HtmlAdjacentPosition.BeforeEnd);
        Assert.Same(target, pBeforeEnd);
        Assert.Equal(0, idxBeforeEnd);

        // AfterEnd: target's parent, index after target (2)
        var (pAfterEnd, idxAfterEnd) = HtmlAdjacentPositionResolver.ResolveTarget(target, HtmlAdjacentPosition.AfterEnd);
        Assert.Same(parent, pAfterEnd);
        Assert.Equal(2, idxAfterEnd);
    }

    [Fact(Timeout = 600000)]
    public void ResolveTarget_Throws_NoModificationAllowedError_When_No_Parent()
    {
        var doc = new DomDocument();
        var detached = doc.CreateElement("div");

        var exBefore = Assert.Throws<DomException>(() =>
            HtmlAdjacentPositionResolver.ResolveTarget(detached, HtmlAdjacentPosition.BeforeBegin));
        Assert.Equal("NoModificationAllowedError", exBefore.Name);

        var exAfter = Assert.Throws<DomException>(() =>
            HtmlAdjacentPositionResolver.ResolveTarget(detached, HtmlAdjacentPosition.AfterEnd));
        Assert.Equal("NoModificationAllowedError", exAfter.Name);

        // AfterBegin and BeforeEnd do not need parent
        var (p1, idx1) = HtmlAdjacentPositionResolver.ResolveTarget(detached, HtmlAdjacentPosition.AfterBegin);
        Assert.Same(detached, p1);
        Assert.Equal(0, idx1);

        var (p2, idx2) = HtmlAdjacentPositionResolver.ResolveTarget(detached, HtmlAdjacentPosition.BeforeEnd);
        Assert.Same(detached, p2);
        Assert.Equal(0, idx2);
    }

    [Fact(Timeout = 600000)]
    public void ResolveParsingContext_Returns_Parent_Or_Element()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var child = doc.CreateElement("span");
        parent.AppendChild(child);

        Assert.Same(parent, HtmlAdjacentPositionResolver.ResolveParsingContext(child, HtmlAdjacentPosition.BeforeBegin));
        Assert.Same(parent, HtmlAdjacentPositionResolver.ResolveParsingContext(child, HtmlAdjacentPosition.AfterEnd));
        Assert.Same(child, HtmlAdjacentPositionResolver.ResolveParsingContext(child, HtmlAdjacentPosition.AfterBegin));
        Assert.Same(child, HtmlAdjacentPositionResolver.ResolveParsingContext(child, HtmlAdjacentPosition.BeforeEnd));

        var detached = doc.CreateElement("p");
        var ex = Assert.Throws<DomException>(() =>
            HtmlAdjacentPositionResolver.ResolveParsingContext(detached, HtmlAdjacentPosition.BeforeBegin));
        Assert.Equal("NoModificationAllowedError", ex.Name);
    }
}
