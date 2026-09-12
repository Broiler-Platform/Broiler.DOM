using System;
using System.Collections.Frozen;

namespace Broiler.Dom.Html;

internal static class HtmlElementNames
{
    // Includes frame: parsing immediately closes it and serialization emits no end tag.
    internal static readonly FrozenSet<string> VoidElements = new[]
    {
        "area", "base", "br", "col", "embed", "frame", "hr", "img", "input",
        "link", "meta", "param", "source", "track", "wbr"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
