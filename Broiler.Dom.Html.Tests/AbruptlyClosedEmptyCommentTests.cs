using Broiler.Dom;
using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// An abruptly closed empty comment, <c>&lt;!--&gt;</c> or <c>&lt;!---&gt;</c>, ends at its own
/// <c>&gt;</c>. HTML §13.2.5.43 (the comment start state) and §13.2.5.44 (the comment start dash
/// state) both treat a <c>&gt;</c> there as the abrupt-closing-of-empty-comment parse error: emit
/// the comment, switch back to the data state.
/// </summary>
/// <remarks>
/// <para>
/// The tokenizer had no comment start dash state. The dash straight after <c>&lt;!--</c> went to
/// the comment end dash state (§13.2.5.50), which has no <c>&gt;</c> rule, so <c>&lt;!---&gt;</c>
/// read its <c>&gt;</c> as comment text and kept going to the next <c>--&gt;</c>, or to the end of
/// the input. Everything in between became comment data: the DOCTYPE after it (which is how
/// HtmlBridge's doctype sniffing came to rewrite <c>&lt;!---&gt;</c> before tokenizing), the page's
/// elements, a script the page expected to run, and in a fragment the wrapper's own closing tags.
/// </para>
/// <para>
/// The two dash states differ ONLY on <c>&gt;</c>. Every other spelling that puts a dash after
/// the opening (<c>&lt;!----&gt;</c>, <c>&lt;!-----&gt;</c>, <c>&lt;!---x--&gt;</c>, the end-of-input
/// variants) is pinned here to the tokens it already produced, so the new state cannot drift from
/// the one it was split out of; and a <c>-&gt;</c> inside a comment's body (<c>&lt;!--a-&gt;b--&gt;</c>)
/// is pinned as comment data, so the end dash state cannot pick up the start dash state's
/// <c>&gt;</c> rule either.
/// </para>
/// </remarks>
public sealed class AbruptlyClosedEmptyCommentTests
{
    private static string[] Tokens(string html) =>
        new HtmlTokenizer().Tokenize(html).Select(Describe).ToArray();

    private static string Describe(HtmlToken token) => token.Type switch
    {
        TokenType.Comment => "Comment:" + token.Data,
        TokenType.Character => "Character:" + token.Data,
        TokenType.Doctype => "Doctype:" + token.Name,
        TokenType.StartTag => "StartTag:" + token.Name,
        TokenType.EndTag => "EndTag:" + token.Name,
        _ => token.Type.ToString(),
    };

    private static string[] Nodes(DomNode parent) =>
        parent.ChildNodes.Select(node => node switch
        {
            DomText text => "#text:" + text.Data,
            DomComment comment => "#comment:" + comment.Data,
            DomElement element => element.LocalName,
            _ => node.GetType().Name,
        }).ToArray();

    private static string[] CommentData(DomDocument document) =>
        document.Descendants().OfType<DomComment>().Select(comment => comment.Data).ToArray();

    // "<!-->": the comment start state's '>' rule. "<!--->": its '-' goes to the comment start dash
    // state, whose '>' rule does the same. Either way the 'x' is character data, not comment text.
    [Theory(Timeout = 600000)]
    [InlineData("<!-->x")]
    [InlineData("<!--->x")]
    public void Greater_Than_In_The_Comment_Start_Or_Comment_Start_Dash_State_Emits_The_Empty_Comment(string html)
    {
        Assert.Equal(["Comment:", "Character:x", "EndOfFile"], Tokens(html));
    }

    // The comment start dash state closes at the FIRST '>'. What used to happen instead: the
    // comment ran on to the later "-->" and took everything before it as data.
    [Fact(Timeout = 600000)]
    public void The_Comment_Start_Dash_State_Does_Not_Read_On_To_A_Later_Comment_End()
    {
        Assert.Equal(["Comment:", "Character:-->", "EndOfFile"], Tokens("<!--->-->"));
        Assert.Equal(["Comment:", "Character:a", "Comment:b", "EndOfFile"], Tokens("<!--->a<!--b-->"));
    }

    // Everything but '>' in the comment start dash state is what the comment end dash state did:
    // '-' goes to the comment end state (§13.2.5.51, whose '-' appends one dash and whose other
    // characters except '>' and '!' append "--"; its '!' rule, the comment end bang state, is not
    // modelled here), anything else appends one '-' and carries on in the comment state.
    //
    // The other side of that difference: the comment end dash state (§13.2.5.50) has NO '>' rule. A
    // '>' after a single dash in a comment's body is "anything else" there, so the dash and the '>'
    // are comment data and the comment runs on to its "-->". The "a->b" and " -> " rows pin that,
    // so the start dash state cannot be folded back into the end dash state by giving the latter a
    // '>' rule: that would close `<!-- step 1 -> step 2 -->` at its arrow.
    [Theory(Timeout = 600000)]
    [InlineData("<!---->", "")]
    [InlineData("<!----->", "-")]
    [InlineData("<!---x-->", "-x")]
    [InlineData("<!-- - -->", " - ")]
    [InlineData("<!-- -- -->", " -- ")]
    [InlineData("<!--x--->", "x-")]
    [InlineData("<!--a->b-->", "a->b")]
    [InlineData("<!-- -> -->", " -> ")]
    public void The_Comment_End_Dash_And_Comment_End_States_Keep_Every_Other_Dash_Spelling(string html, string data)
    {
        Assert.Equal(["Comment:" + data, "EndOfFile"], Tokens(html));
    }

