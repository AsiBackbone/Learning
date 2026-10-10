---
description: Browse standalone AsiBackbone Learning technical articles published at stable year-and-slug URLs for direct external discovery and citation.
---

# Articles

AsiBackbone Learning articles are standalone technical arguments written for direct discovery, sharing, and citation. A reader can arrive from a search engine, newsletter, social link, conference resource list, or external reference without completing the Learning curriculum first.

## Start by Topic

| If you are interested in... | Start with |
| --- | --- |
| Authorization outages, fail-open versus fail-closed, deferral, and degraded mode | [Should Authorization Fail Open, Fail Closed, or Defer?](2026/fail-open-fail-closed-or-defer.md) |
| Shared background workers, tenant isolation, and workload identity | [Can One Worker Safely Execute Delayed Operations for Many Tenants?](2026/multi-tenant-worker-execution-authority.md) |
| Workflow engines, approval state, retries, and who owns the execution decision | [When Should a Workflow Engine Own the Decision?](2026/when-workflow-engine-should-own-decision.md) |
| Delegated authority for background workers, queues, and delayed execution | [How Short-Lived Execution Authority Differs from User Authorization](2026/short-lived-execution-authority-vs-user-authorization.md) |
| Microsoft.AgentGovernance and host application architecture | [How Application Architecture Complements Microsoft.AgentGovernance](2026/application-architecture-complements-microsoft-agent-governance.md) |
| Policy-as-code, policy engines, and remote policy services in ASP.NET Core | [Policy as Code in ASP.NET Core Without Overengineering](2026/policy-as-code-aspnet-core-without-overengineering.md) |
| Authorization, approval, acknowledgment, and delayed execution | [Authorization vs. Approval vs. Acknowledgment: Which Decision Do You Actually Have?](2026/authorization-vs-approval-vs-acknowledgment.md) |
| Validating AI tool calls before execution | [What Should an AI Tool Gateway Validate Before Execution?](2026/validate-ai-tool-call-before-execution.md) |
| MediatR pipeline behaviors, decorators, and authorization boundaries | [Your Pipeline Behavior Can Watch the Operation. It Should Not Decide It.](2026/pipeline-behavior-should-not-decide-the-operation.md) |
| AI-assisted development and maintainer authority | [A Passing Agent Diff Is Not Project Authority](2026/a-passing-agent-diff-is-not-project-authority.md) |
| Audit evidence and decision provenance | [Your Audit Log Records the Story, Not the Decision](2026/your-audit-log-is-not-evidence.md) |
| Capability tokens, roles, and claims | [Do You Need a Capability Token, or Are Roles and Claims Enough?](2026/roles-claims-or-capability-token-dotnet.md) |
| AI tool execution and host authority | [Why an AI Tool Call Is a Proposal, Not Authority](2026/why-ai-tool-call-is-only-a-proposal.md) |
| ASP.NET Core authorization versus governed execution | [When ASP.NET Core Authorization Is Not Enough](2026/when-aspnet-core-authorization-is-not-enough.md) |
| Testing protected execution boundaries | [How to Test That a Denied Operation Never Executes](2026/test-denied-operation-never-executes.md) |
| NuGet package trust and software supply chain evidence | [A Green CI Badge Does Not Prove Your .NET Package Is Trustworthy](2026/ci-badge-does-not-prove-package-integrity.md) |
| Authorization timing and resource-state decisions | [Your Authorization Check Runs Too Late](2026/authorization-check-runs-too-late.md) |

