using System.Text;
using Broiler.Dom;
using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// Elements that contain only text (HTML §13.2.6.2) switch the tokenizer out of the data state, so
/// what follows their start tag is text up to their own end tag, never markup. Title and textarea
/// switch to the RCDATA state (§13.2.5.2), which decodes character references; style, xmp, iframe,
/// noembed, noframes and (with scripting enabled) noscript switch to the RAWTEXT state (§13.2.5.3),
/// which does not; plaintext switches to the PLAINTEXT state (§13.2.5.5), which never ends. Script
/// has its own script data state; its escape states are not modelled, so it reads like RAWTEXT here.
/// </summary>
/// <remarks>
/// <para>
/// Only script, style and noscript used to switch. Everything inside a title, textarea, xmp,
/// iframe, noembed, noframes or plaintext was tokenized as markup, so a <c>&lt;base&gt;</c> or
/// <c>&lt;script&gt;</c> spelled in a textarea's text became an element of the document, a script
/// the host would find and run, and a base URL the pre-parse scans would honour. Browsers read all
/// of it as text. The references in xmp, iframe, noembed and noframes text were decoded as well,
/// which made serializing and re-parsing them unstable.
/// </para>
/// <para>
/// Leaving the state is decided by the "less-than sign", "end tag open" and "end tag name" states of
/// RCDATA (§13.2.5.9–11) and RAWTEXT (§13.2.5.12–14): only an appropriate end tag, one whose name
/// is the start tag's in any ASCII case and is followed by tab, LF, FF, space, <c>/</c> or
/// <c>&gt;</c>, leaves; every other <c>&lt;</c> is text, including an end tag cut off by the end
/// of the input.
/// </para>
/// <para>
/// The tokenizer still decides this by tag name, standing in for the tree builder that makes the
/// switch in the Standard, and it still does not switch on a self-closing start tag. The first is
/// what keeps the token stream right for scanners with no tree builder behind them, and is wrong
/// only inside SVG and MathML; the second keeps <c>&lt;svg&gt;&lt;title/&gt;</c> from swallowing
/// the page. Neither departure is pinned here, only the cases where the two agree with the spec.
/// </para>
/// </remarks>
public sealed class ElementsThatContainOnlyTextTests
{
    // Adjacent character tokens are merged: how a run of text is split into tokens is not what
    // these tests are about, and the Standard emits one token per code point anyway.
    private static string[] Tokens(string html)
    {
        var described = new List<string>();
        var text = new StringBuilder();
        foreach (var token in new HtmlTokenizer().Tokenize(html))
        {
            if (token.Type == TokenType.Character)
            {
                text.Append(token.Data);
                continue;
            }

            if (text.Length > 0)
            {
                described.Add("Character:" + text);
                text.Clear();
            }

            described.Add(token.Type switch
            {
                TokenType.StartTag => "StartTag:" + token.Name + (token.SelfClosing ? "/" : ""),
                TokenType.EndTag => "EndTag:" + token.Name,
                TokenType.Comment => "Comment:" + token.Data,
                TokenType.Doctype => "Doctype:" + token.Name,
                _ => token.Type.ToString(),
            });
        }

        if (text.Length > 0)
            described.Add("Character:" + text);
        return described.ToArray();
    }

    private static HtmlDocumentParseResult Parse(string html) => HtmlDocumentParser.ParseDocument(html);

    private static DomElement Single(DomNode root, string localName) =>
        Assert.Single(root.Descendants().OfType<DomElement>(), element => element.LocalName == localName);

    private static string OnlyText(DomNode parent) => Assert.IsType<DomText>(Assert.Single(parent.ChildNodes)).Data;

    // RCDATA less-than sign state (§13.2.5.9): '<' followed by anything but '/' is a character, so
    // "<base href=x>" never reaches the tag open state.
    [Fact(Timeout = 600000)]
    public void A_Base_Inside_A_Textarea_Is_Text_In_The_Rcdata_State()
    {
        Assert.Equal(
            ["StartTag:textarea", "Character:<base href=x>", "EndTag:textarea", "EndOfFile"],
            Tokens("<textarea><base href=x></textarea>"));
    }

