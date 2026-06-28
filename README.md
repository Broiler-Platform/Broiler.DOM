# Broiler.DOM

The canonical, dependency-free DOM and HTML parsing component for Broiler, targeting
.NET 10.

It contains:

- `Broiler.Dom`: document, node, element, attribute, mutation, range, and traversal
  primitives.
- `Broiler.Dom.Html`: HTML tokenization, document and fragment parsing, tree building,
  and serialization.

## Preview status

This is first-preview software. Its API and behavior may change without compatibility
guarantees. Substantial implementation work was AI-assisted. The component is **not
human-approved for preview use** while [HUMAN_REVIEW.md](HUMAN_REVIEW.md) remains
`PENDING`.

Broiler.DOM is an independent Broiler component. It interoperates with Broiler.HTML,
whose rendering lineage comes from HTML Renderer, but it must not be represented as an
official HTML Renderer component or as endorsed by that project's contributors.

## Ownership and compatibility

`Broiler.Dom` is the only mutable tree and owns engine-neutral DOM algorithms;
`Broiler.Dom.Html` owns parsing and serialization. Renderer geometry and JavaScript
wrappers remain in HtmlBridge because they depend on computed style, layout, or the
script runtime.

The HtmlBridge `DomElement` and `HtmlTreeBuilder` surfaces are versioned as
`htmlbridge-dom-adapter/v1`. They delegate to this component rather than owning a
second tree or parser, and are removable only at the
`htmlbridge-public-surface/v2` boundary.

## Build and test

```bash
dotnet build Broiler.Dom.slnx
dotnet test Broiler.Dom.slnx
```

## License

Broiler.DOM is licensed under the [Apache License 2.0](LICENSE). Third-party material, if
present, retains the license identified with that material. The license provides the
software on an “AS IS” basis, without warranties or conditions.
