using System;

namespace Broiler.Dom;

public sealed class DomException : InvalidOperationException
{
    public DomException(string message)
        : this("Error", message)
    {
    }

    private DomException(string name, string message)
        : base(message)
        => Name = name;

    /// <summary>
    /// The DOM error name (e.g. <c>"HierarchyRequestError"</c>), per the
    /// <c>DOMException</c> interface. Defaults to <c>"Error"</c> when unspecified.
    /// </summary>
    public string Name { get; }

    internal static DomException HierarchyRequest(string message) => new("HierarchyRequestError", message);

    internal static DomException NotFound(string message) => new("NotFoundError", message);

    internal static DomException Namespace(string message) => new("NamespaceError", message);

    internal static DomException WrongDocument(string message) => new("WrongDocumentError", message);
}
