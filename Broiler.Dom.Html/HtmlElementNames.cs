using System;
using System.Collections.Frozen;

namespace Broiler.Dom.Html;

// Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: the void set omits a void name such as img, so the parser leaves <img> open and the following content becomes its children
// Broiler-Human:        PENDING
internal static class HtmlElementNames
{
    // Includes frame: parsing immediately closes it and serialization emits no end tag.
    // Broiler-AI:           Origin=AI; IP=None; Security=High; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a name that takes content, such as template or script, is in the set, so its start tag opens no element and the serializer writes it with no end tag
    // Broiler-Human:        PENDING
    internal static readonly FrozenSet<string> VoidElements = new[]
    {
        "area", "base", "br", "col", "embed", "frame", "hr", "img", "input",
        "link", "meta", "param", "source", "track", "wbr"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
