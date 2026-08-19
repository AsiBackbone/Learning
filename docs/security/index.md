# Security

The Security section examines architectural boundaries that can reduce accidental authority, hidden execution paths, unsafe defaults, and ambiguous control flow.

Security in ASI Backbone Learning is approached as an architectural responsibility rather than a single feature or package.

> **Section status:** Focused security learning now covers trust boundaries, least privilege, and replay protection. Start with [Trust Boundaries and Least Privilege](trust-boundaries-and-least-privilege.md), continue with [Replay Protection and Bounded-Use Authority](replay-protection-and-bounded-use.md), then use the [Foundational Tutorials](../tutorials/index.md) to connect security boundaries to governed execution.

> **A secure boundary should remain visible when the system is under pressure.**

## Start Here

[Trust Boundaries and Least Privilege](trust-boundaries-and-least-privilege.md) is the first focused security tutorial. It treats trust boundaries as changes in control over data or authority and least privilege as an architectural constraint on what authority crosses those boundaries.

The tutorial connects caller-supplied versus authoritative context, authentication, authorization, policy decisions, credential ownership, narrow authority, boundary validation, resource ownership, and fail-safe behavior.

[Replay Protection and Bounded-Use Authority](replay-protection-and-bounded-use.md) continues from narrow authority into stateful execution-boundary enforcement. It covers one-time and bounded-use grants, atomic consumption, multi-instance and restart behavior, durable replay state, failure windows, request idempotency, and why replay resistance is not an exactly-once execution guarantee.

## Security Themes

Current and future material may examine:

- [Trust boundaries](trust-boundaries-and-least-privilege.md)
- [Least privilege](trust-boundaries-and-least-privilege.md)
- Explicit execution boundaries
- Short-lived authority
- Actor and resource binding
- [Replay resistance and bounded-use authority](replay-protection-and-bounded-use.md)
- Input validation
- Secret isolation
- Egress control
- Safe defaults
- Rate limiting
- Authentication and authorization boundaries
- Audit evidence
- Failure behavior
- Degraded-mode operation
- Dependency and supply-chain considerations
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

The Security section now has focused tutorials for trust boundaries and least privilege, plus replay protection and bounded-use authority. Together they establish the first security architecture path from identifying where trust changes to preserving narrow authority and controlling whether that authority may be consumed again.

Future material will extend into capability-based authority, signing and verification, key custody, tamper-evident records, secure logging, dependency integrity, and threat modeling.

Use the [Foundational Tutorials](../tutorials/index.md) to connect these security concepts to the existing governed-execution learning path.

---

> **Read it. Run it. Question it. Improve it.**