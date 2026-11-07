---
description: A validated bearer token tells an ASP.NET Core API which subject a trusted issuer asserted. It does not make the request's IDs, tenant, prices, or status true.
title: Passing Authentication Does Not Make Input Trustworthy
author: Christopher D. Cavell
published: "2026-11-07"
summary: A validated bearer token tells an API which subject a trusted issuer asserted. It does not make the request's identifiers, tenant values, prices, or status true. Sort every field by who owns it. Take identity from the validated principal, resolve selectors and check that this caller may use them, validate the input the caller owns, compute the facts the server owns, and treat the client's copy of a server-owned value as a precondition. One scoped read per resource is a useful default.
feed: true
x_hashtags:
  - DotNet
  - CyberSecurity
---

# Passing Authentication Does Not Make Input Trustworthy

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** No formal prerequisites. Familiarity with ASP.NET Core minimal APIs or controllers, JWT bearer authentication, and EF Core is helpful. No AsiBackbone package or prior Learning material is required.

**Sample scope:** The C# targets .NET 8 or later. The entities, price catalog, validator, and test helpers are illustrative, not a runnable sample or a recommended domain model. The tests rely on helpers that are described but not shown. The scenario and its incidents are hypothetical. The code snippets in this article are also offered under the MIT License, as described in the repository's [licensing terms](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md). Framework behavior is described from Microsoft's documentation as of November 2026; check the current documentation for your versions.

**What this article covers:** what a validated token does and does not establish; five ways an authenticated request can still be wrong; sorting request values into identity, selectors, caller-owned input, server-owned facts, and hints; which component owns each concern in ASP.NET Core; an order endpoint rebuilt so each field is handled by its owner; why validation and authorization answer different questions; using the client's copy of a price as a precondition; which token claims can decide access; how to avoid re-reading every field; the same mistake with gateways, internal services, webhooks, and AI tool calls; and when ordinary validation is enough.

**In plain terms:** a courier shows a valid company badge at the loading dock and hands over a delivery note. The badge passes the dock's identity check for a courier from a company it recognizes. It does not prove the note's prices are current, that the goods are addressed to the right warehouse, or that the "pre-approved" stamp on it came from anyone who may approve. The dock checks the badge once, and then checks each line of the note against whoever actually owns that fact.

Consider a hypothetical wholesale ordering platform. Each tenant is a distributor. Each distributor has customer accounts, and buyers at those customers sign in and place orders. Every API call requires a valid access token, and the order endpoint requires an `orders.place` policy, registered at startup, that requires an authenticated buyer. The external subject and identity-provider tenant come from the validated principal. The application resolves its internal buyer and distributor identifiers on the server, never from request values or from the policy name. A quarterly review of disputed orders finds five problems. Every request involved was authenticated.

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

> **Authentication tells you which subject a trusted issuer vouched for. It does not make anything else the request says true. Take identity from the authenticated principal. Resolve every identifier the caller sends and confirm that this caller may use it. Validate the input the caller owns. Compute the facts the server owns, and treat the caller's copy of them as something to compare, never something to store.**

That does not mean every field needs its own database lookup, or that callers should be treated as attackers. Near the end, this article covers how little extra work most endpoints need.

In this article, the **caller** is the principal that the accepted credential represents: here, a buyer. That is not necessarily the person or software that physically sent the request. An app can act for a user, a workload can hold an app-only token, and a stolen token can be presented by someone else.

---

## What Authentication Actually Established

When a properly configured JWT bearer handler accepts an access token, it has verified the token's signature against trusted signing keys, and checked that the issuer is one it expects, that the audience is this API, and that the token is currently valid under its lifetime claims and the configured clock skew. In `TokenValidationParameters`, issuer, audience, and lifetime validation are on by default, and signed tokens are required. Each can still be relaxed, replaced by a custom validator, or misconfigured. Review the effective settings and test them against the tokens your API accepts. Set them explicitly where that makes the intended contract clearer. The claims inside the token are what that issuer asserted when it issued it. That is a real and useful guarantee:

- **The token identifies the subject the issuer says it represents.** In generic OpenID Connect, the stable key is the issuer (`iss`) with the subject (`sub`). In Microsoft Entra ID, it is the tenant ID (`tid`) with the object ID (`oid`). These are the issuer's statements about whom the token represents, as your claim mapping exposes them. By default, `JwtBearerOptions.MapInboundClaims` is `true`, so the JWT bearer handler renames some claims. For example, `sub` usually arrives as `ClaimTypes.NameIdentifier`, so code that reads the raw `"sub"` name finds nothing. Either set `MapInboundClaims = false` and read the issuer's claim names, or read the mapped claim types consistently, and test whichever you choose with a real token. Your code needs no database read to rely on what the issuer asserted. Mapping that external identity to your own buyer or tenant records may still need a lookup.
- **Issuer-assigned claims are the issuer's statements.** Application roles issued from an administratively controlled assignment can support coarse authorization, according to the issuer's documented semantics, as of the token's issue time.

It does not establish anything else:

