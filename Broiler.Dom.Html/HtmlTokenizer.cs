using System;
using System.Collections.Generic;
using System.Text;

namespace Broiler.Dom.Html;

/// <summary>Identifies the kind of <see cref="HtmlToken"/>.</summary>
public enum TokenType
{
    /// <summary>A DOCTYPE token.</summary>
    Doctype,
    /// <summary>A start-tag token.</summary>
    StartTag,
    /// <summary>An end-tag token.</summary>
    EndTag,
    /// <summary>Character data.</summary>
    Character,
    /// <summary>An HTML comment.</summary>
    Comment,
    /// <summary>End of the input stream.</summary>
    EndOfFile
}

/// <summary>A single token emitted by <see cref="HtmlTokenizer"/>.</summary>
/// <remarks>Creates a new <see cref="HtmlToken"/>.</remarks>
public sealed class HtmlToken(TokenType type, string? name = null, string? data = null,
    bool selfClosing = false, Dictionary<string, string>? attributes = null,
    string publicId = "", string systemId = "")
{
    /// <summary>The kind of token.</summary>
    public TokenType Type { get; } = type;
    /// <summary>Tag or doctype name (lower-cased).</summary>
    public string? Name { get; } = name;
    /// <summary>Payload for character and comment tokens.</summary>
    public string? Data { get; } = data;
    /// <summary>Whether the tag uses self-closing syntax.</summary>
    public bool SelfClosing { get; } = selfClosing;
    /// <summary>Attribute map (keys are lower-cased).</summary>
    public Dictionary<string, string> Attributes { get; } = attributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    /// <summary>Doctype PUBLIC identifier (empty when absent). Only set on <see cref="TokenType.Doctype"/> tokens.</summary>
    public string PublicId { get; } = publicId ?? "";
    /// <summary>Doctype SYSTEM identifier (empty when absent). Only set on <see cref="TokenType.Doctype"/> tokens.</summary>
    public string SystemId { get; } = systemId ?? "";
}

/// <summary>
/// Simplified WHATWG-aligned HTML tokenizer (§13.2.5) that processes an
/// HTML string character-by-character.
/// Shared between Broiler.HTML rendering, canonical DOM parsing, and the
/// JavaScript bridge.
/// </summary>
public sealed class HtmlTokenizer
{
    /// <summary>Tokenizes <paramref name="html"/> into an independent token sequence.</summary>
    public IEnumerable<HtmlToken> Tokenize(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        // Construct inside the iterator so repeated or interleaved enumerations never share state.
        foreach (var token in new Scanner(NormalizeNewlines(html)).Read())
            yield return token;
    }

    /// <summary>
    /// Preprocessing the input stream (HTML §13.2.3.5): CRLF pairs and lone CR characters both
    /// become a single LF before tokenizing, so a document's text nodes read the same whichever
    /// line endings the file was saved with.
    /// </summary>
    private static string NormalizeNewlines(string html) =>
        html.Contains('\r') ? html.Replace("\r\n", "\n").Replace('\r', '\n') : html;

    private sealed class Scanner(string input)
    {
        private enum State
        {
            Data,
            TagOpen,
            EndTagOpen,
            TagName,
            BeforeAttributeName,
            AttributeName,
            AfterAttributeName,
            BeforeAttributeValue,
            AttributeValueDoubleQuoted,
            AttributeValueSingleQuoted,
            AttributeValueUnquoted,
            AfterAttributeValueQuoted,
            SelfClosingStartTag,
            BogusComment,
            MarkupDeclarationOpen,
            CommentStart,
            CommentStartDash,
            Comment,
            CommentEndDash,
            CommentEnd,
            Doctype,
            // RCDATA state (HTML §13.2.5.2): text up to the appropriate end tag, character
            // references decoded.
            RcData,
            // RAWTEXT state (HTML §13.2.5.3): the same, with no character reference state. Script
            // text is read here too; the script data state (§13.2.5.4) differs only in its escape
            // states (§13.2.5.15–31), which are not modelled.
            RawText,
            // PLAINTEXT state (HTML §13.2.5.5): the rest of the input is text. No end tag, never
            // decoded, no way out.
            PlainText
        }

