---
description: A new HttpClient and handler per request wastes connections, and a static client with defaults can keep stale DNS. Reuse handlers, set lifetimes.
title: Stop Creating an HttpClient for Every Request
author: Christopher D. Cavell
published: "2026-11-20"
summary: Creating and disposing an HttpClient that owns its own handler inside every request creates and destroys a connection pool each time, which repeats connection setup instead of reusing connections and can exhaust local ports under load. Replacing it with one static client restores reuse, but with default settings a busy connection is kept, together with the address DNS returned when it opened, for as long as it stays in use. Use either a long-lived client whose handler sets an intentional PooledConnectionLifetime, or short-lived clients from IHttpClientFactory, which pools handlers and replaces them on a schedule. Put request-specific headers on the request rather than on the shared client, keep timeouts, caller cancellation, retries, and authorization as separate decisions, review cookie handling when handlers are pooled, and give genuinely different proxy, certificate, cookie, or trust configurations their own clients.
feed: true
x_post: A new HttpClient and handler per request wastes connections. A default static client can keep stale DNS. Reuse handlers; set lifetimes.
x_hashtags:
  - DotNet
  - AspNetCore
x_question: "Which HttpClient mistake did you meet first?"
---

# Stop Creating an HttpClient for Every Request

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** Comfortable reading C# and ASP.NET Core minimal API code, and registering services with `builder.Services`. Connection pooling, DNS, `IHttpClientFactory`, and typed clients are introduced in the article. No AsiBackbone package or prior Learning material is required.

**Sample scope:** The C# targets .NET 8 or later, where `HttpClient` uses `SocketsHttpHandler`, with `IHttpClientFactory` from `Microsoft.Extensions.Http`, which ASP.NET Core includes. Behavior that changed in .NET 9 is called out where it matters. The tests use xUnit v3 and a fake `HttpMessageHandler`; they open no sockets and make no assertions about elapsed time, and one of them lets a real `HttpClient.Timeout` expire. Supporting types are in the [appendix](#appendix-supporting-types-and-test-helpers). The endpoint is a focused sample: it omits the authorization a real endpoint needs before it reveals a store's stock. Every lifetime and timeout value in the code is illustrative; choose yours from how your dependency's addresses change and how your deployments drain traffic. Framework behavior is described from Microsoft's documentation on the verification date below; check it for your version, and for .NET Framework, where `SocketsHttpHandler` is not available and the guidance differs. The storefront, its inventory service, its incident, and its timings are a hypothetical teaching scenario, not a documented production incident. Original prose is licensed under [Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/), and original code examples are additionally offered under the [MIT License](https://opensource.org/license/mit), as described in the repository's [licensing terms](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md). Third-party material, where identified, remains under its original license. External sources were last verified on November 20, 2026.

