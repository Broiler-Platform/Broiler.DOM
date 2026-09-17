using Broiler.Dom;
using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// Whitespace between an attribute's name and its <c>=</c> belongs to neither:
/// <c>&lt;base href = "x/"&gt;</c> gives <c>href</c> the value <c>x/</c>. HTML §13.2.5.33 (the
/// attribute name state) reconsumes that whitespace in §13.2.5.34, the after attribute name state,
/// which ignores whitespace, sends <c>=</c> to the before attribute value state, <c>/</c> to the
/// self-closing start tag state and <c>&gt;</c> to emitting the tag, and starts a new attribute on
/// anything else.
/// </summary>
/// <remarks>
/// <para>
/// The tokenizer had no after attribute name state. The attribute name state handed the whitespace
/// to the before attribute name state (§13.2.5.32), which treated the <c>=</c> as the start of the
/// NEXT attribute: it committed <c>href</c> with an empty value, read <c>"x/"</c> under an empty
/// name and dropped it. So the token, the DOM built from it, and every pre-parse scan over the
/// token stream (the script scanner, HtmlBridge's <c>&lt;base&gt;</c> and CSP/refresh
/// <c>&lt;meta&gt;</c> discovery) saw <c>href=""</c>. HtmlBridge worked around it by closing up the
/// whitespace in the source text before tokenizing (<c>HtmlSourceAttributes.CloseSpaceBeforeEquals</c>).
/// </para>
/// <para>
/// The same bug decided which of two duplicate attributes won: <c>&lt;a x = "1" x="2"&gt;</c> committed
/// the first <c>x</c> empty, so the spec's "the first one wins" (duplicate-attribute, checked when
/// the attribute name state is left) kept an empty value. The neighbouring spellings that were
/// already right (a valueless attribute before the next name, a solidus, end of input inside the
/// tag, raw text after a valueless attribute) are pinned here too, because every one of them now
/// passes through the new state.
/// </para>
/// </remarks>
public sealed class WhitespaceBeforeAttributeEqualsTests
{
    private static HtmlToken[] Tokenize(string html) => new HtmlTokenizer().Tokenize(html).ToArray();

    private static HtmlToken Tag(string html) =>
        Tokenize(html).First(token => token.Type is TokenType.StartTag or TokenType.EndTag);

    private static string Describe(HtmlToken token) => token.Type switch
    {
        TokenType.Character => "Character:" + token.Data,
        TokenType.StartTag => "StartTag:" + token.Name,
        TokenType.EndTag => "EndTag:" + token.Name,
        _ => token.Type.ToString(),
    };

    private static string[] Describe(IEnumerable<HtmlToken> tokens) => tokens.Select(Describe).ToArray();

    private static string[] Attributes(HtmlToken token) =>
        token.Attributes.Select(attribute => attribute.Key + "=" + attribute.Value).ToArray();

    // After attribute name state: whitespace (TAB, LF, FF, SPACE) is ignored and '=' goes to the before
    // attribute value state, which ignores whitespace in turn. A CR or CRLF in the source reaches
    // these states as LF, after input stream preprocessing (§13.2.3.5); the CRLF row does not depend
    // on that preprocessing, though, since the tag states' char.IsWhiteSpace would skip a CR as well.
    // The attribute name state lowercases the name. The last row puts whitespace only AFTER the '=',
    // which was already right, as the control.
    [Theory(Timeout = 600000)]
    [InlineData("<base href = \"x/\">")]
    [InlineData("<base href =\"x/\">")]
    [InlineData("<base href\t=\t'x/'>")]
    [InlineData("<base href\f=\f\"x/\">")]
    [InlineData("<base href\n=\n\"x/\">")]
    [InlineData("<base href\r\n=\r\n\"x/\">")]
    [InlineData("<base HREF = \"x/\">")]
    [InlineData("<base href= \"x/\">")]
    public void The_After_Attribute_Name_State_Ignores_Whitespace_Before_The_Equals_Sign(string html)
    {
        var tag = Tag(html);

        Assert.Equal(TokenType.StartTag, tag.Type);
        Assert.Equal("base", tag.Name);
        Assert.Equal(["href=x/"], Attributes(tag));
    }

    // The after attribute name state's '=' leads to the before attribute value state, and from there
    // to the attribute value (unquoted) state, which has no solidus rule: '/' is part of the value,
    // so the tag is not self-closing.
    [Fact(Timeout = 600000)]
    public void An_Unquoted_Value_After_A_Spaced_Equals_Sign_Keeps_Its_Solidus()
    {
        var tag = Tag("<base target=_top href\n=\nx/>");

        Assert.Equal(["target=_top", "href=x/"], Attributes(tag));
        Assert.False(tag.SelfClosing);
    }