        // Elements that contain only text (HTML §13.2.6.2), and the state each start tag leaves the
        // tokenizer in. In the Standard the tokenizer does not decide this: tree construction does,
        // as it inserts the element — "in head" (§13.2.6.4.4) runs the generic RCDATA element parsing
        // algorithm for title and the generic raw text one for style, noframes and noscript, and
        // switches to script data for script; "in body" (§13.2.6.4.7) takes those from "in head" and
        // switches textarea to RCDATA, xmp, iframe and noembed to RAWTEXT, and plaintext to PLAINTEXT.
        //
        // Only script, style and noscript used to switch, so a title, textarea, xmp, iframe, noembed,
        // noframes or plaintext was tokenized as markup: a `<base>` or `<script>` spelled in a
        // textarea's text became an element, a base URL and a script to run, and xmp, iframe,
        // noembed and noframes text had its references decoded.
        //
        // The switch stays here, keyed by name, because this tokenizer is a public API in its own
        // right. HtmlScriptScanner and the host's pre-parse scans (base href, CSP and refresh metas,
        // preload, doctype) read its tokens with no tree builder behind them, the way a browser's
        // preload scanner does, and they must not find a script spelled in a textarea any more than
        // the tree may hold one. The price is that the tokenizer cannot see the tree builder's
        // adjusted current node. The Standard switches only for an element inserted under the rules
        // for HTML content; in SVG or MathML (§13.2.6.5, foreign content) these names are ordinary
        // foreign elements and the tokenizer stays in the data state. So here `<svg><title>` reads
        // RCDATA (markup inside an SVG title becomes text; a plain `<title>Icon</title>` reads the
        // same either way), `<svg><style>` and `<svg><script>` read RAWTEXT, and the names no one
        // writes in SVG (`<svg><textarea>`, `<svg><plaintext>`) would swallow what the Standard parses
        // as markup. At an integration point (`<foreignObject>`, `<math><mtext>`, ...) the rules for
        // HTML content apply and this agrees with the Standard.
        private static State TextStateFor(string tagName) => tagName switch
        {
            "title" or "textarea" => State.RcData,
            "script" or "style" or "xmp" or "iframe" or "noembed" or "noframes" => State.RawText,
            // `noscript` is raw text because scripting is ENABLED. That is the whole condition: with
            // scripting off a browser parses a noscript body as ordinary markup so the fallback can
            // render, and with it on the body is raw text, which is what makes everything inside inert
            // — a nested <script> never runs, an <img> never loads, and the DOM holds one text node
            // rather than a subtree a page can walk into. Broiler has no scripting-disabled mode (no
            // such flag exists anywhere), so the mapping is flat; if one is ever added, this is the
            // line that has to consult it.
            "noscript" => State.RawText,
            "plaintext" => State.PlainText,
            _ => State.Data,
        };

        private string _rawTextTag = string.Empty; // the start tag an RCDATA/RAWTEXT run ends at
        private readonly string _input = input;
        private int _pos;
        private State _state;
        private readonly StringBuilder _tag = new();
        private readonly StringBuilder _buf = new();
        private readonly StringBuilder _av = new();
        private Dictionary<string, string> _attrs = NewAttrs();
        private readonly StringBuilder _attributeName = new();
        private bool _selfClose, _isEnd;

