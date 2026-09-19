using System.Collections.Generic;
using Broiler.Dom;
using Xunit;

namespace Broiler.Dom.Html.Tests;

public sealed class HtmlFormQueriesTests
{
    [Theory(Timeout = 600000)]
    [InlineData("button", true)]
    [InlineData("fieldset", true)]
    [InlineData("input", true)]
    [InlineData("object", true)]
    [InlineData("output", true)]
    [InlineData("select", true)]
    [InlineData("textarea", true)]
    [InlineData("BUTTON", true)]
    [InlineData("Input", true)]
    [InlineData("div", false)]
    [InlineData("form", false)]
    [InlineData("span", false)]
    [InlineData("p", false)]
    [InlineData("a", false)]
    public void IsListedFormControl_Identifies_Spec_Tags(string tagName, bool expected)
    {
        var doc = new DomDocument();
        var el = doc.CreateElement(tagName);
        Assert.Equal(expected, HtmlFormQueries.IsListedFormControl(el));
    }

    [Fact(Timeout = 600000)]
    public void IsRadioInput_And_IsCheckboxInput_Identify_Types()
    {
        var doc = new DomDocument();
        var radio = doc.CreateElement("input");
        radio.SetAttribute("type", "radio");

        var checkbox = doc.CreateElement("input");
        checkbox.SetAttribute("type", "checkbox");

        var text = doc.CreateElement("input");
        text.SetAttribute("type", "text");

        var div = doc.CreateElement("div");
        div.SetAttribute("type", "radio");

        Assert.True(HtmlFormQueries.IsRadioInput(radio));
        Assert.False(HtmlFormQueries.IsCheckboxInput(radio));

        Assert.True(HtmlFormQueries.IsCheckboxInput(checkbox));
        Assert.False(HtmlFormQueries.IsRadioInput(checkbox));

        Assert.False(HtmlFormQueries.IsRadioInput(text));
        Assert.False(HtmlFormQueries.IsCheckboxInput(text));

        Assert.False(HtmlFormQueries.IsRadioInput(div));
    }

    [Fact(Timeout = 600000)]
    public void GetFormOwner_Resolves_Ancestor_Form()
    {
        var doc = new DomDocument();
        var root = doc.CreateElement("div");
        doc.AppendChild(root);

        var form = doc.CreateElement("form");
        var div = doc.CreateElement("div");
        var input = doc.CreateElement("input");

        div.AppendChild(input);
        form.AppendChild(div);
        root.AppendChild(form);

        Assert.Same(form, HtmlFormQueries.GetFormOwner(input));
    }

    [Fact(Timeout = 600000)]
    public void GetFormOwner_Resolves_Form_Attribute_In_Document_And_Fragment()
    {
        var doc = new DomDocument();
        var root = doc.CreateElement("div");
        doc.AppendChild(root);

        var form1 = doc.CreateElement("form");
        form1.Id = "f1";
        var form2 = doc.CreateElement("form");
        form2.Id = "f2";
        var notAForm = doc.CreateElement("div");
        notAForm.Id = "not-form";

        // input inside form1, but form attribute points to f2
        var input1 = doc.CreateElement("input");
        input1.SetAttribute("form", "f2");
        form1.AppendChild(input1);

        // input referencing nonexistent form
        var input2 = doc.CreateElement("input");
        input2.SetAttribute("form", "missing");

        // input referencing non-form element
        var input3 = doc.CreateElement("input");
        input3.SetAttribute("form", "not-form");

        root.AppendChild(form1);
        root.AppendChild(form2);
        root.AppendChild(notAForm);
        root.AppendChild(input2);
        root.AppendChild(input3);

        Assert.Same(form2, HtmlFormQueries.GetFormOwner(input1));
        Assert.Null(HtmlFormQueries.GetFormOwner(input2));
        Assert.Null(HtmlFormQueries.GetFormOwner(input3));

        // Fragment (non-document root)
        var fragment = doc.CreateDocumentFragment();
        var fragForm = doc.CreateElement("form");
        fragForm.Id = "frag-f";
        var fragInput = doc.CreateElement("input");
        fragInput.SetAttribute("form", "frag-f");
        fragment.AppendChild(fragForm);
        fragment.AppendChild(fragInput);

        Assert.Same(fragForm, HtmlFormQueries.GetFormOwner(fragInput));
    }

    [Fact(Timeout = 600000)]
    public void GetFormElements_Collects_Form_Elements_In_Tree_Order()
    {
        var doc = new DomDocument();
        var root = doc.CreateElement("div");
        doc.AppendChild(root);

        var externalInputBefore = doc.CreateElement("input");
        externalInputBefore.SetAttribute("form", "my-form");

        var form = doc.CreateElement("form");
        form.Id = "my-form";

        var childInput = doc.CreateElement("input");
        var childSelect = doc.CreateElement("select");
        var nonListedSpan = doc.CreateElement("span");
        var otherFormInput = doc.CreateElement("input");
        otherFormInput.SetAttribute("form", "other-form");

        form.AppendChild(childInput);
        form.AppendChild(nonListedSpan);
        form.AppendChild(childSelect);
        form.AppendChild(otherFormInput);

        var externalInputAfter = doc.CreateElement("textarea");
        externalInputAfter.SetAttribute("form", "my-form");

        root.AppendChild(externalInputBefore);
        root.AppendChild(form);
        root.AppendChild(externalInputAfter);

        var elements = HtmlFormQueries.GetFormElements(form);
        Assert.Equal(4, elements.Count);
        Assert.Same(externalInputBefore, elements[0]);
        Assert.Same(childInput, elements[1]);
        Assert.Same(childSelect, elements[2]);
        Assert.Same(externalInputAfter, elements[3]);
    }

