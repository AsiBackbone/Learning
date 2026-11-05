---
description: Learn which changes should invalidate an earlier authorization or approval before delayed execution, and how versions, expiry, and freshness rules bound reuse.
title: Your Security Check Passed. Then the World Changed.
author: Christopher D. Cavell
published: "2026-11-05"
summary: An earlier authorization or approval stays true as history, but it does not say whether the operation may run now. Bind each decision to exactly what it was about, re-read current preconditions at the point of use, write down what happens when the rules or the decider change, use expiry only for facts you cannot observe, and close the gap between check and use with a conditional write.
feed: true
x_hashtags:
  - DotNet
  - CyberSecurity
---

# Your Security Check Passed. Then the World Changed.

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** No formal prerequisites. Familiarity with ASP.NET Core authorization, EF Core, and HTTP conditional requests is helpful. No AsiBackbone package or prior Learning material is required.

**Sample scope:** The C# targets .NET 8 or later and uses EF Core's `ExecuteUpdateAsync`, available from EF Core 7. The stores, directory, renderer, and test harness are illustrative interfaces and fakes, not a runnable sample or a recommended persistence API. Framework and standards behavior is described from the vendors' documentation as of November 2026; check the current documentation for your versions.

**What this article covers:** why a check that passed earlier is still true as history but says nothing about now; six kinds of change and which of them should stop a delayed operation; why invalidating on every change is its own failure; five ways to bound how long a decision may be reused, and what each is for; why expiry is the weakest of them; closing the gap at the moment a person approves and at the moment the operation runs; keeping the original decision intact while recording the new one; tests for each kind of change; and when ordinary authorization at the point of use is the whole design.

**In plain terms:** an inspector approves a building's plans on Monday. On Wednesday the builder moves a load-bearing wall. On Thursday the fire code changes. On Friday a clerk issues the occupancy permit because the file says "Approved." The inspector did nothing wrong, and the approval really happened. But it was an approval of Monday's plans under Monday's code, and the clerk read it as a statement about Friday's building. The fix is not to make approvals expire faster. It is to record what was approved, and have the clerk check that it still describes the building in front of them.

Consider a hypothetical B2B software company. Sales reps build quotes in an ASP.NET Core application. A discount above 20% needs a sales manager's approval, and each manager may approve discounts up to a limit that finance sets. When the rep clicks **Send**, the application emails the customer a binding offer that they can sign electronically.

The `Quote` table has an `ApprovalStatus` column. The approve endpoint sets it to `Approved`, and the send endpoint checks it:

```csharp
// Anti-pattern: a mutable flag on a mutable object stands in for the decision.
if (quote.ApprovalStatus == ApprovalStatus.Approved)
{
    await offers.SendAsync(quote, ct);
}
```

One week near quarter end:

| When | What happens |
| --- | --- |
| Mon 10:05 | Dana, a rep, builds quote `Q-3107` revision 4: 120 seats at a 28% discount, 254,880 USD. She requests approval |
| Tue 15:18 | Priya, a sales manager with a 30% limit under finance policy revision 7, opens the approval screen. It shows revision 4 |
| Tue 15:19 | Dana, in another tab, raises the seat count to 180. The quote becomes revision 5, 382,320 USD |
| Tue 15:20 | Priya clicks **Approve**. The endpoint sets `ApprovalStatus = Approved` on the quote, which is now revision 5 |
| Wed 09:00 | After a margin review, finance lowers managers' approval limit to 25%. The policy becomes revision 8 |
| Wed 14:00 | Priya transfers to the partner team and is no longer a sales manager |
| Thu 11:30 | The ERP system places the customer on credit hold for an overdue invoice |
| Thu 16:00 | Next quarter's price list is published |
| Fri 09:12 | Dana clicks **Send**. `ApprovalStatus` is `Approved`, so the offer for revision 5 goes out. The customer signs within the hour |

Priya approved a quote she never saw, for half again the amount she reviewed, at a discount no current manager may grant, to a customer the company had just stopped extending credit to. Every check that ran passed.