**What this article covers:** why creating and disposing a separately owned client and handler in every request wastes connections and can exhaust ephemeral ports, and why one static client with default settings is only half a repair; the separate lifetimes of a client, its handler, its connections, the DNS answers behind them, and a request; the two supported strategies, a long-lived client with an intentional connection lifetime and short-lived clients from `IHttpClientFactory`, and when each is proportionate; named and typed clients, and using them from long-lived services; request-specific headers; timeout, cancellation, and resilience as separate decisions; cookies under pooled handlers; when one dependency needs more than one client; tests that use a fake handler; and a [decision guide](#choosing-a-strategy).

**In plain terms:** a delivery company does not buy a van for every parcel and scrap it at the door. It also does not keep one driver on a route memorized years ago after the warehouse has moved. It keeps a fleet, retires each van on a schedule, and gives every new driver the current address. And the label on each parcel says where that parcel goes; nobody writes it on the side of the van.

Consider a hypothetical storefront. Product pages show whether an item is in stock at the shopper's chosen store, and the storefront asks an internal inventory API for that answer. The inventory API is multi-store: it requires the store's identifier in an `X-Store-Id` header and accepts a correlation identifier in `X-Correlation-ID`. It runs behind a DNS name, and the inventory team deploys by bringing up a new cluster, pointing the name at it, and draining the old cluster for thirty minutes before shutting it down.

The first version of the availability endpoint created a client for each request and disposed it when the request finished, because a disposable object seemed to belong to the operation that created it:

```csharp
// Anti-pattern: a new client, handler, and connection pool for every request.
app.MapGet("/stores/{storeId}/products/{sku}/availability",
    async (string storeId, string sku, CancellationToken cancellationToken) =>
    {
        using var client = new HttpClient { BaseAddress = new Uri("https://inventory.internal.example/v1/") };
        client.DefaultRequestHeaders.Add("X-Store-Id", storeId);

        StockLevel? stock = await client.GetFromJsonAsync<StockLevel>(
            $"stock/{Uri.EscapeDataString(sku)}", cancellationToken);

        return Results.Ok(new { sku, InStock = stock?.Available > 0 });
    });
```

It worked in every test. During the first evening of a seasonal sale, product-page traffic rose sharply, and these selected lines from an illustrative log show what followed:

```text
19:02:11.402  web-2  GET /stores/0042/products/SKU-1/availability → 200  inventory call 212 ms
19:14:37.918  web-2  GET /stores/0042/products/SKU-1/availability → 500  HttpRequestException: Cannot assign requested address (inventory.internal.example:443)
19:14:38.051  web-1  GET /stores/0017/products/SKU-9/availability → 500  HttpRequestException: Cannot assign requested address (inventory.internal.example:443)
19:20:02.330  web-2  ss -tan state time-wait | wc -l → 27914
```

Because each request started with a new, empty connection pool, every product page opened a new TCP connection and negotiated a new TLS session with the inventory API, and every disposal closed that connection. The inventory API was healthy. Thousands of local sockets in `TIME-WAIT`, together with connections failing with "Cannot assign requested address", strongly suggest ephemeral port exhaustion, and checking the hosts' available ephemeral ports confirmed it: the storefront's hosts had run out of local ports to open connections from.

A developer replaced the per-request client with one static client and moved the code into a helper. The `DefaultRequestHeaders` line came along, adjusted so that it would not add a second value:

```csharp
// Anti-pattern: one shared client, configured per request.
public static class Inventory
{
    private static readonly HttpClient Client = new() { BaseAddress = new Uri("https://inventory.internal.example/v1/") };

    public static Task<StockLevel?> GetStockAsync(string storeId, string sku, CancellationToken cancellationToken)
    {
        Client.DefaultRequestHeaders.Remove("X-Store-Id");
        Client.DefaultRequestHeaders.Add("X-Store-Id", storeId);
        return Client.GetFromJsonAsync<StockLevel>($"stock/{Uri.EscapeDataString(sku)}", cancellationToken);
    }
}
```

Port exhaustion stopped, latency fell, and the change shipped. Two weeks later, two new problems appeared. Under load, a shopper who chose store 0042 was occasionally shown the stock of a different store, and a few requests failed with exceptions thrown from inside the header collection. And when the inventory team deployed at 14:00, the storefront kept sending most of its inventory calls to the old cluster, which had stopped receiving stock movements at the switch, until that cluster shut down at 14:30 and in-flight calls failed. For half an hour, product pages answered from data that no longer moved.

The first version had too little reuse and the second had the wrong kind. Both confused the lifetime of the `HttpClient` object with the lifetimes underneath it.

> **An `HttpClient` is a front object over a message handler, and the handler owns a pool of connections, each bound to the address DNS returned when it opened. Reuse handlers so connections are reused, give connections an intentional lifetime so address changes are picked up, and put everything that belongs to one request on that request.**

The short version: never create a client and handler per request; choose either one long-lived client with an intentional `PooledConnectionLifetime` or short-lived clients from `IHttpClientFactory`; and never change a shared client while requests use it. The [decision guide](#choosing-a-strategy) near the end turns this into seven questions.

---

## Five Lifetimes Behind One Constructor

`new HttpClient()` looks like one object. It creates a client and, beneath it, a handler that the client owns, and the code that sends requests through them depends on three more things with lifetimes of their own:

| Thing | What it is | How long it should live |
| --- | --- | --- |
| **`HttpClient`** | The front object: base address, default headers, timeout, and thread-safe methods such as `SendAsync` and `GetAsync` | As long as its configuration stays the same. One per application instance, or one per short unit of work when a factory supplies it |
| **`HttpMessageHandler`** | The transport. In .NET Core 2.1 and later the default transport is `SocketsHttpHandler`, which owns a pool of connections. Its connection settings cannot be changed after the first request | Long enough to reuse its connections across many requests |
| **A pooled connection** | For HTTP/1.1 and HTTP/2, one TCP connection, usually with TLS, to one server address. HTTP/3 uses QUIC instead, but the lifetime reasoning is the same | Until it has been idle too long, the server closes it, or it reaches an age you choose |
| **The DNS answer** | The address the connection was opened to | An established connection keeps the address it connected to. `HttpClient` resolves the name when it creates a connection, through whatever resolver and caches the host has, and does not track the DNS record's time to live |
| **`HttpRequestMessage`** | One request: method, URI, headers, content, and options | One send |

Microsoft's [guidelines for using `HttpClient`](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines) describe the same structure: the connection pool is linked to the underlying handler, and disposing a client disposes the pool only when the client owns and disposes that handler.

Every failure in this article comes from treating two or more of these lifetimes as one. Keep them apart and the rest of the guidance follows.

How many connections a pool holds depends on the protocol. An HTTP/1.1 connection carries one request at a time, so concurrent requests open more connections, while HTTP/2 multiplexes many requests over one connection. That changes the connection counts a load test shows, not the lifetime and DNS reasoning that follows. It does make retirement less exact: a connection past its lifetime takes no new requests, but a long request already running on it, which on HTTP/2 may share the connection with others, keeps it open until that request finishes. And reuse is not unlimited concurrency. `SocketsHttpHandler` does not limit HTTP/1.1 connections per server by default, but if you set `MaxConnectionsPerServer`, requests beyond that limit wait for a free connection.

### What a per-request client throws away

`new HttpClient()` without arguments creates a handler that the client owns, an `HttpClientHandler` that is backed by `SocketsHttpHandler` in .NET Core 2.1 and later, so disposing the client disposes the handler and closes its connections. A per-request client therefore builds and destroys a connection pool for every request. For HTTPS calls like the storefront's, every request opens a connection and repeats the TCP and TLS handshakes that a pool exists to avoid.

The closed connections do not disappear immediately. TCP keeps the side that closes a connection in the [`TIME-WAIT` state](https://www.rfc-editor.org/rfc/rfc9293.html#section-3.3.2) for a while, and during that time the same connection identity cannot be used again. A host that opens and closes connections to one server faster than those states end can use up its ephemeral ports. When that happens, the operating system cannot open new connections, and the client throws `SocketException`, which `HttpClient` reports inside an `HttpRequestException`. The [`HttpClient` class remarks](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-net-http-httpclient) warn about exactly this.

Removing the `using` does not help. Each undisposed client still owns a separate pool, so connections are not reused across calls. Removing disposal does not repair that design: cleanup becomes nondeterministic, and open connections can accumulate as well.

The disposal was never the defect. Creating a new handler for each request was. A client that does not own its handler, such as `new HttpClient(sharedHandler, disposeHandler: false)` or a client from `IHttpClientFactory`, can be created and disposed per unit of work without touching the shared pool.

### Why one static client was only half a repair

A single static client fixes reuse: one handler, one pool, connections that serve request after request. It does not, by itself, decide how long a connection may live.

[`SocketsHttpHandler.PooledConnectionLifetime`](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.pooledconnectionlifetime) defaults to `InfiniteTimeSpan`. [`PooledConnectionIdleTimeout`](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.pooledconnectionidletimeout), one minute by default in .NET 6 and later (two minutes in earlier .NET Core versions and .NET 5), closes connections that sit unused. A busy storefront's connections are rarely unused for a whole minute, so they seldom close on their own, and each keeps sending requests to the address it opened to. The storefront's static client resolved the inventory name when those connections were established, before the deployment, and had no reason to resolve it again until the old cluster closed them.

Whether stale addresses ever become visible depends on the deployment. Behind a load balancer whose address never changes, nothing may happen at all. A deployment that moves a DNS name to new addresses while the old ones keep answering, as the inventory team's does, exposes it immediately.

The static client also made its configuration shared. `DefaultRequestHeaders` is sent with every request from that client, from every thread. Changing it while other requests are being sent lets a concurrent request pick up another caller's value, and the header collection, which is not designed for concurrent modification, can throw. That is how one shopper's store identifier went out with another shopper's request.

---

## Two Supported Lifetime Strategies

Microsoft's guidance names two valid strategies for .NET Core and .NET 5 or later: use **long-lived clients and set `PooledConnectionLifetime`**, or use **short-lived clients created by `IHttpClientFactory`**. Neither is the only correct answer, and both solve port exhaustion and stale addresses when they are configured on purpose.

### Strategy 1: one long-lived client with an intentional connection lifetime

The application intentionally keeps one handler and one client for the inventory API for the life of the application instance:

```csharp
// Always ends in "/", even when configuration omits it. See the appendix.
Uri inventoryAddress = InventoryAddress.FromConfiguration(builder.Configuration["Inventory:BaseAddress"]);

var inventoryHandler = new SocketsHttpHandler
{
    // Illustrative. A connection older than this is closed once its current request completes,
    // and the next connection resolves DNS again. Choose the value from how often the inventory
    // API's addresses change and how long a drained cluster keeps answering.
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),

    // The storefront keeps no cookies from the inventory API. See "Cookies and Pooled Handlers".
    UseCookies = false,
};

var inventoryHttp = new HttpClient(inventoryHandler)
{
    BaseAddress = inventoryAddress,
    Timeout = TimeSpan.FromSeconds(10),
};
inventoryHttp.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

// Handed to the container already built, so the container does not dispose it.
// In a typical ASP.NET Core host, the client and its handler deliberately live as long as the application.
builder.Services.AddSingleton(new InventoryClient(inventoryHttp));
```

With a two-minute connection lifetime, a connection stops being reused for new requests once it is two minutes old. Requests already running on it complete, new connections are created as needed, and each new connection resolves the inventory name again, so the new cluster's addresses become eligible for traffic. The [guidelines](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines#dns-behavior) describe this behavior. How quickly traffic actually moves also depends on how long requests run, on DNS caching between the application and the authoritative record, and on the network; the setting bounds connection reuse, not failover time. If you set a connection lifetime because deployments move DNS records, make sure telemetry records the address each connection actually reached, so you can confirm that traffic moved; the [metrics described with the tests](#what-these-tests-deliberately-do-not-assert) carry it. The price is an occasional new handshake per connection, which is usually small next to a handshake on every request.

The trailing slash matters. A relative path such as `stock/SKU-1` replaces the last segment of a base address that does not end in `/`, so `https://inventory.internal.example/v1` would quietly send requests to `/stock/SKU-1` and drop `v1`. `InventoryAddress` adds the slash and rejects addresses with no host, a query, a fragment, credentials, or a scheme other than HTTP or HTTPS; `FromConfiguration` parses a configuration value, and `Normalize` applies the same rules to a `Uri` a caller already has. Tests check both.

Living as long as the process is a deliberate choice for an application that builds one host per process. If a process builds and disposes hosts repeatedly, as integration tests often do, each host leaves its client and connections behind. There, either use Strategy 2, whose factory owns and disposes its handlers, or move the client into a small owner that the container creates and therefore disposes, in place of the instance registration above:

```csharp
// Created by the container, so the container disposes it, and with it the client and handler,
// when the host's service provider is disposed.
public sealed class InventoryConnection : IDisposable
{
    public InventoryConnection(Uri baseAddress)
    {
        Http = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            UseCookies = false,
        })
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(10),
        };
        Http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public HttpClient Http { get; }

    public void Dispose() => Http.Dispose();
}
```

```csharp
// Factory delegates, not instances: the container disposes only singletons it created.
builder.Services.AddSingleton(_ => new InventoryConnection(inventoryAddress));
builder.Services.AddSingleton(provider => new InventoryClient(provider.GetRequiredService<InventoryConnection>().Http));
```

[Your Dependency Injection Lifetime Is an Ownership Decision](dependency-injection-lifetime-is-ownership-decision.md) explains that ownership rule.

This strategy fits code without dependency injection, such as a console tool or a library's static helper, an application with a few stable client configurations, and a client that a singleton or hosted service must hold. Microsoft's [guidelines](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines#recommended-use) also note that a small number of long-lived clients is how an application uses several proxies or separate cookie containers, because each client then owns exactly the handler it should.

### Strategy 2: short-lived clients from `IHttpClientFactory`

The factory separates the two lifetimes the storefront confused. Every call to `CreateClient`, and every resolution of a typed client, returns a **new `HttpClient`**, which is cheap. The factory keeps a **pool of handlers**, one active handler per client name, reuses it for new clients until its handler lifetime expires, then starts handing out a new handler. Requests already running on the old handler complete normally, so rotation does not itself cause failures. The factory disposes an expired handler once no client still uses it, and disposing a factory-created client does not dispose the handler. [Microsoft's `IHttpClientFactory` guidance](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory#httpclient-lifetime-management) gives the default handler lifetime as two minutes.

The storefront registers its client through one extension method, so that the application and its tests use the same registration:

```csharp
public static class InventoryRegistration
{
    // Illustrative. The handler lifetime and the connection lifetime are kept equal on purpose, so a client
    // held longer than intended still replaces its connections on the same schedule. Change them together.
    private static readonly TimeSpan ConnectionLifetime = TimeSpan.FromMinutes(2);

    public static IHttpClientBuilder AddInventoryClient(this IServiceCollection services, Uri baseAddress)
    {
        // Checked here as well, so a caller that does not use FromConfiguration gets the same rules.
        Uri address = InventoryAddress.Normalize(baseAddress);

        return services
            .AddHttpClient<InventoryClient>(client =>
            {
                // Runs for every new HttpClient, before its first request.
                client.BaseAddress = address;
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            // Called for each new handler, so it must return a new instance every time.
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                UseCookies = false,
                PooledConnectionLifetime = ConnectionLifetime,
            })
            // Two minutes is also the factory's default; stating it makes the decision reviewable.
            .SetHandlerLifetime(ConnectionLifetime);
    }
}
```

```csharp
Uri inventoryAddress = InventoryAddress.FromConfiguration(builder.Configuration["Inventory:BaseAddress"]);
builder.Services.AddInventoryClient(inventoryAddress);
```

Choose one strategy for a given client. The two registrations above are alternatives, not layers: registering both would leave two registrations for `InventoryClient`, and the container would resolve whichever came last.

Since .NET 9, when a named or typed client does not configure its own primary handler, the factory's default primary handler is a `SocketsHttpHandler` whose `PooledConnectionLifetime` [matches the handler lifetime](https://learn.microsoft.com/en-us/dotnet/core/compatibility/networking/9.0/default-handler). That default limits the damage when a factory client is held too long. A registration that supplies its own primary handler, as this one does to turn off cookies, replaces that default, so it sets the connection lifetime itself.

A third arrangement combines the two: a factory registration whose primary handler sets `PooledConnectionLifetime`, with `SetHandlerLifetime(Timeout.InfiniteTimeSpan)` so that the factory never rotates the handler. That gives central, dependency-injected configuration with a single long-lived handler, and it makes clients from that registration suitable to hold, provided every handler in the pipeline is safe to share. Use it deliberately; it is Strategy 1 expressed through the factory.

### Choosing between them

| Need | Long-lived client | `IHttpClientFactory` |
| --- | --- | --- |
| **A few stable configurations** | Valid, with an intentional `PooledConnectionLifetime` | Valid |
| **Central named or typed configuration, logging, and handlers from dependency injection** | Manual: build the handler pipeline and registration yourself | Strong fit |
| **Code that runs per request or per job** | Reuse the client; never construct one per request | Create a client per unit of work; the handler stays pooled |
| **Use from a singleton or hosted service** | Inject the client directly | Inject `IHttpClientFactory` or create a scope per unit of work. Hold a typed client only from a registration designed to be long-lived |
| **Cookies** | Own the handler and its `CookieContainer` explicitly | Review pooling carefully: the container is shared through the handler and lost when the handler is replaced |
| **Code outside dependency injection** | Natural fit | Needs a service provider |
| **Replacing the transport in tests** | Construct the client with a test handler | Replace the primary handler in the registration |

---

## Named and Typed Clients, Without a Class for Every Call

The factory offers two common ways to describe a client.

A **named client** is a configuration registered under a string, and consumers ask the factory for it:

```csharp
builder.Services.AddHttpClient("inventory", client => client.BaseAddress = inventoryAddress);

// In a consumer that injected IHttpClientFactory:
HttpClient http = clients.CreateClient("inventory");
```

Named clients suit code that makes occasional, varied calls and does not need a dedicated class. Keep the set of names fixed. A name built from request data, such as one client per tenant identifier, creates handlers without bound, and [Microsoft warns](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory#named-clients) against it.

A **typed client** is a class that receives a configured `HttpClient` in its constructor. The storefront's typed client exposes the one operation the storefront needs, in the storefront's terms:

```csharp
public sealed class InventoryClient(HttpClient http)
{
    public async Task<StockLevel?> GetStockAsync(
        string storeId, string sku, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        // EscapeDataString leaves "." and ".." unchanged, and URI resolution would treat them as path
        // navigation, so the request would no longer address a SKU.
        if (sku is "." or "..")
        {
            throw new ArgumentException("The SKU cannot be a URI navigation segment.", nameof(sku));
        }

        // Relative to BaseAddress. The SKU comes from the caller, so it is escaped.
        using var request = new HttpRequestMessage(HttpMethod.Get, $"stock/{Uri.EscapeDataString(sku)}");

        // Values that belong to this request travel on this request. Headers.Add rejects values that
        // are not valid in a header, such as line breaks, so callers validate what they pass.
        request.Headers.Add(InventoryHeaders.StoreId, storeId);
        request.Headers.Add(InventoryHeaders.CorrelationId, correlationId);

        // ResponseContentRead, the default, stated because the timeout discussion below depends on it:
        // HttpClient.Timeout then also covers reading the body.
        using HttpResponseMessage response =
            await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);

        // The inventory API documents 404 as "this store does not carry this SKU".
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        // A malformed body, or one missing a required property, throws JsonException here. A body that
        // parses but does not describe the requested stock, such as another store's answer, is rejected
        // below rather than read as zero stock.
        StockLevel stock = await response.Content.ReadFromJsonAsync<StockLevel>(cancellationToken)
            ?? throw new InvalidDataException("The inventory API returned JSON null instead of a stock level.");

        if (stock.StoreId != storeId || stock.Sku != sku || stock.Available < 0)
        {
            throw new InvalidDataException(
                "The inventory API returned an invalid stock level or one that does not match the request.");
        }

        return stock;
    }
}
```

Typed clients give the inventory API one home: its base address, its headers, and the mapping from its status codes to the storefront's meaning. That does not mean every downstream call deserves its own class or interface. One typed client per downstream dependency, or per genuinely different configuration of it, with a method for each operation the application uses, is usually enough. The tests later in this article replace the transport under `InventoryClient` without an interface; add an interface when a consumer with its own logic needs a narrower seam, not to make HTTP fakeable.

`AddHttpClient<InventoryClient>` already registers `InventoryClient`. Do not register it again with `AddTransient` or `AddScoped`: the [troubleshooting guide](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory-troubleshooting) explains that a second registration silently replaces the factory's and injects an unconfigured client.

### Short-lived clients in long-lived services

The factory registers a typed client as **transient**, and the `HttpClient` inside it shares its lifetime. Factory-created clients are [meant to be short-lived](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory#avoid-typed-clients-in-singleton-services): once a client exists, the factory no longer controls it. Inject a typed client into a singleton, and that client and its handler are captured for the life of the application, beyond the factory's rotation, which is the [captive dependency](dependency-injection-lifetime-is-ownership-decision.md) problem in HTTP form. Scope validation does not report it, because the typed client is transient rather than scoped.

Whether the captured client still follows address changes depends on its primary handler. The .NET 9 default handler, and this article's registration, set `PooledConnectionLifetime`, so connections are still replaced. The handler pipeline, its delegating handlers, and whatever they depend on are still kept for the life of the singleton. Microsoft's troubleshooting guidance says not to inject typed clients into singletons, and its factory guidance allows a typed client in a singleton when the primary handler is a `SocketsHttpHandler` with a configured `PooledConnectionLifetime`. Treat avoidance as the default. The exception is a deliberate, reviewed registration, such as the infinite-handler-lifetime arrangement above, whose whole pipeline is safe to share.

A singleton or hosted service that calls the inventory API has three good options: hold a Strategy 1 client, inject `IHttpClientFactory` and create a client for each unit of work, or create a scope for each unit of work and resolve the typed client inside it. The storefront's low-stock monitor uses the last option:

```csharp
public sealed class LowStockMonitor(IServiceScopeFactory scopes, ILogger<LowStockMonitor> logger) : BackgroundService
{
    // Simplified sample: one SKU in one store.
    private const string WatchedStore = "0042";
    private const string WatchedSku = "SKU-1";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The first check runs one interval after startup, which suits this monitor. During shutdown,
        // cancellation propagates from WaitForNextTickAsync; that is expected, not a failed check.
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            // One scope per check. The typed client and its HttpClient live only for this check,
            // so each check uses the handler the factory currently hands out.
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            InventoryClient inventory = scope.ServiceProvider.GetRequiredService<InventoryClient>();

            try
            {
                StockLevel? stock = await inventory.GetStockAsync(
                    WatchedStore, WatchedSku, $"low-stock-{Guid.NewGuid():N}", stoppingToken);

                if (stock is { Available: < 5 })
                {
                    logger.LogWarning("Store {StoreId} has {Available} of {Sku} left.",
                        WatchedStore, stock.Available, WatchedSku);
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                // A failed check, including HttpClient.Timeout, is logged, and the next tick tries again.
                // Letting the exception escape would stop the host.
                logger.LogWarning(exception, "The low-stock check failed.");
            }
        }
    }
}
```

---

## Request-Specific Data Belongs on the Request

An `HttpClient`'s properties are configuration for every request sent through it. `BaseAddress` and `Timeout` enforce this: setting them after the client has sent its first request throws `InvalidOperationException`. `DefaultRequestHeaders` does not enforce it, which is why the storefront's static client compiled and failed only under concurrency. Set defaults while the client is being configured, and treat them as read-only afterward.

`InventoryClient` puts each value where it belongs:

| Value | Where it goes | Why |
| --- | --- | --- |
| `Accept`, a fixed `User-Agent`, an API version that never varies | `DefaultRequestHeaders`, in the registration's configuration callback or before the first request | Identical for every request through this client |
| The store, the tenant, a correlation identifier, an idempotency key, a caller's own bearer token | `HttpRequestMessage.Headers` | Belongs to one request, and two concurrent requests must not see each other's value |
| Data for your own handlers that the server must not receive, such as an operation name for logging | `HttpRequestMessage.Options` | Travels with the request without being sent |
| The application's own credential for the dependency, which rotates | A `DelegatingHandler` that reads the current token from a thread-safe singleton source and sets it on each request | Refreshed in one place, and the same for every caller |

Two cautions follow from handler pooling. First, a `DelegatingHandler` created by the factory lives as long as its pooled handler and runs in [a dependency-injection scope of its own](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory-troubleshooting), not in the request's scope. It must not cache anything taken from one caller, such as `HttpContext` data or a user's token; pass those on the request instead. Second, forwarding a caller's own token to another service is a trust decision about that token's audience, not a header placement detail. Decide it separately.

---

## Timeout, Cancellation, and Resilience Are Separate Decisions

The availability endpoint passes its request's cancellation token to the inventory call and maps the failures it can explain. It is a static method, so tests can call it directly:

```csharp
public static class AvailabilityEndpoint
{
    public static async Task<IResult> GetAsync(
        string storeId,
        string sku,
        InventoryClient inventory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        // Focused sample: a real endpoint also checks that the caller may see this store's stock.
        // Store identifiers are four ASCII digits in this sample; validate against your own format.
        if (storeId.Length != 4 || !storeId.All(char.IsAsciiDigit) || string.IsNullOrWhiteSpace(sku) || sku is "." or "..")
        {
            return Results.BadRequest();
        }

        try
        {
            StockLevel? stock = await inventory.GetStockAsync(storeId, sku, context.TraceIdentifier, cancellationToken);
            return stock is null
                ? Results.NotFound()
                : Results.Ok(new AvailabilityResponse(stock.Sku, stock.Available > 0, stock.AsOf));
        }
        catch (OperationCanceledException exception) when (
            exception.InnerException is TimeoutException && !cancellationToken.IsCancellationRequested)
        {
            // The shape HttpClient reports for its own Timeout, while the caller is still waiting.
            return Results.Problem(
                statusCode: StatusCodes.Status504GatewayTimeout,
                title: "The inventory service did not answer in time.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidDataException)
        {
            // Any other unsuccessful status, an unreachable service, or an unusable body is a failure
            // of the dependency, and availability is unknown.
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "The inventory service could not be reached or returned an error.");
        }
    }
}
```

```csharp
app.MapGet("/stores/{storeId}/products/{sku}/availability", AvailabilityEndpoint.GetAsync);
```

Each failure is answered as what it is. An unknown SKU is `404`, and an invalid store identifier is `400` before any call is made. An inventory failure is a gateway error, and availability is unknown; it is never reported as "out of stock". That includes a response the client cannot use: malformed JSON, JSON `null`, a missing property, a negative quantity, or a stock level for a different store or SKU. Those checks follow the inventory API's contract: every property is present, `Available` is zero or more, and `StoreId` and `Sku` echo the request exactly. The sample checks identity and quantity, not freshness; an application that depends on current stock defines an acceptable age for `AsOf` and handles older data separately. The sample maps `404` to not found and treats every other unsuccessful status as a dependency failure; if the inventory API can reject the storefront's input with `400`, map that case separately. A caller who has gone away gets nothing, because the cancellation propagates and ASP.NET Core handles the aborted request.

The storefront sends its own `TraceIdentifier` as the correlation identifier and ignores any value a browser sends, because it does not let callers choose identifiers that appear in its records. That relies on the identifier being generated by the server, as Kestrel's default is; middleware that sets it from request data would have to validate it. A service whose callers are trusted might forward theirs instead.

The timeout filter matches the shape that .NET 5 and later document for `HttpClient.Timeout`, a `TaskCanceledException` wrapping a `TimeoutException`, and also requires that the caller is still waiting. `HttpClient` reports that shape when its own timeout ended the call. A cancellation raised inside the pipeline that neither the caller nor the timeout requested propagates unchanged and is not reported as `504`, and a test below checks that. A resilience pipeline's own timeouts arrive as Polly's `TimeoutRejectedException` and are not caught here either; when you add one, update the filter alongside it.

The mechanisms behind those answers decide different things:

- **`HttpClient.Timeout`** is a ceiling for one call through the client, 100 seconds by default. With the default completion option, the call includes buffering the response body; with `HttpCompletionOption.ResponseHeadersRead` it ends when the headers arrive, and reading the body needs its own budget. The sample uses the default, so its 10-second timeout also covers buffering the body. The timeout surrounds everything the client's handler pipeline does for that call, including any retries a resilience handler performs inside it, so per-attempt timeouts must sit well below it or the client's ceiling ends the call before a retry can run. In .NET 5 and later it surfaces as a `TaskCanceledException` whose inner exception is a `TimeoutException`, as described in the [`HttpClient.Timeout` reference](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.timeout).
- **Caller cancellation** says the result is no longer wanted. Minimal APIs bind a `CancellationToken` parameter to the request's abort token. Passing it on lets abandoned work stop; it is not a timeout and should not be reported as one.
- **Connection settings** such as `SocketsHttpHandler.ConnectTimeout` bound one stage of the transport, such as opening a new connection.
- **Resilience policies**, such as the retries, per-attempt timeouts, and circuit breaker that `Microsoft.Extensions.Http.Resilience` adds, decide whether and when to send the same request again. Polly reports its own timeouts with `TimeoutRejectedException`. Resending is safe only for requests whose repetition is harmless, and that is a separate design question, covered in [Your Retry Logic May Be Replaying an Authorization Decision](retry-logic-may-replay-authorization-decision.md).
- **Authorization** decides whether the call may be made at all. None of the mechanisms above answers that question, and none of them changes when the client's lifetime strategy changes.

Choosing a client lifetime does not choose any of these. A long-lived client and a factory client can carry the same timeout, the same resilience pipeline, and the same authorization checks, and they should be reviewed as separate decisions.

---

## Cookies and Pooled Handlers

Cookies are stored by the handler, in the `CookieContainer` that `SocketsHttpHandler` and `HttpClientHandler` use when `UseCookies` is `true`, which is the default. That couples cookies to the handler's lifetime and sharing:

- With **`IHttpClientFactory`**, every client created for a name during a handler's lifetime shares that handler's container. A cookie set in response to one caller's request is sent with other callers' requests, and when the handler is replaced, every stored cookie is lost. Microsoft's guidance is direct: [if your app requires cookies, avoid `IHttpClientFactory`](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory).
- With a **long-lived client**, the container lives as long as the client, and every request through it shares it. That is correct only if every request through that client belongs to the same cookie conversation.

Many service-to-service clients want no cookies at all, yet still receive them, for example a load balancer's affinity cookie. Stored in a shared container, such a cookie can pin every caller's requests to one backend until the handler is replaced. Both of the storefront's registrations therefore set `UseCookies = false`. If a dependency genuinely requires a cookie value, send it explicitly on the request, where its owner is visible; with `UseCookies = false`, the `Cookie` header a request carries is the one you set.

When an integration really is a cookie-based session, such as a legacy system that authenticates with a session cookie, give each session boundary its own long-lived client and handler, created and owned explicitly, with an intentional `PooledConnectionLifetime`. Never share that container between users.

One .NET 9 change matters here. Code that turned off cookies by casting the factory's default primary handler to `HttpClientHandler` now throws `InvalidCastException`, because the default became `SocketsHttpHandler`. Configure the primary handler explicitly, as the registration above does.

---

## When One Client Is Not Enough

Reuse is about connections to one dependency under one configuration. It is not a reason to send unrelated traffic through one handler. A handler's connection settings cannot change after its first request, and a handler's settings apply to everything sent through it, so give a dependency its own client when any of these genuinely differ:

- **Proxy.** One dependency must go through an egress proxy and another must not.
- **Client certificate.** A mutual-TLS dependency needs the application's certificate; other dependencies must not receive it.
- **Server-certificate trust.** A dependency uses a private certificate authority or a pinned certificate. Never relax certificate validation on a shared handler to make one dependency work.
- **Cookies.** Two conversations must not see each other's cookies.
- **Connection limits or protocol.** One dependency needs `MaxConnectionsPerServer` or a specific HTTP version policy that should not constrain the others.

A small, bounded number of clients is normal; Microsoft's [guidelines](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines#recommended-use) say that what matters is not creating and disposing them per request. A number of clients that grows with input, such as one per tenant, is a different design problem: a bounded cache of handlers with explicit eviction, which is outside this article.

---

## Testing Without a Network

The handler is the seam. A test constructs `InventoryClient` with an `HttpClient` over a fake handler, so it exercises the real request construction and response mapping without sockets or DNS, and without elapsed-time assertions; one test deliberately lets a real `HttpClient.Timeout` expire. The test classes' usings are listed in the appendix. The fake handler, `StubInventoryHandler`, records every request it receives and answers with a function the test chooses; it is in the [appendix](#test-helpers).

### Request-specific values travel on the request

```csharp
public sealed class InventoryClientTests
{
    private static readonly Uri BaseAddress = new("https://inventory.test/v1/");

    [Fact]
    public async Task Store_and_correlation_travel_on_the_request_not_the_client()
    {
        var handler = new StubInventoryHandler(StubInventoryHandler.EchoStock(available: 3));
        using var http = new HttpClient(handler) { BaseAddress = BaseAddress };
        var inventory = new InventoryClient(http);

        await inventory.GetStockAsync("0042", "SKU-1", "corr-1", TestContext.Current.CancellationToken);

        RecordedRequest sent = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("https://inventory.test/v1/stock/SKU-1"), sent.Uri);
        Assert.Equal("0042", sent.StoreId);
        Assert.Equal("corr-1", sent.CorrelationId);

        // The deterministic guard: moving either value onto the shared client fails here.
        Assert.False(http.DefaultRequestHeaders.Contains(InventoryHeaders.StoreId));
        Assert.False(http.DefaultRequestHeaders.Contains(InventoryHeaders.CorrelationId));
    }
```

### Concurrent calls keep their own store

```csharp
    [Fact]
    public async Task Concurrent_calls_each_keep_their_own_store()
    {
        var handler = new StubInventoryHandler(StubInventoryHandler.EchoStock(available: 1));
        using var http = new HttpClient(handler) { BaseAddress = BaseAddress };
        var inventory = new InventoryClient(http);
        string[] stores = [.. Enumerable.Range(1, 50).Select(static i => i.ToString("D4"))];

        StockLevel?[] results = await Task.WhenAll(stores.Select(store =>
            inventory.GetStockAsync(store, "SKU-1", $"corr-{store}", TestContext.Current.CancellationToken)));

        Assert.Equal(stores, results.Select(static result => result?.StoreId));
    }
```

The fake handler answers with the store each request carried, so a crossed header would return the wrong store. A passing run cannot prove that no race exists; this test is a regression guard, and the previous test is the one that fails deterministically.

### An unknown SKU is not zero stock, and a navigation segment is not a SKU

```csharp
    [Fact]
    public async Task An_unknown_sku_is_null_not_zero_stock()
    {
        var handler = new StubInventoryHandler(
            static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var http = new HttpClient(handler) { BaseAddress = BaseAddress };
        var inventory = new InventoryClient(http);

        StockLevel? stock = await inventory.GetStockAsync("0042", "NO-SUCH-SKU", "corr-1", TestContext.Current.CancellationToken);

        Assert.Null(stock);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public async Task A_navigation_segment_is_not_sent_as_a_sku(string sku)
    {
        var handler = new StubInventoryHandler(StubInventoryHandler.EchoStock(available: 1));
        using var http = new HttpClient(handler) { BaseAddress = BaseAddress };
        var inventory = new InventoryClient(http);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            inventory.GetStockAsync("0042", sku, "corr-1", TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }
```

### Caller cancellation reaches the transport

```csharp
    [Fact]
    public async Task Caller_cancellation_reaches_the_transport()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubInventoryHandler(StubInventoryHandler.WaitForCancellation(entered));
        using var http = new HttpClient(handler) { BaseAddress = BaseAddress };
        var inventory = new InventoryClient(http);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        Task<StockLevel?> call = inventory.GetStockAsync("0042", "SKU-1", "corr-1", caller.Token);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);   // the request is inside the transport
        await caller.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    }
}
```

The test waits for a signal that the request reached the handler, not for a delay, so it does not depend on how fast the machine is, and the wait ends with the test's own cancellation if the signal never comes.

### The endpoint reports each failure as what it is

```csharp
public sealed class AvailabilityEndpointTests
{
    private static async Task<IResult> CallAsync(
        StubInventoryHandler handler, string storeId, CancellationToken cancellationToken, TimeSpan? clientTimeout = null)
    {
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://inventory.test/v1/"),
            Timeout = clientTimeout ?? TimeSpan.FromSeconds(100),   // HttpClient's default
        };
        return await AvailabilityEndpoint.GetAsync(
            storeId, "SKU-1", new InventoryClient(http), new DefaultHttpContext(), cancellationToken);
    }

    private static int? StatusOf(IResult result) => Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode;

    [Fact]
    public async Task An_invalid_store_is_rejected_before_any_request()
    {
        var handler = new StubInventoryHandler(StubInventoryHandler.EchoStock(available: 1));

        IResult result = await CallAsync(handler, "00\r\n42", TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(result));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_dependency_failure_is_a_gateway_error_not_out_of_stock()
    {
        var handler = new StubInventoryHandler(
            static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        IResult result = await CallAsync(handler, "0042", TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status502BadGateway, StatusOf(result));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"storeId":"0042","sku":"SKU-1","asOf":"2026-11-20T00:00:00Z"}""")]
    [InlineData("""{"storeId":"0042","sku":"SKU-1","available":3}""")]
    [InlineData("""{"storeId":"0042","sku":"SKU-1","available":-1,"asOf":"2026-11-20T00:00:00Z"}""")]
    [InlineData("""{"storeId":"0017","sku":"SKU-1","available":3,"asOf":"2026-11-20T00:00:00Z"}""")]
    [InlineData("""{"storeId":"0042","sku":"SKU-2","available":3,"asOf":"2026-11-20T00:00:00Z"}""")]
    public async Task An_unusable_body_is_a_gateway_error_not_out_of_stock(string body)
    {
        var handler = new StubInventoryHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }));

        IResult result = await CallAsync(handler, "0042", TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status502BadGateway, StatusOf(result));
    }

    [Fact]
    public async Task The_client_timeout_is_a_gateway_timeout()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubInventoryHandler(StubInventoryHandler.WaitForCancellation(entered));

        // The fake handler never produces a response, so HttpClient.Timeout cancels the call.
        // This checks how the timeout is classified, not how long it took.
        IResult result = await CallAsync(
            handler, "0042", TestContext.Current.CancellationToken, clientTimeout: TimeSpan.FromMilliseconds(100));

        Assert.Equal(StatusCodes.Status504GatewayTimeout, StatusOf(result));
    }

    [Fact]
    public async Task Caller_cancellation_is_not_reported_as_a_timeout()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubInventoryHandler(StubInventoryHandler.WaitForCancellation(entered));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        Task<IResult> call = CallAsync(handler, "0042", caller.Token);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        await caller.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    }

    [Fact]
    public async Task A_cancellation_nobody_requested_is_not_reported_as_a_timeout()
    {
        // Raised inside the pipeline without the caller or HttpClient.Timeout asking for it.
        var handler = new StubInventoryHandler(static (_, _) => throw new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CallAsync(handler, "0042", TestContext.Current.CancellationToken));
    }
}
```

### The registration the application uses

The unit tests above build the client by hand. One more test builds it from the same `AddInventoryClient` registration the application calls, replacing only the primary handler, so a mistake in the base address, the configuration callback, or the typed-client registration fails here:

```csharp
public sealed class InventoryRegistrationTests
{
    [Fact]
    public async Task The_registered_client_uses_the_configured_address_and_defaults()
    {
        var handler = new StubInventoryHandler(StubInventoryHandler.EchoStock(available: 5));
        var services = new ServiceCollection();
        services
            .AddInventoryClient(new Uri("https://inventory.test/v1"))   // no trailing slash
            // Added after the application's own primary handler, so it replaces that handler, and the
            // registration's client configuration still applies. Returning one shared instance is
            // acceptable only in a short test; production delegates return a new handler each time.
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        InventoryClient inventory = scope.ServiceProvider.GetRequiredService<InventoryClient>();

        await inventory.GetStockAsync("0042", "SKU-1", "corr-1", TestContext.Current.CancellationToken);

        RecordedRequest sent = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("https://inventory.test/v1/stock/SKU-1"), sent.Uri);
        Assert.Contains("application/json", sent.Accept);
        Assert.Equal("0042", sent.StoreId);
    }

    [Theory]
    [InlineData("https://inventory.test/v1")]
    [InlineData("https://inventory.test/v1/")]
    [InlineData("https://inventory.test/v1//")]
    public void The_base_address_always_ends_in_a_slash(string configured) =>
        Assert.Equal("https://inventory.test/v1/", InventoryAddress.FromConfiguration(configured).AbsoluteUri);

    [Theory]
    [InlineData(null)]
    [InlineData("inventory.test/v1")]
    [InlineData("https:///v1")]
    [InlineData("ftp://inventory.test/v1/")]
    [InlineData("https://user:secret@inventory.test/v1/")]
    [InlineData("https://inventory.test/v1/?region=us")]
    [InlineData("https://inventory.test/v1/#stock")]
    public void An_unusable_base_address_is_rejected(string? configured) =>
        Assert.Throws<InvalidOperationException>(() => InventoryAddress.FromConfiguration(configured));
}
```

### What these tests deliberately do not assert

They do not count connections or ports, wait for DNS changes, check that the factory rotated a handler, compare handler instances between resolutions, or pin the lifetime values. Those are framework behavior and depend on time, load, and the network, and tests that assert them are slow and flaky. Confirm them where they happen: in a load test, in a deployment rehearsal, and in production telemetry. Since .NET 8, `HttpClient` publishes [built-in metrics](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/built-in-metrics-system-net) such as `http.client.open_connections` and `http.client.connection.duration`, and DNS lookups publish `dns.lookup.duration`. They show pool size, how long connections lived, and DNS lookup activity and latency. The two connection metrics carry a `network.peer.address` attribute, the IP address the socket actually connected to, so keep that dimension when you investigate routing after a deployment, and correlate it with logs and deployment events. DNS lookup duration does not show which addresses a lookup returned.

If a consumer of `InventoryClient` has logic of its own, such as combining stock from several stores, test that logic against a narrow interface the consumer owns, such as one method returning a `StockLevel?`, rather than faking HTTP again.

---

## Choosing a Strategy

Seven questions settle most clients. Ask them in order:

| Question | What the answer means |
| --- | --- |
| **1. Does each call create its own handler and connection pool, for example through a parameterless `new HttpClient()`?** | Yes: stop. Reuse a handler through one of the two strategies below. Creating short-lived clients from `IHttpClientFactory` is expected and is not this problem |
| **2. Is dependency injection available, and do you want central configuration, logging, or handlers from it?** | Yes: `IHttpClientFactory` is a strong fit. No, or only one or two stable clients: a long-lived client is equally valid |
| **3. Will a long-lived component use the client: a singleton, a hosted service, or a static?** | Hold a long-lived client with an intentional `PooledConnectionLifetime`, or inject `IHttpClientFactory` or a scope factory and create a client per unit of work. Do not capture a typed client unless its registration is deliberately long-lived |
| **4. Can the dependency's addresses change while the application runs?** | Yes: bound connection age, with `PooledConnectionLifetime` or factory handler rotation, using a value chosen from how addresses change and how long old endpoints keep answering |
| **5. Does anything vary per request?** | Put it on `HttpRequestMessage`: headers for the server, `Options` for your handlers. Keep `DefaultRequestHeaders` for values that never vary |
| **6. Does the dependency set or need cookies?** | Usually turn them off and send any required value explicitly. For a real cookie session, give each session boundary its own explicitly owned client |
| **7. Does the configuration differ in proxy, certificates, trust, cookies, or connection limits?** | Use separate clients, a small and bounded number of them |

---

## What a Client Lifetime Does Not Give You

A lifetime strategy decides how connections are reused and replaced. It does not provide:

- **Retry safety.** Reusing a connection says nothing about whether a failed request may be sent again.
- **A time budget.** `HttpClient.Timeout`, caller cancellation, and resilience timeouts are separate settings that a lifetime strategy neither sets nor changes.
- **Authorization.** A shared client is not a shared permission. Whether this caller may ask about this store is decided before the call.
- **Correct data.** A connection to the right address can still return stale data, and an unknown outcome is still unknown. [A Payment API Timeout Leaves the Outcome Unknown](payment-api-timeout-unknown-outcome.md) covers what a timeout does and does not tell you.
- **Instant failover.** A connection lifetime limits how long a connection may be reused, and replacement connections resolve DNS again. That lets traffic follow address changes, but it does not interrupt requests in progress, bypass DNS caching, or guarantee when traffic moves, and it does not replace a deployment that drains old endpoints.

---

## Failure Modes

**1. A client and handler per request.** `using var client = new HttpClient()` inside an endpoint or service method. Every request pays for a new connection, and under sufficient load this can exhaust the host's ephemeral ports.

**2. The "fix" that removes `using`.** Undisposed per-request clients still own separate pools, so nothing is reused, and cleanup becomes nondeterministic.

**3. One static client with default settings.** Reuse is restored, but busy connections may stay in use indefinitely, and the client can keep calling addresses DNS no longer returns.

**4. A shared client changed per request.** `DefaultRequestHeaders`, or a property that throws only after the first request, is changed for one caller while other requests are being sent. A concurrent request can carry another caller's value, or the change can throw.

**5. A factory client kept by accident.** A typed client is injected into a singleton, or a client from `CreateClient` is kept in a field "to avoid creating it again". The client, its handler, and the handler pipeline's dependencies then live beyond the factory's rotation, and unless the primary handler retires connections, they keep stale addresses too.

**6. Caller data in a pooled handler.** A `DelegatingHandler` caches a user's token or `HttpContext` data, and a later caller's request carries it.

**7. Cookies in a pooled handler.** A cookie set for one caller is sent for others until the handler is replaced, then disappears.

**8. One client for different trust boundaries.** A handler configured for one dependency's proxy, certificate, or relaxed validation also carries traffic to others.

**9. Client names from input.** Named clients created per tenant or per URL create handlers without bound.

**10. Timeouts, cancellation, and failures reported as answers.** A timeout is reported as the caller leaving, a caller leaving is reported as a timeout, or an inventory outage is shown as "out of stock".

---

## A Short Review Checklist

1. Does any code create a new handler, directly or through a parameterless `new HttpClient()`, per request, per call, or per loop iteration? (Short-lived clients from `IHttpClientFactory` are expected.)
2. For each long-lived client, is `PooledConnectionLifetime` set on purpose, with a value someone can explain from the dependency's address changes?
3. For each factory registration that supplies its own primary handler, is the connection lifetime still decided, and is cookie handling stated?
4. Does any singleton, hosted service, or static hold a typed client or a factory-created `HttpClient`, and if so, is that registration deliberately long-lived?
5. Is every value that varies by request set on `HttpRequestMessage`, and is `DefaultRequestHeaders` written only during configuration?
6. Do `DelegatingHandler` instances avoid storing anything taken from one caller?
7. Are `HttpClient.Timeout`, caller cancellation, resilience policies, and authorization each decided and reviewed separately?
8. Does each distinct proxy, certificate, trust, or cookie boundary have its own client, and is the number of clients small and bounded?
9. Do tests check request construction and response mapping through a fake handler, and leave connection and DNS behavior to load testing and telemetry?

---

## Continue Deeper

A typed client in a singleton is one case of a broader rule: a service must not outlive what it depends on. [Your Dependency Injection Lifetime Is an Ownership Decision](dependency-injection-lifetime-is-ownership-decision.md) explains how transient, scoped, and singleton decide sharing, concurrency, and disposal, and how to give a hosted service a scope for each unit of work.

Once the client is reused correctly, the next question is what to do when a call fails. [Your Retry Logic May Be Replaying an Authorization Decision](retry-logic-may-replay-authorization-decision.md) separates a resilience handler's resends from the question of whether the operation is still permitted.

When the dependency is an authorization service, its outage must not become a decision. [Should Authorization Fail Open, Fail Closed, or Defer?](fail-open-fail-closed-or-defer.md) uses a typed policy client with a resilience pipeline and maps every transport failure to "cannot determine".

When a call has side effects, a timeout leaves its outcome unknown. [A Payment API Timeout Leaves the Outcome Unknown](payment-api-timeout-unknown-outcome.md) explains why, and the [Payment Attempt Recovery Implementation Reference](../../aspnetcore/payment-attempt-recovery-implementation-reference.md) shows a typed provider client that replaces `HttpClient.Timeout` with a per-call budget and reconciles before it resends.

### Related Work

- [Guidelines for using HttpClient](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines), in the .NET documentation, explains DNS behavior, pooled connections and port exhaustion, the two recommended lifetime strategies, cookie containers, and resilience with static clients.
- [IHttpClientFactory with .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory), in the .NET documentation, covers handler pooling and the default handler lifetime, named and typed clients, avoiding typed clients in singletons, cookies, and configuring the primary handler.
- [Common IHttpClientFactory usage issues](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory-troubleshooting), in the .NET documentation, covers handler scopes, stale DNS from captured clients, socket use, and typed-client registration mistakes.
- [HttpClientFactory uses SocketsHttpHandler as primary handler](https://learn.microsoft.com/en-us/dotnet/core/compatibility/networking/9.0/default-handler), a .NET 9 breaking-change note, describes the new default primary handler and its connection lifetime.
- [The `HttpClient` class remarks](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-net-http-httpclient), in the .NET documentation, cover instancing, thread-safe methods, connection pooling, DNS, and timeouts.
- [`SocketsHttpHandler.PooledConnectionLifetime`](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.pooledconnectionlifetime) and [`PooledConnectionIdleTimeout`](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.pooledconnectionidletimeout), in the .NET API reference, give the defaults this article relies on.
- [System.Net metrics](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/built-in-metrics-system-net), in the .NET documentation, lists the built-in HTTP connection and DNS metrics.
- [RFC 9293, section 3.3.2](https://www.rfc-editor.org/rfc/rfc9293.html#section-3.3.2), the TCP specification, defines the connection states, including `TIME-WAIT`.

---

## The Short Answer

An `HttpClient` is cheap; the handler under it is not. The handler owns the connection pool, and each connection keeps the address DNS returned when it opened. Creating a client and handler per request throws that pool away on every call and can exhaust the host's ports. One static client with default settings keeps the pool but has no schedule for retiring a busy connection.

Choose one of the two supported strategies on purpose: a long-lived client whose `SocketsHttpHandler` sets an intentional `PooledConnectionLifetime`, or short-lived clients from `IHttpClientFactory`, which pools handlers and replaces them on a schedule. Keep typed clients out of singletons unless their registration is deliberately long-lived, put everything that belongs to one request on the request, turn off cookies you do not mean to share, give different trust configurations different clients, and test request construction through a fake handler. Then decide timeouts, cancellation, retries, and authorization as the separate questions they are.

None of these failures is a defect in `HttpClient`. Each one appears when an application treats the lifetime of an object, a connection, a DNS answer, and a request as if they were the same lifetime.

---

## Appendix: Supporting Types and Test Helpers

Each type is declared once. The article body shows `InventoryClient`, `InventoryRegistration`, `InventoryConnection`, `LowStockMonitor`, and `AvailabilityEndpoint`; this appendix adds the rest. Application types belong in the `Storefront` namespace in the application project; the body's snippets omit their `namespace Storefront;` line. Test helpers belong in the test project. The code relies on the SDK's implicit usings (`ImplicitUsings` enabled) for `System`, `System.IO`, `System.Collections.Generic`, `System.Linq`, `System.Net.Http`, `System.Threading`, and `System.Threading.Tasks`, and in the application project, the ASP.NET Core, `System.Net.Http.Json`, and `Microsoft.Extensions` namespaces the Web SDK adds.

### Application types

Usings apply per file, so the application project declares the remaining namespaces once as global usings:

```csharp
// GlobalUsings.cs in the application project.
global using System.Net;                // HttpStatusCode
global using System.Net.Http.Headers;   // MediaTypeWithQualityHeaderValue
global using System.Text.Json;          // JsonException
```

```csharp
namespace Storefront;

// Validates the inventory API's base address and makes its path end in "/", so that relative paths
// such as "stock/SKU-1" append to it instead of replacing its last segment.
public static class InventoryAddress
{
    public static Uri FromConfiguration(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? address)
            ? Normalize(address)
            : throw new InvalidOperationException("Inventory:BaseAddress must be an absolute URI.");

    public static Uri Normalize(Uri address)
    {
        // The configuration value is an HTTP(S) base path, not a complete request URI, so it has a host
        // and no query, fragment, or embedded credentials. Production code may also require HTTPS.
        if (!address.IsAbsoluteUri
            || (address.Scheme != Uri.UriSchemeHttps && address.Scheme != Uri.UriSchemeHttp)
            || address.Host.Length == 0
            || address.UserInfo.Length > 0
            || address.Query.Length > 0
            || address.Fragment.Length > 0)
        {
            throw new InvalidOperationException(
                "The inventory base address must be an absolute HTTP or HTTPS URI with a host and without credentials, a query, or a fragment.");
        }

        // Without a query or fragment, AbsoluteUri ends with the path, so this leaves exactly one trailing "/".
        return new Uri(address.AbsoluteUri.TrimEnd('/') + "/");
    }
}

// The inventory API's header names, used by the client and by the tests.
public static class InventoryHeaders
{
    public const string StoreId = "X-Store-Id";
    public const string CorrelationId = "X-Correlation-ID";
}

// The inventory API's stock representation. Required properties make System.Text.Json reject a body
// that omits one, instead of filling in zero or a default date.
public sealed record StockLevel
{
    public required string StoreId { get; init; }
    public required string Sku { get; init; }
    public required int Available { get; init; }
    public required DateTimeOffset AsOf { get; init; }
}

// What the storefront's availability endpoint returns to browsers.
public sealed record AvailabilityResponse(string Sku, bool InStock, DateTimeOffset AsOf);
```

The application's `Program.cs` reads `Inventory:BaseAddress` through `InventoryAddress.FromConfiguration`, registers `InventoryClient` with exactly one of the two strategies, registers `LowStockMonitor` with `AddHostedService<LowStockMonitor>()`, and maps `AvailabilityEndpoint.GetAsync`.

### Test helpers

```csharp
// Records every request it receives and answers with the function the test supplies.
public sealed class StubInventoryHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public ConcurrentQueue<RecordedRequest> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Enqueue(RecordedRequest.From(request));
        return respond(request, cancellationToken);
    }

    // Answers 200 with a stock level for the store and SKU the request asked for.
    public static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> EchoStock(int available) =>
        async (request, _) =>
        {
            await Task.Yield();   // lets concurrent calls interleave
            string store = request.Headers.GetValues(InventoryHeaders.StoreId).Single();
            string sku = Uri.UnescapeDataString(request.RequestUri!.Segments[^1]);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new StockLevel { StoreId = store, Sku = sku, Available = available, AsOf = DateTimeOffset.UnixEpoch }),
            };
        };

    // Signals that the request arrived, then waits until it is canceled by the caller or by HttpClient.Timeout.
    public static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> WaitForCancellation(
        TaskCompletionSource entered) =>
        async (_, cancellationToken) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new UnreachableException();
        };
}

// A copy of what the handler saw, taken before the request message is disposed.
public sealed record RecordedRequest(
    HttpMethod Method, Uri? Uri, string? StoreId, string? CorrelationId, IReadOnlyList<string> Accept)
{
    public static RecordedRequest From(HttpRequestMessage request) => new(
        request.Method,
        request.RequestUri,
        HeaderValue(request, InventoryHeaders.StoreId),
        HeaderValue(request, InventoryHeaders.CorrelationId),
        [.. request.Headers.Accept.Select(static accept => accept.MediaType ?? string.Empty)]);

    private static string? HeaderValue(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.Single() : null;
}
```

The test project, `Storefront.Tests`, references the application project, `Microsoft.NET.Test.Sdk`, `xunit.v3`, and `xunit.runner.visualstudio`, and declares `<FrameworkReference Include="Microsoft.AspNetCore.App" />` for the ASP.NET Core types and `Microsoft.Extensions.Http`. Like the application project, it enables `ImplicitUsings`, and its test files add these namespaces:

```csharp
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Storefront;
using Xunit;
```
