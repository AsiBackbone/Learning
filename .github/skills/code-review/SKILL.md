---
name: code-review
description: Review Learning documentation, samples, links, release records, and cross-repository references. Use this when reviewing pull requests, drafting or revising Learning content, checking historical versus current guidance, or validating alignment with released AsiBackbone and NCAT documentation.
---

# Learning Documentation Review

Use this skill to review or modify AsiBackbone Learning content without losing the repository's educational ownership, version boundaries, historical evidence, link safety, and cross-repository contracts.

## Repository role

Treat Learning as the organization repository for reusable architecture education.

Learning owns:

- architecture education and conceptual explanation;
- tutorials, labs, and minimal teaching examples;
- terminology explanation and lineage;
- comparisons, tradeoffs, and alternatives;
- diagrams and framework-neutral teaching models;
- general secure-by-default and governed-execution learning material.

Learning does not override concrete product/runtime truth.

Use the owning repository for version-specific contracts:

- AsiBackbone owns package installation/configuration, public APIs, runtime semantics, integration boundaries, security implementation posture, migrations, and releases.
- NetCoreApplicationTemplate (NCAT) owns template installation/options, generated structure, configuration defaults, middleware ordering, runtime behavior, ADRs, and generated contracts.

When a concrete implementation already has an authoritative source, explain the architectural lesson in Learning and link to that source instead of duplicating the contract.

## Identify the version boundary before editing

Before changing a page, determine whether it is:

- current Learning guidance;
- a historical Learning release record;
- an immutable release or evidence artifact;
- a current article that links to versioned implementation evidence.

For the current repository state:

- Learning 1.1 aligns with AsiBackbone 7.0;
- Learning 1.0 / AsiBackbone 6.0 is historical.

Do not silently rewrite historical 1.0 / 6.0 material as though it originally targeted 7.0.

When a historical page describes what was current at the time, preserve that fact but time-scope the wording so it cannot be mistaken for the repository's current baseline.

## Preserve immutable historical references

Historical cross-repository evidence should be reproducible.

For historical Learning guidance:

- pin AsiBackbone references to the matching release tag or exact reviewed commit;
- pin Learning historical source references to the matching Learning tag when appropriate;
- avoid mutable `main` URLs when the reference is evidence for a historical release;
- avoid using the live Learning site as historical evidence when the live site has advanced to a newer baseline.

Treat published or attested release-note/evidence artifacts as immutable unless the repository's release process explicitly permits modifying them.

If stale wording exists around an immutable artifact, prefer correcting the surrounding historical guidance rather than rewriting the artifact.

Do not replace an immutable tag or commit link with `main` merely because `main` currently contains similar content.

## Check terminology in context

Distinguish among:

- conceptual teaching terminology;
- current package/API names;
- historical names intentionally preserved for migration or explanation;
- compatibility, protocol, persistence, telemetry, or serialized names that may remain valid after CLR/public terminology changes.

Do not flag every older term as stale until its version and contract context are clear.

When Learning names a concrete AsiBackbone API, verify it against the matching released AsiBackbone baseline rather than assuming current `main` applies to a historical page.

Keep teaching examples framework-neutral unless the page explicitly documents a concrete product integration.

## Preserve the educational model

Prefer teaching the problem before the product.

Where practical, keep the Learning sequence recognizable:

1. problem;
2. common or naive approach;
3. failure mode or limitation;
4. architectural pattern;
5. minimal example;
6. tradeoffs and alternatives;
7. working repository example.

Do not expand a page into package installation or implementation-reference documentation when the owning product repository already provides that material.

Keep examples small enough that the architectural lesson remains visible.

When architectural status matters, preserve the repository's established pattern classifications and do not treat them as quality rankings.

## Respect documentation ownership

Before substantially expanding a topic, classify its owner:

- Learning = reusable/general education;
- AsiBackbone = concrete governance package/API/runtime/integration/security behavior;
- NCAT = concrete template/runtime/generated behavior.

If a detail belongs elsewhere:

- explain only the concept needed for the Learning lesson;
- link directly to the authoritative implementation or documentation;
- avoid making Learning the sole source for a concrete configuration key, runtime behavior, generated contract, migration rule, or package guarantee.

Cross-repository alignment does not mean the repositories should duplicate the same text.

## Protect links, navigation, and published paths

When changing headings, page paths, navigation, or links:

- preserve published article URLs;
- do not rename or move a published year/slug article unless an explicit compatibility mechanism is provided;
- update inbound links when a heading anchor changes;
- preserve established navigation paths when practical;
- use immutable release links for historical evidence;
- do not replace tagged historical links with mutable `main`.

For organization-owned GitHub links, keep repository validation in mind: tagged and branch references are checked against real repository objects.

Avoid circular cross-repository navigation in which each page sends the reader back to the other as its sole authoritative destination.

## Preserve safe and qualified examples

Learning examples must not imply:

- legal or regulatory compliance;
- certification;
- unrestricted AI execution authority;
- guaranteed security;
- that Learning itself is an AI model, AGI/ASI implementation, or robotics controller.

For governance and AI material, preserve the host-execution boundary:

> The model may propose. The host retains execution authority.

Do not put real secrets, credentials, private keys, tokens, production connection strings, confidential data, or personal data into examples, screenshots, tests, or documentation.

When a concrete repository is used as a working example, distinguish the general pattern from that implementation's specific choices.

## Validate the changed surface

Use the repository-prescribed checks in `CONTRIBUTING.md`.

For documentation changes, restore repository tools as needed and build DocFX with warnings treated as errors:

```bash
dotnet tool restore
dotnet tool run docfx docs/docfx.json --warningsAsErrors
```

When article metadata or RSS behavior changes, run the repository's feed and metadata validation described in `CONTRIBUTING.md`.

When organization-owned links change, run the repository's organization-link validation where practical. When headings or external links change, account for the link-validation workflow and anchor checks.

When samples change, run the corresponding sample validation/tests in Release configuration. The repository's always-on Copilot instruction also prefers:

```text
dotnet test --configuration Release
```

Do not claim that a test, DocFX build, metadata check, link check, or CI gate passed unless it was actually run or supported by authoritative CI evidence.

## Prioritize meaningful review findings

Prioritize findings involving:

- incorrect current-versus-historical framing;
- broken documentation ownership;
- stale mutable links in historical records;
- package/API claims not supported by the owning repository;
- mutation of published or attested release evidence;
- broken navigation, anchors, or published URLs;
- unsafe or overbroad examples;
- samples that contradict the prose;
- missing tradeoffs or misleading canonical/alternative framing;
- accessibility regressions that make the lesson unavailable without a visual presentation.

Avoid low-value editorial churn when the existing wording is already accurate and clear.

When reporting a finding, explain the concrete reader, compatibility, historical, or cross-repository consequence that makes the change important.