Security engineers call the general shape a [time-of-check to time-of-use (TOCTOU) race](https://cwe.mitre.org/data/definitions/367.html): something is checked, the world changes, and the result of the check is used anyway. It is usually described for files and memory, measured in microseconds. In business systems the same race is measured in minutes or weeks, and the check is often a human decision.

> **A decision that passed is still true as history. It says nothing about whether the operation may run now. Bind each decision to exactly what it was about, re-read current preconditions at the point of use, write down what happens when the rules or the decider change, and make the final check and the state change one step. Do not rewrite the old decision. Record the new one.**

The opposite mistake is just as real. Not every change invalidates a decision, and a system that throws away approvals whenever anything moves teaches people to approve without reading. Of the five changes that week, two should have stopped Friday's send under any reasonable design, two depend on rules the team should write down in advance, and one should have changed nothing. Near the end, this article covers when ordinary authorization at the point of use is all you need.

---

## Two Questions That Share a Word

The send endpoint asked one question and answered another.

**The historical question:** *Was this quote approved, by whom, for what, and under which rules?* Its answer never changes. If Priya approved revision 4 at 15:20 under policy revision 7, that remains true forever, including after she transfers, after the policy changes, and after the quote is cancelled.

**The current question:** *May this offer be sent now?* Its answer depends on what is true at 09:12 on Friday: what the quote currently says, whether the customer may currently receive an offer, who is clicking **Send**, and which rules are in force.

A historical answer is an input to the current question. It is not an answer to it. Two mistakes follow from mixing them up, and they point in opposite directions:

- **Reading history as permission.** `ApprovalStatus == Approved` is treated as "may send now," which is the incident above.
- **Rewriting history to match the present.** When the credit hold arrives, someone sets the approval to `Revoked`, or a job recomputes every approval under the new policy and stores the result. Now the record says Priya's approval never happened, or that she approved something under rules that did not exist yet. Auditors and future maintainers lose the only true account of Tuesday.

The fix keeps both. The approval is an immutable record of what was decided. The send is a new decision, made from current facts, that consults the approval and records its own outcome.

---

## Six Kinds of Change

"Did anything change?" always has the answer yes. The useful question is which change it was, because each kind calls for a different response. A delayed operation has four possible outcomes at the point of use:

- **Proceed.** Nothing that matters has changed.
- **Deny.** A current fact forbids the operation. Asking someone again will not help until the fact changes.
- **Re-evaluate.** A rule or fact changed that the system can check again by itself.
- **Needs a new decision.** The earlier decision no longer applies, and it was a judgment the system cannot recompute, such as a person's approval. Route it back to someone who can decide.

The last two are different, and the difference matters. A policy rule that reads a credit status is a function of facts: run it again. Priya's approval was a person weighing a deal: no code can re-run her judgment against revision 5. It can only ask a person to make a new one.

| Kind of change | In the example | Default response | Why |
| --- | --- | --- | --- |
| **What the decision was about** | Revision 4 became revision 5 | **Needs a new decision** | The approval describes a different deal. It never applied to this one |
| **A current precondition** | Customer placed on credit hold | **Re-read, and deny if it fails** | A fact that must hold at the moment of use no longer holds |
| **The current actor** | Dana, if she no longer owned the account | **Re-evaluate as an ordinary request** | Sending is Dana's action now, and it needs ordinary authorization now |
| **The rules** | Approval limit lowered from 30% to 25% | **Re-evaluate, under a written policy choice** | Apply the new rule to decisions not yet used, or honor decisions made under the old one |
| **Who decided** | Priya transferred out of sales management | **Written policy choice, often accepted as of the decision** | Her authority was valid when she used it. Whether it must still be valid is up to the organization |
| **Something the decision did not depend on** | Next quarter's price list, Dana's expired session, a note on the quote | **Proceed** | The decision never relied on it |

### What the decision was about

This is the most important row, and the one the `ApprovalStatus` column could not represent. An approval is an approval *of something*: a specific revision, with specific line items, prices, discount, and terms. If the thing changes, the approval does not become stale. It becomes an approval of something else.

That makes this row different from all the others. No freshness rule, expiry time, or re-evaluation can rescue it, because the problem is not age. A five-minute approval lifetime would not have helped on Tuesday: Priya clicked one minute after Dana's edit.

The response is binding: record exactly what was decided, and compare it with what is about to happen. Which fields count is a design decision. Seat count, unit prices, discount, payment terms, and offer validity obviously count. An internal note or a tag usually does not. When in doubt, include the field. A field wrongly included costs one re-approval. A field wrongly excluded is a way to change the deal without anyone looking.

### A current precondition

Some facts must be true at the moment of use, whatever was true before. The customer is not on credit hold. The account is not closed. The resource is not under legal hold. The product is still sold in that region.

These are the cheapest changes to handle and the most often missed. Read them from their authoritative source at the point of use, every time. They do not touch the approval. When the hold clears next week, Priya's approval may again be usable, because nothing about it was wrong.

### The current actor

Dana clicking **Send** on Friday is a new request by an authenticated user. It needs ordinary authorization, exactly as if no approval existed: is she still the account owner, and may she still send offers? This is not part of the old decision at all, and treating it as such is a common way to skip it. "The quote was approved" says nothing about who may act on it today.

### The rules

When the approval limit drops from 30% to 25%, the team has to decide what happens to approvals given under the old limit and not yet used. Both answers are defensible:

- **Apply the current rule to anything not yet used.** An offer that has not been sent commits the company to nothing, so asking a current manager again is cheap. Approvals above the new limit need re-approval.
- **Honor decisions made under the rules in force at the time.** The approval was valid when given, and changing the rules midstream disrupts deals in progress. Optionally, honor it only within a window.

What separates them is usually whether the organization has already committed to anyone. An offer already in the customer's hands should not be retracted because a limit changed afterward. An offer still in draft can be.

Notice the shape of the first rule: it re-checks the *old decision's parameters* against the *current rule*. That is narrower than "any policy revision invalidates every approval," which would also discard approvals every time finance fixed a typo in an unrelated rule. [Policy Versioning and Decision Provenance](../../governance/policy-versioning-and-decision-provenance.md#choose-an-explicit-execution-freshness-strategy) compares exact-version matching, compatibility windows, and operation-specific rules in depth.

### Who decided

Priya was a sales manager with a 30% limit when she approved. On Wednesday she moved teams. Should her approval still count on Friday?

Many organizations say yes: she was authorized when she decided, the decision belongs to the organization, and deals should not stall because someone changed roles. Others require that the approver's authority still be valid at use, typically where the approval is a personal attestation or where a departure may itself be suspicious. [How Short-Lived Execution Authority Differs from User Authorization](short-lived-execution-authority-vs-user-authorization.md#which-facts-must-still-be-true-on-friday) works through the same question for an operator who leaves before a scheduled payout.

The failure is not either answer. It is having no answer, so the outcome depends on something accidental, such as whether a session or cached token happened to survive.

### Something the decision did not depend on

Next quarter's price list does not affect a quote that locks its own prices. Dana's browser session expiring overnight does not revoke anything; she will sign in again. An internal note added to the quote is not part of the deal. None of these should touch the approval, and a design that lets them do so is the subject of the next section.

---

## Do Not Invalidate on Every Change

After an incident like Friday's, the tempting fix is to discard approvals whenever anything about the quote, the customer, the approver, or the policy changes. It feels safe. It is not.

- **Approval fatigue.** If a note or a tag reopens approval, managers see the same deal three times a week. They learn to click **Approve** without reading, which defeats the control more thoroughly than the original bug.
- **Workarounds.** Reps learn to request approval at the last possible minute, to avoid touching an approved quote, or to split one deal into several quotes under the threshold.
- **Lost signal.** When every change invalidates, an invalidation no longer tells anyone that something important happened.

GitHub's branch protection is a useful comparison. Its option to [dismiss stale pull request approvals when new commits are pushed](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches) is tied to new commits, which change the code under review. It is not tied to editing the pull request's title or labels, which do not. The rule follows what the reviewer actually approved.

Classify each change once, deliberately, and test the classification. That is less work than living with either extreme.

---

## Five Ways to Bound Reuse

Once each fact is classified, each one needs a mechanism. There are five, and they are not interchangeable.

| Mechanism | What it is for | In the example | Fails when |
| --- | --- | --- | --- |
| **Re-read at use** | Current preconditions and the current actor | Credit status, account ownership | The source is unavailable and the code treats that as "fine" |
| **Bind to a version or fingerprint** | What the decision was about | The approval records the terms fingerprint of revision 4 | The fingerprint leaves out a field that changes the deal |
| **Invalidate on an event** | Facts that change rarely but matter at once | A fraud flag on the customer cancels pending offers | The event is missed, delayed, or arrives out of order, so re-read at use as well where you can |
| **Expire** | Facts you cannot observe at all | The customer's budget and competitive context, which no system records | It is used instead of binding or re-reading, for facts you could have checked |
| **Accept as of the decision** | Facts the organization deliberately does not re-check | Priya's role at the time she approved | Nobody wrote down that the fact is accepted, so it looks like a bug or gets "fixed" |

### Expiry is a guess about the facts you cannot see

Expiry is the mechanism teams reach for first, and it is the weakest. A timer does not know what changed. It only bets that after some interval, *something* probably has.

That bet is reasonable for facts no system records. A discount approved in January reflected the customer's situation, the competition, and the sales team's priorities in January, and none of those is a column anywhere. This team chose 30 days because that context rarely survives a quarter. That is a judgment about this business, not a universal number, and there is no universal number. Choose a lifetime from three things: how fast the unobserved facts change, how costly it is to act on a stale one, and how costly it is to ask again.

What expiry cannot do is substitute for the other mechanisms. A short lifetime would not have caught Tuesday's one-minute race, and a long one does not excuse skipping the credit check. If you can observe a fact, bind to it or re-read it. Use the timer for what is left.

Identity platforms reached the same conclusion about access tokens. Microsoft Entra's [continuous access evaluation](https://learn.microsoft.com/en-us/entra/identity/conditional-access/concept-continuous-access-evaluation) lets services respond to critical events, such as a disabled account or a revoked session, instead of waiting for a token's lifetime to run out. Lifetime remains, as a backstop.

---

## Close the Gap at Both Ends

The week had two TOCTOU races, and each needs its own fix.

### At the moment of decision: approve what was shown

Priya's screen showed revision 4, and her click approved whatever the quote was when the request arrived. That is the same race in miniature, and HTTP already has a standard answer. The server sends an entity tag with the representation, and the client sends it back with [`If-Match`](https://www.rfc-editor.org/rfc/rfc9110.html#name-if-match). If the resource no longer matches, the server returns `412 Precondition Failed` instead of applying the change. If a client leaves the header out, the server can refuse with [`428 Precondition Required`](https://www.rfc-editor.org/rfc/rfc6585.html).

ASP.NET Core parses the header for you through `HttpRequest.GetTypedHeaders().IfMatch`, but checking it against your own resource is your endpoint's job. Here the entity tag is the fingerprint of the quote's decision-relevant terms, so an internal note does not get in the way of an approval:

```csharp
app.MapPost("/quotes/{quoteId:guid}/approvals", async (
        Guid quoteId, HttpRequest http, ClaimsPrincipal user, QuoteApprovals approvals, CancellationToken ct) =>
    {
        // The approval screen was served with ETag "terms-9f2c...". Approve those terms, or nothing.
        if (!QuoteETag.TryReadTermsHash(http.GetTypedHeaders().IfMatch, out string? shownTermsHash))
        {
            return Results.StatusCode(StatusCodes.Status428PreconditionRequired);
        }

        string approverId = user.FindFirstValue("sub") ?? throw new InvalidOperationException("Missing subject claim.");

        ApprovalResult result = await approvals.ApproveAsync(quoteId, shownTermsHash, approverId, ct);
        return result.ToHttpResult();   // 412 Precondition Failed when the terms have changed
    })
    .RequireAuthorization("quotes.approve");    // an early rejection, not the decision
```

```csharp
public sealed record QuoteApproval(
    Guid ApprovalId,
    Guid QuoteId,
    int Revision,                    // recorded for evidence: the revision the approver saw
    string TermsHash,                // used for applicability: exactly what was approved
    decimal DiscountPercent,
    string ApprovedBy,
    decimal ApproverLimitPercent,    // the limit that applied when the approval was given
    int PolicyRevision,
    DateTimeOffset ApprovedAt,
    DateTimeOffset ExpiresAt);

public sealed class QuoteApprovals(
    IQuoteStore quotes,
    IApproverDirectory directory,
    IPricingPolicyStore policies,
    IApprovalStore approvals,
    TimeProvider clock)
{
    public async Task<ApprovalResult> ApproveAsync(
        Guid quoteId, string shownTermsHash, string approverId, CancellationToken ct)
    {
        QuoteSnapshot? quote = await quotes.FindAsync(quoteId, ct);
        if (quote is null)
        {
            return ApprovalResult.NotFound;
        }

        if (quote.TermsHash != shownTermsHash)
        {
            return ApprovalResult.PreconditionFailed;    // the approver saw different terms
        }

        ApproverFacts approver = await directory.GetAsync(approverId, ct);
        PricingPolicy policy = await policies.CurrentAsync(ct);

        if (!approver.IsSalesManager) return ApprovalResult.Forbidden("approver.not-manager");
        if (quote.DiscountPercent > policy.ManagerApprovalLimitPercent) return ApprovalResult.Forbidden("approver.limit-exceeded");

        DateTimeOffset now = clock.GetUtcNow();
        var approval = new QuoteApproval(
            Guid.NewGuid(), quote.Id, quote.Revision, quote.TermsHash, quote.DiscountPercent,
            approverId, policy.ManagerApprovalLimitPercent, policy.Revision,
            ApprovedAt: now, ExpiresAt: now.AddDays(policy.ApprovalLifetimeDays));

        // Inserted in one transaction, and only if the quote still has these terms. A save that
        // lands between the comparison above and this insert makes it fail rather than succeed.
        return await approvals.TryAddIfTermsUnchangedAsync(approval, ct)
            ? ApprovalResult.Approved(approval.ApprovalId)
            : ApprovalResult.PreconditionFailed;
    }
}
```

The fingerprint has to be computed the same way every time, from a canonical form of the terms, or two identical deals will produce different hashes. [Policy Versioning and Decision Provenance](../../governance/policy-versioning-and-decision-provenance.md#fingerprints-require-canonicalization) covers canonicalization. If your quotes already increment a revision only when the terms change, binding to that revision is simpler and equally correct.

On Tuesday, this version returns `412` at 15:20. Priya's screen reloads, shows 180 seats and 382,320 USD, and she decides about the deal that actually exists.

### At the moment of use: check, then claim, then send

The send endpoint now makes a new decision from current facts. The policy is ordinary code with no I/O. The comments map each check to the kind of change it handles:

```csharp
public sealed class OfferSendPolicy
{
    public OfferSendDecision Evaluate(SendFacts facts, DateTimeOffset now)
    {
        // The current actor: ordinary authorization of this request.
        if (!facts.SenderOwnsAccount) return OfferSendDecision.Deny("sender.not-account-owner");

        // A current precondition, read from the ERP at the point of use.
        if (facts.CreditStatus is CreditStatus.Unknown) return OfferSendDecision.CannotDetermine("credit.unavailable");
        if (facts.CreditStatus is CreditStatus.OnHold) return OfferSendDecision.Deny("customer.credit-hold");

        if (facts.Quote.DiscountPercent <= 20m)
        {
            return OfferSendDecision.Allow(facts.Quote.TermsHash, approvalId: null);
        }

        // What the decision was about: an approval of exactly these terms.
        QuoteApproval? approval = facts.ApprovalForCurrentTerms;
        if (approval is null) return OfferSendDecision.NeedsNewApproval("approval.missing-for-current-terms");

        // Facts nobody records: a backstop, not the main control.
        if (now >= approval.ExpiresAt) return OfferSendDecision.NeedsNewApproval("approval.expired");

        // The rules: this team applies the current limit to offers that have not been sent.
        if (approval.DiscountPercent > facts.CurrentPolicy.ManagerApprovalLimitPercent)
        {
            return OfferSendDecision.NeedsNewApproval("approval.above-current-limit");
        }

        // Who decided: deliberately accepted as of the approval. A later role change does not void it.
        return OfferSendDecision.Allow(facts.Quote.TermsHash, approval.ApprovalId);
    }
}
```

This sketch stops at the first failing rule. A real check may collect every failing reason, so that the rep can resolve them together.

`CannotDetermine` is not `Deny`, and it is never `Allow`. If the ERP cannot answer, the offer waits. [Should Authorization Fail Open, Fail Closed, or Defer?](fail-open-fail-closed-or-defer.md) covers that case.

An allowed decision is still a check, and a check is followed by a gap. Dana could save revision 6 in another tab a millisecond after the policy reads revision 5. So the send claims the quote with one conditional write, and then sends exactly the terms that were checked:

```csharp
// Inside the send handler. `facts` was just loaded from current sources for this quote and this sender.
OfferSendDecision decision = policy.Evaluate(facts, clock.GetUtcNow());
await sendChecks.RecordAsync(quoteId, sender, decision, ct);    // its own record; the approval is untouched

if (!decision.IsAllowed)
{
    return SendResult.From(decision);
}

Guid attemptId = Guid.NewGuid();

// Draft to Sending, only if the quote still has the terms that were checked and nobody else
// is sending it. The quote editor saves only Drafts, so once this succeeds the terms are fixed.
int claimed = await db.Quotes
    .Where(q => q.Id == quoteId
             && q.Status == QuoteStatus.Draft
             && q.TermsHash == decision.TermsHash)
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(q => q.Status, QuoteStatus.Sending)
        .SetProperty(q => q.SendAttemptId, attemptId), ct);

if (claimed == 0)
{
    return SendResult.Conflict("quote.changed-or-already-sending");    // nothing was sent
}

// Render the terms that were checked, by hash. Never a fresh read of "the current quote."
OfferDocument offer = await renderer.RenderAsync(quoteId, decision.TermsHash, ct);
await delivery.SendAsync(offer, attemptId, ct);
```

[`ExecuteUpdateAsync`](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete) returns the number of rows it changed, so zero means the condition failed and the send stops. Tracked entities with a [concurrency token](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) achieve the same thing: `SaveChanges` throws `DbUpdateConcurrencyException` when the row changed after it was read. Either works. A read followed by an unconditional write does not.

The claim only works because every other writer respects it. If the quote editor could save a quote in `Sending`, the condition would protect nothing. Keep the editor's own update conditional on `Status == Draft`.

Two limits of this sketch are deliberate:

- **The credit check is outside the transaction.** Credit status lives in the ERP, so a hold placed between the read and the claim is not seen. For this business, a gap of milliseconds is acceptable for an offer, because signing does not ship anything. The order-booking step, which is where credit actually matters, checks credit status again when the customer signs. When a stale answer from another system is not acceptable, have the system that performs the effect enforce the rule itself. [Where Should Authorization Live in a .NET Application?](where-authorization-should-live-dotnet-application.md) discusses where that check belongs.
- **Delivery failure and recovery are omitted.** A crash after the claim leaves a quote in `Sending` with an unknown outcome. Recovery asks the delivery service what happened to `attemptId` before anything sends again. [An Allowed Decision Does Not Mean the Operation Succeeded](allowed-decision-does-not-mean-execution-succeeded.md) covers that lifecycle, and [Your Retry Logic May Be Replaying an Authorization Decision](retry-logic-may-replay-authorization-decision.md) covers why each retry repeats the check.

---

## The Same Week, With the Fixes

| When | What happens now |
| --- | --- |
| Tue 15:20 | Priya's click returns `412`. Her screen reloads with revision 5 |
| Tue 15:24 | Priya approves revision 5 at 28%: approval `APR-611`, policy revision 7, limit 30%, expires in 30 days |
| Wed 09:00 | The limit drops to 25%. `APR-611` is unchanged. Nothing runs yet, because nothing needs to |
| Wed 14:00 | Priya transfers. `APR-611` is unchanged and, by this team's written rule, still usable |
| Thu 11:30 | The credit hold is placed. `APR-611` is unchanged |
| Thu 16:00 | The new price list is published. Nothing is affected |
| Fri 09:12 | Dana clicks **Send**. The check denies it: `customer.credit-hold`. Nothing is sent |
| Next Tue | The hold clears. Dana clicks **Send** again. The check returns `approval.above-current-limit`, and the quote goes back for approval |
| Next Wed | Marcus, a current manager with a 25% limit, cannot approve 28%. Dana renegotiates to 25%, creating revision 6. Marcus approves it, and the offer is sent |

Each change was handled by the mechanism meant for it. Two of the five, Priya's transfer and the price list, did nothing at all, by design.

---

## Keep the History. Add the New Record.

Every decision appends a record, and no later record edits an earlier one:

```text
Approval    APR-611   Q-3107 rev 5   terms 41ab…   28%   by priya     Tue 15:24
                      limit 30% under policy rev 7   expires +30d
Send check  SND-2290  Q-3107 rev 5   terms 41ab…         by dana      Fri 09:12
                      denied: customer.credit-hold
Send check  SND-2347  Q-3107 rev 5   terms 41ab…         by dana      next Tue 10:03
                      needs new approval: approval.above-current-limit (APR-611, policy rev 8)
Approval    APR-640   Q-3107 rev 6   terms 7d05…   25%   by marcus    next Wed 11:47
                      limit 25% under policy rev 8   expires +30d
Send check  SND-2361  Q-3107 rev 6   terms 7d05…         by dana      next Wed 11:52
                      allowed via APR-640; claimed; attempt 3c9e…
```

Anyone reading this can answer both questions: what was decided, and why the offer did or did not go out at each moment. `APR-611` still says Priya approved 28% under a 30% limit, because she did.

The interface should tell the same story. A badge reading **Approved** on the quote is the original bug in visual form. **Approved for revision 5, not valid for current terms** is the honest version, and it tells the rep what to do next.

This does not need a specialized store. A table of approvals, a table of send checks, and the quote's own status are enough for most applications. [Your Audit Log Records the Story, Not the Decision](your-audit-log-is-not-evidence.md) covers when more is justified.

---

## What Re-evaluation Does Not Promise

A fresh check at the point of use tells you the operation was allowed at that moment. It does not tell you the operation succeeded. The email can bounce, and the customer can sign an hour after a hold lands. A design that re-evaluates carefully and then treats `Allowed` as `Sent` has moved the stale-answer problem one step later, not removed it.

Re-evaluation also cannot reach backward. An offer that was legitimately sent before the limit dropped is a commitment the company made. Whether to honor, renegotiate, or withdraw it is a business decision about a new operation, not something the authorization layer should do by rewriting the old one.

---

## When Ordinary Authorization Is Enough

Most operations do not need any of this. Choose the first row that fits:

| Shape | What can change in between | What you need |
| --- | --- | --- |
| **Check and effect in one request, one database** | Almost nothing you can act on | Ordinary authorization, then a write in the same transaction, or a conditional write if concurrent edits matter |
| **Delayed work whose decision is a pure function of current facts** | Facts, rules, actor | Re-read and re-evaluate at the point of use. No binding and no expiry, because nothing earlier is being preserved |
| **A human decision, or another decision the system cannot recompute, used later** | Everything in the six rows | Bind to what was decided, re-read current preconditions, write down the rules-change and decider-change policy, expire only for unobserved facts, and claim atomically |
| **Execution by another process or service that should not hold standing authority** | All of the above, plus who may present the decision | A narrow, operation-specific grant on top of the above |

The second row covers most background jobs, and [A Background Job Should Not Inherit Everything the User Could Do](background-job-should-not-inherit-user-authority.md) works through it. The fourth row is where [Scoped Capability and Host-Owned Execution](../../tutorials/scoped-capability-and-host-owned-execution.md) earns its cost.

The machinery in this article is justified when a decision is made at one moment, used at a later one, and cannot simply be made again by running the same code. If yours can, run the code.

---

## Testing Each Kind of Change

These tests specify the behavior described above. They are not evidence of a working implementation, and each demonstrates behavior only under the conditions it sets up. Each one targets one row of the classification, so a missing row is visible as a missing test.

```csharp
[Fact]
public async Task Approval_of_terms_the_approver_did_not_see_is_refused()
{
    var harness = QuoteHarness.Create();
    QuoteView shown = await harness.OpenForApprovalAsync("Q-3107", approver: "priya");

    await harness.EditAsync("Q-3107", rep: "dana", seats: 180);
    HttpResponseMessage response = await harness.ApproveAsync("Q-3107", approver: "priya", ifMatch: shown.ETag);

    Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
    Assert.Empty(await harness.ApprovalsAsync("Q-3107"));
}

[Fact]
public async Task Changed_terms_need_a_new_approval_and_the_old_one_is_unchanged()
{
    var harness = QuoteHarness.Create();
    QuoteApproval original = await harness.ApproveCurrentTermsAsync("Q-3107", approver: "priya");

    await harness.EditAsync("Q-3107", rep: "dana", seats: 180);
    SendResult result = await harness.SendAsync("Q-3107", rep: "dana");

    Assert.Equal("approval.missing-for-current-terms", result.ReasonCode);
    Assert.Empty(harness.Delivery.Sent);
    Assert.Equal(original, (await harness.ApprovalsAsync("Q-3107")).Single());    // history stays true
}

[Fact]
public async Task Credit_hold_denies_the_send_without_touching_the_approval()
{
    var harness = QuoteHarness.Create();
    QuoteApproval approval = await harness.ApproveCurrentTermsAsync("Q-3107", approver: "priya");

    harness.Erp.PlaceCreditHold("C-88");
    SendResult result = await harness.SendAsync("Q-3107", rep: "dana");

    Assert.Equal("customer.credit-hold", result.ReasonCode);
    Assert.Empty(harness.Delivery.Sent);

    harness.Erp.ClearCreditHold("C-88");
    Assert.True((await harness.SendAsync("Q-3107", rep: "dana")).IsSent);    // the approval was never wrong
    Assert.Equal(approval.ApprovalId, harness.Delivery.Sent.Single().ApprovalId);
}

[Fact]
public async Task Lowered_limit_requires_reapproval_for_an_unsent_offer()
{
    var harness = QuoteHarness.Create(managerLimitPercent: 30m);
    await harness.ApproveCurrentTermsAsync("Q-3107", approver: "priya");    // 28%

    harness.Policy.SetManagerLimit(25m);
    SendResult result = await harness.SendAsync("Q-3107", rep: "dana");

    Assert.Equal("approval.above-current-limit", result.ReasonCode);
    Assert.Empty(harness.Delivery.Sent);
}

[Fact]
public async Task Approver_role_change_does_not_void_the_approval()
{
    var harness = QuoteHarness.Create();
    await harness.ApproveCurrentTermsAsync("Q-3107", approver: "priya");

    harness.Directory.RemoveSalesManagerRole("priya");
    SendResult result = await harness.SendAsync("Q-3107", rep: "dana");

    Assert.True(result.IsSent);    // the team's written rule: accepted as of the approval
}

[Fact]
public async Task Unrelated_changes_do_not_invalidate_the_approval()
{
    var harness = QuoteHarness.Create();
    await harness.ApproveCurrentTermsAsync("Q-3107", approver: "priya");

    await harness.AddInternalNoteAsync("Q-3107", rep: "dana", note: "Customer prefers PDF.");
    harness.Pricing.PublishNextQuarterPriceList();
    harness.Identity.ExpireAllSessionsFor("dana");

    Assert.True((await harness.SendAsync("Q-3107", rep: "dana")).IsSent);
}
```

Also cover:

- **The race at the claim.** An edit committed between the check and the claim makes the claim fail, and nothing is sent. An edit attempted after the claim is refused, because the quote is no longer a draft.
- **The current actor.** Dana, reassigned away from the account after approval, is denied with `sender.not-account-owner`.
- **Expiry.** An approval used one second after `ExpiresAt` needs a new approval; one used one second before it does not.
- **Unavailable facts.** An ERP timeout produces `CannotDetermine` and no send, never `Allow`.
- **Duplicate sends.** Two concurrent **Send** clicks produce one claim and one offer.
- **Fingerprint coverage.** Changing each decision-relevant field changes the terms hash, and changing each excluded field does not. This test is where the "when in doubt, include it" rule is enforced.

[How to Test That a Denied Operation Never Executes](test-denied-operation-never-executes.md) develops the technique of asserting that an effect never happened.

---

## Failure Modes

**1. A status flag stands in for the decision.** `ApprovalStatus = Approved` sits on a mutable record, describes nothing in particular, and stays `Approved` while the record changes underneath it.

**2. Approving whatever is current when the click arrives.** The approver reviewed one version and the endpoint approved another, because the request carried no precondition.

**3. Expiry used as the only control.** A short lifetime is expected to catch changes it cannot see, including ones that happened a minute after the check.

**4. Invalidating on every change.** Notes, tags, and unrelated policy edits reopen approval until approvers stop reading what they approve.

**5. Rewriting the original decision.** An approval is set to `Revoked` or recomputed under today's rules, and the record of what actually happened is lost.

**6. Skipping the current actor's authorization.** "The quote is approved" is treated as permission for whoever clicks **Send**, without checking that they may act on the account today.

**7. Rules and deciders changing with no written policy.** Whether an approval survives a limit change or the approver's transfer depends on which code path, cache, or session happened to run.

**8. Check, then unconditional write.** The final check passes, then a separate write changes state, and anything that lands in between goes through unexamined.

**9. Rendering a fresh read after the check.** The claim protected revision 5, and the renderer loaded "the current quote," which by then was revision 6.

**10. An unavailable source read as permission.** The credit service times out and the send proceeds because no hold was returned.

**11. Re-evaluation read as success.** A send that was allowed is recorded as sent, and nobody checks whether it was delivered.

---

## A Short Review Checklist

**The decision**

1. Does each decision record exactly what it was about, as a revision or a fingerprint of the decision-relevant fields?
2. Is the list of fingerprinted fields deliberate, and does a test show that each one changes the fingerprint?
3. Does the action that records a human decision require the version the person saw, and refuse when it no longer matches?
4. Are decision records immutable, with later outcomes stored as new records?

**The six kinds of change**

5. Is every current precondition read from its authoritative source at the point of use?
6. Is the current actor authorized for the current request, independently of any earlier decision?
7. Is there a written rule for decisions made under rules that have since changed?
8. Is there a written rule for decisions made by someone whose authority has since changed?
9. Are the changes that deliberately do not invalidate a decision listed and tested too?

**Bounding reuse**

10. Is each fact the decision relied on assigned one mechanism: re-read, bind, invalidate on an event, expire, or accept as of the decision?
11. Is expiry used only for facts no system can observe, with a lifetime the team can justify for this operation?
12. Does an unavailable source produce "cannot determine" rather than "allowed"?

**The point of use**

13. Are the final check and the state change one conditional write, with every other writer respecting the resulting state?
14. Does the effect use exactly what was checked, rather than a fresh read?
15. Where a fact lives outside the transaction, is the remaining gap accepted in writing or enforced again at the system that performs the effect?
16. Does the interface describe what was approved, rather than showing a bare "Approved"?

---

## Continue Deeper

For the full treatment of policy identity, version and fingerprint strategies, and why an old decision must never be explained with today's policy, read [Policy Versioning and Decision Provenance](../../governance/policy-versioning-and-decision-provenance.md). When the inputs that change are risk scores, model outputs, or other probabilistic observations, [Adaptive Risk Context, Freshness, and Drift](../../advanced/adaptive-risk-context-freshness-and-drift.md) separates provider validity from host freshness policy and defines explicit re-evaluation triggers.

If your decision is an approval, an acknowledgment, or an authorization and you are not sure which, [Authorization vs. Approval vs. Acknowledgment](authorization-vs-approval-vs-acknowledgment.md) separates what each one binds to and how long it lasts.

When the later executor is another process that should hold only bounded authority, [Scoped Capability and Host-Owned Execution](../../tutorials/scoped-capability-and-host-owned-execution.md) teaches how a decision becomes a narrow grant with its own resource, policy, and time bounds. [Delayed Financial Payout and Ambiguous External Commit](../../case-studies/delayed-financial-payout-and-ambiguous-external-commit.md) follows one delayed operation through binding, re-validation, an atomic claim, and reconciliation after an uncertain outcome.

### Related Work

- [CWE-367: Time-of-check Time-of-use (TOCTOU) Race Condition](https://cwe.mitre.org/data/definitions/367.html) describes the general weakness this article applies to business decisions.
- [RFC 9110, HTTP Semantics](https://www.rfc-editor.org/rfc/rfc9110.html#name-if-match), defines `If-Match` and `412 Precondition Failed`, and [RFC 6585](https://www.rfc-editor.org/rfc/rfc6585.html) defines `428 Precondition Required`.
- [Handling Concurrency Conflicts](https://learn.microsoft.com/en-us/ef/core/saving/concurrency), in the EF Core documentation, covers concurrency tokens and `DbUpdateConcurrencyException`, and [ExecuteUpdate and ExecuteDelete](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete) covers set-based conditional updates.
- [About protected branches](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches), in the GitHub documentation, describes dismissing stale pull request approvals when new commits are pushed.
- [Continuous access evaluation](https://learn.microsoft.com/en-us/entra/identity/conditional-access/concept-continuous-access-evaluation), in the Microsoft Entra documentation, describes responding to critical events instead of relying on token lifetime alone.

---

## The Short Answer

A check that passed is a true statement about the moment it ran. Keep it that way: record what it decided, about exactly what, under which rules, and never edit it to match a later world.

When the decision is used later, ask the current question with current facts. Find out which kind of change happened. If the thing decided about changed, the decision does not apply, so ask again. If a current precondition fails, deny. If the rules or the decider changed, follow the rule you wrote down in advance. If nothing the decision depended on changed, proceed, and do not punish people with re-approvals for edits that do not matter.

Bind what you can observe, re-read what must be current, and use expiry only for what you cannot see. Then make the final check and the state change one step, and act on exactly what you checked. For most operations, that is one ordinary authorization check and one conditional write. The rest is for decisions that cannot simply be made again.
