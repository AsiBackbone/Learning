# Governance

## Purpose

`AsiBackbone/Learning` is a community-maintained living tutorial for practical .NET architecture, governed execution, policy-driven systems, secure application design, AI integration, and related architectural patterns.

This document explains how the repository is maintained, how decisions are made, how contributions are reviewed, and how disagreements are handled.

The goal is to keep governance lightweight enough for a learning project while still making expectations clear.

## Governance Principles

The project is guided by several principles.

### 1. Learning Value Comes First

Changes should improve the repository as a learning resource.

A contribution does not need to make the project larger to make it better. Clarity, correctness, focus, useful examples, and well-explained tradeoffs are more important than feature count.

### 2. Working Implementations Remain Canonical in Their Own Repositories

`AsiBackbone/Learning` explains architectural ideas and demonstrates them through focused examples.

The primary implementation repositories remain:

- [AsiBackbone](https://github.com/AsiBackbone/AsiBackbone)
- [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

When Learning material refers to behavior implemented elsewhere, the working repository should be treated as the authoritative implementation source unless the tutorial explicitly presents an alternative pattern.

### 3. The Project Is Not Architectural Doctrine

The repository may document preferred or canonical patterns, but it should not imply that one design is universally correct.

Well-reasoned alternative approaches are welcome.

Architectural disagreement should produce better explanations, clearer tradeoffs, and stronger examples.

### 4. Project Boundaries Matter

The Learning repository is an educational resource.

It is not:

- A compliance certification
- A legal standard
- A security guarantee
- An AI model
- An AGI or ASI implementation
- A robotics controller
- A substitute for application-specific security, legal, regulatory, or operational review

Governance decisions should preserve these boundaries and avoid overclaiming.

### 5. Host-Owned Execution Remains a Core Boundary

For governance and AI-related material, the repository should preserve a clear separation between:

```text
Intent
   ↓
Context
   ↓
Constraints
   ↓
Decision
   ↓
Acknowledgment when required
   ↓
Scoped authority
   ↓
Host-owned execution
   ↓
Audit residue
```

A useful design principle is:

> **The model may propose. The host retains execution authority.**

Contributions that intentionally explore a different model may do so, but should clearly identify the approach as an alternative and explain its risks and tradeoffs.

## Current Maintainer Model

The project currently uses a **maintainer-led governance model**.

The repository owner and designated maintainers are responsible for:

- Reviewing and merging pull requests
- Maintaining repository structure
- Resolving scope questions
- Managing releases and documentation publishing
- Maintaining contribution standards
- Moderating repository spaces
- Protecting project boundaries
- Determining when a pattern is presented as canonical, alternative, experimental, or deprecated

As the contributor community grows, this model may evolve.

Future governance may introduce additional maintainers, reviewers, topic owners, or other shared responsibilities when there is sustained participation and a practical need for them.

## Decision Making

Most repository decisions should be made through normal GitHub collaboration.

A typical decision path is:

```text
Question or proposal
   ↓
Issue or Discussion
   ↓
Community feedback
   ↓
Experiment, draft, or pull request
   ↓
Review
   ↓
Maintainer decision
   ↓
Merge, revise, defer, or decline
```

Not every change requires a formal Discussion.

Small corrections and narrowly scoped improvements may proceed directly through pull requests.

Larger architectural or editorial changes should generally receive broader discussion first.

## Types of Decisions

### Routine Decisions

Routine decisions include:

- Typo corrections
- Broken-link fixes
- Small wording improvements
- Minor example corrections
- Documentation navigation fixes
- Test corrections
- Noncontroversial maintenance

These may be approved directly by a maintainer after normal review.

### Significant Decisions

Significant decisions include:

- New learning domains
- Major repository restructuring
- New canonical architectural patterns
- Changes to contribution policy
- Changes to governance policy
- Changes to licensing
- Changes to project scope
- Removal or deprecation of major tutorial areas
- Changes that materially alter how the project relates to other ASI Backbone repositories

These should normally be discussed publicly before implementation.

### Sensitive Decisions

Sensitive decisions include:

- Security-related disclosures
- Code of Conduct enforcement
- Handling private or confidential information
- Legal or licensing concerns
- Actions involving contributor safety or privacy

These may require private maintainer handling and should not be forced into public discussion.

## Canonical, Alternative, Experimental, and Deprecated Material

Learning content may be classified to help readers understand its status.

### Canonical Pattern

A pattern aligned with the current documented architecture of one or more ASI Backbone organization repositories.

Canonical does not mean universally correct.

It means that the pattern represents the organization's current preferred or implemented approach for the scenario being taught.

### Alternative Pattern

A technically grounded approach that differs from the canonical implementation.

Alternative patterns are welcome when they improve understanding by showing different tradeoffs or design choices.

### Experimental Material

Material that is still being explored and should not yet be treated as established project guidance.

Experimental content should clearly identify:

- What is being tested
- What assumptions are being made
- What remains unresolved
- What evidence would strengthen or weaken the approach

### Deprecated Material

Material that is no longer recommended or no longer matches current implementations.

Deprecated material may be retained when it has historical or educational value, but it should be clearly marked and should point readers toward the current approach.

## How Canonical Status Is Determined

A tutorial should not become canonical solely because it was merged first.

Canonical status should generally reflect one or more of the following:

- Alignment with a working implementation
- Alignment with an accepted Architecture Decision Record
- Repeated successful use in the project
- Strong technical justification
- Clear documentation of tradeoffs
- Maintainer approval after appropriate review

When reasonable contributors disagree, the repository may preserve both canonical and alternative approaches rather than forcing a false consensus.

## Relationship to Architecture Decision Records

Architecture Decision Records in the implementation repositories document why specific technical decisions were made.

Learning material should use ADRs as evidence of implementation intent when relevant.

The expected relationship is:

```text
Architecture question
   ↓
ADR or implementation decision
   ↓
Working repository implementation
   ↓
Learning explanation
   ↓
Community feedback and alternatives
```

Learning documentation should not silently rewrite or reinterpret an ADR.

If a tutorial critiques or proposes an alternative to an existing ADR, it should say so explicitly.

## Contribution Review

Pull requests are reviewed for more than technical correctness.

Review may consider:

- Learning value
- Accuracy
- Clarity
- Scope
- Maintainability
- Accessibility
- Appropriate qualification of claims
- Alignment with project boundaries
- Whether tradeoffs are adequately explained
- Whether an example is unnecessarily complex
- Whether content duplicates canonical documentation elsewhere
- Whether links to working implementations are appropriate
- Whether material should be classified as canonical, alternative, experimental, or deprecated

A technically correct contribution may still require revision if it obscures the lesson or creates unnecessary complexity.

## Merge Authority

Only repository maintainers with appropriate GitHub permissions may merge pull requests.

Maintainers may:

- Approve and merge
- Request changes
- Defer pending more evidence or discussion
- Close a proposal that falls outside project scope
- Recommend moving discussion to another repository
- Reclassify a proposal as experimental or alternative

Where practical, maintainers should explain significant decisions.

## Maintainer Expectations

Maintainers are expected to:

- Apply project standards consistently
- Separate technical disagreement from personal disagreement
- Encourage evidence-based discussion
- Avoid overstating project claims
- Protect contributor safety and privacy
- Preserve repository scope
- Give contributors actionable review feedback
- Credit contributors appropriately
- Revisit prior decisions when new evidence justifies doing so

Maintainer status does not make an architectural opinion immune from criticism.

## Adding Maintainers

Additional maintainers may be added when a contributor demonstrates sustained, constructive participation.

Signals may include:

- Consistent high-quality contributions
- Thoughtful code or documentation review
- Reliable technical judgment
- Respectful participation in disagreements
- Understanding of project scope and boundaries
- Willingness to maintain existing material, not only add new material
- Responsible handling of security or community concerns

Maintainer access should reflect demonstrated responsibility rather than contribution count alone.

## Stepping Down or Removing Maintainer Access

A maintainer may step down at any time.

Maintainer access may also be reduced or removed when necessary due to:

- Extended inactivity where access is no longer needed
- Repeated failure to follow project governance
- Security concerns
- Abuse of repository permissions
- Serious Code of Conduct violations
- Conflicts of interest that materially affect project stewardship

Where appropriate and safe, such changes should be documented.

## Issues, Discussions, and Pull Requests

### Issues

Issues are the preferred place for concrete work.

Examples include:

- Documentation defects
- Broken tutorials
- Incorrect code
- Missing tests
- Requested examples
- DocFX problems
- Scoped enhancements

### Discussions

Discussions are preferred for exploratory topics.

Examples include:

- Architecture questions
- New tutorial ideas
- Alternative design proposals
- Tradeoff debates
- Requests for community input
- Questions that may lead to future tutorials

### Pull Requests

Pull requests should contain focused, reviewable changes.

For substantial architectural material, the pull request should reference the relevant Issue or Discussion when one exists.

## Disagreement and Appeals

Technical disagreement is expected.

Contributors should first attempt to resolve disagreements through:

- Clear explanation
- Code examples
- Tests
- Measurements
- Documentation
- ADR references
- Security reasoning
- Reproducible demonstrations
- Explicit tradeoff analysis

When agreement is not possible, a maintainer may decide to:

- Choose one approach as canonical
- Publish multiple approaches
- Mark material experimental
- Defer the decision
- Decline the proposal

A contributor who disagrees with a decision may request reconsideration by providing materially new information, evidence, or reasoning.

Repeatedly reopening the same decision without new information is not productive.

## Moderation

Community behavior is governed by [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

Moderation decisions are separate from architectural decisions.

A contributor should not be moderated merely for:

- Disagreeing with a maintainer
- Criticizing a canonical pattern
- Proposing an alternative architecture
- Questioning project assumptions
- Identifying limitations or failures

Moderation applies to behavior, not viewpoint.

## Security and Private Matters

Security vulnerabilities or sensitive disclosures should not be handled in public Issues when disclosure could create risk.

Maintainers may use private communication channels for:

- Vulnerability reports
- Credential exposure
- Personal safety concerns
- Code of Conduct reports
- Confidential licensing concerns
- Other sensitive matters

Public documentation may be added later when disclosure is safe and useful.

## Licensing and Intellectual Property

The repository is distributed under the MIT License unless explicitly stated otherwise for a particular artifact.

Contributors must have the right to submit their contributions.

Maintainers may reject content when licensing or provenance is unclear.

Third-party material should be attributed and used only when its license is compatible with the repository.

## Changes to Governance

This governance document is itself subject to change.

Minor clarifications may be handled through normal pull requests.

Material changes to governance should normally be discussed before merge.

Examples include:

- Changing the maintainer model
- Introducing voting
- Establishing topic ownership
- Creating a steering group
- Changing merge authority
- Changing contribution rights
- Changing licensing
- Changing moderation structure

Governance should grow only when the community requires additional structure.

Complex governance should not be introduced merely for appearance.

## Project Continuity

The project should remain understandable and maintainable even if individual contributors become unavailable.

To support continuity:

- Important decisions should be documented
- Tutorials should link to authoritative implementation sources
- Repository automation should be reviewable
- Contribution expectations should remain explicit
- Canonical and alternative material should be clearly distinguished
- Maintainer responsibilities should not depend on undocumented personal knowledge

## Governance Philosophy

The Learning project exists to improve understanding through implementation, criticism, experimentation, and shared reasoning.

Its governance should support that goal without becoming heavier than the project itself.

A concise statement of the project's approach is:

> **Maintain the boundaries. Document the reasoning. Welcome the challenge. Improve the pattern.**

And the broader Learning principle remains:

> **Read it. Run it. Question it. Improve it.**