    // RCDATA state (§13.2.5.2): '&' goes to the character reference state, so references are
    // decoded; '<' does not open a tag, so "<i>" stays text — decoded ones included.
    [Theory(Timeout = 600000)]
    [InlineData("title")]
    [InlineData("textarea")]
    public void The_Rcdata_State_Decodes_Character_References_But_Reads_No_Markup(string tag)
    {
        Assert.Equal(
            [$"StartTag:{tag}", "Character:a & <b> <i>&</i>", $"EndTag:{tag}", "EndOfFile"],
            Tokens($"<{tag}>a &amp; &lt;b&gt; <i>&amp;</i></{tag}>"));
    }

    // RAWTEXT state (§13.2.5.3): no character reference branch, so "&amp;" and "&lt;" stay as
    // written, and no tag open either. Script data (§13.2.5.4) agrees for this input.
    [Theory(Timeout = 600000)]
    [InlineData("style")]
    [InlineData("xmp")]
    [InlineData("iframe")]
    [InlineData("noembed")]
    [InlineData("noframes")]
    [InlineData("noscript")]
    [InlineData("script")]
    public void The_Rawtext_State_Leaves_References_And_Markup_Literal(string tag)
    {
        Assert.Equal(
            [$"StartTag:{tag}", "Character:a &amp; <i>&lt;</i>", $"EndTag:{tag}", "EndOfFile"],
            Tokens($"<{tag}>a &amp; <i>&lt;</i></{tag}>"));
    }

    // PLAINTEXT state (§13.2.5.5): no '<' branch, no '&' branch, no end tag. The rest of the input
    // is text, "</plaintext>" included.
    [Fact(Timeout = 600000)]
    public void The_Plaintext_State_Swallows_The_Rest_Of_The_Input_Including_Its_Own_End_Tag()
    {
        Assert.Equal(
            ["StartTag:plaintext", "Character:a &amp; <b></plaintext><p>x", "EndOfFile"],
            Tokens("<plaintext>a &amp; <b></plaintext><p>x"));
    }

    // End tag open (§13.2.5.10/13): anything but an ASCII alpha after "</" is text ("</ textarea>").
    // End tag name (§13.2.5.11/14): a name that is not the start tag's ("</textareax>", "</title>",
    // "</xmp-x>", "</noframe>", a non-ASCII letter), or a terminator that is not tab, LF, FF, space,
    // '/' or '>' (U+000B, U+00A0), is "anything else": "</" and the name are emitted as text. Only
    // ASCII upper alphas are lowercased, so U+017F LATIN SMALL LETTER LONG S, whose uppercase is
    // 'S', matches no "style". The script row is the script data end tag name state (§13.2.5.17),
    // same rule.
    [Theory(Timeout = 600000)]
    [InlineData("textarea", "</textareax>")]
    [InlineData("textarea", "</ textarea>")]
    [InlineData("textarea", "</title>")]
    [InlineData("title", "</textarea>")]
    [InlineData("xmp", "</xmp-x>")]
    [InlineData("noframes", "</noframe>")]
    [InlineData("textarea", "</textareaé>")]
    [InlineData("textarea", "a</textarea\v>b")]
    [InlineData("iframe", "a</iframe\u00A0>b")]
    [InlineData("style", "a</\u017Ftyle>b")]
    [InlineData("script", "a</script\v>b")]
    public void Only_An_Appropriate_End_Tag_Leaves_The_Text_State(string tag, string text)
    {
        Assert.Equal(
            [$"StartTag:{tag}", "Character:" + text, $"EndTag:{tag}", "EndOfFile"],
            Tokens($"<{tag}>{text}</{tag}>"));
    }

    // RCDATA/RAWTEXT end tag name state (§13.2.5.11/14): an ASCII upper alpha appends its lowercase
    // form, and tab, LF, FF, space, '/' and '>' end the name of an appropriate end tag. The token
    // is named in lower case, and the data state resumes after its '>'.
    [Theory(Timeout = 600000)]
    [InlineData("<textarea>x</TEXTAREA><b>y</b>", "textarea")]
    [InlineData("<textarea>x</TextArea ><b>y</b>", "textarea")]
    [InlineData("<textarea>x</textarea/><b>y</b>", "textarea")]
    [InlineData("<textarea>x</textarea\t><b>y</b>", "textarea")]
    [InlineData("<textarea>x</textarea\n><b>y</b>", "textarea")]
    [InlineData("<textarea>x</textarea\r\n><b>y</b>", "textarea")]
    [InlineData("<textarea>x</textarea\f><b>y</b>", "textarea")]
    [InlineData("<textarea>x</textarea x=y><b>y</b>", "textarea")]
    [InlineData("<TITLE>x</title><b>y</b>", "title")]
    [InlineData("<XMP>x</xMp><b>y</b>", "xmp")]
    public void An_Appropriate_End_Tag_Leaves_In_Any_Ascii_Case_After_Any_Terminator(string html, string tag)
    {
        Assert.Equal(
            [$"StartTag:{tag}", "Character:x", $"EndTag:{tag}", "StartTag:b", "Character:y", "EndTag:b", "EndOfFile"],
            Tokens(html));
    }

