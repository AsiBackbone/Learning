---
description: An ASP.NET Core service lifetime decides who shares an instance, who disposes it, and whether it must be thread-safe. Choose it from ownership.
title: Your Dependency Injection Lifetime Is an Ownership Decision
author: Christopher D. Cavell
published: "2026-11-19"
summary: AddTransient, AddScoped, and AddSingleton look like registration syntax, but each one decides who shares an instance, how long the container keeps it and what stays reachable through it, who disposes it, and how widely concurrent requests can reach it. A singleton that captures a scoped DbContext can serve stale data and fail under concurrent load, and scope validation catches it only where validation is enabled and only in the registrations it can analyze. Choose a lifetime from the state a service owns and who should share that state, never let a singleton hold a scoped service, give a hosted service a scope for each unit of work, let the container dispose what it creates, and test the instance identities your design depends on. A lifetime does not provide durability, authorization, caching semantics, or coordination between application instances.
feed: true
x_post: A DI lifetime decides who shares an instance, who disposes it, and whether it must be thread-safe. Choose it on purpose.
x_hashtags:
  - DotNet
  - AspNetCore
x_question: "Which lifetime bug was hardest to find?"
---

# Your Dependency Injection Lifetime Is an Ownership Decision

**Pattern classification:** General learning material

**Difficulty:** Beginner to intermediate

**Prerequisites:** Comfortable reading basic C# and ASP.NET Core minimal API code, and registering services with `builder.Services`. EF Core, hosted services, and scope validation are introduced in the article. No AsiBackbone package or prior Learning material is required.

**Sample scope:** The C# targets .NET 8 or later with the built-in `Microsoft.Extensions.DependencyInjection` container and EF Core. The tests use xUnit v3 and the EF Core in-memory provider, with a separate in-memory database for each test. Most tests use it only so that a `DbContext` can be constructed; one uses it to reproduce EF Core's tracking behavior, which does not depend on the database provider, and one uses it to store a cart. Supporting types are in the [appendix](#appendix-supporting-types-and-test-helpers). The endpoint is a focused sample: it omits the authorization a real cart endpoint needs. Framework behavior is described from Microsoft's documentation on the verification date below; check it for your version, and for a different dependency-injection container. The store, its incident, its exchange rates, and its timings are a hypothetical teaching scenario, not a documented production incident. Original prose is licensed under [Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/), and original code examples are additionally offered under the [MIT License](https://opensource.org/license/mit), as described in the repository's [licensing terms](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md). Third-party material, where identified, remains under its original license. External sources were last verified on November 19, 2026.

