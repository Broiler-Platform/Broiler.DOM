# Human Review: Broiler.DOM

> **Status: PENDING HUMAN REVIEW for `0.1.0-preview.2`.**
>
> `0.1.0-preview.2` was published to GitHub Packages on 2026-09-14 from the commit below,
> **before** this review round. The previous approval (first preview, commit `2a1e370`,
> 2026-07-01) does not cover that revision. Until a human reviewer completes the decision
> and attestation, neither the published preview nor the current checkout may be described
> as approved.

## Review Target

- **Component:** Broiler.DOM
- **Scope:** The canonical DOM and HTML tokenization, parsing, mutation, traversal, and
  serialization assemblies (`Broiler.Dom`, `Broiler.Dom.Html`).
- **Release:** `0.1.0-preview.2` (published to GitHub Packages)
- **Commit:** `8252dbd4b03be779b351081c8a267fef70073555`
- **Previously reviewed commit:** `2a1e370d985d9d6c6a846dec561d8646c17b9b29`
- **Reviewer:** _to be completed by the human reviewer_
- **Reviewer contact or profile:** _to be completed_
- **Review date:** _to be completed_
- **Intended preview use:** An in-memory DOM and HTML parsing and serialization component
  for Broiler preview integration. API and behavior compatibility are not yet guaranteed.

Commits that change only this file do not change the reviewed source. Any other source
change after the commit above invalidates the review until that revision is reviewed.

## Changes Since The Previously Reviewed Commit

`git log 2a1e370..8252dbd` has 58 commits; `git diff --shortstat 2a1e370 8252dbd`
reports 62 files changed, 7,522 insertions and 1,758 deletions, including tests and
documentation. The themes below summarize the history. They are not a substitute for
reviewing the diff.

