using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.Dom;

/// <summary>
/// A live, ordered set of space-separated tokens reflected to an element
/// attribute (DOM Standard §7.1 <c>DOMTokenList</c>), the model behind
/// <c>Element.classList</c>. Tokens are split on ASCII whitespace, kept unique in
/// insertion order, and serialized back to the attribute on mutation. This is the
/// engine-neutral ordered-set algorithm; the JavaScript wrapper (argument
/// marshaling, live indexed access, and error surfacing) stays in the bridge.
/// </summary>
// Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s7.1; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
// Broiler-Falsified-If: a token containing a form feed or carriage return is accepted and then splits into two tokens on the next read of the attribute
// Broiler-Human:        PENDING
public sealed class DomTokenList
{
    // DOM Standard "ASCII whitespace": TAB, LF, FF, CR, SPACE.
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a character outside TAB, LF, FF, CR and SPACE splits tokens, or one of those five does not
    // Broiler-Human:        PENDING
    private static readonly char[] AsciiWhitespace = ['\t', '\n', '\f', '\r', ' '];

    private readonly DomElement _element;
    private readonly string _attributeName;

    /// <summary>Creates a token list over <paramref name="attributeName"/> of <paramref name="element"/>.</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a null element or an empty attribute name is accepted instead of throwing
    // Broiler-Human:        PENDING
    public DomTokenList(DomElement element, string attributeName)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrEmpty(attributeName);
        _element = element;
        _attributeName = attributeName;
    }

    /// <summary>The number of tokens in the set.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a token repeated in the attribute is counted twice
    // Broiler-Human:        PENDING
    public int Length => Parse().Count;

    /// <summary>
    /// The serialized attribute value. Getting returns the raw attribute (or the
    /// empty string when absent); setting replaces the attribute verbatim.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: setting the value stores anything other than the given string, or an absent attribute reads as null instead of the empty string
    // Broiler-Human:        PENDING
    public string Value
    {
        get => _element.GetAttribute(_attributeName) ?? string.Empty;
        set => _element.SetAttribute(_attributeName, value);
    }

    /// <summary>The token at <paramref name="index"/> in tree order, or <c>null</c> if out of range.</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an index equal to the token count, or a negative index, returns a token instead of null
    // Broiler-Human:        PENDING
    public string? Item(int index)
    {
        var tokens = Parse();
        return index >= 0 && index < tokens.Count ? tokens[index] : null;
    }

    /// <summary>The ordered, de-duplicated tokens.</summary>
    public IReadOnlyList<string> ToList() => Parse();

    /// <summary>Whether <paramref name="token"/> is present. An empty token is never present.</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: the empty string, or a token differing only in letter case, is reported present
    // Broiler-Human:        PENDING
    public bool Contains(string token) =>
        !string.IsNullOrEmpty(token) && Parse().Contains(token);

    /// <summary>Adds each token to the set (no-op for tokens already present).</summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s7.1; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: adding a token already present leaves duplicate tokens or extra whitespace in the attribute instead of rewriting it as the ordered set's serialization
    // Broiler-Human:        PENDING
    public void Add(params string[] tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        foreach (var token in tokens)
            ValidateToken(token);

        var set = Parse();
        var seen = new HashSet<string>(set, StringComparer.Ordinal);
        var changed = false;
        foreach (var token in tokens)
        {
            if (seen.Add(token))
            {
                set.Add(token);
                changed = true;
            }
        }

        if (changed)
            Update(set);
    }

    /// <summary>Removes each token from the set.</summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s7.1; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: removing an absent token leaves duplicate tokens or extra whitespace in the attribute instead of rewriting it as the ordered set's serialization
    // Broiler-Human:        PENDING
    public void Remove(params string[] tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        foreach (var token in tokens)
            ValidateToken(token);

        var set = Parse();
        var toRemove = new HashSet<string>(tokens, StringComparer.Ordinal);
        var updated = set.Where(token => !toRemove.Contains(token)).ToList();
        if (updated.Count != set.Count)
            Update(updated);
    }

    /// <summary>
    /// Toggles <paramref name="token"/>. With <paramref name="force"/> supplied, adds
    /// it when <c>true</c> and removes it when <c>false</c>. Returns whether the token
    /// is present after the call.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s7.1; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: toggling a present token with force true removes it, or toggling an absent token with force false adds it
    // Broiler-Human:        PENDING
    public bool Toggle(string token, bool? force = null)
    {
        ValidateToken(token);
        var set = Parse();
        var present = set.Contains(token);

        if (present)
        {
            if (force == true)
                return true;
            set.Remove(token);
            Update(set);
            return false;
        }

        if (force == false)
            return false;
        set.Add(token);
        Update(set);
        return true;
    }

    /// <summary>
    /// Replaces <paramref name="token"/> with <paramref name="newToken"/>, preserving
    /// position. Returns <c>false</c> without changes when <paramref name="token"/> is
    /// absent.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-DOM s7.1; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: replacing a token with one that already appears later in the set leaves the replacement at the later position instead of the replaced token's position
    // Broiler-Human:        PENDING
    public bool Replace(string token, string newToken)
    {
        ValidateToken(token);
        ValidateToken(newToken);

        var set = Parse();
        var index = set.IndexOf(token);
        if (index < 0)
            return false;

        if (string.Equals(token, newToken, StringComparison.Ordinal))
        {
            Update(set);
            return true;
        }

        if (set.Contains(newToken))
            set.RemoveAt(index); // newToken already present — drop the old slot to de-duplicate
        else
            set[index] = newToken;

        Update(set);
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a token repeated in the attribute appears twice in the parsed list, or adjacent whitespace produces an empty token
    // Broiler-Human:        PENDING
    private List<string> Parse()
    {
        var raw = _element.GetAttribute(_attributeName) ?? string.Empty;
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in raw.Split(AsciiWhitespace, StringSplitOptions.RemoveEmptyEntries))
        {
            if (seen.Add(token))
                result.Add(token);
        }

        return result;
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: the serialized attribute joins tokens with anything other than a single space
    // Broiler-Human:        PENDING
    private void Update(List<string> tokens) =>
        _element.SetAttribute(_attributeName, string.Join(' ', tokens));

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an empty token, or one containing TAB, LF, FF, CR or SPACE, is accepted without an exception
    // Broiler-Human:        PENDING
    private static void ValidateToken(string token)
    {
        // DOM Standard: an empty token is a SyntaxError; a token containing ASCII
        // whitespace is an InvalidCharacterError. The engine-neutral list surfaces
        // these as ArgumentException; the bridge maps them to DOMException.
        if (string.IsNullOrEmpty(token))
            throw new ArgumentException("The token provided must not be empty.", nameof(token));
        if (token.AsSpan().IndexOfAny(AsciiWhitespace) >= 0)
            throw new ArgumentException("The token provided must not contain ASCII whitespace.", nameof(token));
    }
}
