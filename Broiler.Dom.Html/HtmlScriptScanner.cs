using System;
using System.Collections.Generic;
using System.Text;

namespace Broiler.Dom.Html;

/// <summary>One discovered <c>&lt;script&gt;</c> element: its parsed (lower-cased)
/// attribute map and its raw, never-entity-decoded body text.</summary>
public readonly record struct HtmlScriptTag(IReadOnlyDictionary<string, string> Attributes, string RawContent);

/// <summary>
/// Locates <c>&lt;script&gt;</c> elements in HTML source text via the shared
/// <see cref="HtmlTokenizer"/>. Because <c>&lt;script&gt;</c> is a raw-text element, a
/// <c>&lt;script&gt;</c> literal inside a comment or another element's text is not
/// discovered, a <c>&gt;</c> inside a quoted attribute does not truncate the start tag,
/// and the body is taken verbatim (raw text is never entity-decoded).
/// </summary>
/// <remarks>
/// Pure HTML scanning with no host, policy, or JavaScript coupling — promoted from the
/// HtmlBridge script-extraction service, which now layers CSP/nonce gating, module
/// classification, and external fetching on top of this primitive.
/// </remarks>
public static class HtmlScriptScanner
{
    /// <summary>
    /// Enumerates every <c>&lt;script&gt;</c> element in document order, pairing each start
    /// tag with its raw body text. A start tag is followed by its character content and then
    /// its end tag; an unterminated final script yields its content to end-of-input (matching
    /// parser behaviour).
    /// </summary>
    public static IEnumerable<HtmlScriptTag> EnumerateScripts(string html)
    {
        HtmlToken? open = null;
        var content = new StringBuilder();

        foreach (var token in new HtmlTokenizer().Tokenize(html))
        {
            if (open != null)
            {
                if (token.Type == TokenType.Character)
                {
                    content.Append(token.Data);
                    continue;
                }

                // Any non-character token (the </script> end tag) closes the current script.
                yield return new HtmlScriptTag(open.Attributes, content.ToString());
                open = null;
                content.Clear();
            }

            if (token.Type == TokenType.StartTag &&
                string.Equals(token.Name, "script", StringComparison.OrdinalIgnoreCase))
            {
                open = token;
                content.Clear();
            }
        }

        if (open != null)
            yield return new HtmlScriptTag(open.Attributes, content.ToString());
    }
}
