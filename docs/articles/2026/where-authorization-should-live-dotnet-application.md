---
description: Place each authorization responsibility where its facts are trustworthy, and enforce the deciding check where every path to the side effect meets.
title: "Where Should Authorization Live in a .NET Application?"
author: Christopher D. Cavell
published: "2026-10-28"
summary: Placing authorization well means separating four responsibilities instead of looking for one check. Identify the caller at the host, read security-relevant facts from authoritative stores, decide where those facts and the policy are available, and enforce at the point every entry path to the side effect must cross. Earlier layers may reject early; they should not be the only thing that says yes.
feed: true
x_hashtags:
  - DotNet
  - AspNetCore
---

# Where Should Authorization Live in a .NET Application?

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** No formal prerequisites. Familiarity with ASP.NET Core authentication and authorization, minimal APIs or controllers, and dependency injection is helpful. No AsiBackbone package or prior Learning material is required.

**Sample scope:** The C# targets .NET 8 or later and uses ASP.NET Core minimal APIs. The stores, directories, harness, and claim names are illustrative interfaces and fakes, not a runnable sample or a recommended persistence API. Framework behavior is described from Microsoft's documentation as of October 2026; check the current documentation for your versions.

**What this article covers:** why "where does authorization go?" is really four questions; what each common .NET placement can and cannot see, from middleware to the final executor; why the deciding check belongs where every entry path to a side effect meets; how domain invariants, data-access filters, and policy engines relate to authorization without replacing it; what changes when the effect happens later in another component; how to keep early checks from drifting into duplicate decisions; tests that prove every path is covered; and when an endpoint policy is the whole answer.

**In plain terms:** an office building has a guard in the lobby, a key-card reader on each floor, and a lock on the records room. The guard confirms that you work there. The floor reader confirms that you work on that floor. Only the records-room lock knows which cabinets you may open, so only the lock can make that decision. If the building also has a loading-dock entrance, the lobby guard protects nothing on that path, and the lock is the only check every visitor meets. Nobody argues about whether the guard *or* the lock is "where security lives." Each checks what it can see, and the last one stands in front of what matters.

Consider a hypothetical accounts-payable application that lets finance staff approve vendor invoices for payment. The rules are ordinary: approvers need the approval permission, may approve only up to their personal limit, and may not approve an invoice they submitted. The team puts the permission check on the endpoint as an ASP.NET Core policy and writes the limit and self-approval checks in the endpoint handler. Everything works and every test passes.

Over the next year, three more ways to approve an invoice appear. A bulk-approval screen gets its own endpoint, which copies the tenant and limit checks into a query but not the self-approval check. A chat-platform approval bot calls the repository directly after checking that the chat user maps to someone with the approver role. A nightly job auto-approves recurring invoices from vendors under contract. At quarter-end, an internal audit finds a clerk who submitted invoices and approved them from the bulk screen, all within that clerk's limit.

Every rule existed somewhere in the codebase. The failure was where each one lived: each entry path carried its own copy of the rules, and the copies drifted.

> **For placement purposes, treat authorization as four responsibilities rather than one check: establish who is asking, establish the facts, decide, and enforce. Put each one where its inputs are trustworthy. Put the deciding check for an operation at the point every path to its side effect must cross, as close to the effect as the facts it needs are available. Earlier layers may reject early. They should not be the only thing that says yes.**

That is not an argument for moving every check into one place or for adding layers. Many ordinary endpoints are well served by an endpoint policy and, when needed, a resource check in the handler. Near the end, this article covers when that is the whole design.

---

## The Example: Approving Invoices for Payment

The rest of the article uses one system. **Payables** is a multi-tenant ASP.NET Core application. Each tenant is a customer company, and each invoice belongs to one tenant. Approving an invoice changes its status from `AwaitingApproval` to `Approved`. A separate **payment run**, a background worker, pays approved invoices later, sometimes days later.

These are the rules, and where each fact comes from:

| Rule | Depends on | Authoritative source |
| --- | --- | --- |
| R1. The caller is authenticated | The caller's credential | The identity provider, validated by the host |
| R2. The caller may approve invoices at all | The caller's permissions in their tenant | The tenant's role assignments |
| R3. The invoice belongs to the caller's tenant | The invoice and the caller's tenant | The invoice record; the tenant from authentication, never from the request |
| R4. The invoice amount is within the caller's approval limit | The caller's current limit and the invoice amount | The finance team's approval-limit table, which changes during the year |
| R5. The caller did not submit the invoice | The invoice's submitter and the caller | The invoice record |
| R6. Only an invoice awaiting approval can be approved | The invoice's status | The invoice record |
| R7. At payment time, the vendor is not on hold and its bank details are the ones the approver saw | Vendor state when the money moves | The vendor record |

Four entry paths can approve an invoice:

1. `POST /invoices/{id}/approve`, called by the web application.
2. `POST /invoices/approve-batch`, called by the bulk-approval screen.
3. A message handler that receives "Approve" button clicks from a chat platform.
4. A nightly job that auto-approves invoices for vendors under a recurring contract, within the contracted amount.

The rules do not all belong in the same place, and the entry paths do not all have the same caller. That is the whole problem in miniature.

---

## Four Responsibilities, Not One Check

"Where should the authorization check go?" hides four different questions. Strictly, the first is authentication rather than authorization, but placement goes wrong when it is left out. Each question has different inputs, so each one has a different natural home.

