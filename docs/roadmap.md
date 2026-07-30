# Broiler.DOM roadmap

The canonical DOM and HTML parser extraction is complete, and the old bridge-owned
`DomElement`/`HtmlTreeBuilder` compatibility surfaces have already been removed from the
main Broiler pipeline. What remains open is narrower and specific: a set of
specification-defined algorithms that HtmlBridge still implements because this component
exposes no equivalent API. Each one is engine-neutral — independent of JavaScript
identity, computed style, layout, and host policy — so it belongs here.

`Broiler.Dom` is 2,473 lines and `Broiler.Dom.Html` is 1,180 lines; the bridge assembly
that adapts them is 36,080 lines. That gap is not a size target: closing the items below
moves roughly 2,000 lines of neutral algorithm into this component and lets the bridge
delete roughly 500 lines of compatibility shims outright.

This file owns the API design, unit tests, and component exit gates. The consumer-side
cutover order, deletions, and guard changes are sequenced by the aggregate Broiler
repository, in the "DOM component rehoming" section of its root roadmap
(`docs/ROADMAP.md`), as waves 1–6 over the items below.

## Admission rules

An algorithm is admissible here only when all of the following hold. Anything failing a
rule stays in the bridge or belongs to another component.

- `Broiler.Dom` acquires no project or package dependency. This is enforced by
  `Future_Dom_Kernel_Project_Must_Remain_Dependency_Free` in the aggregate repository's
  `HtmlBridgeBoundaryGuardTests`.
- `Broiler.Dom.Html` references `Broiler.Dom` and nothing else, enforced by
  `Dom_Html_Depends_Only_On_The_Canonical_Dom_Component`.
- The API signature contains no JavaScript, style, layout, resource, or host type, and
  takes no callback into bridge-owned mutable state. Value and `DomNode` parameters only.
- Failure is reported as a `DomException` carrying the spec error name, or as a return
  value. Callers marshal it into `DOMException`; this component never constructs one.
- Behavior is pinned by owner-local tests in `Broiler.Dom.Tests` or
  `Broiler.Dom.Html.Tests` before any consumer cuts over.

## D1 — element attribute access by qualified name

**Owner:** `Broiler.Dom`.

**Current evidence:** `DomElement.GetAttribute` lowercases the qualified name before the
lookup, so it cannot retrieve `viewBox`, `preserveAspectRatio`, or `xlink:href`. Three
independent case-insensitive scans over `Attributes.Values` exist to work around it: the
bridge's `DomBridge/Attributes.cs` helper set (~195 call sites in the bridge),
`HtmlElementQueries.ReadNumericAttribute` here, and `CssSelectorMatcher.MatchesAttribute`
in `Broiler.CSS.Dom`. Three copies of one lookup is the largest single duplication
standing against this component.

**API:** add to `DomElement`, keyed by qualified name with ASCII case-insensitive
matching over the namespace-keyed attribute set:

- `bool TryGetAttributeByQualifiedName(string qualifiedName, out string value)`
- `string? GetAttributeByQualifiedName(string qualifiedName)`
- `bool HasAttributeByQualifiedName(string qualifiedName)`
- `void SetAttributeByQualifiedName(string qualifiedName, string value)` — updates a
  matched attribute in place, preserving its namespace; otherwise creates a
  no-namespace attribute
- `bool RemoveAttributeByQualifiedName(string qualifiedName)`
- `bool TryGetAttributeNS(string? namespaceUri, string localName, out string qualifiedName, out string value)`
  — yields the possibly-prefixed name together with the value
- `IEnumerable<string> AttributeQualifiedNames { get; }`

**Next actions:**

1. Add the accessors with the bridge helpers' exact semantics (ASCII case-insensitive
   scan, namespace preserved on update, empty-string `out` value when absent) so the
   cutover is behavior-preserving.