        public IEnumerable<HtmlToken> Read()
        {
            while (true)
            {
                bool eof = _pos >= _input.Length;
                char c = eof ? '\0' : _input[_pos];
                switch (_state)
                {
                    case State.Data:
                        if (eof)
                        {
                            if (_buf.Length > 0)
                                yield return CharTok();
                            yield return Eof();
                            yield break;
                        }

                        if (c == '<')
                        {
                            if (_buf.Length > 0)
                                yield return CharTok();
                            _state = State.TagOpen;
                            _pos++;
                        }
                        else
                        {
                            // Consume the whole run up to the next '<' at once. Character data used
                            // to be appended one char at a time, so a text node cost a loop pass per
                            // character and then two full-size buffers — the builder's copy and the
                            // string materialised from it. Nothing is buffered in the common case, so
                            // the run is emitted straight from the input and materialised once; the
                            // buffered case (a '<' that turned out to be text) still needs the builder
                            // but at least appends in one go. Token boundaries are unchanged: a run is
                            // still only flushed at a '<' or at EOF.
                            var next = _input.IndexOf('<', _pos);
                            var end = next < 0 ? _input.Length : next;
                            if (_buf.Length == 0)
                            {
                                var raw = _input[_pos..end];
                                _pos = end;
                                yield return new HtmlToken(TokenType.Character, data: DecodeReferences(raw));
                            }
                            else
                            {
                                _buf.Append(_input, _pos, end - _pos);
                                _pos = end;
                            }
                        }

                        break;
                    case State.TagOpen:
                        if (eof)
                        {
                            _buf.Append('<');
                            _state = State.Data;
                        }
                        else if (c == '!')
                        {
                            _pos++;
                            _state = State.MarkupDeclarationOpen;
                        }
                        else if (c == '?')
                        {
                            _pos++;
                            SkipProcessingInstruction();
                            _state = State.Data;
                        }
                        else if (c == '/')
                        {
                            _pos++;
                            _state = State.EndTagOpen;
                        }
                        else if (char.IsLetter(c))
                        {
                            Reset(false);
                            _state = State.TagName;
                        }
                        else
                        {
                            _buf.Append('<');
                            _state = State.Data;
                        }

                        break;
                    case State.EndTagOpen:
                        if (eof)
                        {
                            _buf.Append("</");
                            _state = State.Data;
                        }
                        else if (char.IsLetter(c))
                        {
                            Reset(true);
                            _state = State.TagName;
                        }
                        else
                        {
                            _buf.Clear();
                            _state = State.BogusComment;
                        }

                        break;
                    case State.TagName:
                        if (eof)
                        {
                            _state = State.Data;
                        }
                        else if (char.IsWhiteSpace(c))
                        {
                            _pos++;
                            _state = State.BeforeAttributeName;
                        }
                        else if (c == '/')
                        {
                            _pos++;
                            _state = State.SelfClosingStartTag;
                        }
                        else if (c == '>')
                        {
                            _pos++;
                            yield return TagTok();
                        }
                        else
                        {
                            _tag.Append(char.ToLowerInvariant(c));
                            _pos++;
                        }

                        break;
                    case State.BeforeAttributeName:
                        if (eof)
                        {
                            Flush();
                            _state = State.Data;
                        }
                        else if (c == '>')
                        {
                            Flush();
                            _pos++;
                            yield return TagTok();
                        }
                        else if (c == '/')
                        {
                            Flush();
                            _pos++;
                            _state = State.SelfClosingStartTag;
                        }
                        else if (char.IsWhiteSpace(c))
                        {
                            _pos++;
                        }
                        else
                        {
                            Flush();
                            _attributeName.Clear();
                            _av.Clear();
                            _state = State.AttributeName;
                        }

                        break;
                    case State.AttributeName:
                        if (eof || c == '>' || c == '/' || char.IsWhiteSpace(c))
                        {
                            // Attribute name state (HTML §13.2.5.33): reconsume in the after attribute
                            // name state. The name is complete, but the attribute is NOT committed yet —
                            // an '=' may still follow the whitespace, and the value it introduces
                            // belongs to this name.
                            _state = State.AfterAttributeName;
                        }
                        else if (c == '=')
                        {
                            _pos++;
                            _state = State.BeforeAttributeValue;
                        }
                        else
                        {
                            _attributeName.Append(char.ToLowerInvariant(c));
                            _pos++;
                        }

                        break;
                    case State.AfterAttributeName:
                        // After attribute name state (HTML §13.2.5.34). Whitespace between a name and
                        // its '=' belongs to neither, so `href = "x/"` gives href the value x/. The
                        // attribute name state used to hand that whitespace to the before attribute name
                        // state, which read the '=' as the start of the NEXT attribute: it committed
                        // href with an empty value, then read "x/" under an empty name and dropped it.
                        // `<base href = "x/">`, `<script src = …>` and `<meta http-equiv = …>` reached
                        // the token stream and the DOM empty, a duplicate's empty first value won over
                        // the real one, and HtmlBridge had to close the whitespace up in the source
                        // before tokenizing (HtmlSourceAttributes.CloseSpaceBeforeEquals).
                        //
                        // Nothing here appends to the pending name, so Flush refusing a name the tag
                        // already has is the spec's duplicate-attribute check "when the user agent
                        // leaves the attribute name state": the first attribute still wins.
                        //
                        // The whitespace test MUST stay the attribute name state's exit predicate,
                        // char.IsWhiteSpace. The Standard means ASCII whitespace only (TAB, LF, FF,
                        // SPACE), so a U+00A0 belongs to the name — but every tag state here uses the
                        // Unicode predicate, and if this one alone were narrowed, a U+00A0 would bounce
                        // between the two states forever without being consumed (the attribute name
                        // state reconsumes it here, and the anything-else rule below reconsumes it
                        // there). That departure has to be fixed across all the tag states at once.
                        if (eof)
                        {
                            // eof-in-tag: the tag token is never emitted.
                            Flush();
                            _state = State.Data;
                        }
                        else if (char.IsWhiteSpace(c))
                        {
                            _pos++;
                        }
                        else if (c == '=')
                        {
                            // No Flush: the pending name receives the value, and the attribute is
                            // committed when that value ends (or with the tag, when it is missing).
                            _pos++;
                            _state = State.BeforeAttributeValue;
                        }
                        else if (c == '>')
                        {
                            Flush();
                            _pos++;
                            yield return TagTok();
                        }
                        else if (c == '/')
                        {
                            Flush();
                            _pos++;
                            _state = State.SelfClosingStartTag;
                        }
                        else
                        {
                            // A valueless attribute (`<input disabled class=x>`): commit it with its
                            // empty value and start the next attribute, reconsuming this character in
                            // the attribute name state.
                            Flush();
                            _attributeName.Clear();
                            _av.Clear();
                            _state = State.AttributeName;
                        }

                        break;
                    case State.BeforeAttributeValue:
                        if (eof)
                        {
                            _state = State.Data;
                        }
                        else if (char.IsWhiteSpace(c))
                        {
                            _pos++;
                        }
                        else if (c == '"')
                        {
                            _pos++;
                            _state = State.AttributeValueDoubleQuoted;
                        }
                        else if (c == '\'')
                        {
                            _pos++;
                            _state = State.AttributeValueSingleQuoted;
                        }
                        else
                        {
                            _state = State.AttributeValueUnquoted;
                        }

                        break;
                    case State.AttributeValueDoubleQuoted:
                        if (eof)
                        {
                            _state = State.Data;
                        }
                        else if (c == '"')
                        {
                            _pos++;
                            _state = State.AfterAttributeValueQuoted;
                        }
                        else
                        {
                            _av.Append(c);
                            _pos++;
                        }

                        break;
                    case State.AttributeValueSingleQuoted:
                        if (eof)
                        {
                            _state = State.Data;
                        }
                        else if (c == '\'')
                        {
                            _pos++;
                            _state = State.AfterAttributeValueQuoted;
                        }
                        else
                        {
                            _av.Append(c);
                            _pos++;
                        }

                        break;
                    case State.AttributeValueUnquoted:
                        if (eof)
                        {
                            Flush();
                            _state = State.Data;
                        }
                        else if (char.IsWhiteSpace(c))
                        {
                            Flush();
                            _pos++;
                            _state = State.BeforeAttributeName;
                        }
                        else if (c == '>')
                        {
                            Flush();
                            _pos++;
                            yield return TagTok();
                        }
                        else
                        {
                            _av.Append(c);
                            _pos++;
                        }

                        break;
                    case State.AfterAttributeValueQuoted:
                        Flush();
                        if (eof)
                        {
                            _state = State.Data;
                        }
                        else if (char.IsWhiteSpace(c))
                        {
                            _pos++;
                            _state = State.BeforeAttributeName;
                        }
                        else if (c == '/')
                        {
                            _pos++;
                            _state = State.SelfClosingStartTag;
                        }
                        else if (c == '>')
                        {
                            _pos++;
                            yield return TagTok();
                        }
                        else
                        {
                            _state = State.BeforeAttributeName;
                        }

                        break;
                    case State.SelfClosingStartTag:
                        if (eof)
                        {
                            _state = State.Data;
                        }
                        else if (c == '>')
                        {
                            _selfClose = true;
                            _pos++;
                            yield return TagTok();
                        }
                        else
                        {
                            _state = State.BeforeAttributeName;
                        }

                        break;
                    case State.RcData:
                    case State.RawText:
                    {
                        // RCDATA (HTML §13.2.5.2) and RAWTEXT (§13.2.5.3), with their less-than sign,
                        // end tag open and end tag name states (§13.2.5.9–14) folded into
                        // AtAppropriateEndTag. Everything up to the appropriate end tag is one run of
                        // text: no '<' here opens a tag, a comment, a DOCTYPE or a CDATA section, so
                        // `<textarea><!--</textarea>` ends at its end tag. The two states differ only
                        // in '&': RCDATA has the character reference state, so title and textarea text
                        // is decoded; RAWTEXT has none, and neither has script text here.
                        var decode = _state == State.RcData;
                        while (_pos < _input.Length)
                        {
                            if (AtAppropriateEndTag())
                            {
                                if (_buf.Length > 0)
                                    yield return CharTok(decode: decode);
                                // Skip to after the '>'
                                _pos += 2 + _rawTextTag.Length;
                                while (_pos < _input.Length && _input[_pos] != '>')
                                    _pos++;
                                if (_pos < _input.Length)
                                    _pos++; // skip '>'
                                yield return new HtmlToken(TokenType.EndTag, name: _rawTextTag);
                                _rawTextTag = string.Empty;
                                _state = State.Data;
                                break;
                            }

                            // Only a '<' can start the end tag, so take the text up to the next one in
                            // one go rather than a character per loop pass: with title, textarea and
                            // iframe now read here, so is a form's prefilled text or a page's fallback.
                            var next = _input.IndexOf('<', _pos + 1);
                            var end = next < 0 ? _input.Length : next;
                            _buf.Append(_input, _pos, end - _pos);
                            _pos = end;
                        }

                        if (_pos >= _input.Length && (_state is State.RcData or State.RawText))
                        {
                            // End of input: the text read so far is the element's (§13.2.5.2/3 emit the
                            // end-of-file token), and a trailing "</title" was already appended to it.
                            if (_buf.Length > 0)
                                yield return CharTok(decode: decode);
                            _state = State.Data;
                        }
                    }

                        break;
                    case State.PlainText:
                        // PLAINTEXT state (HTML §13.2.5.5): no '<' branch and no '&' branch. The rest of
                        // the input is text — a `</plaintext>` included, since nothing can match it —
                        // and the next token is the last one, end-of-file. The data state flushed any
                        // pending text at the '<' of the start tag, so the buffer is empty here.
                        if (_pos < _input.Length)
                        {
                            yield return new HtmlToken(TokenType.Character, data: _input[_pos..]);
                            _pos = _input.Length;
                        }

                        yield return Eof();
                        yield break;
                    case State.MarkupDeclarationOpen:
                        if (Ahead("--"))
                        {
                            _pos += 2;
                            _buf.Clear();
                            _state = State.CommentStart;
                        }
                        else if (AheadCI("DOCTYPE"))
                        {
                            _pos += 7;
                            _tag.Clear();
                            _state = State.Doctype;
                        }
                        else
                        {
                            _buf.Clear();
                            _state = State.BogusComment;
                        }

                        break;
                    case State.CommentStart:
                        if (eof)
                        {
                            yield return ComTok();
                            _state = State.Data;
                        }
                        else if (c == '-')
                        {
                            _pos++;
                            _state = State.CommentStartDash;
                        }
                        else if (c == '>')
                        {
                            // `<!-->`: the abrupt-closing-of-empty-comment parse error (HTML
                            // §13.2.5.43, comment start state). The comment is empty and over.
                            _pos++;
                            yield return ComTok();
                            _state = State.Data;
                        }
                        else
                        {
                            _state = State.Comment;
                        }

                        break;
                    case State.CommentStartDash:
                        // Comment start dash state (HTML §13.2.5.44). `<!--->` is an abruptly closed
                        // empty comment: its '>' ends it, exactly as the '>' straight after `<!--`
                        // does in the comment start state. This state used to be folded into the
                        // comment end dash state, which has no '>' rule, so `<!--->` read its '>' as
                        // comment text and swallowed the document up to the next `-->` — a DOCTYPE,
                        // the markup after it, a script — all one comment node (and in a fragment,
                        // the wrapper's closing tags too). Apart from '>' it behaves exactly like the
                        // comment end dash state, so no other comment spelling changes: '-' goes on
                        // to the comment end state (`<!---->`), and anything else is one literal
                        // dash of comment data (`<!---x-->` holds "-x").
                        if (eof)
                        {
                            yield return ComTok();
                            _state = State.Data;
                        }
                        else if (c == '-')
                        {
                            _pos++;
                            _state = State.CommentEnd;
                        }
                        else if (c == '>')
                        {
                            _pos++;
                            yield return ComTok();
                            _state = State.Data;
                        }
                        else
                        {
                            // Append the dash and reconsume in the comment state: `c` is not
                            // consumed here, so the comment state is the one that appends it.
                            _buf.Append('-');
                            _state = State.Comment;
                        }

                        break;
                    case State.Comment:
                        if (eof)
                        {
                            yield return ComTok();
                            _state = State.Data;
                        }
                        else if (c == '-')
                        {
                            _pos++;
                            _state = State.CommentEndDash;
                        }
                        else
                        {
                            _buf.Append(c);
                            _pos++;
                        }

                        break;
                    case State.CommentEndDash:
                        if (eof)
                        {
                            yield return ComTok();
                            _state = State.Data;
                        }
                        else if (c == '-')
                        {
                            _pos++;
                            _state = State.CommentEnd;
                        }
                        else
                        {
                            _buf.Append('-');
                            _buf.Append(c);
                            _pos++;
                            _state = State.Comment;
                        }

                        break;
                    case State.CommentEnd:
                        if (eof || c == '>')
                        {
                            if (!eof)
                                _pos++;
                            yield return ComTok();
                            _state = State.Data;
                        }
                        else if (c == '-')
                        {
                            _buf.Append('-');
                            _pos++;
                        }
                        else
                        {
                            _buf.Append("--");
                            _buf.Append(c);
                            _pos++;
                            _state = State.Comment;
                        }

                        break;
                    case State.Doctype:
                        if (eof)
                        {
                            yield return new HtmlToken(TokenType.Doctype);
                            yield return Eof();
                            yield break;
                        }
                        else if (char.IsWhiteSpace(c))
                        {
                            _pos++;
                        }
                        else if (c == '>')
                        {
                            _pos++;
                            yield return new HtmlToken(TokenType.Doctype, name: _tag.ToString());
                            _state = State.Data;
                        }
                        else
                        {
                            ReadDoctype(out var dtPublicId, out var dtSystemId);
                            yield return new HtmlToken(TokenType.Doctype, name: _tag.ToString(), publicId: dtPublicId, systemId: dtSystemId);
                            _state = State.Data;
                        }

                        break;
                    case State.BogusComment:
                        if (eof || c == '>')
                        {
                            if (!eof)
                                _pos++;
                            yield return ComTok();
                            _state = State.Data;
                        }
                        else
                        {
                            _buf.Append(c);
                            _pos++;
                        }

                        break;
                }
            }
        }

