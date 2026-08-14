# Executable Samples

Executable samples are the **runnable demonstration layer** of ASI Backbone Learning.

They sit between the problem-first tutorials and the hands-on labs:

```text
Tutorial
   ↓
Executable Sample
   ↓
Hands-On Lab
   ↓
Working Repository
```

The canonical sample source and detailed sample documentation remain under [`samples/`](https://github.com/AsiBackbone/Learning/tree/main/samples) in the GitHub repository. This page keeps the published DocFX learning path intact while giving you enough information to choose and run the right companion sample before following the canonical README for deeper explanation.

## Foundational Sample Set

The five foundational tutorials each have an executable companion sample.

| Sample | Difficulty | Core boundary |
| --- | --- | --- |
| [Decision Before Execution](#decision-before-execution) | Beginner | A blocked decision never reaches the executor. |
| [Policy Context and Explicit Decision Outcomes](#policy-context-and-explicit-decision-outcomes) | Beginner | Policy consumes explicit facts and returns a structured outcome without performing the side effect. |
| [Acknowledgment and Audit Residue](#acknowledgment-and-audit-residue) | Intermediate | Acknowledgment satisfies a governance requirement; it does not become execution authority. |
| [Scoped Capability and Host-Owned Execution](#scoped-capability-and-host-owned-execution) | Intermediate | Narrow authority is validated again at the host-owned execution boundary. |
| [Governed AI Tool Gateway](#governed-ai-tool-gateway) | Advanced | The model may propose; the host retains execution authority. |

### Decision Before Execution

**Learning objective:** Observe how an explicit governance decision controls whether a host-owned executor is invoked.

**Difficulty:** Beginner

**Key invariant:**

> **A blocked decision never reaches the executor.**

Run from the repository root:

```bash
dotnet run --project samples/decision-before-execution/DecisionBeforeExecution/DecisionBeforeExecution.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-before-execution/README.md)
- [Read the tutorial](../tutorials/decision-before-execution.md)
- [Continue with the beginner lab](../labs/decision-before-execution.md)

### Policy Context and Explicit Decision Outcomes

**Learning objective:** Observe how actor, resource, operation, environment, correlation, and policy identity become explicit decision inputs, and how policy returns meaningful outcomes instead of collapsing the result into a boolean.

**Difficulty:** Beginner

**Key invariant:**

> **Policy evaluation consumes an explicit context snapshot and returns a structured outcome without performing the governed side effect.**

Run from the repository root:

```bash
dotnet run --project samples/policy-context-and-explicit-decision-outcomes/PolicyContextAndExplicitDecisionOutcomes/PolicyContextAndExplicitDecisionOutcomes.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/policy-context-and-explicit-decision-outcomes/README.md)
- [Read the tutorial](../tutorials/policy-context-and-explicit-decision-outcomes.md)
- [Continue with the learner exercise](../labs/policy-context-and-explicit-decision-outcomes.md)

### Acknowledgment and Audit Residue

**Learning objective:** Observe how a consequential operation can pause for a narrowly bound acknowledgment, validate the response, re-evaluate current policy, and preserve a correlated audit timeline without treating acknowledgment as standing permission.

**Difficulty:** Intermediate

**Key invariant:**

> **Acknowledgment is a governance boundary, not an execution bypass.**

Decision, acknowledgment, re-evaluation, and execution remain distinguishable evidence events throughout the flow.

Run from the repository root:

```bash
dotnet run --project samples/acknowledgment-and-audit-residue/AcknowledgmentAndAuditResidue/AcknowledgmentAndAuditResidue.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/acknowledgment-and-audit-residue/README.md)
- [Read the tutorial](../tutorials/acknowledgment-and-audit-residue.md)
- [Continue with the intermediate lab](../labs/acknowledgment-and-audit-residue.md)

### Scoped Capability and Host-Owned Execution

**Learning objective:** Observe how an allowed decision can produce short-lived, narrowly bound execution authority and how the host validates those bindings again against current execution context immediately before the side effect.

**Difficulty:** Intermediate

**Key invariants:**

> **A blocked decision cannot mint execution authority.**

> **Expired or stale execution authority never reaches the executor.**

Run from the repository root:

```bash
dotnet run --project samples/scoped-capability-and-host-owned-execution/ScopedCapabilityAndHostOwnedExecution/ScopedCapabilityAndHostOwnedExecution.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/scoped-capability-and-host-owned-execution/README.md)
- [Read the tutorial](../tutorials/scoped-capability-and-host-owned-execution.md)
- [Continue with the intermediate lab](../labs/scoped-capability-and-host-owned-execution.md)

### Governed AI Tool Gateway

**Learning objective:** Run an end-to-end AI-assisted governance flow where a simulated model may propose a tool action, but the host owns the tool allowlist, authoritative context, policy decision, acknowledgment, scoped capability, dry-run execution, and audit evidence.

**Difficulty:** Advanced

**Key invariant:**

> **The model may propose. The host retains execution authority.**

Run from the repository root:

```bash
dotnet run --project samples/governed-ai-tool-gateway/GovernedAiToolGateway/GovernedAiToolGateway.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/governed-ai-tool-gateway/README.md)
- [Read the tutorial](../tutorials/governed-ai-tool-gateway.md)
- [Continue with the advanced lab](../labs/governed-ai-tool-gateway.md)

## Run the Complete Sample Suite

The shared sample solution is [`samples/Samples.slnx`](https://github.com/AsiBackbone/Learning/blob/main/samples/Samples.slnx).

From the repository root:

```bash
dotnet restore samples/Samples.slnx
dotnet build samples/Samples.slnx --no-restore
dotnet test samples/Samples.slnx --no-build
```

The focused xUnit projects make architectural invariants executable rather than leaving them only as prose claims.

## Sample Design Principles

The canonical [`samples/README.md`](https://github.com/AsiBackbone/Learning/blob/main/samples/README.md) contains the full sample guidance. The recurring principles are:

- **Keep samples small.** Optimize for learning value rather than production completeness.
- **Keep side effects visible.** Evaluation, decision, and execution should remain distinguishable.
- **Test architectural invariants.** Tests should prove meaningful boundaries such as blocked execution, stale capability rejection, and unknown-tool rejection.
- **Prefer deterministic local behavior.** Use in-memory state, fakes, simulation, and dry-run execution where practical.
- **Use fictional data.** Do not place real credentials, secrets, tokens, personal information, or production connection strings in teaching samples.
- **Keep secrets host-owned.** A model or proposal generator should not receive infrastructure credentials merely because it can propose an action.
- **Do not hide policy in prompt text.** Prompt guidance may influence a proposal, but host-side controls remain the authoritative execution boundary.
- **Prefer narrow semantic operations.** Small operations are easier to validate, govern, test, and audit than broad arbitrary-execution primitives.

The samples intentionally omit production infrastructure that is not required to make the lesson visible. Follow each canonical README's **What This Sample Intentionally Omits** section before adapting a sample to a real system.

## Working Implementations

After a sample or lab, compare the deliberately small teaching architecture with fuller implementations:

- [AsiBackbone](https://github.com/AsiBackbone/AsiBackbone) — governance and policy-control implementation.
- [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate) — ASP.NET Core reference architecture.

The goal is not to make the sample imitate production complexity. The goal is to make the architectural boundary recognizable before you inspect how broader operational concerns change it.

## Source of Truth and Licensing

The detailed sample READMEs remain canonical under `samples/`; this DocFX page is a navigation and learning-path summary. When sample setup, behavior, or invariants change, update the canonical sample README first and keep this landing page's summary aligned with it.

Documentation and educational content in `docs/` are licensed under **CC BY 4.0**. Executable sample code and sample projects under `samples/` are licensed under the **MIT License** unless otherwise noted.

See [LICENSING.md](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md) for the repository's component-specific licensing policy.

---

> **Read it. Run it. Question it. Improve it.**
