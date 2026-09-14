using System;

namespace Broiler.Dom;

/// <summary>
/// The bitmask <see cref="DomNode.CompareDocumentPosition"/> returns. The values are the DOM
/// Standard's <c>Node.DOCUMENT_POSITION_*</c> constants, so a script binding passes the number
/// through unchanged.
/// </summary>
[Flags]
public enum DomDocumentPosition : ushort
{
    None = 0,
    Disconnected = 0x01,
    Preceding = 0x02,
    Following = 0x04,
    Contains = 0x08,
    ContainedBy = 0x10,
    ImplementationSpecific = 0x20,
}