    // The before attribute value state has no value to read before '>' (missing-attribute-value): the
    // attribute keeps its empty value and the tag is emitted, whether the '=' was spaced or not.
    [Theory(Timeout = 600000)]
    [InlineData("<a href =>x")]
    [InlineData("<a href = >x")]
    [InlineData("<a href=>x")]
    public void An_Equals_Sign_With_No_Value_Leaves_The_Attribute_Empty(string html)
    {
        var tokens = Tokenize(html);

        Assert.Equal(["StartTag:a", "Character:x", "EndOfFile"], Describe(tokens));
        Assert.Equal(["href="], Attributes(tokens[0]));
    }

    // After attribute name state, anything else: start a new attribute and reconsume in the attribute
    // name state. The attribute before it keeps its empty value, and the new one reads its own '='
    // however it is spaced.
    [Theory(Timeout = 600000)]
    [InlineData("<input disabled class=x>", new[] { "disabled=", "class=x" })]
    [InlineData("<input disabled  class = \"x\">", new[] { "disabled=", "class=x" })]
    [InlineData("<input disabled\nchecked>", new[] { "disabled=", "checked=" })]
    [InlineData("<input disabled >", new[] { "disabled=" })]
    [InlineData("<input disabled>", new[] { "disabled=" })]
    public void A_Valueless_Attribute_Ends_Where_The_Next_Attribute_Name_Starts(string html, string[] attributes)
    {
        var tag = Tag(html);

        Assert.Equal(attributes, Attributes(tag));
        Assert.False(tag.SelfClosing);
    }

    // duplicate-attribute: when the attribute name state is left, a name the tag already has removes
    // the new attribute, so the FIRST value stays. The first x used to be committed empty at its
    // spaced '=', which made the empty value the one that won.
    [Theory(Timeout = 600000)]
    [InlineData("<a x = \"1\" x=\"2\">")]
    [InlineData("<a X = \"1\" x = \"2\">")]
    [InlineData("<a x=\"1\" x = \"2\">")]
    [InlineData("<a x = 1 x = 2>")]
    public void The_First_Of_Two_Duplicate_Attributes_Still_Wins(string html)
    {
        Assert.Equal(["x=1"], Attributes(Tag(html)));
    }

    // After attribute name state '/': the self-closing start tag state, whose '>' sets the flag. Its
    // anything-else rule (unexpected-solidus-in-tag) reconsumes in the before attribute name state as
    // if the solidus were whitespace, so a stray '/' between attributes neither self-closes the tag
    // nor loses an attribute.
    [Theory(Timeout = 600000)]
    [InlineData("<img alt = \"x\" />", new[] { "alt=x" }, true)]
    [InlineData("<br data-a />", new[] { "data-a=" }, true)]
    [InlineData("<br data-a/>", new[] { "data-a=" }, true)]
    [InlineData("<input disabled / class=x>", new[] { "disabled=", "class=x" }, false)]
    [InlineData("<input disabled / class = x>", new[] { "disabled=", "class=x" }, false)]
    public void The_After_Attribute_Name_State_Hands_A_Solidus_To_The_Self_Closing_Start_Tag_State(string html, string[] attributes, bool selfClosing)
    {
        var tag = Tag(html);

        Assert.Equal(attributes, Attributes(tag));
        Assert.Equal(selfClosing, tag.SelfClosing);
    }

    // The tokenizer builds an end tag's attributes with the same states. Emitting an end tag that has
    // attributes is the end-tag-with-attributes parse error, a tokenizer error (§13.2.5), and the
    // tree builder ignores end tag attributes; the token still carries the value.
    [Fact(Timeout = 600000)]
    public void An_End_Tag_Reads_A_Spaced_Attribute_The_Same_Way()
    {
        var tag = Tag("</p class = \"x\">");

        Assert.Equal(TokenType.EndTag, tag.Type);
        Assert.Equal("p", tag.Name);
        Assert.Equal(["class=x"], Attributes(tag));
    }

    // eof-in-tag: the after attribute name state and the attribute value states emit an end-of-file
    // token at the end of the input, so the unfinished tag is never emitted. The before attribute
    // value state has no end-of-file rule of its own: it reconsumes the end of input in the attribute
    // value (unquoted) state, which reports it. This also guards that the new state stops at the end
    // of the input.
    [Theory(Timeout = 600000)]
    [InlineData("a<base href")]
    [InlineData("a<base href ")]
    [InlineData("a<base href\n")]
    [InlineData("a<base href =")]
    [InlineData("a<base href = ")]
    [InlineData("a<base href = \"x/")]
    [InlineData("a<base href = x/")]
    [InlineData("a<base href /")]
    public void End_Of_Input_Inside_A_Tag_Emits_No_Tag(string html)
    {
        Assert.Equal(["Character:a", "EndOfFile"], Describe(Tokenize(html)));
    }

