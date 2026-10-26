---
description: When an AI coding agent finds adjacent work outside its assigned issue, finding it does not authorize doing it. Preserve the discovery; keep the scope.
title: "Discovery Is Not Authorization: Using GitHub Issues to Govern AI-Assisted Development"
author: Christopher D. Cavell
published: "2026-10-26"
summary: An AI coding agent working a bounded issue will find adjacent defects, refactorings, security concerns, and documentation gaps. Finding them does not authorize fixing them. Treat the issue as a work order, classify each discovery, then stop, propose a linked issue, escalate privately, or discard it, and keep the evidence that produced it.
feed: true
x_hashtags:
  - DotNet
  - AIGovernance
---

# Discovery Is Not Authorization: Using GitHub Issues to Govern AI-Assisted Development

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** No formal prerequisites. Familiarity with GitHub issues, pull requests, and continuous integration is helpful, as is some experience assigning work to an AI coding agent. No AsiBackbone package or prior Learning material is required.

**Sample scope:** The repository, issue numbers, issue text, and comments are illustrative. The GitHub behavior described here (references, sub-issues, issue dependencies, closing keywords, and private vulnerability reporting) is taken from GitHub's documentation as of October 2026; check the current documentation, because these features change. The practices apply to other issue trackers with equivalent features.

**What this article covers:** why an issue is a better unit of AI-assisted work than a prompt; what a bounded issue should state; four kinds of discovery and the action each one calls for; when an agent should stop, comment, propose a linked issue, escalate privately, or drop a finding; what a follow-on issue should carry so a later reader can trace its origin; what GitHub's linking features do and do not guarantee; how human comments and other artifacts provide different kinds of evidence; treating issue text as data rather than as authority; and when a lighter process is enough.

**In plain terms:** you hire an electrician to replace a kitchen light fixture. With the ceiling open, they notice that the wiring in the hallway looks undersized and that a bathroom outlet is not grounded. A good electrician does not rewire the hallway on your invoice for the light fixture. They tell you what they saw, write it down, and quote it separately. If the outlet is dangerous, they tell you now, not in a note left on the counter, and they take only the protective step their trade already expects of them, such as switching off that circuit. Finding the problem was part of the job. Deciding to fix it was not. In software, the equivalent protective step has to be defined in advance; an agent should not improvise containment, such as disabling a workflow or revoking a credential, because a situation looks urgent.

An AI coding agent is assigned an issue: add a CSV export to an invoice list endpoint. While reading the query code, it notices four other things. The list query filters by date in memory after loading every row. An older PDF endpoint looks up invoices by ID without checking the tenant. A status name in the code does not match the API documentation. And the agent would have structured a helper class differently.

The agent fixes all four in the same pull request. The tests pass, CI is green, and the reviewer, who is checking a CSV export, approves it. The pull request's commit history now describes a cross-tenant read in a public repository before any fix has been deployed. A support console that reached the same lookup through a separate policy now returns 404 for every invoice. The helper refactor changed the order of rows in an unrelated report. Nobody asked for any of it, and nobody reviewed it as the change it actually was.

The opposite failure is just as common and harder to see. The agent notices the same four things, mentions them in a chat transcript or a terminal log, and they disappear when the session ends.

> **Discovering adjacent work does not authorize performing it. The assigned issue is the work order. A discovery is a proposal to whoever owns the backlog: preserve it with its evidence, route it to the right place, and leave the decision to perform it with the people who have that authority.**

This is not an argument for filing an issue for every observation, or for forbidding small cleanups that a repository's conventions already allow. Near the end, this article covers when a lighter process is enough.

---

## The Example: One Issue and Four Discoveries

The rest of the article uses one repository.

A small team maintains `billing-api`, an ASP.NET Core service that stores invoices for many tenants. The repository is public. Two maintainers review every pull request, and a coding agent works issues they assign to it. The agent can read the repository, edit a branch, run the test suite, push that branch, and open a pull request. It cannot merge, change repository settings, read deployment secrets, or file security advisories.

Those limits describe the agent's capability: what it can technically do. They do not describe its authorization. The issue and the repository's policy decide what it is authorized to do, and that is usually much narrower. An agent that can push any change to its branch is not thereby authorized to put any change in its pull request.

On Tuesday, a maintainer assigns issue #212:

