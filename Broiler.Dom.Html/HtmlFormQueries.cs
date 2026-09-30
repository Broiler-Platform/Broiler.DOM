using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Dom;

namespace Broiler.Dom.Html;

/// <summary>
/// Queries over HTML form controls and form ownership per HTML §4.10.
/// Includes form owner resolution, form controls collection, radio group discovery,
/// control disablement checks, and default value/checked reflection.
/// </summary>
// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
// Broiler-Falsified-If: a control whose form attribute names another form's id is listed among this form's controls
// Broiler-Human:        PENDING
public static class HtmlFormQueries
{
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a button, output or object element is missing from the set and so is left out of its form's controls
    // Broiler-Human:        PENDING
    private static readonly HashSet<string> ListedControlTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "button", "fieldset", "input", "object", "output", "select", "textarea"
    };

    /// <summary>
    /// Checks whether <paramref name="element"/> is a listed form-associated control (HTML §4.10.1.1:
    /// <c>button</c>, <c>fieldset</c>, <c>input</c>, <c>object</c>, <c>output</c>, <c>select</c>, <c>textarea</c>).
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a select or textarea whose tag name is upper case is not reported as a listed control
    // Broiler-Human:        PENDING
    public static bool IsListedFormControl(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return ListedControlTags.Contains(element.TagName);
    }

    /// <summary>
    /// Checks whether <paramref name="element"/> is an <c>&lt;input type="radio"&gt;</c>.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an input whose type attribute is 'RADIO' in upper case is not reported as a radio
    // Broiler-Human:        PENDING
    public static bool IsRadioInput(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return string.Equals(element.TagName, "input", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(element.GetAttributeByQualifiedName("type"), "radio", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks whether <paramref name="element"/> is an <c>&lt;input type="checkbox"&gt;</c>.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an input whose type attribute is 'CHECKBOX' in upper case is not reported as a checkbox
    // Broiler-Human:        PENDING
    public static bool IsCheckboxInput(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return string.Equals(element.TagName, "input", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(element.GetAttributeByQualifiedName("type"), "checkbox", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Finds the form owner of <paramref name="control"/> per HTML §4.10.18.3.
    /// Checks the <c>form</c> content attribute first (resolving against document/tree ID),
    /// falling back to the nearest ancestor <c>&lt;form&gt;</c> element.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: a disconnected control that carries a form attribute and sits inside a form gets no owner instead of that ancestor form
    // Broiler-Human:        PENDING
    public static DomElement? GetFormOwner(DomElement control)
    {
        ArgumentNullException.ThrowIfNull(control);

        var formId = control.GetAttributeByQualifiedName("form");
        if (!string.IsNullOrEmpty(formId))
        {
            var root = control.GetRootNode();
            if (root is DomDocument doc)
            {
                var candidate = doc.GetElementById(formId);
                return candidate is not null && string.Equals(candidate.TagName, "form", StringComparison.OrdinalIgnoreCase)
                    ? candidate
                    : null;
            }

            foreach (var el in root.InclusiveDescendants().OfType<DomElement>())
            {
                if (string.Equals(el.TagName, "form", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(el.Id, formId, StringComparison.Ordinal))
                {
                    return el;
                }
            }

            return null;
        }

        for (var ancestor = control.ParentElement; ancestor != null; ancestor = ancestor.ParentElement)
        {
            if (string.Equals(ancestor.TagName, "form", StringComparison.OrdinalIgnoreCase))
                return ancestor;
        }

        return null;
    }

    /// <summary>
    /// Collects all listed elements associated with <paramref name="form"/> in tree order (HTML §4.10.3).
    /// Includes both descendants belonging to the form and external elements referencing this form by id.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: a listed control outside the form that names it through its form attribute is left out of the returned list
    // Broiler-Human:        PENDING
    public static List<DomElement> GetFormElements(DomElement form, Func<DomElement, bool>? isCustomFormAssociated = null)
    {
        ArgumentNullException.ThrowIfNull(form);

        var root = form.GetRootNode();
        var controls = new List<DomElement>();

        foreach (var element in root.InclusiveDescendants().OfType<DomElement>())
        {
            if (IsListedFormControl(element) || (isCustomFormAssociated?.Invoke(element) ?? false))
            {
                if (ReferenceEquals(GetFormOwner(element), form))
                    controls.Add(element);
            }
        }

        return controls;
    }

    /// <summary>
    /// Collects all radio buttons in the same radio group as <paramref name="radio"/> in tree order (HTML §4.10.5.1.16).
    /// Members of a radio group share the same non-empty name attribute and the same form owner
    /// (or both have no form owner within the same root tree).
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s4.10.5.1.16; IP=Low; Security=Medium; Resources=6; Fingerprint=TBF
    // Broiler-Falsified-If: a radio with the same name but a different form owner is returned as part of the group
    // Broiler-Human:        PENDING
    public static List<DomElement> GetRadioGroupElements(DomElement radio)
    {
        ArgumentNullException.ThrowIfNull(radio);
        if (!IsRadioInput(radio))
            return [radio];

        var name = radio.GetAttributeByQualifiedName("name");
        if (string.IsNullOrEmpty(name))
            return [radio];

        var formOwner = GetFormOwner(radio);
        var root = radio.GetRootNode();
        var group = new List<DomElement>();

        foreach (var element in root.InclusiveDescendants().OfType<DomElement>())
        {
            if (IsRadioInput(element) &&
                string.Equals(element.GetAttributeByQualifiedName("name"), name, StringComparison.Ordinal) &&
                ReferenceEquals(GetFormOwner(element), formOwner))
            {
                group.Add(element);
            }
        }

        return group;
    }

    /// <summary>
    /// Checks whether <paramref name="control"/> is disabled by its own <c>disabled</c> attribute
    /// or by an ancestor <c>&lt;fieldset disabled&gt;</c> (HTML §4.10.15).
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an input inside a span within the first legend of a disabled fieldset is reported disabled
    // Broiler-Human:        PENDING
    public static bool IsFormControlDisabled(DomElement control)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (control.HasAttributeByQualifiedName("disabled"))
            return true;

        for (var ancestor = control.ParentElement; ancestor != null; ancestor = ancestor.ParentElement)
        {
            if (string.Equals(ancestor.TagName, "fieldset", StringComparison.OrdinalIgnoreCase) &&
                ancestor.HasAttributeByQualifiedName("disabled"))
            {
                // Elements inside the first <legend> child of a fieldset are not disabled by that fieldset.
                if (control.ParentElement is DomElement parent &&
                    string.Equals(parent.TagName, "legend", StringComparison.OrdinalIgnoreCase) &&
                    ReferenceEquals(parent.ParentElement, ancestor) &&
                    ReferenceEquals(ancestor.FirstElementChild, parent))
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the default value of <paramref name="control"/>: child text content for <c>&lt;textarea&gt;</c>,
    /// or the <c>value</c> content attribute for other controls (HTML §4.10.11, §4.10.5.1.1).
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a textarea's default value includes text from a nested element or a comment rather than only its direct text children
    // Broiler-Human:        PENDING
    public static string GetDefaultValue(DomElement control)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (string.Equals(control.TagName, "textarea", StringComparison.OrdinalIgnoreCase))
        {
            var sb = new System.Text.StringBuilder();
            foreach (var child in control.ChildNodes)
            {
                if (child is DomText text)
                    sb.Append(text.Data);
            }
            return sb.ToString();
        }

        return control.GetAttributeByQualifiedName("value") ?? string.Empty;
    }

    /// <summary>
    /// Gets the default checked state of <paramref name="control"/> (whether it carries a <c>checked</c> content attribute).
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an input whose checked attribute is present but empty reports unchecked by default
    // Broiler-Human:        PENDING
    public static bool GetDefaultChecked(DomElement control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.HasAttributeByQualifiedName("checked");
    }
}