        private static Dictionary<string, string> NewAttrs() => new(StringComparer.OrdinalIgnoreCase);
        private void Reset(bool end)
        {
            _isEnd = end;
            _tag.Clear();
            _selfClose = false;
            _attrs = NewAttrs();
            _attributeName.Clear();
            _av.Clear();
        }

        private void Flush()
        {
            // An attribute value's character references are decoded here, in the tokenizer, exactly as
            // character data's are (WHATWG §13.2.5, the attribute-value character-reference states) —
            // so the DOM holds the value the attribute *means*, not the source text that spelled it.
            //
            // This used to leave the raw text for Broiler.HTML's HtmlParser to decode when it built
            // boxes, which made the rendering right and everything reading the DOM wrong:
            // `getAttribute("href")` on `href="?a=1&amp;b=2"` returned `?a=1&amp;b=2`, an
            // `[attr="…"]` selector had to be written against the escaped spelling, and serializing
            // re-escaped the ampersand so a DOM round-trip corrupted the value a little more each time.
            // The downstream decode is gone with it — decoding twice would eat a level of escaping.
            var attributeName = _attributeName.ToString();
            if (attributeName.Length > 0 && !_attrs.ContainsKey(attributeName))
                _attrs[attributeName] = DecodeReferences(_av.ToString());
            _attributeName.Clear();
            _av.Clear();
        }

