---
description: Learn how to derive audience-appropriate human explanations from structured governance evidence without turning presentation text into policy truth or execution authority.
---

# Decision Explainability for Human Operators

**Learning objective:** Understand how to project structured governance decisions, reason codes, policy provenance, and disclosure constraints into useful human explanations while preserving the original evidence as the authoritative record.

**Pattern classification:** General learning material

**Advanced area:** Decision explainability and evidence projection

**Difficulty:** Advanced

**Required prerequisites:** [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md) and [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md).

**Recommended background:** [AI Governance Observability and End-to-End Decision Tracing](../ai-integration/ai-governance-observability-and-end-to-end-decision-tracing.md), [Structured Logging Without Sensitive-Data Sprawl](../aspnetcore/structured-logging-without-sensitive-data-sprawl.md), and [Secure Logging Across Trust Boundaries](../security/secure-logging-across-trust-boundaries.md).

**Glossary:** [Audit residue](../architecture/glossary.md#audit-residue), [decision provenance](../architecture/glossary.md#decision-provenance), [governed execution](../architecture/glossary.md#governed-execution), and [trust boundary](../architecture/glossary.md#trust-boundary).

> **Scope:** This article treats explanation as a derived presentation layer over structured governance evidence. It does not define a legal right-to-explanation standard, a universal explanation schema, a localization framework, a production redaction engine, or a generative-AI product architecture.

The central lesson is:

> **Audit evidence records what the governed system decided and why in structured terms. A human explanation is a presentation derived from that evidence - not a replacement for the evidence and not a new source of authority.**

---

## 1. Assumptions and Non-Goals

This treatment assumes that a governed system already preserves structured decision information such as:

- Decision identity.
- Explicit decision outcome.
- Stable reason codes.
- Policy identity and version.
- Correlation identity where useful.
- Contributing policy or constraint evidence where several rules participated.
- Enough historical evidence to reconstruct the decision without relying on a natural-language message.

The article does **not** assume that every audience may see every underlying fact. It also does not assume that a reason code is already user-facing language.

The design goal is narrower:

```text
Structured governance evidence
        |
        v
Versioned explanation projection
        |
        v
Audience-appropriate presentation
```

not:

```text
Natural-language message
        |
        v
Treated as policy truth
```

An explanation may be useful, incomplete, translated, reformatted, summarized, or regenerated later. The historical decision evidence should remain independently reconstructable.

---

## 2. Keep Five Different Artifacts Distinct

Several artifacts can describe the same governed event without serving the same purpose.

| Artifact | Primary purpose | Typical consumer | Authority? |
| --- | --- | --- | --- |
| Reason code | Stable machine-readable reason identity | Policy code, tests, diagnostics, evidence processors | No |
| Decision provenance / audit evidence | Reconstruct what was decided under which policy/context state | Auditors, governance services, incident review | Historical evidence, not execution authority by itself |
| Telemetry | Observe runtime behavior and diagnose system operation | Operators, SRE, support tooling | No |
| Explanation projection | Select, order, redact, and phrase decision evidence for a defined audience | UI/API/support/operator surface | No |
| Generated natural-language summary | Optional presentation of an already bounded projection | Human reader | No |

The distinction matters because each artifact answers a different question.

A reason code may say:

```text
regional.data-residency
```

Decision provenance may add:

```text
DecisionId = dec-1042
Outcome = Denied
PolicyId = customer-export
PolicyVersion = 7.3
ReasonCode = regional.data-residency
```

Telemetry may add:

```text
TraceId = trace-88
PolicyEvaluationMs = 17
```

A human explanation may say:

> This operation cannot proceed because regional data-handling requirements apply.

Those are related records. They are not interchangeable records.

---

## 3. A Reason Code Is Not an Explanation

A stable reason code is valuable because software can test and correlate it without parsing prose.

For example:

```text
regional.data-residency
```

can remain stable while the operator-facing presentation changes from:

```text
Regional data-residency requirements block this export.
```

to:

```text
This export is blocked by the active regional data-handling policy.
```

or while the end-user presentation becomes less detailed:

```text
This operation cannot proceed because regional data-handling requirements apply.
```

The reason code should not be overloaded with every sentence the system may ever need to display.

Likewise, the explanation should not become the value that downstream code compares in order to decide what happened.

A useful rule is:

```text
Reason code = stable semantic identifier
Explanation = audience-specific presentation
```

The explanation may evolve without rewriting the historical reason identity.

---

## 4. Build Explanations From Structured Inputs

The explanation layer should consume structured evidence rather than scrape log text or infer a reason from an arbitrary message.

A teaching model might look like:

```csharp
public enum DecisionOutcome
{
    Allowed,
    Denied,
    Deferred,
    AcknowledgmentRequired,
    EscalationRecommended
}

public sealed record PolicyReference(
    string PolicyId,
    string PolicyVersion);

public sealed record ReasonEvidence(
    string ReasonCode,
    int DisplayPriority,
    PolicyReference Policy,
    string? ProtectedContextValue);

public sealed record DecisionEvidence(
    string DecisionId,
    DecisionOutcome Outcome,
    IReadOnlyList<ReasonEvidence> Reasons,
    string CorrelationId,
    DateTimeOffset DecidedAtUtc);
```

The explanation projector can then produce a separate object:

```csharp
public sealed record ExplanationProjection(
    string DecisionId,
    DecisionOutcome Outcome,
    string ProjectionVersion,
    ExplanationAudience Audience,
    string Headline,
    IReadOnlyList<string> Details,
    IReadOnlyList<string> SourceReasonCodes,
    IReadOnlyList<PolicyReference> SourcePolicies,
    DisclosureStatus DisclosureStatus,
    string? DisclosureNotice,
    string CorrelationId,
    DateTimeOffset DecidedAtUtc);
```

Notice what did **not** happen:

```text
Explanation text
        |
        v
Decision outcome changed
```

Projection is downstream from the decision. It cannot broaden, narrow, approve, deny, acknowledge, or execute the operation.

---

## 5. Audience Is Part of the Projection Contract

An operator, support engineer, reviewer, and affected end user may all need different information.

That does not mean the system has four different historical decisions.

A useful teaching shape is:

```text
One decision record
        |
        +--> End-user explanation
        |
        +--> Operator explanation
```

The companion sample models those two audience profiles. A production system may add reviewer, support, regulator, or other bounded profiles without changing the historical decision.

The audience profile controls presentation and disclosure. It should not control the underlying outcome.

For example, an operator might legitimately see:

```text
Denied by customer-export policy 7.3 because regional.data-residency matched.
```

while an end user sees:

```text
This operation cannot proceed because regional data-handling requirements apply.
```

Both presentations should still point back to the same decision identity and policy provenance inside the trusted system.

Audience differences may affect detail more than headline. The teaching projector uses distinct end-user/operator headlines for `Allowed` and `Denied`, but intentionally shares the headline for `Deferred`, `AcknowledgmentRequired`, and `EscalationRecommended` while varying the approved detail text.

The sample catalog includes a fictional `security.sensitive-signal` reason whose approved text is withheld even from the operator profile. "Operator" should never be interpreted as "may see every policy input."

The audience should be explicit data, not inferred from whether a caller requested a verbose message.

---

## 6. Disclosure Rules Belong to the Projection Boundary

Do not let incoming decision evidence decide for itself what is safe to disclose.

For example, this is a weak pattern:

```text
Reason evidence says Sensitive = false
        |
        v
UI prints protected detail
```

A safer design keeps disclosure rules in a recipient-owned explanation catalog or policy:

```text
Reason code
        +
Audience
        +
Versioned disclosure rules
        |
        v
Approved presentation
```

That catalog may define:

- Which reason codes have end-user wording.
- Which have operator-only wording.
- Which protected values are never copied into explanation text.
- Whether a disclosure notice is required.
- Whether an unmapped reason makes the explanation incomplete.

The companion sample follows this design. The explanation catalog, not the incoming evidence payload, declares whether a particular audience-facing template intentionally withholds known detail. `ProtectedContextValue` is deliberately present in source evidence only so tests can prove that the projection never copies the raw value into human-facing text; the projector does not inspect that field to decide disclosure status. One catalog entry intentionally withholds reason-specific text from both end users and operators to make the bounded-operator case executable rather than merely theoretical.

---

## 7. Withholding Can Be Truthful Without Being Misleading

A system may legitimately know more than it can disclose.

Reasons include:

- Privacy.
- Tenant isolation.
- Security-sensitive detection logic.
- Fraud or abuse signals.
- Internal infrastructure state.
- Protected legal or contractual data.

The explanation should not solve that problem by inventing a different cause.

Bad:

```text
Actual reason: protected fraud signal
Displayed explanation: network error
```

The displayed text is now false.

Better:

```text
The request cannot proceed under the current policy.
Some decision details are intentionally withheld for this audience.
```

A projection can therefore carry explicit disclosure state:

```text
Complete
PartiallyWithheld
Incomplete
PartiallyWithheldAndIncomplete
```

`PartiallyWithheld` means the explanation intentionally omits some known detail.

`Incomplete` means the projector cannot fully explain one or more structured reasons, for example because the current explanation catalog has no approved mapping for a reason code.

`PartiallyWithheldAndIncomplete` preserves both facts when a projection intentionally withholds known detail and also cannot map another reason. Those states should not be silently collapsed into one another.

---

## 8. Example: Denied by Regional Data-Residency Policy

The issue scenario is:

```text
Decision = Denied
ReasonCode = regional.data-residency
```

Suppose the evidence also contains a protected internal fact:

```text
ProtectedContextValue = exact-jurisdiction-and-storage-route
```

That protected value may be useful inside the policy or evidence boundary. It does not automatically belong in an end-user explanation.

A safe projection could be:

```text
Outcome: Denied
Headline: This operation cannot proceed.
Detail: This operation cannot proceed because regional data-handling requirements apply.
Disclosure: PartiallyWithheld
```

An operator with a broader but still bounded profile might receive:

```text
Outcome: Denied
Headline: The governed decision denied the operation.
Detail: The active regional data-residency rule blocked this operation.
Policy: customer-export 7.3
Disclosure: PartiallyWithheld
```

The protected route value is omitted from both examples.

This demonstrates an important distinction:

> **An operator-facing explanation may be more detailed without becoming a raw dump of policy inputs.**

---

## 9. Deferred Must Not Be Explained as Denied

A deferred outcome is not a softer spelling of denial.

For example:

```text
Decision = Deferred
ReasonCode = dependency.current-context-unavailable
```

means the system could not establish information required to make or continue the decision safely.

A useful explanation is:

> The request is deferred because required current information could not be established. This is not a policy denial.

A misleading explanation would be:

> Your request violates policy.

Nothing in the structured outcome supports that claim.

The distinction matters operationally because the next step differs:

```text
Denied
   |
   v
Current policy says no
```

versus:

```text
Deferred
   |
   v
Required decision information is not currently established
```

An explanation layer must preserve that semantic difference.

---

## 10. Explain Different Outcomes Differently

A single generic message such as `Request could not be completed` throws away useful governance semantics.

A projection should preserve the meaning of the outcome.

| Outcome | Explanation emphasis | What not to imply |
| --- | --- | --- |
| `Allowed` | The governed decision currently permits continuation | Permanent permission or completed execution |
| `Denied` | Current policy does not permit the operation | Technical outage or retry guarantee |
| `Deferred` | Required information/condition is not established now | Policy denial |
| `AcknowledgmentRequired` | A specific acknowledgment step is required before reevaluation/continuation | Approval or pre-issued execution authority |
| `EscalationRecommended` | Another review/authority path is required or recommended | Guaranteed approval |

For acknowledgment:

```text
Acknowledgment required
        |
        v
Explain the required human/system step
        |
        v
Do not say "approved"
```

For escalation:

```text
Escalation recommended
        |
        v
Explain that additional review is required
        |
        v
Do not promise the review outcome
```

The explanation should help a person understand the state transition without inventing authority the decision did not create.

---

## 11. Multiple Reasons Need Explicit Presentation Semantics

A governed decision may contain several contributing reasons:

```text
Denied
  |
  +-- regional.data-residency
  +-- tenant.operation-restricted
  +-- resource.classification-restricted
```

The explanation layer should not arbitrarily choose whichever reason happened to be first in a dictionary or list.

Legitimate designs include:

- Preserve all applicable reasons.
- Designate one reason as primary in the decision evidence.
- Use an explicit display priority that is part of the projection contract.
- Group several reasons under a safe higher-level explanation while preserving the complete source-reason set internally.

The companion sample uses `DisplayPriority`, then reason code as a deterministic tie-breaker.

That ordering is a **presentation rule**, not policy precedence.

This is important:

```text
Display first
!=
Policy had greater authority
```

If policy precedence matters to the decision, that fact belongs in decision/provenance evidence rather than being inferred from explanation order.

---

## 12. Preserve Contributing Policy Provenance

An explanation should remain traceable to the policy evidence that produced the decision.

For a multi-policy result, preserve references such as:

```text
customer-export 7.3
regional-baseline 12.1
tenant-controls 4.0
```

The human-facing end-user message may not display those identifiers. The trusted projection object can still preserve them so operators and reviewers can connect the presentation back to the historical evidence.

Useful internal fields include:

```text
DecisionId
SourceReasonCodes[]
SourcePolicyReferences[]
ProjectionVersion
Audience
CorrelationId
```

The projection should not replace the evidence store with only those fields. They are lineage references, not a complete governance receipt.

---

## 13. Policy Drift Does Not Rewrite Historical Explanations

Suppose decision `dec-1042` was produced by:

```text
Policy = customer-export 7.3
Outcome = Denied
Reason = regional.data-residency
```

Later, policy `8.0` changes the rule.

Two different questions now exist:

1. What explains the historical decision made under `7.3`?
2. What would current policy `8.0` decide now?

Do not answer question 1 by silently reevaluating the request under `8.0`.

A historical explanation should remain bound to the historical decision evidence:

```text
Historical decision evidence 7.3
        |
        v
Historical explanation projection
```

A current reevaluation should create a new decision identity and new provenance:

```text
Current context
        +
Current policy 8.0
        |
        v
New decision
        |
        v
New explanation
```

This is the same provenance discipline used elsewhere in Learning: preserve historical truth and evaluate freshness/current state separately.

---

## 14. Version the Explanation Projection When It Matters

Explanation wording can change even when policy does not.

Changes may come from:

- Better wording.
- Localization.
- New audience profiles.
- Revised disclosure rules.
- Accessibility improvements.
- New reason-code mappings.

When deterministic reconstruction matters, record a projection version:

```text
DecisionId = dec-1042
ProjectionVersion = decision-explanation-v1
Audience = EndUser
```

A versioned projection rule helps answer:

> Why did the system show this wording to this audience at that time?

It does not turn the explanation template into policy.

A useful separation is:

```text
Policy version
        |
        v
Why the governance outcome existed

Projection version
        |
        v
How evidence was presented to an audience
```

Those versions have different jobs.

---

## 15. Deterministic Reconstruction Is Better Than Ad Hoc String Building

Avoid explanation code scattered across controllers:

```csharp
if (decision.ReasonCode == "regional.data-residency")
{
    message = "Something about region...";
}
```

A dedicated projector makes the behavior inspectable and testable:

```text
Decision evidence
        |
        v
Known projection version
        |
        v
Reason catalog + disclosure rules
        |
        v
Deterministic ordered projection
```

Benefits include:

- Stable tests.
- Explicit audience policy.
- Reviewable redaction rules.
- Consistent fallback behavior.
- Historical reconstruction where required.
- Fewer opportunities for one UI path to leak more than another.

Deterministic does not mean every audience sees identical text. It means the same structured input, audience, and projection version produce the same approved projection.

---

## 16. Telemetry, Evidence, and Explanation Should Cross Different Boundaries Deliberately

Telemetry is optimized for operations. Governance evidence is optimized for decision/lifecycle reconstruction. Explanation is optimized for human comprehension.

A useful separation is:

```text
Governance engine
    |
    +--> Decision evidence store
    |
    +--> Operational telemetry
    |
    +--> Explanation projector
```

Do not make the explanation projector scrape a log backend for its truth.

Operational telemetry may be:

- Sampled.
- Rotated.
- Filtered.
- Retained for a shorter period.
- Visible to a different operator population.

Those properties make telemetry a poor canonical explanation source for consequential governance decisions.

Likewise, do not copy the entire evidence record into logs just because the explanation feature needs it. The logging guidance still applies: minimize data before it crosses the observability boundary.

---

## 17. Technical Failure and Governance Outcome Are Different Facts

A technical failure can prevent the system from establishing the inputs needed for governance.

For example:

```text
Current-context dependency unavailable
        |
        v
Governance result = Deferred
```

The explanation may say:

> Required current information could not be established, so the request is deferred.

The telemetry may separately say:

```text
Dependency = resource-classification-service
Failure = timeout
RetryCount = 2
```

Whether that dependency name is safe to expose to an end user is a disclosure decision.

The important rule is:

> **Do not convert a diagnostic failure into a governance cause that the decision evidence does not support.**

Similarly, an internal exception message should not automatically become the human explanation.

---

## 18. Unknown Reasons Need Safe, Honest Fallbacks

A projector may receive a valid structured reason code that its current catalog does not know.

Unsafe choices include:

- Guessing what the reason probably means.
- Printing an internal payload verbatim.
- Dropping the reason and pretending the explanation is complete.

A safer fallback is:

```text
The governed decision contains additional information that this explanation profile cannot currently describe.
```

and mark the projection:

```text
DisclosureStatus = Incomplete
```

The source reason code should remain available inside the trusted lineage so the mapping gap can be diagnosed.

Unknown explanation mapping is an explanation-layer defect or compatibility condition. It does not invalidate or alter the underlying governance decision.

The same rule applies when a known reason appears with an outcome that the projection catalog does not consider compatible. The projector should not emit denial wording for a `Deferred` record merely because it recognizes the reason code. Mark the projection incomplete and preserve the structured mismatch for diagnosis rather than fabricating a coherent story.

---

## 19. Generated Natural-Language Summaries Are Optional Presentation

A system may choose to use a language model or another generator to make a projection easier to read.

That generator should receive a **bounded explanation projection**, not unrestricted application state or an instruction to determine why the decision happened.

Preferred direction:

```text
Structured decision evidence
        |
        v
Deterministic explanation projection
        |
        v
Bounded allowed facts
        |
        v
Optional generated summary
```

Not:

```text
Raw logs + request + database state
        |
        v
"Explain why this was denied"
        |
        v
Generated text treated as truth
```

If generated summaries are used, useful controls include:

- Provide only allowlisted projected facts.
- Include decision identity and projection version outside the generated prose.
- Prevent the generator from changing the outcome.
- Prevent the generator from adding undisclosed policy facts.
- Preserve the structured projection used as grounding input.
- Treat generated text as replaceable presentation.
- Fall back to deterministic templates when generation fails or cannot be trusted.

The companion sample deliberately stops at deterministic projection. It does not call a model.

---

## 20. A Generated Explanation Must Not Become Authority

A particularly dangerous inversion is:

```text
Generated text says "approved"
        |
        v
Host treats request as allowed
```

The correct direction remains:

```text
Authoritative governance decision
        |
        v
Explanation projection
        |
        v
Optional generated wording
```

No downstream authorization or execution component should parse the explanation to recover the outcome.

Use the structured decision object for machine behavior.

This keeps the familiar ASI Backbone boundary intact:

> **Presentation may describe authority. Presentation does not create authority.**

---

## 21. When a Simple Reason Message Is Enough

Not every decision needs an explanation subsystem.

A simple stable reason code plus a fixed safe message may be sufficient when:

- There is one policy source.
- There is one audience.
- Reasons contain no sensitive context.
- No localization/versioned presentation requirement exists.
- The decision is immediate and low consequence.
- Historical reconstruction of wording is not required.

For example:

```json
{
  "outcome": "Denied",
  "reasonCode": "feature.disabled",
  "message": "This operation is disabled."
}
```

may be entirely adequate.

Use a richer projection layer when the problem actually contains audience, provenance, disclosure, multi-policy, historical, or generated-summary concerns.

Complexity should be earned by those requirements.

---

## 22. Failure Modes and Threats

| Failure mode | Why it matters | Safer direction |
| --- | --- | --- |
| Explanation replaces reason code | Software begins parsing prose | Preserve machine-readable reasons separately |
| Explanation replaces evidence | Historical reconstruction becomes presentation-dependent | Keep canonical decision/provenance evidence |
| Raw policy inputs copied to UI | Sensitive data crosses a new trust boundary | Use allowlisted templates and minimization |
| End-user and operator views share unrestricted payload | Audience boundaries become cosmetic | Project separately for each audience |
| First reason wins by collection order | Presentation becomes nondeterministic and misleading | Use explicit display semantics |
| Deferred rendered as denied | Technical uncertainty becomes false policy claim | Preserve outcome semantics |
| Policy update rewrites old explanation | Historical decision appears to change | Bind explanation to historical decision/version |
| Unknown reason silently omitted | Explanation appears complete when it is not | Mark incomplete and preserve source reason identity |
| Generated summary invents rationale | Presentation creates unsupported facts | Ground generation in bounded structured projection |
| Explanation parsed for execution | Presentation becomes an authority channel | Machine behavior consumes structured decision only |
| Debug telemetry becomes explanation source | Sampled/retained operational data is mistaken for evidence | Project from governance evidence |
| Redaction occurs after raw data leaves trust boundary | Sensitive value may already be exposed | Minimize before projection/export |

The explanation layer deserves security review because it is an outbound information boundary even though it is not an execution boundary.

---

## 23. Design Checklist

Before adding a human explanation surface, ask:

- What structured decision artifact is authoritative?
- Which reason codes contributed?
- Which policy identities and versions produced them?
- Is reason ordering semantic or merely presentational?
- Which audiences exist?
- Which fields may each audience see?
- Which protected facts should never cross the explanation boundary?
- How is intentional withholding represented honestly?
- How is an unknown reason represented?
- Does `Deferred` remain distinct from `Denied`?
- Does `AcknowledgmentRequired` remain distinct from approval?
- Does escalation avoid promising approval?
- Is the projection versioned when deterministic reconstruction matters?
- Can historical explanations remain tied to historical policy evidence?
- Are telemetry and evidence still separate stores/concepts?
- If generation is used, what bounded projection grounds it?
- Can any machine authorization path consume explanation text? It should not.
- Would a simple fixed message solve the actual problem with less machinery?

---

## 24. Companion Sample

A small runnable sample accompanies this article:

[Decision Explainability sample](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-explainability/README.md)

The sample uses fictional decision evidence and a deterministic in-memory explanation projector. It demonstrates:

```text
Decision evidence
        |
        v
Versioned reason catalog
        +
Audience profile
        |
        v
Minimized explanation projection
```

It does not implement a policy engine, production redaction service, localization framework, legal explanation standard, or generative model.

The sample is intentionally downstream from governance. It cannot change the decision or invoke an executor.

---

## 25. Executable Coverage

The companion tests make the presentation boundaries explicit.

| Invariant | Companion test |
| --- | --- |
| Regional data-residency denial produces a useful end-user explanation without exposing the protected source value. | ✅ `RegionalDataResidencyDenialProducesSafeEndUserExplanation` |
| Operator projection preserves policy identity/version without dumping protected source context. | ✅ `OperatorProjectionPreservesPolicyIdentityAndVersionWithoutProtectedContext` |
| Deferred explanation does not claim policy denial. | ✅ `DeferredExplanationDoesNotClaimPolicyDenial` |
| Acknowledgment-required explanation does not claim approval or execution permission. | ✅ `AcknowledgmentRequiredExplanationDoesNotClaimApproval` |
| Escalation explanation does not promise approval. | ✅ `EscalationExplanationDoesNotPromiseApproval` |
| Multiple contributing reasons remain visible in deterministic display order. | ✅ `MultipleReasonsUseDeterministicPresentationOrder` |
| Input collection order does not change the projection. | ✅ `ReasonInputOrderDoesNotChangeProjection` |
| Projection preserves the source `DecisionId` and `Outcome` across all six teaching scenarios and both audiences. | ✅ `ProjectionPreservesDecisionIdentityAndOutcome` |
| Audience changes presentation/disclosure without changing decision lineage. | ✅ `AudienceChangesPresentationButNotDecisionLineage` |
| Disclosure status is owned by the catalog rather than inferred from the protected payload field. | ✅ `DisclosureStatusIsCatalogOwnedRatherThanPayloadDriven` |
| Operator-only reason detail is withheld from the end-user projection and marked. | ✅ `OperatorOnlyReasonIsWithheldFromEndUserProjection` |
| A security-sensitive reason can remain intentionally withheld even from the operator projection. | ✅ `SensitiveSignalCanBeWithheldFromOperatorProjection` |
| An all-withheld projection uses a safe default detail rather than inventing hidden content. | ✅ `AllWithheldReasonsUseSafeDefaultDetail` |
| A projection that is both intentionally withheld and unmapped preserves both disclosure facts. | ✅ `WithheldAndUnmappedReasonsPreserveBothDisclosureFacts` |
| Unknown reason uses a safe fallback and marks the projection incomplete. | ✅ `UnknownReasonUsesSafeFallbackAndMarksProjectionIncomplete` |
| Multiple unmapped reasons produce one aggregate fallback sentence while preserving every source reason code in lineage. | ✅ `MultipleUnknownReasonsUseOneAggregateFallback` |
| A recognized reason paired with an incompatible outcome does not produce contradictory explanation text. | ✅ `ReasonOutcomeMismatchDoesNotRenderContradictoryCause` |
| Projection version is explicit and stable for the teaching projector. | ✅ `ProjectionVersionIsExplicit` |
| A later policy decision does not rewrite an already projected historical decision. | ✅ `NewPolicyDecisionDoesNotRewriteHistoricalProjection` |
| Allowed explanation describes current permission without claiming execution occurred. | ✅ `AllowedExplanationDoesNotClaimExecutionOccurred` |

### Intentionally not modeled

| Boundary | Status |
| --- | --- |
| Production policy evaluation | ◐ Not modeled; the sample begins with fictional structured decision evidence. |
| Localization/resource management | ◐ Not modeled. |
| Production data-classification/redaction framework | ◐ Not modeled; disclosure is represented by a small teaching catalog. |
| Role/identity authorization for viewing explanations | ◐ Not modeled; audience is an explicit input, not an authentication system. |
| Rich reviewer/support audience profiles | ◐ Not modeled; the teaching enum contains only `EndUser` and `Operator`, and production profiles may be narrower than either. |
| Generative model summaries | ◐ Not modeled; the article defines grounding requirements, while the sample remains deterministic. |
| Legal/compliance explanation obligations | ◐ Not modeled. |
| Durable evidence storage | ◐ Not modeled; inputs are in-memory teaching records. |
| Protected execution | ◐ Not modeled; explanation is presentation only. |

---

## 26. Related Learning

Continue with:

- [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md) for policy identity, historical evidence, and drift.
- [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) for decision, acknowledgment, and evidence separation.
- [AI Governance Observability and End-to-End Decision Tracing](../ai-integration/ai-governance-observability-and-end-to-end-decision-tracing.md) for telemetry and end-to-end correlation.
- [Structured Logging Without Sensitive-Data Sprawl](../aspnetcore/structured-logging-without-sensitive-data-sprawl.md) for minimizing operational event data.
- [Secure Logging Across Trust Boundaries](../security/secure-logging-across-trust-boundaries.md) for provider, transport, storage, access, and retention boundaries around telemetry.
- [Constraint Composition and Policy Precedence](../governance/constraint-composition-and-policy-precedence.md) when several policy layers contribute to one result.
- [Federated Governance and Independent Authority Coordination](federated-governance-and-independent-authority-coordination.md) when several independently governed authorities contribute to one decision.

---

## 27. Closing Principle

A governed system should be able to explain a decision without turning the explanation into the decision.

The durable direction is:

```text
Structured decision evidence
        |
        v
Versioned, audience-aware projection
        |
        v
Useful human explanation
```

while preserving:

```text
Explanation
!= reason-code identity
!= policy provenance
!= audit evidence
!= telemetry
!= authorization
!= execution authority
```

The explanation is valuable precisely because the underlying evidence remains stronger than the prose built from it.

---

> **Read it. Run it. Question it. Improve it.**