    [Fact(Timeout = 600000)]
    public void GetRadioGroupElements_Scopes_By_Form_Owner_And_Name()
    {
        var doc = new DomDocument();
        var root = doc.CreateElement("div");
        doc.AppendChild(root);

        var form1 = doc.CreateElement("form");
        var r1_f1 = doc.CreateElement("input");
        r1_f1.SetAttribute("type", "radio");
        r1_f1.SetAttribute("name", "gender");

        var r2_f1 = doc.CreateElement("input");
        r2_f1.SetAttribute("type", "radio");
        r2_f1.SetAttribute("name", "gender");

        var diffName = doc.CreateElement("input");
        diffName.SetAttribute("type", "radio");
        diffName.SetAttribute("name", "color");

        form1.AppendChild(r1_f1);
        form1.AppendChild(r2_f1);
        form1.AppendChild(diffName);

        var form2 = doc.CreateElement("form");
        var r1_f2 = doc.CreateElement("input");
        r1_f2.SetAttribute("type", "radio");
        r1_f2.SetAttribute("name", "gender");
        form2.AppendChild(r1_f2);

        root.AppendChild(form1);
        root.AppendChild(form2);

        var groupF1 = HtmlFormQueries.GetRadioGroupElements(r1_f1);
        Assert.Equal(2, groupF1.Count);
        Assert.Same(r1_f1, groupF1[0]);
        Assert.Same(r2_f1, groupF1[1]);

        var groupF2 = HtmlFormQueries.GetRadioGroupElements(r1_f2);
        Assert.Single(groupF2);
        Assert.Same(r1_f2, groupF2[0]);

        // Unnamed radio
        var unnamed = doc.CreateElement("input");
        unnamed.SetAttribute("type", "radio");
        form1.AppendChild(unnamed);
        Assert.Single(HtmlFormQueries.GetRadioGroupElements(unnamed));
    }

    [Fact(Timeout = 600000)]
    public void IsFormControlDisabled_Checks_Control_And_Ancestor_Fieldset_And_Legend()
    {
        var doc = new DomDocument();
        var root = doc.CreateElement("div");
        doc.AppendChild(root);

        var fieldset = doc.CreateElement("fieldset");
        fieldset.SetAttribute("disabled", "");

        var firstLegend = doc.CreateElement("legend");
        var inputInFirstLegend = doc.CreateElement("input");
        firstLegend.AppendChild(inputInFirstLegend);

        var secondLegend = doc.CreateElement("legend");
        var inputInSecondLegend = doc.CreateElement("input");
        secondLegend.AppendChild(inputInSecondLegend);

        var normalInputInFieldset = doc.CreateElement("input");

        var selfDisabledInput = doc.CreateElement("input");
        selfDisabledInput.SetAttribute("disabled", "");

        fieldset.AppendChild(firstLegend);
        fieldset.AppendChild(secondLegend);
        fieldset.AppendChild(normalInputInFieldset);
        root.AppendChild(fieldset);
        root.AppendChild(selfDisabledInput);

        Assert.True(HtmlFormQueries.IsFormControlDisabled(selfDisabledInput));
        Assert.True(HtmlFormQueries.IsFormControlDisabled(normalInputInFieldset));
        Assert.False(HtmlFormQueries.IsFormControlDisabled(inputInFirstLegend));
        Assert.True(HtmlFormQueries.IsFormControlDisabled(inputInSecondLegend));
    }

    [Fact(Timeout = 600000)]
    public void GetDefaultValue_And_GetDefaultChecked_Reflect_Markup()
    {
        var doc = new DomDocument();
        var input = doc.CreateElement("input");
        input.SetAttribute("value", "initial");
        input.SetAttribute("checked", "");

        Assert.Equal("initial", HtmlFormQueries.GetDefaultValue(input));
        Assert.True(HtmlFormQueries.GetDefaultChecked(input));

        var inputEmpty = doc.CreateElement("input");
        Assert.Equal(string.Empty, HtmlFormQueries.GetDefaultValue(inputEmpty));
        Assert.False(HtmlFormQueries.GetDefaultChecked(inputEmpty));

        var textarea = doc.CreateElement("textarea");
        textarea.AppendChild(doc.CreateTextNode("line 1\nline 2"));
        Assert.Equal("line 1\nline 2", HtmlFormQueries.GetDefaultValue(textarea));
    }
}
