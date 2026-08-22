# Lab Architectural Acceptance Criteria Template

Use this authoring template when creating or revising a hands-on lab.

The purpose is to help learners answer a question that functional success alone cannot answer:

> **Did I preserve and demonstrate the architectural boundary this lab was intended to teach?**

Copy the section below into the lab and specialize it to the exercise. Remove criteria that do not apply and add domain-specific criteria where the lab requires stronger evidence.

Do not turn the criteria into a prescribed solution when multiple implementations can preserve the same architectural property.

---

## Architectural Acceptance Criteria

You have completed the architectural objective of this lab when you can demonstrate all applicable criteria:

- [ ] **Required boundary demonstrated** — `<name the responsibility, trust, decision, authority, persistence, or execution boundary that must remain explicit>`.
- [ ] **Prohibited path absent** — `<identify the bypass, hidden side effect, privilege broadening, stale-authority path, or other behavior that must not occur>`.
- [ ] **Decision or lifecycle evidence preserved** — `<state what outcome, reason, correlation, provenance, acknowledgment, capability, or execution evidence must remain observable>`.
- [ ] **Relevant failure path observable** — `<name at least one denied, invalid, stale, unavailable, replayed, conflicting, or partial-failure scenario the learner must be able to reproduce or reason about>`.
- [ ] **Architectural invariant verified** — `<name the focused test, deterministic observation, or other evidence that demonstrates the property rather than merely asserting it>`.
- [ ] **Tradeoff or alternative explained** — `<ask the learner to explain one credible alternative, simpler design, operational cost, or reason the demonstrated pattern may not fit another system>`.

### Evidence

For each applicable criterion, identify the evidence you used:

```text
Criterion:
Evidence:
Why the evidence demonstrates the boundary:
```

Evidence may include:

- a focused invariant test,
- deterministic sample output,
- a before/after failure observation,
- a diagram or trace showing responsibility flow,
- persisted or correlated lifecycle evidence,
- a code review of the execution or trust boundary,
- or a concise written explanation of the tradeoff.

Passing a broad test suite is useful, but it should not be the only evidence when the lab is teaching an architectural property that can be violated while functional behavior still appears correct.

### Alternative Implementations

If more than one implementation can satisfy the lab, state that explicitly.

For example:

> The acceptance criteria describe the required boundary and observable behavior. They do not require a particular class layout, persistence provider, policy engine, or framework package.

### Completion Statement

A concise completion statement can use this form:

> I can show where the required boundary is enforced, demonstrate that the prohibited path does not cross it, reproduce the relevant failure behavior, verify the invariant with explicit evidence, and explain at least one tradeoff or alternative.