    // eof-in-comment: the comment start state reconsumes in the comment state and the comment start
    // dash, comment end and comment state EOF rules all emit the comment, then end-of-file. The
    // last row reaches the end of input after a '>' the comment end dash state kept as data.
    [Theory(Timeout = 600000)]
    [InlineData("<!--", "")]
    [InlineData("<!---", "")]
    [InlineData("<!----", "")]
    [InlineData("<!---x", "-x")]
    [InlineData("<!--a->", "a->")]
    public void End_Of_Input_In_The_Comment_Start_Dash_And_Neighbouring_States_Emits_The_Comment(string html, string data)
    {
        Assert.Equal(["Comment:" + data, "EndOfFile"], Tokens(html));
    }

    // "<!-->" also appears inside the downlevel-revealed conditional comment, but there it is the
    // tail of a comment already in the comment state, so it closes as "-->" always did.
    [Fact(Timeout = 600000)]
    public void The_Comment_State_Still_Reads_A_Downlevel_Revealed_Conditional_Comment()
    {
        Assert.Equal(
            ["Comment:[if !IE]><!", "StartTag:p", "Character:x", "EndTag:p", "Comment:<![endif]", "EndOfFile"],
            Tokens("<!--[if !IE]><!--><p>x</p><!--<![endif]-->"));
    }

    // The case HtmlBridge found: the comment start dash state hands the rest of the input back to
    // the data state, so the DOCTYPE and the markup after it are tokens again.
    [Fact(Timeout = 600000)]
    public void The_Comment_Start_Dash_State_Hands_A_Following_Doctype_Back_To_The_Data_State()
    {
        var tokens = new HtmlTokenizer().Tokenize("<!---><!DOCTYPE html><p id='x'>a</p>").ToArray();

        Assert.Equal(
            ["Comment:", "Doctype:html", "StartTag:p", "Character:a", "EndTag:p", "EndOfFile"],
            tokens.Select(Describe));
        Assert.Equal("x", tokens[2].Attributes["id"]);
    }

    // "in body" (§13.2.6.4.7) inserts a comment token at the current node; the 'b' after it is
    // character data again, not the tail of the comment.
    [Theory(Timeout = 600000)]
    [InlineData("<!-->")]
    [InlineData("<!--->")]
    public void In_Body_The_Text_After_An_Abruptly_Closed_Comment_Is_A_Text_Node(string comment)
    {
        var document = HtmlDocumentParser.ParseDocument("<body>a" + comment + "b</body>").Document;

        Assert.Equal(["#text:a", "#comment:", "#text:b"], Nodes(document.Body!));
    }

    // Where a comment before any element belongs (the Document, per the "initial" insertion mode)
    // is a separate departure of this tree builder, so only its data is asserted, not its parent.
    [Fact(Timeout = 600000)]
    public void Elements_After_An_Abruptly_Closed_Comment_Are_Built()
    {
        var document = HtmlDocumentParser.ParseDocument("<!---><p id='after'>x</p>").Document;

        var paragraph = Assert.Single(document.GetElementsByTagName("p"));
        Assert.Equal("after", paragraph.GetAttribute("id"));
        Assert.Equal("x", paragraph.TextContent);
        Assert.Same(document.Body, paragraph.ParentNode);
        Assert.Equal([""], CommentData(document));
    }

    // The "initial" insertion mode (§13.2.6.4.1) inserts a comment into the Document and STAYS in
    // the initial mode, so a DOCTYPE after one — however the comment was spelled — still declares
    // the document (no-quirks). This has to keep holding once a DOCTYPE after real content is
    // ignored: a comment is not content.
    [Theory(Timeout = 600000)]
    [InlineData("<!---><!DOCTYPE html><p id='x'>a</p>", new[] { "" })]
    [InlineData("<!---><!DOCTYPE html><!-- --><p id='x'>a</p>", new[] { "", " " })]
    [InlineData("<!-- lead --><!---><!DOCTYPE html><!-- --><p id='x'>a</p>", new[] { " lead ", "", " " })]
    public void The_Initial_Insertion_Mode_Keeps_A_Doctype_That_Follows_An_Abruptly_Closed_Comment(string html, string[] comments)
    {
        var document = HtmlDocumentParser.ParseDocument(html).Document;

        Assert.Equal("html", Assert.IsType<DomDocumentType>(document.DocumentType).Name);
        var paragraph = Assert.Single(document.GetElementsByTagName("p"));
        Assert.Equal("x", paragraph.GetAttribute("id"));
        Assert.Equal("a", paragraph.TextContent);
        Assert.Same(document.Body, paragraph.ParentNode);
        Assert.Equal(comments, CommentData(document));
    }

    // innerHTML-style fragment parsing wraps the input in a synthetic document, so an unclosed
    // comment used to swallow the input's own markup AND the wrapper's closing tags.
    [Fact(Timeout = 600000)]
    public void A_Fragment_Keeps_The_Nodes_After_An_Abruptly_Closed_Comment()
    {
        var fragment = HtmlDocumentParser.ParseFragment("a<!--->b<span>c</span>", "div").Fragment;

        Assert.Equal(["#text:a", "#comment:", "#text:b", "span"], Nodes(fragment));
    }

    // The script scanner reads the same token stream: a script after "<!--->" is a script, the way
    // a browser runs it, not comment text nobody sees.
    [Fact(Timeout = 600000)]
    public void The_Script_Scanner_Finds_A_Script_After_An_Abruptly_Closed_Comment()
    {
        var script = Assert.Single(HtmlScriptScanner.EnumerateScripts("<!---><script>run()</script>"));

        Assert.Equal("run()", script.RawContent);
    }
}