    // End of input inside the end tag open or end tag name state is "anything else": "</" and the
    // buffered name are emitted as characters, then the end-of-file token (§13.2.5.10–14). The
    // partial "</title" used to be dropped with the tag the data state thought it was reading.
    [Theory(Timeout = 600000)]
    [InlineData("title", "a</title")]
    [InlineData("textarea", "a</textare")]
    [InlineData("xmp", "a</xm")]
    [InlineData("iframe", "a</")]
    [InlineData("noembed", "a<")]
    public void An_End_Tag_Cut_Off_By_The_End_Of_The_Input_Is_Text(string tag, string text)
    {
        Assert.Equal([$"StartTag:{tag}", "Character:" + text, "EndOfFile"], Tokens($"<{tag}>{text}"));
    }

    // RAWTEXT and RCDATA less-than sign states (§13.2.5.9/12) have no '!' or '?' branch: a comment,
    // CDATA section, DOCTYPE or processing instruction is text, and a "</xmp>" inside what looks like
    // a comment still ends the element.
    [Theory(Timeout = 600000)]
    [InlineData("<xmp><!-- </xmp> --></xmp>",
        new[] { "StartTag:xmp", "Character:<!-- ", "EndTag:xmp", "Character: -->", "EndTag:xmp", "EndOfFile" })]
    [InlineData("<xmp><![CDATA[x]]><?pi?><!DOCTYPE html></xmp>",
        new[] { "StartTag:xmp", "Character:<![CDATA[x]]><?pi?><!DOCTYPE html>", "EndTag:xmp", "EndOfFile" })]
    [InlineData("<textarea><!--</textarea>-->",
        new[] { "StartTag:textarea", "Character:<!--", "EndTag:textarea", "Character:-->", "EndOfFile" })]
    public void The_Text_States_Have_No_Comments_Or_Markup_Declarations(string html, string[] expected)
    {
        Assert.Equal(expected, Tokens(html));
    }

    // Every state that emits a start tag — the tag name state, the before and after attribute name
    // states, the unquoted value state and the after quoted value state — must leave the tokenizer
    // in the element's text state rather than the data state.
    [Theory(Timeout = 600000)]
    [InlineData("<textarea><i></textarea>")]
    [InlineData("<textarea ><i></textarea>")]
    [InlineData("<textarea rows><i></textarea>")]
    [InlineData("<textarea rows ><i></textarea>")]
    [InlineData("<textarea rows=2><i></textarea>")]
    [InlineData("<textarea rows=\"2\"><i></textarea>")]
    [InlineData("<textarea rows='2'  ><i></textarea>")]
    [InlineData("<textarea rows = 2><i></textarea>")]
    public void Every_Start_Tag_Path_Switches_To_The_Text_State(string html)
    {
        Assert.Equal(["StartTag:textarea", "Character:<i>", "EndTag:textarea", "EndOfFile"], Tokens(html));
    }

    [Theory(Timeout = 600000)]
    [InlineData("<plaintext x><i>")]
    [InlineData("<plaintext x = 'y'><i>")]
    public void Every_Start_Tag_Path_Switches_To_The_Plaintext_State(string html)
    {
        Assert.Equal(["StartTag:plaintext", "Character:<i>", "EndOfFile"], Tokens(html));
    }

    // Rules for parsing tokens in foreign content (§13.2.6.5): an SVG title is an "any other start
    // tag"; its self-closing flag is acknowledged and the element popped, and the tokenizer never
    // leaves the data state. The markup after the icon must stay markup.
    [Fact(Timeout = 600000)]
    public void A_Self_Closing_Svg_Title_Does_Not_Swallow_The_Rest_Of_The_Document()
    {
        Assert.Equal(
            ["StartTag:svg", "StartTag:title/", "EndTag:svg", "StartTag:p", "Character:x", "EndTag:p", "EndOfFile"],
            Tokens("<svg><title/></svg><p>x</p>"));
    }