**What this article covers:** a singleton cache that captured a request-scoped `DbContext`; transient, scoped, and singleton described by sharing, lifetime, concurrency, and disposal; what scope validation catches and what it cannot see; one small application with a stated reason for every lifetime; scopes, thread safety, disposal, hosted services, and `IDbContextFactory<TContext>`; tests for the instance identities a design depends on; a [decision guide](#choosing-a-lifetime); and what a lifetime does not give you.

**In plain terms:** a hotel puts out paper cups that each person takes, and that staff clear away at the end of the day. It hands each guest a key card for one stay, which the front desk cancels at checkout. It keeps one clock in the lobby, which everyone reads at once, so it has to make sense to all of them at once. Now suppose the lobby clock needs a key card to open a cabinet, and someone hands it a card issued for no particular stay. No checkout ever cancels that card, and everyone in the lobby uses it at the same time. Nobody decided that; it followed from who issued the card.

Consider a hypothetical online store; the incident below is a teaching scenario built to show several lifetime failures at once. Prices are stored in US dollars, and the cart page converts the total into the shopper's currency. The back-office application, a separate process, maintains one exchange-rate row per currency, including a row for US dollars at 1.0. A developer noticed that the cart page read the same rate row on almost every request and added a small cache, registered as a singleton so that every request would share it:

```csharp
public sealed class ExchangeRateCache(ShopDbContext db)
{
    private readonly ConcurrentDictionary<string, CachedRate> _rates = new(StringComparer.Ordinal);

    public async Task<decimal> GetUnitsPerUsdAsync(string currency, CancellationToken cancellationToken)
    {
        if (_rates.TryGetValue(currency, out CachedRate? cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.UnitsPerUsd;
        }

        ExchangeRate row = await db.ExchangeRates.SingleAsync(r => r.Currency == currency, cancellationToken);
        _rates[currency] = new CachedRate(row.UnitsPerUsd, DateTimeOffset.UtcNow.AddMinutes(15));
        return row.UnitsPerUsd;
    }

    private sealed record CachedRate(decimal UnitsPerUsd, DateTimeOffset ExpiresAt);
}
```

```csharp
builder.Services.AddDbContext<ShopDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddSingleton<ExchangeRateCache>();
```

The code compiled, the cart page got faster, and every manual test passed. The team's local launch profile set `ASPNETCORE_ENVIRONMENT` to `Local`, a name they had chosen years earlier, and production ran as `Production`.

A few weeks later, finance lowered the euro rate at 09:00. These selected lines from an illustrative log show what the store did with that change:

```text
09:00:03.114  back-office  ExchangeRates EUR 0.9180 → 0.9025 saved
09:31:42.318  web-1        GET /carts/7d1e…?currency=EUR → 200  total 91.80 EUR
10:02:17.640  web-2        GET /carts/7d1e…?currency=EUR → 200  total 90.25 EUR
11:48:09.551  web-1        GET /carts/a03c…?currency=GBP → 500  InvalidOperationException: A second operation was started on this context instance before a previous operation completed. …
15:30:12.004  web-1        Restarted for an unrelated configuration change
15:31:05.876  web-1        GET /carts/7d1e…?currency=EUR → 200  total 90.25 EUR
```

From mid-morning until `web-1` restarted that afternoon, the same $100.00 cart cost €91.80 on one instance and €90.25 on the other, although every cache entry expired every fifteen minutes. Around lunchtime, a handful of cart requests on `web-1` failed with an EF Core concurrency error that nobody could reproduce. A restart "fixed" both problems, which is usually the first clue that something was living as long as the process when it should not have.

The `ConcurrentDictionary` protected the cache's own entries, but nothing protected the `DbContext` behind it. The defect was the combination of a singleton registration and a constructor dependency meant for one short unit of work: the cache was a singleton, so the context it received was the only one it would ever have.

> **A service lifetime is more than registration syntax or an allocation optimization. It decides who shares an instance, how long the container keeps it and what stays reachable through it, who disposes it, and how widely concurrent requests can reach it. Choose it from the state the service owns and who should share that state, never let a singleton hold a scoped service, and test the identities your design relies on.**

The short version: decide who should share the state a service owns, whether that sharing is safe under concurrency, whether the service would hold a scoped dependency, and who disposes it. The [decision guide](#choosing-a-lifetime) near the end turns this into six questions.

---

## What the Container Actually Decides

A registration such as `AddScoped<CheckoutService>()` answers four questions at once, whether or not anyone asked them:

- **Sharing.** Which consumers receive the same instance?
- **Lifetime.** How long does the container keep the instance, and which objects and resources stay reachable because the instance references them?
- **Concurrency exposure.** How widely is the instance shared, and therefore how likely is it that more than one thread uses it at the same time?
- **Disposal.** Who calls `Dispose` or `DisposeAsync`, and when?

In an ASP.NET Core application, [the framework creates a dependency-injection scope for each request](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection#request-services), and the three built-in lifetimes answer those questions like this:

| | **Transient** | **Scoped** | **Singleton** |
| --- | --- | --- | --- |
| **Who shares an instance** | Each resolution normally creates a new instance, and the container keeps none for reuse. Whoever receives it may keep it and share it with everything that consumer serves | Every consumer that resolves the registration from the same scope: normally the request's scope, unless code creates another scope or uses a different provider | Every consumer resolved from the same root service provider, on every thread and for every request. A standard ASP.NET Core host has one root provider per running copy of the application, which this article calls an *application instance* |
| **How long the container keeps it** | A disposable transient is disposed when the scope it was resolved from is disposed, even if a consumer still holds a reference; one resolved from the root provider is kept until shutdown. A non-disposable transient lives as long as something references it | Until its scope is disposed; for a request scope, the end of the request | Until the root service provider is disposed, normally at application shutdown |
| **Concurrency exposure** | Low while its consumer keeps it to itself; a transient held by a singleton is reached by every request that reaches the singleton | Normally reached by one request's code at a time, but parallel work within that request still shares it | Reached concurrently, so it must be safe for concurrent use |
| **Who disposes it** | The scope it was resolved from, or the root provider if it was resolved there | Its scope | The root provider, unless your code created the instance and handed it to the container |

Microsoft's [service lifetimes documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/service-lifetimes) states the same rules in the container's terms: scoped services are created once per request and disposed at its end, singleton services must be thread-safe and are disposed when the service provider is disposed at shutdown, and services should never be disposed by the code that resolved them.

Three consequences follow, and the rest of the article leans on them:

- **Holding is not owning.** A reference keeps an object reachable; it does not extend the period during which its owner considers it valid. With validation off, a scoped dependency resolved while a singleton is constructed is owned by the root provider, so it stays valid, and shared by every caller of the singleton, until shutdown. That is a *captive dependency*, a term [Mark Seemann coined](https://blog.ploeh.dk/2014/06/02/captive-dependency/) and Microsoft's [dependency-injection guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines) use. If a singleton instead keeps a scoped object captured from a request or explicit scope, that scope still disposes the object when it ends, and the singleton is left holding a disposed object.
- **A singleton can capture a transient, too.** The same ownership reasoning applies to transients, and validation allows the capture. A transient held by a singleton effectively lives and is shared like a singleton, so it must be safe to keep and share that way. A transient with mutable state, written for one consumer, becomes state shared by every request.
- **The lifetime describes sharing, not thread safety.** Transient does not make a type thread-safe, and scoped does not make it single-threaded. Those are properties of the type and of how its consumers use it; the lifetime only decides how widely it is shared.

### Instance lifetime is not data lifetime

The cache in the opening example involved four different lifetimes, and only one of them was written in `Program.cs`:

| Thing | How long it should live |
| --- | --- |
| The `ExchangeRateCache` instance | The application instance, because the whole point is that every request shares it |
| The rates it holds | Until finance changes them, which the cache approximates with a fifteen-minute expiry |
| The `DbContext` it used to load them | One unit of work. A context [is designed for a single, usually short, unit of work](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/), and it keeps change-tracking state for every entity its tracking queries load |
| The rate rows in the database | Indefinitely; they are the source of truth |

When a registration stretches an instance's lifetime, everything that instance references stays reachable for as long. That does not mean every underlying resource stays open: EF Core normally opens a database connection for an operation and releases it afterward. What stayed alive here was the context's change tracker, and with it the first euro row the context had loaded.

---

## Why the Cache Served Yesterday's Rate

The singleton was created the first time a request needed it. Because a singleton belongs to the root provider, its constructor dependencies were resolved from the root provider too, and that included the `ShopDbContext`. With scope validation off, the container did what it was told: it created one context outside any request scope and gave it to the singleton, which kept it until shutdown.

That one context produced both symptoms:

- **Stale data.** EF Core queries are tracking queries by default. When a tracking query returns a row whose entity the context already tracks, [EF Core returns the existing instance and does not overwrite its current and original values with the database's](https://learn.microsoft.com/en-us/ef/core/querying/tracking). `web-1` had loaded the euro row before 09:00, so every fifteen-minute reload issued a real query, received the new rate from the database, and returned the old tracked object anyway. `web-2` first loaded the row after the change, which is why the two instances disagreed. The expiry was working. The cache was simply asking a context whose idea of the data was as old as the process. A [test below](#reproducing-the-stale-rate) reproduces this without a web server.
- **Concurrency failures.** EF Core does not support more than one operation running on the same `DbContext` instance at the same time, and [it throws `InvalidOperationException` when it detects one](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/#avoiding-dbcontext-threading-issues). Two requests whose cache entries for different currencies expired at the same moment both queried the singleton's one context. Most of the time the queries did not overlap, which is why nobody could reproduce the error on demand.

Had the cache ever modified an entity and called `SaveChangesAsync`, a third problem would have appeared: every request's changes would have accumulated in one change tracker, and any request could have saved another request's half-finished edits.

---

## Scope Validation Would Have Refused It

The built-in container can check for this mistake. [`ServiceProviderOptions`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.serviceprovideroptions) has two relevant switches:

- `ValidateScopes` makes the container refuse to resolve a scoped service from the root provider, which includes resolving it as a dependency of a singleton.
- `ValidateOnBuild` checks the registrations it can analyze when the provider is built, so the mistake surfaces at startup rather than on the first request that needs the service.

In the Development environment, `WebApplication.CreateBuilder` enables both, and [the service lifetimes documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/service-lifetimes) notes that, by default in development, resolving a service from a longer-lived service throws an exception. `Host.CreateApplicationBuilder` and `Host.CreateDefaultBuilder` apply the same Development default. A bare `new HostBuilder()` [gained it only in .NET 9](https://learn.microsoft.com/en-us/dotnet/core/compatibility/aspnet-core/9.0/hostbuilder-validation), and only when the options were not set explicitly, so check the hosting API as well as the framework version. In the opening example, the developers' environment was named `Local`, so `IsDevelopment()` was false, validation was off on every machine that ran the code, and the singleton was built without complaint.

With validation on, the same registrations stop the application at startup:

```text
System.AggregateException: Some services are not able to be constructed (Error while validating the service
descriptor 'ServiceType: Shop.ExchangeRateCache Lifetime: Singleton ImplementationType: Shop.ExchangeRateCache':
Cannot consume scoped service 'Shop.ShopDbContext' from singleton 'Shop.ExchangeRateCache'.)
```

Validation should not depend on someone remembering to run the application in one particular environment. Two inexpensive habits close that gap:

1. **Validate the real registrations in a test**, with both switches on, on every build. The [first test below](#scope-validation-rejects-the-captive-dependency) does that.
2. **Enable validation in every environment where you run the application before production**, whatever those environments are called:

```csharp
builder.Host.UseDefaultServiceProvider((context, options) =>
{
    bool preProduction = !context.HostingEnvironment.IsProduction();
    options.ValidateScopes = preProduction;
    options.ValidateOnBuild = preProduction;
});
```

Those two habits give most teams most of the benefit. Whether to validate in production as well is a tradeoff: validation adds work when the provider is built and, for `ValidateScopes`, when services are resolved. For an application that restarts rarely, a little extra startup time may be a fair price for refusing to start with an invalid dependency graph. Measure it before deciding either way.

### What validation can and cannot see

Scope validation checks the dependency graph the container builds from the registrations it can analyze. It does not run your code, and it does not check what your code does with the objects afterward.

| Caught | Not caught |
| --- | --- |
| A singleton whose constructor depends, directly or through transient services, on a scoped service | A singleton that keeps a scoped object passed to one of its *methods*, such as `cache.Remember(dbContext)` |
| A scoped service resolved from the root provider, for example `app.Services.GetRequiredService<ShopDbContext>()` in `Program.cs`, or from the provider a singleton's factory receives | The body of a factory lambda, which `ValidateOnBuild` cannot inspect. With `ValidateScopes`, a singleton factory fails only when it runs and resolves a scoped service from the root provider. A factory that returns an object holding the provider and resolving later fails only at that later call, and without validation it hands out root-owned instances every time. A factory that captures an existing instance from surrounding code is invisible to both switches |
| | Open generic registrations, which `ValidateOnBuild` does not validate; only the closed types you actually resolve are checked, when you resolve them |
| | A transient or singleton with mutable state that a singleton shares across requests |
| | Any type that is not thread-safe, and any parallel use of a scoped service inside one request |
| | Code that disposes an injected service, and disposable transients that accumulate in the root provider |
| | A lambda or `Task.Run` that captures a request's services and runs after the request ends |
| | Anything at all in an environment where validation is off |

A passing validation therefore means the container found no scoped-lifetime violation in the dependency paths it could analyze. It does not prove that every runtime resolution is safe, that every service can be constructed, or that application code respects ownership boundaries.

---

## Two Opposite Repairs That Do Not Work

Once the team understood the incident, two quick repairs were proposed. Both change a lifetime without asking what the service owns.

**Make everything scoped.** Registering the cache as scoped satisfies validation immediately. It also creates a new, empty cache for every request, so every request queries the database, and the cache becomes a more complicated way of not caching. "Scoped by default" also does not answer the concurrency question: a scoped `DbContext` still fails if one request runs two queries on it in parallel.

**Promote services to singleton to save allocations.** Someone suggested making `CheckoutService`, the request service that prices the cart, a singleton, because it is "created on every request." It depends on `ShopDbContext`, so the result would be the same captive dependency in a different class. A stateless service that depends on nothing shorter-lived can safely be a singleton, but that is a performance decision; make it only when a measurement shows the allocation matters. Microsoft's [guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines) recommend the singleton lifetime for services whose own state is expensive to create or genuinely shared, and recommend against it for services that have no state of their own.

The repair that works starts from ownership: **the cache owns rate data, not a long-lived unit of work.** Loading the data is a unit of work, and a unit of work needs its own short-lived context.

---

## One Small Application, a Reason for Every Lifetime

Here is the store after the repair. Each registration states the reason for its lifetime:

```csharp
public static class ShopServiceRegistration
{
    public static IServiceCollection AddShop(
        this IServiceCollection services, Action<DbContextOptionsBuilder> configureDatabase)
    {
        // Stateless: holds nothing between calls and depends on nothing.
        services.AddTransient<CartSummaryMapper>();

        // One unit of work per request: not thread-safe, disposed with the request.
        services.AddDbContext<ShopDbContext>(configureDatabase);

        // Does one request's work with that request's context.
        services.AddScoped<CheckoutService>();
        services.AddScoped<IExchangeRateLoader, ExchangeRateLoader>();

        // Rate data shared by every request in this application instance; safe for concurrent reads.
        services.AddSingleton<ExchangeRateCache>();

        // Owned by the host for the life of the application; creates a scope for each refresh.
        services.AddHostedService<ExchangeRateRefresher>();

        return services;
    }
}
```

```csharp
builder.Services.AddShop(options => options.UseSqlServer(builder.Configuration.GetConnectionString("Shop")));
```

Putting the registrations in one extension method is not a dependency-injection rule. It lets the application and the tests register exactly the same services, which matters for the [tests later in the article](#testing-the-identities-you-depend-on).

### The stateless mapper

`CartSummaryMapper` converts a cart and a rate into the summary the page shows. It has no fields and no dependencies:

```csharp
public sealed class CartSummaryMapper
{
    public CartSummary ToSummary(Cart cart, string currency, decimal unitsPerUsd)
    {
        ArgumentNullException.ThrowIfNull(cart);   // Lines must be loaded; CheckoutService includes them

        decimal totalUsd = cart.Lines.Sum(line => line.UnitPriceUsd * line.Quantity);

        // This sample deliberately uses ToEven. Production rounding must follow the application's monetary rules.
        decimal total = decimal.Round(totalUsd * unitsPerUsd, 2, MidpointRounding.ToEven);
        return new CartSummary(cart.Id, currency, total, cart.Lines.Count);
    }
}
```

For a type like this, the lifetime barely matters: no state can leak between consumers, nothing needs disposing, and nothing it depends on can be captured. Transient is the plain choice. Singleton would also be correct, so nothing else in the application, including the tests, should depend on which one it is.

### The request-scoped service and its `DbContext`

`AddDbContext` registers the context [with a scoped lifetime by default](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/service-lifetimes), because a web request is the usual unit of work. Services that depend on `ShopDbContext` and are resolved through the same request scope receive the same context instance, so they see the same tracked entities and can save their changes together.

A request scope is a convenient default boundary for a unit of work, not a guarantee that everything done during a request is one database transaction. A request that calls `SaveChangesAsync` more than once, or that needs several independent units of work, still needs an explicit transaction design.

`CheckoutService` does one request's work with that context. It reports a missing rate as its own outcome instead of quietly pricing the cart in another currency:

```csharp
public sealed class CheckoutService(ShopDbContext db, ExchangeRateCache rates, CartSummaryMapper mapper)
{
    public async Task<CartQuote> QuoteAsync(Guid cartId, string currency, CancellationToken cancellationToken)
    {
        // Currency codes are compared case-insensitively: normalize once, as the loader does for stored codes.
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        string code = currency.Trim().ToUpperInvariant();

        // Rate checks need no database call, so they come first.
        RateSnapshot? snapshot = rates.Current;   // read once, so this quote uses one consistent snapshot
        if (snapshot is null)
        {
            return new CartQuote(CartQuoteStatus.RatesNotLoaded);
        }

        if (!snapshot.UnitsPerUsd.TryGetValue(code, out decimal unitsPerUsd))
        {
            return new CartQuote(CartQuoteStatus.UnsupportedCurrency);
        }

        Cart? cart = await db.Carts
            .AsNoTracking()
            .Include(c => c.Lines)
            .SingleOrDefaultAsync(c => c.Id == cartId, cancellationToken);

        return cart is null
            ? new CartQuote(CartQuoteStatus.CartNotFound)
            : new CartQuote(CartQuoteStatus.Priced, mapper.ToSummary(cart, code, unitsPerUsd));
    }
}
```

The order of the checks is a stated precedence: when rates are unavailable or the currency is unsupported, the quote says so without querying for the cart, and `CartNotFound` is reported only when a price could otherwise have been produced. The endpoint, in the [appendix](#application-types), turns those outcomes into `200`, `404`, `400`, and `503` responses. Returning a price in US dollars when the caller requested euros would silently change the quote's currency unless the caller checked the response; an unavailable rate should be a distinct outcome.

This sample deliberately does not enforce a maximum snapshot age: after repeated refresh failures, it keeps quoting with the last rates it loaded. A production quoting policy must decide when a stale rate stops being acceptable, for example by comparing `LoadedAt` with a limit and reporting the rates as unavailable.

Scoped is the natural fit. Transient would also work, because a transient service may depend on a scoped one; it would simply be a new object for each resolution within the request. Singleton would not, because it would capture the context.

The dependency on `ExchangeRateCache` is fine: a shorter-lived service may always depend on a longer-lived one.

Scoped does not mean single-threaded. A request that starts two operations on its one context at the same time fails exactly as the singleton did:

```csharp
// Don't: two operations on the request's one context at the same time.
Task<Cart?> cart = db.Carts.Include(c => c.Lines).SingleOrDefaultAsync(c => c.Id == cartId, cancellationToken);
Task<List<ExchangeRate>> allRates = db.ExchangeRates.ToListAsync(cancellationToken);
await Task.WhenAll(cart, allRates);
```

Await each operation before starting the next, or give each parallel operation its own context, as [shown later](#when-work-needs-its-own-context).

### The singleton cache holds data, not a context

The repaired cache has no dependencies at all. It holds the most recent snapshot of rates:

```csharp
public sealed class ExchangeRateCache
{
    private RateSnapshot? _current;

    public RateSnapshot? Current => Volatile.Read(ref _current);

    // Single writer: only ExchangeRateRefresher calls this, from a loop that never overlaps itself.
    internal void Replace(RateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Volatile.Write(ref _current, snapshot);
    }
}
```

```csharp
public sealed record RateSnapshot(FrozenDictionary<string, decimal> UnitsPerUsd, DateTimeOffset LoadedAt);
```

A singleton is reached by many requests at once, so it [must be thread-safe](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/service-lifetimes). The container does not provide that: Microsoft's [guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines) state that the container's own thread safety covers constructing and resolving services, not the instances it returns. This cache gets it from three choices:

- **Immutable contents.** A `RateSnapshot` holds a `FrozenDictionary`, so the type itself guarantees that nobody can change a snapshot after it is built. A record declared with `IReadOnlyDictionary` would only promise that the cache does not change it; a caller could still pass a `Dictionary` and keep modifying it.
- **Safe publication.** The loader builds the snapshot completely before `Replace` publishes it with `Volatile.Write`, and readers obtain it with `Volatile.Read`, so a reader that sees the new snapshot also sees its fully built contents.
- **One read per use.** `CheckoutService` reads `Current` once and works only with that snapshot, so a refresh in the middle of a quote cannot mix two snapshots.

`Volatile.Write` provides safe publication, not writer coordination. The sequential refresher loop is the only production writer in this sample. Making `Replace` and `RefreshOnceAsync` `internal` limits who can call them, but it does not serialize calls; other code in the same assembly must preserve the convention. If refreshes could overlap, two loads could finish out of order and an older result could replace a newer one. Serialize the refreshes, for example with a `SemaphoreSlim`, or order snapshots by a version the source data itself carries, not by wall-clock time. `LoadedAt` in particular records when this application finished loading the data, not when the source rows last changed, and a slow load that started first can finish last.

The tempting alternative, a shared `Dictionary<string, decimal>` that requests update when they find a missing entry, is not safe. `Dictionary<TKey, TValue>` does not support a write concurrent with other reads or writes, and the result is unpredictable: exceptions, wrong answers, or an inconsistent dictionary. A `ConcurrentDictionary`, as in the opening example, is safe for concurrent access, but it would not have helped there, because the problem was the context behind it.

This cache is hand-built so that its ownership is visible. For many applications, `IMemoryCache`, which `AddMemoryCache` registers as a singleton, is a better starting point than a custom cache, and the same lifetime rules apply to whatever you put in it: cache data, never a `DbContext` or an entity a context is still tracking.

The snapshot records when it was loaded. How old a rate may become before the page should stop using it is a caching decision, not a lifetime one, and the [last section](#what-a-lifetime-does-not-give-you) returns to it.

Notice also what "singleton" does not mean here. A singleton is one instance per root service provider. Separate application instances have separate providers, and therefore separate caches, which may briefly disagree after finance changes a rate. A singleton is not global business state.

### The hosted service owns a scope per unit of work

Something still has to load the rates. That is the job of a hosted service:

```csharp
public sealed class ExchangeRateRefresher(
    IServiceScopeFactory scopeFactory,
    ExchangeRateCache cache,
    ILogger<ExchangeRateRefresher> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            // Refresh once at startup, then on periodic five-minute ticks. Refreshes are awaited one at a time;
            // ticks that pass during a slow refresh are coalesced, so the next one may follow immediately.
            do
            {
                try
                {
                    await RefreshOnceAsync(stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    // Keep the previous snapshot, if any, and try again on the next tick.
                    if (cache.Current is { } kept)
                    {
                        logger.LogError(exception, "Exchange-rate refresh failed; keeping the snapshot loaded at {LoadedAt}.", kept.LoadedAt);
                    }
                    else
                    {
                        logger.LogError(exception, "Exchange-rate refresh failed; no snapshot has loaded yet.");
                    }
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping. This is normal shutdown, not a failed refresh.
        }
    }

    // One unit of work: a new scope, a new loader and context, and both disposed when it ends.
    // Internal, and visible to the test project. This limits callers; it does not serialize them.
    internal async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IExchangeRateLoader loader = scope.ServiceProvider.GetRequiredService<IExchangeRateLoader>();

        RateSnapshot snapshot = await loader.LoadAsync(cancellationToken);
        cache.Replace(snapshot);
    }
}
```

```csharp
public sealed class ExchangeRateLoader(ShopDbContext db) : IExchangeRateLoader
{
    public async Task<RateSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        // Normalize stored codes the same way CheckoutService normalizes requested ones. Two rows that
        // differ only by case or spacing make ToDictionaryAsync throw, which fails this refresh loudly.
        Dictionary<string, decimal> rates = await db.ExchangeRates
            .AsNoTracking()
            .ToDictionaryAsync(r => r.Currency.Trim().ToUpperInvariant(), r => r.UnitsPerUsd, cancellationToken);

        // An empty table is a configuration error, not a set of unsupported currencies: refuse to publish it.
        if (rates.Count == 0)
        {
            throw new InvalidOperationException("The exchange-rate table is empty.");
        }

        return new RateSnapshot(rates.ToFrozenDictionary(StringComparer.Ordinal), DateTimeOffset.UtcNow);
    }
}
```

> The dictionary's key selector normalizes values during materialization on the application side. It is not intended to demonstrate SQL-side normalization.

`AddHostedService` registers the refresher as a singleton `IHostedService`. The host creates it once at startup and keeps it until shutdown, so whatever its class looks like, it lives like a singleton and may depend in its constructor only on things that are safe to keep that long: here, the scope factory, the cache, and a logger. [No scope is created for a hosted service by default](https://learn.microsoft.com/en-us/dotnet/core/extensions/scoped-service); Microsoft's pattern is to create one for each piece of scoped work, which is what `RefreshOnceAsync` does.

The loader normalizes currency codes rather than trusting the database to: SQL Server's default collation is case-insensitive, but the in-memory provider and other databases compare text exactly. It also refuses to publish an empty snapshot, so an empty table leaves quotes reporting `RatesNotLoaded` rather than calling every currency unsupported.

Each refresh gets a new scope, a new `ExchangeRateLoader`, and a new `ShopDbContext` with an empty change tracker, all disposed when the `await using` block ends, whether the load succeeds or throws. The query also uses `AsNoTracking`, because nothing loaded here will be saved. Holding one scope, or one context, for the refresher's whole life would recreate the original incident inside the hosted service.

A few behaviors are easy to misread:

- **The first refresh runs immediately.** The `do` loop refreshes once at startup and then waits for the timer's next periodic tick, which is not necessarily five minutes after the previous refresh finished. Requests can still arrive before that first refresh completes, which is why `CheckoutService` reports `RatesNotLoaded` instead of guessing. An application that must not serve requests without rates should add a readiness check that reports unhealthy until `Current` is set.
- **Shutdown is not a failure.** When the host stops, it cancels `stoppingToken`. A pending timer wait or database query then throws `OperationCanceledException`. The inner `catch` deliberately excludes that case, so it is not logged as a failed refresh, and the outer `catch` ends the loop quietly.
- **`RefreshOnceAsync` is internal.** The tests call it through `InternalsVisibleTo`. That keeps code outside the application assembly from calling it; inside the assembly, keeping it to the loop is a convention, and the cache's single-writer assumption depends on it.

Two boundaries are worth stating plainly:

- **This is not service location as a design.** Microsoft's [guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines) recommend avoiding the service locator pattern, in which ordinary classes ask a provider for their dependencies when they could receive them through injection. The refresher calls `GetRequiredService` once per cycle, at the boundary where a unit of work begins, to obtain the root object of that unit of work; everything below it, including the loader's context, still arrives by constructor injection. If you cannot point to the place where a unit of work starts, a call to `GetRequiredService` is a lookup, not a boundary, and the design needs another look.
- **A scope does not make the work durable.** The scope gives each refresh a fresh, disposable unit of work. If the process stops halfway through a refresh, that refresh is simply gone, which is harmless here because the next tick, or the next process, loads the rates again. Work that must not be lost needs a durable record before anyone relies on it; [Task.Run Does Not Make ASP.NET Core Work Durable](task-run-does-not-make-aspnet-core-work-durable.md) covers that choice.

The direction of ownership matters too. The refresher depends on the cache; the cache does not know the refresher exists. A cache that loaded its own data on a miss would need a different way to reach the database, which is the case for a [context factory](#when-work-needs-its-own-context).

---

## Request Scopes and Explicit Scopes

A scope is an ownership boundary: it owns the scoped instances resolved from it, and the disposable scoped and transient instances it created, and disposes them when it is disposed.

- **Request scopes** are created and disposed by ASP.NET Core. Scoped and transient services resolved for controllers and their constructor dependencies, for minimal API endpoint parameters, or through `HttpContext.RequestServices` belong to the current request's scope, and [they are valid for as long as the request is active](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection#request-services). Singleton dependencies resolved the same way remain owned by the root provider.
- **Explicit scopes** are created by your code with `IServiceScopeFactory.CreateScope` or `CreateAsyncScope`, and your code must dispose them. Use one where a unit of work begins outside a request: a hosted service cycle, a queued work item, or a startup task that needs scoped services. Prefer `CreateAsyncScope` with `await using` when any service in the scope might implement `IAsyncDisposable`, as a `DbContext` does.

Minimal APIs follow the same rule. The endpoint delegate is created once, when the route is mapped, but the services among its parameters, such as `CheckoutService`, are resolved from the current request's scope on every call. Declare services as parameters of the handler; do not resolve them from `app.Services` while mapping routes and capture them in the lambda, because that resolves them from the root provider once, for every request.

In Microsoft's built-in container, scopes are independent. A scope created during a request is not a child of the request's scope and does not inherit or share its scoped instances; Microsoft's [guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines) state that there is no special connection among scopes. Some third-party containers support nested lifetime scopes with different rules.

Other framework components besides hosted services also live like singletons. Conventional middleware, added with `UseMiddleware<T>` and an `InvokeAsync` method, is constructed once; the ASP.NET Core documentation says to resolve scoped services as `InvokeAsync` parameters rather than in its constructor, because constructor injection would make them behave like singletons. Factory-based middleware that implements `IMiddleware` is different: it is activated per request, so scoped services can be injected into its constructor. An ASP.NET Core authorization handler registered as a singleton has the singleton constraint, which [Policy as Code in ASP.NET Core Without Overengineering](policy-as-code-aspnet-core-without-overengineering.md) notes for a handler that would use a scoped `DbContext`.

---

## Disposal Follows Ownership

The rule is short: **disposal follows ownership.** Who owns an instance is not always the code that contains its `new` expression.

- **Services the container creates or obtains from a registration factory**, such as `AddSingleton(sp => new ExampleService())`, are owned by the container. It disposes them at the end of the scope they were resolved from, or when the root provider is disposed for singletons. [Services resolved from the container should never be disposed by the developer](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines), and a class that receives an `IDisposable` dependency does not need to implement `IDisposable` merely because of it.
- **An instance you hand to the container already built**, as in `AddSingleton(existingInstance)`, is not disposed by the container. Its disposal is your responsibility.
- **Contexts created by an `IDbContextFactory<TContext>`** are not managed by the container, so [the code that creates them must dispose them](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/). Other factories should say in their contract who owns what they create.

The most common disposal bug is a consumer that tidies up something it does not own:

```csharp
public async Task ApplyDiscountAsync(Guid cartId, string code, CancellationToken cancellationToken)
{
    using (db)   // Don't: the request's scope owns this context.
    {
        // ...
        await db.SaveChangesAsync(cancellationToken);
    }
}
```

The method works when tested alone. In the application, any other code that uses the request's context after it, such as another service or a filter that saves pending changes, receives an `ObjectDisposedException` that has nothing to do with the code that caused it.

The opposite leak also exists. A disposable *transient* service resolved from the root provider, for example inside a singleton's factory or from `app.Services`, is tracked by the root provider so that it can be disposed at shutdown, and it is therefore kept alive until then. Resolving one repeatedly accumulates them; Microsoft's [guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines) list this as an anti-pattern. Scope validation does not flag it.

---

## When Work Needs Its Own Context

Sometimes the unit of work does not line up with any scope. Examples include a singleton that genuinely needs occasional database access, several independent operations in one request that should not share a change tracker, and parallel operations, each of which needs its own context. EF Core supports these cases with `IDbContextFactory<TContext>`, registered with [`AddDbContextFactory`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.entityframeworkservicecollectionextensions.adddbcontextfactory). The factory is registered as a singleton by default, so even a singleton may depend on it, and every context it creates belongs to the code that created it.

If the store preferred to load rates on a cache miss rather than refresh them on a timer, the cache could use a factory:

```csharp
public sealed class LoadingExchangeRateCache(IDbContextFactory<ShopDbContext> contexts)
{
    private readonly ConcurrentDictionary<string, CachedRate> _rates = new(StringComparer.Ordinal);

    // Callers pass normalized upper-case codes. Returns null for a currency with no rate; callers treat that
    // as unsupported. A sketch: concurrent misses for one currency can race, as the text below explains.
    public async Task<decimal?> GetUnitsPerUsdAsync(string currency, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        if (_rates.TryGetValue(currency, out CachedRate? cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.UnitsPerUsd;
        }

        // One context per load: owned here, disposed here, never shared with a concurrent load.
        await using ShopDbContext db = await contexts.CreateDbContextAsync(cancellationToken);
        decimal? unitsPerUsd = await db.ExchangeRates
            .AsNoTracking()
            .Where(r => r.Currency == currency)
            .Select(r => (decimal?)r.UnitsPerUsd)
            .SingleOrDefaultAsync(cancellationToken);

        if (unitsPerUsd is { } value)
        {
            _rates[currency] = new CachedRate(value, DateTimeOffset.UtcNow.AddMinutes(15));
        }

        return unitsPerUsd;
    }

    private sealed record CachedRate(decimal UnitsPerUsd, DateTimeOffset ExpiresAt);
}
```

It is registered with `services.AddSingleton<LoadingExchangeRateCache>()`, and like any singleton it may hold the factory but never a context. Concurrent misses now use separate contexts, and each load uses a fresh context that cannot hand back a previously tracked rate, which is the guarantee the original incident lacked. Each context is disposed as soon as its query finishes. A fresh context removes staleness caused by reusing a change tracker; it does not guarantee that the query sees the latest committed state, which still depends on transaction isolation, replication, and concurrent updates.

This version is a sketch with a known race: two concurrent misses for the same currency may both query the database, and if they finish out of order, the older result may overwrite the newer one until the entry expires. Comparing expiry times would not fix it, for the same reason `LoadedAt` would not; preventing it needs per-currency coordination, such as allowing only one load per currency at a time. That is a caching decision, not a lifetime one, which is why the article's main design refreshes on a single timer instead.

A factory is not a way around lifetime design:

- Each context it creates is still a single unit of work and is still not thread-safe. Do not store one in a field or pass it to parallel operations.
- A factory used everywhere instead of the request's context gives up the shared unit of work that scoped services rely on to see each other's changes and save them together.

`AddDbContextFactory` [also registers the context type itself as a scoped service](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.entityframeworkservicecollectionextensions.adddbcontextfactory), so one call can serve both request-scoped injection and independently created contexts:

```csharp
// Usually enough on its own: a singleton factory and a scoped ShopDbContext.
services.AddDbContextFactory<ShopDbContext>(configureDatabase);
```

An application that also calls `AddDbContext` for the same context, for example because existing registration code already does, needs compatible options lifetimes. Depending on the EF Core version and the order of the calls, `AddDbContext`'s default scoped options can end up consumed by the default singleton factory, which scope validation reports as the same singleton-to-scoped error. One way to align them, provided the options configuration does not depend on scoped services such as the current tenant:

```csharp
services.AddDbContext<ShopDbContext>(configureDatabase, optionsLifetime: ServiceLifetime.Singleton);
services.AddDbContextFactory<ShopDbContext>();   // reuses the options configured above; configure them once
```

Whatever you choose, build the result with `ValidateOnBuild` on your EF Core version and resolve both the context and the factory in a test.

---

## Testing the Identities You Depend On

Lifetime bugs are bugs about identity: two consumers that should share an instance do not, or two that should not, do. The tests below check exactly the identities the store depends on, using the application's own `AddShop` method with validation always on, whatever environment the test runner reports.

```csharp
public sealed class LifetimeTests
{
    private static readonly ServiceProviderOptions Validated = new() { ValidateScopes = true, ValidateOnBuild = true };

    private static ServiceCollection ShopServices()
    {
        var services = new ServiceCollection();
        string databaseName = Guid.NewGuid().ToString("N");   // one in-memory database per test
        services.AddLogging();
        services.AddShop(options => options.UseInMemoryDatabase(databaseName));
        return services;
    }

    private static ServiceProvider BuildShop(Action<IServiceCollection>? overrides = null)
    {
        ServiceCollection services = ShopServices();
        overrides?.Invoke(services);
        return services.BuildServiceProvider(Validated);
    }
}
```

The remaining tests are members of the same class. `Snapshot`, `LoaderLog`, and `RecordingLoader` are in the [appendix](#test-helpers). Each test owns its own provider, in-memory database, and log, so the tests do not depend on running in any particular order. They verify dependency-injection ownership and EF Core's tracking behavior, not relational database semantics: the in-memory provider uses non-relational comparison semantics and does not enforce relational foreign-key constraints or unique indexes in the same way as a relational database. It does, however, enforce primary-key identity. These tests do not replace integration tests against the application's actual database provider.

### Scope validation rejects the captive dependency

```csharp
[Fact]
public void A_singleton_that_captures_the_DbContext_is_rejected_when_the_provider_is_built()
{
    ServiceCollection services = ShopServices();
    services.AddSingleton<CaptiveExchangeRateCache>();   // the opening example's cache

    AggregateException failure = Assert.Throws<AggregateException>(() => services.BuildServiceProvider(Validated));

    Assert.Contains(failure.InnerExceptions, problem =>
        problem is InvalidOperationException
        && problem.Message.Contains(nameof(CaptiveExchangeRateCache))
        && problem.Message.Contains(nameof(ShopDbContext)));
}
```

The same `BuildShop` call succeeding in every other test is the other useful half of this check: the application's real registrations pass the analysis `ValidateOnBuild` can perform, on every build, with no dependency on an environment name. That does not run factory bodies, validate open generic registrations, construct every service, or check that the database is reachable. Where a factory-registered or generic service matters, resolve it explicitly in a test. The test also covers only what `AddShop` registers: a captive registration added directly in `Program.cs` would pass it, so keep registrations in methods the tests call, or validate the complete application's services with a test host such as `WebApplicationFactory`. If you build the provider with `ValidateScopes` but without `ValidateOnBuild`, the captive dependency surfaces as an `InvalidOperationException` when the singleton is first resolved instead.

### One scope shares one context, and scopes do not share

```csharp
[Fact]
public async Task A_scope_shares_one_DbContext_and_separate_scopes_do_not()
{
    await using ServiceProvider provider = BuildShop();
    await using AsyncServiceScope first = provider.CreateAsyncScope();
    await using AsyncServiceScope second = provider.CreateAsyncScope();

    ShopDbContext context = first.ServiceProvider.GetRequiredService<ShopDbContext>();

    Assert.Same(context, first.ServiceProvider.GetRequiredService<ShopDbContext>());      // one unit of work per request
    Assert.NotSame(context, second.ServiceProvider.GetRequiredService<ShopDbContext>());  // requests never share one
}
```

### Every scope sees the same cache

```csharp
[Fact]
public async Task A_snapshot_replaced_in_one_scope_is_visible_in_every_other()
{
    await using ServiceProvider provider = BuildShop();
    await using AsyncServiceScope first = provider.CreateAsyncScope();
    await using AsyncServiceScope second = provider.CreateAsyncScope();

    ExchangeRateCache cache = first.ServiceProvider.GetRequiredService<ExchangeRateCache>();
    cache.Replace(Snapshot(("EUR", 0.9025m)));

    ExchangeRateCache other = second.ServiceProvider.GetRequiredService<ExchangeRateCache>();
    Assert.Same(cache, other);
    Assert.Equal(0.9025m, other.Current!.UnitsPerUsd["EUR"]);
}
```

### Each refresh gets its own scope, and the scope is disposed

These two tests replace the loader with a scoped fake that records each instance it creates and whether that instance was disposed. The fake's registration is added after `AddShop`, so it is the one `GetRequiredService` returns; the original registration remains in the collection, and `GetServices<IExchangeRateLoader>()` would return both. Resolving the refresher through `IHostedService` uses the real `AddHostedService` registration.

```csharp
[Fact]
public async Task Each_refresh_resolves_its_loader_in_a_new_scope_and_disposes_it()
{
    var log = new LoaderLog();
    log.Results.Enqueue(() => Snapshot(("EUR", 0.9180m)));
    log.Results.Enqueue(() => Snapshot(("EUR", 0.9025m)));

    await using ServiceProvider provider = BuildShop(services =>
    {
        services.AddSingleton(log);
        services.AddScoped<IExchangeRateLoader, RecordingLoader>();   // last registration wins for GetRequiredService
    });
    ExchangeRateRefresher refresher = provider.GetServices<IHostedService>().OfType<ExchangeRateRefresher>().Single();

    await refresher.RefreshOnceAsync(TestContext.Current.CancellationToken);
    await refresher.RefreshOnceAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, log.Created.Count);
    Assert.NotSame(log.Created[0], log.Created[1]);                  // a fresh scoped loader for each refresh
    Assert.All(log.Created, loader => Assert.True(loader.Disposed)); // released when its scope ends

    await using AsyncServiceScope request = provider.CreateAsyncScope();
    ExchangeRateCache cache = request.ServiceProvider.GetRequiredService<ExchangeRateCache>();
    Assert.Equal(0.9025m, cache.Current!.UnitsPerUsd["EUR"]);        // requests see what the refresher loaded
}

[Fact]
public async Task A_failed_refresh_disposes_its_scope_and_keeps_the_previous_snapshot()
{
    var log = new LoaderLog();
    log.Results.Enqueue(() => Snapshot(("EUR", 0.9180m)));
    log.Results.Enqueue(() => throw new TimeoutException("Database timeout"));

    await using ServiceProvider provider = BuildShop(services =>
    {
        services.AddSingleton(log);
        services.AddScoped<IExchangeRateLoader, RecordingLoader>();
    });
    ExchangeRateRefresher refresher = provider.GetServices<IHostedService>().OfType<ExchangeRateRefresher>().Single();

    await refresher.RefreshOnceAsync(TestContext.Current.CancellationToken);
    await Assert.ThrowsAsync<TimeoutException>(() => refresher.RefreshOnceAsync(TestContext.Current.CancellationToken));

    Assert.All(log.Created, loader => Assert.True(loader.Disposed));
    Assert.Equal(0.9180m, provider.GetRequiredService<ExchangeRateCache>().Current!.UnitsPerUsd["EUR"]);
}
```

These tests show that each refresh creates and disposes a fresh scope containing a scoped loader. The production loader receives its context from that scope, so the context is released with it, but the tests observe the loader, not the context. The failure test calls `RefreshOnceAsync` directly; it shows that a failed load leaves the previous snapshot in place, not that the background loop logs the failure and tries again.

### Reproducing the stale rate

This test reproduces the opening incident's stale data without a web server. A context that stays alive across two units of work stands in for the captive one:

```csharp
[Fact]
public async Task A_context_that_outlives_its_unit_of_work_keeps_returning_the_rate_it_first_tracked()
{
    CancellationToken cancellationToken = TestContext.Current.CancellationToken;
    await using ServiceProvider provider = BuildShop();

    await using (AsyncServiceScope seed = provider.CreateAsyncScope())
    {
        ShopDbContext db = seed.ServiceProvider.GetRequiredService<ShopDbContext>();
        db.ExchangeRates.Add(new ExchangeRate { Currency = "EUR", UnitsPerUsd = 0.9180m });
        await db.SaveChangesAsync(cancellationToken);
    }

    await using AsyncServiceScope longLived = provider.CreateAsyncScope();
    ShopDbContext captive = longLived.ServiceProvider.GetRequiredService<ShopDbContext>();
    Assert.Equal(0.9180m, (await captive.ExchangeRates.SingleAsync(r => r.Currency == "EUR", cancellationToken)).UnitsPerUsd);

    await using (AsyncServiceScope backOffice = provider.CreateAsyncScope())
    {
        ShopDbContext db = backOffice.ServiceProvider.GetRequiredService<ShopDbContext>();
        ExchangeRate euro = await db.ExchangeRates.SingleAsync(r => r.Currency == "EUR", cancellationToken);
        euro.UnitsPerUsd = 0.9025m;
        await db.SaveChangesAsync(cancellationToken);
    }

    ExchangeRate tracked = await captive.ExchangeRates.SingleAsync(r => r.Currency == "EUR", cancellationToken);
    ExchangeRate current = await captive.ExchangeRates.AsNoTracking().SingleAsync(r => r.Currency == "EUR", cancellationToken);

    Assert.Equal(0.9180m, tracked.UnitsPerUsd);   // the tracking query returns the instance it already tracks
    Assert.Equal(0.9025m, current.UnitsPerUsd);   // the database has the new rate
}
```

### A missing rate is an explicit answer

```csharp
[Fact]
public async Task A_quote_reports_missing_rates_instead_of_pricing_in_another_currency()
{
    CancellationToken cancellationToken = TestContext.Current.CancellationToken;
    await using ServiceProvider provider = BuildShop();
    await using AsyncServiceScope request = provider.CreateAsyncScope();

    var cartId = Guid.NewGuid();
    ShopDbContext db = request.ServiceProvider.GetRequiredService<ShopDbContext>();
    db.Carts.Add(new Cart { Id = cartId, Lines = [new CartLine { Sku = "MUG-01", Quantity = 1, UnitPriceUsd = 100.00m }] });
    await db.SaveChangesAsync(cancellationToken);

    CheckoutService checkout = request.ServiceProvider.GetRequiredService<CheckoutService>();

    // Before the first refresh: no snapshot at all.
    Assert.Equal(CartQuoteStatus.RatesNotLoaded, (await checkout.QuoteAsync(cartId, "EUR", cancellationToken)).Status);

    request.ServiceProvider.GetRequiredService<ExchangeRateCache>().Replace(Snapshot(("USD", 1m), ("EUR", 0.9025m)));

    CartQuote euros = await checkout.QuoteAsync(cartId, "eur", cancellationToken);   // codes are case-insensitive
    Assert.Equal(CartQuoteStatus.Priced, euros.Status);
    Assert.Equal("EUR", euros.Summary!.Currency);
    Assert.Equal(90.25m, euros.Summary.Total);
    Assert.Equal(CartQuoteStatus.UnsupportedCurrency, (await checkout.QuoteAsync(cartId, "GBP", cancellationToken)).Status);
}
```

### What these tests deliberately do not assert

Each assertion above checks something the design relies on: a request's services share one unit of work, requests do not share one, every request reads the cache the refresher fills, each refresh owns and releases its own scope, and a missing rate is reported rather than replaced. The tests avoid assertions that would pass or fail for reasons that do not matter:

- **Nothing pins the mapper's lifetime.** No code depends on whether `CartSummaryMapper` is transient or singleton, so a test asserting `NotSame` for two mappers would only make a harmless change fail.
- **Nothing reads `ServiceDescriptor.Lifetime`.** Asserting that the cache is registered with `ServiceLifetime.Singleton` restates the registration; asserting that two scopes see the same instance and the same data tests the behavior the registration exists to produce.
- **Nothing counts constructor calls** through static counters, compares hash codes, or depends on the order in which a scope disposes its services. The container does not promise those details, and the design does not need them.

These tests cannot show that a type is thread-safe. A test that reads and replaces snapshots from many threads at once can find a race, but a passing run cannot prove there is none; the cache's safety comes from its design, and code review has to check it. The tests also cannot show that no code keeps a scoped object beyond its request, or that production enables validation. The [review checklist](#a-short-review-checklist) below covers those.

---

## Choosing a Lifetime

Six questions decide most registrations. Ask them in order:

| Question | What the answer means |
| --- | --- |
| **1. Does the service hold mutable state?** | No: transient is a reasonable starting point, but continue through the dependency, disposal, and boundary checks. Yes: identify who should share that state |
| **2. Who should share that state?** | A separate instance per resolution or consumer: transient. One shared instance across a unit of work: scoped. Every request in this application instance: singleton, and only with a yes to question 3 |
| **3. Is concurrent use safe?** | No: not a singleton, and do not share the scoped instance across parallel work within a request |
| **4. Would it hold a dependency beyond that dependency's intended owner?** | A scoped dependency: the service must not be a singleton; shorten its lifetime, pass data instead of the service, use a factory that creates owned instances, or create a scope at a real unit-of-work boundary. A transient dependency: holding it gives it the holder's lifetime and sharing, which is fine only if that instance is safe to keep and share that way |
| **5. Who owns disposal?** | Instances the container creates, including through registration factories: the container. Instances you hand to the container already built, contexts from `IDbContextFactory<TContext>`, and other objects whose factory transfers ownership to the caller: your code |
| **6. Does the work cross a request or host-lifecycle boundary?** | Yes: it must not keep request-owned services. If it needs scoped services, create a scope where the unit of work begins and dispose it where it ends; if it uses factory-created resources, dispose them as the factory's ownership contract says. Either way, the lifetime alone does not make the work durable |

Each lifetime has cases where it is the right answer:

| Lifetime | Reasonable when | In this article |
| --- | --- | --- |
| **Transient** | The service is stateless, or each consumer needs its own instance of a small stateful object, and nothing depends on identity | `CartSummaryMapper` |
| **Scoped** | The service is, or uses, a unit of work that the rest of the request should share, such as a `DbContext`, or it holds state that belongs to one request | `ShopDbContext`, `CheckoutService`, `ExchangeRateLoader` |
| **Singleton** | The service holds state that is expensive to create or genuinely shared by the whole application instance, is safe for concurrent use, and holds no scoped service | `ExchangeRateCache`, and the host-owned `ExchangeRateRefresher` |

---

## What a Lifetime Does Not Give You

A lifetime says how many instances exist, who shares them, and when they are disposed. It does not provide:

- **Durability.** A singleton's state ends with its process, and a scope created for a job ends with that job's code. Neither records anything that survives a restart.
- **Authorization.** A request scope is created per request, not per user, tenant, or permission. A scoped service still has to check that the caller may see what it loads, and a singleton cache that stores tenant-specific data under a key without the tenant will serve one tenant's data to another. When work leaves the request, [A Background Job Should Not Inherit Everything the User Could Do](background-job-should-not-inherit-user-authority.md) covers what authority the job's scope should carry.
- **Caching semantics.** "One instance per provider" says nothing about how long a value may be used, how it is invalidated, how large the cache may grow, or how stale a value is acceptable when refreshes fail. The store's refresher and snapshot timestamp are a deliberate, small caching design; registering a type as a singleton is not one.
- **Distributed coordination.** Every application instance has its own singletons and runs its own hosted services. Two instances of the store run two refreshers, which is harmless for an idempotent read. A hosted service whose work must run once across all instances needs a lease, a lock in a shared store, or a scheduler, and a `lock` statement inside a singleton coordinates only threads within one process.

---

## Failure Modes

**1. A singleton that captures a scoped service.** A scoped `DbContext` or other per-request service is injected into a singleton, directly or through a transient, and becomes application-wide: stale data, shared change tracking, and concurrency failures.

**2. A scoped service resolved from the root provider.** `app.Services.GetRequiredService<ShopDbContext>()` at startup, or a resolution from the provider a singleton's factory receives, creates an instance that no request owns and that lives until shutdown.

**3. Validation that only runs somewhere nobody looks.** Validation defaults depend on the hosting API and the environment. An application that runs under a custom environment name, or builds its provider without validation, misses the mistake unless a test explicitly enables both switches.

**4. A stateful transient shared by a singleton.** A transient with mutable fields, written for one consumer, is injected into a singleton. Validation allows it, and every request now shares that one instance.

**5. A singleton with unsynchronized mutable state.** A singleton updates a `Dictionary`, `List`, or plain fields from concurrent requests. The failures are intermittent and may produce wrong data rather than an exception.

**6. Parallel work on one scoped context.** A request runs two EF Core operations at once on the request's `DbContext`, for example with `Task.WhenAll`, and fails with `InvalidOperationException`, some of the time.

**7. Disposing an injected service.** A consumer wraps an injected `DbContext` in `using`, and a later consumer in the same request fails with `ObjectDisposedException`.

**8. Disposable transients accumulating in the root provider.** Disposable transients resolved from the root provider are kept until shutdown, and memory grows with each resolution.

**9. A hosted service that holds one scope for its whole life.** The service creates a scope, or resolves a `DbContext`, once in its constructor or at the start of `ExecuteAsync` and reuses it on every cycle, recreating failure mode 1 inside the hosted service.

**10. Lifetime changes made by reflex.** Everything is made scoped to silence validation, or services are promoted to singleton to avoid allocations, without asking what state each service owns.

---

## A Short Review Checklist

1. For each registration, can someone state who shares the instance and why, in one sentence?
2. Does any singleton, or any service a singleton depends on, directly or transitively, have a scoped dependency, or a transient dependency that is not safe to keep and share?
3. Is every singleton with mutable state safe for concurrent use, and is that safety designed, such as immutable snapshots or synchronization, rather than assumed?
4. Do hosted services, conventional middleware, and other singleton-like components create or receive scoped services only per unit of work or per invocation?
5. Does any code dispose an injected service, or resolve scoped or disposable transient services from `app.Services`?
6. Does a test build the application's real registrations with `ValidateScopes` and `ValidateOnBuild` enabled, independent of the environment name, and explicitly resolve the factory-registered and generic services that matter?
7. Do tests check the instance identities the design depends on, within a scope, across scopes, and across the request and background boundary, without pinning lifetimes nothing depends on?
8. Where work needs its own context, does the code that creates each context dispose it, and avoid sharing it across parallel operations?
9. Is it written down what the design does *not* provide, such as durability, cross-instance consistency, or authorization, wherever a reader might assume a singleton or a scope does?

---

## Continue Deeper

When work leaves the request entirely, the scope is only the first question. [Task.Run Does Not Make ASP.NET Core Work Durable](task-run-does-not-make-aspnet-core-work-durable.md) shows the `ObjectDisposedException` that follows when detached work keeps the request's `DbContext`, a bounded queue whose worker creates a scope for each item, and when work must be recorded durably instead.

A background job's scope should carry the right identity as well as the right services. [A Background Job Should Not Inherit Everything the User Could Do](background-job-should-not-inherit-user-authority.md) covers what the job records about who asked and how the worker checks it again before acting.

If a `DbContext` is the unit of work, its lifetime is part of the data-access design. [Data-Access Boundaries and Transaction Reasoning with EF Core](../../aspnetcore/data-access-boundaries-and-transaction-reasoning.md) explains why a context is one intentionally owned unit of work, when a repository adds a boundary, and how local transactions differ from effects outside the database.

Authorization handlers are resolved from the container too, and their lifetime follows the same rules. [Policy as Code in ASP.NET Core Without Overengineering](policy-as-code-aspnet-core-without-overengineering.md) shows where a handler's decision belongs and what it may depend on.

### Related Work

- [Service lifetimes](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/service-lifetimes), in the .NET documentation, defines transient, scoped, and singleton, states that `AddDbContext` registers contexts as scoped, says not to resolve a scoped service from a singleton except through an explicit scope, and requires singletons to be thread-safe.
- [Dependency injection guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines), in the .NET documentation, covers disposal ownership, thread safety, when to choose singleton, the service locator pattern, and the captive-dependency and disposable-transient anti-patterns.
- [Dependency injection in ASP.NET Core: Request Services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection#request-services) explains the per-request scope and also covers Entity Framework contexts and scoped services in middleware.
- [Use scoped services within a BackgroundService](https://learn.microsoft.com/en-us/dotnet/core/extensions/scoped-service), in the .NET documentation, shows creating a scope from a hosted service, which has none by default.
- [DbContext Lifetime, Configuration, and Initialization](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/), in the EF Core documentation, describes the context as a short-lived unit of work, the threading restrictions, and `AddDbContextFactory`.
- [AddDbContextFactory](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.entityframeworkservicecollectionextensions.adddbcontextfactory), in the EF Core API reference, gives the factory's default singleton lifetime and notes that it also registers the context type as a scoped service.
- [Tracking vs. No-Tracking Queries](https://learn.microsoft.com/en-us/ef/core/querying/tracking), in the EF Core documentation, explains why a tracking query returns an already-tracked instance without refreshing its values.
- [HostBuilder enables ValidateOnBuild/ValidateScopes in development environment](https://learn.microsoft.com/en-us/dotnet/core/compatibility/aspnet-core/9.0/hostbuilder-validation), a .NET 9 breaking-change note, describes when the generic host enables both validation options.
- [Captive Dependency](https://blog.ploeh.dk/2014/06/02/captive-dependency/), by Mark Seemann, names and explains the misconfiguration this article opens with.

---

## The Short Answer

`AddTransient`, `AddScoped`, and `AddSingleton` decide who shares an instance, how long the container keeps it and what stays reachable through it, how widely concurrent requests reach it, and who disposes it. Make those decisions on purpose.

Give each service the lifetime of the state it owns: transient for stateless helpers, scoped for a request's unit of work such as a `DbContext`, and singleton only for state the whole application instance shares and can use concurrently. Never let a singleton hold a scoped service, and let a longer-lived service keep a transient only when that instance is safe to keep and share. Let hosted services create a scope for each unit of work, let the container dispose what it creates, and use a context factory when work genuinely needs its own context. Build the real registrations with scope validation in a test, check the identities your design depends on, and remember that no lifetime makes work durable, authorized, correctly cached, or coordinated across instances.

---

## Appendix: Supporting Types and Test Helpers

Each type is declared once. The article body shows `ShopServiceRegistration`, `CartSummaryMapper`, `CheckoutService`, `ExchangeRateCache`, `RateSnapshot`, `ExchangeRateRefresher`, and `ExchangeRateLoader`; this appendix adds the rest. Application types belong in the `Shop` namespace in the application project, and test helpers in the test project. The code relies on the SDK's implicit usings (`ImplicitUsings` enabled) for `System`, `System.Collections.Generic`, `System.Linq`, and `System.Threading.Tasks`, and in the application project, the ASP.NET Core and `Microsoft.Extensions` namespaces the Web SDK adds.

### Application types

```csharp
using Microsoft.EntityFrameworkCore;

namespace Shop;

public sealed class ShopDbContext(DbContextOptions<ShopDbContext> options) : DbContext(options)
{
    public DbSet<Cart> Carts => Set<Cart>();

    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExchangeRate>().HasKey(r => r.Currency);
        modelBuilder.Entity<ExchangeRate>().Property(r => r.Currency).HasMaxLength(3);   // ISO 4217 code, stored upper case
        modelBuilder.Entity<ExchangeRate>().Property(r => r.UnitsPerUsd).HasPrecision(18, 6);

        modelBuilder.Entity<Cart>().HasMany(c => c.Lines).WithOne().HasForeignKey(l => l.CartId);
        modelBuilder.Entity<CartLine>().Property(l => l.Sku).HasMaxLength(64);
        modelBuilder.Entity<CartLine>().Property(l => l.UnitPriceUsd).HasPrecision(18, 2);
    }
}

public sealed class ExchangeRate
{
    public required string Currency { get; set; }

    public decimal UnitsPerUsd { get; set; }
}

public sealed class Cart
{
    public Guid Id { get; set; }

    public List<CartLine> Lines { get; set; } = [];
}

public sealed class CartLine
{
    public int Id { get; set; }

    public Guid CartId { get; set; }

    public required string Sku { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPriceUsd { get; set; }
}

public sealed record CartSummary(Guid CartId, string Currency, decimal Total, int LineCount);

public enum CartQuoteStatus
{
    Priced,
    CartNotFound,
    RatesNotLoaded,
    UnsupportedCurrency,
}

public sealed record CartQuote(CartQuoteStatus Status, CartSummary? Summary = null);

public interface IExchangeRateLoader
{
    Task<RateSnapshot> LoadAsync(CancellationToken cancellationToken);
}
```

The endpoint, in `Program.cs`, resolves `CheckoutService` from the request's scope and turns each quote outcome into a response:

```csharp
using Microsoft.EntityFrameworkCore;
using Shop;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddShop(options => options.UseSqlServer(builder.Configuration.GetConnectionString("Shop")));

var app = builder.Build();

app.MapGet("/carts/{cartId:guid}", async (
    Guid cartId, string? currency, CheckoutService checkout, HttpResponse response, CancellationToken cancellationToken) =>
{
    CartQuote quote = await checkout.QuoteAsync(cartId, string.IsNullOrWhiteSpace(currency) ? "USD" : currency, cancellationToken);

    if (quote.Status == CartQuoteStatus.RatesNotLoaded)
    {
        response.Headers.RetryAfter = "60";
    }

    return quote.Status switch
    {
        CartQuoteStatus.Priced => Results.Ok(quote.Summary),
        CartQuoteStatus.CartNotFound => Results.NotFound(),
        CartQuoteStatus.UnsupportedCurrency => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Unsupported currency."),
        CartQuoteStatus.RatesNotLoaded => Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Exchange rates are not available yet."),
        _ => throw new InvalidOperationException($"Unhandled quote status {quote.Status}."),   // a new status must be mapped here
    };
});

app.Run();
```

`CaptiveExchangeRateCache`, used by the validation test, is the opening example's cache, renamed so that it can sit beside the repaired `ExchangeRateCache`:

```csharp
public sealed class CaptiveExchangeRateCache(ShopDbContext db)
{
    private readonly ConcurrentDictionary<string, CachedRate> _rates = new(StringComparer.Ordinal);

    public async Task<decimal> GetUnitsPerUsdAsync(string currency, CancellationToken cancellationToken)
    {
        if (_rates.TryGetValue(currency, out CachedRate? cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.UnitsPerUsd;
        }

        ExchangeRate row = await db.ExchangeRates.SingleAsync(r => r.Currency == currency, cancellationToken);
        _rates[currency] = new CachedRate(row.UnitsPerUsd, DateTimeOffset.UtcNow.AddMinutes(15));
        return row.UnitsPerUsd;
    }

    private sealed record CachedRate(decimal UnitsPerUsd, DateTimeOffset ExpiresAt);
}
```

The opening cache's `SingleAsync` throws for a currency with no row, and its case-sensitive keys are not normalized; it is kept as it shipped and is not a model to copy. The repaired code treats a missing rate consistently: `CheckoutService` reports `UnsupportedCurrency`, and `LoadingExchangeRateCache` returns `null`.

The application project references `Microsoft.EntityFrameworkCore.SqlServer`, or the provider for your database, and exposes its internal members to the test project:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="Shop.Tests" />
</ItemGroup>
```

`RateSnapshot`, `ExchangeRateLoader`, and the test helpers need `using System.Collections.Frozen;`, and `CaptiveExchangeRateCache` and `LoadingExchangeRateCache` need `using System.Collections.Concurrent;`.

### Test helpers

```csharp
// Members of LifetimeTests.
private static RateSnapshot Snapshot(params (string Currency, decimal UnitsPerUsd)[] rates) =>
    new(rates.ToFrozenDictionary(r => r.Currency, r => r.UnitsPerUsd, StringComparer.Ordinal), DateTimeOffset.UtcNow);
```

```csharp
// Shared by every RecordingLoader one test's scopes create. Thread-safe, so a test may refresh concurrently.
public sealed class LoaderLog
{
    private readonly List<RecordingLoader> _created = [];
    private readonly object _gate = new();

    public ConcurrentQueue<Func<RateSnapshot>> Results { get; } = new();

    public IReadOnlyList<RecordingLoader> Created
    {
        get { lock (_gate) { return [.. _created]; } }
    }

    public void Record(RecordingLoader loader)
    {
        lock (_gate) { _created.Add(loader); }
    }
}

// Registered as scoped in place of ExchangeRateLoader. Records its own creation and disposal.
public sealed class RecordingLoader : IExchangeRateLoader, IDisposable
{
    private readonly LoaderLog _log;

    public RecordingLoader(LoaderLog log)
    {
        _log = log;
        _log.Record(this);
    }

    private volatile bool _disposed;

    public bool Disposed => _disposed;

    public async Task<RateSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        return _log.Results.TryDequeue(out Func<RateSnapshot>? next)
            ? next()
            : throw new InvalidOperationException("The test queued no result for this load.");
    }

    public void Dispose() => _disposed = true;
}
```

The test project, `Shop.Tests`, references the application project, `Microsoft.EntityFrameworkCore.InMemory`, `Microsoft.NET.Test.Sdk`, `xunit.v3`, and `xunit.runner.visualstudio`, and its test file uses these namespaces:

```csharp
using System.Collections.Concurrent;
using System.Collections.Frozen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shop;
using Xunit;
```
