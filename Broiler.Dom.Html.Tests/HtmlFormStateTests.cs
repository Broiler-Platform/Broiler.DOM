using System.Collections.Generic;
using Broiler.Dom;
using Xunit;

namespace Broiler.Dom.Html.Tests;

public sealed class HtmlFormStateTests
{
    [Fact(Timeout = 600000)]
    public void DirtyValue_Lifecycle_And_EffectiveValue()
    {
        var state = new HtmlFormState();
        var doc = new DomDocument();
        var input = doc.CreateElement("input");
        input.SetAttribute("value", "default-val");

        // Initially not dirty, effective is default
        Assert.False(state.IsValueDirty(input));
        Assert.False(state.TryGetDirtyValue(input, out _));
        Assert.Equal("default-val", state.GetEffectiveValue(input));

        // Set dirty value
        var changedNotified = false;
        state.OnStateChanged = () => changedNotified = true;

        state.SetDirtyValue(input, "edited-val");
        Assert.True(changedNotified);
        Assert.True(state.IsValueDirty(input));
        Assert.True(state.TryGetDirtyValue(input, out var val));
        Assert.Equal("edited-val", val);
        Assert.Equal("edited-val", state.GetEffectiveValue(input));

        // Clear dirty value reverts to markup
        changedNotified = false;
        state.ClearDirtyValue(input);
        Assert.True(changedNotified);
        Assert.False(state.IsValueDirty(input));
        Assert.Equal("default-val", state.GetEffectiveValue(input));
    }

    [Fact(Timeout = 600000)]
    public void DirtyValue_Textarea_Reflects_Child_Text_When_Not_Dirty()
    {
        var state = new HtmlFormState();
        var doc = new DomDocument();
        var textarea = doc.CreateElement("textarea");
        textarea.AppendChild(doc.CreateTextNode("default content"));

        Assert.Equal("default content", state.GetEffectiveValue(textarea));

        state.SetDirtyValue(textarea, "user edited text");
        Assert.Equal("user edited text", state.GetEffectiveValue(textarea));

        state.ClearDirtyValue(textarea);
        Assert.Equal("default content", state.GetEffectiveValue(textarea));
    }

    [Fact(Timeout = 600000)]
    public void DirtyChecked_Lifecycle_And_EffectiveChecked()
    {
        var state = new HtmlFormState();
        var doc = new DomDocument();
        var checkbox = doc.CreateElement("input");
        checkbox.SetAttribute("type", "checkbox");
        checkbox.SetAttribute("checked", "");

        Assert.False(state.IsCheckedDirty(checkbox));
        Assert.True(state.GetEffectiveChecked(checkbox));

        // Set to false
        state.SetDirtyChecked(checkbox, false);
        Assert.True(state.IsCheckedDirty(checkbox));
        Assert.False(state.GetEffectiveChecked(checkbox));

        // Clear dirty checked
        state.ClearDirtyChecked(checkbox);
        Assert.False(state.IsCheckedDirty(checkbox));
        Assert.True(state.GetEffectiveChecked(checkbox));
    }

    [Fact(Timeout = 600000)]
    public void RadioGroup_Mutual_Exclusion_When_Setting_DirtyChecked()
    {
        var state = new HtmlFormState();
        var doc = new DomDocument();
        var form = doc.CreateElement("form");

        var r1 = doc.CreateElement("input");
        r1.SetAttribute("type", "radio");
        r1.SetAttribute("name", "choice");
        r1.SetAttribute("checked", "");

        var r2 = doc.CreateElement("input");
        r2.SetAttribute("type", "radio");
        r2.SetAttribute("name", "choice");

        var r3 = doc.CreateElement("input");
        r3.SetAttribute("type", "radio");
        r3.SetAttribute("name", "choice");

        form.AppendChild(r1);
        form.AppendChild(r2);
        form.AppendChild(r3);
        doc.AppendChild(form);

        Assert.True(state.GetEffectiveChecked(r1));
        Assert.False(state.GetEffectiveChecked(r2));
        Assert.False(state.GetEffectiveChecked(r3));

        // Select r2 -> r1 becomes unchecked and dirty
        state.SetDirtyChecked(r2, true);

        Assert.False(state.GetEffectiveChecked(r1));
        Assert.True(state.IsCheckedDirty(r1));
        Assert.True(state.GetEffectiveChecked(r2));
        Assert.False(state.GetEffectiveChecked(r3));

        // Select r3 -> r2 becomes unchecked and dirty
        state.SetDirtyChecked(r3, true);

        Assert.False(state.GetEffectiveChecked(r1));
        Assert.False(state.GetEffectiveChecked(r2));
        Assert.True(state.GetEffectiveChecked(r3));
    }