    // Tree construction dispatcher (§13.2.6): a start tag whose adjusted current node is a MathML
    // text integration point (mtext) is processed by the rules for HTML content, and "in body"
    // switches a textarea to RCDATA there.
    [Fact(Timeout = 600000)]
    public void A_Textarea_At_A_MathML_Text_Integration_Point_Is_Rcdata()
    {
        Assert.Equal(
            ["StartTag:math", "StartTag:mtext", "StartTag:textarea", "Character:<b>", "EndTag:textarea",
             "EndTag:mtext", "EndTag:math", "EndOfFile"],
            Tokens("<math><mtext><textarea><b></textarea></mtext></math>"));
    }

    // "in body" textarea (RCDATA) and the generic raw text element parsing algorithm (xmp, iframe,
    // noembed; noframes via "in head"), then the "text" insertion mode: the one character token is
    // inserted into the element and its end tag pops it. No base element exists anywhere.
    //
    // The element is looked up document-wide, not under the body. With no <body> in the input, the
    // "before head" mode opens the head and "in head" inserts a noframes element THERE; this builder
    // puts it in the body instead (noframes is not among its head metadata elements), a separate
    // departure this test must neither pin nor break on once it is fixed.
    [Theory(Timeout = 600000)]
    [InlineData("textarea")]
    [InlineData("xmp")]
    [InlineData("iframe")]
    [InlineData("noembed")]
    [InlineData("noframes")]
    public void A_Base_Inside_Text_Content_Is_Not_An_Element(string tag)
    {
        var document = Parse($"<{tag}><base href=x></{tag}>").Document;

        Assert.Empty(document.GetElementsByTagName("base"));
        Assert.Equal("<base href=x>", OnlyText(Single(document, tag)));
    }

    // "in head" title uses the generic RCDATA element parsing algorithm: one decoded text child, no
    // b element, and the document's title is that text as written.
    [Fact(Timeout = 600000)]
    public void Title_Text_Is_Rcdata_In_The_Tree_And_In_The_Result_Title()
    {
        var result = Parse("<title>a &amp; <b>b</b></title><p>x");

        Assert.Equal("a & <b>b</b>", result.Title);
        Assert.Equal("a & <b>b</b>", OnlyText(Single(result.Document.Head!, "title")));
        Assert.Empty(result.Document.GetElementsByTagName("b"));
        Assert.Equal("x", OnlyText(Single(result.Document.Body!, "p")));
    }

    // The generic raw text element parsing algorithm: the element holds one undecoded text child,
    // and parsing resumes in the data state after its end tag.
    [Theory(Timeout = 600000)]
    [InlineData("xmp")]
    [InlineData("iframe")]
    [InlineData("noembed")]
    [InlineData("noframes")]
    public void Raw_Text_Elements_Hold_One_Undecoded_Text_Child(string tag)
    {
        var document = Parse($"<body><{tag}><p>&amp;</p></{tag}><div id=after></div>").Document;

        Assert.Equal("<p>&amp;</p>", OnlyText(Single(document.Body!, tag)));
        Assert.Empty(document.GetElementsByTagName("p"));
        Assert.Same(document.Body, Single(document.Body!, "div").ParentNode);
    }

    // "in body" plaintext (§13.2.6.4.7): insert the element and switch to PLAINTEXT. The tokenizer
    // never leaves that state, so only character tokens and end-of-file follow. The element is never
    // closed, and its end tag and the div after it are its text.
    [Fact(Timeout = 600000)]
    public void A_Plaintext_Element_Holds_The_Rest_Of_The_Document()
    {
        var document = Parse("<p>a</p><plaintext></plaintext><div>b").Document;
        var body = document.Body!;

        Assert.Equal(["p", "plaintext"], body.ChildNodes.OfType<DomElement>().Select(element => element.LocalName));
        Assert.Equal("</plaintext><div>b", OnlyText(Single(body, "plaintext")));
        Assert.Empty(document.GetElementsByTagName("div"));
    }

    // The script scanner reads tokens with no tree builder. A "<script>" in RCDATA, RAWTEXT or
    // PLAINTEXT text produces no script start tag, so there is nothing to discover (or run).
    [Fact(Timeout = 600000)]
    public void Scripts_Spelled_In_Text_Content_Are_Not_Discovered()
    {
        var scripts = HtmlScriptScanner.EnumerateScripts(
            "<textarea><script>a()</script></textarea><title><script>b()</script></title>" +
            "<xmp><script>c()</script></xmp><script>d()</script><plaintext><script>e()</script>").ToArray();

        Assert.Equal("d()", Assert.Single(scripts).RawContent);
    }