```text
#212  Add CSV export to GET /invoices

Outcome
  Tenant users can download their invoice list as CSV with the same
  filters the JSON endpoint accepts.

Acceptance criteria
  - GET /invoices?format=csv returns text/csv with the documented columns.
  - The export respects the caller's tenant and the existing date filters.
  - A 12-month export for the largest test tenant completes within 30 s.
  - Tests cover the column set, an empty result, and a cross-tenant request.

Non-goals
  - No changes to the JSON response shape.
  - No new export formats.

Constraints
  - Reuse InvoiceQueries; do not add a second data-access path.
  - Do not edit files under .github/ or deploy/.
```

During its investigation, the agent finds:

| | Discovery | Where |
| --- | --- | --- |
| D1 | `InvoiceQueries.List` loads every invoice for the tenant and applies the date filter in memory. A 12-month export for the largest test tenant takes about 90 seconds. | The query the issue says to reuse |
| D2 | `GET /invoices/{id}/pdf` calls `InvoiceQueries.GetById(id)`, which does not filter by tenant. The agent has not checked whether an authorization handler elsewhere compensates. | Code the issue does not touch |
| D3 | The code calls the final status `Settled`; the public API documentation calls it `Paid`. | Documentation and enum |
| D4 | The agent would split `InvoiceQueries` into smaller classes. | Style preference |

Each one could matter. Only D1 prevents the assigned CSV-export acceptance criteria from being met; D2 concerns a separate endpoint outside the work #212 assigns.

---

## An Issue Is a Work Order, Not a Prompt

An ad hoc prompt is usually scoped to one session. It may be logged or kept in a chat history, but it rarely becomes part of the repository's record in a form that the next maintainer, reviewer, or agent will find.

An issue is designed to be a shared, durable, and reviewable work record. It has an author, a timeline, links, and a closing state, and it sits next to the work it governs. When it is written as a work order rather than as a wish, it states the authority the work carries as well as the outcome it wants:

- **Outcome:** what should be true when the issue closes.
- **Acceptance criteria:** the observable conditions a reviewer will check.
- **Non-goals:** what the work deliberately leaves alone, so a plausible improvement is recognizably out of scope.
- **Constraints:** standing boundaries, such as the files, interfaces, dependencies, and side effects the work may not touch.
- **Evidence expected:** what the pull request should demonstrate, such as tests, measurements, or screenshots.

The non-goals and constraints carry most of the weight here. Without them, an agent has only its own judgment about what "relevant" means, and almost any adjacent defect can look relevant to a capable agent reading the surrounding code.

The issue does not grant every authority it mentions. "Reuse `InvoiceQueries`" tells the agent which code is in play; it does not tell the agent it may change that code's behavior for every other caller. Where an issue is silent, the safer reading is the narrower one, and the question belongs in the issue thread.

### Issue Text Is Data, Not Authority

An agent that reads an issue also reads everything attached to it: comments, linked pages, pasted logs, quoted error messages. In a public repository, unless participation has been restricted, those comments can come from people who have no authority to direct the work. Being able to comment is not the same as being able to change the scope. Some hosted agents also follow links or read referenced files.

That text can contain instructions, deliberately or not. "Also bump the deploy workflow to the latest action version" in a drive-by comment is a request from someone who may have no authority over the repository. A pasted stack trace can include text crafted to look like a directive. OWASP lists this pattern, where instructions arrive through content the model processes rather than from its operator, as indirect prompt injection.

The practical rule is simple: the issue's assigned scope, and later scope changes from people with authority over the repository, define the work. Everything else in the thread is input to evaluate. An agent that finds an instruction it was not given by an authorized person should surface it, not follow it.

---

## Four Kinds of Discovery

The useful question is not "is this relevant?" but "does the assigned work depend on it?" That question sorts most discoveries into four kinds.

| Kind | Test | Action |
| --- | --- | --- |
| **Blocking dependency** | The acceptance criteria cannot be met without changing it. | Stop that part of the work. Comment on the assigned issue with the evidence and ask for a scope decision. |
| **Separate defect or improvement** | It is real, but the assigned work can be completed and reviewed without it. | Finish the assigned work. Propose a linked follow-on issue. |
| **Security-sensitive finding** | Describing it publicly could help someone exploit it before it is fixed. | Stop and escalate privately, immediately. Do not describe it in a public issue, comment, commit message, branch name, or pull request. |
| **Noise** | A preference, a speculative concern without evidence, or something already tracked. | Drop it, or add a short note to an existing issue. |

