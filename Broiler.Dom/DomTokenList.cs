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
public sealed class DomTokenList
{
    // DOM Standard "ASCII whitespace": TAB, LF, FF, CR, SPACE.
    private static readonly char[] AsciiWhitespace = ['\t', '\n', '\f', '\r', ' '];

    private readonly DomElement _element;
    private readonly string _attributeName;

    /// <summary>Creates a token list over <paramref name="attributeName"/> of <paramref name="element"/>.</summary>
    public DomTokenList(DomElement element, string attributeName)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrEmpty(attributeName);
        _element = element;
        _attributeName = attributeName;
    }

    /// <summary>The number of tokens in the set.</summary>
    public int Length => Parse().Count;

    /// <summary>
    /// The serialized attribute value. Getting returns the raw attribute (or the
    /// empty string when absent); setting replaces the attribute verbatim.
    /// </summary>
    public string Value
    {
        get => _element.GetAttribute(_attributeName) ?? string.Empty;
        set => _element.SetAttribute(_attributeName, value);
    }

    /// <summary>The token at <paramref name="index"/> in tree order, or <c>null</c> if out of range.</summary>
    public string? Item(int index)
    {
        var tokens = Parse();
        return index >= 0 && index < tokens.Count ? tokens[index] : null;
    }

    /// <summary>The ordered, de-duplicated tokens.</summary>
    public IReadOnlyList<string> ToList() => Parse();

    /// <summary>Whether <paramref name="token"/> is present. An empty token is never present.</summary>
    public bool Contains(string token) =>
        !string.IsNullOrEmpty(token) && Parse().Contains(token);

    /// <summary>Adds each token to the set (no-op for tokens already present).</summary>
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
    public bool Replace(string token, string newToken)
    {
        ValidateToken(token);
        ValidateToken(newToken);

        var set = Parse();
        var index = set.IndexOf(token);
        if (index < 0)
            return false;

        if (set.Contains(newToken))
            set.RemoveAt(index); // newToken already present — drop the old slot to de-duplicate
        else
            set[index] = newToken;

        Update(set);
        return true;
    }

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

    private void Update(List<string> tokens) =>
        _element.SetAttribute(_attributeName, string.Join(' ', tokens));

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
