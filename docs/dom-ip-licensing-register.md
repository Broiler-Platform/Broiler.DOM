# Broiler.DOM IP, Licensing, And Standards Register

**Register version:** 1.0
**Updated:** 2026-09-03 (evidence assembled; no row is decided and no legal review is claimed)
**Owner:** Broiler.DOM maintainers
**Approval authority:** Maik Ratzmer (GitHub [MaiRat](https://github.com/MaiRat)), project reviewer

**Governance.** This register follows the discipline first written down as
`Broiler.Documents` ADR 0013: standards acquisition, patent evidence,
implementation provenance, source records, approved wording, and a written
decision per row, with each component's rights recorded where its code lives.
That the discipline is stated in a repository which *depends on this one* is
backwards, and is noted rather than tidied over: if the platform ever wants a
single governance document it belongs above both, not in either.

---

## ⚠ NO LAWYER HAS REVIEWED ANY OF THIS, AND NO ROW HERE IS DECIDED YET

The standard of review is an engineer reading published evidence. No legal
opinion was sought, none was given, and none is represented. **No
freedom-to-operate determination has been made** and patent-freedom is not
claimed for HTML or the DOM.

The evidence was gathered by Claude (Anthropic's coding agent, engineering seat)
on 2026-09-03 at the maintainer's direction. **Assembling an evidence record is
not approving it.** Every row is `Pending` or a recorded inspection finding
awaiting sign-off.

## Why this component needed a register of its own

`Broiler.Documents`' HTML register records that it does not parse HTML — this
component does — and stops at its own mapping layer. Everything with real
implementation surface is here: a tokenizer, a tree builder, and the DOM
algorithms the roadmap tracks against the standard's own text.

That matters for one row in particular. A tokenizer is exactly where a
specification's **normative tables** live if a project transcribes them, and the
HTML Standard has the largest one anybody copies: roughly two and a quarter
thousand named character references. Whether this repository contains it is the
question this register was worth opening for, and DOM-IP-003 answers it.

## Decision fields

Implementation jurisdictions and expiry/review dates are unrecorded on every row
and are not blocking under this register's standard.

| ID | Technology / exact scope | Primary evidence | Current assessment | Status / required action |
|---|---|---|---|---|
| DOM-IP-001 | The DOM and HTML algorithms this component implements: `Broiler.Dom`'s node, attribute, mutation, range and traversal primitives, and `Broiler.Dom.Html`'s tokenization, parsing, tree building and serialization | The [WHATWG IPR Policy](https://whatwg.org/ipr-policy); the [W3C Patent Policy](https://www.w3.org/policies/patent-policy/); the DOM and HTML Living Standards | Both bodies that have stewarded these standards operate **royalty-free** patent policies. WHATWG requires a licence to Essential Patent Claims that "may not be conditioned on payment of royalties, fees, or other consideration"; W3C requires an RF licence "available to all, worldwide" on the same terms. No implementation royalty is identified from any source. **What neither establishes.** Both bind *participants* over *Essential Claims*, and both let a participant **exclude specific patents** during a disclosure window — so they are a commitment mechanism rather than a finding that no relevant claim exists, and they reach nobody who never participated. Reciprocity and defensive suspension are permitted by both. Risk assessed **very low**. | **Pending the project reviewer's decision.** Evidence complete and primary-sourced from both policies. A decision should record that the exclusion mechanism is what stops this being a patent-freedom statement. |
| DOM-IP-002 | Acquiring, quoting, and reproducing the specification text | The WHATWG IPR Policy's copyright terms: Living Standards are published under **CC BY 4.0**, and "when portions of Living Standards are incorporated into source code, they are licensed under the **BSD 3-Clause License**" | **This is the row that is unlike every other acquisition row in the platform, and the difference is worth stating plainly.** For PDF, SRC-001 had to close by establishing that no ISO text was committed anywhere, because ISO grants nothing. For ODT, the OASIS notice permits explanatory copying and the row still declined to rely on it. Here there is an **affirmative licence for exactly this use**: incorporating specification text into source code is licensed, under BSD-3, by the body that publishes it. The transcription question that gates the fax decoder under SRC-017 has, for HTML, a published answer. **It is nevertheless not relied on**, because DOM-IP-003 and DOM-IP-004 find nothing incorporated. Both licences require attribution, so the moment anything is, a notice obligation attaches — the same shape as IP-013's Unicode notice, carried forward rather than discharged. Risk assessed **very low**. | **Inspection finding recorded 2026-09-03; awaiting sign-off.** Incorporating specification text would not reopen the *permission* question — it is answered — but would create the attribution obligation, which belongs with release notices rather than with this source. |
| DOM-IP-003 | The named character reference table: the roughly 2,231 entries the HTML Standard defines for `&amp;`-style references | `Broiler.Dom.Html/HtmlTokenizer.cs`; `Broiler.Dom.Html/HtmlSerializer.cs` | **The table is not here. It is the platform's.** `DecodeReferences` is one line that hands the value to `System.Net.WebUtility.HtmlDecode`, and the serializer's `Encode` hands its value to `WebUtility.HtmlEncode`; those two call sites are the whole of this component's involvement with named references. That is the same conclusion IP-014 reached for URI parsing and IP-023 for DEFLATE — consuming a platform API satisfies an implementation-provenance requirement precisely because there is nothing imported to account for — and it lands on the one artifact in HTML most likely to have been copied. Risk assessed **very low**. | **Inspection finding recorded 2026-09-03; awaiting sign-off.** A hand-written reference table appearing in this component would reopen this row, and `DomClaimGuardTests` fails the build if one does. |
| DOM-IP-004 | Implementation provenance: whether either library embeds third-party DOM or HTML code or data | Inspection of `Broiler.Dom` and `Broiler.Dom.Html` and the whole tracked tree; `DomClaimGuardTests` | Four findings, each repeatable. Neither library's directory contains **anything but `.cs` files and its `.csproj`** — no data file, table, or fixture. Neither takes **any package reference**; `Broiler.Dom` references nothing at all and `Broiler.Dom.Html` references only `Broiler.Dom`, so the component is dependency-free in the literal sense and no third-party parser is present to account for. **No `.html`, `.htm`, `.json`, or `.dat` file is tracked anywhere** in this repository. Test documents are constructed as strings in code. Risk assessed **very low**; the first three are guarded. | **Inspection finding recorded 2026-09-03; awaiting sign-off.** |
| DOM-IP-005 | Third-party conformance suites, and `html5lib-tests` in particular | Per-artifact origin, author, licence, and approval | Named specifically rather than left to the general rule, because it is the realistic case and the general rule is easy to satisfy by accident. `html5lib-tests` is the suite every HTML parser is measured against, it is distributed as `.dat` and `.json` files designed to be vendored, and vendoring it would be the single most likely way third-party material enters this component. **None is committed** — verified under DOM-IP-004 and guarded by extension. Importing it would reopen this row and require its licence to be read and recorded, which is a small piece of work and not an obstacle; what is not acceptable is importing it without doing that. | **Rejected by default**, per artifact. |
| DOM-IP-006 | The platform facilities both libraries are built on | `System.Net.WebUtility`, and the .NET runtime generally; the platform's own licence and notices | Neither library implements character-reference tables, text encoding, or collections of its own; it calls the runtime. A platform dependency rather than a bundled component, carrying no HTML-specific obligation — the obligation travels with the runtime's own notices, as it does for every framework API this component calls. Risk assessed **very low**. | **Inspection finding recorded 2026-09-03 under DOM-IP-004; awaiting sign-off.** |
| DOM-IP-007 | How this component may be described | The wording rules below | The negative rule, and for a parser it has a specific edge the format codecs do not. **Nothing describes this component as HTML5-conformant, standards-conformant, spec-compliant, certified, endorsed, or as a browser engine.** The last is the one that would mislead: this is a tokenizer and a tree builder, its own roadmap tracks algorithms it does not yet implement to the standard's text, and "browser engine" promises scripting, layout and rendering that are not here and are not planned here. The README's preview note already says the API may change; that is a stability statement and not a conformance one, and neither substitutes for the other. | **Pending the reviewer's approval of the wording rules.** |
| DOM-SRC-001 | The DOM and HTML Living Standards as sources consulted while writing this component | `Broiler.Dom` and `Broiler.Dom.Html`, in full; DOM-IP-002, DOM-IP-003 | Written against the published standards for this repository. The two halves hold separately: nothing was copied, per DOM-IP-003's finding about the one table worth copying and DOM-IP-004's about the tree; and nothing third-party was consulted for content, there being no other DOM or HTML implementation in the tree. Structural correspondence to the standards' algorithms is expected and is not evidence of copying — a tree builder that did not follow the standard's steps would build a different tree. | **Inspection finding recorded 2026-09-03; awaiting sign-off.** |

## Approved wording

**Proposed 2026-09-03 under DOM-IP-007. Not yet approved.**

This component is a library rather than a format, so what needs bounding is how
it is described rather than what a file dialog calls it.

| Context | Proposed wording |
|---|---|
| Component summary | **Canonical, dependency-free DOM and HTML parsing for .NET** |
| What it does | **HTML tokenization, document and fragment parsing, tree building, and serialization** |
| Technical documentation | **DOM and HTML, per the WHATWG Living Standards** |

Deliberately absent: **HTML5**, **spec-compliant**, **conformant**, **browser**,
**browser engine**, and **rendering** in any form.

## What still blocks a claim

| Blocker | Kind | State |
|---|---|---|
| DOM-IP-001 | Two royalty-free patent policies | **Pending a decision only.** Primary-sourced from both; the exclusion mechanism is what stops it being a patent-freedom claim |
| DOM-IP-002 to DOM-IP-004, DOM-IP-006, DOM-SRC-001 | What this repository contains and consulted | **Findings recorded, awaiting sign-off.** Three guarded mechanically |
| DOM-IP-005 | Third-party conformance suites | **Rejected by default.** Guarded by extension |
| DOM-IP-007 wording | How the component is described | **Pending approval.** The negative rule is the operative half and is enforced now |

## Review record

| Review | Reviewer | Date | Scope | Result |
|---|---|---|---|---|
| DOM and HTML evidence assembly and register creation | Claude (Anthropic coding agent, engineering seat), at the maintainer's direction — **not legal counsel, and not the approval authority** | 2026-09-03 | The WHATWG IPR Policy and W3C Patent Policy as published, including the copyright terms of Living Standards; inspection of both libraries, their tests, their project references, and every tracked file | **Evidence recorded; no row decided.** This register was opened because `Broiler.Documents`' HTML record explicitly could not cover it, and the question it existed to answer had a clean answer: the named character reference table — the largest normative artifact in HTML and the one a parser would plausibly transcribe — is not in this repository. Two call sites hand the work to `System.Net.WebUtility`, which is the same conclusion IP-014 reached about URIs and for the same reason. The finding worth carrying to other registers is DOM-IP-002's: WHATWG licenses its standards under CC BY 4.0, and BSD-3 where text is incorporated into source, so the transcription question that gates the PDF fax decoder under SRC-017 has a published answer here. This component does not need it, and knowing that it exists is worth more than the row it closes. |
