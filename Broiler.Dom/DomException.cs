using System;

namespace Broiler.Dom;

public sealed class DomException(string message) : InvalidOperationException(message)
{
    internal static DomException HierarchyRequest(string message) => new(message);

    internal static DomException NotFound(string message) => new(message);

    internal static DomException Namespace(string message) => new(message);

    internal static DomException WrongDocument(string message) => new(message);
}
