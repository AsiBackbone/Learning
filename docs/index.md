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
  <p class="home-why">Learning makes the reasoning visible: why an operation was permitted, under which policy, with what evidence, and when a simpler design is enough. Use it before you adopt any implementation, or when a .NET system you design or review needs explicit decisions and audit evidence.</p>
  <div class="home-actions" role="group" aria-label="Start learning">
    <a class="btn btn-primary" href="getting-started/index.md">Get Started</a>
    <a class="btn btn-outline-primary" href="getting-started/find-your-path.md">Find Your Path</a>
    <a class="btn btn-outline-primary" href="articles/index.md">Browse Articles</a>
  </div>
  <p class="home-principle">A proposed action should become a governed decision before it becomes real-world execution.</p>
</div>

## Start here

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

## Is Learning a fit?

<div class="fit-grid">
  <section class="fit-panel" aria-labelledby="fit-use">
    <h3 id="fit-use">Use Learning when</h3>
    <ul>
      <li>you need to explain why an operation was permitted, under which policy, and with what evidence;</li>
      <li>you are comparing governed execution with familiar .NET patterns before choosing an implementation; or</li>
      <li>you want runnable examples and labs rather than a package to install.</li>
    </ul>
    <p>Learning does not require installing an <code>AsiBackbone</code> package. Use it as independent .NET architecture education or connect it to the working repositories.</p>
  </section>
  <section class="fit-panel" aria-labelledby="fit-simpler">
    <h3 id="fit-simpler">A simpler design may be enough when</h3>
    <ul>
      <li>a role or policy check on the endpoint fully answers who may act: see <a href="architecture/when-aspnet-core-authorization-is-enough.md">When ASP.NET Core Authorization Is Enough</a>;</li>
      <li>one service call, one transaction, and ordinary logs describe the whole operation: see <a href="architecture/when-a-simple-application-service-is-enough.md">When a Simple Application Service Is Enough</a>.</li>
    </ul>
  </section>
</div>

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
    <p>Run small .NET companion samples for the foundation, governance, security, and ASP.NET Core paths, each with focused tests that make architectural invariants observable.</p>
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

## Recently added

Learning 1.4 is the current documentation baseline aligned with its reviewed AsiBackbone 7.0.0 and NetCoreApplicationTemplate 2.11.2 boundaries. The dated [implementation-currentness review](getting-started/implementation-currentness-review-2026-10-24.md) records why Learning retains those archival pins after reviewing AsiBackbone 7.5.1 and NetCoreApplicationTemplate 2.11.6. The [compatibility and API boundary](getting-started/asibackbone-7-api-boundary.md) pins the selected implementation references; earlier Learning snapshots remain available as historical guidance. Recent publications include:

