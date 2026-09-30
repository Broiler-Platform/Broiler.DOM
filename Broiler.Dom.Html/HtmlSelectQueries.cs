using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Broiler.Dom.Html;

/// <summary>
/// HTML-semantic select and option operations (HTMLSelectElement and HTMLOptionElement).
/// Stateless option collection, whitespace collapsing, option/select value resolution,
/// and option addition algorithms with no host or JavaScript engine dependencies.
/// </summary>
// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
// Broiler-Falsified-If: the value reported for a select comes from an option other than the one at its resolved selected index
// Broiler-Human:        PENDING
public static class HtmlSelectQueries
{
    // -------- Option Collection --------

    /// <summary>
    /// Collects all descendant &lt;option&gt; elements of <paramref name="select"/> in tree order,
    /// recursing into containers such as &lt;optgroup&gt;.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: an option inside an optgroup is missing from the returned list
    // Broiler-Human:        PENDING
    public static IReadOnlyList<DomElement> GetOptions(DomElement select)
    {
        ArgumentNullException.ThrowIfNull(select);
        var options = new List<DomElement>();
        CollectOptionsRecursive(select, options);
        return options;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: an option nested inside another option is listed as a separate option
    // Broiler-Human:        PENDING
    private static void CollectOptionsRecursive(DomElement parent, List<DomElement> options)
    {
        foreach (var child in parent.ChildNodes.OfType<DomElement>())
        {
            if (string.Equals(child.TagName, "option", StringComparison.OrdinalIgnoreCase))
            {
                options.Add(child);
                continue;
            }

            CollectOptionsRecursive(child, options);
        }
    }

    /// <summary>
    /// Returns whether <paramref name="element"/> is an HTML option element:
    /// local name "option" in the HTML namespace (or default namespace).
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an SVG or MathML element whose local name is option is reported as an HTML option
    // Broiler-Human:        PENDING
    public static bool IsHtmlOption(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return string.Equals(element.LocalName, "option", StringComparison.Ordinal) &&
               element.NamespaceUri == DomNamespaces.Html;
    }

    // -------- Option Text and Value --------

    /// <summary>
    /// Resolves an option's text per HTML §4.10.10: descendant text nodes in tree order,
    /// skipping descendant &lt;script&gt; elements and shadow trees, with ASCII whitespace
    /// stripped from both ends and internal runs collapsed to a single space.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.10.10; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: leading or trailing ASCII whitespace survives, or a run of inner whitespace is not collapsed to one space
    // Broiler-Human:        PENDING
    public static string GetOptionText(DomElement option)
    {
        ArgumentNullException.ThrowIfNull(option);
        var builder = new StringBuilder();
        AppendNonScriptText(option, builder);
        var raw = builder.ToString();

        var text = new StringBuilder(raw.Length);
        var pendingSpace = false;
        foreach (var c in raw)
        {
            if (c is '\t' or '\n' or '\f' or '\r' or ' ')
            {
                pendingSpace = text.Length > 0;
                continue;
            }

            if (pendingSpace)
                text.Append(' ');
            pendingSpace = false;
            text.Append(c);
        }

        return text.ToString();
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: text inside a script element nested in the option is included in the option's text
    // Broiler-Human:        PENDING
    private static void AppendNonScriptText(DomNode node, StringBuilder text)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child is DomText textNode)
            {
                text.Append(textNode.Data);
            }
            else if (child is DomElement element &&
                     !IsHtmlOrSvgScript(element) &&
                     !string.Equals(element.TagName, "#shadow-root", StringComparison.Ordinal))
            {
                AppendNonScriptText(element, text);
            }
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an SVG script element is not recognised, so its text joins the option's text
    // Broiler-Human:        PENDING
    private static bool IsHtmlOrSvgScript(DomElement element) =>
        string.Equals(element.LocalName, "script", StringComparison.Ordinal) &&
        element.NamespaceUri is DomNamespaces.Html or DomNamespaces.Svg;

    /// <summary>
    /// Resolves an option's effective value: the "value" content attribute if present,
    /// falling back to <see cref="GetOptionText"/>.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: an option with an empty value attribute reports its text instead of the empty string
    // Broiler-Human:        PENDING
    public static string GetOptionValue(DomElement option)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (option.TryGetAttributeByQualifiedName("value", out var attrValue))
            return attrValue;
        return GetOptionText(option);
    }