2. Cover attribute-name case, prefixed names, SVG mixed-case names, namespace
   preservation across an update, removal by qualified name, and a no-namespace attribute
   colliding with a namespaced one of the same local name.
3. Route `HtmlElementQueries.ReadNumericAttribute` and `CssSelectorMatcher`
   `MatchesAttribute` through the new accessor and delete their local scans.
4. Record, but do not yet perform, the related spec correction: `getAttribute` should
   lowercase only for an HTML element in an HTML document. Changing the existing
   `GetAttribute` alters behavior for every consumer and needs its own pixel and WPT
   baseline, so it is a separate item gated on D1 landing first.

**Exit gate:** one case-insensitive qualified-name lookup exists in the tree and it is in
`Broiler.Dom`; the bridge's `Attributes.cs` accessor set and the `Broiler.CSS.Dom` and
`Broiler.Dom.Html` local scans are deleted; the attribute suites pass unchanged.

## D2 — CharacterData mutation methods

**Owner:** `Broiler.Dom`.

**Current evidence:** `DomCharacterData` is 54 lines and exposes only the `Data`
property. DOM §4.10's mutation methods and `Text.splitText` are implemented in the
bridge's `Features/CharacterDataBinding.cs` as string arithmetic plus a sibling insert,
raising `INDEX_SIZE_ERR` as a raw JavaScript exception string. `DomException` has no
`IndexSize` factory.

**API:**

- `DomCharacterData.Length { get; }`
- `string SubstringData(int offset, int count)`
- `void AppendData(string data)`
- `void InsertData(int offset, string data)`
- `void DeleteData(int offset, int count)`
- `void ReplaceData(int offset, int count, string data)`
- `DomText DomText.SplitText(int offset)`
- `DomException.IndexSize(string message)`

**Next actions:**

1. Add the methods with spec offset validation (`IndexSizeError` when `offset` exceeds
   `Length`) and `count` clamped to the remaining length.
2. Keep each operation a single `Data` write so exactly one `CharacterData` mutation
   record reaches `DomDocument.Mutated`, matching what observers and live ranges see
   today.
3. Implement `SplitText` as: truncate the node, create the new `DomText` from the owner
   document, insert it as the next sibling. Confirm against a tracked `DomRange` that
   boundary points inside the split node move as the spec requires, and add that test
   whether or not current behavior already satisfies it.
4. Reduce the bridge binding to argument coercion, `DOMException` marshalling, and
   wrapper-cache invalidation.

**Exit gate:** the character-data methods are canonical and spec-validated; the bridge
binding contains no string arithmetic; the mutation-record count per operation is pinned
by test.

## D3 — node relationship and traversal gaps

**Owner:** `Broiler.Dom`.

**Current evidence:** `DomNode.TextContent` is get-only, so the bridge keeps its own
`SetElementTextContent`, while `DomBridge.GetElementTextContent` duplicates the canonical
getter outright. `compareDocumentPosition` lives in the bridge as `CompareTreeOrder`,
which already delegates to `DomRange.CompareBoundaryPoints` but keeps the disconnected
and ancestor guards and returns `-1`/`0`/`1` rather than the spec bitmask. The
element-only traversal accessors are recomputed per call in
`Features/ElementTraversalBinding.cs`.

**API:**

- a `DomNode.TextContent` setter: replaces all children with a single `DomText`, or
  removes all children for a null or empty value
- `DomNode.CompareDocumentPosition(DomNode other)` returning the spec bitmask, with
  `DomDocumentPosition` constants (`Disconnected`, `Preceding`, `Following`, `Contains`,
  `ContainedBy`, `ImplementationSpecific`)
- `DomElement.ChildElements`, `FirstElementChild`, `LastElementChild`,
  `NextElementSibling`, `PreviousElementSibling`, `ChildElementCount`

**Next actions:**

1. Add the members and cover disconnected nodes, ancestor/descendant pairs, sibling
   order, and text-only trees.
2. Keep the bitmask spec-shaped. The bridge maps it to the IDL number; the current
   tri-state contract is not preserved.
