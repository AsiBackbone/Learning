# Governed Failure-Injection Trace Sample

This sample is an executable companion for the governance and policy architecture material in AsiBackbone Learning.

**Learning objective:** Inject one failure at a time into a fictional governed operation and see which trust boundary stopped it, which component owned that refusal, whether execution authority was issued, and whether the protected executor ran.

**Difficulty:** Intermediate

**Pattern classification:** General learning material

Useful prerequisites:

- [Decision Before Execution](../../docs/tutorials/decision-before-execution.md)
- [Decision Receipts and Acknowledgment](../../docs/tutorials/decision-receipts-and-acknowledgment.md)
- [Scoped Capability and Host-Owned Execution](../../docs/tutorials/scoped-capability-and-host-owned-execution.md)

The central boundary is:

> **Every way a governed operation can fail stops at a named stage, owned by a named component, before the protected executor runs. Only the valid path reaches the executor, and it does so exactly once.**

## How This Relates to the Policy Simulation Harness

The [Minimal Policy Simulation Harness](../policy-simulation-harness/README.md) and the [Policy Simulation and Change-Impact Analysis lab](../../docs/labs/policy-simulation-and-change-impact-analysis.md) stop deliberately at the policy decision. They show how one intent produces different decisions as context or policy changes, and they never own an executor.

This sample starts where that boundary ends. It keeps the policy deliberately small and asks a different question: once a decision exists, what else can go wrong between the decision and the side effect, and who catches it?

| | Policy Simulation Harness | Governed Failure-Injection Trace |
| --- | --- | --- |
| Question | What would policy decide if this input changed? | Where does this operation stop, and who stopped it? |
| Stages covered | Policy selection, constraint evaluation, decision composition | Intent through to the protected executor |
| Owns an executor | No, by design | Yes, a counting executor that performs nothing |
| Varies | Region, tenant, risk, environment, policy version | One injected failure per run |

Use the harness to reason about policy behavior. Use this sample to reason about the boundaries around it.

## The Stage Trace

Every run records the same seven stages:

```text
intent                            GovernedEntryPoint
  -> authoritative context        AuthoritativeContextStore
  -> policy decision              PolicyEvaluator
  -> continuation verification    ContinuationVerifier
  -> scoped execution authority   AuthorityIssuer
  -> host enforcement             ProtectedHost
  -> protected executor           ProtectedExecutor
```

Each stage ends in one status:

| Status | Meaning |
| --- | --- |
| `Completed` | The stage did its job and progress continued |
| `Refused` | The stage stopped progress |
| `Deferred` | The stage could not decide now; progress waits |
| `Unavailable` | The stage could not supply what it owns |
| `AwaitingContinuation` | Progress needs a further step, such as an acknowledgment |
| `NotReached` | An earlier stage stopped the run |
| `SkippedByCaller` | The caller went around the stage entirely |

## Run the Sample

From the repository root, run every scenario:

```bash
dotnet run --project samples/governed-failure-injection-trace/Sample/GovernedFailureInjectionTrace.csproj
```

List the scenarios:

```bash
dotnet run --project samples/governed-failure-injection-trace/Sample/GovernedFailureInjectionTrace.csproj -- --list
```

Run one or more named scenarios:

```bash
dotnet run --project samples/governed-failure-injection-trace/Sample/GovernedFailureInjectionTrace.csproj -- --scenario receipt-tampered --scenario bypass-entry-point
```

Emit structured JSON instead of text:

```bash
dotnet run --project samples/governed-failure-injection-trace/Sample/GovernedFailureInjectionTrace.csproj -- --scenario authority-replayed --format json
```

The command never prompts for input. It exits with `0` when every selected scenario behaved as expected, `1` when one did not, and `2` for an unknown scenario or argument, so it can run unchanged in CI.

## The Scenarios

Every scenario uses the same fictional `customer.export` of `customer-batch-42` by `analyst-7`, and injects exactly one failure.