        private HtmlToken TagTok()
        {
            Flush();
            var tagName = _tag.ToString();
            var tok = new HtmlToken(_isEnd ? TokenType.EndTag : TokenType.StartTag, name: tagName, selfClosing: _selfClose, attributes: _attrs);

            // Every state that emits a tag does it through here, so this is the one place that decides
            // the state the tokenizer resumes in — before the token is yielded. Each call site used to
            // reset the state to data after the yield unless it read RawText, which a new text state
            // (or a new tag state that copied the line) would silently undo.
            //
            // A self-closing start tag does not switch. For an HTML element that departs from the
            // Standard: the self-closing flag on a non-void element is a parse error with no effect,
            // so `<textarea/>` and `<iframe src=x />` still open RCDATA and RAWTEXT. But in foreign
            // content (§13.2.6.5) the flag is acknowledged and the element popped at once, and a
            // switch keyed by name cannot tell `<svg><title/>` from `<title/>`: switching would turn
            // the rest of a page with an inline SVG icon into title text. HtmlDocumentParser does not
            // push a self-closing element either, so a switched tokenizer would pour that text into
            // the element's parent, and the host's pre-parse scans already read a self-closing tag as
            // opening nothing. Title is the parser's exception: its branch pushes the element whatever
            // the flag says, so after `<title/>` the markup that follows, which stays markup here,
            // becomes the title element's children (a separate inconsistency of the tree builder).
            _state = State.Data;
            if (!_isEnd && !_selfClose)
            {
                _state = TextStateFor(tagName);
                if (_state is State.RcData or State.RawText)
                    _rawTextTag = tagName;
            }

            return tok;
        }