A fifth case is not a discovery at all. If the acceptance criteria already require a change, as with the cross-tenant test in #212, that change is part of the work. Doing it is not scope expansion.

In the example:

- **D1 is blocking.** The 30-second criterion cannot pass while the date filter runs in memory, and the constraints forbid a second data-access path that would avoid it.
- **D2 is security-sensitive.** It may turn out to be harmless, but the agent cannot rule that out, and a public description would be useful to an attacker if it is not.
- **D3 is a separate defect.** The CSV export can be built and reviewed with either status name.
- **D4 is noise.** A different class structure is a preference, not a finding.

A few questions settle most borderline cases:

1. Would fixing it change behavior that a reviewer of the assigned issue will not be looking at?
2. Does it affect callers, users, or data outside the issue's stated outcome?
3. Is the evidence strong enough that someone else could confirm it without repeating the whole investigation?
4. Is it already tracked?
5. Would describing it in public cause harm before it is fixed?

A "yes" to the first two pushes a finding out of the current pull request. A "no" to the third suggests a comment rather than an issue. A "yes" to the fourth means a short note on the existing issue. A "yes" to the fifth overrides everything else.

---

## Stop, Comment, Propose, or Escalate

Each action has a different cost and leaves a different record.

### Stop and Ask When the Work Is Blocked

For D1, the agent stops the export work and comments on #212:

```text
Blocked on the 30 s acceptance criterion.

InvoiceQueries.List (src/Billing/InvoiceQueries.cs, lines 48-71 at commit
4c1e9a2) loads all invoices for the tenant and applies the date range in
memory. A 12-month CSV export for the largest test tenant takes ~90 s
locally (measurement in the draft PR description).

Options I can see:
  1. Move the date filter into the query in this PR, as a separate commit,
     with a test for the 12-month range. This changes List for its two
     other callers (the JSON endpoint and the monthly statement job).
  2. Fix the query in a separate issue first and mark #212 blocked by it.
  3. Relax the 30 s criterion.

I have not changed InvoiceQueries.List. Which do you want?
```

The comment states the evidence, what was not done, and what each option would affect. It does not choose. The performance outcome is already authorized: #212 asks for it. The open question is whether that authorization extends to changing shared behavior for two callers the issue does not otherwise place in scope, and that #212's reviewer was not going to examine. That is a scope decision, and scope decisions belong to the maintainers.

A maintainer replies:

```text
Go with option 1. Keep the query change in its own commit, add a test
for the statement job's date range as well, and call out both other
callers in the PR description.
```

That reply is the authorization. It comes from a person who can grant it, it is recorded where a later reader will find it, and it is specific about the extra review the wider change needs. If the maintainers had chosen option 2, the new issue would be linked to #212 as a dependency, as described in the next section.

### Propose a Linked Issue for Separate Work

For D3, the agent finishes #212 and adds a short section to the pull request description:

```text
Discovered outside scope (not changed in this PR)

- The code names the final invoice status "Settled"; docs/api/invoices.md
  calls it "Paid". The CSV export uses the enum name. Proposed follow-on:
  decide which name is authoritative and align code, docs, and CSV header.
```

Whether the agent may file that issue itself is a project decision, not a technical one. Opening an issue is a side effect: it notifies watchers, appears in public, and asks someone to triage it. Some teams let agents file issues with a label such as `agent-proposed` so they can be reviewed as a batch. Others prefer the agent to draft the text and a maintainer to file it. Either is reasonable when it is a deliberate choice. In `billing-api`, a maintainer files it as #219.

### Escalate Privately When Disclosure Could Cause Harm

For D2, the agent stops immediately and tells the maintainer who assigned the work through a private channel, such as its session with that maintainer, not through any public surface. An agent that has no private channel, such as one that can only comment on the issue, should say only that it has a concern to raise privately and stop, without describing it.

In `billing-api`, the agent's session report reads:

```text
Possible security issue, outside #212's scope. Not mentioned in the
branch, commits, or PR.

GET /invoices/{id}/pdf calls InvoiceQueries.GetById(id) without a
tenant filter. I have not confirmed whether an authorization handler
elsewhere prevents a cross-tenant read. Please follow SECURITY.md;
I have made no changes related to this.
```

Urgency changes how fast a finding is raised. It does not change who may act on it. A possible cross-tenant read is exactly the kind of change that needs a deliberate fix, a private review, and a coordinated disclosure, none of which a CSV export pull request can provide.

