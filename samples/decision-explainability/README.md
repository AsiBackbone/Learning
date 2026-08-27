# Decision Explainability for Human Operators Sample

This sample is the executable companion to [Decision Explainability for Human Operators](../../docs/advanced/decision-explainability-for-human-operators.md).

It demonstrates a narrow architectural boundary:

> **Human explanation is a projection of structured governance evidence. It is not the evidence itself and it does not create policy or execution authority.**

## What the Sample Models

The sample begins after a fictional governance decision already exists.

```text
Structured decision evidence
        |
        v
Versioned explanation projector
        +
Explicit audience
        |
        v
Minimized human explanation
```

The projector uses:

- `DecisionId`
- explicit `DecisionOutcome`
- stable reason codes
- policy identity/version references
- deterministic display priority
- audience-specific approved templates
- catalog-owned disclosure metadata
- explicit disclosure status
- a versioned projection rule

The source evidence may also contain a fictional protected context value. The projector never inspects that payload field to decide whether disclosure is complete or withheld; that decision belongs to the recipient-owned reason catalog. The raw value exists only so tests can prove it is never copied into explanation text.

The projection also retains `SourceReasonCodes` and `SourcePolicies` as trusted internal lineage. Those fields are not a recommendation to serialize the complete projection unchanged to every audience. A production host should expose only the audience-approved surface appropriate to its API or UI trust boundary.

## Run It

From the repository root:

```bash
dotnet run --project samples/decision-explainability/DecisionExplainability/DecisionExplainability.csproj
```

Run the focused tests with:

```bash
dotnet test samples/decision-explainability/DecisionExplainability.Tests/DecisionExplainability.Tests.csproj
```

Or validate the complete sample solution:

```bash
dotnet build samples/Samples.slnx
dotnet test samples/Samples.slnx
```

## Scenarios

The console sample prints projections for:

- a regional data-residency denial for an end user;
- the same decision for an operator;
- a deferred decision caused by unavailable current context;
- a denial with multiple contributing reasons.

The same structured decision can produce different audience-appropriate wording while preserving the same decision identity, outcome, reason lineage, and policy provenance. The teaching projector varies some headlines by audience (`Allowed` and `Denied`) while intentionally sharing the headline for `Deferred`, `AcknowledgmentRequired`, and `EscalationRecommended`; audience differences often live primarily in detail and disclosure.

## Reason Catalog

The teaching projector includes a deliberately small reason catalog:

| Reason code | Teaching purpose | End-user disclosure | Operator disclosure |
| --- | --- | --- | --- |
| `regional.data-residency` | Denial with protected source context | Safe regional explanation | Bounded regional explanation |
| `tenant.operation-restricted` | Operator-only contributing reason | Withheld with disclosure notice | Operator detail |
| `dependency.current-context-unavailable` | Deferred rather than denied | Explicitly says this is not policy denial | Operator diagnostic wording |
| `ack.bulk-impact` | Acknowledgment-required outcome | Describes required step, not approval | Operator workflow wording |
| `review.security` | Escalation/review outcome | Does not promise approval | Operator review-path wording |
| `policy.currently-allows` | Allowed outcome | Does not claim execution occurred | Does not claim execution occurred |
| `security.sensitive-signal` | Security/fraud-style reason that remains bounded even for operators | Withheld | Withheld |

The catalog is an explanation rule set, not a policy engine. Adding a template cannot change the decision outcome.

## Focused Invariants

The test suite verifies:

- protected source values do not appear in end-user or operator explanation text;
- policy identity and version remain attached to the projection lineage;
- `Deferred` is not explained as policy denial;
- `AcknowledgmentRequired` is not explained as approval;
- escalation does not promise approval;
- multiple reasons use deterministic presentation order;
- incoming reason-list order does not change the projection;
- projection preserves the original decision identity and outcome across every teaching scenario and both audiences;
- audience changes presentation without changing decision lineage;
- disclosure status is owned by the catalog rather than inferred from source payload fields;
- operator-only reason detail is withheld from end users and disclosed as withheld;
- a security-sensitive reason can remain withheld even from the operator projection;
- an all-withheld projection falls back to safe generic detail;
- intentional withholding and unmapped reasons can be reported at the same time;
- unknown reason codes use a safe fallback and mark the explanation incomplete;
- multiple unknown reason codes produce one aggregate fallback sentence while preserving every source reason code in lineage;
- reason/outcome mismatches are marked incomplete rather than rendered as a contradictory cause;
- the projection version is explicit;
- a later policy decision does not rewrite the historical projection;
- `Allowed` does not imply that protected execution already occurred.

## What the Sample Does Not Model

This is not a production explainability service.

It intentionally does not provide:

- policy evaluation;
- durable governance evidence storage;
- localization/resource management;
- a production data-classification or redaction framework;
- authentication or authorization for explanation viewers;
- a generative model;
- a legal/compliance explanation standard;
- protected execution.

The audience enum is a teaching input, not proof that the caller is allowed to request that view. A production host must authorize access before selecting an audience profile. The sample models only `EndUser` and `Operator`; reviewer/support profiles are an extension, and production operator profiles may themselves withhold sensitive fraud, security, or tenant-isolation detail.

If generated natural-language summaries are added later, they should consume a bounded projection such as this one rather than raw logs or unrestricted policy inputs, and the generated text should remain replaceable presentation rather than canonical governance evidence.

> **Read it. Run it. Question it. Improve it.**