    // Serializing HTML fragments (§13.3): title and textarea are not among the elements whose text
    // is written literally, so '<', '>' and '&' are escaped, and RCDATA decodes them back to the
    // same single text child on re-parse.
    [Theory(Timeout = 600000)]
    [InlineData("textarea")]
    [InlineData("title")]
    public void Rcdata_Text_Serializes_Escaped_And_Round_Trips(string tag)
    {
        var first = Single(Parse($"<{tag}>&lt;b&gt;<b>&amp;</b></{tag}>").Document, tag);
        Assert.Equal("<b><b>&</b>", OnlyText(first));

        var markup = HtmlSerializer.Serialize(first);
        Assert.Equal($"<{tag}>&lt;b&gt;&lt;b&gt;&amp;&lt;/b&gt;</{tag}>", markup);

        var second = Single(Parse(markup).Document, tag);
        Assert.Equal("<b><b>&</b>", OnlyText(second));
        Assert.Equal(markup, HtmlSerializer.Serialize(second));
    }

    // §13.3 writes xmp text literally, and RAWTEXT reads it back undecoded, so the round trip is
    // stable. With the text decoded on the way in, "&amp;lt;" became "&lt;", then "<".
    [Fact(Timeout = 600000)]
    public void Xmp_Text_Serializes_Literally_And_The_Round_Trip_Is_Stable()
    {
        var first = HtmlSerializer.Serialize(Single(Parse("<xmp>&amp;lt;<b></b></xmp>").Document, "xmp"));
        Assert.Equal("<xmp>&amp;lt;<b></b></xmp>", first);

        var second = HtmlSerializer.Serialize(Single(Parse(first).Document, "xmp"));
        Assert.Equal(first, second);
    }

    // Parsing HTML fragments (§13.4): the context element sets the tokenizer's state — RCDATA for
    // title and textarea, RAWTEXT for xmp and iframe, PLAINTEXT for plaintext — so the input is
    // one text node. The plaintext row also shows that nothing after the input is read as its text.
    [Theory(Timeout = 600000)]
    [InlineData("textarea", "<b>&amp;</b>", "<b>&</b>")]
    [InlineData("title", "a<b>&amp;", "a<b>&")]
    [InlineData("xmp", "<b>&amp;</b>", "<b>&amp;</b>")]
    [InlineData("iframe", "<p>&amp;</p>", "<p>&amp;</p>")]
    [InlineData("plaintext", "a&amp;<b>", "a&amp;<b>")]
    public void The_Fragment_Context_Selects_The_Text_State(string context, string html, string text)
    {
        var fragment = HtmlDocumentParser.ParseFragment(html, context).Fragment;

        Assert.Equal(text, OnlyText(fragment));
    }

    // The fragment input ends where the caller's string ends (§13.4 tokenizes only the input), so
    // an unterminated text element in it holds exactly the text that follows its start tag.
    [Theory(Timeout = 600000)]
    [InlineData("div", "<textarea>abc", "textarea", "abc")]
    [InlineData("head", "<title>t", "title", "t")]
    [InlineData("body", "<xmp>a", "xmp", "a")]
    [InlineData("div", "<plaintext>p", "plaintext", "p")]
    [InlineData("div", "<script>x", "script", "x")]
    public void An_Unterminated_Text_Element_In_A_Fragment_Ends_Where_The_Input_Ends(
        string context, string html, string tag, string text)
    {
        var fragment = HtmlDocumentParser.ParseFragment(html, context).Fragment;

        var element = Assert.IsType<DomElement>(Assert.Single(fragment.ChildNodes));
        Assert.Equal(tag, element.LocalName);
        Assert.Equal(text, OnlyText(element));
    }

    // Likewise for a tag or comment the input leaves open: end of input in a tag is the eof-in-tag
    // parse error, which emits nothing (§13.2.5.32 onward), and end of input in a comment emits the
    // comment as read so far (§13.2.5.45).
    [Fact(Timeout = 600000)]
    public void A_Tag_Or_Comment_Cut_Off_By_The_End_Of_Fragment_Input_Reads_Nothing_Beyond_It()
    {
        Assert.Empty(HtmlDocumentParser.ParseFragment("<a href=x", "div").Fragment.ChildNodes);

        var comment = Assert.IsType<DomComment>(
            Assert.Single(HtmlDocumentParser.ParseFragment("<!-- b", "div").Fragment.ChildNodes));
        Assert.Equal(" b", comment.Data);
    }
}
