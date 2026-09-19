---
description: Understand the Learning 1.0 and AsiBackbone 6.0 baseline, changes from 5.x, and where authoritative implementation behavior is documented.
---

# Learning 1.0 and AsiBackbone 6.0 Compatibility Guide

> **Production baseline:** Learning 1.0 documents and teaches the AsiBackbone 6.0 production surface. Earlier Learning releases remain historical educational records and may reference APIs or terminology that were valid in earlier AsiBackbone release lines.

Learning 1.0 is the educational companion to AsiBackbone 6.0. That alignment means current Learning terminology, API-facing examples, and implementation links are interpreted against the `release/6.0.0` product baseline.

It does **not** mean that Learning depends on the AsiBackbone packages. Most Learning samples remain framework-neutral teaching models, and the architecture lessons are intended to remain useful even when you implement them without AsiBackbone.

This guide explains the version relationship and the educational impact of the 6.0 transition. For exact package syntax, runtime behavior, and the complete migration inventory, use the implementation repository.

## Version Relationship

| Material | How to interpret it |
| --- | --- |
| **Learning 1.0** | Current production architecture education aligned with AsiBackbone 6.0 terminology and public API concepts. |
| **AsiBackbone 6.0** | Authoritative implementation baseline for package IDs, namespaces, public types, members, defaults, runtime behavior, compatibility, and migration details. |
| **Earlier Learning releases** | Historical educational records. Their architecture explanations may still be useful, but API-facing examples can reflect the AsiBackbone release line current when they were published. |
| **AsiBackbone 5.x material** | Historical implementation guidance. Translate package API syntax through the 5.x-to-6.0 migration guidance before using it in current code. |

The ownership rule is intentionally simple:

> **Learning teaches the architecture. AsiBackbone defines the released API and runtime truth.**

