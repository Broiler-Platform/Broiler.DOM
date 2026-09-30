namespace Broiler.Dom;

// Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
// Broiler-Human:        PENDING
public class DomDocumentFragment : DomNode
{
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    internal DomDocumentFragment(DomDocument ownerDocument)
        : base(DomNodeType.DocumentFragment, ownerDocument)
    {
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    internal override DomNode CloneShallow(DomDocument ownerDocument) =>
        new DomDocumentFragment(ownerDocument);
}
