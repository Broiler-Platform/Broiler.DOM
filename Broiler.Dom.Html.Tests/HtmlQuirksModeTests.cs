using Broiler.Dom;
using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// <see cref="HtmlDocumentQueries.IsQuirksMode"/> against the HTML Standard's DOCTYPE conditions
/// ("The initial insertion mode", §13.2.6.4.1). The public-identifier cases are the point: a doctype
/// whose *name* is <c>html</c> can still select quirks mode, and every legacy page that relies on
/// quirks-mode behaviour is written that way.
/// </summary>
/// <remarks>
/// <para>
/// Every markup case is asked twice: of the source, and of the tree <see cref="HtmlDocumentParser"/>
/// builds from it (no DocumentType, or one <see cref="HtmlDocumentQueries.IsQuirksDoctype"/> calls
/// quirks). A host reads the mode both ways, so the two must not differ on any input here.
/// </para>
/// <para>
/// Where this tokenizer gives a different answer from the Standard, the expected answer is the
/// tokenizer's, which is the tree's. Those tests are grouped last and named
/// <c>…_As_The_Tokenizer_Reads_It</c>; each says what the Standard answers instead, and they change
/// when the tokenizer does.
/// </para>
/// <para>
/// These cases came from Broiler.Layout's <c>DocumentModeContext</c>, whose hand-written scanner
/// mirrored this tokenizer until the classification moved here.
/// </para>
/// </remarks>
public sealed class HtmlQuirksModeTests
{
    private static void AssertQuirks(bool expected, string html)
    {
        Assert.Equal(expected, HtmlDocumentQueries.IsQuirksMode(html));

        var doctype = HtmlDocumentParser.ParseDocument(html).Document.DocumentType;
        Assert.Equal(
            expected,
            doctype is null || HtmlDocumentQueries.IsQuirksDoctype(doctype.Name, doctype.PublicId, doctype.SystemId));
    }