    // Emitting a script or style start tag switches the tokenizer to its text state, and that has to
    // hold when the tag is emitted by the after attribute name state's '>' rule too — which is where
    // "<script async>" and "<style media >" end. Otherwise their bodies would be tokenized as markup.
    [Theory(Timeout = 600000)]
    [InlineData("<script async>if (a<b) {}</script>", "script", "if (a<b) {}")]
    [InlineData("<script async >if (a<b) {}</script>", "script", "if (a<b) {}")]
    [InlineData("<style media >p<b{}</style>", "style", "p<b{}")]
    [InlineData("<script type = \"module\">x<y</script>", "script", "x<y")]
    public void A_Raw_Text_Element_Still_Switches_After_A_Valueless_Or_Spaced_Attribute(string html, string name, string text)
    {
        Assert.Equal(["StartTag:" + name, "Character:" + text, "EndTag:" + name, "EndOfFile"], Describe(Tokenize(html)));
    }

    // The same spelling as the last raw text row, asserted on the value this time.
    [Fact(Timeout = 600000)]
    public void A_Spaced_Type_Attribute_On_A_Script_Keeps_Its_Value()
    {
        Assert.Equal(["type=module"], Attributes(Tag("<script type = \"module\">x<y</script>")));
    }

    // U+00A0 is not ASCII whitespace, so per the Standard it is part of the attribute name ("b\u00A0").
    // Every tag state here uses char.IsWhiteSpace instead (a shared departure), and the after attribute
    // name state must use the SAME predicate the attribute name state leaves on: with any mismatch the
    // two states would hand the character back and forth without consuming it. Only the termination is
    // asserted, not the attribute.
    [Fact(Timeout = 600000)]
    public void A_Non_Ascii_Space_Between_Name_And_Equals_Sign_Still_Ends_The_Tag()
    {
        Assert.Equal(["StartTag:a", "Character:d", "EndOfFile"], Describe(Tokenize("<a b\u00A0=c>d")));
    }

    // Tree construction copies a token's attributes onto the element it inserts: "in head" for base,
    // ordinary element insertion for the p and a, and "after head" for the body start tag, which
    // follows </head> here and inserts the body element with the token's attributes. This builder
    // creates the body up front, so it copies them onto that element instead (CopyAttributes); the
    // "in body" rule that adds missing attributes to an existing body applies only to a second <body>
    // tag. So the DOM reads what the tokenizer committed.
    [Fact(Timeout = 600000)]
    public void Spaced_Attribute_Values_Reach_The_Document()
    {
        var document = HtmlDocumentParser.ParseDocument(
            "<head><base href = \"x/\"></head><body class = \"c\"><p id = \"a\" id=\"b\"><a href\n=\n'y'>z</a></p></body>")
            .Document;

        var baseElement = Assert.Single(document.GetElementsByTagName("base"));
        Assert.Same(document.Head, baseElement.ParentNode);
        Assert.Equal("x/", baseElement.GetAttribute("href"));
        Assert.Equal("c", document.Body!.GetAttribute("class"));
        Assert.Equal("a", Assert.Single(document.GetElementsByTagName("p")).Id);
        var anchor = Assert.Single(document.GetElementsByTagName("a"));
        Assert.Equal("y", anchor.GetAttribute("href"));
        Assert.Equal("z", anchor.TextContent);
    }

    [Fact(Timeout = 600000)]
    public void Spaced_Attribute_Values_Reach_A_Fragment()
    {
        var fragment = HtmlDocumentParser.ParseFragment("<img alt = \"x\" src\t=\tp.png>", "div").Fragment;

        var image = Assert.IsType<DomElement>(Assert.Single(fragment.ChildNodes));
        Assert.Equal("img", image.LocalName);
        Assert.Equal("x", image.GetAttribute("alt"));
        Assert.Equal("p.png", image.GetAttribute("src"));
    }

    // The script scanner reads the same token stream: a module script spelled with spaced '=' is a
    // module with a src, not a classic inline script with an empty body.
    [Fact(Timeout = 600000)]
    public void The_Script_Scanner_Reads_Spaced_Attribute_Values()
    {
        var script = Assert.Single(HtmlScriptScanner.EnumerateScripts("<script type = \"module\" src = 'a.js'></script>"));

        Assert.Equal("module", script.Attributes["type"]);
        Assert.Equal("a.js", script.Attributes["src"]);
        Assert.Equal("", script.RawContent);
    }
}