- **New DOM API:**
  - `Node.moveBefore` (`994e196`)
  - `DomNode.IsEqualNode` (`fd76f5e`)
  - `DomNode.CommonAncestorWith` (`8a48f1b`)
  - public `DomNodeCollectionExtensions` (`5c71ac9`)
  - `DomRange` content operations and stringifier (#7, #9)
  - name-validation refactor and colon handling in `createElement` (#11, `6361b63`)
  - `Normalize` publishing one record per text run (`8e8325f`)
  - in `0.1.0-preview.2` (#18): the `DomNode.TextContent` setter,
    `CompareDocumentPosition` with `DomDocumentPosition`, and the element-traversal
    members (`ParentElement`, `ChildElements`, `First`/`LastElementChild`,
    `ChildElementCount`, `Previous`/`NextElementSibling`)
- **HTML parsing and serialization:**
  - iterative serialization, so deep trees no longer overflow the stack (`44a4fdc`)
  - attribute character references decoded in the tokenizer (`7e06203`)
  - `<frame>` as a void element (`55057b8`)
  - `<noscript>` as raw text (`e27ac6f`)
  - tokenizer runs (#12)
  - raw-text serialization and entity decoding promoted from the bridge (#8, #10)
- **Refactors:** broad refactor commits (`01d9046`, `4350769`, `37207ac`, `62e0f21`,
  `8d0cc44`, `14f1d43`).
- **Packaging, CI and rights:**
  - vendored packaging props (`c89a0b3`, #13)
  - CI (#15), then the shared CI/CD with Publish (`ce05520`)
  - Source Link from the SDK (#17)
  - the IP and licensing register and its approvals (#14, #16)

## Evidence

The evidence below was assembled with AI assistance on 2026-09-15. It is input to the
review, not its result; the reviewer confirms it.

- **CI:** `CI` run 34892923126 on the target commit passed on `ubuntu-latest` and
  `windows-latest`.
- **Publish:** `Publish` run 34892923492 succeeded on the target commit. The resolver
  selected `0.1.0-preview.2`, both packages were verified, a fresh consumer restore
  against GitHub Packages was verified, and the packages were pushed.
- **Local build and tests:** run on 2026-09-14 on Windows with .NET SDK 10.0.400, on a
  tree identical to the target commit.
  - Release build: 0 warnings, 0 errors.
  - `Broiler.Dom.Tests`: 122 passed, 0 failed.
  - `Broiler.Dom.Html.Tests`: 55 passed, 0 failed.
- **Consumer compile check:** `Broiler.CSS`, where 771 tests pass, and
  `Broiler.Documents.Html` both built against packages packed from this tree. Both
  consumers' CI later passed against the published packages (Broiler.CSS #33,
  Broiler.Documents #91).
- **Runtime dependencies:** `Broiler.Dom` has no project or package references.
  `Broiler.Dom.Html` references `Broiler.Dom` only.
- **Security-sensitive API sweep (production code):** no file-system, process,
  native-interop, `unsafe`/`stackalloc`, reflection, environment, or network-client usage
  was found. Present:
  - `System.Net.WebUtility`, used only for `HtmlEncode` (serializer) and `HtmlDecode`
    (tokenizer), per register row DOM-IP-003
  - two compile-time generated name-validation regular expressions
    (`DomNameValidation.cs`)

### Commands

```sh
dotnet build Broiler.Dom.slnx -c Release
bash ./eng/run-tests.sh Release
```

## Findings And Residual Risks

### Carried from the first-preview review

- **Dead code:** dead or transitional code remains, pending the global refactoring.
- **Compiler cleanup:** cleanup opportunities remain, such as unused `using` directives
  and methods eligible to be `static`.
- **Parser and string handling:** malformed input, edge-case HTML, and serializer
  round-trips need continued focused tests.
- **Static analysis:** no separate full static-analysis or vulnerability-scanning pass
  has been recorded.

### Open items for this round, not yet confirmed by the reviewer

- **Published before review:** `0.1.0-preview.2` is already consumed by Broiler.CSS and
  Broiler.Documents.
- **New behavior to review:**
  - The `TextContent` setter replaces all children and publishes a single child-list
    record, as the spec's "replace all" does; `DomRange` and `DomNodeIterator` are
    relied on to handle that record shape.
  - `CompareDocumentPosition` orders disconnected trees by a per-root sequence number
    held in a static `ConditionalWeakTable`.
- **Performance:** sibling access (`PreviousSibling`, `NextSibling`, and the element
  variants) scans the parent's child list per call.
- **Test framework:** the test projects still use xunit 2.5.3 with `Timeout` on
  synchronous `[Fact]`s. Newer xunit rejects that, as Broiler.CSS and Broiler.Documents
  found when updating.

## Decision

- [ ] **APPROVED FOR PREVIEW** within the intended-use scope above.
- [ ] **APPROVED WITH CONDITIONS** listed below.
- [ ] **NOT APPROVED** for preview use.

**Conditions:** _to be completed by the reviewer._

## Human Attestation

I confirm that I am a human developer, that I personally reviewed the revision and
evidence identified above, and that the decision is my own. I understand that this
attestation is a scoped engineering review, not a warranty or a claim that the component
is free of defects or vulnerabilities.

- **Name:** _to be completed_
- **Reviewer alias:** _to be completed_
- **Signature or attributable commit:** _to be completed_
- **Date:** _to be completed_

AI tools may help assemble evidence, but the review decision, reviewer identity, and
attestation are attributable to the human reviewer named above.

## Previous Review

- **First preview: APPROVED WITH CONDITIONS.**
  - Reviewer: MaiRat / Maik Ratzmer.
  - Commit: `2a1e370d985d9d6c6a846dec561d8646c17b9b29`.
  - Date: 2026-07-01.
  - Tests: 25 passed.
  - Conditions:
    - approval limited to first-preview use and that commit
    - dead or transitional code accepted temporarily
    - compiler cleanup and parser edge-case coverage as follow-up work
- **Full signed record:** `git show ffc11ef:HUMAN_REVIEW.md`.
