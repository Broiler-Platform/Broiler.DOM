namespace Broiler.Dom;

public sealed class DomDocumentType : DomNode
{
    internal DomDocumentType(
        DomDocument ownerDocument,
        string name,
        string publicId,
        string systemId)
        : base(DomNodeType.DocumentType, ownerDocument)
    {
        Name = name;
        PublicId = publicId;
        SystemId = systemId;
    }

    public string Name { get; }

    public string PublicId { get; }

    public string SystemId { get; }

    internal override DomNode CloneShallow(DomDocument ownerDocument) =>
        new DomDocumentType(ownerDocument, Name, PublicId, SystemId);
}
