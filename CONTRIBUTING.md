# Contributing to ASI Backbone Learning

Thank you for your interest in contributing to `AsiBackbone/Learning`.

This repository is intended to be a community-maintained living tutorial for practical .NET architecture, governed execution, policy-driven systems, secure application design, AI integration, and related architectural patterns.

Contributions do not need to be large to be useful. A clearer explanation, corrected example, improved diagram, new lab, alternative implementation, or well-reasoned architecture critique can all materially improve the project.

## Project Philosophy

A core principle of this repository is:

> **Read it. Run it. Question it. Improve it.**

The goal is not to enforce one architectural doctrine.

The goal is to help developers understand problems, compare approaches, study tradeoffs, and connect architectural ideas to working implementations.

Where appropriate, tutorials should remain useful even to readers who never install the `AsiBackbone` package or use `NetCoreApplicationTemplate`.

## Ways to Contribute

Contributions may include:

- New tutorials
- Corrections and clarifications
- Minimal code examples
- Architecture diagrams
- Hands-on labs
- Alternative implementations
- Failure-mode analysis
- Security or governance examples
- ASP.NET Core examples
- AI integration examples
- Architecture critiques
- Documentation improvements
- Accessibility improvements
- Cross-links to relevant implementation examples
- Requests for new learning topics
- Tests for tutorial or lab code
- Fixes for outdated examples
- Improvements to DocFX navigation or presentation

Small contributions are welcome.

## Before You Start

For a small correction, typo, broken link, or narrow improvement, opening a pull request directly is usually appropriate.

For larger work, such as:

- a new tutorial,
- a new architectural pattern,
- a substantial lab,
- a major documentation restructuring,
- a new category of examples,
- or a proposal that changes the project's learning model,

please consider opening an Issue or Discussion first.

This helps avoid duplicated work and gives contributors an opportunity to refine the idea before investing significant effort.

## Issues vs. Discussions

Use **Issues** for concrete work such as:

- Broken documentation
- Incorrect examples
- Missing tests
- Tutorial defects
- Requested implementation work
- Outdated links
- DocFX problems
- Clearly scoped enhancements