        /// <summary>
        /// Whether the input at the current position is the appropriate end tag of the RCDATA or
        /// RAWTEXT run in progress (HTML §13.2.5.9–14): <c>&lt;/</c>, the name of the start tag that
        /// began the run in any ASCII case, and then tab, LF, FF, space, <c>/</c> or <c>&gt;</c>.
        /// </summary>
        /// <remarks>
        /// Anything else is "anything else" in the end tag open or end tag name state, which emits
        /// <c>&lt;/</c> and the name read so far as text: <c>&lt;/ textarea&gt;</c>,
        /// <c>&lt;/textareax&gt;</c>, a name of another text element, or an end tag the input cuts
        /// off before its terminator (<c>a&lt;/title</c> at end of input stays text). Case is folded
        /// for ASCII letters only, and the terminators are ASCII whitespace only — CR cannot occur
        /// once the input stream is preprocessed. Script text shares the test (the script data end tag
        /// name state, §13.2.5.17, has the same rule); it used to match with
        /// <c>OrdinalIgnoreCase</c> and <c>char.IsWhiteSpace</c>, so <c>&lt;/script\v&gt;</c> or a
        /// U+00A0 after the name ended a script there too.
        /// </remarks>
        private bool AtAppropriateEndTag()
        {
            var name = _pos + 2;
            var terminator = name + _rawTextTag.Length;
            if (terminator >= _input.Length || _input[_pos] != '<' || _input[_pos + 1] != '/')
                return false;

            for (var i = 0; i < _rawTextTag.Length; i++)
            {
                var c = _input[name + i];
                if ((c is >= 'A' and <= 'Z' ? (char)(c + ('a' - 'A')) : c) != _rawTextTag[i])
                    return false;
            }

            return _input[terminator] is '\t' or '\n' or '\f' or ' ' or '/' or '>';
        }