The implementation repository documents the same boundary in its [Documentation Ownership](https://github.com/AsiBackbone/AsiBackbone/blob/release/6.0.0/docs/articles/documentation-ownership.md) guidance.

## What Changed Conceptually

The 6.0 transition refines terminology without changing the central governed-execution model taught by Learning.

### Decision receipt replaces audit residue in current teaching

Learning 1.0 uses **decision receipt** for the structured record of a policy decision, its outcome, and its reasons. The 6.0 product API likewise renamed the `AuditResidue` family to the `DecisionReceipt` family.

A decision receipt is intentionally narrower than a claim of execution proof:

- it records what evaluation produced;
- later acknowledgment, capability, persistence, gateway, and execution evidence can correlate with it;
- it does not by itself prove that the host executed the protected operation;
- it does not by itself imply durable storage, signing, immutability, or tamper evidence.

Use **audit ledger**, **outbox**, **signing**, and **execution evidence** when those distinct responsibilities are what you mean.

### Acknowledgment is the ordinary educational term

Learning now prefers **acknowledgment** for the architectural concept: an identified actor or system responds to a defined challenge or responsibility statement before continuation.

The 6.0 Core API intentionally retains `LiabilityHandshakeRequest` and `LiabilityHandshakeAcknowledgment` for the concrete multi-step protocol. ASP.NET Core challenge types use the shorter `AcknowledgmentChallenge` naming.

The distinction remains the same:

> **Acknowledgment ≠ authorization ≠ execution authority.**

The retained word `Liability` in exact Core type names does not claim legal protection, legal advice, or automatic transfer of responsibility.

### Product type names are more domain-focused

AsiBackbone 6.0 removes redundant product-name prefixes from many public types and uses domain qualifiers where they communicate architectural role. Current Learning prose therefore emphasizes familiar concepts such as **context**, **constraint**, **decision**, **evaluator**, **receipt**, and **actor**, while exact API examples use the finalized 6.0 names.

Examples include:

| 5.x API name | 6.0 API name |
| --- | --- |
| `IAsiBackboneConstraint<TContext>` | `IGovernanceConstraint<TContext>` |
| `AsiBackboneConstraintEvaluationContext` | `GovernanceEvaluationContext` |
| `IAsiBackbonePolicyEvaluator<TContext>` | `IGovernancePolicyEvaluator<TContext>` |
| `DefaultAsiBackbonePolicyEvaluator<TContext>` | `DefaultGovernancePolicyEvaluator<TContext>` |
| `AsiBackbonePolicyEvaluatorBuilder<TContext>` | `GovernancePolicyEvaluatorBuilder<TContext>` |
| `AsiBackbonePolicyEvaluatorOptions` | `GovernancePolicyOptions` |
| `AsiBackboneActorContext` | `GovernanceActorContext` |
| `AuditResidue` | `DecisionReceipt` |
| `IAsiBackboneAuditSink` | `IDecisionReceiptSink` |
| `IAsiBackboneEndpointGovernanceService` | `IEndpointGovernanceService` |
| `IAsiBackboneAcknowledgmentChallengeService` | `IAcknowledgmentChallengeService` |
| `RequireGovernancePolicyAttribute` | `GovernancePolicyAttribute` |

This is a representative teaching-oriented subset, not the complete rename inventory. Use the authoritative [6.0 public API naming convention](https://github.com/AsiBackbone/AsiBackbone/blob/release/6.0.0/docs/articles/public-api-naming-600.md) for the full list.

### Learning can stay simpler than the product surface

Learning commonly teaches five workflow outcomes:

```text
Allow
Deny
Defer
Require acknowledgment
Escalate
```

The released 6.0 product also includes `GovernanceDecisionOutcome.Warning`. That product-specific continuation-with-warning state does not require every foundational Learning example to expand to six outcomes.

The teaching model is allowed to be smaller when the simplification is explicit and does not misrepresent package syntax.

## What Changed in Code Examples

Two categories matter most when translating 5.x package examples.

### Use the finalized 6.0 public names

Current package-facing snippets should use the 6.0 names and namespaces. The [AsiBackbone 6.0 API Boundary](asibackbone-6-api-boundary.md) lists the high-frequency types used by Learning and shows supported evaluator construction and endpoint metadata examples.

Learning-owned sample types remain local teaching models unless a section is explicitly labeled **AsiBackbone 6.0 API**.

### Removed 5.x compatibility members are no longer callable

AsiBackbone 6.0 removes exactly seven public members whose obsolete compatibility windows ended at the major-version boundary. For Learning readers, the practical impact is concentrated in two areas:

- the five partial `DefaultAsiBackbonePolicyEvaluator<TContext>` constructors are gone; use `DefaultGovernancePolicyEvaluator.CreateBuilder<TContext>()` or the supported full-dependency constructor;
- the two `RequireGovernancePolicy(...)` route-builder extension methods are gone; use `MarkGovernancePolicy(...)` instead.

The 5.x `RequireGovernancePolicyAttribute` type was also renamed to `GovernancePolicyAttribute` in 6.0 so the attribute and route-builder paths use the same marker terminology. That type rename is separate from the seven obsolete-member removals.

Do not use this page as the complete implementation migration checklist. The authoritative [Upgrade from 5.x to 6.0](https://github.com/AsiBackbone/AsiBackbone/blob/release/6.0.0/docs/articles/upgrade-500-to-600.md) guide contains the exact removed-member inventory, replacement guidance, dependency-injection notes, and complete public type rename table.

## What Did Not Change

The major-version transition does not change the architecture boundaries that Learning is built around.

The current model is still:

```text
Proposed intent
   ↓
Authoritative policy context
   ↓
Constraints
   ↓
Explicit decision
   ↓
Decision receipt
   ↓
Acknowledgment when required
   ↓
Scoped authority when required
   ↓
Host-owned execution
   ↓
Correlated lifecycle evidence
```

The durable ideas are:

- policy evaluation produces decision data; it does not perform the protected side effect;
- a denied decision must not reach the executor;
- acknowledgment does not silently become authorization or execution authority;
- scoped capability is bounded authority that must still be validated at the execution boundary;
- the host retains control of real-world side effects;
- AI tool calls are proposals until a trusted host evaluates and authorizes the operation;
- decision evidence, persistence, outbox delivery, signing, telemetry, and execution are related but distinct responsibilities;
- Learning samples can remain framework-neutral even while current implementation references align to AsiBackbone 6.0.

Those concepts remain the reason Learning can teach the architecture independently of one package release.

## How to Read Older Learning and 5.x Material

Older content is useful when read in version context.

| When you encounter... | Read it this way |
| --- | --- |
| `AuditResidue` or **audit residue** in an older package example | Historical 5.x naming for what current Learning and the 6.0 API call a decision receipt. Preserve the old wording when discussing the historical release itself. |
| `IAsiBackbone*`, `DefaultAsiBackbonePolicyEvaluator`, or other product-prefixed public types | Treat them as 5.x API names and translate them through the 6.0 naming and migration guides before copying code. |
| `RequireGovernancePolicy(...)` on a route builder or `RequireGovernancePolicyAttribute` on an endpoint | Treat them as historical 5.x names. Current 6.0 route metadata uses `MarkGovernancePolicy(...)`, and attribute-based metadata uses `GovernancePolicyAttribute`. |
| A Learning sample that declares its own context, decision, receipt, acknowledgment, capability, or gateway type | Treat it as a teaching model unless the page explicitly labels the snippet as AsiBackbone 6.0 API. |
| A historical release note, tag, or archived page | Read it as evidence of what that release taught or implemented at the time, not as the current production API contract. |
| A conceptual statement about decision-before-execution, acknowledgment, scoped authority, or host-owned execution | Treat the concept as current unless a newer Learning page explicitly revises it. Verify package-specific behavior in AsiBackbone 6.0. |

Do not rewrite historical 5.x release records so they appear to have used 6.0 names. Versioned history is useful precisely because it preserves the contract and vocabulary that existed at that point in time.

## Where to Verify Exact Behavior

Use these sources in order, depending on the question:

1. [Learning Architecture Glossary](../architecture/glossary.md) — canonical Learning definitions and teaching vocabulary.
2. [AsiBackbone 6.0 API Boundary](asibackbone-6-api-boundary.md) — the high-frequency 6.0 package names and examples that Learning readers are most likely to copy.
3. [Upgrade from 5.x to 6.0](https://github.com/AsiBackbone/AsiBackbone/blob/release/6.0.0/docs/articles/upgrade-500-to-600.md) — authoritative breaking-change and migration guidance.
4. [6.0 Public API Naming Convention](https://github.com/AsiBackbone/AsiBackbone/blob/release/6.0.0/docs/articles/public-api-naming-600.md) — complete public type rename inventory and retained-name decisions.
5. [AsiBackbone API Terminology Map](https://github.com/AsiBackbone/AsiBackbone/blob/release/6.0.0/docs/articles/terminology-map.md) — mapping from Learning concepts to concrete product APIs.
6. [AsiBackbone API Glossary](https://github.com/AsiBackbone/AsiBackbone/blob/release/6.0.0/docs/articles/glossary.md) — implementation-specific meanings, invariants, and host responsibilities.
7. [Documentation Ownership](https://github.com/AsiBackbone/AsiBackbone/blob/release/6.0.0/docs/articles/documentation-ownership.md) — the source-of-truth boundary between the two repositories.

When those sources overlap, exact API/runtime behavior belongs to `AsiBackbone/AsiBackbone`; architecture teaching and canonical Learning terminology belong to `AsiBackbone/Learning`.

## Migration Checklist for Learning Readers

If you are moving a code example or internal document from an AsiBackbone 5.x baseline to the Learning 1.0 / AsiBackbone 6.0 baseline:

- [ ] Decide whether the material is architecture teaching or exact package usage.
- [ ] Keep framework-neutral Learning sample types labeled as teaching models.
- [ ] Replace 5.x package type names with the finalized 6.0 names where the snippet uses the real product API.
- [ ] Replace removed partial evaluator constructors with the builder or full-dependency constructor.
- [ ] Replace removed route-builder `RequireGovernancePolicy(...)` calls with `MarkGovernancePolicy(...)`.
- [ ] Replace 5.x `RequireGovernancePolicyAttribute` usage with `GovernancePolicyAttribute`.
- [ ] Use **decision receipt** in current teaching prose while preserving historical wording in historical release material.
- [ ] Use **acknowledgment** as the ordinary teaching term and reserve **handshake** for the actual protocol or exact retained type names.
- [ ] Keep implementation links pinned to `release/6.0.0` when documenting the Learning 1.0 production baseline.
- [ ] Verify exact behavior in the implementation repository instead of copying a second runtime contract into Learning.

## Continue

If you are learning the architecture rather than migrating package code, continue with [Decision Before Execution](../tutorials/decision-before-execution.md).

If you are copying AsiBackbone package syntax, continue with the [AsiBackbone 6.0 API Boundary](asibackbone-6-api-boundary.md), then use the implementation repository for exact API and runtime details.
