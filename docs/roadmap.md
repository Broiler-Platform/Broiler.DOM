# Broiler.DOM roadmap

The canonical DOM and HTML parser extraction is complete, and the old bridge-owned
`DomElement`/`HtmlTreeBuilder` compatibility surfaces have already been removed from the
main Broiler pipeline. There is no open component feature or compatibility migration in
the current tree.

## Preview review

Human review is tied to the revision recorded in `HUMAN_REVIEW.md`. A new preview claim
requires review of subsequent changes and an updated commit-scoped record. Until then,
this is the only open release gate tracked here.

The review should rerun the DOM, parser, serializer, mutation-observer, range, traversal,
and architecture suites and confirm that:

- `Broiler.Dom` remains free of non-BCL project dependencies;
- `Broiler.Dom.Html` remains the only HTML parse/serialize owner; and
- the main Broiler pipeline still uses one canonical mutable document.
