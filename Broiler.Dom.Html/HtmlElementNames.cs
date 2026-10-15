using System;
using System.Collections.Frozen;

namespace Broiler.Dom.Html;

// Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: the void set omits a void name such as img, so the parser leaves <img> open and the following content becomes its children
// Broiler-Human:        PENDING
internal static class HtmlElementNames
{
    // The void elements plus the legacy names HTML still parses and serializes as void: basefont,
    // bgsound, frame and keygen. Tree construction inserts each and immediately pops it (HTML
    // §13.2.6.4.4 for basefont and bgsound, §13.2.6.4.7 for keygen), and fragment serialization
    // emits no end tag for any of them (§13.3 "serializes as void").
    // Broiler-AI:           Origin=AI; Spec=WHATWG-HTML s13.3; IP=None; Security=High; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a name that takes content, such as template or script, is in the set, so its start tag opens no element and the serializer writes it with no end tag
    // Broiler-Human:        PENDING
    internal static readonly FrozenSet<string> VoidElements = new[]
    {
        "area", "base", "basefont", "bgsound", "br", "col", "embed", "frame", "hr", "img", "input",
        "keygen", "link", "meta", "param", "source", "track", "wbr"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