Use [**ASI Backbone Organization Discussions**](https://github.com/orgs/AsiBackbone/discussions) for broader or exploratory topics such as:

- Architecture questions
- Tutorial proposals
- Alternative patterns
- Design debates
- "How would you model this?" questions
- Community learning ideas
- Topics that may eventually become tutorials or labs

A useful flow is:

```text
Question
   ↓
Discussion
   ↓
Experiment or competing approaches
   ↓
Tutorial or documentation contribution
   ↓
Working example
   ↓
Feedback
   ↺
```

## Contribution Principles

### 1. Teach the Problem Before the Product

Whenever practical, begin with the architectural problem rather than a package API.

A tutorial should explain why the problem exists before showing how a specific implementation addresses it.

Prefer:

```text
Problem
   ↓
Common or naive implementation
   ↓
Failure mode or limitation
   ↓
Architectural pattern
   ↓
Minimal example
   ↓
Tradeoffs and alternatives
   ↓
Working repository example
```

over documentation that begins and ends with package installation instructions.

### 2. Keep Teaching Examples Small

The full `AsiBackbone` and `NetCoreApplicationTemplate` repositories already contain realistic complexity.

Examples in this repository should be intentionally small enough that the architectural lesson remains visible.

Avoid adding infrastructure or abstractions that are not necessary to teach the concept.

### 3. Separate Pattern From Implementation

Where useful, distinguish among:

- **Architecture Pattern** — the general idea
- **Minimal Teaching Example** — the simplified demonstration
- **Working Repository Example** — the fuller implementation in another ASI Backbone repository

Do not duplicate large portions of canonical implementation documentation when a direct reference will serve better.

### 4. Explain Tradeoffs

Strong architectural documentation should explain when a pattern helps and when it may not.

Where appropriate, include:

- Benefits
- Costs
- Complexity
- Failure modes
- Alternatives
- Cases where the pattern is unnecessary

### 5. Alternatives Are Welcome

Architectural disagreement can be educational.

Contributions may present approaches that differ from the current ASI Backbone implementation when those approaches are:

- Technically grounded
- Clearly explained
- Presented in good faith
- Explicit about tradeoffs
- Clearly identified as alternatives where necessary

Possible classifications include:

**Canonical Pattern**  
Aligned with the current architecture of one or more ASI Backbone organization repositories.

**Alternative Pattern**  
A different approach that addresses the same problem and is included for comparison or learning.

An alternative does not need to be treated as incorrect merely because it differs from the canonical implementation.

### 6. Preserve Project Boundaries

Do not present the Learning repository as:

- A compliance certification
- A legal standard
- A security guarantee
- An AI model
- An AGI or ASI implementation
- A robotics controller
- A replacement for application-specific security review

Examples should not imply that using a demonstrated pattern automatically satisfies legal, regulatory, safety, or compliance obligations.

### 7. Keep Execution Ownership Clear

For governance and AI-related examples, preserve a clear distinction between proposed intent, governance decisions, authority, and execution.

A useful design principle is:

> **The model may propose. The host retains execution authority.**

Do not describe the learning examples as granting unrestricted execution authority to AI systems.

## Tutorial Guidelines

A strong tutorial will usually contain some combination of the following:

### Problem

Describe the architectural problem in practical terms.

### Why It Matters

Explain the consequences of solving the problem poorly.

### Common or Naive Approach

Show a recognizable implementation that makes the problem easy to understand.

### Failure Mode or Limitation

Explain why the simpler approach may be insufficient.

### Architectural Pattern

Introduce the pattern being taught.

### Minimal Example

Provide the smallest practical implementation that demonstrates the pattern.

### Walkthrough

Explain how the example works.

### Tradeoffs

Describe costs, limitations, and alternatives.

### When Not to Use It

Identify situations where the pattern may be unnecessary or inappropriate.

### Working Example

Link to the relevant `AsiBackbone` or `NetCoreApplicationTemplate` implementation when one exists.

### Further Questions

Where useful, leave readers with unresolved questions or possible experiments.

Not every tutorial must contain every section, but contributions should prioritize clarity and learning value.

## Lab Guidelines

Labs should encourage active reasoning rather than simply repeating tutorial steps.

A lab may include:

- A partially implemented application
- A deliberately weak architecture to improve
- Failing tests
- A policy-design problem
- A security-boundary exercise
- An AI tool-execution scenario
- A decision-pipeline exercise
- A debugging or review task

A good lab should clearly state:

- The learning objective
- Starting conditions
- Constraints
- Expected outcome
- How to validate the result

Where solutions are included, consider keeping them separate from the exercise so learners can attempt the problem first.

## Code Contributions

For code examples:

- Prefer clear, idiomatic C# and .NET patterns.
- Keep dependencies minimal unless a dependency is central to the lesson.
- Avoid unnecessary abstractions.
- Use meaningful names.
- Prefer examples that compile and can be tested.
- Add tests when the lesson depends on behavioral correctness.
- Avoid embedding real credentials, secrets, tokens, connection strings, or personally identifiable information.
- Use obviously fictional or placeholder values where examples require identifiers or sensitive-looking data.

Code should optimize for understanding first, while still modeling responsible engineering practice.

## Documentation Style

Documentation should generally be:

- Clear
- Direct
- Technically grounded
- Accessible to developers at different experience levels
- Explicit about assumptions
- Honest about tradeoffs
- Careful about overclaiming

Avoid unnecessary promotional language.

Prefer statements that explain what a pattern **does** over statements that declare it universally superior.

## Building Documentation Locally

The Learning documentation site is generated with **DocFX**.

The repository pins the DocFX version through `.config/dotnet-tools.json`, so contributors should use the repository-local .NET tool manifest rather than relying on a separately installed global DocFX version.

To match the repository's documentation-validation workflow as closely as practical, use the **.NET 10 SDK**.

From the repository root, restore the pinned tools:

```bash
dotnet tool restore
```

Build the documentation using the same strict validation used by CI:

```bash
dotnet tool run docfx docs/docfx.json --warningsAsErrors
```

A successful build generates the static documentation site under:

```text
docs/_site/
```

Warnings are treated as errors intentionally. Contributors should resolve DocFX warnings rather than relying on CI to accept a locally warning-producing build.

## Preview the Documentation Locally

After building the site, start the DocFX local server with:

```bash
dotnet tool run docfx serve docs/_site
```

Then open:

```text
http://localhost:8080
```

Stop the local server with `Ctrl+C`.

For a combined build-and-preview workflow, you may also run:

```bash
dotnet tool run docfx docs/docfx.json --warningsAsErrors --serve
```

When documentation content, navigation, links, diagrams, or DocFX configuration changes, review the rendered site locally when practical before opening a pull request.

Before submitting documentation changes, rerun:

```bash
dotnet tool run docfx docs/docfx.json --warningsAsErrors
```

This is the closest local equivalent to the repository's required **Build DocFX documentation** validation check.

## Diagrams

Diagrams are encouraged when they make an architectural boundary or sequence easier to understand.

Useful diagram types include:

- Request-flow diagrams
- Sequence diagrams
- Trust-boundary diagrams
- Policy evaluation flows
- Capability-grant flows
- AI tool-execution gateways
- Middleware pipelines
- C4-style architecture diagrams

Mermaid is preferred when practical because diagrams remain reviewable as text.

Image-based diagrams are also acceptable when they provide clear value.

## Accessibility

Accessibility is part of documentation quality.

As the Learning repository adds diagrams, images, tables, media, custom
styling, and interactive content, contributors should preserve an
equivalent learning path for readers who use assistive technology,
keyboard navigation, zoom, high-contrast settings, or other
accessibility features.

When contributing documentation:

- Provide meaningful alternative text for informative images.
- Use empty alternative text only for images that are truly decorative.
- Accompany important diagrams with surrounding prose that explains the
  sequence, relationships, boundaries, or conclusions being illustrated.
- Do not rely on color, shape, position, or animation alone to
  communicate meaning.
- Prefer ordinary text over images of text when practical.
- Use descriptive headings and meaningful link text.
- Keep heading levels logically structured rather than choosing them
  only for visual appearance.
- Use tables for genuinely tabular information rather than page
  only for visual appearance.
- Use tables for genuinely tabular information rather than page layout,
  and keep table structures as simple as practical.
- Ensure code examples and architectural flows remain understandable
  without depending on syntax color or visual position alone.
- Review custom colors and styling for adequate contrast.
- Review custom layouts at increased zoom and narrow viewport widths.
- Check both light and dark presentation when custom styling is
  introduced and both modes are supported.
- Ensure custom interactive content can be operated with a keyboard,
  preserves visible focus, and does not trap keyboard focus.
- Provide appropriate captions, transcripts, or equivalent alternatives
  if audio or video content is introduced.

Mermaid and text-based diagrams remain useful because their source is
reviewable as text, but diagram source should not be treated as a
substitute for a clear prose explanation.

A reader who cannot perceive a diagram should still be able to
understand the architectural lesson from the surrounding material.

For substantial new diagrams, media, interactive components, or theme
customizations, additional automated or manual accessibility testing
may be appropriate.

## Links to Working Repositories

When a tutorial maps to an existing implementation, link directly to the most relevant file, folder, documentation page, or ADR.

Primary implementation repositories include:

- [AsiBackbone](https://github.com/AsiBackbone/AsiBackbone)
- [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

Avoid copying large implementation sections into Learning when a maintained source already exists elsewhere.

## Branches

For contributed work, use a short descriptive branch name when practical.

Examples:

```text
tutorial/policy-context
tutorial/decision-before-execution
lab/ai-tool-gateway
docs/improve-contribution-guide
fix/broken-learning-link
diagram/capability-flow
```

Exact naming is not mandatory, but descriptive names make review easier.

## Commit Messages

Prefer concise commit messages that describe the change.

Examples:

```text
docs: add policy context tutorial
docs: clarify host-owned execution boundary
feat: add beginner decision pipeline lab
fix: correct capability token example
test: add validation for tutorial sample
```

Large contributions may use multiple focused commits when that improves reviewability.

## Pull Requests

A pull request should explain:

- What changed
- Why the change is useful
- Which learning area it affects
- How the contribution was validated
- Whether it represents a canonical or alternative pattern when that distinction matters

For code-based tutorials or labs, include relevant build or test results.

For diagrams or documentation-only contributions, describe how you verified links, rendering, or navigation when applicable.

Keep pull requests focused where practical.

A focused tutorial improvement is easier to review than a large unrelated collection of changes.

## Suggested Pull Request Summary

A simple pull request description may use:

```markdown
## Summary

Briefly explain what this contribution adds or changes.

## Learning Value

Explain what readers should understand or be able to do after this change.

## Changes

- Change one
- Change two
- Change three

## Validation

Describe how the documentation, example, lab, or code was checked.

## Pattern Classification

- [ ] Canonical Pattern
- [ ] Alternative Pattern
- [ ] General Learning Material
- [ ] Not applicable
```

This structure is optional unless a future repository template makes it mandatory.

## Review Expectations

Review may consider:

- Technical correctness
- Learning value
- Clarity
- Scope
- Maintainability
- Consistency with project boundaries
- Whether claims are appropriately qualified
- Whether alternatives and tradeoffs are represented fairly
- Whether the example duplicates material that belongs in another repository
- Whether code or diagrams can be simplified

Review comments should focus on improving the contribution rather than proving one person right.

## Changes Requested During Review

Contributors should expect that tutorials may require revision.

Educational material often needs refinement even when the underlying code is technically correct.

Requests may include:

- Simplifying an example
- Clarifying assumptions
- Adding a tradeoff section
- Correcting overbroad claims
- Separating canonical and alternative patterns
- Linking to the real implementation
- Adding validation or tests
- Improving accessibility or readability

These requests are part of maintaining a useful learning resource.

## Security

Do not report security vulnerabilities through a public Issue when disclosure could create risk.

Follow the security reporting guidance provided by the repository or ASI Backbone organization when available.

Never include:

- Real passwords
- API keys
- Signing keys
- Access tokens
- Private certificates
- Production connection strings
- Confidential organization information
- Personal data

in tutorial examples, commits, Issues, Discussions, or pull requests.

## Contribution Licensing

Contributions are accepted under the license applicable to the
material being modified:

- Documentation and educational content: CC BY 4.0
- Executable sample code: MIT
- Code snippets included in documentation: additionally available under MIT

Contributors must have the right to submit contributed material under
the applicable license.

## Code of Conduct

All participation is subject to the repository's [Code of Conduct](CODE_OF_CONDUCT.md).

Technical disagreement, skepticism, and architecture criticism are welcome.

Harassment, personal attacks, intimidation, discrimination, or deliberate disruption are not.

## Recognition

Contributions of all sizes are valued.

The project may recognize contributors through Git history, release notes, contributor listings, documentation acknowledgments, or other appropriate mechanisms as the repository evolves.

## Questions

If you are unsure whether an idea belongs in the repository, start an [ASI Backbone Organization Discussion](https://github.com/orgs/AsiBackbone/discussions).

If you have identified a concrete problem, open an Issue.

If you already have a focused improvement, a pull request is welcome.

The project is intended to evolve through exactly this kind of participation.

---

**Read it. Run it. Question it. Improve it.**
