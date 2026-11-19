---
description: A discarded Task.Run in an ASP.NET Core endpoint is async but not durable. Choose between awaiting, a bounded in-memory queue, and storing the job.
title: Task.Run Does Not Make ASP.NET Core Work Durable
author: Christopher D. Cavell
published: "2026-11-17"
summary: Starting work with Task.Run and returning 202 Accepted makes an ASP.NET Core endpoint fast, but the work still lives only in that process. Nothing in that design coordinates it with shutdown, a request-scoped service it captured may already be disposed, its failure goes unrecorded by the normal error path, and a crash or deployment can end it without a trace. Choose deliberately among finishing the work inside the request, handing it to a bounded in-memory queue that a hosted service processes, and recording it durably before you answer. Each gives a different guarantee, and the right choice depends on whether losing the work on restart is acceptable. State that guarantee, and test it.
feed: true
x_post: Task.Run plus 202 Accepted makes an endpoint fast, not durable. Unfinished work dies with the process. Decide if that loss is OK.
x_hashtags:
  - DotNet
  - AspNetCore
x_question: "What happens to your background work during a deploy?"
---

# Task.Run Does Not Make ASP.NET Core Work Durable

**Pattern classification:** General learning material

**Difficulty:** Beginner to intermediate, with advanced production notes

**Prerequisites:** Comfortable reading basic C# and ASP.NET Core minimal API code. Familiarity with `async` and `await` and dependency injection is helpful. Hosted services, channels, and durable job processing are introduced in the article; the durable-job section and the appendix go further, as production notes. No AsiBackbone package or prior Learning material is required.

**Sample scope:** The C# targets .NET 8 or later. The fire-and-forget endpoint, the bounded queue, the endpoints, the worker, and the tests in this article, together with the code in the [appendix](#appendix-types-fakes-and-test-helpers), were compiled and run on .NET 10 with xUnit v3 and `Microsoft.AspNetCore.TestHost`: the suite reports twelve passing test cases, counting each row of a parameterized theory separately. The durable-job sketch was compiled against EF Core 10 together with the supporting definitions in the appendix, but was not run against a database, and its duplicate-key check is a provider-specific placeholder. The shutdown ordering and timeout behavior described for Kestrel were observed on .NET 10 with the small program in the appendix; they are observations of that configuration, not documented guarantees for every server and host. The endpoints are focused samples: they omit the authorization a real statement endpoint needs, which is discussed where it matters. Framework behavior is described from Microsoft's documentation on the verification date below; check it for your version. The reporting application, the incident, the customers, and the timings are hypothetical. Original prose is licensed under [Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/), and original code examples are additionally offered under the [MIT License](https://opensource.org/license/mit), as described in the repository's [licensing terms](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md). Third-party material, where identified, remains under its original license. External sources were last verified on November 17, 2026.

**What this article covers:** an endpoint that returns `202 Accepted` while its work runs unobserved, and the month-end statements lost when its process exits; the difference between detached work, host-managed execution, and durable acceptance; why detached work must not use a request-scoped service, and how to create a scope for each work item; a bounded in-memory queue with backpressure, failure recording, and a shutdown that reports what it abandons; deterministic tests for a full queue, a failed work item, work still pending when the host stops, and admission closing at shutdown; what `202 Accepted` does and does not promise; a status resource with a stable identifier; storing the job before answering; when a separate worker or scheduler is worth it; and when losing the work on restart is an acceptable, documented tradeoff.

**In plain terms:** a waiter takes your order and says "coming right up." If the waiter only memorized it, the order is gone when the waiter's shift ends early or they trip on the way to the kitchen, and nobody else ever knew about it. If the waiter pinned a ticket on the kitchen rail, the kitchen knows, works through tickets in order, and stops taking new ones when the rail is full, but the tickets still go in the bin if the kitchen closes or the power fails. If the order was entered into the till, it survives the shift change, and anyone can look it up. All three waiters gave the same immediate answer, but the systems behind that answer made different promises. Only one recorded the order somewhere that outlasts the kitchen.

Consider a hypothetical accounting application. At month-end close, staff request statements for their customers. Generating a statement means querying a few thousand ledger lines, rendering a PDF, and emailing it, which usually takes ten to forty seconds. The original endpoint did all of that inside the request, and the browser waited. A developer made it responsive with one change:

```csharp
app.MapPost("/reports", (ReportCommand command, IReportGenerator generator, IEmailSender email) =>
{
    var request = new ReportRequest(Guid.NewGuid(), command.CustomerId, command.Email);

    _ = Task.Run(async () =>
    {
        byte[] pdf = await generator.GenerateAsync(request, CancellationToken.None);
        await email.SendAsync(request.Email, pdf, CancellationToken.None);
    });

    return Results.Accepted(value: new { request.ReportId });
});
```

For this first version, assume the captured services stay usable after the request ends; the lifetime problem that breaks that assumption comes later. The endpoint now answers in a few milliseconds, the screen says "Your statement will be emailed in a few minutes," and in development, every statement arrives.

In this hypothetical incident, operations deploy a fix to an unrelated page at 16:58 on the last business day of October, while staff are working through the close. This illustrative excerpt shows three of the requests on one of the two instances that were replaced:

```text
16:58:14.204  api-2   POST /reports  C-1180 → 202 Accepted  R-5521
16:58:22.611  api-2   POST /reports  C-1192 → 202 Accepted  R-5524
16:58:31.377  api-2   POST /reports  C-1207 → 202 Accepted  R-5527
16:58:40.019  deploy  Draining api-2 for rolling update
16:58:40.020  api-2   Application is shutting down...
16:58:40.413  api-2   Process exited with code 0
```

There is nothing more about `R-5521`, `R-5524`, or `R-5527`. The application did not track these tasks, so the log cannot say whether any of them finished before the process exited. There was no error, because nothing the application watched had failed: each request had completed the moment it returned `202`, so the shutdown had no in-flight request to wait for, and the process exited normally, ending any of those operations that had not already completed. The application found out on November 3, when customers called: across the two replaced instances, fourteen accepted statement requests had never reached them. The application had no durable completion record for any of the fourteen, and its surviving logs did not show how far any of them had progressed.

The same week, a statement failed when the mail relay timed out. That task's exception was stored in a `Task` object that no code held, so the application's normal error handling never saw it: it was not logged, not retried, and not shown to the person who asked.

> **`Task.Run` changes how the work is scheduled. It does not change who is responsible for it. In this endpoint, discarding the returned task leaves it with no application-level owner: no component awaits it, observes its failure, or coordinates it with shutdown. Before returning `202 Accepted`, decide which of three guarantees you need, finished in the request, owned by the host, or recorded durably, and build that one deliberately.**

---

## Three Different Things Called "Background"

"Run it in the background" mixes three separate ideas, and it helps to pull them apart first.

- **Scheduling.** `Task.Run` queues a delegate to the thread pool. Calling an `async` method is different: it runs synchronously on the calling thread until its first incomplete `await`, and then yields without blocking a thread. Neither says anything about who is responsible for the result.
- **Tracking.** Code that retains the returned `Task` and eventually awaits it, inspects it, or hands it to something that supervises it can observe its completion and its exception. Merely storing the task where nothing acts on it does not count. This is what makes work *detached*, whether it was started with `Task.Run` or by calling an `async` method.
- **Lifetime and persistence.** Whether anything outside the code that started the work knows it exists: the host, during shutdown, or a store that survives the process.

That gives three levels of guarantee an endpoint can use while returning before the work finishes:

| Level | In .NET | Who knows the work exists | What can end it unfinished |
| --- | --- | --- | --- |
| **Detached work** | `_ = Task.Run(...)`, or an `async` method called and its `Task` discarded | No application component in this pattern, once the returned task is discarded | Process exit, for any reason. An exception faults the task and, with nobody observing it, goes unrecorded by the normal error path |
| **Host-managed execution** | A `BackgroundService` or other `IHostedService` | The host, which starts it, signals it to stop, and waits for it during a graceful shutdown, up to a configurable timeout | Process exit before the work completes, including an exit after the host has stopped waiting. The timeout ends the host's waiting, not the work |
| **Durable acceptance** | A row in a database, a message in a durable broker, a job in a library with persistent storage | The store, which survives the process | The record survives within the store's guarantees. Processing can still fail permanently, be canceled, expire, or exhaust its retries |

Scheduling decides how execution begins. Tracking decides who observes its completion. Host management decides which application lifetime owns it. Durable acceptance decides whether the request is remembered outside the process that accepted it. `Task.Run` gives you thread-pool scheduling, and `async` and `await` let work yield without blocking a thread. Neither gives the work host ownership or durability.