- [Your Retry Logic May Be Replaying an Authorization Decision](articles/2026/retry-logic-may-replay-authorization-decision.md)
- [You Don't Need to Roll Back What You Never Merged](articles/2026/you-dont-need-to-roll-back-what-you-never-merged.md)
- [An Allowed Decision Does Not Mean the Operation Succeeded](articles/2026/allowed-decision-does-not-mean-execution-succeeded.md)

New publications are announced in the [RSS feed](https://asibackbone.github.io/Learning/feed.xml).

<details class="home-details">
  <summary>Scope, terminology, and important limitations</summary>

  <p>Canonical patterns document what the working repositories currently do; alternative patterns create room for comparison, criticism, and improvement. Canonical does not mean universal.</p>

  <p>AsiBackbone Learning is an educational architecture resource—not a compliance certification, legal standard, security guarantee, AI model, AGI or ASI implementation, robotics controller, replacement for application-specific security review, or requirement to use the AsiBackbone package. Examples are teaching artifacts; production systems remain responsible for their own security, infrastructure, persistence, regulatory requirements, safety controls, and execution.</p>

  <p><code>AsiBackbone</code> is the product name; no acronym expansion is required to follow the material. AsiBackbone Learning is not affiliated with the Artificial Superintelligence Alliance.</p>
</details>

## Related projects

<div class="project-grid">
  <article class="project-card" aria-current="page">
    <span class="project-role">Learn the patterns</span>
    <h3>AsiBackbone Learning <span class="current-site">Current site</span></h3>
    <p>Practical .NET architecture tutorials, labs, runnable samples, and reference patterns for governed execution, secure applications, AI integration, and policy-driven systems, including comparisons that show when these patterns are unnecessary.</p>
    <p class="project-links">
      <a href="https://github.com/AsiBackbone/Learning" aria-label="AsiBackbone Learning repository">Repository</a>
      <a href="https://doi.org/10.5281/zenodo.21938556" aria-label="AsiBackbone Learning archive (DOI)">Archive (DOI)</a>
    </p>
  </article>
  <article class="project-card">
    <span class="project-role">Add governed decisions</span>
    <h3><a href="https://asibackbone.github.io/AsiBackbone/">AsiBackbone</a></h3>
    <p>A .NET governance and policy-control framework for auditable decision flows: policy evaluation, auditable decision records, acknowledgment workflows, capability-based authorization, host-owned execution boundaries, and ASP.NET Core integration.</p>
    <p class="project-links">
      <a href="https://asibackbone.github.io/AsiBackbone/" aria-label="AsiBackbone documentation">Documentation</a>
      <a href="https://github.com/AsiBackbone/AsiBackbone" aria-label="AsiBackbone repository">Repository</a>
      <a href="https://doi.org/10.5281/zenodo.20546032" aria-label="AsiBackbone archive (DOI)">Archive (DOI)</a>
    </p>
  </article>
  <article class="project-card">
    <span class="project-role">Start a secure application</span>
    <h3><a href="https://asibackbone.github.io/NetCoreApplicationTemplate/">.NET Core Application Template</a></h3>
    <p>A secure-by-default ASP.NET Core application template with authentication and authorization, structured logging, security headers, rate limiting, EF Core data access, reverse proxy support, and Problem Details error handling.</p>
    <p class="project-links">
      <a href="https://asibackbone.github.io/NetCoreApplicationTemplate/" aria-label=".NET Core Application Template documentation">Documentation</a>
      <a href="https://github.com/AsiBackbone/NetCoreApplicationTemplate" aria-label=".NET Core Application Template repository">Repository</a>
      <a href="https://doi.org/10.5281/zenodo.20373042" aria-label=".NET Core Application Template archive (DOI)">Archive (DOI)</a>
    </p>
  </article>
  <article class="project-card">
    <span class="project-role">Follow the integration</span>
    <h3><a href="https://asibackbone.github.io/ReferenceApplication/">AsiBackbone Reference Application</a></h3>
    <p>An NCAT-generated ASP.NET Core host that records the step-by-step integration of AsiBackbone, with host-specific API documentation and runnable governance examples added as the walkthrough progresses.</p>
    <p class="project-links">
      <a href="https://asibackbone.github.io/ReferenceApplication/" aria-label="AsiBackbone Reference Application documentation">Documentation</a>
      <a href="https://github.com/AsiBackbone/ReferenceApplication" aria-label="AsiBackbone Reference Application repository">Repository</a>
    </p>
  </article>
</div>

Treat the working repositories as specimens to study, question, and compare rather than unquestioned templates.

## Design philosophy

<div class="philosophy-callout">
  <p class="philosophy-statement">We favor architectures where consequential operations are explicit, constrained, reviewable, and auditable.</p>
  <p>We also document when these patterns are unnecessary: see <a href="architecture/when-aspnet-core-authorization-is-enough.md">When ASP.NET Core Authorization Is Enough</a> and <a href="architecture/when-a-simple-application-service-is-enough.md">When a Simple Application Service Is Enough</a>.</p>
  <p>The projects focus on practical .NET engineering. They do not claim regulatory certification or automatic compliance.</p>
  <p class="philosophy-motto">Read it. Run it. Question it. Improve it.</p>
</div>
