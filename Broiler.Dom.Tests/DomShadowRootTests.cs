using System;
using Xunit;

namespace Broiler.Dom.Tests;

public class DomShadowRootTests
{
    [Fact]
    public void AttachShadow_Creates_ShadowRoot_And_Sets_Host_And_Mode()
    {
        var doc = new DomDocument();
        var host = doc.CreateElement("div");
        doc.AppendChild(host);

        var shadow = host.AttachShadow(DomShadowRootMode.Open, delegatesFocus: true, DomSlotAssignmentMode.Manual);

        Assert.NotNull(shadow);
        Assert.Same(host, shadow.Host);
        Assert.Equal(DomShadowRootMode.Open, shadow.Mode);
        Assert.True(shadow.DelegatesFocus);
        Assert.Equal(DomSlotAssignmentMode.Manual, shadow.SlotAssignment);
        Assert.Equal(DomNodeType.DocumentFragment, shadow.NodeType);
        Assert.Null(shadow.ParentNode);
    }

    [Fact]
    public void AttachShadow_Throws_NotSupported_On_Invalid_Tag()
    {
        var doc = new DomDocument();
        var host = doc.CreateElement("input"); // input is not an allowed shadow host

        var ex = Assert.Throws<DomException>(() => host.AttachShadow(DomShadowRootMode.Open));
        Assert.Equal("NotSupportedError", ex.Name);
    }

    [Fact]
    public void AttachShadow_Throws_NotSupported_When_Already_Attached()
    {
        var doc = new DomDocument();
        var host = doc.CreateElement("div");
        host.AttachShadow(DomShadowRootMode.Open);

        var ex = Assert.Throws<DomException>(() => host.AttachShadow(DomShadowRootMode.Open));
        Assert.Equal("NotSupportedError", ex.Name);
    }

    [Fact]
    public void AttachShadow_ValidCustomElementName_Allowed()
    {
        var doc = new DomDocument();
        var host = doc.CreateElement("my-element");
        var shadow = host.AttachShadow(DomShadowRootMode.Open);

        Assert.NotNull(shadow);
        Assert.Same(shadow, host.ShadowRoot);
    }

    [Fact]
    public void AttachShadow_ReservedCustomElementName_Throws()
    {
        var doc = new DomDocument();
        var host = doc.CreateElement("font-face");

        var ex = Assert.Throws<DomException>(() => host.AttachShadow(DomShadowRootMode.Open));
        Assert.Equal("NotSupportedError", ex.Name);
    }

    [Fact]
    public void AttachShadow_Open_Mode_Is_Visible_Via_ShadowRoot()
    {
        var doc = new DomDocument();
        var host = doc.CreateElement("span");
        var shadow = host.AttachShadow(DomShadowRootMode.Open);

        Assert.Same(shadow, host.ShadowRoot);
        Assert.Same(shadow, host.InternalShadowRoot);
    }

    [Fact]
    public void AttachShadow_Closed_Mode_Returns_Null_Via_ShadowRoot_But_Present_In_Internal()
    {
        var doc = new DomDocument();
        var host = doc.CreateElement("span");
        var shadow = host.AttachShadow(DomShadowRootMode.Closed);

        Assert.Null(host.ShadowRoot);
        Assert.Same(shadow, host.InternalShadowRoot);
    }

    [Fact]
    public void ShadowRoot_IsNotPartOfHostChildren_And_ParentNodeIsNull()
    {
        var doc = new DomDocument();
        var host = doc.CreateElement("div");
        doc.AppendChild(host);

        var shadow = host.AttachShadow(DomShadowRootMode.Open);
        var child = doc.CreateElement("p");
        shadow.AppendChild(child);

        Assert.Empty(host.ChildNodes);
        Assert.Equal(0, host.ChildElementCount);
        Assert.False(host.Contains(shadow));
        Assert.False(host.Contains(child));
        Assert.False(doc.Contains(shadow));
        Assert.Null(shadow.ParentNode);
        Assert.Null(shadow.ParentElement);
    }

    [Fact]
    public void GetRootNode_Composed_TraversesThroughHostToDocument()
    {
        var doc = new DomDocument();
        var host = doc.CreateElement("div");
        doc.AppendChild(host);

        var shadow = host.AttachShadow(DomShadowRootMode.Open);
        var inner = doc.CreateElement("span");
        shadow.AppendChild(inner);

        Assert.Same(shadow, inner.GetRootNode(composed: false));
        Assert.Same(shadow, shadow.GetRootNode(composed: false));
        Assert.Same(doc, inner.GetRootNode(composed: true));
        Assert.Same(doc, shadow.GetRootNode(composed: true));
        Assert.True(inner.IsConnected);
        Assert.True(shadow.IsConnected);
    }

    [Fact]
    public void DisconnectedHost_ShadowChildren_AreNotConnected()
    {
        var doc = new DomDocument();
        var host = doc.CreateElement("div"); // not in doc

        var shadow = host.AttachShadow(DomShadowRootMode.Open);
        var inner = doc.CreateElement("span");
        shadow.AppendChild(inner);

        Assert.Same(host, inner.GetRootNode(composed: true));
        Assert.False(inner.IsConnected);
        Assert.False(shadow.IsConnected);
    }

    [Fact]
    public void CloneShallow_ThrowsNotSupportedError()
    {
        var doc = new DomDocument();
        var host = doc.CreateElement("div");
        var shadow = host.AttachShadow(DomShadowRootMode.Open);

        var ex = Assert.Throws<DomException>(() => shadow.CloneShallow(doc));
        Assert.Equal("NotSupportedError", ex.Name);
    }

    [Fact]
    public void Slotting_AssignsNodesToSlotsCorrectly()
    {
        var doc = new DomDocument();
        var host = doc.CreateElement("div");
        doc.AppendChild(host);

        var shadow = host.AttachShadow(DomShadowRootMode.Open);
        var namedSlot = doc.CreateElement("slot");
        namedSlot.SetAttribute("name", "header");
        var defaultSlot = doc.CreateElement("slot");
        shadow.AppendChild(namedSlot);
        shadow.AppendChild(defaultSlot);

        var headerChild = doc.CreateElement("h1");
        headerChild.SetAttribute("slot", "header");
        var bodyChild = doc.CreateElement("p");
        host.AppendChild(headerChild);
        host.AppendChild(bodyChild);

        Assert.Same(namedSlot, DomSlotting.FindAssignedSlot(headerChild));
        Assert.Same(defaultSlot, DomSlotting.FindAssignedSlot(bodyChild));

        var headerAssigned = DomSlotting.GetAssignedNodes(namedSlot);
        Assert.Single(headerAssigned);
        Assert.Same(headerChild, headerAssigned[0]);

        var defaultAssigned = DomSlotting.GetAssignedNodes(defaultSlot);
        Assert.Single(defaultAssigned);
        Assert.Same(bodyChild, defaultAssigned[0]);
    }
}
