namespace Broiler.Dom;

public sealed class DomDocumentFragment : DomNode
{
    internal DomDocumentFragment(DomDocument ownerDocument)
        : base(DomNodeType.DocumentFragment, ownerDocument)
    {
    }

    internal override DomNode CloneShallow(DomDocument ownerDocument) =>
        new DomDocumentFragment(ownerDocument);
}