3. Delete `DomBridge.GetElementTextContent`, `SetElementTextContent`, and
   `CompareTreeOrder`, routing their call sites to the canonical members.

**Exit gate:** no bridge copy of `textContent`, document-order comparison, or
element-traversal accessors remains, and the returned position is a spec bitmask.

## D4 — tree-mutation mixins and shim retirement

**Owner:** `Broiler.Dom` for the API; the bridge owns the call-site cleanup.

**Current evidence:** `Features/ChildNodeBinding.cs` and `Features/TreeMutationBinding.cs`
implement the `ChildNode` and `ParentNode` mixins over a block of bridge helpers in
`DomBridge.cs` (`ChildElements`, `ChildAt`, `InsertChildAt`, `RemoveChildFrom`,
`RemoveNthChild`, `ClearChildren`, `SetParent`, `ParentEl`, `IsText`, `IsComment`,
`BridgeText`). Those helpers are self-documented as shims for a removed legacy facade and
account for roughly 490 occurrences in the bridge. Most are one-line paraphrases of
canonical members and need no promotion at all — only `Before`, `After`, `ReplaceWith`,
`Append`, and `Prepend` are missing API.

**API:** `DomNode.Before(params DomNode[] nodes)`, `After(...)`, `ReplaceWith(...)`,
`Append(...)`, and `Prepend(...)`, each performing the spec's pre-insert validity checks
and throwing `DomException.HierarchyRequest` on a cycle.

**Next actions:**

1. Add the five mixin methods taking `DomNode` parameters only. Converting a string
   argument into a text node is IDL behavior and stays in the bridge.
2. Cover the move-within-same-parent index adjustment, cycle rejection, a
   `DomDocumentFragment` argument expanding to its children, and the record count per
   operation.
3. Classify each shim in `DomBridge.cs` as *delete and inline the canonical member* or
   *keep, with a documented bridge-specific reason*, then retire the first group. This
   step is mechanical but large, and it is what makes the remaining bridge readable.

**Exit gate:** the mixins are canonical; the shim block in `DomBridge.cs` retains only
helpers with a stated bridge-specific reason; no call site paraphrases a canonical member.

## D5 — HTML element operations

**Owner:** `Broiler.Dom.Html`.

**Current evidence:** `HtmlElementQueries.CollectTableRows` and `CollectFormControls` are
already canonical, which proves the seam, but the neighboring operations never followed.
`Features/TableBinding.cs` owns caption/section/row/cell creation, insertion, deletion,
and index resolution; `Features/SelectBinding.cs` owns option collection,
`selectedIndex`, and value resolution. Both are pure tree work over the owner document's
element factories.

**API:** two stateless static classes beside `HtmlElementQueries`:

- `HtmlTableOperations` — `GetCaption`, `CreateCaption`, `DeleteCaption`, `GetSection`,
  `CreateSection`, `DeleteSection`, `GetTableBodies`,
  `InsertRow(DomElement table, int index)`, `DeleteRow`, `SectionInsertRow`,
  `GetRowIndex`, `GetSectionRowIndex`, `GetRowCells`, `InsertCell`, `DeleteCell`.
  Elements are created from `table.OwnerDocument`, so no host contract or factory
  delegate is required.
- `HtmlSelectQueries` — `GetOptions`, `GetSelectedIndex`, `SetSelectedIndex`, `GetSize`,
  `ResolveValue`, `TrySelectValue`, `AddOption(select, option, reference)`.

**Next actions:**

1. Add the table operations with the spec ordering rules, pinning the cases the current
   implementation gets subtly right: `insertRow(-1)` on a table with no sections creating
   a `tbody`, a negative `deleteRow` index counting from the end, `rowIndex` spanning
   `thead`/`tbody`/`tfoot`, `sectionRowIndex` counting only `tr` siblings, and
   `insertCell` positioning against `td`/`th` children rather than all children.
