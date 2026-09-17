using Broiler.Dom;
using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// Only a DOCTYPE token that arrives while the tree builder is still in the "initial" insertion
/// mode (HTML §13.2.6.4.1) declares the document. That mode ignores character tokens that are
/// ASCII whitespace (tab, LF, FF, CR, space) and inserts comments, staying put for both; a DOCTYPE
/// token appends the DocumentType node and moves on to "before html"; anything else (text, a start
/// tag, an end tag) is a parse error that sets quirks mode and moves on too. Every later mode,
/// from "before html" (§13.2.6.4.2) to "in body" (§13.2.6.4.7) and beyond, ignores a DOCTYPE as a
/// parse error, and the HTML fragment parsing algorithm never starts in "initial" at all.
/// </summary>
/// <remarks>
/// <para>
/// This builder had no insertion-mode tracking for it: the first named DOCTYPE token was inserted
/// before <c>&lt;html&gt;</c> wherever it appeared, so <c>&lt;p&gt;x&lt;/p&gt;&lt;!DOCTYPE html&gt;</c>
/// got a DocumentType a browser never builds (<c>document.doctype === null</c>, quirks mode). Broiler.Dom
/// has no document-mode property — a DocumentType node's presence (and name) is the only mode
/// signal this component emits — so a DOCTYPE accepted after content turned a quirks page into a
/// standards one for every consumer that reads the tree: HtmlBridge's <c>document.doctype</c>, its
/// render-string doctype stamp, frames and <c>document.write</c>. HtmlBridge's token-level
/// <c>HasHtmlDoctype</c> already applied the initial-mode rule on its own, so the tree and the stamp
/// disagreed.
/// </para>
/// <para>
/// The tokenizer is deliberately unchanged: it has no insertion mode, and the markup declaration
/// open state (§13.2.5.42) emits a DOCTYPE token wherever it meets one, so the first tests pin that
/// the decision lives in tree construction. The whitespace test is ASCII whitespace, not
/// <c>char.IsWhiteSpace</c>: U+00A0, U+000B and U+FEFF are ordinary characters that end the mode.
/// </para>
/// </remarks>
public sealed class LateDoctypeTests
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

    private static DomDocument Parse(string html) => HtmlDocumentParser.ParseDocument(html).Document;

    // Every DocumentType anywhere in the tree, not just DomDocument.DocumentType (the first one
    // among the Document's children), so a doctype that went somewhere odd cannot slip past.
    private static DomDocumentType[] Doctypes(DomNode root) =>
        root.Descendants().OfType<DomDocumentType>().ToArray();

    private static bool IsAsciiWhitespace(char c) => c is '\t' or '\n' or '\f' or '\r' or ' ';

    // The markup declaration open state has no idea what came before it: a DOCTYPE after content
    // is still a DOCTYPE token. Ignoring it is tree construction's job, and token-level consumers
    // (HtmlBridge's HasHtmlDoctype) still get the token to apply the rule themselves.
    [Fact(Timeout = 600000)]
    public void The_Markup_Declaration_Open_State_Emits_A_Doctype_Token_Wherever_It_Appears()
    {
        Assert.Equal(
            ["StartTag:p", "Character:x", "EndTag:p", "Doctype:html", "EndOfFile"],
            Tokens("<p>x</p><!DOCTYPE html>"));
    }

    // The comment states (§13.2.5.45 onward) keep "<!DOCTYPE html>" as comment data; no DOCTYPE
    // token exists for tree construction to accept or ignore.
    [Fact(Timeout = 600000)]
    public void The_Comment_States_Keep_A_Quoted_Doctype_As_Comment_Data()
    {
        var tokens = new HtmlTokenizer().Tokenize("<!-- <!DOCTYPE html> --><p>x</p>").ToArray();

        Assert.Equal(TokenType.Comment, tokens[0].Type);
        Assert.Equal(" <!DOCTYPE html> ", tokens[0].Data);
        Assert.DoesNotContain(tokens, token => token.Type == TokenType.Doctype);
    }

    // The tag open state (§13.2.5.6) turns "<?" into a bogus comment (§13.2.5.41) — a comment token,
    // which keeps the initial mode. This tokenizer skips the processing instruction without a token
    // instead (a separate departure), which keeps the mode just the same. Asserted as "the first
    // token that could end the mode is the DOCTYPE" so it holds either way.
    [Fact(Timeout = 600000)]
    public void An_Xml_Declaration_Yields_No_Token_That_Ends_The_Initial_Insertion_Mode()
    {
        var tokens = new HtmlTokenizer()
            .Tokenize("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE html>")
            .ToArray();

        var first = tokens.First(token =>
            token.Type != TokenType.Comment &&
            !(token.Type == TokenType.Character && token.Data!.All(IsAsciiWhitespace)));
        Assert.Equal(TokenType.Doctype, first.Type);
        Assert.Equal("html", first.Name);
    }

    // "initial", DOCTYPE token: append a DocumentType node to the Document — before the html
    // element, the only place a Document allows one.
    [Fact(Timeout = 600000)]
    public void The_Initial_Insertion_Mode_Appends_A_Leading_Doctype_Before_The_Html_Element()
    {
        var document = Parse("<!DOCTYPE html><p>x</p>");

        var children = document.ChildNodes.ToArray();
        Assert.Equal(2, children.Length);
        var doctype = Assert.IsType<DomDocumentType>(children[0]);
        Assert.Equal("html", doctype.Name);
        Assert.Equal("", doctype.PublicId);
        Assert.Equal("", doctype.SystemId);
        Assert.Same(document.DocumentElement, children[1]);
    }

    // "initial": a comment token is inserted and the mode stays; a character token that is ASCII
    // whitespace — however it was written: literally, as a CRLF the input stream preprocessing
    // turned into LF, or as a character reference such as &#32; &#9; &#13; — is ignored and the mode
    // stays. So the DOCTYPE after them still arrives in "initial". Where the comment itself ends up
    // (the spec says the Document; this builder uses the head) is a separate departure, so only
    // the doctype is asserted.
    [Theory(Timeout = 600000)]
    [InlineData("<!-- lead --><!DOCTYPE html><p>x</p>")]
    [InlineData(" \n\t\f<!DOCTYPE html><p>x</p>")]
    [InlineData("\r\n<!DOCTYPE html><p>x</p>")]
    [InlineData("&#32;&#9;&#13;&#10;&#x0C;<!DOCTYPE html><p>x</p>")]
    [InlineData("<!----><!-- a --> \n<!-- b --><!DOCTYPE html><p>x</p>")]
    [InlineData("<!--><!---><!DOCTYPE html><p>x</p>")]
    [InlineData("<?xml version=\"1.0\"?>\n<!DOCTYPE html><p>x</p>")]
    [InlineData("<!-- <!DOCTYPE foo> --><!DOCTYPE html><p>x</p>")]
    public void Comments_And_Ascii_Whitespace_Keep_The_Parser_In_The_Initial_Insertion_Mode(string html)
    {
        var document = Parse(html);

        var doctype = Assert.Single(Doctypes(document));
        Assert.Equal("html", doctype.Name);
        Assert.Same(document, doctype.ParentNode);
        var children = document.ChildNodes.ToList();
        Assert.True(children.IndexOf(doctype) < children.IndexOf(document.DocumentElement!));
        Assert.Equal("x", Assert.Single(document.GetElementsByTagName("p")).TextContent);
    }

    // "initial", anything else: parse error, quirks mode, switch to "before html" and reprocess.
    // The DOCTYPE then arrives in "before html", "before head", "in head" or "in body", every one
    // of which ignores it as a parse error — so no DocumentType node. U+00A0 (literally, &#160; or
    // &nbsp;) and U+000B are not ASCII whitespace; "\n x" is one character token here, and its x
    // ends the mode even though the whitespace before it would not have. Before this fix every
    // one of these got DocumentType "html".
    [Theory(Timeout = 600000)]
    [InlineData("x<!DOCTYPE html><p>y</p>")]
    [InlineData("\u00A0<!DOCTYPE html><p>y</p>")]
    [InlineData("&#160;<!DOCTYPE html><p>y</p>")]
    [InlineData("&nbsp;<!DOCTYPE html><p>y</p>")]
    [InlineData("\v<!DOCTYPE html><p>y</p>")]
    [InlineData("\n x<!DOCTYPE html><p>y</p>")]
    [InlineData("< <!DOCTYPE html><p>y</p>")]
    [InlineData("<p>x</p><!DOCTYPE html>")]
    [InlineData("<html><!DOCTYPE html><body>x</body>")]
    [InlineData("<head><!DOCTYPE html></head><p>y</p>")]
    [InlineData("<meta charset=\"utf-8\"><!DOCTYPE html>")]
    [InlineData("<title>t</title><!DOCTYPE html>")]
    [InlineData("</p><!DOCTYPE html>")]
    [InlineData("<!-- lead -->x<!-- tail --><!DOCTYPE html>")]
    public void Any_Other_Token_Ends_The_Initial_Insertion_Mode_So_A_Later_Doctype_Is_Ignored(string html)
    {
        var document = Parse(html);

        Assert.Null(document.DocumentType);
        Assert.Empty(Doctypes(document));
    }

    // The first DOCTYPE moves the parser to "before html", where the second one is a parse error
    // and ignored: its name and identifiers never reach the tree.
    [Fact(Timeout = 600000)]
    public void A_Second_Doctype_Arrives_In_The_Before_Html_Insertion_Mode_And_Is_Ignored()
    {
        var document = Parse("<!DOCTYPE html><!DOCTYPE foo PUBLIC \"-//x\" \"y\"><p>x</p>");

        var doctype = Assert.Single(Doctypes(document));
        Assert.Equal("html", doctype.Name);
        Assert.Equal("", doctype.PublicId);
        Assert.Equal("", doctype.SystemId);
    }

    // "<!DOCTYPE>" is a DOCTYPE token with a missing name (missing-doctype-name, force-quirks). It is
    // processed in "initial" and moves the parser on like any DOCTYPE, so the named one after it is
    // ignored. The spec appends a DocumentType with an empty name for it; this builder does not
    // create that node yet (a separate gap), so only the absence of an "html" doctype — the
    // standards-mode answer the second token used to give — is asserted, which stays true once
    // that gap is closed.
    [Theory(Timeout = 600000)]
    [InlineData("<!DOCTYPE><!DOCTYPE html><p>x</p>")]
    [InlineData("<!DOCTYPE ><!DOCTYPE html><p>x</p>")]
    public void A_Nameless_Doctype_Still_Ends_The_Initial_Insertion_Mode(string html)
    {
        var document = Parse(html);

        Assert.DoesNotContain(Doctypes(document), doctype => doctype.Name == "html");
    }

    // No DOCTYPE token at all (the comment states own it), and the p start tag ends "initial".
    [Fact(Timeout = 600000)]
    public void A_Doctype_Quoted_In_A_Comment_Declares_Nothing()
    {
        var document = Parse("<!-- <!DOCTYPE html> --><p>x</p>");

        Assert.Null(document.DocumentType);
        Assert.Empty(Doctypes(document));
    }

    // A byte order mark belongs to the input BYTE stream (§13.2.3): the Encoding Standard's decode
    // strips it before tokenization ever runs. ParseDocument takes the already-decoded string, and
    // a U+FEFF still at its start is an ordinary character — not ASCII whitespace — so it ends the
    // initial mode like any other text. Callers that decode bytes themselves must strip it (as
    // File.ReadAllText and StreamReader do; Encoding.UTF8.GetString does not).
    [Fact(Timeout = 600000)]
    public void A_Byte_Order_Mark_Left_In_The_Decoded_String_Ends_The_Initial_Insertion_Mode()
    {
        Assert.Null(Parse("\uFEFF<!DOCTYPE html><p>x</p>").DocumentType);
        Assert.Equal("html", Parse("<!DOCTYPE html><p>x</p>").DocumentType?.Name);
    }

    // The HTML fragment parsing algorithm resets the insertion mode from the context element
    // ("in body", "in cell", "before head", ...) and never enters "initial", so a DOCTYPE in fragment
    // input is always ignored — neither the fragment nor the synthetic document it was built in
    // gets a DocumentType. The latter used to: ParseDocument inserted the input's DOCTYPE into the
    // wrapper document.
    [Theory(Timeout = 600000)]
    [InlineData("div")]
    [InlineData("body")]
    [InlineData("html")]
    [InlineData("td")]
    public void Fragment_Parsing_Never_Starts_In_The_Initial_Insertion_Mode(string context)
    {
        var fragment = HtmlDocumentParser.ParseFragment("<!DOCTYPE html><p>x</p>", context).Fragment;

        Assert.Empty(Doctypes(fragment));
        Assert.Contains(fragment.Descendants(), node => node is DomElement { LocalName: "p" });
        Assert.Null(fragment.OwnerDocument.DocumentType);
        Assert.Empty(Doctypes(fragment.OwnerDocument));
    }

    // ParseDocument(html, document) clears the target first, and each call starts in "initial"
    // again: a doctype the document already had does not survive markup whose DOCTYPE comes late,
    // and a later parse with a leading DOCTYPE gets exactly one.
    [Fact(Timeout = 600000)]
    public void Reparsing_Into_An_Existing_Document_Takes_Only_A_Doctype_From_The_Initial_Insertion_Mode()
    {
        var document = new DomDocument();
        document.AppendChild(document.CreateDocumentType("html"));
        document.AppendChild(document.CreateElement("html"));

        var late = HtmlDocumentParser.ParseDocument("<p>x</p><!DOCTYPE html>", document);
        Assert.Same(document, late.Document);
        Assert.Null(document.DocumentType);
        Assert.Empty(Doctypes(document));

        HtmlDocumentParser.ParseDocument("<!DOCTYPE html><p>y</p>", document);
        var doctype = Assert.Single(Doctypes(document));
        Assert.Same(document.FirstChild, doctype);
        Assert.Equal("html", doctype.Name);
    }

    // The serializer writes the tree it is given, so an ignored DOCTYPE no longer comes back as a
    // leading "<!DOCTYPE html>" — which used to make a quirks document round-trip as a standards
    // one. The late DOCTYPE also leaves nothing in the body: "in body" ignores the token outright.
    [Fact(Timeout = 600000)]
    public void Serializing_A_Document_With_A_Late_Doctype_Writes_No_Doctype()
    {
        var late = HtmlSerializer.Serialize(Parse("<p>x</p><!DOCTYPE html><p>y</p>"));
        Assert.Equal("<html><head></head><body><p>x</p><p>y</p></body></html>", late);
        Assert.Null(Parse(late).DocumentType);

        var leading = HtmlSerializer.Serialize(Parse("<!DOCTYPE html><p>x</p>"));
        Assert.Equal("<!DOCTYPE html><html><head></head><body><p>x</p></body></html>", leading);
        Assert.Equal("html", Parse(leading).DocumentType?.Name);
    }
}
