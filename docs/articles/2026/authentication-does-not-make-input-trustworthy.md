---
description: A valid token tells you who sent an ASP.NET Core request. It does not make the account IDs, tenant, prices, or status in that request true.
title: Passing Authentication Does Not Make Input Trustworthy
author: Christopher D. Cavell
published: "2026-11-07"
summary: Authentication establishes who sent a request, as asserted by an issuer you trust. It does not make the request's identifiers, tenant values, prices, or status true. Sort every field by who owns it. Take identity from the validated principal, resolve selectors and check that this caller may use them, validate the input the caller owns, compute the facts the server owns, and treat the client's copy of a server-owned value as a precondition. One scoped read per resource is usually enough.
feed: true
x_hashtags:
  - DotNet
  - CyberSecurity
---

# Passing Authentication Does Not Make Input Trustworthy

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** No formal prerequisites. Familiarity with ASP.NET Core minimal APIs or controllers, JWT bearer authentication, and EF Core is helpful. No AsiBackbone package or prior Learning material is required.

**Sample scope:** The C# targets .NET 8 or later. The entities, price catalog, validator, and test helpers are illustrative, not a runnable sample or a recommended domain model. Framework behavior is described from Microsoft's documentation as of November 2026; check the current documentation for your versions.

**What this article covers:** what a validated token does and does not establish; five ways an authenticated request can still be wrong; sorting request values into identity, selectors, caller-owned input, server-owned facts, and hints; which component owns each concern in ASP.NET Core; an order endpoint rebuilt so each field is handled by its owner; why validation and authorization answer different questions; using the client's copy of a price as a precondition; which token claims can decide access; how to avoid re-reading every field; the same mistake with gateways, internal services, webhooks, and AI tool calls; and when ordinary validation is enough.

**In plain terms:** a courier shows a valid company badge at the loading dock and hands over a delivery note. The badge proves the courier works for a company you deal with. It does not prove the note's prices are current, that the goods are addressed to the right warehouse, or that the "pre-approved" stamp on it came from anyone who may approve. The dock checks the badge once, and then checks each line of the note against whoever actually owns that fact.

Consider a hypothetical wholesale ordering platform. Each tenant is a distributor. Each distributor has customer accounts, and buyers at those customers sign in and place orders. Every API call requires a valid access token, and the order endpoint requires an `orders.place` policy. A quarterly review of disputed orders finds five problems. Every request involved was authenticated.

1. **Old prices.** A mobile app release cached the price list and kept sending last month's unit prices after a price increase. The server stored them. Nobody was attacking anything.
2. **Someone else's account.** A buyer who had moved from one customer to another still had the old customer's account ID bookmarked. Orders went onto the old customer's credit line.
3. **The wrong tenant.** An integration partner's client read `TenantId` from a configuration file that had been copied between environments. For two days its orders landed in another distributor's tenant.
4. **A negative line.** A quantity of `-40` produced a negative total, which the credit check read as available credit.
5. **A self-approved order.** A buyer's script added `"status": "Approved"` to the request body. The orders skipped credit review and went straight to the warehouse.

The endpoint looked like this:

```csharp
// Anti-pattern: the request body is bound straight to the entity.
app.MapPost("/api/orders", async (Order order, ClaimsPrincipal user, OrderingDbContext db, CancellationToken ct) =>
    {
        // An authorization-looking check on the one field that matters least.
        if (order.PlacedByUserId != user.FindFirstValue("sub"))
        {
            return Results.Forbid();
        }

        // TenantId, CustomerAccountId, ShippingAddressId, UnitPrice, Total, and Status
        // all arrive from JSON and are saved as they are.
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/orders/{order.Id}", order);
    })
    .RequireAuthorization("orders.place");
```

The team's first reaction was to treat buyers as hostile and add checks everywhere. That would not have explained the first three problems, which were mistakes, and it would not have pointed to the fix. The real mistake was in what the code assumed authentication had settled.