    // -------- Selection and Value Resolution --------

    /// <summary>
    /// Resolves the selected index of <paramref name="select"/>:
    /// <paramref name="dirtyIndex"/> if valid, otherwise the first option marked "selected"
    /// or <paramref name="isDefaultSelected"/>, otherwise 0 (or -1 if no options exist).
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: a single select with the selected attribute on two options resolves to the first of them, where the later one is the selected option
    // Broiler-Human:        PENDING
    public static int ResolveSelectedIndex(
        DomElement select,
        int? dirtyIndex = null,
        Func<DomElement, bool>? isDefaultSelected = null)
    {
        ArgumentNullException.ThrowIfNull(select);
        var options = GetOptions(select);
        if (options.Count == 0)
            return -1;

        if (dirtyIndex.HasValue)
            return dirtyIndex.Value >= 0 && dirtyIndex.Value < options.Count ? dirtyIndex.Value : -1;

        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            if (option.HasAttributeByQualifiedName("selected") || (isDefaultSelected?.Invoke(option) == true))
                return index;
        }

        return 0;
    }

    /// <summary>
    /// Resolves the current string value of <paramref name="select"/> based on its selected index.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: an override that returns null makes the select's value empty instead of falling back to the option's own value
    // Broiler-Human:        PENDING
    public static string ResolveSelectValue(
        DomElement select,
        int? dirtyIndex = null,
        Func<DomElement, bool>? isDefaultSelected = null,
        Func<DomElement, string?>? getOptionValueOverride = null)
    {
        ArgumentNullException.ThrowIfNull(select);
        var options = GetOptions(select);
        var selectedIndex = ResolveSelectedIndex(select, dirtyIndex, isDefaultSelected);
        if (selectedIndex < 0 || selectedIndex >= options.Count)
            return string.Empty;

        var option = options[selectedIndex];
        if (getOptionValueOverride != null && getOptionValueOverride(option) is { } overridden)
            return overridden;

        return GetOptionValue(option);
    }

    /// <summary>
    /// Finds the 0-based index of the first option whose value matches <paramref name="value"/>,
    /// or -1 if no option matches.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: a value that matches an option only when compared case-insensitively returns that option's index
    // Broiler-Human:        PENDING
    public static int FindOptionIndexByValue(
        DomElement select,
        string value,
        Func<DomElement, string?>? getOptionValueOverride = null)
    {
        ArgumentNullException.ThrowIfNull(select);
        ArgumentNullException.ThrowIfNull(value);
        var options = GetOptions(select);
        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            var optionValue = getOptionValueOverride != null && getOptionValueOverride(option) is { } overridden
                ? overridden
                : GetOptionValue(option);

            if (string.Equals(optionValue, value, StringComparison.Ordinal))
                return index;
        }

        return -1;
    }

    // -------- Option Addition & Attributes --------

    /// <summary>
    /// Adds <paramref name="option"/> to <paramref name="select"/> before <paramref name="reference"/>,
    /// or appends to <paramref name="select"/> if <paramref name="reference"/> is null or not a child.
    /// Detaches <paramref name="option"/> from its previous parent first.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: adding an option that is an ancestor of the select detaches it from its parent before the insert throws
    // Broiler-Human:        PENDING
    public static void AddOption(DomElement select, DomElement option, DomElement? reference = null)
    {
        ArgumentNullException.ThrowIfNull(select);
        ArgumentNullException.ThrowIfNull(option);

        option.Remove();
        if (reference != null && ReferenceEquals(reference.ParentElement, select))
            select.InsertBefore(option, reference);
        else
            select.AppendChild(option);
    }

    /// <summary>
    /// Reads the positive integer value of the "size" attribute on <paramref name="select"/>,
    /// or 0 if missing or invalid.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a size attribute of zero or a negative number is returned as the select's size
    // Broiler-Human:        PENDING
    public static int GetSize(DomElement select)
    {
        ArgumentNullException.ThrowIfNull(select);
        if (select.TryGetAttributeByQualifiedName("size", out var rawSize) &&
            int.TryParse(rawSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSize) &&
            parsedSize > 0)
        {
            return parsedSize;
        }

        return 0;
    }
}