- **That the presenter is the rightful holder.** A bearer token can be used by anyone who holds it. Validation proves that the token is genuine, not that it was not copied. Sender-constrained tokens, through DPoP or mutual TLS, require the presenter to prove that it holds a key bound to the token. A DPoP proof is also tied to the HTTP method and target URI.
- **The body, route, query string, and headers.** The token is attached to the request, and the request is not signed by the token. A valid token says nothing about the JSON next to it. Even a sender-constrained token does not sign the body.
- **That the client is your client.** A public client, whether a browser app, a mobile app, or a script, runs on a device you do not control. The request may come from a modified build, a stale build, or `curl`. Checks done only in the client can be skipped. [CWE-602](https://cwe.mitre.org/data/definitions/602.html) names this weakness: client-side enforcement of server-side security.
- **Current state.** Claims are a snapshot from issue time, and ordinary local JWT validation does not look up revocation or current authorization state on each request. A buyer removed from an account an hour ago may still carry a token issued before the removal. Short token lifetimes narrow that window. A check of the current assignment can refuse a buyer whose access has already been revoked.
- **Permission for this operation on this resource.** `orders.place` says the caller may place orders somewhere. It says nothing about account `C-2041`.

Two misconceptions sit on either side of this. One treats a valid token as proof that the request is honest. The other treats every claim in a validated token as attacker-controlled. Neither is right. The token is reliable for which subject it represents and what the issuer asserted. The rest of the request needs to be judged field by field.

---

## Five Ways an Authenticated Request Can Be Wrong

The five incidents are different failures, and each has a different owner:

| Kind of wrong | In the example | Possible origin | What catches it |
| --- | --- | --- | --- |
| **Malformed** | Quantity `-40` | A client bug, or anyone | Shape and range validation at the API boundary |
| **Stale** | Last month's prices | An old client build | Computing the price on the server, and comparing the client's copy as a precondition |
| **Unauthorized** | The old customer's account ID | A buyer acting in good faith | Resolving the account and checking that this buyer may use it |
| **Cross-tenant** | `TenantId` from the wrong configuration file | A misconfigured integration | Resolving the distributor tenant on the server from the validated external identity |
| **Adversarial** | `"status": "Approved"` | A buyer trying to skip review | Never binding server-owned fields from the request |

Only one of the five was an attack. All five were authenticated. The fix for each is the same habit: know who owns each value, and let only that owner set it.

The second and fifth problems are common enough to have names. [Broken Object Level Authorization](https://owasp.org/API-Security/editions/2023/en/0xa1-broken-object-level-authorization/) is first in the OWASP API Security Top 10 (2023), and [CWE-639](https://cwe.mitre.org/data/definitions/639.html) describes authorization bypass through a user-controlled key. Writing fields the caller should not control is mass assignment ([CWE-915](https://cwe.mitre.org/data/definitions/915.html)), which OWASP now groups under [Broken Object Property Level Authorization](https://owasp.org/API-Security/editions/2023/en/0xa3-broken-object-property-level-authorization/).

---

## Sort Every Value by Who Owns It

Before writing any check, sort each value the request carries into one of five kinds. The kind decides what happens to it.

| Kind | Who is authoritative | What the server does | Examples in the order request |
| --- | --- | --- | --- |
| **Identity** | The authentication boundary for external identity; trusted application mapping for internal identity | Read external identity from the validated principal, then resolve internal identity through trusted server-side mappings. Ignore or reject any copy in the request. | External subject key, identity-provider tenant, internal buyer ID, server-resolved distributor tenant |
| **Selectors** | The caller chooses *which*; the server decides *whether it may* | Resolve to an authoritative record, scoped to what this caller may use | Customer account ID, shipping address ID, SKU |
| **Caller-owned input** | The caller, for its own intent | Validate shape and range. Apply business rules against authoritative state where they exist. Then accept it as the caller's stated intent. | Quantity, purchase-order number, delivery notes, requested date |
| **Server-owned facts** | A system of record | Look up or compute. Never bind from the request. A client copy is at most a precondition. | Unit price, total, discount eligibility, credit limit, order status |
| **Hints** | Not authoritative by default; infrastructure you configured may establish a trusted form | Use for non-security purposes, or only after a trusted component establishes them | Locale, client time zone, `X-Forwarded-For`, app version |

Applied to the original `Order` entity:

| Field | Kind | Treatment |
| --- | --- | --- |
| `PlacedByUserId` | Identity | The internal buyer ID, resolved on the server from the validated principal's stable external key: `tid` with `oid` in Microsoft Entra ID, or `iss` with `sub` in generic OpenID Connect. The original check compared the body with the token and then ignored everything else. |
| `TenantId` | Identity | Resolved on the server from the validated principal, through a mapping table from the identity tenant to an internal distributor ID, or a lookup keyed by the stable user key. The handler below uses the tenant mapping. The two IDs are different values. This sample assumes one Entra tenant per distributor; real deployments may map several either way. A different value in the body is a mistake or an attempt, never an instruction. |
| `CustomerAccountId` | Selector | Must exist in this tenant, and this buyer must be assigned to it with permission to order. |
| `ShippingAddressId` | Selector | Must belong to *that* account. Checking only the first identifier in a request is a common gap. |
| `Lines[].Sku` | Selector | Must exist in the catalog that applies to this account. |
| `Lines[].Quantity` | Caller-owned input | A positive integer within a sensible range. Minimum order quantities are a business rule. |
| `Lines[].UnitPrice` | Server-owned fact | From the account's price list today. |
| `Total` | Server-owned fact | Computed from server prices. |
| `Status` | Server-owned fact | Set by the server, based on account state and credit. |
| `PurchaseOrderNumber` | Caller-owned input | Validate length and characters, and keep it exactly as supplied. Store the raw text, and encode it for each output: HTML-encode it in a page, and let the serializer handle it in JSON. |

Two things about this sort are easy to miss.

**Caller-owned input is genuinely authoritative.** When the buyer says "twelve pallets," that is the caller's stated intent, and no other source is needed to establish what quantity the buyer requested. The server validates that twelve is a legal quantity. It does not need to doubt that the buyer meant twelve. Treating every value with suspicion buries the few that need it.

**Where a value arrives does not change its kind.** A tenant ID in the route (`/tenants/{tenantId}/orders`), a header (`X-Tenant-Id`), or the query string is still a value the caller chose. A route parameter is not more trustworthy than a body field. If the route carries the application tenant for routing, compare it with the distributor tenant that the server resolved from the validated principal, and refuse a mismatch.

---

## Each Concern Has an Owner

In ASP.NET Core, the five kinds of wrong map to different components. Mixing them up is how an endpoint ends up with checks that look thorough and miss the case that matters.

| Concern | Component that owns it | ASP.NET Core mechanism | It is not |
| --- | --- | --- | --- |
| Is this a well-formed request? | Binding and validation | DTOs, DataAnnotations or a validator, `[ApiController]` automatic `400` for controllers, `Results.ValidationProblem` | Permission |
| Who is calling? | Authentication | JWT bearer or cookie handler, `ClaimsPrincipal` | Permission for this resource |
| May this caller use this resource? | Resource-based authorization | A scoped query, or `IAuthorizationService.AuthorizeAsync(user, resource, requirement)` | Validation that the ID is well formed |
| What are the authoritative facts? | Systems of record | Repositories, the price catalog, the credit service | Anything the client sends |
| Does the operation satisfy business rules? | Application service or domain model | Credit check, minimum order, account on hold | Authorization of the caller |
| Is the client acting on what it last saw? | Preconditions | An expected total, a version, `If-Match` | A source of the current value |

The first row and the third row are where most confusion lives. `[Range(1, 10_000)]` on `Quantity` says the number is sensible. It does not say this buyer may order on this account. And an authorization handler that confirms the buyer is assigned to the account does not say that `-40` is a valid quantity. Each answers its own question. An endpoint needs both, and neither substitutes for the other.

Business rules are a third, separate question. A buyer may be fully authorized to place orders on an account that is on credit hold. The order is then refused, or held for review, because of the account's state, not because of who the buyer is. Many resource-based authorization handlers do check resource state, and some teams deliberately model "no orders on a held account" as authorization. Keeping it as a business rule is usually clearer. The caller gets an outcome such as `409` or `422` that explains what to fix, rather than a `403` that suggests they lack permission, and the policy does not change when facts unrelated to the caller change.

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
    IReadOnlyList<PlaceOrderLine?>? Lines,
    // Nullable, so a missing precondition is a 400 rather than a silent zero.
    // Strict by contract: this endpoint accepts a JSON number token, not a numeric string,
    // which the web defaults would otherwise accept. Server-side pricing, not this
    // attribute, is what keeps the client from setting prices.
    [property: JsonNumberHandling(JsonNumberHandling.Strict)] decimal? ExpectedTotal,
    string? PurchaseOrderNumber);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlaceOrderLine(string? Sku, int Quantity);

// What the handler works with after validation: no null or default selectors, lines, or
// total, and normalized SKUs. The purchase-order number stays optional.
public sealed record ValidatedOrder(
    Guid CustomerAccountId,
    Guid ShippingAddressId,
    IReadOnlyList<ValidatedOrderLine> Lines,
    decimal ExpectedTotal,
    string? PurchaseOrderNumber);

public sealed record ValidatedOrderLine(string Sku, int Quantity);
```

A JSON body can omit any property, and it can send `null` for a nullable one. Without the nullable annotation, an omitted `ExpectedTotal` would arrive as `0`, which is indistinguishable from a real zero total. As `decimal?`, an omitted or `null` total arrives as `null`, and the validator rejects it. The `Guid` selectors stay non-nullable: an omitted one binds to `Guid.Empty`, which the validator rejects, while an explicit `null` or a malformed value fails deserialization with `400`. Either way, the handler never has to guess whether a value was sent.

The safety comes from the shape of the type. A request DTO with no `Status` property cannot set a status, whatever the JSON says. Microsoft's web API tutorial describes the same pattern as [preventing over-posting](https://learn.microsoft.com/en-us/aspnet/core/tutorials/first-web-api).

`JsonUnmappedMemberHandling.Disallow` is a separate choice. By default, System.Text.Json [ignores JSON properties that have no matching member](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members), so `"status": "Approved"` would be silently dropped. That is already safe. `Disallow` makes deserialization throw a `JsonException` instead. In the standard minimal API request-binding path that this example assumes, the failed body binding is returned as `400 Bad Request` before the handler runs. If you have custom exception handling, check that it does not turn this into a `500`. The request fails, but nobody notices unless something records it, so log or count these binding failures. With that in place, both the smuggled `status` and the misconfigured integration that still sends `tenantId` show up. The cost is that a client sending a field the server does not yet know about breaks. Many teams disallow unknown members on write endpoints for public clients and allow them elsewhere. Either choice is defensible. What matters is that safety does not depend on it.

### Validation answers "is this a sensible request?"

```csharp
public static class PlaceOrderValidator
{
    // This illustrative catalog matches SKUs case-insensitively after trimming. The
    // boundary normalizes each SKU with one rule, and the catalog only receives that form.
    // Whether a SKU exists is still the catalog's decision.
    private static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();

    public static (ValidatedOrder? Order, Dictionary<string, string[]> Errors) Validate(PlaceOrderRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.CustomerAccountId == Guid.Empty)
        {
            errors["customerAccountId"] = ["A customer account is required."];
        }

        if (request.ShippingAddressId == Guid.Empty)
        {
            errors["shippingAddressId"] = ["A shipping address is required."];
        }

        // Zero is allowed, because a valid order can be fully discounted. A missing or
        // negative total is rejected.
        if (request.ExpectedTotal is null or < 0m)
        {
            errors["expectedTotal"] = ["The total shown to the buyer is required and cannot be negative."];
        }

        if (request.Lines is not { Count: > 0 and <= 200 })
        {
            errors["lines"] = ["An order needs between 1 and 200 lines."];
        }
        else if (request.Lines.Any(line => line is null || string.IsNullOrWhiteSpace(line.Sku)))
        {
            errors["lines"] = ["Each line needs a SKU."];
        }
        else if (request.Lines.Any(line => line!.Quantity is < 1 or > 10_000))
        {
            errors["lines"] = ["Each quantity must be between 1 and 10,000."];
        }
        else if (request.Lines.GroupBy(line => NormalizeSku(line!.Sku!)).Any(group => group.Count() > 1))
        {
            errors["lines"] = ["Each SKU may appear only once."];
        }

        // An example business format, not a security control. The value is kept exactly as
        // supplied: surrounding whitespace is rejected, not trimmed. Omit the property, or
        // send null, when there is no purchase-order number; an empty string is rejected.
        if (request.PurchaseOrderNumber is { } po
            && (po.Length is 0 or > 40 || po.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '/' or '.'))))
        {
            errors["purchaseOrderNumber"] = ["Use 1 to 40 ASCII letters, digits, hyphens, slashes, or periods."];
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new ValidatedOrder(
            request.CustomerAccountId,
            request.ShippingAddressId,
            request.Lines!.Select(line => new ValidatedOrderLine(NormalizeSku(line!.Sku!), line.Quantity)).ToArray(),
            request.ExpectedTotal!.Value,
            request.PurchaseOrderNumber), errors);
    }
}
```

The validator returns either errors or a `ValidatedOrder`. Everything after it works with present selectors, lines, and total, and with normalized SKUs, so the rule that found duplicates is the same rule the catalog sees.

This is a hand-written validator so that the example behaves the same on every supported version. On controllers, DataAnnotations with `[ApiController]` return an automatic `400`. On minimal APIs, built-in DataAnnotations validation arrived in .NET 10 and is opt-in through `builder.Services.AddValidation()`. On .NET 8 and 9, use an explicit validator like this one, an endpoint filter, or a validation library. None of these consult the database or the user. They only decide whether the request makes sense on its own.

### The handler sends each field to its owner

```csharp
app.MapPost("/api/orders", async (
        PlaceOrderRequest request,
        ClaimsPrincipal user,
        OrderingDbContext db,
        IBuyerResolver buyers,
        IPriceCatalog prices,
        CancellationToken ct) =>
    {
        // 1. Shape and range. Nothing below runs for a malformed request, and everything
        //    below uses the validated, normalized order rather than the raw request.
        (ValidatedOrder? input, Dictionary<string, string[]> errors) = PlaceOrderValidator.Validate(request);
        if (input is null)
        {
            return Results.ValidationProblem(errors);
        }

        // 2. Identity comes from the validated principal, never from the request.
        //    The user key is tid with oid in Microsoft Entra ID, or iss with sub in generic
        //    OpenID Connect. Those identify the user, not the distributor. The resolver maps
        //    that external key to the application's internal buyer ID, and looks up the
        //    Entra tid in a server-side mapping table to find the internal distributor ID.
        //    Both are different values from the claims, and neither comes from the request.
        //    It reads claim types that match the host's JWT claim mapping (MapInboundClaims).
        //    It returns null when the internal mapping is missing, or, as a fallback, when
        //    the principal lacks the required claims.
        Buyer? buyer = await buyers.ResolveAsync(user, ct);
        if (buyer is null)
        {
            // The caller is authenticated but has no internal application mapping: 403.
            // Permission to order on a particular account is checked in step 3.
            return Results.Forbid();
        }

        // 3. Selectors become records, scoped to what this buyer may use. Tenant, existence,
        //    assignment, and the address's owner are checked in one query.
        var match = await db.CustomerAccounts
            .Where(a => a.TenantId == buyer.DistributorTenantId
                && a.Id == input.CustomerAccountId
                && a.Buyers.Any(b => b.UserId == buyer.UserId && b.CanPlaceOrders))
            .Select(a => new
            {
                Account = a,
                AddressBelongs = a.ShippingAddresses.Any(x => x.Id == input.ShippingAddressId),
            })
            .SingleOrDefaultAsync(ct);

        // Every identifier in the request, not only the first one. A foreign account and a
        // foreign address get the same answer, so neither response reveals what exists.
        if (match is null || !match.AddressBelongs)
        {
            return Results.NotFound();
        }

        CustomerAccount account = match.Account;

        // 4. Server-owned facts: prices from the catalog that applies to this account today.
        //    PriceAsync owns rounding. It uses the account's currency, never one from the
        //    request, rounds each line to that currency's minor unit with
        //    MidpointRounding.AwayFromZero, and sums the rounded lines. The client
        //    computes its displayed total by the same rule.
        PricedOrder priced = await prices.PriceAsync(account, input.Lines, ct);

        if (priced.UnknownSkus.Count > 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["lines"] = [$"Unknown or unavailable SKUs: {string.Join(", ", priced.UnknownSkus)}."],
            });
        }

        // 5. The client's total is a precondition. It is compared, never stored. This buyer may
        //    see this account's prices, so the current total can be returned.
        if (priced.Total != input.ExpectedTotal)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The order total differs from the expected total.",
                extensions: new Dictionary<string, object?> { ["currentTotal"] = priced.Total });
        }

        // 6. Business rules and status belong to the domain, not to the caller.
        //    Accepted gaps in this example: no idempotency key, and the checks above are
        //    not re-enforced at commit. See "Checked at lookup, not at commit."
        //    buyer.UserId is the internal ID from the resolver, not a raw oid or sub.
        Order order = account.PlaceOrder(
            placedBy: buyer.UserId,
            shippingAddressId: input.ShippingAddressId,
            priced,
            input.PurchaseOrderNumber);

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/orders/{order.Id}", OrderResponse.From(order));
    })
    .RequireAuthorization("orders.place");
    // At startup, register the orders.place policy with AddAuthorization, and register
    // IBuyerResolver and IPriceCatalog with dependency injection.