    [Fact(Timeout = 600000)]
    public void Select_And_Option_State_Management()
    {
        var state = new HtmlFormState();
        var doc = new DomDocument();
        var select = doc.CreateElement("select");

        Assert.False(state.TryGetDirtySelectedIndex(select, out _));
        Assert.Null(state.GetDirtySelectedIndexOrNull(select));

        state.SetDirtySelectedIndex(select, 2);
        Assert.True(state.TryGetDirtySelectedIndex(select, out var idx));
        Assert.Equal(2, idx);
        Assert.Equal(2, state.GetDirtySelectedIndexOrNull(select));

        state.ClearDirtySelectedIndex(select);
        Assert.False(state.TryGetDirtySelectedIndex(select, out _));

        // Option selectedness
        var option = doc.CreateElement("option");
        option.SetAttribute("selected", "");

        Assert.False(state.TryGetDirtyOptionSelected(option, out _));
        Assert.True(state.GetEffectiveOptionSelected(option));

        state.SetDirtyOptionSelected(option, false);
        Assert.True(state.TryGetDirtyOptionSelected(option, out var optSel));
        Assert.False(optSel);
        Assert.False(state.GetEffectiveOptionSelected(option));

        state.ClearDirtyOptionSelected(option);
        Assert.True(state.GetEffectiveOptionSelected(option));
    }

    [Fact(Timeout = 600000)]
    public void Dialog_ReturnValue_State()
    {
        var state = new HtmlFormState();
        var doc = new DomDocument();
        var dialog = doc.CreateElement("dialog");

        Assert.False(state.TryGetReturnValue(dialog, out _));

        state.SetReturnValue(dialog, "confirmed");
        Assert.True(state.TryGetReturnValue(dialog, out var rv));
        Assert.Equal("confirmed", rv);
    }

    [Fact(Timeout = 600000)]
    public void CopyControlState_Transfers_All_Flags()
    {
        var state = new HtmlFormState();
        var doc = new DomDocument();
        var src = doc.CreateElement("input");
        var dst = doc.CreateElement("input");

        state.SetDirtyValue(src, "transferred-value");
        state.SetDirtyChecked(src, true);

        state.CopyControlState(src, dst);

        Assert.True(state.IsValueDirty(dst));
        Assert.Equal("transferred-value", state.GetEffectiveValue(dst));
        Assert.True(state.IsCheckedDirty(dst));
        Assert.True(state.GetEffectiveChecked(dst));
    }

    [Fact(Timeout = 600000)]
    public void ResetControl_And_ResetForm_Restore_Defaults()
    {
        var state = new HtmlFormState();
        var doc = new DomDocument();
        var form = doc.CreateElement("form");

        var input = doc.CreateElement("input");
        input.SetAttribute("value", "initial");
        state.SetDirtyValue(input, "dirty-input");

        var select = doc.CreateElement("select");
        var opt = doc.CreateElement("option");
        opt.SetAttribute("value", "a");
        opt.SetAttribute("selected", "");
        select.AppendChild(opt);
        state.SetDirtySelectedIndex(select, 5);
        state.SetDirtyOptionSelected(opt, false);

        form.AppendChild(input);
        form.AppendChild(select);
        doc.AppendChild(form);

        var resetList = new List<DomElement>();
        state.ResetForm(form, el => resetList.Add(el));

        Assert.Equal(2, resetList.Count);
        Assert.False(state.IsValueDirty(input));
        Assert.Equal("initial", state.GetEffectiveValue(input));
        Assert.False(state.TryGetDirtySelectedIndex(select, out _));
        Assert.True(state.GetEffectiveOptionSelected(opt));
    }

    [Fact(Timeout = 600000)]
    public void EnforceRadioGroupExclusivity_Keeps_Last_Checked_In_Tree_Order()
    {
        var state = new HtmlFormState();
        var doc = new DomDocument();
        var form = doc.CreateElement("form");

        var r1 = doc.CreateElement("input");
        r1.SetAttribute("type", "radio");
        r1.SetAttribute("name", "g");
        r1.SetAttribute("checked", "");

        var r2 = doc.CreateElement("input");
        r2.SetAttribute("type", "radio");
        r2.SetAttribute("name", "g");
        r2.SetAttribute("checked", "");

        var r3 = doc.CreateElement("input");
        r3.SetAttribute("type", "radio");
        r3.SetAttribute("name", "g");
        r3.SetAttribute("checked", "");

        form.AppendChild(r1);
        form.AppendChild(r2);
        form.AppendChild(r3);
        doc.AppendChild(form);

        // Before enforcement, all 3 have checked attribute
        Assert.True(state.GetEffectiveChecked(r1));
        Assert.True(state.GetEffectiveChecked(r2));
        Assert.True(state.GetEffectiveChecked(r3));

        state.EnforceRadioGroupExclusivity(form);

        // Last checked (r3) stays checked; r1 and r2 are unchecked
        Assert.False(state.GetEffectiveChecked(r1));
        Assert.False(state.GetEffectiveChecked(r2));
        Assert.True(state.GetEffectiveChecked(r3));
    }
}