Prefer new articles as they are published? [Subscribe to the existing Learning RSS feed](https://asibackbone.github.io/Learning/feed.xml).

## Publication Model

Articles are not curriculum stages and do not require prior Learning material.

Each published article keeps a stable year-and-slug address:

```text
https://asibackbone.github.io/Learning/articles/<year>/<slug>.html
```

Once released, that URL is treated as permanent. Reorganizing tutorials, architecture pages, security material, or other curriculum content does not move an article.

The Learning site is the canonical publication host. Cross-posted copies should point back to the Learning article when the external platform supports canonical attribution.

## 2026 Archive

### [Should Authorization Fail Open, Fail Closed, or Defer?](2026/fail-open-fail-closed-or-defer.md)

**Christopher D. Cavell** · **October 10, 2026**

When a remote policy service times out, the code that called it makes an authorization decision whether anyone designed one or not. This guide follows one ASP.NET Core export endpoint through a policy outage to separate an explicit denial from an unavailable decision, keep timeouts, retries, circuit breakers, and fallbacks from manufacturing authority, choose a per-operation outage treatment of rejection, bounded deferral, escalation, or tightly bounded local evaluation, stop and reconcile when an external side effect may already have happened, and test that nothing protected executes while authority is unavailable, including when a plain "unavailable, try again later" is the whole answer.

Permanent URL: `https://asibackbone.github.io/Learning/articles/2026/fail-open-fail-closed-or-defer.html`

### [Can One Worker Safely Execute Delayed Operations for Many Tenants?](2026/multi-tenant-worker-execution-authority.md)

**Christopher D. Cavell** · **October 6, 2026**

Checking that a grant's tenant matches the resource's tenant stops one tenant's delayed operation from being turned into another's, but it does not contain a compromised shared worker that is authorized for every tenant. This guide follows one overnight customer-data export to separate substitution prevention, workload identity, and compromise isolation, shows how the protected host resolves and re-enforces the tenant, explains how to identify a workload unambiguously and handle ASP.NET Core claim mapping, and compares a shared worker with partitioned identities, per-tenant execution, and tenant-bound token exchange, including when the shared worker is the right choice.

Permanent URL: `https://asibackbone.github.io/Learning/articles/2026/multi-tenant-worker-execution-authority.html`

### [When Should a Workflow Engine Own the Decision?](2026/when-workflow-engine-should-own-decision.md)

**Christopher D. Cavell** · **October 5, 2026**

A workflow engine can own sequencing, timers, retries, and approval tasks without owning the permission to perform a side effect now. This guide follows one production deployment to show why `Approved` is not current permission, how to evaluate independently owned policy at the protected execution boundary when work is delayed or retried, and when a workflow that shares one cohesive boundary with the executor can simply own the decision.

Permanent URL: `https://asibackbone.github.io/Learning/articles/2026/when-workflow-engine-should-own-decision.html`

### [How Short-Lived Execution Authority Differs from User Authorization](2026/short-lived-execution-authority-vs-user-authorization.md)

**Christopher D. Cavell** · **October 3, 2026**

A user's authorization answers whether they may request an operation now; it is not the right authority to forward to a background worker. This guide follows one scheduled vendor payout to show how to delegate only one operation, resource, audience, time window, and use count to a later executor, what the protected host must still check at execution time, and when no extra grant is needed at all.

Permanent URL: `https://asibackbone.github.io/Learning/articles/2026/short-lived-execution-authority-vs-user-authorization.html`

### [How Application Architecture Complements Microsoft.AgentGovernance](2026/application-architecture-complements-microsoft-agent-governance.md)

**Christopher D. Cavell** · **October 2, 2026**

Microsoft.AgentGovernance gives .NET teams deterministic runtime policy evaluation for agent actions. That verdict is strongest when the surrounding application still owns authoritative context, workflow state, protected execution, failure handling, and evidence. This guide follows one AI-proposed refund through a host-owned control flow and shows where the governance verdict belongs.

Permanent URL: `https://asibackbone.github.io/Learning/articles/2026/application-architecture-complements-microsoft-agent-governance.html`

### [Policy as Code in ASP.NET Core Without Overengineering](2026/policy-as-code-aspnet-core-without-overengineering.md)

**Christopher D. Cavell** · **September 30, 2026**

Policy-as-code is a way to make decision logic explicit, reviewable, and testable, not a requirement to adopt a separate engine or remote service. This guide follows one ASP.NET Core refund endpoint to show when ordinary code or framework authorization is enough, and which real pressures justify an in-process policy component, an embedded engine, or a remote decision service.

Permanent URL: `https://asibackbone.github.io/Learning/articles/2026/policy-as-code-aspnet-core-without-overengineering.html`

### [Authorization vs. Approval vs. Acknowledgment: Which Decision Do You Actually Have?](2026/authorization-vs-approval-vs-acknowledgment.md)

**Christopher D. Cavell** · **September 27, 2026**

Authorization, approval, and acknowledgment answer different questions, bind to different things, and expire on different clocks. This guide uses one sensitive-data export to show what each decision proves, what it does not, and why none of them should silently become permission to execute later.

Permanent URL: `https://asibackbone.github.io/Learning/articles/2026/authorization-vs-approval-vs-acknowledgment.html`

### [What Should an AI Tool Gateway Validate Before Execution?](2026/validate-ai-tool-call-before-execution.md)

**Christopher D. Cavell** · **September 27, 2026**

Structured model output becomes eligible for execution only after the trusted host independently validates its shape, meaning, context, authority, and current permission. This is the ordered checklist, covering approvals, retries, and uncertain outcomes, with tests that prove proposals blocked before execution never reach the executor.

Permanent URL: `https://asibackbone.github.io/Learning/articles/2026/validate-ai-tool-call-before-execution.html`

### [Your Pipeline Behavior Can Watch the Operation. It Should Not Decide It.](2026/pipeline-behavior-should-not-decide-the-operation.md)

**Christopher D. Cavell** · **September 26, 2026**

A generic, operation-agnostic pipeline behavior wraps a call that has already been chosen; it sees the request rather than the resource, cannot express operation-specific outcomes without an operation-specific contract, and can be bypassed by registration order or a second entry path.

Permanent URL: `https://asibackbone.github.io/Learning/articles/2026/pipeline-behavior-should-not-decide-the-operation.html`


### [A Passing Agent Diff Is Not Project Authority](2026/a-passing-agent-diff-is-not-project-authority.md)

**Christopher D. Cavell** · **September 13, 2026**

A case study of AI-assisted development: two pull requests for an optional X publisher show why passing checks, production evidence, and maintainer authority answer different questions.

Permanent URL: `https://asibackbone.github.io/Learning/articles/2026/a-passing-agent-diff-is-not-project-authority.html`

### [Your Audit Log Records the Story, Not the Decision](2026/your-audit-log-is-not-evidence.md)

**Christopher D. Cavell** · **September 7, 2026**

An audit line written after execution describes an outcome; evidence requires a recorded decision, a binding to the operation it authorized, and integrity that outlives the process that wrote it.

Permanent URL: `https://asibackbone.github.io/Learning/articles/2026/your-audit-log-is-not-evidence.html`

### [Do You Need a Capability Token, or Are Roles and Claims Enough?](2026/roles-claims-or-capability-token-dotnet.md)

**Christopher D. Cavell** · **September 2, 2026**

Use roles or claims when current host authorization is enough; introduce a capability only when narrow authority must survive a later, delegated, or cross-boundary execution step.

Permanent URL: `https://asibackbone.github.io/Learning/articles/2026/roles-claims-or-capability-token-dotnet.html`

### [Why an AI Tool Call Is a Proposal, Not Authority](2026/why-ai-tool-call-is-only-a-proposal.md)

**Christopher D. Cavell** · **August 28, 2026**

A model may propose a tool call, but valid JSON and a known tool do not create authority; the trusted host still owns context, authorization, credentials, and execution.

Permanent URL: `https://asibackbone.github.io/Learning/articles/2026/why-ai-tool-call-is-only-a-proposal.html`

### [When ASP.NET Core Authorization Is Not Enough](2026/when-aspnet-core-authorization-is-not-enough.md)

**Christopher D. Cavell** · **August 26, 2026**

ASP.NET Core authorization is often the right answer; add a broader decision/execution lifecycle only when workflow, time, authority, or evidence crosses the request boundary.

Permanent URL: `https://asibackbone.github.io/Learning/articles/2026/when-aspnet-core-authorization-is-not-enough.html`

### [How to Test That a Denied Operation Never Executes](2026/test-denied-operation-never-executes.md)

**Christopher D. Cavell** · **August 26, 2026**

A denied result is useful evidence, but a denied result plus zero protected executor calls proves the execution boundary held.

Permanent URL: `https://asibackbone.github.io/Learning/articles/2026/test-denied-operation-never-executes.html`

### [A Green CI Badge Does Not Prove Your .NET Package Is Trustworthy](2026/ci-badge-does-not-prove-package-integrity.md)

**Christopher D. Cavell** · **August 23, 2026**

A practical guide to tracing a NuGet package from reviewed source through build, publication, and verifiable release evidence.

Permanent URL: `https://asibackbone.github.io/Learning/articles/2026/ci-badge-does-not-prove-package-integrity.html`

### [Your Authorization Check Runs Too Late](2026/authorization-check-runs-too-late.md)

**Christopher D. Cavell** · **August 23, 2026**

Authorization can succeed while resource-state or workflow rules still block execution; resolve that decision before protected side effects begin.

Permanent URL: `https://asibackbone.github.io/Learning/articles/2026/authorization-check-runs-too-late.html`

## Articles and Tutorials Serve Different Jobs

| Tutorial | Article |
| --- | --- |
| Part of a learning progression | Standalone technical argument |
| May have prerequisites | No curriculum prerequisite |
| Teaches a concept systematically | Makes one useful argument completely |
| May move as the curriculum evolves | Keeps a permanent publication URL |

Articles may link to tutorials, samples, labs, ADRs, or implementation repositories for deeper study, but those materials are optional follow-up rather than prerequisites.

## Contribute an Article

Want to contribute an article? See the [article publishing guidance](https://github.com/AsiBackbone/Learning/blob/main/CONTRIBUTING.md#publishing-authored-articles).
