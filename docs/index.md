---
title: "Governed Execution & Secure .NET Architecture Tutorials"
description: Practical .NET architecture tutorials, labs, and reference patterns for governed execution, secure applications, AI integration, and policy-driven systems.
_disableAffix: true
_disableBreadcrumb: true
_disableToc: true
---

# AsiBackbone Learning

<div class="home-hero">
  <p class="home-kicker">Practical .NET architecture for accountable systems</p>
  <p class="home-summary">Learn governed execution, secure application design, policy-driven systems, and safe AI integration through focused tutorials, hands-on labs, and runnable examples.</p>
  <div class="home-actions" aria-label="Start learning">
    <a class="btn btn-primary" href="getting-started/index.md">Get Started</a>
    <a class="btn btn-outline-primary" href="getting-started/find-your-path.md">Find Your Path</a>
    <a class="btn btn-outline-primary" href="articles/index.md">Browse Articles</a>
  </div>
  <p class="home-principle">A proposed action should become a governed decision before it becomes real-world execution.</p>
</div>

AsiBackbone Learning explains architectural ideas, demonstrates them with focused examples, examines their tradeoffs, and connects the lessons to fuller working implementations. `AsiBackbone` is the product name; no acronym expansion is required to follow the material.

> **Read it. Run it. Question it. Improve it.**

## Start with what describes you

<div class="entry-point-grid">
  <a class="entry-point" href="getting-started/index.md">
    <strong>New to the project</strong>
    <span>Follow the recommended introduction to the core concepts.</span>
  </a>
  <a class="entry-point" href="getting-started/find-your-path.md">
    <strong>I have a problem to solve</strong>
    <span>Choose a short, goal-specific route through the material.</span>
  </a>
  <a class="entry-point" href="getting-started/adoption-personas-and-entry-points.md">
    <strong>I am evaluating by role</strong>
    <span>Start from your developer, architecture, platform, AI, or security responsibility.</span>
  </a>
  <a class="entry-point" href="architecture/terminology-and-established-concepts.md">
    <strong>The vocabulary is new</strong>
    <span>Map Learning terms to established architecture concepts.</span>
  </a>
  <a class="entry-point" href="architecture/when-aspnet-core-authorization-is-enough.md">
    <strong>I already use ASP.NET Core authorization</strong>
    <span>See when framework-native authorization is enough—and when the problem is broader.</span>
  </a>
</div>

Learning does not require installing an `AsiBackbone` package. Use it as independent .NET architecture education or connect it to the working repositories.

## The core model

The material uses a recurring separation of responsibilities:

<ol class="process-flow" aria-label="Governed execution flow">
  <li>Intent</li>
  <li>Context</li>
  <li>Constraints</li>
  <li>Decision</li>
  <li>Decision receipt</li>
  <li>Acknowledgment <span>when required</span></li>
  <li>Scoped authority</li>
  <li>Host-owned execution</li>
</ol>

## Choose a learning path