> **Authentication tells you who sent the request. It does not make anything the request says true. Take identity from the authenticated principal. Resolve every identifier the caller sends and confirm that this caller may use it. Validate the input the caller owns. Compute the facts the server owns, and treat the caller's copy of them as something to compare, never something to store.**

That does not mean every field needs its own database lookup, or that callers should be treated as attackers. Near the end, this article covers how little extra work most endpoints need.

---

## What Authentication Actually Established

When the JWT bearer handler accepts a token, it has checked that a trusted issuer signed it, that it was issued for this API, and that it has not expired. The claims inside it are what that issuer asserted when it issued the token. That is a real and useful guarantee:

- **The caller is who the token says.** The `sub` or object ID and the tenant ID identify the user, as the issuer knows them. Your code can rely on these without a database read.
- **Issuer-assigned claims are the issuer's statements.** Application roles that an administrator assigned can be relied on to the extent that you trust that administrator, as of the token's issue time.

It does not establish anything else:

- **The body, route, query string, and headers.** The token is attached to the request, and the request is not signed by the token. A valid token says nothing about the JSON next to it.
- **That the client is your client.** A public client, whether a browser app, a mobile app, or a script, runs on a device you do not control. The request may come from a modified build, a stale build, or `curl`. Checks done only in the client can be skipped. [CWE-602](https://cwe.mitre.org/data/definitions/602.html) names this weakness: client-side enforcement of server-side security.
- **Current state.** Claims are a snapshot from issue time. A buyer removed from an account an hour ago may still carry a token issued before the removal.
- **Permission for this operation on this resource.** `orders.place` says the caller may place orders somewhere. It says nothing about account `C-2041`.

Two misconceptions sit on either side of this. One treats a valid token as proof that the request is honest. The other treats every claim in a validated token as attacker-controlled. Neither is right. The token is reliable for who the caller is and what the issuer asserted. The rest of the request needs to be judged field by field.

---

## Five Ways an Authenticated Request Can Be Wrong

The five incidents are different failures, and each has a different owner:

| Kind of wrong | In the example | Who caused it | What catches it |
| --- | --- | --- | --- |
| **Malformed** | Quantity `-40` | A client bug, or anyone | Shape and range validation at the API boundary |
| **Stale** | Last month's prices | An old client build | Computing the price on the server, and comparing the client's copy as a precondition |
| **Unauthorized** | The old customer's account ID | A buyer acting in good faith | Resolving the account and checking that this buyer may use it |
| **Cross-tenant** | `TenantId` from the wrong configuration file | A misconfigured integration | Taking the tenant from the authenticated principal |
| **Adversarial** | `"status": "Approved"` | A buyer trying to skip review | Never binding server-owned fields from the request |

Only one of the five was an attack. All five were authenticated. The fix for each is the same habit: know who owns each value, and let only that owner set it.

The second and fifth problems are common enough to have names. [Broken Object Level Authorization](https://owasp.org/API-Security/editions/2023/en/0xa1-broken-object-level-authorization/) is first in the OWASP API Security Top 10 (2023), and [CWE-639](https://cwe.mitre.org/data/definitions/639.html) describes authorization bypass through a user-controlled key. Writing fields the caller should not control is mass assignment ([CWE-915](https://cwe.mitre.org/data/definitions/915.html)), which OWASP now groups under [Broken Object Property Level Authorization](https://owasp.org/API-Security/editions/2023/en/0xa3-broken-object-property-level-authorization/).

---

## Sort Every Value by Who Owns It

Before writing any check, sort each value the request carries into one of five kinds. The kind decides what happens to it.

| Kind | Who is authoritative | What the server does | Examples in the order request |
| --- | --- | --- | --- |
| **Identity** | The authentication boundary | Read from the validated principal. Ignore or reject any copy in the request. | User ID, tenant ID |
| **Selectors** | The caller chooses *which*; the server decides *whether it may* | Resolve to an authoritative record, scoped to what this caller may use | Customer account ID, shipping address ID, SKU |
| **Caller-owned input** | The caller, for its own intent | Validate shape, range, and business rules. Then accept. | Quantity, purchase-order number, delivery notes, requested date |
| **Server-owned facts** | A system of record | Look up or compute. Never bind from the request. A client copy is at most a precondition. | Unit price, total, discount eligibility, credit limit, order status |
| **Hints** | Nobody, unless infrastructure you configured vouches for them | Use for non-security purposes, or only after a trusted component establishes them | Locale, client time zone, `X-Forwarded-For`, app version |

Applied to the original `Order` entity:

| Field | Kind | Treatment |
| --- | --- | --- |
| `PlacedByUserId` | Identity | From `sub`. The original check compared the body with the token and then ignored everything else. |
| `TenantId` | Identity | From the token's tenant claim. A different value in the body is a mistake or an attempt, never an instruction. |
| `CustomerAccountId` | Selector | Must exist in this tenant, and this buyer must be assigned to it with permission to order. |
| `ShippingAddressId` | Selector | Must belong to *that* account. Checking only the first identifier in a request is a common gap. |
| `Lines[].Sku` | Selector | Must exist in the catalog that applies to this account. |
| `Lines[].Quantity` | Caller-owned input | A positive integer within a sensible range. Minimum order quantities are a business rule. |
| `Lines[].UnitPrice` | Server-owned fact | From the account's price list today. |
| `Total` | Server-owned fact | Computed from server prices. |
| `Status` | Server-owned fact | Set by the server, based on account state and credit. |
| `PurchaseOrderNumber` | Caller-owned input | Validate length and characters. Store it as text and encode it on output. |

Two things about this sort are easy to miss.

**Caller-owned input is genuinely authoritative.** When the buyer says "twelve pallets," that is the truth about what the buyer wants. The server validates that twelve is a legal quantity. It does not need to doubt that the buyer meant twelve. Treating every value with suspicion buries the few that need it.

**Where a value arrives does not change its kind.** A tenant ID in the route (`/tenants/{tenantId}/orders`), a header (`X-Tenant-Id`), or the query string is still a value the caller chose. A route parameter is not more trustworthy than a body field. If the route carries the tenant for routing, compare it with the principal's tenant and refuse a mismatch.

---

## Each Concern Has an Owner

In ASP.NET Core, the five kinds of wrong map to different components. Mixing them up is how an endpoint ends up with checks that look thorough and miss the case that matters.

| Concern | Component that owns it | ASP.NET Core mechanism | It is not |
| --- | --- | --- | --- |
| Is this a well-formed request? | Binding and validation | DTOs, DataAnnotations or a validator, `[ApiController]` automatic `400`, `Results.ValidationProblem` | Permission |
| Who is calling? | Authentication | JWT bearer or cookie handler, `ClaimsPrincipal` | Permission for this resource |
| May this caller use this resource? | Resource-based authorization | A scoped query, or `IAuthorizationService.AuthorizeAsync(user, resource, requirement)` | Validation that the ID is well formed |
| What are the authoritative facts? | Systems of record | Repositories, the price catalog, the credit service | Anything the client sends |
| Does the operation satisfy business rules? | Application service or domain model | Credit check, minimum order, account on hold | Authorization of the caller |
| Is the client acting on what it last saw? | Preconditions | An expected total, a version, `If-Match` | A source of the current value |

The first row and the third row are where most confusion lives. `[Range(1, 10_000)]` on `Quantity` says the number is sensible. It does not say this buyer may order on this account. And an authorization handler that confirms the buyer is assigned to the account does not say that `-40` is a valid quantity. Each answers its own question. An endpoint needs both, and neither substitutes for the other.

Business rules are a third, separate question. A buyer may be fully authorized to place orders on an account that is on credit hold. The order is then refused, or held for review, because of the account's state, not because of who the buyer is. Putting "account on hold" inside an authorization policy makes the policy depend on facts that change for reasons unrelated to the caller, and it turns a business outcome into a `403`.

For a fuller guide to which layer should establish facts and which should decide, see [Where Should Authorization Live in a .NET Application?](where-authorization-should-live-dotnet-application.md).

---

## The Endpoint, Rebuilt

### The request carries only what the caller owns

```csharp
// Only selectors, caller-owned input, and one precondition. No identity,
// no prices, no total, no status.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlaceOrderRequest(
    Guid CustomerAccountId,
    Guid ShippingAddressId,
    IReadOnlyList<PlaceOrderLine> Lines,
    decimal ExpectedTotal,
    string? PurchaseOrderNumber);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlaceOrderLine(string Sku, int Quantity);
```

The safety comes from the shape of the type. A request DTO with no `Status` property cannot set a status, whatever the JSON says. Microsoft's web API tutorial describes the same pattern as [preventing over-posting](https://learn.microsoft.com/en-us/aspnet/core/tutorials/first-web-api).

`JsonUnmappedMemberHandling.Disallow` is a separate choice. By default, System.Text.Json [ignores JSON properties that have no matching member](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members), so `"status": "Approved"` would be silently dropped. That is already safe. `Disallow` makes deserialization fail instead, which minimal APIs return as `400 Bad Request`. The benefit is that the attempt becomes visible, and so does the misconfigured integration that still sends `tenantId`. The cost is that a client sending a field the server does not yet know about breaks. Many teams disallow unknown members on write endpoints for public clients and allow them elsewhere. Either choice is defensible. What matters is that safety does not depend on it.

### Validation answers "is this a sensible request?"

```csharp
public static class PlaceOrderValidator
{
    public static Dictionary<string, string[]> Validate(PlaceOrderRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Lines is not { Count: > 0 and <= 200 })
        {
            errors["lines"] = ["An order needs between 1 and 200 lines."];
        }
        else if (request.Lines.Any(line => line.Quantity is < 1 or > 10_000))
        {
            errors["lines"] = ["Each quantity must be between 1 and 10,000."];
        }
        else if (request.Lines.GroupBy(line => line.Sku).Any(group => group.Count() > 1))
        {
            errors["lines"] = ["Each SKU may appear only once."];
        }

        if (request.PurchaseOrderNumber is { Length: > 40 })
        {
            errors["purchaseOrderNumber"] = ["A purchase-order number has at most 40 characters."];
        }

        return errors;
    }
}
```

This is a hand-written validator so that the example does not depend on a particular library or framework version. DataAnnotations with `[ApiController]`, a validation library, or the built-in minimal API validation in recent ASP.NET Core versions all do the same job. None of them consult the database or the user. They only decide whether the request makes sense on its own.

### The handler sends each field to its owner

```csharp
app.MapPost("/api/orders", async (
        PlaceOrderRequest request,
        ClaimsPrincipal user,
        OrderingDbContext db,
        IPriceCatalog prices,
        CancellationToken ct) =>
    {
        // 1. Shape and range. Nothing below runs for a malformed request.
        Dictionary<string, string[]> errors = PlaceOrderValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // 2. Identity comes from the validated principal, never from the request.
        Buyer buyer = Buyer.FromPrincipal(user); // Reads sub and tid; fails if either is missing.

        // 3. The selector becomes a record, scoped to what this buyer may use.
        //    Tenant, existence, and assignment are checked in one query.
        CustomerAccount? account = await db.CustomerAccounts
            .Where(a => a.TenantId == buyer.TenantId
                && a.Id == request.CustomerAccountId
                && a.Buyers.Any(b => b.UserId == buyer.UserId && b.CanPlaceOrders))
            .SingleOrDefaultAsync(ct);

        if (account is null)
        {
            return Results.NotFound(); // Not found and not yours look the same to the caller.
        }

        // Every identifier in the request, not only the first one.
        bool addressBelongsToAccount = await db.ShippingAddresses
            .AnyAsync(a => a.Id == request.ShippingAddressId && a.CustomerAccountId == account.Id, ct);

        if (!addressBelongsToAccount)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["shippingAddressId"] = ["Choose one of this account's shipping addresses."],
            });
        }

        // 4. Server-owned facts: prices from the catalog that applies to this account today.
        PricedOrder priced = await prices.PriceAsync(account, request.Lines, ct);

        if (priced.UnknownSkus.Count > 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["lines"] = [$"Unknown or unavailable SKUs: {string.Join(", ", priced.UnknownSkus)}."],
            });
        }

        // 5. The client's total is a precondition. It is compared, never stored.
        if (priced.Total != request.ExpectedTotal)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Prices have changed.",
                extensions: new Dictionary<string, object?> { ["currentTotal"] = priced.Total });
        }

        // 6. Business rules and status belong to the domain, not to the caller.
        Order order = account.PlaceOrder(
            placedBy: buyer.UserId,
            shippingAddressId: request.ShippingAddressId,
            priced,
            request.PurchaseOrderNumber);

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/orders/{order.Id}", OrderResponse.From(order));
    })
    .RequireAuthorization("orders.place");
```

Walking the five incidents through it:

- **Quantity `-40`** fails validation in step 1, before any query.
- **The old customer's account ID** resolves to nothing in step 3, because the buyer is no longer assigned to it. The buyer gets `404`, and no order is created.
- **The wrong tenant** is impossible to express. The DTO has no `TenantId`, and the account query is scoped to the principal's tenant. With `Disallow`, the partner's request fails loudly with `400`, which is how the misconfiguration gets found in hours instead of two days.
- **Last month's prices** are never read. The server prices the order, and if the mobile app's total differs, the buyer gets `409` with the current total to confirm.
- **`"status": "Approved"`** has nowhere to go. `account.PlaceOrder` decides the initial status from the account's credit position.

Step 3 combines the lookup and the authorization into one query. That is a common and efficient form of resource-based authorization. When the rule is more involved, or shared with other endpoints, load the account first and call [`IAuthorizationService.AuthorizeAsync(user, account, requirement)`](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased) with an authorization handler. Either way, the buyer's permission is checked against the account the server loaded, not against anything the request claims about it. A tenant-scoped EF Core global query filter can supply the `TenantId` predicate everywhere, as long as it reads the tenant from the authenticated principal and has no "no tenant means all tenants" escape. [A Background Job Should Not Inherit Everything the User Could Do](background-job-should-not-inherit-user-authority.md#the-job-inherits-the-worker) shows how that escape goes wrong.

Returning `404` for an account the buyer may not use is a choice. It avoids confirming that an account ID exists. A `403` is also reasonable when existence is not sensitive. Use one response consistently, so the difference between the two does not leak what the choice was meant to hide.

---

## When the Client Sends a Value You Own

The mobile app showed the buyer a total before they pressed **Place order**. That total is useful information. It is the buyer's record of what they agreed to. It is not a price.

So the rebuilt request keeps it, renamed `ExpectedTotal`, and the server uses it only for comparison. If the server's price differs, the order is not placed, and the client shows the new total for the buyer to confirm. The result is a better outcome than either alternative:

- Accepting the client's price lets a stale or modified client set prices.
- Silently repricing charges the buyer an amount they never saw.

This is the same pattern as an `If-Match` header or a version number on an update: the client tells the server what it believes is current, and the server refuses to act if that belief is wrong. The client's value is a precondition, not an input. [Your Security Check Passed. Then the World Changed.](security-check-passed-then-world-changed.md) applies the same idea to approvals that must bind to exactly what a person reviewed.

The same treatment fits other server-owned values that a client legitimately displays: a quote version, an exchange rate, a shipping charge, or the status an editor saw before saving.

---

## Which Claims Can Decide Access

Once the body is handled, the remaining question is how far to trust the principal itself. Not every claim in a validated token is equally suitable for authorization. The token's signature proves who issued each claim. It does not prove how the issuer obtained it.

| Claim | Where its value comes from | Suitable for authorization? |
| --- | --- | --- |
| `sub`, or `oid` with `tid` in Microsoft Entra ID | The issuer's stable identifier for the user and tenant | Yes. This is the identity to key on. |
| Application roles or groups assigned by an administrator | Administrative assignment, as of issue time | Yes, for coarse permission, remembering it is a snapshot |
| `email`, `preferred_username`, `upn`, `unique_name` | Often editable by tenant administrators, and sometimes by users | No. Microsoft's guidance on [validating claims](https://learn.microsoft.com/en-us/entra/identity-platform/claims-validation) explicitly warns against using these to decide access. |
| Custom claims mapped from profile attributes | Whoever can edit that attribute, possibly the user | Only if you know who can write the attribute |
| Claims added by your `IClaimsTransformation` | Your code and the sources it reads | As trustworthy as those sources. A transformation that copies a request header into a claim has made the header look like identity. |
| A user ID or role in a header set by an API gateway | The gateway, if nothing else can reach the service | Only when the network guarantees that every request passed through the gateway, and the gateway strips any incoming copy |

Identity claims from a validated token do not need a database read to be believed. Permission claims are a snapshot. If a removed assignment must take effect before the token expires, check the assignment at the point of use, as step 3 does, rather than trusting the token's copy.

The same applies to network facts. `X-Forwarded-For` and `X-Forwarded-Proto` are request headers that any client can send. ASP.NET Core's Forwarded Headers Middleware applies them only from [known proxies or networks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer) that you configure. A client IP address used for rate limiting or risk scoring is only as trustworthy as that configuration.

---

## Do Not Re-Read Everything

The cure for trusting everything is not to doubt everything. Most fields need no lookup at all, and the ones that do can usually be checked together.

- **Caller-owned input needs validation, not lookups.** A quantity, a note, or a requested date is checked for shape and business rules. There is nothing to read.
- **Identity needs no read.** The validated principal is the source.
- **Each resource needs one scoped read.** Load the account with the tenant and assignment in the predicate. Existence, tenant, and permission come back in one query, and the result is the record you were going to use anyway.
- **Server-owned facts are read once, where they are used.** The price catalog is called once for the whole order. It would be called whether or not the client sent prices.
- **Child identifiers often fold into the parent's query.** The shipping address can be loaded through the account's navigation property, or checked in the same round trip.

In the rebuilt endpoint, every query except the address check would exist even if the client were perfectly trustworthy. The secure version is mostly the same work done in the right order, with the client's values in different places.

---

## The Same Mistake With Other Senders

The habit applies wherever an authenticated party sends data, not only to users.

**Internal services.** A service authenticated with a workload identity or mutual TLS sends `{"onBehalfOfUserId": "u-17", "tenantId": "t-4"}`. The credential proves which service sent the request. The user and tenant inside it are that service's assertion. Whether to accept them is a decision about how much you trust that service, and it should be written down rather than implied by "it's internal." Where the downstream service needs the user's own authority, delegation protocols such as On-Behalf-Of carry it in a token the issuer signs, rather than in a field the caller writes.

**Webhooks.** A valid signature proves the payload came from the provider. It does not prove the payload is the latest state, that it has not been delivered before, or that it refers to an object in your tenant. Treat event identifiers as selectors, de-duplicate deliveries, and look up current state from the provider when the decision matters.

**AI tool calls.** When a model produces arguments for a tool, the session may be authenticated as a real user, and the arguments are still proposals. They are selectors and caller-owned input at best, and they never establish identity, tenant, or permission. [Typed AI Proposed Intent and Schema-Validation Boundaries](../../ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md) applies this field-by-field sort to model output.

**Your own front end.** A single-page application written by your own team is still a public client. Hiding the **Approve** button for buyers is good design. It is not enforcement.

---

## When Ordinary Validation Is Enough

Many endpoints need only part of this.

An endpoint where every field is caller-owned input, scoped entirely to the caller's own data, needs validation and a query filtered by the caller's ID. A user updating their own display name and notification preferences is an example. There are no selectors to authorize and no server-owned facts to protect.

A read-only endpoint that returns records filtered by the principal's tenant and user needs the scoped query, and attention to which properties it returns. Returning fields the caller should not see is the read side of the same OWASP category as mass assignment.

The full treatment is for endpoints that do at least one of the following:

- Accept an identifier for a resource the caller might not be allowed to use.
- Carry, or are tempted to carry, a value that a system of record owns, such as a price, a balance, a status, or a limit.
- Accept identity or tenant values from anywhere other than the authentication boundary.
- Serve several tenants or several kinds of caller from the same code.

---

## Testing the Boundary

Unit tests of the validator are useful, but the failures in this article live in the gaps between components. Integration tests with `WebApplicationFactory` and a test authentication handler exercise the whole path. Each test should state what was sent, as whom, and what must not have happened.

```csharp
public sealed class PlaceOrderBoundaryTests(OrderingApiFactory factory) : IClassFixture<OrderingApiFactory>
{
    [Fact]
    public async Task Buyer_cannot_order_on_an_account_they_are_not_assigned_to()
    {
        HttpClient client = factory.CreateClientAs(TestBuyers.AliceAtNorthwind);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/orders", TestOrders.Valid(account: TestAccounts.Contoso));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await factory.CountOrdersAsync(TestAccounts.Contoso));
    }

    [Fact]
    public async Task A_status_in_the_body_is_rejected_and_nothing_is_saved()
    {
        HttpClient client = factory.CreateClientAs(TestBuyers.AliceAtNorthwind);
        JsonObject body = TestOrders.ValidJson(account: TestAccounts.Northwind);
        body["status"] = "Approved";

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/orders", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await factory.CountOrdersAsync(TestAccounts.Northwind));
    }

    [Fact]
    public async Task Prices_come_from_the_catalog_and_a_stale_total_is_a_conflict()
    {
        factory.Prices.Set("SKU-100", unitPrice: 12.50m);
        HttpClient client = factory.CreateClientAs(TestBuyers.AliceAtNorthwind);

        // The client still believes SKU-100 costs 11.00.
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/orders",
            TestOrders.Valid(account: TestAccounts.Northwind, lines: [("SKU-100", 10)], expectedTotal: 110.00m));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(125.00m, ((JsonElement)problem!.Extensions["currentTotal"]!).GetDecimal());
        Assert.Equal(0, await factory.CountOrdersAsync(TestAccounts.Northwind));
    }
}
```

Also cover:

- A body containing `tenantId`, `placedByUserId`, `unitPrice`, or `total`, each rejected, or, if you allow unknown members, ignored, with the stored order showing the server's values.
- A route or header tenant that differs from the principal's tenant.
- A shipping address that exists but belongs to another account in the same tenant.
- A buyer removed from an account after their token was issued, using that token.
- The same request from a buyer who *is* assigned, which must succeed. A boundary that refuses everyone passes every negative test.
- Zero, negative, and very large quantities, an empty line list, and duplicate SKUs.
- A token whose `email` claim matches an assigned buyer but whose `sub` does not, which must be refused.

---

## Failure Modes

**1. Binding entities from the request.** The request type is the persistence type, so every property, including status, tenant, and totals, is writable by the caller.

**2. Checking the identity copy instead of ignoring it.** The endpoint compares `PlacedByUserId` with the token, approves of the match, and then trusts every other field.

**3. Authorizing the first identifier only.** The account is checked, and the shipping address, the SKU, or a nested child ID is used as sent.

**4. Validation standing in for authorization.** A well-formed account ID that passes `[Required]` and a GUID format check is treated as an account the caller may use.

**5. Authorization standing in for validation.** A caller allowed to order on the account is assumed to send sensible quantities.

**6. Trusting the route more than the body.** A tenant ID in the path is used for data access because routes feel like part of the server.

**7. Storing the client's price.** A value that a system of record owns is accepted because the client usually sends the right one.

**8. Deciding access on mutable claims.** `email` or a user-editable profile attribute is used to match a user to a record.

**9. Turning headers into identity.** Claims transformation or middleware copies a header into the principal without a network guarantee about who could set it.

**10. Doubting everything.** Every field gets its own database read, the endpoint becomes slow and hard to read, and the few checks that matter are lost among the ones that do not.

---

## A Short Review Checklist

**The request type**

1. Is the request bound to a DTO that contains only selectors, caller-owned input, and preconditions, never to an entity?
2. Is there no identity, tenant, status, price, total, or limit field that the server reads as a fact?
3. Is the handling of unknown JSON members a deliberate choice, with safety that does not depend on it?

**Identity and claims**

4. Do the user and tenant come only from the validated principal?
5. Are authorization decisions keyed on stable identifiers such as `sub`, or `oid` with `tid`, never on `email` or other editable claims?
6. Does every claim added by claims transformation, a gateway, or middleware come from a source that the caller cannot set?

**Selectors**

7. Is every identifier in the request, including nested and child identifiers, resolved to a record that this caller may use?
8. Is the tenant scope taken from the principal, with no "no tenant means all tenants" path?
9. Do route, query, and header values get the same treatment as body values?
10. Is the response for "not found" and "not yours" a deliberate, consistent choice?

**Facts and preconditions**

11. Are server-owned facts computed or looked up on the server, every time?
12. Where the client sends its copy of a server-owned value, is it only compared, with a conflict response when it differs?

**Proportion and tests**

13. Does each resource cost one scoped read, rather than one read per field?
14. Do integration tests cover a foreign account, a foreign child ID, a smuggled server-owned field, a stale precondition, and the allowed case?

---

## Continue Deeper

For the general model behind this article, including what crosses a trust boundary, a source-of-authority table for security-sensitive values, and why authentication is not authority, read [Trust Boundaries and Least Privilege](../../security/trust-boundaries-and-least-privilege.md#caller-supplied-context-is-not-automatically-authoritative).

When the resolved facts feed a policy decision, [Policy Context and Explicit Decision Outcomes](../../tutorials/policy-context-and-explicit-decision-outcomes.md) separates the facts the host gathers from the rules that interpret them, and shows how to keep caller-supplied conclusions out of the context.

If you are deciding whether roles, claims, or something narrower should carry permission, [Role-Based, Claims-Based, and Capability-Based Authorization](../../architecture/role-based-claims-based-and-capability-based-authorization.md#claims-are-attributes-not-automatically-current-world-state) explains why a claim is an attribute rather than current world state.

When the input comes from a language model rather than a person, [Typed AI Proposed Intent and Schema-Validation Boundaries](../../ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md) applies the same sort to model output, stage by stage.

### Related Work

- [OWASP API Security Top 10 (2023)](https://owasp.org/API-Security/editions/2023/en/0xa1-broken-object-level-authorization/): API1, Broken Object Level Authorization, and [API3, Broken Object Property Level Authorization](https://owasp.org/API-Security/editions/2023/en/0xa3-broken-object-property-level-authorization/), which covers mass assignment and excessive data exposure.
- [OWASP Mass Assignment Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Mass_Assignment_Cheat_Sheet.html) lists framework-specific defenses, including allowlisted DTOs.
- [CWE-602](https://cwe.mitre.org/data/definitions/602.html), [CWE-639](https://cwe.mitre.org/data/definitions/639.html), and [CWE-915](https://cwe.mitre.org/data/definitions/915.html) describe client-side enforcement, user-controlled keys, and mass assignment.
- [Resource-based authorization in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased) shows authorizing against a loaded resource with `IAuthorizationService`.
- [Secure applications and APIs by validating claims](https://learn.microsoft.com/en-us/entra/identity-platform/claims-validation), in the Microsoft Entra documentation, explains which token claims are suitable for authorization.

---

## The Short Answer

A validated token answers one question well: who sent this request, according to an issuer you trust. Rely on it for that. Do not let it vouch for anything else in the request.

Sort each value by who owns it. Identity comes from the principal. Selectors are resolved and checked against what this caller may use, every one of them. Caller-owned input is validated and then believed. Server-owned facts are computed on the server, and the client's copy of one is a precondition to compare. Hints stay hints unless infrastructure you configured vouches for them.

For most endpoints, that means a narrow DTO, a validator, one scoped query per resource, and no field the caller can use to set what the server owns. The rest is the work you were already doing, in the right place.
