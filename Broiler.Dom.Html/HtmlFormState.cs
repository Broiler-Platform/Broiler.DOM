using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Broiler.Dom;

namespace Broiler.Dom.Html;

/// <summary>
/// Companion state container for HTML form controls, tracking dirty IDL value and checkedness
/// states, selected option state, and radio-button mutual exclusion per HTML §4.10.
/// </summary>
public sealed class HtmlFormState
{
    private readonly ConditionalWeakTable<DomElement, ElementFormState> _states = [];

    /// <summary>
    /// Optional notification callback invoked whenever form control state is modified or cleared.
    /// Used by host environments (e.g. to advance runtime state epochs or invalidate style scopes).
    /// </summary>
    public Action? OnStateChanged { get; set; }

    private sealed class ElementFormState
    {
        public bool IsValueDirty;
        public string? Value;

        public bool IsCheckedDirty;
        public bool Checked;

        public bool IsSelectedIndexDirty;
        public int SelectedIndex;

        public bool IsDefaultSelectedDirty;
        public bool DefaultSelected;

        public string? ReturnValue;
    }

    // -------- Value State --------

    /// <summary>
    /// Attempts to retrieve the dirty IDL value for <paramref name="element"/>.
    /// Returns <see langword="false"/> if no dirty value flag has been set.
    /// </summary>
    public bool TryGetDirtyValue(DomElement element, out string? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (_states.TryGetValue(element, out var state) && state.IsValueDirty)
        {
            value = state.Value;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Sets the dirty IDL value for <paramref name="element"/>, marking the dirty value flag.
    /// </summary>
    public void SetDirtyValue(DomElement element, string? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        var state = _states.GetOrCreateValue(element);
        state.Value = value;
        state.IsValueDirty = true;
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Clears the dirty value flag for <paramref name="element"/>, reverting its effective value to default markup.
    /// </summary>
    public void ClearDirtyValue(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (_states.TryGetValue(element, out var state) && state.IsValueDirty)
        {
            state.Value = null;
            state.IsValueDirty = false;
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Returns whether the dirty value flag is set for <paramref name="element"/>.
    /// </summary>
    public bool IsValueDirty(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return _states.TryGetValue(element, out var state) && state.IsValueDirty;
    }

    /// <summary>
    /// Resolves the current effective value of <paramref name="element"/>: the dirty value when set,
    /// or the default value according to its element type and markup content.
    /// </summary>
    public string GetEffectiveValue(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (TryGetDirtyValue(element, out var dirty))
            return dirty ?? string.Empty;

        var tag = element.TagName.ToLowerInvariant();
        if (tag == "textarea")
            return HtmlFormQueries.GetDefaultValue(element);

        if (tag == "select")
            return HtmlSelectQueries.ResolveSelectValue(element, GetDirtySelectedIndexOrNull(element));

        if (tag == "option")
            return HtmlSelectQueries.GetOptionValue(element);

        return element.GetAttributeByQualifiedName("value") ?? string.Empty;
    }

    // -------- Checked State --------

    /// <summary>
    /// Attempts to retrieve the dirty IDL checkedness for <paramref name="element"/>.
    /// Returns <see langword="false"/> if no dirty checkedness flag has been set.
    /// </summary>
    public bool TryGetDirtyChecked(DomElement element, out bool isChecked)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (_states.TryGetValue(element, out var state) && state.IsCheckedDirty)
        {
            isChecked = state.Checked;
            return true;
        }

        isChecked = false;
        return false;
    }

    /// <summary>
    /// Sets the dirty IDL checkedness for <paramref name="element"/>. If <paramref name="isChecked"/>
    /// is <see langword="true"/> and the element is an <c>&lt;input type="radio"&gt;</c>, automatically
    /// unchecks all other radio buttons in the same radio group (HTML §4.10.5.1.16).
    /// </summary>
    public void SetDirtyChecked(DomElement element, bool isChecked)
    {
        ArgumentNullException.ThrowIfNull(element);
        var state = _states.GetOrCreateValue(element);
        state.Checked = isChecked;
        state.IsCheckedDirty = true;

        if (isChecked && HtmlFormQueries.IsRadioInput(element))
        {
            var group = HtmlFormQueries.GetRadioGroupElements(element);
            foreach (var sibling in group)
            {
                if (!ReferenceEquals(sibling, element))
                {
                    var sibState = _states.GetOrCreateValue(sibling);
                    sibState.Checked = false;
                    sibState.IsCheckedDirty = true;
                }
            }
        }

        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Clears the dirty checkedness flag for <paramref name="element"/>, reverting to its <c>checked</c> content attribute.
    /// </summary>
    public void ClearDirtyChecked(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (_states.TryGetValue(element, out var state) && state.IsCheckedDirty)
        {
            state.Checked = false;
            state.IsCheckedDirty = false;
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Returns whether the dirty checkedness flag is set for <paramref name="element"/>.
    /// </summary>
    public bool IsCheckedDirty(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return _states.TryGetValue(element, out var state) && state.IsCheckedDirty;
    }

    /// <summary>
    /// Resolves the current effective checkedness of <paramref name="element"/>: the dirty checkedness
    /// when set, or the presence of the <c>checked</c> content attribute.
    /// </summary>
    public bool GetEffectiveChecked(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return TryGetDirtyChecked(element, out var dirty)
            ? dirty
            : HtmlFormQueries.GetDefaultChecked(element);
    }

    // -------- Select & Option State --------

    /// <summary>
    /// Attempts to retrieve the dirty selected index for a <c>&lt;select&gt;</c> element.
    /// </summary>
    public bool TryGetDirtySelectedIndex(DomElement select, out int index)
    {
        ArgumentNullException.ThrowIfNull(select);
        if (_states.TryGetValue(select, out var state) && state.IsSelectedIndexDirty)
        {
            index = state.SelectedIndex;
            return true;
        }

        index = 0;
        return false;
    }

    /// <summary>
    /// Retrieves the dirty selected index for <paramref name="select"/>, or <see langword="null"/> if unset.
    /// </summary>
    public int? GetDirtySelectedIndexOrNull(DomElement select) =>
        TryGetDirtySelectedIndex(select, out var idx) ? idx : null;

    /// <summary>
    /// Sets the dirty selected index for <paramref name="select"/>.
    /// </summary>
    public void SetDirtySelectedIndex(DomElement select, int index)
    {
        ArgumentNullException.ThrowIfNull(select);
        var state = _states.GetOrCreateValue(select);
        state.SelectedIndex = index;
        state.IsSelectedIndexDirty = true;
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Clears the dirty selected index for <paramref name="select"/>.
    /// </summary>
    public void ClearDirtySelectedIndex(DomElement select)
    {
        ArgumentNullException.ThrowIfNull(select);
        if (_states.TryGetValue(select, out var state) && state.IsSelectedIndexDirty)
        {
            state.SelectedIndex = 0;
            state.IsSelectedIndexDirty = false;
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Attempts to retrieve the dirty selectedness state for an <c>&lt;option&gt;</c> element.
    /// </summary>
    public bool TryGetDirtyOptionSelected(DomElement option, out bool selected)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (_states.TryGetValue(option, out var state) && state.IsDefaultSelectedDirty)
        {
            selected = state.DefaultSelected;
            return true;
        }

        selected = false;
        return false;
    }

    /// <summary>
    /// Sets the dirty selectedness state for an <c>&lt;option&gt;</c> element.
    /// </summary>
    public void SetDirtyOptionSelected(DomElement option, bool selected)
    {
        ArgumentNullException.ThrowIfNull(option);
        var state = _states.GetOrCreateValue(option);
        state.DefaultSelected = selected;
        state.IsDefaultSelectedDirty = true;
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Clears the dirty selectedness state for an <c>&lt;option&gt;</c> element.
    /// </summary>
    public void ClearDirtyOptionSelected(DomElement option)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (_states.TryGetValue(option, out var state) && state.IsDefaultSelectedDirty)
        {
            state.DefaultSelected = false;
            state.IsDefaultSelectedDirty = false;
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Resolves the effective selectedness of <paramref name="option"/>: dirty selectedness when set,
    /// or the presence of the <c>selected</c> content attribute.
    /// </summary>
    public bool GetEffectiveOptionSelected(DomElement option)
    {
        ArgumentNullException.ThrowIfNull(option);
        return TryGetDirtyOptionSelected(option, out var selected)
            ? selected
            : option.HasAttributeByQualifiedName("selected");
    }

    // -------- Dialog ReturnValue State --------

    /// <summary>
    /// Attempts to retrieve the <c>returnValue</c> state for a <c>&lt;dialog&gt;</c> element.
    /// </summary>
    public bool TryGetReturnValue(DomElement element, out string? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (_states.TryGetValue(element, out var state) && state.ReturnValue is not null)
        {
            value = state.ReturnValue;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Sets the <c>returnValue</c> state for a <c>&lt;dialog&gt;</c> element.
    /// </summary>
    public void SetReturnValue(DomElement element, string? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        var state = _states.GetOrCreateValue(element);
        state.ReturnValue = value;
        OnStateChanged?.Invoke();
    }

    // -------- Cloning & Reset --------

    /// <summary>
    /// Copies all dirty form control states from <paramref name="source"/> to <paramref name="target"/>.
    /// </summary>
    public void CopyControlState(DomElement source, DomElement target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (_states.TryGetValue(source, out var srcState))
        {
            var targetState = _states.GetOrCreateValue(target);
            targetState.IsValueDirty = srcState.IsValueDirty;
            targetState.Value = srcState.Value;
            targetState.IsCheckedDirty = srcState.IsCheckedDirty;
            targetState.Checked = srcState.Checked;
            targetState.IsSelectedIndexDirty = srcState.IsSelectedIndexDirty;
            targetState.SelectedIndex = srcState.SelectedIndex;
            targetState.IsDefaultSelectedDirty = srcState.IsDefaultSelectedDirty;
            targetState.DefaultSelected = srcState.DefaultSelected;
            targetState.ReturnValue = srcState.ReturnValue;
        }
    }

    /// <summary>
    /// Resets the dirty state of <paramref name="control"/> to default values per HTML §4.10.21.
    /// </summary>
    public void ResetControl(DomElement control)
    {
        ArgumentNullException.ThrowIfNull(control);

        if (_states.TryGetValue(control, out var state))
        {
            state.IsValueDirty = false;
            state.Value = null;
            state.IsCheckedDirty = false;
            state.Checked = false;
            state.IsSelectedIndexDirty = false;
            state.SelectedIndex = 0;
            state.IsDefaultSelectedDirty = false;
            state.DefaultSelected = false;
        }

        if (string.Equals(control.TagName, "select", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var option in control.InclusiveDescendants().OfType<DomElement>())
            {
                if (string.Equals(option.TagName, "option", StringComparison.OrdinalIgnoreCase))
                    ClearDirtyOptionSelected(option);
            }
        }

        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Executes the form reset algorithm on <paramref name="form"/> (HTML §4.10.21):
    /// resets all listed controls belonging to the form and enforces radio group exclusivity.
    /// </summary>
    public void ResetForm(DomElement form, Action<DomElement>? onReset = null)
    {
        ArgumentNullException.ThrowIfNull(form);
        var controls = HtmlFormQueries.GetFormElements(form);
        foreach (var control in controls)
        {
            ResetControl(control);
            onReset?.Invoke(control);
        }

        EnforceRadioGroupExclusivity(form);
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Enforces the invariant that at most one radio button in any radio group is checked within
    /// <paramref name="scope"/>, keeping the last checked member in tree order (HTML §4.10.5.1.16).
    /// </summary>
    public void EnforceRadioGroupExclusivity(DomElement scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        Dictionary<(DomElement? FormOwner, string Name), DomElement>? lastChecked = null;

        foreach (var element in scope.InclusiveDescendants().OfType<DomElement>())
        {
            if (!HtmlFormQueries.IsRadioInput(element))
                continue;

            var name = element.GetAttributeByQualifiedName("name");
            if (string.IsNullOrEmpty(name))
                continue;

            if (!GetEffectiveChecked(element))
                continue;

            lastChecked ??= [];
            var formOwner = HtmlFormQueries.GetFormOwner(element);
            var key = (FormOwner: formOwner, Name: name);

            if (lastChecked.TryGetValue(key, out var previous))
            {
                var prevState = _states.GetOrCreateValue(previous);
                prevState.Checked = false;
                prevState.IsCheckedDirty = true;
            }

            lastChecked[key] = element;
        }
    }
}