<div class="learning-path-grid">
  <article class="learning-path-card">
    <span class="path-level">Beginner to advanced</span>
    <h3>Architecture</h3>
    <p>Explore boundaries, responsibilities, tradeoffs, and structural patterns behind governed systems.</p>
    <a href="architecture/index.md" aria-label="Explore Architecture">Explore Architecture <span aria-hidden="true">→</span></a>
  </article>
  <article class="learning-path-card">
    <span class="path-level">Beginner to advanced</span>
    <h3>Governance</h3>
    <p>Connect intent, policy context, explicit decisions, acknowledgment, scoped authority, and audit evidence.</p>
    <a href="governance/index.md" aria-label="Explore Governance">Explore Governance <span aria-hidden="true">→</span></a>
  </article>
  <article class="learning-path-card">
    <span class="path-level">Beginner to intermediate</span>
    <h3>ASP.NET Core</h3>
    <p>Study secure defaults, middleware organization, operational structure, and modern .NET implementation patterns.</p>
    <a href="aspnetcore/index.md" aria-label="Explore ASP.NET Core">Explore ASP.NET Core <span aria-hidden="true">→</span></a>
  </article>
  <article class="learning-path-card">
    <span class="path-level">Beginner to advanced</span>
    <h3>Security</h3>
    <p>Examine trust boundaries, least authority, explicit control flow, defensive defaults, and accidental privilege.</p>
    <a href="security/index.md" aria-label="Explore Security">Explore Security <span aria-hidden="true">→</span></a>
  </article>
  <article class="learning-path-card">
    <span class="path-level">Intermediate to advanced</span>
    <h3>AI Integration</h3>
    <p>Apply governed execution to AI-assisted systems, tool calls, agents, workflows, and host-controlled effects.</p>
    <a href="ai-integration/index.md" aria-label="Explore AI Integration">Explore AI Integration <span aria-hidden="true">→</span></a>
  </article>
  <article class="learning-path-card">
    <span class="path-level">Beginner to intermediate</span>
    <h3>Tutorials</h3>
    <p>Move from a familiar implementation through failure modes, tradeoffs, and working references.</p>
    <a href="tutorials/index.md" aria-label="Browse Tutorials">Browse Tutorials <span aria-hidden="true">→</span></a>
  </article>
  <article class="learning-path-card">
    <span class="path-level">Beginner to intermediate</span>
    <h3>Executable Samples</h3>
    <p>Run five small .NET companions with focused tests that make architectural invariants observable.</p>
    <a href="samples/index.md" aria-label="Browse Executable Samples">Browse Executable Samples <span aria-hidden="true">→</span></a>
  </article>
  <article class="learning-path-card">
    <span class="path-level">Intermediate to advanced</span>
    <h3>Labs</h3>
    <p>Practice with exercises, incomplete implementations, architecture critiques, and policy scenarios.</p>
    <a href="labs/index.md" aria-label="Browse Labs">Browse Labs <span aria-hidden="true">→</span></a>
  </article>
  <article class="learning-path-card">
    <span class="path-level">Intermediate to advanced</span>
    <h3>Case Studies</h3>
    <p>See Learning boundaries composed in realistic simulated scenarios without prescribing a production framework.</p>
    <a href="case-studies/index.md" aria-label="Browse Reference Architecture Case Studies">Browse Reference Architecture Case Studies <span aria-hidden="true">→</span></a>
  </article>
  <article class="learning-path-card">
    <span class="path-level">Advanced</span>
    <h3>Advanced Topics</h3>
    <p>Explore complex integration patterns, alternative approaches, and deeper architectural questions.</p>
    <a href="advanced/index.md" aria-label="Explore Advanced Topics">Explore Advanced Topics <span aria-hidden="true">→</span></a>
  </article>
</div>

## How Learning works

Tutorials use a problem-first progression:

<ol class="process-flow process-flow-short" aria-label="Learning progression">
  <li>Problem</li>
  <li>Common implementation</li>
  <li>Failure mode</li>
  <li>Architectural pattern</li>
  <li>Teaching example</li>
  <li>Tradeoffs</li>
  <li>Working example</li>
</ol>

The goal is to make the reasoning visible—not to prove that one framework or architecture is always correct. Study a pattern, reimplement it, compare alternatives, identify when a simpler design is better, and treat working repositories as specimens rather than unquestioned templates.

## Working repositories

- [AsiBackbone/AsiBackbone](https://github.com/AsiBackbone/AsiBackbone) — a .NET governance and policy-control framework for explicit, auditable decision pipelines.
- [AsiBackbone/NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate) — an enterprise-oriented ASP.NET Core reference application with secure defaults and operational patterns.

## Recently added

Learning 1.1 is the current documentation baseline aligned with released AsiBackbone 7.0.0. The [current compatibility and API boundary](getting-started/asibackbone-7-api-boundary.md) pins implementation references to the immutable release tag; Learning 1.0 / AsiBackbone 6.0 remains available as historical guidance. Recent publications include:

- [A Passing Agent Diff Is Not Project Authority](articles/2026/a-passing-agent-diff-is-not-project-authority.md)
- [Why an AI Tool Call Is Only a Proposal](articles/2026/why-ai-tool-call-is-only-a-proposal.md)
- [Your Audit Log Is Not Evidence](articles/2026/your-audit-log-is-not-evidence.md)

<details class="home-details">
  <summary>Scope, terminology, and important limitations</summary>

  <p>Canonical patterns document what the working repositories currently do; alternative patterns create room for comparison, criticism, and improvement. Canonical does not mean universal.</p>

  <p>AsiBackbone Learning is an educational architecture resource—not a compliance certification, legal standard, security guarantee, AI model, AGI or ASI implementation, robotics controller, replacement for application-specific security review, or requirement to use the AsiBackbone package. Examples are teaching artifacts; production systems remain responsible for their own security, infrastructure, persistence, regulatory requirements, safety controls, and execution.</p>

  <p>AsiBackbone Learning is not affiliated with the Artificial Superintelligence Alliance.</p>
</details>

---

**Start with [Getting Started](getting-started/index.md), then follow the path that best matches the problem you want to understand.**
