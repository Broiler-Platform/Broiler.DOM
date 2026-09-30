using System.Collections.Generic;

namespace Broiler.Dom;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public enum DomMutationType
{
    ChildList,
    Attributes,
    CharacterData,
    Adoption,
}

// Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed record DomMutationRecord(
    DomMutationType Type,
    DomNode Target,
    IReadOnlyList<DomNode>? AddedNodes = null,
    IReadOnlyList<DomNode>? RemovedNodes = null,
    DomNode? PreviousSibling = null,
    DomNode? NextSibling = null,
    string? AttributeName = null,
    string? AttributeNamespace = null,
    string? OldValue = null,
    string? NewValue = null,
    DomDocument? OldDocument = null,
    DomDocument? NewDocument = null);
