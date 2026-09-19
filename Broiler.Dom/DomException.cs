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

    public static DomException InvalidCharacter(string message) => new("InvalidCharacterError", message);

    public static DomException HierarchyRequest(string message) => new("HierarchyRequestError", message);

    public static DomException NotFound(string message) => new("NotFoundError", message);

    public static DomException Namespace(string message) => new("NamespaceError", message);

    public static DomException WrongDocument(string message) => new("WrongDocumentError", message);

    public static DomException InvalidState(string message) => new("InvalidStateError", message);

    public static DomException InvalidNodeType(string message) => new("InvalidNodeTypeError", message);

    public static DomException IndexSize(string message) => new("IndexSizeError", message);

    public static DomException Syntax(string message) => new("SyntaxError", message);

    public static DomException NoModificationAllowed(string message) => new("NoModificationAllowedError", message);

    public static DomException NotSupported(string message) => new("NotSupportedError", message);
}
