---
description: Explore security architecture for trust boundaries, least privilege, secret handling, secure logging, replay protection, cryptographic evidence, supply-chain integrity, and fail-safe behavior.
---

# Security

The Security section examines architectural boundaries that can reduce accidental authority, hidden execution paths, unsafe defaults, and ambiguous control flow.

Security in ASI Backbone Learning is approached as an architectural responsibility rather than a single feature or package.

> **Section status:** Focused security learning now covers trust boundaries, least privilege, secret handling, secure logging, replay protection, cryptographic evidence boundaries, and software supply-chain integrity. Start with [Trust Boundaries and Least Privilege](trust-boundaries-and-least-privilege.md), continue with [Secret Handling Across Trust Boundaries](secret-handling-across-trust-boundaries.md) to follow authority-bearing values through custody, delivery, use, rotation, and revocation, then use [Secure Logging Across Trust Boundaries](secure-logging-across-trust-boundaries.md) to examine observability as an outbound data boundary. Continue with [Replay Protection and Bounded-Use Authority](replay-protection-and-bounded-use.md), [Signing, Verification, Key Custody, and Tamper Evidence](signing-verification-key-custody-and-tamper-evidence.md), and [Software Supply-Chain Integrity for .NET Repositories](software-supply-chain-integrity-for-dotnet-repositories.md) before using the [Foundational Tutorials](../tutorials/index.md) to connect security boundaries to governed execution.

> **A secure boundary should remain visible when the system is under pressure.**

## Start Here

[Trust Boundaries and Least Privilege](trust-boundaries-and-least-privilege.md) is the first focused security tutorial. It treats trust boundaries as changes in control over data or authority and least privilege as an architectural constraint on what authority crosses those boundaries.

The tutorial connects caller-supplied versus authoritative context, authentication, authorization, policy decisions, credential ownership, narrow authority, boundary validation, resource ownership, and fail-safe behavior.

[Secret Handling Across Trust Boundaries](secret-handling-across-trust-boundaries.md) extends the configuration and least-privilege material into a full credential lifecycle. It treats passwords, API keys, client secrets, database credentials, tokens, and private keys as authority-bearing values whose creation, custody, delivery, runtime use, rotation, revocation, compromise response, and removal cross distinct trust boundaries.

[Secure Logging Across Trust Boundaries](secure-logging-across-trust-boundaries.md) applies the same boundary reasoning to operational telemetry. It builds on the ASP.NET Core [Structured Logging Without Sensitive-Data Sprawl](../aspnetcore/structured-logging-without-sensitive-data-sprawl.md) article without duplicating its `ILogger` and event-design guidance, concentrating instead on data minimization before emission, provider/export trust, collector and storage access, tenant separation, retention, degraded observability, and the boundary between operational logs and governance evidence.

[Replay Protection and Bounded-Use Authority](replay-protection-and-bounded-use.md) continues from narrow authority into stateful execution-boundary enforcement. It covers one-time and bounded-use grants, atomic consumption, multi-instance and restart behavior, durable replay state, failure windows, request idempotency, and why replay resistance is not an exactly-once execution guarantee.

Run the [Replay Protection and Bounded-Use Authority sample](https://github.com/AsiBackbone/Learning/blob/main/samples/replay-protection-and-bounded-use/README.md) to compare a deterministic check-then-act race with atomic in-process consumption, then use the [intermediate concurrency lab](../labs/replay-protection-and-bounded-use.md) to repair and extend the boundary.

[Signing, Verification, Key Custody, and Tamper Evidence](signing-verification-key-custody-and-tamper-evidence.md) adds the cryptographic evidence boundary. It separates hashes from signatures, signing from verification and authorization, key custody from ordinary application configuration, normal rotation from compromise, and tamper evidence from tamper prevention.

[Software Supply-Chain Integrity for .NET Repositories](software-supply-chain-integrity-for-dotnet-repositories.md) extends those trust questions into the process that creates and publishes software. It uses current Learning, AsiBackbone, and NetCoreApplicationTemplate repository practices as selective specimens for workflow permissions, SHA-pinned actions, dependency management, locked restore, package validation, SBOMs, attestations, publication authority, and precise provenance claims.

## Security Themes

Current and future material may examine:

- [Trust boundaries](trust-boundaries-and-least-privilege.md)
- [Least privilege](trust-boundaries-and-least-privilege.md)
- [Secure logging across trust boundaries](secure-logging-across-trust-boundaries.md)
- Explicit execution boundaries
- Short-lived authority
- Actor and resource binding
- [Replay resistance and bounded-use authority](replay-protection-and-bounded-use.md)
- [Signing, verification, key custody, and tamper evidence](signing-verification-key-custody-and-tamper-evidence.md)
- Input validation
- [Secret handling across trust boundaries](secret-handling-across-trust-boundaries.md)
- Egress control
- Safe defaults
- Rate limiting
- Authentication and authorization boundaries
- Audit evidence
- Failure behavior
- Degraded-mode operation
- [Software supply-chain integrity for .NET repositories](software-supply-chain-integrity-for-dotnet-repositories.md)
- AI tool-execution risks

## Approval Is Not Unlimited Authority

A recurring security principle in the foundational material is:

```text
Allowed Decision
   ≠
Broad Standing Permission
```

An operation that has been approved may still benefit from authority that is:

* Narrow
* Bound to a specific actor
* Bound to a specific operation
* Bound to a specific resource
* Bound to an intended executor or audience
* Time-limited
* Revalidated before execution

This concept is explored in:

[Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md)

## Security and AI-Assisted Systems

For AI-assisted workflows, prompt instructions are not treated as security controls.

The central boundary remains:

> **The model may propose. The host retains execution authority.**

Host-side code remains responsible for:

* Tool allowlists
* Argument validation
* Authoritative context
* Policy evaluation
* Secret ownership
* Destination and egress controls
* Capability validation
* Real-world execution

See:

[Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md)

## Related Foundational Material

* [Decision Before Execution](../tutorials/decision-before-execution.md)
* [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md)
* [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md)
* [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md)

## Working References

Security-related implementation examples can be studied in:

* [AsiBackbone](https://github.com/AsiBackbone/AsiBackbone)
* [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

The first focuses on governed decision and execution boundaries.

The second provides a broader ASP.NET Core reference architecture with secure application defaults and operational controls.

## Scope

The material in this section is educational.

It does not constitute:

* A security certification
* A penetration test
* A formal threat model
* A compliance assessment
* A guarantee that a demonstrated pattern is sufficient for production

Application-specific security analysis remains necessary.

## Current Status

The Security section now has focused material for trust boundaries and least privilege, secret handling, secure logging, replay protection and bounded-use authority, signing/verification with key-custody and tamper-evidence boundaries, and software supply-chain integrity. Together they establish a security architecture path from identifying where trust changes, to reducing the lifetime and distribution of authority-bearing secrets, to deciding what data may safely cross into observability systems, to preserving narrow authority, to controlling whether authority may be consumed again, to deciding what cryptographic evidence can safely establish, and finally to applying the same trust-boundary reasoning to source, dependencies, CI, generated artifacts, and publication.

Future material will extend into threat modeling as architecture reasoning.

Use the [Foundational Tutorials](../tutorials/index.md) to connect these security concepts to the existing governed-execution learning path.

---

> **Read it. Run it. Question it. Improve it.**