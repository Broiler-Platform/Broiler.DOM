namespace Broiler.Dom;

public sealed class DomException : InvalidOperationException
{
    public DomException(string name, string message)
        : base(message)
    {
        Name = name;
    }

    public string Name { get; }

    internal static DomException HierarchyRequest(string message) =>
        new("HierarchyRequestError", message);

    internal static DomException NotFound(string message) =>
        new("NotFoundError", message);

    internal static DomException Namespace(string message) =>
        new("NamespaceError", message);

    internal static DomException InvalidState(string message) =>
        new("InvalidStateError", message);

    internal static DomException WrongDocument(string message) =>
        new("WrongDocumentError", message);

    internal static DomException IndexSize(string message) =>
        new("IndexSizeError", message);
}