| Responsibility | The question it answers | What it needs | Natural home |
| --- | --- | --- | --- |
| **Establish who is asking** | Which user, service, or job is the actor, in which tenant? | A validated credential, or the identity of the system process | The host boundary: authentication middleware, the message consumer, the job runner |
| **Establish the facts** | What is true about the actor, the resource, and the environment right now? | Authoritative stores, not values copied from the request | Code that can read the stores that own those facts |
| **Decide** | Given those facts, what does policy say about this operation? | The facts, the policy, and the requested operation | A policy component the operation calls: an ASP.NET Core authorization handler, an in-process class, or a policy engine |
| **Enforce** | What stops the side effect when the answer is not "yes"? | Control over the code path that causes the effect | The point every entry path to the effect must cross |

Separating them settles many placement arguments. The authentication middleware is excellent at the first question and cannot answer the second. A policy engine is excellent at the third and cannot do the fourth for you. The boundary that actually produces the effect is the last reliable enforcement point: earlier components may also enforce, but they cannot substitute for the component that controls whether the effect occurs. That boundary may not know who the original requester was unless the earlier layers pass it along.

One operation can therefore legitimately involve several layers. That is not duplication, as long as each layer answers a different question.

---

## The Usual Places, and What Each Can See

### Authentication middleware and a fallback policy

The host's authentication pipeline validates the applicable credential and establishes the principal for the request, and `UseAuthorization` evaluates the endpoint's authorization metadata. A **fallback policy**, set through `AuthorizationOptions.FallbackPolicy`, applies to endpoints that have no authorization metadata. Setting it to require an authenticated user means an endpoint with no authorization metadata rejects anonymous callers, for requests that pass through the authorization middleware. Anything served earlier in the pipeline, such as static files served by middleware that runs before `UseAuthorization`, is outside it. An endpoint can still opt out explicitly with `AllowAnonymous`.

A fallback policy restores authentication, not a missing operation permission. An authenticated caller still reaches an endpoint whose developer forgot `RequireAuthorization("invoices.approve")`.

This layer is the right home for R1 and for "deny by default." It sees the request and the caller's claims. It does not see the invoice.

### Endpoint authorization metadata

`[Authorize(Policy = "...")]` and `RequireAuthorization("...")` attach a named policy to an endpoint or a route group. The policy's requirements can check roles, claims, scopes, or anything an authorization handler can compute from the user.