2. Add the select queries, including the value fallback to an option's text content and
   the first-non-disabled-option default.
3. Report index failures as `DomException.IndexSize` or a `bool`, never as a bridge
   exception type, so the binding maps them to the IDL error.
4. Reduce `TableBinding` and `SelectBinding` to wrapper installation, argument coercion,
   and JavaScript collection identity.

**Exit gate:** the table and select algorithms have one owner with owner-local tests; the
two bindings contain no tree arithmetic; the bridge's `IsTableCellElement` predicate is
canonical or deleted.

## D6 — fragment, document metadata, and adjacency semantics

**Owner:** `Broiler.Dom.Html`.

**Current evidence:** four HTML-semantic rules are bridge-private today.

- `TryBuildInnerHtmlFragmentContainer` in `DomBridge/HtmlFragmentMutation.cs` decides the
  fragment parsing context: it rejects void elements using `HtmlSerializer.VoidElements`
  and substitutes a neutral context for a `#`-prefixed bridge sentinel.
- `TryFindDocumentBaseHref` in `DomBridge/StyleBaseHref.cs` finds the document's
  effective `<base href>`.
- `<meta>` discovery exists twice, in two shapes: `ApplyMetaColorScheme` scans the parsed
  tree, and `CspMetaDiscovery.FindPolicyContent` in `Broiler.HtmlBridge.Core` tokenizes
  raw text before parsing, because it must run before script execution.
- `Features/InsertAdjacentBinding.cs` resolves `beforebegin`/`afterbegin`/`beforeend`/
  `afterend` to a parent and index.

**API:**

- `HtmlFragmentParsing.TryGetFragmentParsingContext(DomElement contextElement, out string contextTagName)`
- `HtmlDocumentQueries.GetEffectiveBaseHref(DomNode root)`,
  `TryGetMetaContent(DomNode root, string name, out string content)`, and
  `TryGetMetaHttpEquivContent(DomNode root, string httpEquiv, out string content)`
- `HtmlMetaScanner.TryFindMetaContent(string html, string httpEquiv, out string content)`
  for the pre-parse token-based path
- `HtmlAdjacentPosition.TryResolve(DomElement element, string position, out DomNode parent, out int index)`

**Next actions:**

1. Add the four APIs. Keep both meta shapes — the tree walk and the token scan are
   different phases of the document lifecycle, not a duplication to collapse — but let
   them share the token and attribute reading already in this component.
2. Keep policy out. `HtmlMetaScanner` returns a directive string; CSP parsing,
   enforcement, and nonce handling stay in the bridge.
3. Delete `DomBridge/HtmlTreeBuilding.cs` once its callers use `HtmlDocumentParser`
   directly; its own comments already record it as retired.
4. Leave the `innerHTML`/`outerHTML` orchestration in the bridge. Only the context rules
   move; the surrounding wrapper-cache teardown and style-scope invalidation are bridge
   state.

**Exit gate:** the bridge holds no `<base>` or `<meta>` scan and no fragment-context
rule; `HtmlTreeBuilding.cs` is deleted; CSP discovery uses the canonical scanner while
policy stays in the bridge.

## D7 — form control state companion

**Owner:** `Broiler.Dom.Html`, blocked on characterization.

**Current evidence:** `Features/FormControlBinding.cs` mixes two kinds of behavior.
Content-attribute reflection (`type`, `name`, `disabled`, `hidden`, `tabIndex`,
`required`) is neutral and admissible now. The dirty IDL `value` and `checked` state is
per-element bridge runtime state reached through `IFormControlHost`, and radio-group
mutual exclusion is a bridge tree walk. `HtmlElementQueries.CollectFormControls` is
already canonical.

Promoting the state as it stands would make unverified behavior canonical: the default,
dirty-flag, and `reset()` rules are not currently pinned against a reference browser.

**Next actions:**