```

Where authentication is configured to reject a token that lacks the required identity claims, for example in the JWT bearer handler's token validation, that request fails with `401` and never reaches the handler. The resolver's `403` is for an authenticated external identity with no internal buyer or distributor mapping. It is also the fallback if a principal without the required claims reaches the handler. Whether the buyer may order on a particular account is a separate question, answered in step 3, and a buyer without that permission gets `404`, not `403`.

Walking the five incidents through it:

- **Quantity `-40`** fails validation in step 1, before any query.
- **The old customer's account ID** resolves to nothing in step 3, because the buyer is no longer assigned to it. The buyer gets `404`, and no order is created.
- **The wrong tenant** cannot override the distributor tenant that the server resolved from the validated principal. The DTO has no `TenantId`, and the account query is scoped to `buyer.DistributorTenantId`, which came from the server-side mapping, not the request. With `Disallow`, the partner's request fails at binding with `400` on its first call, instead of succeeding quietly in the wrong tenant.
- **Last month's prices** are never read. The server prices the order, and if the mobile app's total differs, the buyer gets `409` with the current total to confirm.
- **`"status": "Approved"`** has nowhere to go. `account.PlaceOrder` decides the initial status from the account's credit position.

Step 3 combines the lookup and the authorization into one query. That is a common and efficient form of resource-based authorization. The sample projects the whole account because the domain operation uses it next. A production query should load only the state that operation needs. When the rule is more involved, or shared with other endpoints, load the account first and call [`IAuthorizationService.AuthorizeAsync(user, account, requirement)`](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased) with an authorization handler. Either way, the buyer's permission is checked against the account the server loaded, not against anything the request claims about it. A tenant-scoped EF Core global query filter can supply the `TenantId` predicate by default, provided it reads the internal distributor tenant from a trusted server-side tenant context established from the validated principal, such as the resolver's result, and has no "no tenant means all tenants" escape. A filter applies to queries for the entity types it is configured on. `IgnoreQueryFilters` bypasses it, and it does not check writes. [A Background Job Should Not Inherit Everything the User Could Do](background-job-should-not-inherit-user-authority.md#the-job-inherits-the-worker) shows how that escape goes wrong.

Returning `404` for an account or address the buyer may not use is a choice. It avoids confirming that an ID exists. A `403` is also reasonable when existence is not sensitive. For each selector whose existence is sensitive, return the same response for "missing" and "not yours," so the difference does not leak what the choice was meant to hide. Here, addresses identify customers, so the address gets the same `404` as the account. The cost is that a buyer who picks a just-deleted address gets a plain `404`, and the client has to refresh its list. SKUs are different: the catalog is not secret from a buyer who may order on the account, so an unknown SKU is reported as a validation error.

### Checked at lookup, not at commit

Each check in the handler describes what it saw when it ran. If the buyer's assignment is revoked, or a price changes, after step 3 or step 4 but before `SaveChangesAsync`, the order is still created on the strength of facts that are no longer true. Wrapping the handler in a transaction does not change that by itself. Under read-committed isolation, rows read earlier can still change before the commit.

Whether that gap matters is a business decision. For many ordering systems, a few milliseconds is accepted. When the facts must still hold at commit, use the mechanism that fits where the fact lives:

- **A fact in the same database**: make the order's transaction contend with the change that would invalidate it. EF Core's optimistic concurrency check applies when it updates or deletes the entity that carries the token, so inserting the order does not, by itself, compare the account's token. One general protocol:
  1. Open an explicit transaction, and keep that same transaction open through the locking reads and `SaveChangesAsync`, then call `CommitAsync`.
  2. As the last step before inserting the order, after pricing, re-read the versioned state the decision depended on, such as the assignment and the price list. Use a database-specific locking read that stops an invalidating writer from committing until this transaction ends. EF Core LINQ has no locking hint, so this is usually raw SQL. In PostgreSQL, `SELECT ... FOR SHARE` is one fit. In SQL Server, use and test a locking query suited to your schema, indexes, isolation settings, and row-versioning configuration. A query using `HOLDLOCK`, which keeps shared locks until the transaction ends, may be a starting point, but lock granularity and escalation remain the engine's decisions. In either engine, verify under concurrency that an invalidating update cannot commit while the order holds the relevant locks, and that concurrent orders can acquire compatible verification locks.
  3. Confirm that each version still matches the one the decision used, and roll back if it does not. The order only verifies versions, so its locking reads are compatible with other orders' locking reads.
  4. Every writer that revokes an assignment or changes a price updates that version. If prices are stored as history rather than updated in place, the version lives on the price-list header, not on an individual historical price row.

  When every invalidating writer follows this protocol, and the locks you chose have the conflict behavior you verified, an order cannot commit against a version that an already committed revocation or repricing has replaced. A revocation that starts while the order holds its lock waits, then succeeds after the order commits. Prove this with concurrency tests on your actual engine. Raising the isolation level is not a substitute. It does not state which business change should win, and it adds serialization or deadlock errors that need a retry.
- **A fact owned by another service**, such as an external price catalog with no row to lock: a server-issued quote or reservation with its own expiry, which the order consumes. A quote binds price and terms, not permission. When consuming it, verify that it belongs to this buyer and account and has not expired, and re-check the buyer's assignment at that moment, rather than relying on the authorization that existed when the quote was issued.

Retries create a separate risk: a client that times out and resends can place the same order twice. The example accepts that gap. [Your Security Check Passed. Then the World Changed.](security-check-passed-then-world-changed.md) covers the check-to-use gap in depth, and [Idempotency Does Not Prevent Authority Replay](idempotency-does-not-prevent-authority-replay.md) explains how to scope an idempotency key and what it does and does not prove.

---

## When the Client Sends a Value You Own

The mobile app showed the buyer a total before they pressed **Place order**. That total is useful information. It is the buyer's record of what they agreed to. It is not a price.

So the rebuilt request keeps it, renamed `ExpectedTotal`, and the server uses it only for comparison. If the server's total differs, the order is not placed, and the client shows the new total for the buyer to confirm. The result is a better outcome than either alternative:

- Accepting the client's price lets a stale or modified client set prices.
- Silently repricing charges the buyer an amount they never saw.

Three limits apply. The comparison is exact, so the server and client must compute the total the same way: the account's currency, each line rounded to that currency's minor unit with the same midpoint rule, then summed. A JSON number is culture-independent whether or not `Strict` is set, so locale affects only how the client displays the total. `Strict` is this endpoint's contract choice to accept only a number token, so a quoted value such as `"125.00"` is rejected. It checks only the grand total: two line prices can change and offset each other, and the amount sent does not prove what the buyer actually read. If the order must bind to specific lines, prices, currency, or terms, have the server issue a quote with an ID and version, and let the order reference it. Finally, returning `currentTotal` is itself an output decision. Here the buyer may already see this account's prices. Where a caller may not see the authoritative value, return the conflict without it.

This follows the same idea as an `If-Match` header: the client tells the server what it believes is current, and the server refuses to act if that belief is wrong. `If-Match` compares an entity tag for the target resource and normally answers a mismatch with `412 Precondition Failed`. `If-Match` also needs an existing resource. For a create such as this one, a body precondition or a server-issued quote is the closer fit, so this example uses `409 Conflict`. Either way, the client's value is a precondition, not an input. [Your Security Check Passed. Then the World Changed.](security-check-passed-then-world-changed.md) applies the same idea to approvals that must bind to exactly what a person reviewed.

The same treatment fits other server-owned values that a client legitimately displays: a quote version, an exchange rate, a shipping charge, or the status an editor saw before saving.

---

## Which Claims Can Decide Access

Once the body is handled, the remaining question is how far to trust the principal itself. Not every claim in a validated token is equally suitable for authorization. Successful signature and issuer validation establish that the token, including its claims, came from the expected issuer and was not altered after signing. They do not establish how the issuer obtained each claim's value.

| Claim | Where its value comes from | Suitable for authorization? |
| --- | --- | --- |
| Generic OpenID Connect: `iss` with `sub`. Microsoft Entra ID: `tid` with `oid`. | The issuer's stable identifier for the subject. A `sub` is unique only within its issuer, and in Entra ID it is also specific to the application. | Yes. This pair is the identity to key on, within the scope the issuer defines. |
| Application roles or groups from an administratively controlled assignment | Administrative assignment, as of issue time | Yes, for coarse permission, subject to token freshness, the issuer's claim semantics, and any group-overage handling the platform requires |
| Delegated scopes, such as `scp` in Microsoft Entra ID | Consent for what the client may do on the user's behalf | As a limit on the client, yes. They let the client request operations on the user's behalf, but effective access is still limited by the user's own authorization and the resource's rules. |
| `email`, `preferred_username`, `upn`, `unique_name` | Often editable by tenant administrators, and sometimes by users | No. Microsoft's guidance on [validating claims](https://learn.microsoft.com/en-us/entra/identity-platform/claims-validation) explicitly warns against using these to decide access. |
| Custom claims mapped from profile attributes | Whoever can edit that attribute, possibly the user | Only if you know who can write the attribute |
| Claims added by your `IClaimsTransformation` | Your code and the sources it reads | As trustworthy as those sources. A transformation that copies a request header into a claim has made the header look like identity. |
| A user ID or role in a header set by an API gateway | The gateway, if nothing else can reach the service | Only when every request must pass through the gateway, enforced by network isolation or by mutual TLS between gateway and service, and the gateway strips any incoming copy. Stripping alone does not help a caller who can reach the service directly. |

Stable identity claims from a validated token do not need a database read to establish what the issuer asserted. Your application may still need a lookup to map that external identity to an internal buyer, distributor, or current assignment. Permission claims are a snapshot. If a removed assignment must take effect before the token expires, check the assignment at the point of use, as step 3 does, rather than trusting the token's copy.

The same applies to network facts. `X-Forwarded-For` and `X-Forwarded-Proto` are request headers that any client can send. ASP.NET Core's Forwarded Headers Middleware is meant to apply them only from [known proxies or networks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer) that you configure, and that guarantee has conditions:

- **Patch level.** Before ASP.NET Core 8.0.17 and 9.0.6, when `X-Forwarded-For` processing was not enabled, the middleware accepted `X-Forwarded-Proto`, `X-Forwarded-Host`, and `X-Forwarded-Prefix` from any source. Microsoft's [security-hardening change](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/8/forwarded-headers-unknown-proxies) closed that gap, so stay on a patched release.
- **Configuration.** Clearing `KnownProxies` and `KnownNetworks` (named `KnownIPNetworks` in newer releases) trusts every source again. The `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` setting enables forwarding without restricting which proxies are accepted. Keep an allowlist of the proxies or networks that actually sit in front of the app.

A client IP address used for rate limiting or risk scoring is only as trustworthy as that patch level and allowlist.

---

## Do Not Re-Read Everything

The cure for trusting everything is not to doubt everything. Most fields need no lookup at all, and the ones that do can usually be checked together.

- **Caller-owned input needs boundary validation first, not lookups.** A quantity, a note, or a requested date is checked for shape and range without reading anything. Business rules may then evaluate it against authoritative state, such as stock, shipping cutoffs, or a customer-specific minimum, where the operation needs them.
- **Identity needs no read to be believed.** The validated principal is the source. Translating it to internal buyer and distributor IDs is one mapping lookup, which can come from configuration, a cache, or a table.
- **One scoped read per resource is a useful default.** Load the account with the tenant and assignment in the predicate. Existence, tenant, and permission come back in one query, and the result is the record you were going to use anyway. Authorization handlers, external policy services, and complex aggregates sometimes need more calls. The goal is to avoid redundant reads, not to hit a number.
- **Server-owned facts are read once, where they are used.** The price catalog is called once for the whole order. It would be called whether or not the client sent prices.
- **Child identifiers often fold into the parent's query.** The rebuilt handler checks the shipping address in the same query as the account, through the account's navigation property.

In the rebuilt endpoint, every query would exist even if the client were perfectly trustworthy. The secure version is mostly the same work done in the right order, with the client's values in different places.

---

## The Same Mistake With Other Senders

The habit applies wherever an authenticated party sends data, not only to users.

**Internal services.** A service authenticated with a workload identity or mutual TLS sends `{"onBehalfOfUserId": "u-17", "tenantId": "t-4"}`. A workload token identifies the workload its issuer says it represents. With mutual TLS, the identity comes instead from the client certificate, validated against a certificate authority you trust, and you also gain stronger assurance about who presented it. The user and tenant inside the payload are that service's assertion. Whether to accept them is a decision about how much you trust that service, and it should be written down rather than implied by "it's internal." Where the downstream service needs the user's own authority, delegation protocols such as On-Behalf-Of carry it in a token the issuer signs, rather than in a field the caller writes.

**Webhooks.** A correctly verified signature shows that the payload was signed with the provider's configured key or secret and was not altered afterward. It does not show that the event is fresh or unique, that the payload is the latest state, that it has not been delivered before, or that it refers to an object in your tenant. If the provider signs a timestamp, reject deliveries outside a short tolerance. A replay inside that window is still possible, so de-duplicate deliveries by event identifier as well. Treat event identifiers as selectors, and look up current state from the provider when the decision matters.

**AI tool calls.** When a model produces arguments for a tool, the session may be authenticated as a real user, and the arguments are still proposals. They are selectors and caller-owned input at best, and they never establish identity, tenant, or permission. [Typed AI Proposed Intent and Schema-Validation Boundaries](../../ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md) applies this field-by-field sort to model output.

**Your own front end.** A single-page application written by your own team is still a public client. Hiding the **Approve** button for buyers is good design. It is not enforcement.

---

## When Ordinary Validation Is Enough

Many endpoints need only part of this.

An endpoint where every field is caller-owned input, scoped entirely to the caller's own data, needs validation and a query filtered by the caller's ID. A user updating their own display name and notification preferences is an example. There are no selectors to authorize and no server-owned facts to protect. The display name still needs encoding wherever it is rendered, because validation does not make stored text safe to output.

A read-only endpoint that returns records filtered by the principal's tenant and user needs the scoped query, and attention to which properties it returns. Returning fields the caller should not see is the read side of the same OWASP category as mass assignment.

The full treatment is for endpoints that do at least one of the following:

- Accept an identifier for a resource the caller might not be allowed to use.
- Carry, or are tempted to carry, a value that a system of record owns, such as a price, a balance, a status, or a limit.
- Accept identity or tenant values from anywhere other than the authentication boundary.
- Serve several tenants or several kinds of caller from the same code.

---

## Testing the Boundary

Unit tests of the validator are useful, but the failures in this article live in the gaps between components. Integration tests with `WebApplicationFactory` and a test authentication handler exercise the application from binding to persistence, using a principal the test supplies. They do not test your real JWT validation or claim mapping. Cover those separately, with tests that send tokens through the configured bearer handler. Each test should state what was sent, as whom, and what must not have happened.

The tests below are illustrative. `OrderingApiFactory`, `CreateClientAs`, the `Test*` fixtures, `Prices`, and `CountOrdersAsync` stand for helpers you would write: a factory that swaps in a test authentication handler and a seeded database, and a fake price catalog.

```csharp
public sealed class PlaceOrderBoundaryTests(OrderingApiFactory factory) : IClassFixture<OrderingApiFactory>
{
    [Fact]
    public async Task Buyer_cannot_order_on_an_unassigned_account_in_their_own_tenant()
    {
        // Contoso is in the same tenant as Alice, so only the assignment check can refuse this.
        HttpClient client = factory.CreateClientAs(TestBuyers.AliceAtNorthwind);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/orders", TestOrders.Valid(account: TestAccounts.ContosoInAlicesTenant));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await factory.CountOrdersAsync(TestAccounts.ContosoInAlicesTenant));
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
- An account ID from another tenant, tested separately from the same-tenant assignment case.
- A route or header application-tenant value that differs from the distributor tenant the server resolved.
- A shipping address that exists but belongs to another account in the same tenant, which must get the same `404` as a foreign account.
- A buyer removed from an account after their token was issued, using that token.
- The same request from a buyer who *is* assigned, which must succeed. A boundary that refuses everyone passes every negative test.
- Zero, negative, and very large quantities, an empty line list, a `null` line, and a blank SKU.
- SKUs that differ only in case or surrounding whitespace, which are duplicates, and a single SKU with surrounding whitespace, which must be priced as its normalized form.
- A missing, negative, or quoted `expectedTotal`, an empty account ID, or an empty purchase-order number, each a `400`, not a `409` or a `404`.
- A token whose tenant has no distributor mapping, refused with `403` before any account is queried.
- A token missing the required identity claims: `401` if your authentication configuration rejects it, otherwise the resolver's `403` fallback. Test whichever your configuration produces.
- If you add idempotency: the same request sent twice with one key must create one order, and the same key with different order details must be refused.
- A token whose `email` claim matches an assigned buyer but whose subject does not, which must be refused.

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
2. Is there no identity, tenant, status, price, total, or limit field that the server reads as a fact, except as a compared precondition?
3. Are missing values, `null` elements, and default `Guid` or numeric values rejected rather than silently used?
4. Is the handling of unknown JSON members a deliberate choice, with safety that does not depend on it, and are rejections logged or counted?

**Identity and claims**

5. Does the external identity come only from the validated principal, and are internal user and tenant identifiers resolved from trusted server-side mappings?
6. Are authorization decisions keyed on stable identifiers, such as `iss` with `sub`, or `tid` with `oid` in Microsoft Entra ID, never on `email` or other editable claims?
7. Does every claim added by claims transformation, a gateway, or middleware come from a source that the caller cannot set?

**Selectors**

8. Is every identifier in the request, including nested and child identifiers, resolved to a record that this caller may use?
9. Is the application tenant scope resolved on the server from the validated principal, with no request-controlled or "no tenant means all tenants" path?
10. Do route, query, and header values get the same treatment as body values?
11. For each selector whose existence is sensitive, do "not found" and "not yours" get the same response?

**Facts and preconditions**

12. Are server-owned facts computed or looked up on the server, every time?
13. Where the client sends its copy of a server-owned value, is it only compared, under a stated rounding rule, with a conflict response when it differs, or replaced by a server-issued quote where exact terms must bind?
14. Is the gap between the checks and the commit either accepted in writing or closed by re-checking, under a lock held to commit, the versions the decision used, with every invalidating writer updating them? Where a quote binds the price and terms, is the buyer's current authorization still enforced separately? Is duplicate submission on retry either accepted or handled with a scoped idempotency key?

**Proportion and tests**

15. Is each resource read once with its scope in the predicate, rather than once per field?
16. Do integration tests cover a foreign account in the same tenant and in another tenant, a foreign child ID, a smuggled server-owned field, a missing precondition, a stale precondition, and the allowed case?

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

A validated token tells you which subject a trusted issuer vouched for. Rely on it for that, and for nothing else in the request. Identity is established from the validated principal and mapped to application identity on the server. Every selector is resolved and checked against what this caller may use. Caller-owned input is validated and then accepted as the caller's stated intent. Server-owned facts are computed on the server, and the client's copy is only compared. For most endpoints, that is a narrow DTO, a validator, and one scoped query per resource: the work you were already doing, in the right place.
