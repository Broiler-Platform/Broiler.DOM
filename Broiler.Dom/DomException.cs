using System;

namespace Broiler.Dom;

// Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: a factory-built exception reports the default Name Error instead of its own DOMException name, so script that branches on the name takes the wrong path
// Broiler-Human:        PENDING
public sealed class DomException : InvalidOperationException
{
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an exception built from a message alone reports a Name other than Error
    // Broiler-Human:        PENDING
    public DomException(string message)
        : this("Error", message)
    {
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: the exception reports a Name other than the name it was constructed with
    // Broiler-Human:        PENDING
    private DomException(string name, string message)
        : base(message)
        => Name = name;

    /// <summary>
    /// The DOM error name (e.g. <c>"HierarchyRequestError"</c>), per the
    /// <c>DOMException</c> interface. Defaults to <c>"Error"</c> when unspecified.
    /// </summary>
    public string Name { get; }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: InvalidCharacter returns an exception whose Name is not InvalidCharacterError
    // Broiler-Human:        PENDING
    public static DomException InvalidCharacter(string message) => new("InvalidCharacterError", message);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: HierarchyRequest returns an exception whose Name is not HierarchyRequestError
    // Broiler-Human:        PENDING
    public static DomException HierarchyRequest(string message) => new("HierarchyRequestError", message);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: NotFound returns an exception whose Name is not NotFoundError
    // Broiler-Human:        PENDING
    public static DomException NotFound(string message) => new("NotFoundError", message);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: Namespace returns an exception whose Name is not NamespaceError
    // Broiler-Human:        PENDING
    public static DomException Namespace(string message) => new("NamespaceError", message);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: WrongDocument returns an exception whose Name is not WrongDocumentError
    // Broiler-Human:        PENDING
    public static DomException WrongDocument(string message) => new("WrongDocumentError", message);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: InvalidState returns an exception whose Name is not InvalidStateError
    // Broiler-Human:        PENDING
    public static DomException InvalidState(string message) => new("InvalidStateError", message);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: InvalidNodeType returns an exception whose Name is not InvalidNodeTypeError
    // Broiler-Human:        PENDING
    public static DomException InvalidNodeType(string message) => new("InvalidNodeTypeError", message);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: IndexSize returns an exception whose Name is not IndexSizeError
    // Broiler-Human:        PENDING
    public static DomException IndexSize(string message) => new("IndexSizeError", message);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: Syntax returns an exception whose Name is not SyntaxError
    // Broiler-Human:        PENDING
    public static DomException Syntax(string message) => new("SyntaxError", message);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: NoModificationAllowed returns an exception whose Name is not NoModificationAllowedError
    // Broiler-Human:        PENDING
    public static DomException NoModificationAllowed(string message) => new("NoModificationAllowedError", message);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: NotSupported returns an exception whose Name is not NotSupportedError
    // Broiler-Human:        PENDING
    public static DomException NotSupported(string message) => new("NotSupportedError", message);
}
