# Federated Governance Coordination Sample

This sample is the runnable companion for [Federated Governance and Independent Authority Coordination](../../docs/advanced/federated-governance-and-independent-authority-coordination.md).

It keeps the federation problem deliberately small and local. Two fictional governance domains contribute to a `records.transfer` decision, while the sample makes authority-set resolution, contribution health, deterministic composition, coordinator failure, and authority-set drift observable through ordinary .NET code and xUnit tests.

> **The sample demonstrates composition invariants. It does not define a federation protocol or production governance service.**

## Central Flow

```text
Current resource + destination
        |
        v
Authority-set resolution
        |
        +-- LocalOnly  -> local policy
        |
        +-- Federated  -> required authority contributions
                             |
                             v
                       Versioned contract
                             |
                             v
                       Federated outcome
```

The resolver runs before coordinator availability is considered. That ordering protects a key invariant:

> **An outage cannot reclassify a federated operation as local-only.**

## What the Sample Models

The sample implements one deliberately narrow composition strategy: every required authority must provide an acceptable contribution. Domain contributions use `AuthorityOutcome.Allow / Deny / Defer`; the composed result uses `FederatedOutcome.Allowed / Denied / Deferred`. The contract separately chooses whether valid peer disagreement uses `DenialWins`, `PreserveConflict`, or `RouteToEscalation`; the resulting federated outcomes are `Denied`, `Conflict`, or `EscalationRecommended`.

It demonstrates:

- `ContributionStatus` separate from semantic `AuthorityOutcome`.
- List-based current authority sets rather than hardcoded source/destination fields.
- Order-independent composition.
- `Unavailable` remaining distinct from both federated `Allowed` and `Denied`.
- Explicit handling of invalid contributions.
- Authority-set identity/version preserved in the federated decision.
- Old composite decisions becoming stale after region/resource movement.
- Coordinator outage preserving a federated requirement.
- Legitimate local-only continuation only when current facts classified the operation as local before the outage.
- Contract-defined `Conflict`, denial-dominant, and escalation behavior.

## Important Simplifications

The sample does not model:

- Network calls between authorities.
- Authentication or signatures for contributions.
- Byzantine or malicious consensus.
- Cached contribution storage.
- Delegated override grants.
- Production policy engines.
- Protected side effects or executor credentials.
- Multi-region durability.

Those concerns remain article-level architecture responsibilities.

## Run the Sample

From the repository root:

```bash
dotnet run --project samples/federated-governance-coordination/FederatedGovernanceCoordination/FederatedGovernanceCoordination.csproj
```

The console demonstrates:

- all-required-allow behavior;
- peer conflict;
- an unavailable required authority;
- coordinator failure for a federated operation; and
- a pre-classified local-only operation during the same coordinator outage.

## Run the Tests

```bash
dotnet test samples/federated-governance-coordination/FederatedGovernanceCoordination.Tests/FederatedGovernanceCoordination.Tests.csproj
```

The focused suite proves these invariants:

- all required domain `Allow` contributions produce federated `Allowed`;
- permuting the same peer contributions does not change the result;
- an unavailable required authority does not become federated `Denied` accidentally;
- an unavailable required authority does not become federated `Allowed` accidentally;
- an invalid contribution produces an explicit `Deferred` result and stable reason code;
- authority-set drift makes an old federated decision stale;
- coordinator failure cannot turn a cross-region operation into local-only work;
- an operation already classified as local-only can remain independent of coordinator availability;
- a `PreserveConflict` contract keeps peer disagreement explicit as `Conflict`;
- a `DenialWins` contract can choose denial dominance instead; and
- a `RouteToEscalation` contract can route peer disagreement to `EscalationRecommended`.

## Why There Is No Executor Here

This sample stops at the federated decision boundary on purpose.

The article teaches that a later execution host must still revalidate current resource state, the current authority set, and whatever continuation authority the protected operation requires. Those execution concerns are already demonstrated elsewhere in Learning, especially [Cross-System Capability Exchange and Delegated Authority](../../docs/advanced/cross-system-capability-exchange-and-delegated-authority.md) and [Scoped Capability and Host-Owned Execution](../../docs/tutorials/scoped-capability-and-host-owned-execution.md).

The purpose here is narrower: make federation composition deterministic and failure-aware before execution enters the picture.

---

> **Read it. Run it. Question it. Improve it.**