1. Write characterization tests first, against the recorded Chromium baseline, for
   `value`/`defaultValue`, `checked`/`defaultChecked`, the dirty-flag transitions on
   attribute versus property writes, `form.reset()`, and radio-group exclusion across
   nested and `form`-attribute-associated controls.
2. Correct whatever the baseline shows to be wrong while the code is still bridge-local
   and cheap to change.
3. Then add a document-scoped `HtmlFormState` owning the dirty value/checked flags, plus
   `HtmlFormQueries` for listed elements, reset behavior, and radio-group resolution.
   Document-scoped, never process-static.
4. Move the neutral reflectors independently; they do not need to wait for the state work.

**Exit gate:** form control state has one documented owner, the reset/default/radio rules
match the recorded baseline, and the binding keeps only IDL plumbing and conversions.

## D8 — canonical shadow tree model

**Owner:** `Broiler.Dom`, coordinated with `Broiler.CSS.Dom`, `Broiler.HTML.Dom`, and
Layout through Phase 3 of the aggregate repository's root roadmap.

**Current evidence:** there is no shadow model here at all. The bridge represents a shadow
root as a synthetic `#shadow-root` element, and rendering hides light children, unwraps
the sentinel, and rewrites selectors onto marker attributes (`DomBridge/ShadowDom.cs`,
`ShadowHostSelectors.cs`, `ShadowSlotRendering.cs`). This is the largest blocked item and
the one where promoting the current shape would do real harm: it would make the
workaround permanent.

**API:**

- `DomShadowRoot : DomDocumentFragment` with `Host` and `Mode`
- `DomElement.AttachShadow(DomShadowRootMode mode)` and `DomElement.ShadowRoot`
- slot assignment: `DomSlotAssignment`, plus `DomElement.AssignedSlot` and
  `DomElement.AssignedNodes`
- composed-tree traversal: `DomNode.ComposedChildNodes`, `ComposedParent`,
  `ComposedAncestors`, and `GetRootNode(bool composed)`

**Next actions:**

1. Add the model and traversal with no rendering or selector concern, and test it against
   the cases the synthetic root currently encodes: closed versus open mode, nested shadow
   roots, default and named slots, fallback content, and slot reassignment on mutation.
2. Coordinate the consumer cutover through that Phase 3. Scoped selector matching is
   `Broiler.CSS` work and composed-tree painting is `Broiler.HTML`/Layout work; neither
   belongs in this component.

**Exit gate:** the canonical model expresses every case the bridge sentinel does, with
owner-local tests. Deleting the bridge's selector stamping, marker attributes, and
light-child hiding is gated on the consumers listed in that Phase 3.

## Explicitly not owned here

These are frequently mistaken for DOM work. Accepting any of them would violate the
admission rules above.

- Layout, anchor positioning, sticky, scroll snap, transforms, hit testing, and used
  geometry — `Broiler.Layout`.
- CSS import and URL resolution, cascade, keyframe interpolation, and view-transition
  pseudo-style resolution — `Broiler.CSS` and `Broiler.CSS.Dom`.
- Replaced-element, top-layer, and composed-tree painting — `Broiler.HTML`.
- Regex HTML post-processing (`HtmlPostProcessor`) and the render-time serialization
  transforms — retired or moved to `Broiler.Wpt`, never promoted.
- JavaScript wrapper identity, callbacks, promises, event dispatch, timers, networking,
  origins, and CSP enforcement — HtmlBridge and the host.

## Preview review

Human review is tied to the revision recorded in `HUMAN_REVIEW.md`. A new preview claim
requires review of subsequent changes and an updated commit-scoped record. Until then,
this is the only open release gate tracked here.

The review should rerun the DOM, parser, serializer, mutation-observer, range, traversal,
and architecture suites and confirm that:

- `Broiler.Dom` remains free of non-BCL project dependencies;
- `Broiler.Dom.Html` remains the only HTML parse/serialize owner; and
- the main Broiler pipeline still uses one canonical mutable document.