The [ASP.NET Core hosted-services documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services) describes what host management adds: hosted services are started with the application, and during a graceful shutdown the host calls each service's `StopAsync`. For a `BackgroundService`, that signals the stopping token and waits for `ExecuteAsync` to finish, bounded by the host's [shutdown timeout](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host#shutdown-timeout), which is 30 seconds by default and configurable through `HostOptions.ShutdownTimeout`. The same page states the limit: if the process fails unexpectedly, `StopAsync` might not be called. Hosting platforms add their own limit, because an orchestrator or service manager that asks the process to stop will usually terminate it after its own grace period, whatever the host's timeout says. A host-managed worker is better owned than a detached task. It is not durable.

### What happens to detached work

Detached work has three problems. The first is the absence of durability. The other two are about observability and lifetime, but they produce the same result from the outside: accepted work that never completes, and nobody who knows.

**Nothing waits for it.** During a graceful shutdown, the [host stops the server](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host#ihostapplicationlifetime), which, as that page puts it, waits for requests on existing connections for as long as the shutdown timeout allows, and stops hosted services. In the tested configuration, `WebApplication` with Kestrel on .NET 10, active requests were allowed to finish within the configured shutdown timeout and were cut off when it was shorter than the request; the [appendix](#shutdown-harness) has the harness and the observed timings. Other servers, IIS in-process hosting, and hosting platforms can impose different limits, so verify the behavior of the configuration you deploy. The detached task in this example is neither an in-flight request nor work registered with a hosted service, so the host has nothing associated with it to wait for. When the process exits, execution ends regardless of how far the task had progressed: before the query, halfway through the PDF, or after the email went out but before anything recorded that it did.

**Nobody observes its failure.** An exception inside a task is stored on the task. If no code awaits it or reads it, the application's normal exception handling and logging never see it, and since .NET Framework 4.5 an unobserved task exception does not crash the process. If something subscribes to `TaskScheduler.UnobservedTaskException`, the runtime may raise that event later, typically after the faulted task becomes unreachable and the garbage collector processes it. The timing is nondeterministic, and it is no substitute for observing the task. The caller already has a `202`.

**It may outlive the services it captured.** The next section covers this one, because it often appears as part of a later change.

---

## Detached Work Must Not Use the Request's Services

Two weeks after the incident, a developer moved statement generation onto the application's EF Core `DbContext` to reuse an existing query. `IReportGenerator` became a scoped service that depends on the `DbContext`, which is itself registered with a scoped lifetime, as is usual. The fire-and-forget endpoint did not change, but the `generator` it captures is now a scoped object.

ASP.NET Core [creates one dependency-injection scope per request](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection#request-services), and its scoped services are meant to be used only while the request is active. When the request ends, the scope is disposed, and every `IDisposable` scoped service is disposed with it. The detached task still holds a reference to the generator and, through it, to the `DbContext`. When it reaches the first query, the context has been disposed:

```text
16:12:08.441  api-1  POST /reports  C-1180 → 202 Accepted  R-5602
(no further output)
```

The `ObjectDisposedException` is thrown inside a task that nobody awaits, so the application's normal exception handling does not record it, and the log shows nothing further for `R-5602`. Favorable timing, such as a query that happens to finish before the request's scope is disposed, can let some statements through, which is how this kind of bug passes a quick manual test.

The test below shows the failure without timing luck. The fake generator waits until the request scope has been disposed and then checks whether it was disposed, exactly as a `DbContext` would:

```csharp
[Fact]
public async Task Detached_work_that_captured_a_request_scoped_service_fails_after_the_response()
{
    var behavior = new ReportBehavior { WaitForRelease = true };
    var email = new RecordingEmailSender();

    WebApplicationBuilder builder = WebApplication.CreateBuilder();
    builder.WebHost.UseTestServer();
    builder.Services.AddSingleton(behavior);
    builder.Services.AddScoped<IReportGenerator, FakeReportGenerator>();   // scoped, like a DbContext
    builder.Services.AddSingleton<IEmailSender>(email);
    await using WebApplication app = builder.Build();
    app.MapFireAndForgetReports();                                          // the endpoint shown above
    await app.StartAsync(TestContext.Current.CancellationToken);

    HttpResponseMessage response = await app.GetTestClient().PostAsJsonAsync(
        "/reports", new ReportCommand("C-1180", "ap@example.com"), TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);   // the caller is told it was accepted

    await behavior.ScopeDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    behavior.ReleaseGeneration.SetResult();                        // the detached work continues after the request ends
    Exception fault = await behavior.BackgroundFault.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    Assert.IsType<ObjectDisposedException>(fault);                 // its scoped service was already disposed
    Assert.Empty(email.Sent);                                      // and the statement is never sent
}
```

The rule is simple: **work that outlives the request gets its own scope, created for that work item, and disposed when that work item finishes.** It copies plain data out of the request, such as the customer ID, the email address, and the report ID, and resolves its own services inside its own scope. It never carries the request's `HttpContext`, `ClaimsPrincipal`, `DbContext`, or any other scoped object across the boundary. [A Background Job Should Not Inherit Everything the User Could Do](background-job-should-not-inherit-user-authority.md) covers the identity half of that rule: what the job should carry about who asked, and how the worker re-checks it.

The worker later in this article does exactly that, with `IServiceScopeFactory.CreateAsyncScope()` for each item, as the [hosted-services documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services#consuming-a-scoped-service-in-a-background-task) recommends.

---

## Option 1: Finish the Work Inside the Request

Before reaching for a queue, ask whether the work needs to leave the request at all.

```csharp
app.MapPost("/reports", async (ReportCommand command, IReportGenerator generator, IEmailSender email,
    CancellationToken cancellationToken) =>
{
    var request = new ReportRequest(Guid.NewGuid(), command.CustomerId, command.Email);

    byte[] pdf = await generator.GenerateAsync(request, cancellationToken);
    await email.SendAsync(request.Email, pdf, cancellationToken);

    return Results.Ok(new { request.ReportId, Status = "Sent" });
});
```

This version keeps the work inside the request's own execution and error-handling path, which every other option has to work to recover. The request's scope is valid throughout, so the scoped `DbContext` is fine. An exception reaches the normal ASP.NET Core error handling and logging. Assuming `IEmailSender.SendAsync` returns only once the mail service has acknowledged the message, which is the contract this article assumes throughout, a `200` means the statement was generated and the mail service accepted it; that is an acknowledgment of submission, not proof that it reached the customer's inbox. An error response usually means it was not sent, although a failure during the send itself can leave that unknown.

It is not immune to interruption. If the process dies, or the connection drops, mid-request, the caller typically sees a connection reset or a timeout rather than an error response, and cannot tell whether the email went out. During a graceful shutdown in the tested Kestrel configuration, the server waited for in-flight requests only for as long as the host's shutdown timeout allowed, and a hosting platform may end the process sooner, so a forty-second statement started just before a deployment can still be cut off. The difference from the detached version is that the caller knows it did not get an answer.

The other costs are latency and capacity. The caller waits ten to forty seconds. A proxy, load balancer, or client may time out first, leaving an outcome that neither side knows. Each pending request keeps its request state alive and occupies an active request slot; depending on the HTTP version and the proxies in between, concurrent requests may or may not share connections. An asynchronous wait does not ordinarily occupy a thread, but a burst of month-end requests still consumes memory, server and proxy capacity, and concurrent load on the database and mail relay.

Passing the request-aborted token, as this example does, lets the work observe cancellation when the client disconnects; the work stops only where it, and the code it calls, honor that token. Whether that is right is a business decision, not a technical default. A disconnect may mean the person gave up, or only that a phone changed networks or a proxy timed out. Decide separately whether generation and delivery should stop, continue, or move to durable work when the client goes away.

For work that typically finishes in a second or two, this is often the right answer, and it is the baseline the other options should be measured against.

### Where `Task.Run` still belongs

None of this makes `Task.Run` wrong. It is the right tool for running CPU-bound work on the thread pool when the caller *awaits* the result, for example, inside a hosted service or a desktop application, or for starting several independent computations and awaiting them together with `Task.WhenAll`. Inside an ASP.NET Core request, `Task.Run` around CPU-bound work does not add CPU capacity or isolate that work from the server: it runs on the same shared thread pool that handles requests. In those cases, the code that started the task owns it, waits for it, and observes its exceptions. The problem in the opening example is not `Task.Run`. It is discarding the returned task without handing it to anything that tracks its completion, failure, cancellation, and shutdown. The `_ =` only makes that discard explicit; calling an `async` method without awaiting it, or storing its task in a variable nobody reads, does the same. (In an ASP.NET Core request, awaiting `Task.Run` around ordinary asynchronous I/O usually adds a scheduling hop without making anything faster, because the request is already running on the thread pool.)

---

## Option 2: Host-Owned, Bounded, Best-Effort Processing

If the caller should not wait, the next step is to hand the work to something the host owns: a `BackgroundService` that reads from a bounded `Channel<T>`. This builds on the [queued background tasks](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services#queued-background-tasks) pattern in Microsoft's hosted-services documentation; the code below was written for this article and differs from that sample in three ways this example needs: a full queue is refused rather than waited on, each item's outcome is recorded, and shutdown reports what it abandons.

### The queue

```csharp
public sealed class ReportQueue
{
    private readonly Channel<ReportRequest> channel;

    public ReportQueue(int capacity) =>
        channel = Channel.CreateBounded<ReportRequest>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,   // TryWrite returns false when full
            SingleReader = true,
        });

    public bool TryEnqueue(ReportRequest request) => channel.Writer.TryWrite(request);

    public ChannelReader<ReportRequest> Reader => channel.Reader;

    public void Close() => channel.Writer.TryComplete();
}
```

The capacity is the backpressure. Without one, a sustained period in which requests arrive faster than the worker finishes them can grow the queue until memory runs out, and every item in it is another statement lost in the next restart. With one, the application has to decide what to do when it is full, and there are only a few honest answers:

- **Refuse the request.** With `BoundedChannelFullMode.Wait`, `TryWrite` never waits: it returns `false` immediately when the channel is full, or when it has been closed. The endpoint returns `503 Service Unavailable` with a `Retry-After` header, and the caller knows the statement was not accepted.
- **Wait for space.** `WriteAsync` waits until the worker makes room, which is what the documentation's sample does. That pushes the backpressure onto the request, which is reasonable when the queue rarely fills, but under sustained load it reintroduces the long-held requests that Option 1 had.
- **Drop something, deliberately.** The other [`BoundedChannelFullMode`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.channels.boundedchannelfullmode) values discard the item being written (`DropWrite`), or an item already queued to make room for the new one (`DropNewest`, `DropOldest`). The documentation describes which item each mode discards; it is less explicit about the return value. On .NET 10, `TryWrite` returns `true` in all three modes even though an item was discarded, and a test in the appendix pins that behavior and which items survive in each mode. So the return value no longer means "this request will be processed." With `DropNewest` or `DropOldest`, the item that disappears was accepted earlier, and in this example its status would stay `Queued` forever. That suits telemetry samples or cache refreshes where the newest request supersedes older ones. It does not suit statements.

### The endpoint and the status resource

```csharp
app.MapPost("/reports", (ReportCommand command, ReportQueue queue, ReportStatusStore statuses, HttpResponse response) =>
{
    var request = new ReportRequest(Guid.NewGuid(), command.CustomerId, command.Email);

    statuses.Set(request.ReportId, ReportState.Queued);   // before the worker can see it
    if (!queue.TryEnqueue(request))
    {
        statuses.Remove(request.ReportId);   // full, or closed for shutdown: not accepted
        response.Headers.RetryAfter = "30";   // illustrative; base it on how quickly capacity returns
        return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Report processing is temporarily unavailable. Try again shortly.");
    }

    return Results.Accepted($"/reports/{request.ReportId}", new { request.ReportId, Status = "Queued" });
});

app.MapGet("/reports/{reportId:guid}", (Guid reportId, ReportStatusStore statuses) =>
    statuses.Get(reportId) is { } state
        ? Results.Ok(new { reportId, Status = state.ToString() })
        : Results.NotFound());
```

The `202` now carries a `Location` header pointing at a status resource, and the report ID, in the body and in that URL, identifies this piece of work. The identifier never changes, but this sample keeps its status only for the life of the process. A GUID is an identifier, not a storage mechanism. The status is written *before* the item is enqueued, because a worker can pick it up immediately. If the endpoint wrote `Queued` afterward, it could overwrite `Sent`.

That ordering avoids one race; it does not make the status and the queue one record. Writing a dictionary entry and writing to a channel are two separate operations, and nothing makes them atomic. When the acceptance record itself has to be authoritative, that is a job for Option 3.

`TryEnqueue` returns `false` both when the queue is full and when it has been closed for shutdown, so the response describes the service as temporarily unavailable rather than claiming a reason. An application that wants different messages or retry guidance for the two cases can return a richer result than a `bool`. That matters for `Retry-After`: when the refusal comes from shutdown rather than capacity, a fixed delay is only honest if traffic is known to be moving to a healthy instance.

These endpoints are focused on durability, so they leave out checks a real statement endpoint needs: that the caller may request statements for this customer, with a recipient address resolved or verified by the server rather than taken from the request, and that the caller may read this report's status. A report ID in a URL is an identifier, not a permission. A real status endpoint should also send cache headers that keep user-specific status out of shared caches and give polling clients a sensible freshness policy.

`ReportStatusStore` is a singleton wrapping a `ConcurrentDictionary<Guid, ReportState>`. That is enough to show the pattern, and it has the same weakness as the queue: it lives in this process. Behind a load balancer, a status request that lands on a different instance gets `404`, and after a restart every status is gone, so the client cannot tell "never existed" from "forgotten." Entries also have to expire, or the dictionary grows without bound; this sample does not implement that. It also accepts any status change unconditionally, which is safe here only because one worker owns each item. A production status model should enforce valid transitions, with a conditional update or a version check, so that a stale writer cannot overwrite a finished outcome. If people depend on the status page, the status belongs in shared storage, and at that point you are most of the way to Option 3.

### The worker

```csharp
public sealed class ReportWorker(
    ReportQueue queue,
    IServiceScopeFactory scopes,
    ReportStatusStore statuses,
    ILogger<ReportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Close admission when this worker is told to stop, not after the running item unwinds.
        using CancellationTokenRegistration closeOnStop = stoppingToken.Register(queue.Close);

        try
        {
            while (!stoppingToken.IsCancellationRequested && await queue.Reader.WaitToReadAsync(stoppingToken))
            {
                // Check the token before each item: a channel hands out buffered items without waiting.
                while (!stoppingToken.IsCancellationRequested && queue.Reader.TryRead(out ReportRequest? request))
                {
                    await ProcessAsync(request, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        finally
        {
            // Runs when ExecuteAsync unwinds, which can be after the host has stopped waiting for it.
            // If the process exits first, or the work never returns, it gets no chance to run.
            // Closing here also covers other ordinary exits from this worker: once there is no consumer, stop admission.
            queue.Close();
            while (queue.Reader.TryRead(out ReportRequest? pending))
            {
                statuses.Set(pending.ReportId, ReportState.NotStarted);
                logger.LogWarning("Report {ReportId} was accepted but never started; it is lost", pending.ReportId);
            }
        }
    }

    private async Task ProcessAsync(ReportRequest request, CancellationToken stoppingToken)
    {
        statuses.Set(request.ReportId, ReportState.Running);
        AsyncServiceScope? scope = null;
        try
        {
            scope = scopes.CreateAsyncScope();   // one scope per work item, created inside the item boundary
            IServiceProvider services = scope.Value.ServiceProvider;
            var generator = services.GetRequiredService<IReportGenerator>();
            var email = services.GetRequiredService<IEmailSender>();

            byte[] pdf = await generator.GenerateAsync(request, stoppingToken);
            await email.SendAsync(request.Email, pdf, stoppingToken);

            statuses.Set(request.ReportId, ReportState.Sent);   // the mail service accepted it
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            statuses.Set(request.ReportId, ReportState.Interrupted);   // the email may or may not have gone
            logger.LogWarning("Report {ReportId} was interrupted by shutdown", request.ReportId);
        }
        catch (Exception ex)
        {
            statuses.Set(request.ReportId, ReportState.Failed);   // no confirmed submission; see the text
            logger.LogError(ex, "Report {ReportId} failed", request.ReportId);
        }
        finally
        {
            // Dispose on its own, so a cleanup failure cannot hide the work's exception or change its status.
            if (scope is { } created)
            {
                try
                {
                    await created.DisposeAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Report {ReportId} cleanup failed", request.ReportId);
                }
            }
        }
    }
}
```

Register it with `builder.Services.AddSingleton(new ReportQueue(capacity: 100))`, `builder.Services.AddSingleton<ReportStatusStore>()`, and `builder.Services.AddHostedService<ReportWorker>()`.

These details carry the design:

- **One scope per item.** Each statement gets a fresh scope, so it gets a fresh `DbContext`, and a failure in one statement cannot leave a broken context behind for the next.
- **Every item's ordinary exception is caught, recorded, and logged, and the loop continues.** That includes an exception thrown while creating the item's scope, which is why the scope is created inside the `try`; outside it, that exception would escape `ProcessAsync`, leave the item `Running`, and end the worker. Catching it keeps the item from being stuck in `Running`. Whether the worker should then carry on is a policy decision: an item-local activation failure may justify continuing, but a disposed root provider or a broken container points to a systemic problem, and an application may prefer to stop the worker or the host for those. This is deliberate. Since .NET 6, an exception that escapes a `BackgroundService`'s `ExecuteAsync` is logged and, by default, [stops the whole host](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/hosting-exception-handling). One bad customer record should not take the web application down with it. The worker catches at the item boundary, so a failure becomes an observable `Failed` status instead.
- **Disposing the scope is kept apart from the work.** The scope is disposed in its own `try` block, so an exception from cleanup is logged as a cleanup problem and cannot replace the work's own exception, turn `Interrupted` into `Failed`, or turn `Sent` into `Failed`. With `await using`, a disposal exception thrown after the body has already failed would replace the body's exception.
- **Admission closes when the worker is told to stop.** The registration on the worker's stopping token closes the queue when that token is canceled. From then on, `TryEnqueue` fails and the endpoint returns `503`, even while a long statement is still running. Two qualifications apply. First, the documented contracts differ: [`ApplicationStopping`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.ihostapplicationlifetime.applicationstopping) is the application-wide signal that graceful shutdown has begun, while a `BackgroundService`'s token is canceled when the host stops that service. In the tested `WebApplication` on .NET 10, `ApplicationStopping` fired first, the server then finished its in-flight requests, and only then was this worker's token canceled, so requests still finishing during that drain could enqueue work while the worker kept processing. The sample as written does not do that. An application that must refuse new work from the first shutdown signal should also register `queue.Close` with `IHostApplicationLifetime.ApplicationStopping`. Shutdown waits for `ApplicationStopping` callbacks to finish, so keep them short and non-blocking: completing the channel writer is fine; draining jobs or calling other services there is not. Second, an enqueue that races the close can still succeed just before it; that item is then either taken and processed by the worker or reported as `NotStarted` with the others.
- **The loop checks the stopping token before taking each item.** A channel that already holds items hands them out without waiting, so a loop like `await foreach (var item in reader.ReadAllAsync(stoppingToken))` can keep returning buffered items after shutdown has begun, and each one would start and be interrupted immediately. That bug is easy to write, and the shutdown test below catches it. The checks narrow the window rather than close it. An item taken just before cancellation still enters `ProcessAsync` with a token that is already canceled. If a dependency observes the token and throws, the item is recorded as `Interrupted`; if not, it may finish as `Sent` or `Failed`.
- **Shutdown reports what it abandons.** When the stopping token fires, the running item receives it, and once the worker unwinds, the `finally` block records every item that never started. This worker chooses prompt shutdown over draining its backlog. Another application could drain for a few seconds instead, but that is still bounded by the shutdown timeout and still does not survive a crash.
- **It processes one statement at a time**, to keep ownership and shutdown easy to see. A production worker might run several items concurrently; each concurrent item still needs its own scope and its own failure handling.

The capacity bounds the number of waiting items, not the process's memory. The status dictionary still grows until entries expire, and each item's own work can use memory too.

`Failed` means processing ended with an exception and no confirmed submission. It does not prove that no email was sent: if the mail relay accepted the message and the acknowledgment was lost, the send throws and the statement is recorded as `Failed`. `Interrupted` has the same limit when cancellation arrives during the send. Treat both as "check before resending," not "safe to resend." A design that needs to tell these cases apart can record a separate `OutcomeUnknown` state for anything that ends during submission.

The `NotStarted` and `Interrupted` records go to the logging pipeline and to the in-memory status store. The status store disappears with the process. The log entries outlive it only if the configured logging provider sends them somewhere outside the process; console output from a container that is being replaced may not survive.

### What this example guarantees, and what it may lose

Stated plainly, so it can go in the API documentation:

**Under this design:**

- the channel buffers at most 100 statements, and the worker can hold one more that it is processing. An enqueue attempt is refused with `503` while all 100 slots are full, and once the queue has been closed for shutdown;
- each statement runs in its own dependency-injection scope;
- a statement whose processing ends with an ordinary exception and no confirmed submission, including one thrown while creating its scope, is logged and marked `Failed`, and the worker continues with the next one; a cleanup failure is logged separately and does not change the statement's status;
- `Sent` means `IEmailSender.SendAsync` completed, which under the assumed contract means the mail service accepted the message;
- when the host stops the worker, the queue stops accepting work, no further buffered statement is started except in the narrow races described above, and the running statement is signaled to stop. Requests that finish between `ApplicationStopping` and that moment can still enqueue work, unless the application also closes the queue on `ApplicationStopping`.

**Provided the worker unwinds before the process exits:**

- the running statement is marked `Interrupted` if cancellation aborted it, `Sent` if submission completed, or `Failed` if another exception occurred;
- every queued statement that never started is logged by ID and marked `NotStarted`.

That proviso matters. If a dependency ignores the token, the statement runs on, and the host waits for it only until its shutdown timeout elapses or the token passed to `StopAsync` is canceled. That ends the host's waiting; it does not stop the work. If the process exits before `ExecuteAsync` unwinds, the `finally` block gets no chance to run and the statement's status stays `Running`. If the process stays alive and the work eventually returns, the cleanup can still run later.

**It may lose:**

- every queued statement, on any shutdown, graceful or not. A graceful shutdown only reports the loss;
- the running statement, which may or may not have been emailed when it was interrupted;
- everything, with no record or only a partial one, if the process crashes, is killed, runs out of memory, or exceeds the shutdown timeout;
- all statuses, on any restart, and any status requested from a different instance.

It never retries. A `Failed` or `Interrupted` statement stays that way until someone asks again, and whoever asks should first check whether the earlier attempt reached the mail service.

That is a real improvement over the opening example: losses are bounded and, on the graceful path, reported, and the caller has an identifier to ask about. But nothing about it survives the process. Using `BackgroundService` and `Channel<T>` makes the work *host-managed*. It does not make it durable.

---

## Testing the Guarantee

The guarantees above are claims about failures, so the tests have to cause those failures on purpose. They use three small fakes, listed in full in the [appendix](#appendix-types-fakes-and-test-helpers) with the helpers below:

- `FakeReportGenerator` is registered as scoped. It can be told to fail for a particular customer, to wait for a signal before finishing, to ignore cancellation, and to throw when disposed. It signals when it starts, when it observes cancellation, when its scope is disposed, and when it is used after disposal.
- `RecordingEmailSender` records each address it was asked to send to. It completes immediately and does not check the token.
- `CapturingLoggerProvider` keeps every log entry in memory.

None of the tests sleep or poll. The tests that coordinate with the worker or with detached work wait on a signal from a fake, with a timeout that fails the test if the signal never arrives; the endpoint test waits only for its HTTP responses, and the channel tests in the appendix need no waiting at all.

### A full queue

The worker is not registered, so nothing drains the queue during the test. `MapQueuedReports` maps the two endpoints shown above.

```csharp
[Fact]
public async Task A_full_queue_rejects_the_request_instead_of_accepting_it()
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder();
    builder.WebHost.UseTestServer();
    builder.Services.AddSingleton(new ReportQueue(capacity: 2));
    builder.Services.AddSingleton<ReportStatusStore>();
    await using WebApplication app = builder.Build();
    app.MapQueuedReports();
    await app.StartAsync(TestContext.Current.CancellationToken);
    HttpClient client = app.GetTestClient();
    CancellationToken ct = TestContext.Current.CancellationToken;

    HttpResponseMessage first = await client.PostAsJsonAsync("/reports", new ReportCommand("C-1", "a@example.com"), ct);
    HttpResponseMessage second = await client.PostAsJsonAsync("/reports", new ReportCommand("C-2", "b@example.com"), ct);
    HttpResponseMessage third = await client.PostAsJsonAsync("/reports", new ReportCommand("C-3", "c@example.com"), ct);

    Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
    Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
    Assert.Equal(HttpStatusCode.ServiceUnavailable, third.StatusCode);
    Assert.Equal(TimeSpan.FromSeconds(30), third.Headers.RetryAfter?.Delta);

    HttpResponseMessage status = await client.GetAsync(first.Headers.Location, ct);
    Assert.Contains("Queued", await status.Content.ReadAsStringAsync(ct));
}
```

### An observed worker failure

The worker tests build a plain host with `Host.CreateApplicationBuilder()` and put items directly on the queue, so they exercise the worker, not the HTTP endpoint. `CreateHost` registers the queue, the status store, the fakes, and `ReportWorker`, and replaces the logging providers with the capturing one. `Report` creates a `ReportRequest`.

```csharp
[Fact]
public async Task A_failed_item_is_recorded_and_the_worker_keeps_going()
{
    var (host, queue, statuses, behavior, email, logs) = CreateHost();
    behavior.FailFor.Add("C-broken");
    ReportRequest broken = Report("C-broken");
    ReportRequest healthy = Report("C-healthy");
    statuses.Set(broken.ReportId, ReportState.Queued);
    statuses.Set(healthy.ReportId, ReportState.Queued);
    Assert.True(queue.TryEnqueue(broken));
    Assert.True(queue.TryEnqueue(healthy));

    await host.StartAsync(TestContext.Current.CancellationToken);
    await email.FirstSent.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    await host.StopAsync(TestContext.Current.CancellationToken);
    host.Dispose();

    Assert.Equal(ReportState.Failed, statuses.Get(broken.ReportId));
    Assert.Equal(ReportState.Sent, statuses.Get(healthy.ReportId));
    Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error && e.Error is InvalidOperationException);
}
```

The healthy statement was queued behind the broken one, so its being sent proves that the worker survived the failure.

### Work still pending when the host stops

Three statements are queued. The first one starts and waits, honoring the stopping token, and the host is stopped gracefully while it waits.

```csharp
[Fact]
public async Task Accepted_work_that_has_not_started_is_lost_when_the_host_stops()
{
    var (host, queue, statuses, behavior, email, logs) = CreateHost();
    behavior.WaitForRelease = true;                    // the first statement never finishes on its own
    ReportRequest[] reports = [Report("C-1"), Report("C-2"), Report("C-3")];
    foreach (ReportRequest r in reports)
    {
        statuses.Set(r.ReportId, ReportState.Queued);
        Assert.True(queue.TryEnqueue(r));
    }

    await host.StartAsync(TestContext.Current.CancellationToken);
    await behavior.GenerationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    await host.StopAsync(TestContext.Current.CancellationToken);   // a graceful stop: deployment, scale-in, recycle
    host.Dispose();

    Assert.Equal(ReportState.Interrupted, statuses.Get(reports[0].ReportId));
    Assert.Equal(ReportState.NotStarted, statuses.Get(reports[1].ReportId));
    Assert.Equal(ReportState.NotStarted, statuses.Get(reports[2].ReportId));
    Assert.Empty(email.Sent);
    Assert.Equal(2, logs.Entries.Count(e => e.Message.Contains("never started")));
    Assert.False(queue.TryEnqueue(Report("C-4")));    // the queue refuses new work once it is closed
}
```

Read the assertions as the documentation of a loss, not a success. Three statements were accepted, and none was sent. The test proves that, on the graceful path, the loss is reported.

### Admission closes when the worker is told to stop

Several outcomes depend on the running statement honoring the token. This test covers the case where it does not: the fake ignores cancellation, so the statement keeps running after the worker's token is canceled. It checks the queue directly; the endpoint turns the same refusal into a `503`.

```csharp
[Fact]
public async Task Admission_closes_when_the_worker_is_stopped_even_if_the_running_item_ignores_cancellation()
{
    var (host, queue, statuses, behavior, email, logs) = CreateHost();
    behavior.WaitForRelease = true;
    behavior.IgnoreCancellation = true;               // a dependency that does not honor the stopping token
    ReportRequest running = Report("C-1");
    statuses.Set(running.ReportId, ReportState.Queued);
    Assert.True(queue.TryEnqueue(running));

    await host.StartAsync(TestContext.Current.CancellationToken);
    await behavior.GenerationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    Task stopping = host.StopAsync(TestContext.Current.CancellationToken);
    await behavior.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    Assert.False(queue.TryEnqueue(Report("C-2")));    // refused while the first item is still running
    Assert.False(stopping.IsCompleted);               // shutdown is waiting for the item

    behavior.ReleaseGeneration.SetResult();
    await stopping;
    host.Dispose();

    Assert.Equal(ReportState.Sent, statuses.Get(running.ReportId));   // it ignored the token and finished
}
```

The appendix adds tests that a failure while disposing the scope neither erases a successful send nor hides the work's own exception, that an exception thrown while creating a scope is caught at the item boundary and becomes that item's `Failed` status rather than ending the worker, and of what `TryWrite` returns, and which items survive, in each full mode.

### What these tests cannot show

These are in-process tests, and they exercise the graceful path only. A test running inside the application's process cannot check what happens after that process is killed, because it is killed too. That needs an external, process-level test: start the application, submit work, terminate the process, restart it, and check what clients can observe. For this design, the answer to that test is already known and should be written down: everything in memory is gone, and nothing may have been logged.

[How to Test That a Denied Operation Never Executes](test-denied-operation-never-executes.md#async-fire-and-forget-and-cancellation-hazards) develops the related technique of asserting that a side effect did *not* happen, including for work dispatched asynchronously.

---

## What `202 Accepted` Promises

[RFC 9110](https://www.rfc-editor.org/rfc/rfc9110#section-15.3.3) defines `202 Accepted` as meaning the request has been accepted for processing, but processing has not been completed, and it may or may not eventually be acted upon. The status code confirms acceptance for processing. It does not guarantee completion, retries, or survival across a restart, and the RFC calls it intentionally noncommittal. Whatever more the server promises, the application has to say.

The opening endpoint and the bounded queue both returned `202`. They meant different things, and neither said which. A useful `202` response, and the documentation behind it, answers four questions:

1. **What identifies this work?** An ID, returned in the body and in the status location, with its lifetime stated.
2. **Where can the caller check on it?** The RFC says the response ought to describe the request's current status and point to or embed a status monitor. A `Location` header pointing at a status resource, as above, is one good way to do that. Say how long the status is kept.
3. **What can still happen to it?** For the bounded queue: "may be lost if the service restarts before it is sent." For a durable job with the claim, retry, and failure handling described below: "will be attempted until it succeeds or is marked failed."
4. **What should the caller do if it never completes?** Ask again, contact support, or wait for a retry the server will make.

If the honest answer to the third question is "it may be lost, and nobody will know," the client deserves to be told that in the API documentation, not discover it on November 3.

Stable identifiers matter in the other direction too. If the client's `POST` times out, it does not know whether the statement was accepted, and retrying creates a second statement. If duplicates matter, let the client send its own identifier, such as an `Idempotency-Key` header or a client-generated report ID, and have the server return the existing report instead of creating a new one. With the in-memory queue, that deduplication only works within one process lifetime. With durable storage, the store can enforce it, as the next section shows.

---

## Option 3: Record the Job Durably Before Answering

When a lost statement is not acceptable, the request must be remembered somewhere that outlives the process before the endpoint says it was accepted. One durable acceptance mechanism, often the simplest, is a job row committed to the database the application already has. The table has a unique index on `(CallerIssuer, CallerSubject, IdempotencyKey)`; that index, not any check in the endpoint, is what stops two jobs being created for one request:

```csharp
// Illustrative sketch: compiled against EF Core 10 with the supporting definitions in the appendix, but not
// run against a database. Assumes the caller is authorized for command.CustomerId.
app.MapPost("/reports", async (ReportCommand command, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
    ClaimsPrincipal user, ReportsDbContext db, CancellationToken cancellationToken) =>
{
    if (!IdempotencyKeys.IsValid(idempotencyKey))
    {
        return Results.BadRequest("A valid Idempotency-Key header is required.");
    }

    if (user.FindFirst(ClaimTypes.NameIdentifier) is not { } subject)
    {
        return Results.Unauthorized();
    }

    // The caller comes from authentication, never from the body. Issuer and subject stay separate columns.
    string callerIssuer = subject.Issuer;
    string callerSubject = subject.Value;

    // Assumes ordinary validation has already accepted the request. A real endpoint resolves or verifies the
    // recipient on the server; whatever value it settles on is normalized once, then both hashed and stored.
    string email = command.Email.Trim();
    string fingerprint = ReportFingerprint.Create(command.CustomerId, email);

    var job = new ReportJob
    {
        Id = Guid.NewGuid(),
        CallerIssuer = callerIssuer,
        CallerSubject = callerSubject,
        IdempotencyKey = idempotencyKey,
        Fingerprint = fingerprint,
        CustomerId = command.CustomerId,
        Email = email,
        Status = ReportJobStatus.Pending,
        CreatedAt = DateTimeOffset.UtcNow,
    };
    db.ReportJobs.Add(job);

    try
    {
        await db.SaveChangesAsync(cancellationToken);   // the 202 is sent only after this commits
        return Results.Accepted($"/reports/{job.Id}", new { ReportId = job.Id, Status = "Pending" });
    }
    catch (DbUpdateException ex) when (DuplicateKeys.IsIdempotencyKeyViolation(ex))
    {
        // Another request with this key committed first: a retry, or a concurrent duplicate.
        db.ChangeTracker.Clear();
        ReportJob existing = await db.ReportJobs.SingleAsync(
            j => j.CallerIssuer == callerIssuer && j.CallerSubject == callerSubject
              && j.IdempotencyKey == idempotencyKey, cancellationToken);

        return existing.Fingerprint == fingerprint
            ? ReplayContract.Existing(existing)   // this API's choice: 202 while pending or running, 200 once finished
            : Results.Conflict();                 // the same key reused for a different request
    }
}).RequireAuthorization();

public static class ReportFingerprint
{
    // Versioned, structured, and built from the normalized values the job stores. Not GetHashCode().
    public static string Create(string customerId, string email) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new FingerprintV1(Version: 1, CustomerId: customerId, Email: email))));

    private sealed record FingerprintV1(int Version, string CustomerId, string Email);
}

public static class IdempotencyKeys
{
    // Opaque: compared exactly as sent. Validate it; do not trim or case-fold it.
    public static bool IsValid([NotNullWhen(true)] string? key) =>
        key is { Length: > 0 and <= 100 } && key.All(c => c is >= '!' and <= '~');   // printable ASCII, no spaces
}
```

A few details in that sketch carry the guarantee:

- **The caller comes from authentication, not the request body.** The endpoint requires authorization, and the caller is the token's issuer plus its subject, stored as separate columns, because a subject value alone is not necessarily unique across identity providers. In a multi-tenant system, include the tenant too, in both the unique index and the lookup. The exact principal key depends on the authentication system: use the stable, validated issuer, subject, and tenant values your deployment provides, rather than assuming every identity maps the same way to `ClaimTypes.NameIdentifier`. Likewise, whether a principal without that claim gets `401` or `403` is a policy choice. Taking the caller from the body would let a client choose whose keys it collides with.
- **The key is opaque and validated.** A missing or empty key would otherwise become one key shared by unrelated requests. The sample accepts up to 100 printable ASCII characters, an example limit, and compares keys exactly as sent; it does not trim or case-fold them, and the index's collation has to agree.
- **The insert is tried first, and only the duplicate-key violation is handled.** `DuplicateKeys.IsIdempotencyKeyViolation` is provider-specific, for example, checking for PostgreSQL error `23505` on that index's name. Never treat every `DbUpdateException` as a duplicate: a deadlock, a timeout, or another constraint means something else. A check-then-insert without the index would let two concurrent requests both find nothing and both insert.
- **The fingerprint is a versioned digest of a structured encoding of the normalized fields that define the operation.** It lets the endpoint tell a retry from a different request that reused the key. The email is normalized once, and the same value is hashed and stored. JSON serialization of a fixed record keeps the encoding unambiguous; it is not a general canonical-JSON algorithm, so keep the record, its field order, and the serializer options fixed, and bump the version if they change. Apply whatever further normalization the business operation uses, such as rules for customer identifiers, before hashing; normalization is not a substitute for validating the values first. When the command gains a field that changes what is generated or where it goes, add it under a new fingerprint version before deploying. `GetHashCode()` is not stable across processes and must not be used for it.
- **A repeated request gets the current state of the original job.** In this illustrative contract, `ReplayContract.Existing` returns `202` with the status location while the job is pending or running, and `200` with its outcome once it has finished. Other APIs replay the original response instead; either is fine if it is documented and consistent.
- **Deduplication lasts only as long as the key is kept.** Define an idempotency retention window, and keep the key, the fingerprint, and a reference to the original outcome for at least that long, even if the rest of the job is archived. Once the row is deleted, the same key can create a new job. If jobs and idempotency keys need different retention periods, keep the keys in a table of their own rather than tying duplicate prevention to the job row's lifetime.

The row supplies durable *recording*. Reliable *processing* comes from the worker: it finds pending jobs, claims each one with a conditional update so that two instances cannot both take it, generates and sends the statement, and records the outcome on the same row. Any application instance connected to the same database can read the status after a restart, subject to that database's consistency and availability.

A failure before the response is not always a clean "not accepted." If the database is unavailable, the endpoint fails before it says `202`. But if the connection drops while the commit is in progress, or the response is lost on the way back, the row may exist even though the caller saw an error. The caller then has no confirmation either way, which is exactly why the job is keyed by the caller's idempotency key: a retry with the same key hits the unique index and is answered with the existing job instead of creating a second one.

A durable broker such as a cloud message queue does the same job when the work is already message-shaped or other services need to consume it. Return `202` only after the broker has acknowledged the message under its configured persistence and replication settings. If the application also keeps its own record of the job, such as the row behind the status page, use a transactional outbox: commit the job row and an outbox row in the same database transaction, and let a separate publisher deliver the outbox message to the broker afterward, retrying until it succeeds. The acceptance point is that commit. Publication is eventual and may happen more than once, so consumers must tolerate duplicates.

Durable acceptance moves the hard questions rather than removing them:

- **Work can run more than once.** Many durable job and messaging designs provide at-least-once processing in practice, because recovery retries work whose final outcome was not recorded. A worker that crashes after the mail service accepted the statement but before recording `Sent` leaves a job that looks unfinished, and the application may submit the statement a second time, potentially delivering it twice, unless the send is idempotent, for example, with a provider-supported idempotency key, or is reconciled with the provider before being retried.
- **Stuck work needs a rule.** A claimed job whose worker died needs a lease or timeout, and a cap on attempts, so it is retried a bounded number of times and then marked failed for a person to look at.
- **The backlog still needs a limit.** A table can grow without bound just as a channel can. Decide what happens when the backlog is too large, and monitor both its size and the age of its oldest pending job; age is usually the more direct sign of delay that users will notice.
- **Graceful shutdown still matters.** It no longer decides whether the work is lost, but it decides how often work is interrupted and retried.

Those are deeper topics with their own articles. [Canceling a Queued Job Can Race With Execution](revoke-job-after-it-enters-queue.md) shows a conditional claim and what recovery after a crash must not do. [A Payment API Timeout Leaves the Outcome Unknown](payment-api-timeout-unknown-outcome.md) covers an external call whose outcome is unknown after an interruption, which is what the statement email is when the worker dies mid-send. [Your Retry Logic May Be Replaying an Authorization Decision](retry-logic-may-replay-authorization-decision.md) covers what each retry layer knows.

---

## A Deployment Choice: A Separate Worker or Scheduler

The durable job above can be processed by a `BackgroundService` inside the web application. Moving the work into a separate process, such as a .NET Worker Service, a serverless function triggered by the queue, or a job library with persistent storage and its own servers, is an operational decision, not a fourth guarantee. It is justified when:

- **the work is long or heavy** enough to compete with request handling for CPU, memory, or database connections;
- **the work must scale independently**, for example, ten workers during month-end close and one the rest of the month;
- **web deployments should not interrupt it**, because the web tier is deployed several times a day and statements take minutes;
- **the work needs a schedule**, such as "generate every customer's statement at 02:00 on the first," which belongs to something that runs whether or not anyone made a request;
- **the work has different permissions**, such as access to the mail relay that the web tier should not have.

A separate process adds a deployment, monitoring, configuration, and a contract between the two. It does not add durability by itself. A separate worker that reads from a non-durable source, such as its own in-memory queue fed over HTTP, is still Option 2, with more moving parts.

---

## Choosing

| | Caller learns the outcome | Survives a restart | Response time | Backpressure | Failure visibility | Operational cost |
| --- | --- | --- | --- | --- | --- | --- |
| **Await in the request** | Yes, in the response, unless the connection fails | No; an interrupted request leaves the caller without an answer and the outcome possibly unknown | As long as the work | Server and connection limits | Normal error handling | None added |
| **Discarded `Task.Run`** | No | No, and no record of the loss | Milliseconds | None | None through the normal error path | None, until the incident |
| **Bounded channel and `BackgroundService`** | Through a status resource, while the process lives | No; a graceful stop can report what is lost | Milliseconds | Queue capacity; refuse with `503` | Logged and recorded per item | Low: one hosted service |
| **Durable job or durable queue** | Through a status resource that survives restarts | Yes, within the store's durability | Milliseconds plus one durable write | Backlog limits and alerts | Recorded in the store, retried, then failed | Moderate: schema or broker, claims, retries, idempotency |
| **Separate worker or scheduler** | Same as the store it reads | Same as the store it reads | Same as the store it reads | Worker count and backlog limits | Same, plus its own monitoring | Higher: another deployable |

Two questions decide most cases. **Must the caller know the outcome before moving on?** Then await it in the request, and make the request fast enough or the client patient enough. **If the process restarts right after `202`, is losing the work acceptable?** If yes, the bounded in-memory queue is enough, and simpler. If no, store the job before answering.

---

## When Losing the Work Is Acceptable

Plenty of background work can disappear without harm: refreshing a cache entry that the next request would rebuild anyway, recalculating a dashboard aggregate that a scheduled job also recalculates, sending a "someone viewed your profile" notification, or warming a search index. For work like that, durable storage may add more complexity than the loss tolerance justifies, and recommending it by reflex would be bad advice.

The simplest valid design for disposable work is still not `_ = Task.Run(...)`, because nothing supervises that. For an ASP.NET Core application without an existing job system, the simplest supervised design for best-effort, in-process work is the bounded channel and `BackgroundService` above, possibly without the status resource. An application that already runs a scheduler or broker may find that route simpler. Either way, it needs:

- a **bounded** queue, so the number of buffered items cannot grow without limit, with a full mode chosen on purpose; for a cache refresh, dropping an item may be exactly right;
- **a scope per item**, so nothing uses a disposed service;
- **failures caught, logged, and counted** at the item boundary, so a broken item is visible and does not stop the host;
- **the stopping token honored**, so shutdown is prompt and does not start new work;
- **one sentence of documentation** that says the loss is accepted, next to the code that makes it:

```csharp
// Cache warm-up requests are best-effort. They are lost on restart, and a full queue discards the
// incoming request (DropWrite). A miss is rebuilt on the next read, so loss costs latency, not correctness.
```

That sentence is the difference between a tradeoff and an accident. The opening example made the same tradeoff for month-end statements without anyone writing it down, and the first time anyone read it was in a customer's complaint.

---

## Failure Modes

**1. The discarded task.** `_ = Task.Run(...)` or an un-awaited `async` call returns `202` for work that nothing in the application tracks. A deployment, scale-in, crash, or recycle can erase it, and its exceptions are not logged unless something outside the normal error path catches them.

**2. A request-scoped service in detached work.** The background code uses a `DbContext`, `HttpContext`, or other scoped service captured from the request, and fails with `ObjectDisposedException` after the response, or worse, works some of the time.

**3. A queue with no limit, or a limit that drops silently.** `Channel.CreateUnbounded` or a `ConcurrentQueue` grows during a burst until the process runs out of memory; a bounded channel in a drop mode reports success for an item it discarded.

**4. A worker that ends on the first bad item.** An exception escapes a `BackgroundService`'s `ExecuteAsync`. Since .NET 6, it is logged and, by default, stops the host. With `BackgroundServiceExceptionBehavior.Ignore`, it is still logged and the host keeps running, but that worker has ended and is not restarted, so the endpoint can keep accepting work that nothing will process.

**5. A shutdown that keeps starting work.** The worker loops over buffered items without checking the stopping token, so items start during shutdown and are interrupted moments later.

**6. Calling host-managed work durable.** The team believes a `BackgroundService` survives restarts because the host "manages" it, and documents a promise the design does not keep.

**7. A `202` that promises nothing in particular.** The response has no identifier and no status resource, and the API documentation does not say what can be lost, so clients cannot tell an accepted request from a forgotten one.

---

## A Short Review Checklist

1. Is every task the request starts either awaited by the request or handed to something the host owns?
2. Does detached work carry only plain data, and resolve its services in a scope created for that work item?
3. Is the in-memory queue bounded, and is the behavior when it is full a deliberate choice, visible to the caller?
4. Is every work item's failure caught at the item boundary, logged with its identifier, and recorded where someone can see it?
5. On a graceful shutdown, does the worker stop accepting and starting new items, honor the stopping token in the work it runs, and record what was queued but never started?
6. Does the `202` response include an identifier and a status location, and does the API documentation say how long each lasts and whether the work survives a restart?
7. If losing the work is not acceptable, is the job stored durably *before* the response, keyed so that a retried request finds it, and is the work safe to run more than once?
8. If losing the work is acceptable, is that written down next to the code?
9. Are there tests for a full queue, a failing item, and a host stopping with work pending, and is the crash case documented, or covered by a process-level test?

---

## Continue Deeper

The scope rule in this article is one case of a broader one: a service must not outlive what it depends on. [Your Dependency Injection Lifetime Is an Ownership Decision](dependency-injection-lifetime-is-ownership-decision.md) explains how transient, scoped, and singleton decide sharing, concurrency, and disposal, what scope validation catches, and how to test the instance identities a design depends on.

When the work crosses from the request into a job, the job also needs to carry the right identity. [A Background Job Should Not Inherit Everything the User Could Do](background-job-should-not-inherit-user-authority.md) covers what the job should record about who asked, and how the worker re-checks that before it starts.

Once the job is durable, two workers can race to claim it, and an operator may try to cancel it. [Canceling a Queued Job Can Race With Execution](revoke-job-after-it-enters-queue.md) shows the conditional claim that decides those races and what recovery must not do after a crash.

When a worker dies during an external call, such as sending the statement email, [A Payment API Timeout Leaves the Outcome Unknown](payment-api-timeout-unknown-outcome.md) explains how to record and resolve an outcome nobody observed, and [Your Retry Logic May Be Replaying an Authorization Decision](retry-logic-may-replay-authorization-decision.md) covers what each retry layer can and cannot know.

### Related Work

- [Background tasks with hosted services in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services), in the ASP.NET Core documentation, describes `IHostedService` and `BackgroundService`, shutdown and its timeout, the fact that `StopAsync` might not be called if the process fails, consuming scoped services from a background task, and a queued background task built on a bounded channel.
- [.NET Generic Host in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host), in the ASP.NET Core documentation, describes the graceful shutdown sequence, including how long the server waits for in-flight requests, and the configurable shutdown timeout.
- [Dependency injection in ASP.NET Core: Request Services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection#request-services) explains that the framework creates a scope per request and that scoped services are valid only while the request is active.
- [Exception handling in hosting](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/hosting-exception-handling), a .NET 6 breaking-change note, describes how an unhandled exception in `BackgroundService.ExecuteAsync` is logged and stops the host by default, and how `BackgroundServiceExceptionBehavior` changes that.
- [BoundedChannelFullMode](https://learn.microsoft.com/en-us/dotnet/api/system.threading.channels.boundedchannelfullmode), in the .NET API reference, lists what a bounded channel does when it is full.
- [RFC 9110, section 15.3.3: 202 Accepted](https://www.rfc-editor.org/rfc/rfc9110#section-15.3.3) defines what the status code does and does not commit the server to.

---

## The Short Answer

`Task.Run` queues work to the thread pool. It does not give the work an owner. A task discarded without being handed to anything that tracks it is not awaited at shutdown, its failures bypass the normal error path, it may use services the request has already disposed, and it ends with the process.

Pick one of three guarantees on purpose. If the caller needs the outcome, finish the work in the request. If losing the work on restart is acceptable, use a bounded in-memory queue processed by a `BackgroundService`, with a scope per item, failures recorded, and the loss written down. If it is not acceptable, store the job durably before you return `202`, key it so a retry finds it, and make the work safe to run twice. Then test the guarantee you chose, including the part where it fails.

---

## Appendix: Types, Fakes, and Test Helpers

These are the remaining definitions the examples use. To reproduce the suite, put the article's listings and these in an xUnit v3 test project (`OutputType` `Exe`, the `Microsoft.Testing.Platform` runner) with a framework reference to `Microsoft.AspNetCore.App` and package references to `Microsoft.AspNetCore.TestHost` and, for the durable sketch, `Microsoft.EntityFrameworkCore`; add the usual `using` directives, and place the tests in test classes. That is how the twelve test cases were run and the sketch was compiled.

### Application types

```csharp
public sealed record ReportCommand(string CustomerId, string Email);

public sealed record ReportRequest(Guid ReportId, string CustomerId, string Email);

public interface IReportGenerator
{
    Task<byte[]> GenerateAsync(ReportRequest request, CancellationToken cancellationToken);
}

public interface IEmailSender
{
    Task SendAsync(string to, byte[] attachment, CancellationToken cancellationToken);
}

public enum ReportState { Queued, Running, Sent, Failed, Interrupted, NotStarted }

public sealed class ReportStatusStore
{
    private readonly ConcurrentDictionary<Guid, ReportState> states = new();

    public void Set(Guid reportId, ReportState state) => states[reportId] = state;

    public void Remove(Guid reportId) => states.TryRemove(reportId, out _);

    public ReportState? Get(Guid reportId) => states.TryGetValue(reportId, out ReportState s) ? s : null;
}
```

The two route-mapping helpers contain the endpoints shown earlier, unchanged:

```csharp
public static class ReportEndpoints
{
    public static void MapFireAndForgetReports(this WebApplication app)
    {
        app.MapPost("/reports", (ReportCommand command, IReportGenerator generator, IEmailSender email) =>
        {
            var request = new ReportRequest(Guid.NewGuid(), command.CustomerId, command.Email);

            _ = Task.Run(async () =>
            {
                byte[] pdf = await generator.GenerateAsync(request, CancellationToken.None);
                await email.SendAsync(request.Email, pdf, CancellationToken.None);
            });

            return Results.Accepted(value: new { request.ReportId });
        });
    }

    public static void MapQueuedReports(this WebApplication app)
    {
        app.MapPost("/reports", (ReportCommand command, ReportQueue queue, ReportStatusStore statuses, HttpResponse response) =>
        {
            var request = new ReportRequest(Guid.NewGuid(), command.CustomerId, command.Email);

            statuses.Set(request.ReportId, ReportState.Queued);   // before the worker can see it
            if (!queue.TryEnqueue(request))
            {
                statuses.Remove(request.ReportId);   // full, or closed for shutdown: not accepted
                response.Headers.RetryAfter = "30";   // illustrative; base it on how quickly capacity returns
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Report processing is temporarily unavailable. Try again shortly.");
            }

            return Results.Accepted($"/reports/{request.ReportId}", new { request.ReportId, Status = "Queued" });
        });

        app.MapGet("/reports/{reportId:guid}", (Guid reportId, ReportStatusStore statuses) =>
            statuses.Get(reportId) is { } state
                ? Results.Ok(new { reportId, Status = state.ToString() })
                : Results.NotFound());
    }
}
```

### Durable-sketch definitions

These complete the Option 3 sketch for compilation. The duplicate-key check is deliberately left for your database provider.

```csharp
public enum ReportJobStatus { Pending, Running, Sent, Failed }

public sealed class ReportJob
{
    public Guid Id { get; set; }
    public required string CallerIssuer { get; set; }
    public required string CallerSubject { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string Fingerprint { get; set; }
    public required string CustomerId { get; set; }
    public required string Email { get; set; }
    public ReportJobStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ReportsDbContext(DbContextOptions<ReportsDbContext> options) : DbContext(options)
{
    public DbSet<ReportJob> ReportJobs => Set<ReportJob>();

    protected override void OnModelCreating(ModelBuilder model) =>
        model.Entity<ReportJob>()
            .HasIndex(j => new { j.CallerIssuer, j.CallerSubject, j.IdempotencyKey }, "UX_ReportJobs_Caller_IdempotencyKey")
            .IsUnique();   // the database enforces uniqueness
}

public static class ReplayContract
{
    public static IResult Existing(ReportJob job) =>
        job.Status is ReportJobStatus.Pending or ReportJobStatus.Running
            ? Results.Accepted($"/reports/{job.Id}", new { ReportId = job.Id, Status = job.Status.ToString() })
            : Results.Ok(new { ReportId = job.Id, Status = job.Status.ToString() });
}

public static class DuplicateKeys
{
    // Provider-specific. For PostgreSQL with Npgsql, for example: the inner PostgresException has
    // SqlState "23505" and ConstraintName "UX_ReportJobs_Caller_IdempotencyKey". Never treat every
    // DbUpdateException as a duplicate.
    public static bool IsIdempotencyKeyViolation(DbUpdateException ex) =>
        throw new NotImplementedException("Supply the check for your database provider.");
}
```

### Fakes

```csharp
public sealed class ReportBehavior
{
    public HashSet<string> FailFor { get; } = [];
    public bool WaitForRelease { get; set; }
    public bool IgnoreCancellation { get; set; }
    public bool ThrowOnDispose { get; set; }
    public TaskCompletionSource ReleaseGeneration { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<ReportRequest> GenerationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ScopeDisposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<Exception> BackgroundFault { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed class FakeReportGenerator(ReportBehavior behavior) : IReportGenerator, IDisposable
{
    private bool disposed;

    public async Task<byte[]> GenerateAsync(ReportRequest request, CancellationToken cancellationToken)
    {
        behavior.GenerationStarted.TrySetResult(request);
        using CancellationTokenRegistration observe =
            cancellationToken.Register(() => behavior.CancellationObserved.TrySetResult());

        if (behavior.WaitForRelease)
        {
            await (behavior.IgnoreCancellation
                ? behavior.ReleaseGeneration.Task
                : behavior.ReleaseGeneration.Task.WaitAsync(cancellationToken));
        }

        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);   // like a DbContext after its scope ends
        }
        catch (Exception ex)
        {
            behavior.BackgroundFault.TrySetResult(ex);
            throw;
        }

        if (behavior.FailFor.Contains(request.CustomerId))
        {
            throw new InvalidOperationException($"No data for {request.CustomerId}");
        }

        return [1, 2, 3];
    }

    public void Dispose()
    {
        disposed = true;
        behavior.ScopeDisposed.TrySetResult();
        if (behavior.ThrowOnDispose)
        {
            throw new InvalidOperationException("Connection pool was already shut down");
        }
    }
}

public sealed class RecordingEmailSender : IEmailSender
{
    public ConcurrentQueue<string> Sent { get; } = new();
    public TaskCompletionSource<string> FirstSent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task SendAsync(string to, byte[] attachment, CancellationToken cancellationToken)
    {
        Sent.Enqueue(to);
        FirstSent.TrySetResult(to);
        return Task.CompletedTask;
    }
}

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<(LogLevel Level, string Message, Exception? Error)> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(this);

    public void Dispose() { }

    private sealed class Logger(CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            owner.Entries.Enqueue((logLevel, formatter(state, exception), exception));
    }
}
```

### Worker test helpers

```csharp
private static (IHost Host, ReportQueue Queue, ReportStatusStore Statuses, ReportBehavior Behavior,
    RecordingEmailSender Email, CapturingLoggerProvider Logs) CreateHost(int capacity = 10)
{
    var queue = new ReportQueue(capacity);
    var statuses = new ReportStatusStore();
    var behavior = new ReportBehavior();
    var email = new RecordingEmailSender();
    var logs = new CapturingLoggerProvider();

    HostApplicationBuilder builder = Host.CreateApplicationBuilder();
    builder.Logging.ClearProviders();
    builder.Logging.AddProvider(logs);
    builder.Services.AddSingleton(queue);
    builder.Services.AddSingleton(statuses);
    builder.Services.AddSingleton(behavior);
    builder.Services.AddScoped<IReportGenerator, FakeReportGenerator>();
    builder.Services.AddSingleton<IEmailSender>(email);
    builder.Services.AddHostedService<ReportWorker>();
    return (builder.Build(), queue, statuses, behavior, email, logs);
}

private static ReportRequest Report(string customerId) => new(Guid.NewGuid(), customerId, $"{customerId}@example.com");
```

### Additional tests

```csharp
[Fact]
public async Task A_cleanup_failure_after_the_send_does_not_erase_the_send()
{
    var (host, queue, statuses, behavior, email, logs) = CreateHost();
    behavior.ThrowOnDispose = true;
    ReportRequest report = Report("C-1");
    statuses.Set(report.ReportId, ReportState.Queued);
    Assert.True(queue.TryEnqueue(report));

    await host.StartAsync(TestContext.Current.CancellationToken);
    await behavior.ScopeDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    await host.StopAsync(TestContext.Current.CancellationToken);
    host.Dispose();

    Assert.Single(email.Sent);
    Assert.Equal(ReportState.Sent, statuses.Get(report.ReportId));
    Assert.Contains(logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("cleanup failed"));
}

[Fact]
public async Task A_cleanup_failure_does_not_hide_the_work_failure()
{
    var (host, queue, statuses, behavior, email, logs) = CreateHost();
    behavior.FailFor.Add("C-broken");
    behavior.ThrowOnDispose = true;
    ReportRequest report = Report("C-broken");
    statuses.Set(report.ReportId, ReportState.Queued);
    Assert.True(queue.TryEnqueue(report));

    await host.StartAsync(TestContext.Current.CancellationToken);
    await behavior.ScopeDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    await host.StopAsync(TestContext.Current.CancellationToken);
    host.Dispose();

    Assert.Equal(ReportState.Failed, statuses.Get(report.ReportId));
    Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error && e.Error?.Message == "No data for C-broken");
    Assert.Contains(logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("cleanup failed"));
}

[Fact]
public async Task A_scope_creation_exception_is_caught_at_the_item_boundary()
{
    var behavior = new ReportBehavior();
    var email = new RecordingEmailSender();
    var logs = new CapturingLoggerProvider();
    using ServiceProvider root = new ServiceCollection()
        .AddSingleton(behavior)
        .AddScoped<IReportGenerator, FakeReportGenerator>()
        .AddSingleton<IEmailSender>(email)
        .BuildServiceProvider();
    var scopes = new FailingOnceScopeFactory(root.GetRequiredService<IServiceScopeFactory>());
    var queue = new ReportQueue(capacity: 10);
    var statuses = new ReportStatusStore();
    using var loggerFactory = new LoggerFactory([logs]);
    using var worker = new ReportWorker(queue, scopes, statuses, loggerFactory.CreateLogger<ReportWorker>());

    ReportRequest first = Report("C-1");
    ReportRequest second = Report("C-2");
    Assert.True(queue.TryEnqueue(first));
    Assert.True(queue.TryEnqueue(second));

    await worker.StartAsync(TestContext.Current.CancellationToken);
    await email.FirstSent.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    await worker.StopAsync(TestContext.Current.CancellationToken);

    Assert.Equal(ReportState.Failed, statuses.Get(first.ReportId));    // not left Running
    Assert.Equal(ReportState.Sent, statuses.Get(second.ReportId));     // and the worker survived it
}

public sealed class FailingOnceScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
{
    private int calls;

    public IServiceScope CreateScope() =>
        Interlocked.Increment(ref calls) == 1
            ? throw new InvalidOperationException("Scope activation failed once")   // an item-local, one-time failure
            : inner.CreateScope();
}

[Theory]
[InlineData(BoundedChannelFullMode.DropWrite, new[] { 1, 2 })]    // the incoming item is discarded
[InlineData(BoundedChannelFullMode.DropNewest, new[] { 1, 3 })]   // the newest queued item makes room
[InlineData(BoundedChannelFullMode.DropOldest, new[] { 2, 3 })]   // the oldest queued item makes room
public void Every_drop_mode_reports_success_while_discarding_an_item(BoundedChannelFullMode mode, int[] survivors)
{
    var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(2) { FullMode = mode });

    Assert.True(channel.Writer.TryWrite(1));
    Assert.True(channel.Writer.TryWrite(2));
    Assert.True(channel.Writer.TryWrite(3));   // "accepted"...
    channel.Writer.Complete();

    List<int> remaining = [];
    while (channel.Reader.TryRead(out int item))
    {
        remaining.Add(item);
    }

    Assert.Equal(survivors, remaining);        // ...but one of the three items is gone
}

[Fact]
public void Wait_mode_refuses_immediately_when_full()
{
    var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.Wait });

    Assert.True(channel.Writer.TryWrite(1));
    Assert.False(channel.Writer.TryWrite(2));  // TryWrite never waits; WriteAsync would
}
```

### Shutdown harness

This file-based program (`dotnet run --file harness.cs`, .NET 10 SDK) produced the shutdown observations in the article. It runs real Kestrel, not `TestServer`, starts a twelve-second request, and calls `StopAsync` directly one second later. Pass a number of seconds to set `HostOptions.ShutdownTimeout`; omit it for the default.

```csharp
#:sdk Microsoft.NET.Sdk.Web
#:property TargetFramework=net10.0
using System.Diagnostics;

var stopwatch = Stopwatch.StartNew();
void Log(string message) => Console.WriteLine($"{stopwatch.Elapsed.TotalSeconds,6:F2}s  {message}");

var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
if (args.Length > 0)
{
    builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(int.Parse(args[0])));
}
builder.Services.AddSingleton<Action<string>>(Log);
builder.Services.AddHostedService<ProbeWorker>();

var app = builder.Build();
app.Lifetime.ApplicationStopping.Register(() => Log("ApplicationStopping"));
app.MapGet("/slow", async () => { await Task.Delay(TimeSpan.FromSeconds(12)); Log("request finished"); return "done"; });
await app.StartAsync();

using var client = new HttpClient();
Task<string> request = Task.Run(async () =>
{
    try { return $"HTTP {(int)(await client.GetAsync(app.Urls.First() + "/slow")).StatusCode}"; }
    catch (HttpRequestException) { return "connection closed before a response"; }
});

await Task.Delay(TimeSpan.FromSeconds(1));
Log("StopAsync called");
await app.StopAsync();
Log("StopAsync returned");
Log($"client saw: {await request.WaitAsync(TimeSpan.FromSeconds(30))}");

sealed class ProbeWorker(Action<string> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using CancellationTokenRegistration registration = stoppingToken.Register(() => log("worker stopping token canceled"));
        try { await Task.Delay(Timeout.Infinite, stoppingToken); } catch (OperationCanceledException) { }
    }
}
```

With the default timeout on .NET 10, it printed `ApplicationStopping` at once, `request finished` and `HTTP 200` about eleven seconds later, and the worker's token was canceled only after that. With `3`, the worker's token was canceled and the client saw the connection close without a response about four seconds after `StopAsync` was called. Configuring the timeout through the web host's `UseShutdownTimeout` instead of `HostOptions.ShutdownTimeout` produced the same result in this .NET 10 test, and no separate Kestrel shutdown-timeout option was involved. When the host receives a supported graceful-termination signal, such as SIGTERM, it enters the same shutdown path, but the hosting platform's own grace period and eventual forced termination still apply.
