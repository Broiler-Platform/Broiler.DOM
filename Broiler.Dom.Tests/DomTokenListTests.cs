namespace Broiler.Dom.Tests;

public sealed class DomTokenListTests
{
    private static (DomElement Element, DomTokenList Tokens) NewList(string? initial = null)
    {
        var document = new DomDocument();
        var element = document.CreateElement("div");
        document.AppendChild(element);
        if (initial is not null)
            element.SetAttribute("class", initial);
        return (element, new DomTokenList(element, "class"));
    }

    [Fact]
    public void Parse_Splits_On_Ascii_Whitespace_And_Dedupes_In_Order()
    {
        var (_, tokens) = NewList("a  b\tc\nb a");
        Assert.Equal(["a", "b", "c"], tokens.ToList());
        Assert.Equal(3, tokens.Length);
        Assert.Equal("a", tokens.Item(0));
        Assert.Equal("c", tokens.Item(2));
        Assert.Null(tokens.Item(3));
    }

    [Fact]
    public void Contains_Reflects_Membership()
    {
        var (_, tokens) = NewList("red blue");
        Assert.True(tokens.Contains("red"));
        Assert.False(tokens.Contains("green"));
        Assert.False(tokens.Contains(""));
    }

    [Fact]
    public void Add_Appends_New_Tokens_And_Syncs_Attribute()
    {
        var (element, tokens) = NewList("a");
        tokens.Add("b", "c", "a"); // "a" already present
        Assert.Equal("a b c", element.GetAttribute("class"));
        Assert.Equal(["a", "b", "c"], tokens.ToList());
    }

    [Fact]
    public void Remove_Drops_Tokens_And_Syncs_Attribute()
    {
        var (element, tokens) = NewList("a b c");
        tokens.Remove("b", "missing");
        Assert.Equal("a c", element.GetAttribute("class"));
    }

    [Theory]
    [InlineData("a b", "a", null, false, "b")]      // present, no force -> removed
    [InlineData("b", "a", null, true, "b a")]       // absent, no force -> added
    [InlineData("a b", "a", true, true, "a b")]     // force true keeps present
    [InlineData("a b", "a", false, false, "b")]     // force false removes
    [InlineData("b", "a", false, false, "b")]       // absent + force false -> stays absent
    [InlineData("b", "a", true, true, "b a")]       // absent + force true -> added
    public void Toggle_Follows_The_Force_Rules(string initial, string token, bool? force, bool expectedReturn, string expectedClass)
    {
        var (element, tokens) = NewList(initial);
        Assert.Equal(expectedReturn, tokens.Toggle(token, force));
        Assert.Equal(expectedClass, element.GetAttribute("class"));
    }

    [Fact]
    public void Replace_Swaps_A_Present_Token_Preserving_Position()
    {
        var (element, tokens) = NewList("a b c");
        Assert.True(tokens.Replace("b", "x"));
        Assert.Equal("a x c", element.GetAttribute("class"));
    }

    [Fact]
    public void Replace_Returns_False_When_Token_Absent()
    {
        var (element, tokens) = NewList("a b");
        Assert.False(tokens.Replace("missing", "x"));
        Assert.Equal("a b", element.GetAttribute("class"));
    }

    [Fact]
    public void Replace_Dedupes_When_New_Token_Already_Present()
    {
        var (element, tokens) = NewList("a b c");
        Assert.True(tokens.Replace("a", "c"));
        Assert.Equal("b c", element.GetAttribute("class"));
    }

    [Fact]
    public void Value_Get_And_Set_Reflect_The_Raw_Attribute()
    {
        var (element, tokens) = NewList("a b");
        Assert.Equal("a b", tokens.Value);
        tokens.Value = "x y";
        Assert.Equal("x y", element.GetAttribute("class"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("a\tb")]
    public void Mutations_Reject_Empty_Or_Whitespace_Tokens(string token)
    {
        var (_, tokens) = NewList("x");
        Assert.Throws<ArgumentException>(() => tokens.Add(token));
        Assert.Throws<ArgumentException>(() => tokens.Remove(token));
        Assert.Throws<ArgumentException>(() => tokens.Toggle(token));
        Assert.Throws<ArgumentException>(() => tokens.Replace("x", token));
    }

    [Fact]
    public void No_Op_Add_Does_Not_Rewrite_The_Attribute()
    {
        var (element, tokens) = NewList("a b");
        tokens.Add("a"); // already present
        Assert.Equal("a b", element.GetAttribute("class"));
    }
}