On GitHub, a public repository's owners can enable private vulnerability reporting, which lets someone submit a report that only maintainers can see and that can become a draft security advisory. When it is not enabled, GitHub's guidance is to follow the repository's security policy or to open an issue asking for a preferred security contact, without the details. A human, not the agent, decides which path to use.

### Drop What Is Not a Finding

D4 gets no issue and no comment. Filing every preference turns the backlog into the agent's diary and makes the real findings harder to see. If the agent's reviewers want its structural opinions, they can ask for them.

---

## What a Follow-On Issue Should Carry

A follow-on issue has two readers: a maintainer deciding whether to do the work, and whoever eventually does it, which may be another agent months later. Both need to know where the concern came from without re-reading the original investigation.

```text
#219  Align "Settled" and "Paid" invoice status naming

Discovered while working #212 (PR #214, at commit 7b20f3d).

Observed
  - InvoiceStatus.Settled in src/Billing/InvoiceStatus.cs
  - docs/api/invoices.md describes the final status as "Paid"
  - The CSV export from #212 uses the enum name in its header row

Not checked
  - Whether any API client depends on either name
  - Whether "Paid" and "Settled" were ever intended to mean different things

Why not fixed in #212
  - Not required by #212's acceptance criteria; renaming affects the
    JSON response, which #212 lists as a non-goal.

Proposed outcome
  - Decide the authoritative name, then align code, docs, and CSV header.
```

A useful follow-on issue:

- **Links its origin.** The originating issue, pull request, and commit let a reader reconstruct the context without copying it.
- **Pins evidence to a revision.** Code moves. A line reference or permalink at a specific commit still points at what the agent saw after the file changes.
- **Separates observation from inference.** "The documentation says Paid" is an observation. "Clients probably depend on Paid" is a guess, and a later reader should be able to tell the difference.
- **States what was not checked.** That prevents a later reader from assuming the investigation was complete.
- **Explains why it was out of scope.** That records the scope decision, not only the finding.
- **Proposes, rather than decides,** priority, severity, and approach.

It should not carry the whole investigation transcript, credentials or tokens that appeared in logs, or details of a security-sensitive finding.

---

## Links Organize Work. They Do Not Enforce It.

GitHub offers several ways to connect issues. Each one records a relationship; none of them makes the relationship true.

| Mechanism | What it records | What it does not do |
| --- | --- | --- |
| A plain reference such as `#212` in an issue, pull request, or comment | A link to the referenced issue and, by default, a backlink from it | Imply any dependency, ordering, or ownership. GitHub does not create these autolinks in repository files or wikis |
| Sub-issues | A parent-child breakdown of larger work, visible in issues and Projects | Make a discovery part of the parent's scope. GitHub documents up to 100 sub-issues per parent and eight levels of nesting |
| Issue dependencies ("blocked by" and "blocking") | That one issue is considered blocked by another, displayed in issue and project views | Prevent anyone from working on or closing the blocked issue. GitHub describes them as indicators for spotting blocked work |
| Closing keywords such as `Fixes #219` in a pull request description or commit message | That the pull request is meant to resolve the issue. When the pull request merges into the default branch and issue auto-closing is enabled, as it is by default, the linked issue closes | Apply to a pull request that targets any other branch, where GitHub ignores them, or close anything in a repository whose administrators have turned auto-closing off |

Two practical consequences follow.

First, a follow-on discovery is usually a plain reference, not a sub-issue. Making #219 a sub-issue of #212 would say that renaming the status is part of the CSV export, which is the scope expansion the article argues against. A sub-issue fits when a maintainer deliberately breaks one piece of work into several.

Second, use closing keywords only for the issue a pull request actually resolves. A pull request description that says "Fixes #212, also see #219" is fine. One that says "Fixes #212, fixes #219" because the agent touched one line of #219's concern will, with the default setting, close an issue nobody has reviewed as done.

Links reduce duplicate investigation when people search before they file and when an agent checks for an existing issue before proposing a new one. They do not prevent duplicates: two agents working in parallel can file the same finding minutes apart. GitHub can close an issue as a duplicate, which preserves the link between the two.

---

## Comments, Tests, and Commits Are Different Kinds of Evidence

An issue thread accumulates contributions from people, agents, and automation. They do not carry the same weight.