This is a good home for R2 as an early, coarse check: callers without any approval permission are rejected before the endpoint runs. [Microsoft's documentation on resource-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased) notes the limit: attribute evaluation happens before data binding and before the handler that loads the resource runs. A policy evaluated from endpoint metadata therefore cannot check R3 to R6 against the invoice the endpoint will load. A custom authorization handler can use injected services to fetch resource facts itself. Unless loading is coordinated, for example through a shared scoped loader, the handler and the endpoint may duplicate the work or retrieve different versions of the invoice.

### Resource-based checks in the endpoint or controller

Inside the handler, after loading the invoice, `IAuthorizationService.AuthorizeAsync(user, invoice, "ApproveInvoice")` evaluates a policy against the resource. An authorization handler for `Invoice` can check tenant, limit, and submitter. This keeps policy in ASP.NET Core's familiar model and is often exactly right.

Its weakness is not what it can see but where it sits: in one endpoint. It protects calls that arrive through that endpoint. The bulk endpoint, the chat handler, and the nightly job are other doors into the same room.

### A pipeline behavior or decorator

A MediatR `IPipelineBehavior` or a decorator around command handlers is a good home for uniform mechanics, such as logging, validation, and telemetry, and for coarse checks. A generic behavior sees the command, not the loaded resource, unless it is purpose-built to load one, and it protects only calls that pass through the decorated registration. [Your Pipeline Behavior Can Watch the Operation. It Should Not Decide It.](pipeline-behavior-should-not-decide-the-operation.md) covers its failure modes in detail; for this article, treat it like endpoint metadata: useful for early rejection, weak as the only check.

### The application service, or use-case handler

An application service such as `InvoiceApprovalService.ApproveAsync` is the code that performs the use case. It can load the invoice and the approver's current facts, evaluate policy, and perform the change. If every entry path calls it, it is the place the paths meet.

That makes it the natural home for the deciding check on R2 to R5 in many applications. It requires one discipline: the service must receive the actor as an explicit argument that each entry path constructs from its own authenticated source. It should not reach for `HttpContext`, which the job and the chat handler do not have.

### The domain model

An `Invoice` entity with an `Approve` method that refuses to move anything but an `AwaitingApproval` invoice is enforcing R6. That is a **domain invariant**: it holds for every caller that goes through the entity, including the nightly job. Code that bypasses the entity, such as direct SQL, a migration, or a repair script, also bypasses the method; when that matters, a database constraint or trigger is the layer that covers it.

Invariants and authorization are different kinds of rules. A useful test: *would the rule still hold if a fully trusted system process performed the operation?* If yes, it is an invariant and belongs with the data it protects. If the answer depends on who is asking, it is authorization. R6 passes the test. R4 does not. R5 is a judgment call that a later section returns to.

### Data access

EF Core [**global query filters**](https://learn.microsoft.com/en-us/ef/core/querying/filters), configured with `HasQueryFilter`, can add `TenantId == currentTenant` to every LINQ query for an entity type. They are strong defense in depth for R3 on reads, especially for list and search queries, where a forgotten `Where` clause is the classic cross-tenant read.

A query filter is a backstop, not the decision. It can be disabled with `IgnoreQueryFilters`. It does not constrain SQL executed directly through APIs such as `Database.ExecuteSql`, or any access outside EF's entity-query pipeline. Composable `FromSql` entity queries can still have the filter applied. It also does not validate what `SaveChanges` writes: tenant-safe reads and tenant-safe writes need separate attention. Its tenant value is only as trustworthy as where it came from, which must be the authenticated context, not a request parameter. And the simple tenant filter shown later enforces row scope, not the approval policy: it knows nothing about limits or submitters.

Database **row-level security** is a different and stronger mechanism. SQL Server security policies can include block predicates that reject writes, and PostgreSQL row security policies can restrict both the rows a statement sees and the rows it may write, and can consult other tables. Depending on how far it is taken, the database can enforce part of the rule set itself. Its policies key on the database identity, a trusted session value the application sets, or both, so it is only as trustworthy as that identity or value. It covers only the tables and commands its policies apply to, subject to role exemptions such as table owners and roles allowed to bypass row security, and it protects only the database it runs in.

### A policy engine

A remote or embedded policy engine, such as Open Policy Agent, Cedar, or an in-house decision service, is a **policy decision point**: it decides. Your code is still the **policy enforcement point**: it asks the question and refuses to perform the effect unless the answer allows it. Adopting an engine moves the third responsibility. It does not move the fourth, and it moves the second only as far as the engine is configured to fetch its own data; an engine that receives facts from the caller decides on whatever the caller claims. [Policy as Code in ASP.NET Core Without Overengineering](policy-as-code-aspnet-core-without-overengineering.md) covers when an engine is worth its cost.

### The final executor, when execution happens later

The payment run is a different component that acts hours or days after the approval. It does not have the approver's request and should not need it. What it needs is the answer to a different question: is this approved invoice still eligible to be paid now? That is R7, and the payment run is the effect-producing boundary for it, because it is the component standing in front of the money.

---

## What Each Placement Can See

| Placement | Sees the caller | Sees the resource | Covers every entry path | Good for | Fails when |
| --- | --- | --- | --- | --- | --- |
| Authentication middleware and fallback policy | Yes | No | HTTP only | R1, deny by default | Treated as authorization for specific resources |
| Endpoint policy metadata | Yes | Not the endpoint's loaded instance | That endpoint only | Early, coarse checks such as R2 | Used for rules that need the loaded resource |
| Resource-based check in the endpoint | Yes | Yes | That endpoint only | R2 to R5 when the endpoint is the only door | A second door appears |
| Pipeline behavior or decorator | Usually | Not unless built to load it | Calls through the decorated registration | Uniform mechanics, coarse checks | It becomes the only check for resource rules |
| Application service | Yes, if passed explicitly | Yes | Every path that calls it | The deciding check for R2 to R5 | Some path skips it, or it reads identity from ambient HTTP state |
| Domain model | Only what it is passed | Yes | Every path that mutates through the entity | Invariants such as R6 | Coupled directly to identity infrastructure |
| EF Core query filter | Only the configured scope | Rows in LINQ queries | LINQ queries over the filtered entity | Defense in depth for R3 on reads | Mistaken for the decision, or fed a tenant from the request |
| Database row-level security | The database identity, trusted session context, or both | Rows, and writes if configured | Applicable statements on protected tables, subject to policy and role exemptions | Defense in depth for R3, and more if policies cover writes | Fed an untrusted identity, or assumed to cover other stores |
| Policy engine | Supplied facts and any configured trusted data sources | Supplied facts and any configured trusted data sources | None by itself | Deciding when policy is shared or changes independently | Treated as enforcement, or given caller-supplied facts |
| Later effect-producing boundary | Usually a service identity | Yes, at execution time | Every path to that effect | Execution-time eligibility such as R7 | Assumed to inherit the original decision forever |

The column that decides the deciding check's location is "covers every entry path." The column that decides what it can check is "sees the resource." In this design, the application service is the first placement with "yes" in both. In other systems, another operation boundary can play that role: a command handler, a façade that owns a transactional aggregate, a database procedure, or a separate service.

---

## Put the Deciding Check Where the Paths Meet

### The version that drifts

This is the shape the Payables team had after a year:

```csharp
// Anti-pattern: each entry path carries its own copy of the rules.
app.MapPost("/invoices/{id:guid}/approve", async (
        Guid id, ClaimsPrincipal user, PayablesDb db, CancellationToken ct) =>
    {
        Invoice? invoice = await db.Invoices.SingleOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null || invoice.TenantId != user.GetTenantId()) return Results.NotFound();
        if (invoice.SubmittedBy == user.GetUserId()) return Results.Forbid();

        decimal limit = await db.ApprovalLimits
            .Where(l => l.UserId == user.GetUserId())
            .Select(l => l.Amount)
            .SingleAsync(ct);
        if (invoice.Amount > limit) return Results.Forbid();

        invoice.Status = InvoiceStatus.Approved;
        invoice.ApprovedBy = user.GetUserId();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    })
    .RequireAuthorization("invoices.approve");

app.MapPost("/invoices/approve-batch", async (
        Guid[] ids, ClaimsPrincipal user, PayablesDb db, CancellationToken ct) =>
    {
        decimal limit = await db.ApprovalLimits
            .Where(l => l.UserId == user.GetUserId())
            .Select(l => l.Amount)
            .SingleAsync(ct);

        List<Invoice> invoices = await db.Invoices
            .Where(i => ids.Contains(i.Id) && i.TenantId == user.GetTenantId() && i.Amount <= limit)
            .ToListAsync(ct);

        foreach (Invoice invoice in invoices)
        {
            // The self-approval rule was never copied here.
            invoice.Status = InvoiceStatus.Approved;
            invoice.ApprovedBy = user.GetUserId();
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    })
    .RequireAuthorization("invoices.approve");
```

`GetTenantId` and `GetUserId` are illustrative extension methods over the authenticated principal.

Several individual checks are sensible: the tenant comes from the authenticated principal, the current limit is read from the database, and an endpoint policy runs. The design is still unsafe, because the rule set is duplicated across entry paths and each copy is incomplete in its own way. Neither endpoint protects the state transition required by R6, so an invoice that was already paid could be approved again. The chat handler and the nightly job, which are not shown, each have a third and fourth copy. Because `Status` has a public setter, any code that can load an invoice can approve it.

### The version with one shared approval policy

Move the deciding check into the operation, and make each entry path's job only to establish who is asking.

Each path builds an explicit actor from its own authenticated source:

```csharp
public abstract record Actor(string TenantId)
{
    // A person, identified by the host that authenticated them.
    public sealed record Person(string UserId, string TenantId) : Actor(TenantId);

    // A system process acting under its own policy, not on behalf of a person.
    public sealed record Automation(string ProcessName, string TenantId) : Actor(TenantId);
}

// At the HTTP boundary. The claim names depend on your identity provider and claim mapping.
static Actor.Person ToActor(ClaimsPrincipal user) => new(
    UserId: user.FindFirstValue("sub") ?? throw new InvalidOperationException("Missing subject claim."),
    TenantId: user.FindFirstValue("tid") ?? throw new InvalidOperationException("Missing tenant claim."));
```

The service performs the use case. It reads the facts from the stores that own them, asks the policy, and applies the change only if the invoice version it evaluated is still current:

```csharp
public sealed class InvoiceApprovalService(
    IInvoiceStore invoices,          // tenant-scoped reads and conditional writes
    IApproverDirectory approvers,    // current permissions and approval limits
    InvoiceApprovalPolicy policy,
    TimeProvider clock)
{
    public async Task<ApprovalResult> ApproveAsync(Actor actor, Guid invoiceId, CancellationToken ct)
    {
        // R3: the lookup is scoped to the actor's tenant, so another tenant's invoice is
        // indistinguishable from a missing one.
        InvoiceSnapshot? invoice = await invoices.FindAsync(actor.TenantId, invoiceId, ct);
        if (invoice is null)
        {
            return ApprovalResult.NotFound;
        }

        // R2 and R4 come from the directory now, not from claims issued at sign-in.
        ApproverFacts facts = await approvers.GetAsync(actor, ct);

        ApprovalDecision decision = policy.Evaluate(actor, facts, invoice);
        if (!decision.IsAllowed)
        {
            return ApprovalResult.Denied(decision.ReasonCode);
        }

        // Applies only if the invoice is still at the version that was evaluated. The
        // entity's own Approve method enforces R6 again inside the write.
        bool applied = await invoices.TryApproveAsync(
            invoice.Id, invoice.Version, actor, clock.GetUtcNow(), ct);

        return applied ? ApprovalResult.Approved : ApprovalResult.Conflict;
    }
}
```

The policy is ordinary code with no I/O. It receives facts and returns a decision with a stable reason code:

```csharp
public sealed class InvoiceApprovalPolicy
{
    public ApprovalDecision Evaluate(Actor actor, ApproverFacts facts, InvoiceSnapshot invoice) =>
        (actor, invoice) switch
        {
            // A friendlier early answer for R6; the entity still enforces it.
            (_, { Status: not InvoiceStatus.AwaitingApproval }) =>
                ApprovalDecision.Deny("invoice.not-awaiting-approval"),

            (Actor.Person, _) when !facts.CanApproveInvoices =>
                ApprovalDecision.Deny("approver.permission-missing"),            // R2
            (Actor.Person person, _) when person.UserId == invoice.SubmittedBy =>
                ApprovalDecision.Deny("approver.submitted-invoice"),             // R5
            (Actor.Person, _) when invoice.Amount > facts.ApprovalLimit =>
                ApprovalDecision.Deny("approver.over-limit"),                    // R4
            (Actor.Person, _) =>
                ApprovalDecision.Allow(),

            // The nightly job has its own, narrower rule. The directory says whether this
            // process is enabled for this tenant; ContractedAmount is a per-invoice ceiling.
            (Actor.Automation, _) when !facts.CanApproveInvoices =>
                ApprovalDecision.Deny("automation.not-enabled"),
            (Actor.Automation, { RecurringContract: { } contract })
                when invoice.Amount <= contract.ContractedAmount =>
                ApprovalDecision.Allow(),
            (Actor.Automation, _) =>
                ApprovalDecision.Deny("automation.outside-contract"),

            _ => ApprovalDecision.Deny("actor.unrecognized"),
        };
}
```

Every entry path now does the same small thing:

```csharp
var invoices = app.MapGroup("/invoices").RequireAuthorization("invoices.approve");   // early R2

invoices.MapPost("/{id:guid}/approve", async (
        Guid id, ClaimsPrincipal user, InvoiceApprovalService approvals, CancellationToken ct) =>
    (await approvals.ApproveAsync(ToActor(user), id, ct)).ToHttpResult());

invoices.MapPost("/approve-batch", async (
        Guid[] ids, ClaimsPrincipal user, InvoiceApprovalService approvals, CancellationToken ct) =>
    {
        Actor actor = ToActor(user);
        var results = new Dictionary<Guid, string>();
        foreach (Guid id in ids.Distinct())
        {
            // One decision per invoice. A batch is many operations, not one wider permission.
            results[id] = (await approvals.ApproveAsync(actor, id, ct)).ReasonCode;
        }

        return Results.Ok(results);
    });

public sealed class ChatApprovalHandler(IChatIdentityMap identities, InvoiceApprovalService approvals)
{
    public async Task<ApprovalResult> HandleAsync(ChatApprovalClicked click, CancellationToken ct)
    {
        // The platform's verified interaction identifies a chat account. Which Payables user
        // and tenant that account is comes from our own mapping, never from the message body.
        Actor.Person? actor = await identities.FindPersonAsync(click.ChatWorkspaceId, click.ChatUserId, ct);
        return actor is null
            ? ApprovalResult.Denied("chat.unlinked-account")
            : await approvals.ApproveAsync(actor, click.InvoiceId, ct);
    }
}
```

The nightly job calls the same service with `new Actor.Automation("recurring-contract-approval", tenantId)`. The job runner creates that actor from trusted job configuration and the tenant it is processing, and accepts only process names from a configured allow-list. No request-controlled input may select an automation identity or process name, and the directory, not the job, decides whether that process may approve invoices for that tenant. A per-invoice ceiling is the simplest contract rule; a cumulative contract budget would need the remaining budget as another fact, read and consumed in the same write.

Notice what moved and what stayed:

- **The endpoint policy stayed.** `RequireAuthorization("invoices.approve")` still rejects callers with no approval permission before any database work. It is now an early rejection, not the decision: the chat and job paths have no endpoint metadata, and the service checks R2 from the directory on every path anyway.
- **The tenant check became structural.** The store's tenant-scoped lookup makes R3 hard to forget. A global query filter on `Invoice` can back it up for every other query in the application.
- **R6 appears twice, deliberately.** The policy returns a readable reason. The entity guarantees the rule for every caller, including code that does not go through the service. One is the guarantee and the other is a better error message.
- **The batch is a loop over single decisions.** The bulk screen cannot approve anything the single-invoice path would refuse.

### Be explicit about what the conditional write protects

`TryApproveAsync` protects the invoice version that was evaluated, assuming the store implements the conditional update correctly. It does not protect the other facts the policy read. If finance lowers the approver's limit from 5,000 to 1,000 between the directory read and the write, a 4,000 invoice is still approved, because the invoice did not change.

For Payables, that is an acceptable, stated guarantee: the approver's permission and limit are evaluated at the time of the request, and the write is bound to the invoice that was evaluated. When revocation must take effect before the write commits, the write must cover those facts too, for example by including the limit table's version in the same conditional update or by reading and writing them in one sufficiently isolated transaction. Either choice is reasonable. The mistake is not stating one.

### Make the effect reachable only through the operation

Moving the check into the service protects the effect only if the service is the only way to cause it. Close the other doors:

- Remove the public setter on `Invoice.Status`. Route ordinary mutation through an `Approve` method that enforces R6, and limit its accessibility, for example to `internal` in the assembly that owns the invoice or to the store's `TryApproveAsync`, so application code cannot casually bypass the operation.
- Keep `TryApproveAsync` off general-purpose repositories. A new caller of it is a review event.
- Bind the write to the evaluated state with a version or concurrency token, so a change between evaluation and write produces a conflict instead of a silent approval. [Your Pipeline Behavior Can Watch the Operation](pipeline-behavior-should-not-decide-the-operation.md#keep-the-decision-true-until-the-write) shows the conditional update and its EF Core equivalent.

Accessibility modifiers such as `internal` are a control against accidental misuse, not a security boundary against hostile code in the same process. That is the right threat model for this problem: the bulk endpoint was not written by an attacker. It was written by a colleague who did not know the self-approval check existed.

---

## Domain Rules Are Not a Substitute for Authorization

Two opposite mistakes are common.

**Every caller-dependent rule pushed into the entity.** An entity can check caller-dependent rules without doing I/O: the service loads an immutable approver value and passes it to `invoice.Approve(approver)`. That is a legitimate design, and some teams prefer it because the rules live next to the data. Its cost is that the entity's API now models callers, limits, and kinds of automation, and a new kind of actor may require changes to the domain API. Payables keeps the entity to rules about what an invoice can become and keeps rules about who may cause it in the policy. Both designs work if the split is deliberate.

**Invariants left to authorization.** If R6 lives only in the policy, any path that does not call the policy can approve a paid invoice: a future endpoint, or a job that reuses the store. The rule was never about permission. It was about what an invoice can become.

R5 sits between them. "The submitter may not approve their own invoice" is a separation-of-duties control. In Payables it applies only to people. The nightly job has no submitter conflict, so it lives in the policy. In an organization whose auditors require that *no* approval, including automated ones, share an identity with the submitter, it would become an invariant that the entity checks against the recorded submitter and approver. Either placement is defensible if it is a deliberate, written choice. What fails is a rule that each team member assumes the other layer is enforcing.

---

## Data-Access Filters Are a Backstop, Not the Decision

A tenant filter is one of the most valuable defenses a multi-tenant .NET application can have. It is still not where the tenant decision is made:

```csharp
modelBuilder.Entity<Invoice>()
    .HasQueryFilter(invoice => invoice.TenantId == _tenantContext.TenantId);
```

This filter is only as good as `_tenantContext`. If the tenant is set from authentication, the filter quietly protects every query a developer forgets to scope. If it is ever set from a route value, header, or request body, the filter faithfully enforces whatever tenant the caller asked for. The filter reads the tenant from the context instance that runs the query, so a background job that processes several tenants should create a separate scope, and set the tenant, for each unit of work rather than reusing one context across tenants.

Keep the explicit, tenant-scoped lookup in the operation as well. The filter catches what the code forgets. The code states what the operation requires. When the two disagree in a test, you have found a bug before a customer did.

---

## A Policy Engine Decides. Your Code Still Enforces.

Teams sometimes ask whether adopting a policy engine answers the placement question. It answers one quarter of it.

```text
Entry path        →  establishes the actor
Operation         →  reads authoritative facts
Policy engine     →  decides, given those facts
Operation         →  enforces: performs the effect only on an allow
```

In this design, the operation supplies the facts. Engines such as Open Policy Agent can also fetch external data during evaluation, which moves the question of which facts are authoritative into the engine's configuration rather than removing it. Either way, the engine never performs the approval or calls the payment API. If the operation sends `{ "amount": 900, "limit": 5000 }` that it read from its own stores, the engine decides correctly. If the operation forwards a limit that arrived in the request, the engine decides correctly about a false premise. And if any entry path performs the effect without asking, the engine is not involved at all.

The enforcement point also needs an explicit failure policy. When the decision service is unavailable, times out, or returns something the caller cannot interpret, a consequential operation should normally fail closed or defer rather than proceed. [Should Authorization Fail Open, Fail Closed, or Defer?](fail-open-fail-closed-or-defer.md) covers that choice.

Externalizing policy is worth it when several services must share one policy or when policy changes on a different schedule from the code. It does not remove the need for one enforcement point per effect.

---

## When the Effect Happens Later or Elsewhere

The approval is a decision about whether this person may approve this invoice. The payment run executes days later, and by then a different question matters.

Suppose that after the invoice is approved, someone changes the vendor's bank account. That creates an obvious payment-redirection risk: the approved invoice is untouched, but the money would go somewhere else. The approver approved paying *this vendor's account as it was*. The approval is still a true historical fact, and it is no longer enough.

The payment run is the only component standing in front of the money, so it owns R7:

```csharp
public static string? IneligibleForPayment(InvoiceSnapshot invoice, VendorSnapshot vendor)
{
    if (invoice.Status != InvoiceStatus.Approved) return "invoice.not-approved";
    if (vendor.PaymentHold) return "vendor.payment-hold";

    // Recorded at approval time: the bank-account version the approver was shown.
    if (vendor.BankAccountVersion != invoice.ApprovedBankAccountVersion) return "vendor.bank-details-changed";

    return null;
}
```

This requires the approval to be bound to what the approver actually reviewed, which has two parts:

- **Record the version the approver saw.** The approval screen shows the vendor's bank details along with a version that the server issued. The approve request sends that version back, and the service treats it as an assertion to validate: it approves only if the version still matches the current vendor record, and stores it with the approval. Reading the latest version when the request arrives would record what was current, not what the approver saw.
- **Pay to exactly what was approved.** Checking the version and then fetching the payment destination in a separate step leaves another window for change. The payment instruction should use the destination stored with, or locked to, the approved version, or check the version and read the destination in the same transaction.

The earlier `ApproveAsync` snippet omits the reviewed-version input and its persistence; implementing R7 requires extending that flow. Checking the vendor version and recording the approval also need the consistency guarantee the application intends, such as including the vendor version in the conditional write, rather than assuming a separate read is still current.

The payment run does not re-run the approver's permission check; that decision is history. In this example, an approver who later leaves or loses the permission does not by that fact alone invalidate a completed approval; if your policy says otherwise, that is one more execution-time rule to add here. The payment run checks the facts that must still hold for *execution*: status, holds, and the bank details the approval covered.

The same separation applies to any queue, worker, or downstream service. The executor should receive an operation identifier and read current state, not a copy of the requester's claims or token. [How Short-Lived Execution Authority Differs from User Authorization](short-lived-execution-authority-vs-user-authorization.md#which-facts-must-still-be-true-on-friday) covers the handoff in detail, and [Your Retry Logic May Be Replaying an Authorization Decision](retry-logic-may-replay-authorization-decision.md) covers what happens when that executor retries.

---

## Early Checks Should Preserve the Final Policy

Once there is one deciding check, what should the earlier layers do? A simple rule keeps them useful without letting them drift into a second decision:

> **An early check may reject, but every operation the deciding check would allow should also pass the early check. And an early check should never be the only thing that allows.**

`RequireAuthorization("invoices.approve")` is meant to follow the rule: R2 is part of the policy, so a caller without the permission would be refused by the service anyway. The early check saves a database round trip and narrows the attack surface.

It follows the rule only as far as its inputs are current. The endpoint policy reads the permission from a claim issued at sign-in, and the service reads it from the directory. A person granted the permission an hour ago passes the service's check but fails the endpoint's until they sign in again. Claims suit identity and other facts whose acceptable staleness matches the credential's lifetime. For frequently changing permissions or limits, either accept that window deliberately or read them at decision time in both places.

Three things break the rule:

- **An early check that is stricter than the decision.** An endpoint policy that also requires an "Invoice Manager" role, while the service policy does not, means the chat bot can approve invoices that the web application cannot. Now there are two policies, and nobody chose the difference.
- **An early check that is the only check.** Hiding the Approve button for users without the permission is good user experience. It is not authorization, because the endpoint behind the button accepts requests from any client. The [OWASP Authorization Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html) makes the same point: authorization decisions must be enforced on the server, on every request.
- **An early check that copies the operation rule but drifts.** A copy of the same rule at the edge has the same standing as the original, and the two will eventually disagree.

The rule applies to checks that stand for the same operation permission. Requirements that belong to a channel can legitimately differ. For example, the chat path may require that the chat account was linked within the last 90 days, or the web path may require multi-factor authentication. Those are extra conditions on how an actor is established, not a second approval policy.

When a rule must appear in two places, as R6 does, make one of them the guarantee and say so in the code. Copying the same rule into several places with equal standing is how the self-approval check went missing.

---

## Avoid Both Extremes

**Scattered checks.** Each endpoint, handler, and job implements the rules it remembers. This is the Payables starting point. It fails gradually, as entry paths multiply and copies diverge. Its tell is a rule whose implementations you cannot list without searching.

**A central "authorization service" that owns more than it can know.** The opposite reaction builds one `IAuthorizationGate.CanAsync(user, action, resourceId)` for the whole application. To answer for invoices, it must load invoices, limits, and vendors, so it couples to every module's storage. Otherwise it accepts the facts as parameters, and each caller supplies whatever it believes. It also becomes the place where every module's rules are written by one team that understands none of them deeply. Its tell is a resource ID passed in with no resource loaded, or a growing `switch` on action names.

The middle path is less exciting: each operation owns its deciding check and calls a policy it can test, using facts it reads from the stores it owns. Shared infrastructure handles what really is shared: authentication, the fallback policy, the actor type, logging of decisions, and, when it is justified, a policy engine that the operations call.

---

## A Proportional Path

Placement is not a maturity ladder. Most operations should stay at the first stage that works:

1. **Endpoint policy only.** A single entry path, and a rule that depends only on the caller: authenticated users may read their own profile; administrators may change site settings. `RequireAuthorization` with a fallback policy is the whole design.
2. **Resource-based check in the endpoint.** Still one entry path, but the rule needs the loaded resource: tenant, ownership, status. `IAuthorizationService.AuthorizeAsync(user, resource, policy)` in the handler.
3. **Deciding check in the application service.** A second entry path appears, or the operation is consequential enough that a missed check would be costly. Move the check into the use case, pass the actor explicitly, and make the effect reachable only through it. Most of Payables lives here.
4. **Separate decision and execution boundaries.** The effect happens later, elsewhere, or under a different identity. The executor checks execution-time eligibility from current state. The payment run lives here.
5. **Externalized policy.** Several services enforce the same policy, or policy changes on its own release cycle and needs its own versioning, testing, and audit. The operations still read facts and enforce; an engine decides.

Moving a step later should be a response to a specific pressure, such as a new entry path, a delay before execution, or shared policy. It should not be a response to the existence of the next step.

---

## Testing That Every Path Is Covered

The tests that matter assert that the effect did not happen, through each real entry path, not that one method returned `Denied`. They are specifications of the behavior described above, not evidence of a working implementation, and each demonstrates behavior only under the conditions it sets up.

```csharp
public static TheoryData<EntryPath> AllEntryPaths =>
    new() { EntryPath.Api, EntryPath.Batch, EntryPath.Chat };

[Theory]
[MemberData(nameof(AllEntryPaths))]
public async Task Submitter_cannot_approve_their_own_invoice(EntryPath path)
{
    var harness = PayablesHarness.Create();
    harness.Directory.GrantApprover("clerk-17", limit: 5_000m);
    Guid invoice = await harness.SubmitInvoiceAsync(submittedBy: "clerk-17", amount: 900m);

    await harness.ApproveThroughAsync(path, invoice, userId: "clerk-17");

    Assert.Equal(InvoiceStatus.AwaitingApproval, await harness.StatusAsync(invoice));
    Assert.Empty(harness.Store.ApprovalsWritten);
}

[Theory]
[MemberData(nameof(AllEntryPaths))]
public async Task Approval_over_the_current_limit_is_refused(EntryPath path)
{
    var harness = PayablesHarness.Create();
    harness.Directory.GrantApprover("approver-4", limit: 5_000m);
    Guid invoice = await harness.SubmitInvoiceAsync(submittedBy: "clerk-17", amount: 4_000m);
    harness.Directory.SetLimit("approver-4", 1_000m);   // lowered after sign-in

    await harness.ApproveThroughAsync(path, invoice, userId: "approver-4");

    Assert.Empty(harness.Store.ApprovalsWritten);
}

[Fact]
public async Task Payment_run_does_not_pay_after_bank_details_change()
{
    var harness = PayablesHarness.Create();
    Guid invoice = await harness.ApprovedInvoiceAsync(vendor: "V-311");
    await harness.ChangeVendorBankAccountAsync("V-311");

    await harness.RunPaymentsAsync();

    Assert.Empty(harness.PaymentProvider.PaymentsSent);
}
```

`ApproveThroughAsync` drives each path through its real composition: the HTTP pipeline through `WebApplicationFactory` for the API and batch endpoints, and the registered message handler for the chat path. A test that calls `InvoiceApprovalService` directly exercises the policy. Tests through each entry path demonstrate that the tested restrictions hold at that boundary; architecture tests, described below, separately check that callers use the shared operation. The second test shows that a limit lowered after sign-in is respected; on its own it does not prove which source supplied the limit, so pair it with a test of the directory the service reads from.

Two more checks are worth automating:

- **An architecture rule that the effect has one caller.** An architecture-test library or a Roslyn analyzer can fail the build when any type other than the invoice store calls `Invoice.Approve`, or when anything outside `InvoiceApprovalService` calls `TryApproveAsync`.
- **Coverage tests for endpoint metadata.** Enumerate the application's endpoints and assert that each has authorization metadata or an explicit `AllowAnonymous`, which catches the endpoint someone forgot. Separately, send an unauthenticated HTTP request to an endpoint without metadata and assert that the fallback policy rejects it; the enumeration proves the metadata exists, and only the request proves the pipeline enforces it.

Also cover a cross-tenant invoice ID on every path (expect not found and no write), the nightly job approving an over-contract invoice (expect a denial), an automation process the directory has not enabled for the tenant approving an otherwise eligible invoice (expect a denial and no write), a concurrent change to the invoice between evaluation and write (expect a conflict and no approval), and a vendor placed on hold after approval (expect no payment). [How to Test That a Denied Operation Never Executes](test-denied-operation-never-executes.md) develops the technique of counting calls to the executor.

---

## When an Endpoint Policy Is the Whole Answer

For a great many endpoints, stage 1 or 2 of the proportional path is the entire design, and adding a service layer, an actor type, and architecture tests would be ceremony. An endpoint policy, or a resource-based check in the endpoint, is enough when:

- the endpoint is the only way to cause the effect, and is likely to stay that way;
- the effect happens within the request, in the same process;
- the rule depends on the caller and, at most, the resource the endpoint loads;
- a fallback policy rejects anonymous requests when authorization metadata is missing, and a test verifies the endpoint's required operation permission;
- a test sends a request without the required permission and asserts no effect.

Read endpoints often meet all five conditions, provided their queries are scoped by tenant or ownership in the query itself. The signal to move the check is not size or seniority. It is a second door into the same room: a batch endpoint, a message consumer, a job, an admin tool, or an executor that acts later.

---

## Failure Modes

### 1. The second entry path

A new endpoint, consumer, job, or tool performs the effect with a partial copy of the rules. This is a recurring way correctly implemented authorization becomes incomplete, and it usually arrives as a feature, not a bug.

### 2. Endpoint metadata as the only check for resource rules

A policy attribute is expected to enforce tenant, ownership, or status against a resource the endpoint has not loaded yet, or a handler fetches its own copy that can differ from what the endpoint then changes.

### 3. Identity from ambient state

The service reads `IHttpContextAccessor` to find the user. It works for HTTP and silently has no user, or the wrong one, in a job or consumer.

### 4. Facts from the request

Tenant, limit, owner, or price arrives in the request and is used as a fact. Authentication proves who sent the request, not that its fields are true.

### 5. Facts frozen at sign-in

An approval limit, role, or account status is read from a claim issued hours earlier, with no decision about whether that much staleness is acceptable.

### 6. Invariants left to authorization

A state rule lives only in the policy, so a path that skips the policy can make a transition the domain should never allow.

### 7. Caller-dependent rules coupled to identity infrastructure

The entity depends directly on HTTP state, identity-provider claims, or directory access, which makes jobs and other callers hard to support. Passing immutable, application-loaded actor facts into the entity is a different and legitimate design.

### 8. A filter or engine mistaken for the decision and its enforcement

A tenant query filter, row-level security policy, or remote policy decision is treated as the place the operation is allowed, while some code path performs the effect without it.

### 9. Early checks that disagree with the decision

An endpoint or UI rule rejects operations the deciding check would allow, or is the only thing that allows, so different entry paths enforce different policies.

### 10. The executor that trusts the approval forever

A later worker performs the effect because approval once happened, without checking the facts that must still hold at execution time.

---

## A Short Review Checklist

**Entry paths**

1. Can you list every path that can cause this side effect: endpoints, batch operations, consumers, jobs, admin tools, and scripts?
2. Does every one of them pass through the same deciding check, and is that check where the paths meet?
3. Is the effect reachable only through that operation, with no public setter or general-purpose repository method that bypasses it?

**Responsibilities**

4. Does each entry path establish the actor from its own authenticated source and pass it explicitly?
5. Are security-relevant facts read from the stores that own them, not from the request or from long-lived claims?
6. Which component decides, and which component enforces? If a policy engine decides, does your code still refuse the effect without an allow?
7. Are invariants enforced for every supported mutation path, and is the placement of caller-dependent rules a deliberate choice?

**Layers**

8. Does every operation the deciding check would allow also pass each early check that stands for the same permission, apart from deliberate channel-specific requirements, and is no early check the only allow?
9. Is there a fallback policy so an endpoint without authorization metadata requires authentication, and does each consequential endpoint still declare its operation permission explicitly?
10. Are query filters and row-level security keyed on a trusted identity, either the authenticated application context or a trusted database identity, are writes covered as well as reads, and are they treated as defense in depth rather than as the decision?

**Later execution**

11. If the effect happens later, does the executor check current execution-time eligibility, using the facts the decision recorded?

**Tests**

12. Is there a test, through each real entry path, that a forbidden request produces no effect?

---

## Continue Deeper

If your application has only one entry path and you are deciding how much framework to use, [When ASP.NET Core Authorization Is Enough](../../architecture/when-aspnet-core-authorization-is-enough.md) shows how far built-in policies and resource-based handlers go, and [When a Simple Application Service Is Enough](../../architecture/when-a-simple-application-service-is-enough.md) makes the case for the plain use-case boundary this article recommends for most consequential operations.

If the operation's outcome is more than allow or deny, or execution is separated from the request, [Decision Before Execution](../../tutorials/decision-before-execution.md) walks through the proposal, decision, and host-owned execution boundary step by step, with a runnable sample.

If several services need the same policy, [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../../architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md#4-policy-decision-points-and-policy-enforcement-points) covers decision and enforcement placement across services, including why the enforcement point owns the last meaningful check.

To practice consolidating scattered checks into one deciding step, work through [Refactor Scattered Governance Checks into an Explicit Decision Pipeline](../../labs/refactor-scattered-governance-checks.md).

### Related Work

- [Resource-based authorization in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased), Microsoft's documentation, explains why attribute-based authorization cannot see the resource and shows imperative checks with `IAuthorizationService`.
- [Policy-based authorization in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies) describes requirements, handlers, and policies.
- [Global query filters](https://learn.microsoft.com/en-us/ef/core/querying/filters), in the EF Core documentation, covers multi-tenancy filters and how they are disabled.
- [Row-level security in SQL Server](https://learn.microsoft.com/en-us/sql/relational-databases/security/row-level-security) and [PostgreSQL row security policies](https://www.postgresql.org/docs/current/ddl-rowsecurity.html) describe filter and block predicates and policies that restrict both reads and writes.
- The [OWASP Authorization Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html) recommends denying by default, validating permissions on every request, and enforcing authorization on the server.

---

## The Short Answer

There is no single layer where authorization lives, because authorization is not a single thing. Establish who is asking at the host boundary where the credential is validated. Read the facts from the stores that own them. Decide where those facts and the policy are available. Enforce at the point every path to the side effect must cross.

In many ASP.NET Core applications, that means a fallback policy and coarse endpoint policies at the edge, the deciding check inside the application service that performs the use case, invariants in the domain, tenant filters underneath as a backstop, and an execution-time check in any component that acts later. Earlier layers may reject early, but apart from deliberate channel-specific requirements they should not reject what the deciding check would allow, and they should never be the only thing that says yes. When an endpoint is the only door and the effect happens inside the request, an endpoint policy is the whole design. Revisit the question when a second door appears.