| Scenario | Injected failure | Stops at | Owner | Outcome | Reason code | Authority issued | Executor runs |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `allowed-executes-once` | None | — | — | `Executed` | `execution.completed` | Yes | 1 |
| `policy-denied` | Context reports high risk | Policy decision | `PolicyEvaluator` | `Denied` | `export.risk-high` | No | 0 |
| `context-unavailable` | Context store cannot answer | Policy decision | `PolicyEvaluator` | `Deferred` | `context.unavailable` | No | 0 |
| `acknowledgment-missing` | Required acknowledgment withheld | Continuation verification | `ContinuationVerifier` | `ContinuationRequired` | `continuation.acknowledgment-missing` | No | 0 |
| `acknowledged-continuation` | None; EU export with valid acknowledgment | — | — | `Executed` | `execution.completed` | Yes | 1 |
| `receipt-tampered` | Receipt outcome flipped to `Allowed` | Continuation verification | `ContinuationVerifier` | `VerificationFailed` | `receipt.seal-mismatch` | No | 0 |
| `receipt-expired` | Continuation arrives after receipt expiry | Continuation verification | `ContinuationVerifier` | `VerificationFailed` | `receipt.expired` | No | 0 |
| `receipt-intent-mismatch` | Intact receipt for a different intent | Continuation verification | `ContinuationVerifier` | `VerificationFailed` | `receipt.intent-mismatch` | No | 0 |
| `authority-expired` | Host reached after authority expired | Host enforcement | `ProtectedHost` | `EnforcementRefused` | `authority.expired` | Yes | 0 |
| `authority-replayed` | Authority already consumed | Host enforcement | `ProtectedHost` | `EnforcementRefused` | `authority.already-used` | Yes | 0 |
| `authority-wrong-audience` | Authority bound to another host | Host enforcement | `ProtectedHost` | `EnforcementRefused` | `authority.audience-mismatch` | Yes | 0 |
| `authority-wrong-operation` | Authority bound to another operation | Host enforcement | `ProtectedHost` | `EnforcementRefused` | `authority.operation-mismatch` | Yes | 0 |
| `bypass-entry-point` | Caller skips the entry point with fabricated authority | Host enforcement | `ProtectedHost` | `EnforcementRefused` | `authority.not-issued` | No | 0 |

## Reading a Trace

A tampered receipt produces:

```text
Scenario: receipt-tampered
Injected failure: TamperReceipt
  The caller flips the receipt outcome to Allowed to skip acknowledgment; the seal no longer matches.
Stage trace:
  1. Intent                    GovernedEntryPoint         Completed            intent.accepted
  2. AuthoritativeContext      AuthoritativeContextStore  Completed            context.loaded
  3. PolicyDecision            PolicyEvaluator            AwaitingContinuation export.region-eu-acknowledgment
  4. ContinuationVerification  ContinuationVerifier       Refused              receipt.seal-mismatch  <- stopped here
  5. AuthorityIssuance         AuthorityIssuer            NotReached           -
  6. HostEnforcement           ProtectedHost              NotReached           -
  7. ProtectedExecutor         ProtectedExecutor          NotReached           -
Outcome: VerificationFailed (receipt.seal-mismatch)
First stopping stage: ContinuationVerification, owned by ContinuationVerifier
Execution authority issued: no
Protected executor invocations: 0
Decision evidence: rcpt-intent-5310 AcknowledgmentRequired export.region-eu-acknowledgment (customer-export@1.0)
Execution evidence: none; the protected executor did not run
Expectation: met
```

Read it from the bottom up. The executor did not run, no authority existed, and the run stopped at verification. The decision evidence still says what policy actually concluded, `AcknowledgmentRequired`, not what the altered receipt claimed.

The JSON output carries the same fields, so a script or test can assert on them directly.

## Five Kinds of Refusal

The trace keeps refusals apart instead of reporting every failure as a denial:

- **Policy denial** (`Denied`, stopped by `PolicyEvaluator`): current policy forbids the operation. It is an answer, and retrying it unchanged will not help.
- **Inability to decide** (`Deferred`, stopped by `PolicyEvaluator`): the authoritative context was unavailable, so policy explicitly declined to decide. The context stage shows `Unavailable`, and the decision evidence records `Deferred`, not `Denied`.
- **Unsatisfied continuation** (`ContinuationRequired`, stopped by `ContinuationVerifier`): the decision was valid but required an acknowledgment that has not been given. Progress waits.
- **Verification failure** (`VerificationFailed`, stopped by `ContinuationVerifier`): the decision evidence presented with the continuation was altered, expired, or bound to a different intent. No authority is issued.
- **Host enforcement** (`EnforcementRefused`, stopped by `ProtectedHost`): authority exists but is expired, already used, bound to the wrong host or operation, or was never issued at all.

Policy refusal, verification failure, and host enforcement have different owners. The tests assert that, so a change that moves a check to the wrong component fails visibly.

## A Bypass Is Not a Denial

In `bypass-entry-point`, the caller never enters the governed path. Every stage before host enforcement shows `SkippedByCaller`, and there is no decision evidence because no policy decision was ever made.

The host refuses anyway. It does not trust that the caller came through the entry point; it validates whatever authority it is handed. That is the difference from `policy-denied`, where the governed path ran and policy said no. A denial is the system working as designed. A bypass is an attempt to avoid the design, caught by the one boundary the caller cannot skip.

## Decision Evidence and Execution Evidence

Each trace keeps two records apart:

- **Decision evidence** is the receipt: what policy concluded, why, and under which policy version. It exists whenever policy ran, including when the run later stopped.
- **Execution evidence** exists only when the protected executor actually ran. It references the authority and the receipt it came from.

In `authority-expired`, the decision evidence says `Allowed` and there is no execution evidence. Both are true: policy allowed the operation, and the host still refused to perform it. An audit that read only the decision would get this wrong.

## Simulated Verification Boundaries

Two parts of this sample stand in for mechanisms a production system implements differently. They are labeled `SIMULATED` in the code.

- **`SimulatedReceiptSeal` is not production cryptography.** It is an unkeyed SHA-256 digest of the receipt's fields. It shows where a verifier detects an altered receipt, but anyone who can read the fields can recompute it, so it cannot detect a forged one. Production systems use a keyed signature or MAC with managed keys, or keep the receipt in a store the caller cannot write.
- **`IssuedAuthorityRegistry` stands in for validating an issuer's signature.** The host asks whether the issuer recorded the authority identifier. A production host validates a signed grant from a trusted issuer instead.

`AuthorityUseStore` is also single-process. A production bounded-use store must make the consume step atomic and durable across every host instance. The [Replay Protection and Bounded-Use Authority sample](../replay-protection-and-bounded-use/README.md) covers that boundary in depth.

For the released implementation types that correspond to these responsibilities, see [From Learning Samples to a Production Host](../../docs/getting-started/from-learning-samples-to-production-host.md).

## Runtime Failure Injection Is Not Compile-Time Analysis

This sample injects failures at runtime and observes how the boundaries respond. It does not demonstrate compile-time analyzers, which find unsafe patterns in source code before anything runs. The two are complementary and neither substitutes for the other. AsiBackbone's analyzer additions are outside the reviewed implementation boundary for this Learning release; see [Learning 1.3.0 Release Readiness](../../docs/getting-started/learning-1-3-release-readiness.md) for that scope.

## Run the Tests

Run the focused test project:

```bash
dotnet test samples/governed-failure-injection-trace/Tests/GovernedFailureInjectionTrace.Tests.csproj
```

Or run the complete Learning sample suite:

```bash
dotnet test samples/Samples.slnx
```

## Architectural Invariant Tests

The test project verifies that:

1. Every scenario produces its declared outcome, reason code, and executor invocation count.
2. Every blocked, invalid, expired, replayed, or bypass path invokes the protected executor zero times and produces no execution evidence.
3. Each valid path invokes the executor exactly once.
4. The scenario catalog covers every injected failure, and scenario names are unique.
5. Policy denial stops before authority issuance.
6. Unavailable context produces an explicit `Deferred` decision, not a denial.
7. A missing acknowledgment waits for continuation rather than failing verification.
8. Policy refusal, verification failure, and host enforcement are owned by different components.
9. Verification failures never issue authority, and invalid authority is issued but refused by the host.
10. A bypass skips every governed stage, records no decision, and is still refused by the host.
11. Replaying consumed authority against the same host executes only once, and a refused attempt does not consume authority.
12. Decision evidence and execution evidence are separate records.
13. The simulated seal detects a change to any sealed field.
14. Runs are deterministic, and the CLI runs every scenario non-interactively, emits valid JSON, and rejects invalid arguments.

These tests make the teaching contract executable. They are not a production conformance suite, a penetration test, or a substitute for an implementation repository's own tests.

## What This Sample Intentionally Omits

This sample does not implement:

- Authentication or ordinary authorization.
- Real customer data or a real export.
- External policy engines, stores, or networks.
- Production signing, key management, or token formats.
- Durable or distributed replay protection.
- Human acknowledgment user interfaces.
- Retries, outboxes, or reconciliation of ambiguous side effects.
- A browser or graphical view; the text and JSON traces are the interface.

Those concerns would make the sample more realistic but would hide the specific lesson.

## Related Material

- [Minimal Policy Simulation Harness sample](../policy-simulation-harness/README.md) - compare decisions without an executor.
- [Policy Simulation and Change-Impact Analysis lab](../../docs/labs/policy-simulation-and-change-impact-analysis.md) - practice change-impact analysis on policy.
- [Decision Receipts and Acknowledgment](../../docs/tutorials/decision-receipts-and-acknowledgment.md) - the continuation and receipt boundary.
- [Scoped Capability and Host-Owned Execution](../../docs/tutorials/scoped-capability-and-host-owned-execution.md) - the authority and host-enforcement boundary.
- [Governed AI Tool Gateway lab](../../docs/labs/governed-ai-tool-gateway.md) - the same boundaries around model-proposed actions.
- [How to Test That a Denied Operation Never Executes](../../docs/articles/2026/test-denied-operation-never-executes.md) - the zero-execution assertion pattern these tests use.

---

> **Read it. Run it. Question it. Improve it.**