    [Theory(Timeout = 600000)]
    // No doctype at all, and a doctype that is not html.
    [InlineData("<html><body>x</body></html>", true)]
    [InlineData("<!DOCTYPE foo><html></html>", true)]
    // The standards-mode doctype, in the forms a page actually writes it.
    [InlineData("<!DOCTYPE html><html></html>", false)]
    [InlineData("<!doctype html>\n<html></html>", false)]
    [InlineData("<!DOCTYPE HTML>", false)]
    // about:legacy-compat is standards mode: the name is html and neither identifier is listed.
    [InlineData("""<!DOCTYPE html SYSTEM "about:legacy-compat">""", false)]
    // www.7-zip.org's doctype, and more legacy public identifiers matched by prefix: HTML 4.0, 2.0 and 3.2.
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.0 Transitional//EN">""", true)]
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.0 Frameset//EN">""", true)]
    [InlineData("""<!DOCTYPE html PUBLIC "-//IETF//DTD HTML 2.0//EN">""", true)]
    [InlineData("""<!DOCTYPE html PUBLIC "-//W3C//DTD HTML 3.2 Final//EN">""", true)]
    // Exact-match public identifiers.
    [InlineData("""<!DOCTYPE html PUBLIC "HTML">""", true)]
    [InlineData("""<!DOCTYPE html PUBLIC "-/W3C/DTD HTML 4.0 Transitional/EN">""", true)]
    // The listed system identifier selects quirks whatever the public identifier says.
    [InlineData("""<!DOCTYPE html PUBLIC "" "http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtd">""", true)]
    // HTML 4.01 Transitional/Frameset: full quirks without a system identifier, limited-quirks with
    // one — and limited-quirks is not what this predicate reports.
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">""", true)]
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN" "http://www.w3.org/TR/html4/loose.dtd">""", false)]
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Frameset//EN" "http://www.w3.org/TR/html4/frameset.dtd">""", false)]
    // HTML 4.01 Strict is standards mode either way — it is on neither list.
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01//EN" "http://www.w3.org/TR/html4/strict.dtd">""", false)]
    // XHTML 1.0 Transitional is limited-quirks, so not full quirks; XHTML 1.0 Strict is standards mode.
    [InlineData("""<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">""", false)]
    [InlineData("""<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Strict//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd">""", false)]
    public void Classifies(string html, bool quirks) =>
        AssertQuirks(quirks, html);

    // The spec compares both identifiers ASCII case-insensitively, and the keyword and quoting style
    // vary across real documents.
    [Theory(Timeout = 600000)]
    [InlineData("""<!doctype html public "-//w3c//dtd html 4.0 transitional//en">""")]
    [InlineData("""<!DOCTYPE HTML PUBLIC '-//W3C//DTD HTML 4.0 Transitional//EN'>""")]
    [InlineData("""<!DoCtYpE   hTmL   pUbLiC   "-//W3C//DTD HTML 4.0 Transitional//EN"  >""")]
    public void Identifier_Matching_Is_Case_Insensitive(string html) =>
        AssertQuirks(true, html);

    [Fact(Timeout = 600000)]
    public void Empty_Input_Is_Quirks()
    {
        AssertQuirks(true, "");
        Assert.True(HtmlDocumentQueries.IsQuirksMode(null));
    }

    [Theory(Timeout = 600000)]
    // Whitespace the Standard expects but recovers without: before the name, after PUBLIC or SYSTEM, and
    // between the two identifiers. The name and the identifiers are read all the same, and they decide.
    [InlineData("<!DOCTYPEhtml>", false)]
    [InlineData("<!doctypehtml><html><head></head><body>x</body></html>", false)]
    [InlineData("""<!DOCTYPEhtml PUBLIC "-//W3C//DTD HTML 4.0 Transitional//EN">""", true)]
    [InlineData("""<!DOCTYPE html PUBLIC"-//W3C//DTD HTML 4.0 Transitional//EN">""", true)]
    [InlineData("""<!DOCTYPE html SYSTEM"http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtd">""", true)]
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN""http://www.w3.org/TR/html4/loose.dtd">""", false)]
    // An identifier cut off by '>' ends there, whether or not a quote comes later in the document. The
    // Standard also sets force-quirks then (abrupt-doctype-public-identifier), which these identifiers
    // select anyway.
    [InlineData("""<!DOCTYPE html PUBLIC "HTML>""", true)]
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 3.2 Final//EN><p>x</p>""", true)]
    [InlineData("""<!DOCTYPE html PUBLIC "HTML><p class="x">y</p>""", true)]
    public void A_Doctype_Missing_Whitespace_Or_A_Closing_Quote_Is_Still_Read(string html, bool quirks) =>
        AssertQuirks(quirks, html);

    // ────────────────────────────── the initial insertion mode ──────────────────────────────

    // Comments and ASCII whitespace keep the parser in the "initial" insertion mode (HTML §13.2.6.4.1),
    // so a DOCTYPE after them is still the document's DOCTYPE. A character reference is decoded before
    // the tree builder sees it, so &#32; is whitespace. <!x> and </> are bogus comments, which keep the
    // mode too, and <?…?> produces no token here at all.
    [Theory(Timeout = 600000)]
    [InlineData("<!DOCTYPE html><p>x</p>")]
    [InlineData("<!-- a comment --><!DOCTYPE html>")]
    [InlineData("<!--><!DOCTYPE html>")]
    [InlineData("<!---><!DOCTYPE html>")]
    [InlineData("<!----><!DOCTYPE html>")]
    [InlineData(" \t\n\f\r<!DOCTYPE html>")]
    [InlineData("\r\n<!-- one -->\n<!-- two -->\n<!DOCTYPE html>")]
    [InlineData("""<?xml version="1.0" encoding="utf-8"?><!DOCTYPE html>""")]
    [InlineData("<!x><!DOCTYPE html>")]
    [InlineData("</><!DOCTYPE html>")]
    [InlineData("&#32;&#x9;&#10;&#X0C;&#13;<!DOCTYPE html>")]
    [InlineData("<!-- <p>not a tag</p> --><!DOCTYPE html>")]
    public void A_Doctype_After_Comments_And_Ascii_Whitespace_Selects_Standards_Mode(string html) =>
        AssertQuirks(false, html);

    // Any other token sets quirks mode and ends the initial insertion mode; a DOCTYPE after it is ignored.
    [Theory(Timeout = 600000)]
    // Text: any character that is not ASCII whitespace, including a '<' that starts no tag, a no-break
    // space written as the character or as a reference, U+000B, and a U+FEFF left in a decoded string.
    [InlineData("x<!DOCTYPE html>")]
    [InlineData("<!-- c -->x<!DOCTYPE html>")]
    [InlineData("< <!DOCTYPE html>")]
    [InlineData(" <!DOCTYPE html>")]
    [InlineData("&nbsp;<!DOCTYPE html>")]
    [InlineData("&#160;<!DOCTYPE html>")]
    [InlineData("﻿<!DOCTYPE html>")]
    [InlineData("\v<!DOCTYPE html>")]
    // A reference to a character outside the BMP is text, even when the low 16 bits of its code point
    // spell SPACE (U+10020) or TAB (U+10009).
    [InlineData("&#x10020;<!DOCTYPE html>")]
    [InlineData("&#65545;<!DOCTYPE html>")]
    // A start or end tag.
    [InlineData("""<meta charset="utf-8"><!DOCTYPE html>""")]
    [InlineData("<html><!DOCTYPE html>")]
    [InlineData("</p><!DOCTYPE html>")]
    public void A_Doctype_After_Any_Other_Token_Is_Ignored(string html) =>
        AssertQuirks(true, html);

    // A DOCTYPE spelled inside a comment, or after a comment that has not ended, is comment text and
    // declares nothing.
    [Theory(Timeout = 600000)]
    [InlineData("<!-- <!DOCTYPE html> --><html></html>")]
    [InlineData("<!-- unterminated <!DOCTYPE html>")]
    public void A_Doctype_Inside_A_Comment_Declares_Nothing(string html) =>
        AssertQuirks(true, html);

    [Theory(Timeout = 600000)]
    // After comments and whitespace the DOCTYPE's name and identifiers still decide: a name other than
    // html and a legacy public identifier are quirks mode, HTML 4.01 Transitional with a system
    // identifier limited-quirks.
    [InlineData("<!-- c --><!DOCTYPE svg>", true)]
    [InlineData("\n<!DOCTYPE math PUBLIC \"-//W3C//DTD MathML 2.0//EN\">", true)]
    [InlineData("""<!-- c --><!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.0 Transitional//EN">""", true)]
    [InlineData("""<?xml?>  <!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN" "http://www.w3.org/TR/html4/loose.dtd">""", false)]
    // A DOCTYPE right after another is ignored, whichever way it would decide; one with no name decides
    // quirks mode like any name other than html.
    [InlineData("<!DOCTYPE html><!DOCTYPE foo>", false)]
    [InlineData("<!DOCTYPE foo><!DOCTYPE html>", true)]
    [InlineData("<!DOCTYPE><!DOCTYPE html>", true)]
    [InlineData("<!DOCTYPE ><!DOCTYPE html>", true)]
    public void The_Doctype_That_Ends_The_Initial_Insertion_Mode_Keeps_Its_Own_Conditions(string html, bool quirks) =>
        AssertQuirks(quirks, html);

    // ───────────────────── where this tokenizer departs from the Standard ─────────────────────

    // The Standard closes a comment at "--!>" (comment end bang state, incorrectly-closed-comment), so
    // the DOCTYPE after it would count. This tokenizer has no comment end bang state and reads the
    // rest of this input as comment text.
    [Fact(Timeout = 600000)]
    public void A_Comment_Closed_With_Dash_Dash_Bang_Is_Still_Open_As_The_Tokenizer_Reads_It() =>
        AssertQuirks(true, "<!-- x --!><!DOCTYPE html>");

    // The Standard's end tag open state wants an ASCII alpha, so "</é>" is a bogus comment and the DOCTYPE
    // after it would count. This tokenizer takes any letter (char.IsLetter) and reads an end tag, which
    // ends the initial insertion mode.
    [Fact(Timeout = 600000)]
    public void A_Non_Ascii_Letter_Starts_An_End_Tag_As_The_Tokenizer_Reads_It() =>
        AssertQuirks(true, "</é><!DOCTYPE html>");

    // The Standard decodes each of these to whitespace, so the DOCTYPE after it would count: a numeric
    // reference with no semicolon (missing-semicolon-after-character-reference), &Tab; and &NewLine;.
    // This tokenizer decodes only references WebUtility.HtmlDecode knows, terminated by ';', and leaves
    // these as text.
    [Theory(Timeout = 600000)]
    [InlineData("&#32<!DOCTYPE html>")]
    [InlineData("&Tab;<!DOCTYPE html>")]
    [InlineData("&NewLine;<!DOCTYPE html>")]
    public void A_Whitespace_Reference_Left_Undecoded_Is_Text_As_The_Tokenizer_Reads_It(string html) =>
        AssertQuirks(true, html);

    // Here the Standard is stricter: its numeric reference is the digits right after "&#" or "&#x", so
    // each of these leaves text and the DOCTYPE after it is ignored. WebUtility.HtmlDecode, which this
    // tokenizer decodes with, parses a decimal reference as a number, accepting a sign, whitespace
    // around the digits and NULs after them (NULs after hex digits too), so each is a space.
    [Theory(Timeout = 600000)]
    [InlineData("&#+32;<!DOCTYPE html>")]
    [InlineData("&# 32;<!DOCTYPE html>")]
    [InlineData("&#\t32\n;<!DOCTYPE html>")]
    [InlineData("&#32\0;<!DOCTYPE html>")]
    [InlineData("&#x20\0;<!DOCTYPE html>")]
    public void A_Numeric_Reference_Decodes_As_Leniently_As_The_Tokenizer_Reads_It(string html) =>
        AssertQuirks(false, html);

    // U+00A0 is not whitespace to the Standard's DOCTYPE states, so the name there is U+00A0 followed by
    // html, which is quirks mode. This tokenizer skips char.IsWhiteSpace in its DOCTYPE states and reads
    // the name html.
    [Fact(Timeout = 600000)]
    public void A_No_Break_Space_Before_The_Doctype_Name_Is_Whitespace_As_The_Tokenizer_Reads_It() =>
        AssertQuirks(false, "<!DOCTYPE html>");

    // The Standard sets the force-quirks flag on each of these DOCTYPEs, which puts them in quirks mode:
    // end of input inside it, PUBLIC with no identifier, an identifier cut off by '>', other text after
    // the name. This tokenizer has no force-quirks flag, so each is a DOCTYPE named html with no quirks
    // identifier.
    [Theory(Timeout = 600000)]
    [InlineData("<!DOCTYPE html")]
    [InlineData("<!DOCTYPE html PUBLIC>")]
    [InlineData("""<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Strict//EN>""")]
    [InlineData("<!DOCTYPE html bogus>")]
    public void A_Doctype_Has_No_Force_Quirks_Flag_As_The_Tokenizer_Reads_It(string html) =>
        AssertQuirks(false, html);

    // ─────────────────────────────── a DOCTYPE already parsed ───────────────────────────────

    // IsQuirksDoctype asks the same conditions of a DOCTYPE already parsed into a name and two
    // identifiers. The cases are the ones where a name-only test disagrees — a DOCTYPE named html that
    // still selects quirks mode by its public identifier — and the limited-quirks carve-out, where the
    // system identifier alone flips the answer.
    [Theory(Timeout = 600000)]
    // A name other than html is quirks mode whatever the identifiers say.
    [InlineData("foo", "", "", true)]
    [InlineData("HTML PUBLIC", "", "", true)]
    [InlineData("", "", "", true)]
    // The bare standards-mode doctype, in either case, and about:legacy-compat.
    [InlineData("html", "", "", false)]
    [InlineData("HTML", "", "", false)]
    [InlineData("html", "", "about:legacy-compat", false)]
    // www.7-zip.org's doctype: the name is html and the public identifier selects quirks mode. This is
    // the case a host testing doctype.Name alone gets wrong, flipping the mode on a serialization
    // round trip.
    [InlineData("html", "-//W3C//DTD HTML 4.0 Transitional//EN", "", true)]
    // More prefix-matched legacy identifiers: HTML 4.0 Frameset, 2.0, 3.2.
    [InlineData("html", "-//W3C//DTD HTML 4.0 Frameset//EN", "", true)]
    [InlineData("html", "-//IETF//DTD HTML 2.0//EN", "", true)]
    [InlineData("html", "-//W3C//DTD HTML 3.2 Final//EN", "", true)]
    // Exact-match public identifiers.
    [InlineData("html", "HTML", "", true)]
    [InlineData("html", "-/W3C/DTD HTML 4.0 Transitional/EN", "", true)]
    [InlineData("html", "-//W3O//DTD W3 HTML Strict 3.0//EN//", "", true)]
    // The listed system identifier selects quirks mode whatever the public identifier says.
    [InlineData("html", "", "http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtd", true)]
    // HTML 4.01 Strict is on neither list, and XHTML 1.0 Transitional is limited-quirks, which this
    // predicate does not report.
    [InlineData("html", "-//W3C//DTD HTML 4.01//EN", "http://www.w3.org/TR/html4/strict.dtd", false)]
    [InlineData("html", "-//W3C//DTD XHTML 1.0 Transitional//EN",
        "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd", false)]
    public void Classifies_A_Parsed_Doctype(string name, string publicId, string systemId, bool quirks) =>
        Assert.Equal(quirks, HtmlDocumentQueries.IsQuirksDoctype(name, publicId, systemId));

    // HTML 4.01 Transitional and Frameset are the one pair whose answer the system identifier alone
    // decides: full quirks without one, limited-quirks with one. Getting this backwards is how a
    // conforming HTML 4.01 Transitional page ends up in quirks mode.
    [Theory(Timeout = 600000)]
    [InlineData("-//W3C//DTD HTML 4.01 Transitional//EN")]
    [InlineData("-//W3C//DTD HTML 4.01 Frameset//EN")]
    public void Html4_01_Transitional_And_Frameset_Are_Quirks_Only_Without_A_System_Identifier(string publicId)
    {
        Assert.True(HtmlDocumentQueries.IsQuirksDoctype("html", publicId, ""));
        Assert.True(HtmlDocumentQueries.IsQuirksDoctype("html", publicId, null));
        Assert.False(HtmlDocumentQueries.IsQuirksDoctype("html", publicId, "http://www.w3.org/TR/html4/loose.dtd"));
        // Any system identifier, not only the matching one: the condition is presence, not value.
        Assert.False(HtmlDocumentQueries.IsQuirksDoctype("html", publicId, "x"));
    }

    // A missing identifier reaches this method as null from a nullable-oblivious caller just as easily
    // as it does as an empty string, and both mean the same thing to the spec conditions. A null name
    // is not a name of html, so it is quirks mode.
    [Fact(Timeout = 600000)]
    public void A_Null_Identifier_Counts_As_Missing()
    {
        Assert.False(HtmlDocumentQueries.IsQuirksDoctype("html", null, null));
        Assert.True(HtmlDocumentQueries.IsQuirksDoctype("html", "-//W3C//DTD HTML 4.0 Transitional//EN", null));
        Assert.True(HtmlDocumentQueries.IsQuirksDoctype(null, null, null));
    }

    // Both identifiers and the name are compared ASCII case-insensitively, so a tokenizer that keeps the
    // source case of <!DOCTYPE HTML PUBLIC "…"> answers the same as one that lowercases the name.
    [Theory(Timeout = 600000)]
    [InlineData("html", "-//w3c//dtd html 4.0 transitional//en")]
    [InlineData("HTML", "-//W3C//DTD HTML 4.0 Transitional//EN")]
    [InlineData("hTmL", "-//W3C//dtd HTML 4.0 TRANSITIONAL//EN")]
    public void Parsed_Doctype_Matching_Is_Case_Insensitive(string name, string publicId) =>
        Assert.True(HtmlDocumentQueries.IsQuirksDoctype(name, publicId, ""));

    [Fact(Timeout = 600000)]
    public void The_System_Identifier_Match_Is_Case_Insensitive_Too() =>
        Assert.True(HtmlDocumentQueries.IsQuirksDoctype(
            "html", "", "HTTP://WWW.IBM.COM/data/dtd/v11/ibmxhtml1-transitional.dtd"));

    // The two entry points share one implementation, so the answer a host computes from the source and
    // the answer it recomputes from the parsed triple must not differ. These are the triples the
    // markup on the left parses to.
    [Theory(Timeout = 600000)]
    [InlineData("<!DOCTYPE html>", "html", "", "")]
    [InlineData("<!DOCTYPE foo>", "foo", "", "")]
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.0 Transitional//EN">""",
        "HTML", "-//W3C//DTD HTML 4.0 Transitional//EN", "")]
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN" "http://www.w3.org/TR/html4/loose.dtd">""",
        "HTML", "-//W3C//DTD HTML 4.01 Transitional//EN", "http://www.w3.org/TR/html4/loose.dtd")]
    [InlineData("""<!DOCTYPE html SYSTEM "about:legacy-compat">""", "html", "", "about:legacy-compat")]
    [InlineData("""<!DOCTYPE html PUBLIC "" "http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtd">""",
        "html", "", "http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtd")]
    public void Agrees_With_The_Markup_Predicate(string html, string name, string publicId, string systemId) =>
        Assert.Equal(
            HtmlDocumentQueries.IsQuirksMode(html),
            HtmlDocumentQueries.IsQuirksDoctype(name, publicId, systemId));

    // HasHtmlDoctype's true is not standards mode: the legacy doctype is named html and still selects
    // quirks mode, which is why IsQuirksMode exists beside it.
    [Fact(Timeout = 600000)]
    public void An_Html_Doctype_Can_Still_Select_Quirks_Mode()
    {
        const string html = """<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.0 Transitional//EN"><p>x</p>""";

        Assert.True(HtmlDocumentQueries.HasHtmlDoctype(html));
        AssertQuirks(true, html);
    }
}