| Artifact | What it can establish | What it does not establish |
| --- | --- | --- |
| Issue body | The requested outcome, constraints, and non-goals at the time it was written | That the request is complete or still current |
| Maintainer comment | A recorded scope decision, correction, or authorization, when written by someone with authority over the affected scope | That the commenter had authority over every affected system, or that the decision was correct |
| Comment from someone without authority over the repository | A report, suggestion, or question to evaluate | Any change in scope |
| Agent comment | An observation, proposal, or question, with the evidence offered for it | That the observation is complete or that the proposal is approved |
| Tests | The behavior the tests check passes in that environment | That untested behavior is correct, or that the tests check the right thing |
| CI result | The configured gates passed for that revision | That the change matches the issue's intent |
| Pull request review | A responsible person accepted the change and its tradeoffs | That every consequence was understood |
| Merge commit | A reviewed proposal became repository state | That it was deployed or that it works in production |
| Linked follow-on issue | A discovery was preserved with its origin | That the discovery is correct, prioritized, or will be done |

A year later, #212's thread shows what was asked, who widened the scope and why, and where the adjacent finding went. The security finding is deliberately absent from that public record.

### What the Record Still Misses

Repository history is not complete institutional memory. Decisions made in a call or a chat, reasons nobody wrote down, private security reports, and context from deleted comments do not appear in it. Edited issue bodies can change what later readers see. Treat the thread as the best available record, not as a complete one.

When a discovery leads to a consequential design choice, such as how tenant isolation is enforced across every query, the reasoning deserves more than an issue comment. An architecture decision record keeps the context, alternatives, and consequences in a form that outlives the issue that prompted it.

### Reading the Record Later

When an agent or a person picks up #219 three months later, the link back to #212 tells them where the concern came from. It does not tell them the concern is still true. The evidence was pinned to commit `7b20f3d`; the first step is to check it against the current code. An agent that treats an old issue's evidence as current is repeating the mistake in reverse: it is treating a historical observation as present authority.

---

## Staying in Scope Is Not the Same as Being Right

An agent that stayed inside its issue's scope has made the review smaller, not unnecessary. A pull request that touches only what #212 allowed still needs the same review of correctness, tests, and consequences as any other change.

The reverse also holds. An agent that correctly proposes a follow-on issue has not shown that the finding is true or important. A maintainer still has to evaluate it.

---

## When a Lighter Process Is Enough

Most of this is proportional to what a mistake would cost.

- **Small, conventional cleanups.** Many repositories allow fixing a typo, an obviously wrong comment, or an unused `using` in a file the change already touches. If the convention exists, follow it, and mention the cleanup in the pull request description so the reviewer sees it.
- **A solo maintainer working interactively.** When the person who owns the backlog is reading every turn of the session, "I also noticed X; want me to fix it?" in the conversation is the scope question. Writing the answer into the issue or pull request still helps the next reader.
- **Throwaway or exploratory work.** A spike branch, a prototype, or a scratch repository does not need follow-on issues for every finding. A notes file is enough.
- **Observations without evidence.** "This might be slow" is a comment at most, not an issue.

Use more structure when the work is delegated to an agent that runs without someone watching each step, when the repository is public, when several people or agents work the same backlog, or when a finding could touch security, data, money, or other tenants.

---

## Failure Modes

### 1. Silent scope expansion

The agent fixes adjacent problems in the assigned pull request. The reviewer evaluates the change they expected and approves changes they never examined.

### 2. Silent loss

The agent notices problems and mentions them in a session transcript or log that nobody reads again.

### 3. Filing everything

Every observation becomes an issue. Triage time goes to preferences and speculation, and real findings get lost.

### 4. Treating thread text as authority

The agent follows an instruction from a comment, a pasted log, or a linked page that did not come from someone with authority over the repository.

### 5. Disclosure through helpfulness

A security-sensitive finding appears in a public issue, a commit message, a branch name, a test name, or a pull request description before a fix exists.

### 6. Linking that claims too much

A follow-on finding becomes a sub-issue of the assigned work, or a closing keyword closes an issue the pull request did not resolve.

### 7. Stale evidence

A later reader treats a follow-on issue's evidence as current without checking it against the code as it is now.

### 8. Approval by scope

A reviewer approves a pull request quickly because the agent stayed in scope, as though staying in scope were evidence of correctness.

---

## A Short Review Checklist

**The work order**

