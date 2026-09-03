# Broiler.DOM

The canonical, dependency-free DOM and HTML parsing component for Broiler, targeting
.NET 10.

It contains:

- `Broiler.Dom`: document, node, element, attribute, mutation, range, and traversal
  primitives.
- `Broiler.Dom.Html`: HTML tokenization, document and fragment parsing, tree building,
  and serialization.

## Continuous integration

Every push and pull request builds both libraries on Linux and Windows, runs both
test suites, and packs the two packages to prove they still build. Both legs run
the same platform-neutral code deliberately: a tokenizer is where two hosts
disagree about newlines, string comparison and culture, and the character-reference
decoding this component hands to the platform is a different platform on each.

## Standards and rights

Both bodies that have stewarded the DOM and HTML — the WHATWG and the W3C —
operate **royalty-free** patent policies. Both bind participants over Essential
Claims and both let a participant exclude specific patents during a disclosure
window, so neither is a statement that no relevant claim exists.

That, and what this repository does and does not contain, is recorded in
[the DOM IP and licensing register](docs/dom-ip-licensing-register.md). **Read it
before relying on anything here: every row is pending a decision.** No lawyer has
reviewed it, patent-freedom is not claimed, and no freedom-to-operate
determination has been made.

One finding is worth repeating outside the register. The HTML Standard's named
character reference table — roughly 2,231 entries, and the largest thing an HTML
parser might copy — **is not in this repository**. Two call sites hand the work to
`System.Net.WebUtility`, and a guard test fails the build if a transcribed table
ever appears. Neither library takes a package reference, no third-party
conformance suite is committed, and no specification text is reproduced.

This component is a tokenizer, a tree builder and a serializer. **It is not a
browser engine** and nothing here may describe it as one, or as HTML5-conformant.

## Preview status

This is first-preview software. Its API and behavior may change without compatibility
guarantees. Substantial implementation work was AI-assisted. Human-review approval is
revision-scoped; consult [HUMAN_REVIEW.md](HUMAN_REVIEW.md) for the reviewed revision
and conditions before describing the current checkout as approved.

Broiler.DOM is an independent Broiler component. It interoperates with Broiler.HTML,
whose rendering lineage comes from HTML Renderer, but it must not be represented as an
official HTML Renderer component or as endorsed by that project's contributors.

## Ownership and compatibility

`Broiler.Dom` is the only mutable tree and owns engine-neutral DOM algorithms;
`Broiler.Dom.Html` owns parsing and serialization. Renderer geometry and JavaScript
wrappers remain in HtmlBridge because they depend on computed style, layout, or the
script runtime.

The current HtmlBridge pipeline consumes this canonical model directly. Bridge-owned
JavaScript wrappers do not constitute a second DOM tree, and bridge compatibility or
public-surface work is owned by the main Broiler repository rather than this component.

## Build and test

```bash
dotnet build Broiler.Dom.slnx
dotnet test Broiler.Dom.slnx
```

## Documentation

- [Current roadmap](docs/roadmap.md) — the remaining component release gate
- [Human-review record](HUMAN_REVIEW.md) — revision-scoped preview decision

## License

Broiler.DOM is licensed under the [Apache License 2.0](LICENSE). Third-party material, if
present, retains the license identified with that material. The license provides the
software on an “AS IS” basis, without warranties or conditions.