        private HtmlToken CharTok(bool decode = true)
        {
            var raw = _buf.ToString();
            _buf.Clear();
            return new HtmlToken(TokenType.Character, data: decode ? DecodeReferences(raw) : raw);
        }

        /// <summary>
        /// Decodes HTML character references (named like <c>&amp;nbsp;</c>, decimal
        /// <c>&amp;#160;</c>, and hex <c>&amp;#xA0;</c>) in ordinary character data, in RCDATA
        /// (<c>&lt;title&gt;</c>/<c>&lt;textarea&gt;</c> text) and in attribute values (WHATWG §13.2.5
        /// character-reference state). RAWTEXT (style, xmp, iframe, noembed, noframes, noscript),
        /// script and PLAINTEXT text has no character reference state and is emitted undecoded — its
        /// call sites pass <c>decode: false</c> or skip this. A fast path skips strings with no
        /// ampersand.
        /// </summary>
        /// <remarks>
        /// Only a reference terminated by <c>;</c> is decoded, which is what makes this safe to share
        /// with attribute values: the spec's ambiguous-ampersand rule exists to keep
        /// <c>href="?a=1&amp;copy=2"</c> from turning into a <c>©</c>, and a semicolon-only decoder
        /// never had to be told. The cost is the other half of that rule — a terminator-less
        /// <c>title="&amp;copy"</c> stays literal where a browser would resolve it — which is the
        /// same conservative gap this helper already had in character data.
        /// </remarks>
        private static string DecodeReferences(string value) => value.IndexOf('&') < 0 ? value : System.Net.WebUtility.HtmlDecode(value);
        private HtmlToken ComTok()
        {
            var t = new HtmlToken(TokenType.Comment, data: _buf.ToString());
            _buf.Clear();
            return t;
        }

