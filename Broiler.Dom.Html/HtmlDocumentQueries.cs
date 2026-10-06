using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Broiler.Dom;

namespace Broiler.Dom.Html;

/// <summary>
/// Queries on parsed HTML DOM trees and raw HTML markup streams for document-level metadata,
/// including document base URL (<c>&lt;base href&gt;</c>), doctype detection and quirks mode.
/// </summary>
// Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=TBF
// Broiler-Falsified-If: a <base href> inside a <template>, as a DOM element or between <template> and </template> in raw source, is returned as the document's base URL
// Broiler-Human:        PENDING
public static class HtmlDocumentQueries
{
    // Broiler-AI:           Origin=AI; IP=None; Security=High; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: the constant holds a character beyond tab, LF, FF, CR and space, so source starting with U+00A0 or U+000B before <!DOCTYPE html> reports an html doctype
    // Broiler-Human:        PENDING
    private const string AsciiWhitespace = "\t\n\f\r ";

    /// <summary>
    /// Finds the effective document base URL in a DOM tree: the <c>href</c> attribute of the
    /// first <c>&lt;base&gt;</c> element in tree order that carries a non-whitespace value, trimmed.
    /// Returns <see langword="null"/> if none is found (HTML §4.2.3).
    /// </summary>
    /// <remarks>
    /// This agrees with the <see cref="GetEffectiveBaseHref(string)"/> overload about a
    /// <c>&lt;base&gt;</c> inside a <c>&lt;template&gt;</c> without a guard of its own: the parser
    /// puts template children in the template's contents fragment (HTML §4.12.3), which is not in
    /// the tree, so a descendant walk never reaches one. The token overload cannot rely on that —
    /// it never builds a tree — which is why it counts template depth itself.
    /// </remarks>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.2.3; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: a <base> with a non-blank href is passed over in favour of one later in tree order
    // Broiler-Human:        PENDING
    public static string? GetEffectiveBaseHref(DomNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        foreach (var element in root.InclusiveDescendants().OfType<DomElement>())
        {
            if (string.Equals(element.TagName, "base", StringComparison.OrdinalIgnoreCase) &&
                element.GetAttributeByQualifiedName("href") is { } href &&
                !string.IsNullOrWhiteSpace(href))
            {
                return href.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to find the effective document base URL in a DOM tree.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: it returns false while GetEffectiveBaseHref finds a base, or true with a null baseHref
    // Broiler-Human:        PENDING
    public static bool TryGetEffectiveBaseHref(DomNode root, [NotNullWhen(true)] out string? baseHref)
    {
        baseHref = GetEffectiveBaseHref(root);
        return baseHref is not null;
    }

    /// <summary>
    /// Finds the effective document base URL in raw HTML source by tokenizing the markup: the
    /// <c>href</c> of the first <c>&lt;base&gt;</c> start tag outside of any <c>&lt;template&gt;</c>
    /// that carries a non-whitespace value, trimmed. Returns <see langword="null"/> if none is found.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.2.3; IP=Low; Security=High; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a <base href> between a <template> start tag and its </template> in the raw source is returned as the base URL
    // Broiler-Human:        PENDING
    public static string? GetEffectiveBaseHref(string html)
    {
        if (string.IsNullOrEmpty(html) || !html.Contains("<base", StringComparison.OrdinalIgnoreCase))
            return null;

        var templateDepth = 0;
        foreach (var token in new HtmlTokenizer().Tokenize(html))
        {
            if (token.Type == TokenType.EndTag)
            {
                if (templateDepth > 0 && string.Equals(token.Name, "template", StringComparison.OrdinalIgnoreCase))
                    templateDepth--;
                continue;
            }

            if (token.Type != TokenType.StartTag)
                continue;

            if (string.Equals(token.Name, "template", StringComparison.OrdinalIgnoreCase))
            {
                if (!token.SelfClosing)
                    templateDepth++;
                continue;
            }

            if (templateDepth == 0 &&
                string.Equals(token.Name, "base", StringComparison.OrdinalIgnoreCase) &&
                token.Attributes.TryGetValue("href", out var href) &&
                !string.IsNullOrWhiteSpace(href))
            {
                return href.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to find the effective document base URL in raw HTML source.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: source whose only <base href> sits between <template> and </template> returns true
    // Broiler-Human:        PENDING
    public static bool TryGetEffectiveBaseHref(string html, [NotNullWhen(true)] out string? baseHref)
    {
        baseHref = GetEffectiveBaseHref(html);
        return baseHref is not null;
    }

    /// <summary>
    /// Determines whether the HTML source begins with a standards-mode DOCTYPE: the first token
    /// that is neither a comment nor ASCII whitespace is a DOCTYPE named <c>html</c> (HTML §13.2.6.4.1).
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.2.6.4.1; IP=Low; Security=High; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: source whose first token besides comments and ASCII whitespace is text, such as U+00A0, followed by <!DOCTYPE html> returns true
    // Broiler-Human:        PENDING
    public static bool HasHtmlDoctype(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return false;

        foreach (var token in new HtmlTokenizer().Tokenize(html))
        {
            if (token.Type == TokenType.Comment ||
                (token.Type == TokenType.Character && token.Data.AsSpan().Trim(AsciiWhitespace).IsEmpty))
            {
                continue;
            }

            return token.Type == TokenType.Doctype && string.Equals(token.Name, "html", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// Whether HTML source puts its document in quirks mode: it has no DOCTYPE that the "initial"
    /// insertion mode takes (HTML §13.2.6.4.1), or the one it has selects quirks mode by its name and
    /// identifiers (<see cref="IsQuirksDoctype"/>). A <c>null</c> or empty source is quirks mode.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The answer is the tree builder's, by construction: this reads the same tokens through the
    /// same initial-mode predicate (<see cref="HtmlDocumentParser.StaysInInitialInsertionMode"/>),
    /// so a page is quirks mode here exactly when <see cref="HtmlDocumentParser"/> gives it no
    /// DocumentType, or a DocumentType that <see cref="IsQuirksDoctype"/> calls quirks. That is
    /// what lets a host read the mode from the source before it builds the tree, then again from the
    /// tree once it serializes it, and get one answer. Wherever this tokenizer departs from the
    /// Standard, this follows the tokenizer: <c>--!&gt;</c> does not close a comment, a reference the
    /// platform's decoder leaves as written (<c>&amp;Tab;</c>, <c>&amp;#32</c>) is text, and there is
    /// no force-quirks flag, so a malformed DOCTYPE named <c>html</c> is decided by its identifiers.
    /// </para>
    /// <para>
    /// <see cref="HasHtmlDoctype"/> asks a narrower question, and its <c>true</c> does not mean
    /// standards mode: <c>&lt;!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.0 Transitional//EN"&gt;</c>
    /// is named <c>html</c> and selects quirks mode.
    /// </para>
    /// <para>
    /// Only the tokens up to the end of the initial insertion mode are read, but the tokenizer first
    /// normalizes the newlines of the whole input, which copies a source that contains a CR.
    /// </para>
    /// </remarks>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.2.6.4.1; IP=Low; Security=High; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: source that HtmlDocumentParser gives no DocumentType, such as text before <!DOCTYPE html>, is reported as not quirks mode
    // Broiler-Human:        PENDING
    public static bool IsQuirksMode(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return true;

        foreach (var token in new HtmlTokenizer().Tokenize(html))
        {
            if (HtmlDocumentParser.StaysInInitialInsertionMode(token))
                continue;

            return token.Type != TokenType.Doctype || IsQuirksDoctype(token.Name, token.PublicId, token.SystemId);
        }

        return true;
    }

    /// <summary>
    /// The HTML Standard's quirks-mode conditions for a DOCTYPE already parsed into a name and a
    /// public and a system identifier, as a <see cref="DomDocumentType"/> carries them (HTML
    /// §13.2.6.4.1). A name other than <c>html</c> is quirks mode, and so is a name of <c>html</c>
    /// with one of the legacy identifiers. A missing identifier is <c>null</c> or empty.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Testing the name alone is wrong for exactly the pages that need quirks mode:
    /// <c>&lt;!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.0 Transitional//EN"&gt;</c>, which legacy
    /// pages such as www.7-zip.org declare, has the name <c>html</c>. A host that serializes its tree
    /// on a name-only test flips such a page to standards mode on the round trip.
    /// </para>
    /// <para>
    /// The answer is full quirks or not. Limited-quirks mode is not reported: XHTML 1.0
    /// Transitional and Frameset, and HTML 4.01 Transitional and Frameset with a system identifier,
    /// answer <c>false</c>. The name and both identifiers are compared ASCII case-insensitively, so a
    /// tokenizer that keeps the source case of <c>&lt;!DOCTYPE HTML&gt;</c> gets the same answer as one
    /// that lowercases it.
    /// </para>
    /// </remarks>
    /// <param name="name">The DOCTYPE name, as parsed.</param>
    /// <param name="publicId">The public identifier, or null/empty when the DOCTYPE carries none.</param>
    /// <param name="systemId">The system identifier, or null/empty when the DOCTYPE carries none.</param>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.2.6.4.1; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public static bool IsQuirksDoctype(string? name, string? publicId, string? systemId)
    {
        if (name is null || !name.Equals("html", StringComparison.OrdinalIgnoreCase))
            return true;

        return SelectsQuirksMode(publicId ?? string.Empty, systemId ?? string.Empty);
    }

    /// <summary>
    /// The quirks-mode conditions for a DOCTYPE named <c>html</c>, on its identifiers. Both are
    /// compared ASCII case-insensitively, and an empty system identifier counts as missing.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.2.6.4.1; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    private static bool SelectsQuirksMode(string publicId, string systemId)
    {
        foreach (var exact in QuirksPublicIdentifiers)
            if (publicId.Equals(exact, StringComparison.OrdinalIgnoreCase))
                return true;

        if (systemId.Equals(
                "http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtd",
                StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var prefix in QuirksPublicIdentifierPrefixes)
            if (publicId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;

        // HTML 4.01 Transitional/Frameset are full quirks only without a system identifier; with one
        // they are limited-quirks, which is not this predicate's answer.
        if (systemId.Length == 0)
            foreach (var prefix in QuirksPublicIdentifierPrefixesWithoutSystemId)
                if (publicId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;

        return false;
    }

    /// <summary>Public identifiers that select quirks mode by exact match.</summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.2.6.4.1; IP=None; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    private static readonly string[] QuirksPublicIdentifiers =
    [
        "-//W3O//DTD W3 HTML Strict 3.0//EN//",
        "-/W3C/DTD HTML 4.0 Transitional/EN",
        "HTML",
    ];

    /// <summary>Public-identifier prefixes that select quirks mode regardless of system identifier.</summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.2.6.4.1; IP=None; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    private static readonly string[] QuirksPublicIdentifierPrefixes =
    [
        "+//Silmaril//dtd html Pro v0r11 19970101//",
        "-//AS//DTD HTML 3.0 asWedit + extensions//",
        "-//AdvaSoft Ltd//DTD HTML 3.0 asWedit + extensions//",
        "-//IETF//DTD HTML 2.0 Level 1//",
        "-//IETF//DTD HTML 2.0 Level 2//",
        "-//IETF//DTD HTML 2.0 Strict Level 1//",
        "-//IETF//DTD HTML 2.0 Strict Level 2//",
        "-//IETF//DTD HTML 2.0 Strict//",
        "-//IETF//DTD HTML 2.0//",
        "-//IETF//DTD HTML 2.1E//",
        "-//IETF//DTD HTML 3.0//",
        "-//IETF//DTD HTML 3.2 Final//",
        "-//IETF//DTD HTML 3.2//",
        "-//IETF//DTD HTML 3//",
        "-//IETF//DTD HTML Level 0//",
        "-//IETF//DTD HTML Level 1//",
        "-//IETF//DTD HTML Level 2//",
        "-//IETF//DTD HTML Level 3//",
        "-//IETF//DTD HTML Strict Level 0//",
        "-//IETF//DTD HTML Strict Level 1//",
        "-//IETF//DTD HTML Strict Level 2//",
        "-//IETF//DTD HTML Strict Level 3//",
        "-//IETF//DTD HTML Strict//",
        "-//IETF//DTD HTML//",
        "-//Metrius//DTD Metrius Presentational//",
        "-//Microsoft//DTD Internet Explorer 2.0 HTML Strict//",
        "-//Microsoft//DTD Internet Explorer 2.0 HTML//",
        "-//Microsoft//DTD Internet Explorer 2.0 Tables//",
        "-//Microsoft//DTD Internet Explorer 3.0 HTML Strict//",
        "-//Microsoft//DTD Internet Explorer 3.0 HTML//",
        "-//Microsoft//DTD Internet Explorer 3.0 Tables//",
        "-//Netscape Comm. Corp.//DTD HTML//",
        "-//Netscape Comm. Corp.//DTD Strict HTML//",
        "-//O'Reilly and Associates//DTD HTML 2.0//",
        "-//O'Reilly and Associates//DTD HTML Extended 1.0//",
        "-//O'Reilly and Associates//DTD HTML Extended Relaxed 1.0//",
        "-//SQ//DTD HTML 2.0 HoTMetaL + extensions//",
        "-//SoftQuad Software//DTD HoTMetaL PRO 6.0::19990601::extensions to HTML 4.0//",
        "-//SoftQuad//DTD HoTMetaL PRO 4.0::19971010::extensions to HTML 4.0//",
        "-//Spyglass//DTD HTML 2.0 Extended//",
        "-//Sun Microsystems Corp.//DTD HotJava HTML//",
        "-//Sun Microsystems Corp.//DTD HotJava Strict HTML//",
        "-//W3C//DTD HTML 3 1995-03-24//",
        "-//W3C//DTD HTML 3.2 Draft//",
        "-//W3C//DTD HTML 3.2 Final//",
        "-//W3C//DTD HTML 3.2//",
        "-//W3C//DTD HTML 3.2S Draft//",
        "-//W3C//DTD HTML 4.0 Frameset//",
        "-//W3C//DTD HTML 4.0 Transitional//",
        "-//W3C//DTD HTML Experimental 19960712//",
        "-//W3C//DTD HTML Experimental 970421//",
        "-//W3C//DTD W3 HTML//",
        "-//W3O//DTD W3 HTML 3.0//",
        "-//WebTechs//DTD Mozilla HTML 2.0//",
        "-//WebTechs//DTD Mozilla HTML//",
    ];

    /// <summary>
    /// Public-identifier prefixes that select quirks mode only when the system identifier is missing
    /// or empty; with one present these are limited-quirks instead.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.2.6.4.1; IP=None; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    private static readonly string[] QuirksPublicIdentifierPrefixesWithoutSystemId =
    [
        "-//W3C//DTD HTML 4.01 Frameset//",
        "-//W3C//DTD HTML 4.01 Transitional//",
    ];
}