1. Does the issue state an outcome, acceptance criteria, non-goals, and constraints?
2. Does it say which side effects, such as files, workflows, dependencies, or external calls, are out of bounds?
3. Is it clear who can change the scope, and where that change will be recorded?

**Discoveries**

4. Has each discovery been classified as blocking, separate, security-sensitive, or noise?
5. Did blocked work stop with a comment that states the evidence, what was not done, and the options?
6. Is every scope expansion recorded as an explicit decision from someone with authority to make it?
7. Does each follow-on issue link its origin, pin its evidence to a revision, separate observation from inference, and say what was not checked?
8. Were security-sensitive findings kept out of every public surface and routed through the repository's security policy?

**Links and evidence**

9. Do closing keywords name only the issues the pull request resolves?
10. Are follow-on discoveries plain references rather than sub-issues of the work that found them?
11. Was the pull request reviewed as carefully as it would have been if the agent had not stayed in scope?

---

## Continue Deeper

[A Passing Agent Diff Is Not Project Authority](a-passing-agent-diff-is-not-project-authority.md#risks-the-process-does-not-remove) follows one AI-assisted pull request through proposal, validation, authority, execution, and evidence. Its list of risks that the process does not remove, including automation bias, shared blind spots, and untrusted context, applies to issue-driven work as much as to the pull request that follows it.

When a discovery leads to a consequential design choice, [Architecture Decision Records Preserve Architectural Reasoning](../../aspnetcore/architecture-decision-records-preserve-architectural-reasoning.md#when-does-a-decision-merit-an-adr) explains when the reasoning deserves a decision record rather than an issue comment, and what that record should contain.

When later evidence changes that choice, [Architecture Decision Record Lifecycle, Review, Deprecation, and Supersession](../../aspnetcore/architecture-decision-record-lifecycle-review-deprecation-and-supersession.md#record-the-trigger-not-just-the-new-preference) shows how to record what triggered the change without rewriting the history of the original decision.

[Why an AI Tool Call Is a Proposal, Not Authority](why-ai-tool-call-is-only-a-proposal.md) makes the same argument inside an application: a well-formed proposal from a model does not grant the authority to carry it out.

### Related Work

- GitHub's [issue dependencies](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/creating-issue-dependencies) documentation describes marking issues as blocked by or blocking other issues.
- GitHub's [sub-issues](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/adding-sub-issues) documentation describes parent-child issue hierarchies and their limits.
- [Linking a pull request to an issue](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/linking-a-pull-request-to-an-issue) lists the closing keywords and explains that they apply only to pull requests that target the default branch.
- [Managing the automatic closing of issues](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/managing-repository-settings/managing-auto-closing-issues) describes the repository setting that controls whether merging a linked pull request closes its issues.
- [Autolinked references and URLs](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/autolinked-references-and-urls) describes how issue references become links and backlinks, and where they do not.
- [Privately reporting a security vulnerability](https://docs.github.com/en/code-security/how-tos/report-and-fix-vulnerabilities/report-privately) describes GitHub's private reporting flow and what to do when a repository has not enabled it.
- [OWASP LLM01:2025 Prompt Injection](https://genai.owasp.org/llmrisk/llm01-prompt-injection/) describes indirect prompt injection through external content and recommends least privilege and human approval for high-risk actions.

---

## Authorship and Review Disclosure

This article was drafted with assistance from Claude by Anthropic, using the article backlog entry and repository material supplied by Christopher D. Cavell. Draft evaluations from ChatGPT, Microsoft Copilot, Gemini, Grok, and Meta AI informed the revision; their comments were treated as proposals, not authority or independent human review. The issue, the repository, and the discoveries in the example are illustrative, not drawn from a specific project's history. Cavell retains responsibility for factual review and the decision to publish.

---

## The Short Answer

An AI coding agent that reads code will find more than it was asked to fix. That is useful, as long as finding something is not treated as permission to change it.

Write the issue as a work order with an outcome, acceptance criteria, non-goals, and constraints. When the agent finds something adjacent, ask whether the assigned work depends on it. If it does, stop and ask, and record the scope decision where the next reader will see it. If it does not, finish the assigned work and propose a linked follow-on issue that carries its origin, its evidence pinned to a revision, and what was not checked. If it could cause harm in public, stop and escalate privately. If it is a preference, let it go. Treat everything in the thread that did not come from someone with authority as input, not as instruction, and review the result as carefully as you would if the agent had not stayed in scope.