        private static HtmlToken Eof() => new(TokenType.EndOfFile);
        private bool Ahead(string s) => _pos + s.Length <= _input.Length && _input.AsSpan(_pos, s.Length).SequenceEqual(s.AsSpan());
        private bool AheadCI(string s) => _pos + s.Length <= _input.Length && string.Compare(_input, _pos, s, 0, s.Length, StringComparison.OrdinalIgnoreCase) == 0;
        // Reads a DOCTYPE's name into _tag, plus its optional PUBLIC/SYSTEM external-identifier
        // strings (HTML Standard §13.2.5.53-70), then consumes through the closing '>'. Anything
        // unrecognized between the identifiers and '>' is ignored, matching the prior name-only skip.
        private void ReadDoctype(out string publicId, out string systemId)
        {
            publicId = "";
            systemId = "";
            _tag.Clear();
            while (_pos < _input.Length && _input[_pos] != '>' && !char.IsWhiteSpace(_input[_pos]))
            {
                _tag.Append(char.ToLowerInvariant(_input[_pos]));
                _pos++;
            }

            SkipWhitespace();
            if (AheadCI("PUBLIC"))
            {
                _pos += 6;
                SkipWhitespace();
                publicId = ReadDoctypeQuotedString();
                SkipWhitespace();
                systemId = ReadDoctypeQuotedString();
            }
            else if (AheadCI("SYSTEM"))
            {
                _pos += 6;
                SkipWhitespace();
                systemId = ReadDoctypeQuotedString();
            }

            while (_pos < _input.Length && _input[_pos] != '>')
                _pos++;
            if (_pos < _input.Length)
                _pos++;
        }

        private void SkipWhitespace()
        {
            while (_pos < _input.Length && char.IsWhiteSpace(_input[_pos]))
                _pos++;
        }

        // Reads a single- or double-quoted DOCTYPE identifier string (without the quotes).
        // Returns "" when the next character is not a quote.
        private string ReadDoctypeQuotedString()
        {
            if (_pos >= _input.Length || (_input[_pos] != '"' && _input[_pos] != '\''))
                return "";
            var quote = _input[_pos];
            _pos++;
            var start = _pos;
            while (_pos < _input.Length && _input[_pos] != quote && _input[_pos] != '>')
                _pos++;
            var value = _input[start.._pos];
            if (_pos < _input.Length && _input[_pos] == quote)
                _pos++;
            return value;
        }

        /// <summary>
        /// Skips an XML processing instruction (<c>&lt;?...?&gt;</c>), such as
        /// <c>&lt;?xml version="1.0" encoding="UTF-8"?&gt;</c>.
        /// </summary>
        private void SkipProcessingInstruction()
        {
            while (_pos < _input.Length)
            {
                if (_input[_pos] == '?' && _pos + 1 < _input.Length && _input[_pos + 1] == '>')
                {
                    _pos += 2;
                    return;
                }

                if (_input[_pos] == '>')
                {
                    _pos++;
                    return;
                }

                _pos++;
            }
        }
    }
}
