using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// Character-data tokenisation (WHATWG §13.2.5.1 data state). The data state consumes a
/// whole run up to the next <c>&lt;</c> in one step rather than a character at a time, so
/// these pin the run boundaries that rewrite has to preserve: a run is still flushed only
/// at a <c>&lt;</c> or at end-of-file, and a <c>&lt;</c> that turns out not to open a tag
/// is still carried into the following run.
/// </summary>
public sealed class HtmlTokenizerCharacterRunTests
{
    private static string[] CharacterData(string html) =>
        new HtmlTokenizer().Tokenize(html)
            .Where(t => t.Type == TokenType.Character)
            .Select(t => t.Data)
            .ToArray();

    [Fact]
    public void Text_Between_Tags_Is_One_Character_Token()
    {
        Assert.Equal(["hello world"], CharacterData("<p>hello world</p>"));
    }

    [Fact]
    public void Text_Runs_Are_Split_At_Each_Tag()
    {
        Assert.Equal(["before", "inside", "after"],
            CharacterData("before<b>inside</b>after"));
    }

    [Fact]
    public void Trailing_Text_Is_Emitted_At_End_Of_Input()
    {
        Assert.Equal(["tail"], CharacterData("<p>tail"));
    }

    [Fact]
    public void A_Less_Than_That_Opens_No_Tag_Stays_With_The_Following_Text()
    {
        // "<" before a space is not a tag open, so it is character data and joins the run
        // that follows it — the run before it has already been flushed.
        Assert.Equal(["a ", "< b"], CharacterData("a < b"));
    }

    [Fact]
    public void A_Trailing_Less_Than_Is_Character_Data()
    {
        Assert.Equal(["a", "<"], CharacterData("a<"));
    }

    [Fact]
    public void Character_References_Are_Decoded_Per_Run()
    {
        Assert.Equal(["a&b", "c<d"], CharacterData("a&amp;b<i>c&lt;d"));
    }

    [Fact]
    public void Text_Before_A_Comment_Is_Its_Own_Run()
    {
        Assert.Equal(["a", "b"], CharacterData("a<!-- c -->b"));
    }

    [Fact]
    public void Whitespace_Only_Text_Is_Preserved_Verbatim()
    {
        // The collapsible-whitespace crashtests hand the tokenizer a single enormous
        // run of spaces; the tokenizer must hand it back unchanged (white-space
        // processing is layout's job, not the tokenizer's).
        var spaces = new string(' ', 5000);
        Assert.Equal(["\n" + spaces], CharacterData($"<svg></svg>\n{spaces}"));
    }
}
