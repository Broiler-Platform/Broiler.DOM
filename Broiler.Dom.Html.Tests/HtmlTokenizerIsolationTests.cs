using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

public sealed class HtmlTokenizerIsolationTests
{
    [Fact]
    public void Interleaved_Inputs_On_One_Tokenizer_Keep_Their_Own_State()
    {
        var tokenizer = new HtmlTokenizer();
        using var first = tokenizer.Tokenize("<script>a < b</script>").GetEnumerator();
        using var second = tokenizer.Tokenize("<p>second</p>").GetEnumerator();

        Assert.True(first.MoveNext());
        Assert.Equal("script", first.Current.Name);
        Assert.True(second.MoveNext());
        Assert.Equal("p", second.Current.Name);
        Assert.True(first.MoveNext());
        Assert.Equal("a < b", first.Current.Data);
        Assert.True(second.MoveNext());
        Assert.Equal("second", second.Current.Data);
        Assert.True(first.MoveNext());
        Assert.Equal("script", first.Current.Name);
        Assert.True(second.MoveNext());
        Assert.Equal("p", second.Current.Name);
    }

    [Fact]
    public void The_Same_Sequence_Can_Be_Enumerated_Concurrently_And_Replayed()
    {
        var sequence = new HtmlTokenizer().Tokenize("<div title='one'>text</div>");
        using var first = sequence.GetEnumerator();
        using var second = sequence.GetEnumerator();
        Assert.True(first.MoveNext());
        Assert.True(first.MoveNext());
        Assert.Equal("text", first.Current.Data);
        Assert.True(second.MoveNext());
        Assert.Equal("one", second.Current.Attributes["title"]);
        Assert.True(first.MoveNext());
        Assert.Equal(TokenType.EndTag, first.Current.Type);
        Assert.True(second.MoveNext());
        Assert.Equal("text", second.Current.Data);
        Assert.Equal(new[] { TokenType.StartTag, TokenType.Character, TokenType.EndTag, TokenType.EndOfFile },
            sequence.Select(token => token.Type));
    }

    [Fact]
    public void Long_Attribute_Names_Keep_Duplicate_And_Empty_Attribute_Behavior()
    {
        var name = new string('a', 4096);
        var token = new HtmlTokenizer().Tokenize($"<div {name}='first' {name}='second' disabled>").First();
        Assert.Equal(2, token.Attributes.Count);
        Assert.Equal("first", token.Attributes[name]);
        Assert.Equal(string.Empty, token.Attributes["disabled"]);
    }
}
