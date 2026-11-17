---
description: A discarded Task.Run in an ASP.NET Core endpoint is async but not durable. Choose between awaiting, a bounded in-memory queue, and storing the job.
title: Task.Run Does Not Make ASP.NET Core Work Durable
author: Christopher D. Cavell
published: "2026-11-17"
summary: Starting work with Task.Run and returning 202 Accepted makes an ASP.NET Core endpoint fast, but the work still lives only in that process. Nothing waits for it at shutdown, a request-scoped service it captured may already be disposed, its failure reaches no one, and a crash or deployment can erase it without a trace. Choose deliberately among finishing the work inside the request, handing it to a bounded in-memory queue that a hosted service drains, and recording it durably before you answer. Each gives a different guarantee, and the right choice depends on whether losing the work on restart is acceptable. State that guarantee, and test it.
feed: true
x_post: Task.Run plus 202 Accepted makes an endpoint fast, not durable. The work dies with the process. Decide if that loss is OK.
x_hashtags:
  - DotNet
  - AspNetCore
x_question: "What happens to your background work during a deploy?"
---

# Task.Run Does Not Make ASP.NET Core Work Durable

**Pattern classification:** General learning material

**Difficulty:** Beginner to intermediate

**Prerequisites:** No formal prerequisites. Familiarity with ASP.NET Core minimal APIs, `async` and `await`, and dependency injection is helpful. No AsiBackbone package or prior Learning material is required.

**Sample scope:** The C# targets .NET 8 or later. The fire-and-forget endpoint, the bounded queue, the worker, and the four tests in this article were compiled and run on .NET 10 with xUnit v3 and `Microsoft.AspNetCore.TestHost`; the fakes they use are described in the testing section. The durable-job sketch near the end is illustrative and was not run. Framework behavior is described from Microsoft's documentation on the verification date below; check it for your version. The reporting application, the customers, and the timings are hypothetical. Original prose is licensed under [Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/), and original code examples are additionally offered under the [MIT License](https://opensource.org/license/mit), as described in the repository's [licensing terms](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md). Third-party material, where identified, remains under its original license. External sources were last verified on November 17, 2026.

**What this article covers:** an endpoint that returns `202 Accepted` before an unobserved task finishes, and the month-end statements it lost; the difference between scheduling work on the thread pool, running it under the host's management, and accepting it durably; why detached work must not use a request-scoped service, and how to create a scope for each work item; a bounded in-memory queue with backpressure, failure recording, and an honest shutdown; deterministic tests for a full queue, a failed work item, and work still pending when the host stops; what `202 Accepted` does and does not promise; a status resource with a stable identifier; storing the job before answering; when a separate worker or scheduler is worth it; and when losing the work on restart is an acceptable, documented tradeoff.

**In plain terms:** a waiter takes your order and says "coming right up." If the waiter only memorized it, the order is gone when the waiter's shift ends early or they trip on the way to the kitchen, and nobody else ever knew about it. If the waiter pinned a ticket on the kitchen rail, the kitchen knows, works through tickets in order, and stops taking new ones when the rail is full, but the tickets still go in the bin if the kitchen closes or the power fails. If the order was entered into the till, it survives the shift change, and anyone can look it up. All three waiters said the same words. Only one of them made a promise that outlasts the kitchen.

Consider a hypothetical accounting application. At month-end close, staff request statements for their customers. Generating a statement means querying a few thousand ledger lines, rendering a PDF, and emailing it, which takes ten to forty seconds. The original endpoint did all of that inside the request, and the browser waited. A developer made it responsive with one change:

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

The endpoint now answers in a few milliseconds, the screen says "Your statement will be emailed in a few minutes," and in development, every statement arrives.

On the last business day of October, operations deployed a fix to an unrelated page at 16:58, while staff were working through the close:

```text
16:57:31.004  api-2   POST /reports  C-1180 → 202 Accepted  R-5521
16:57:49.611  api-2   POST /reports  C-1192 → 202 Accepted  R-5524
16:58:02.377  api-2   POST /reports  C-1207 → 202 Accepted  R-5527
16:58:40.019  deploy  Draining api-2 for rolling update
16:58:40.020  api-2   Application is shutting down...
16:58:40.311  api-2   Waiting for 0 in-flight requests
16:58:40.413  api-2   Process exited with code 0
```

That log is complete. There is no error for `R-5521`, `R-5524`, or `R-5527`, because nothing failed in a way the application could see: the process ended normally, and the three tasks ended with it. The shutdown waited for in-flight requests, and there were none; each request had finished the moment it returned `202`. Fourteen statements were lost that afternoon across two instances. The application found out on November 3, when customers called.

The same week, a statement failed when the mail relay timed out. That task's exception was stored in a `Task` object that nobody held, so it was never logged, never retried, and never shown to the person who asked.

> **`Task.Run` changes which thread runs the work. It does not change who owns it. A task you discard is owned by nothing: the host does not wait for it, no one observes its failure, and nothing remembers it if the process ends. Before returning `202 Accepted`, decide which of three guarantees you need, finished in the request, owned by the host, or recorded durably, and build that one deliberately.**

---

## Three Different Things Called "Background"

"Run it in the background" can mean three things that are easy to confuse because all three return quickly.

| What it is | In .NET | Who knows the work exists | What ends it without finishing |
| --- | --- | --- | --- |
| **Thread-pool scheduling** | `Task.Run`, an `async` method called without `await` | Only the code holding the `Task`, and in fire-and-forget code, nobody | Process exit, for any reason; an unhandled exception that no one observes |
| **Host-managed execution** | A `BackgroundService` or other `IHostedService` | The host, which starts it, signals it to stop, and waits for it during a graceful shutdown | Process exit without a graceful shutdown, or a shutdown that outlasts the timeout |
| **Durable acceptance** | A row in a database, a message in a durable broker, a job in a library with persistent storage | The store, which survives the process | Only the loss of the store itself, within its durability guarantees |

The first is about *which thread* runs the work. The second is about *which lifetime* the work belongs to. The third is about *whether the request is remembered* outside the process that accepted it. Asynchronous code gives you the first. It gives you neither of the others.

The [ASP.NET Core hosted-services documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services) describes what host management adds: hosted services are started with the application, their `StopAsync` is called during a graceful shutdown, and `BackgroundService` exposes a stopping token and is awaited before the host finishes stopping, for up to a default 30-second timeout. The same page also states the limit: if the process fails unexpectedly, `StopAsync` might not be called. A host-managed worker is better owned than a detached task. It is not durable.

### What happens to a discarded task

A discarded task has three problems, and only the first is about durability.

**Nothing waits for it.** During a graceful shutdown, ASP.NET Core stops accepting requests, waits for in-flight requests, and stops hosted services. A task started by a request that has already returned is none of those. When the process exits, the task stops wherever it is: before the query, halfway through the PDF, or after the email went out but before anything recorded that it did.

**Nobody observes its failure.** An exception inside a task is stored on the task. If no code awaits it or reads it, the exception is not logged by default, and since .NET Framework 4.5 an unobserved task exception does not crash the process. It surfaces only through `TaskScheduler.UnobservedTaskException`, if anything subscribes, and only when the garbage collector finalizes the task. The caller already has a `202`.

**It may outlive the services it captured.** The next section covers this one, because it often appears as the "fix" for the first two.

---

## Detached Work Must Not Use the Request's Services

Two weeks after the incident, a developer moved statement generation onto the application's EF Core `DbContext` to reuse an existing query. The `DbContext` is registered with a scoped lifetime, as is usual, and the endpoint receives it through dependency injection. The fire-and-forget code did not change.

ASP.NET Core [creates one dependency-injection scope per request](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection#request-services), and its scoped services are valid for as long as the request is active. When the response completes, the scope is disposed, and every `IDisposable` scoped service is disposed with it. The detached task still holds a reference to the generator and, through it, to the `DbContext`. When it reaches the first query, the context has been disposed:

```text
16:12:08.441  api-1  POST /reports  C-1180 → 202 Accepted  R-5602
(no further output)
```

The `ObjectDisposedException` is thrown inside a task that nobody awaits, so the log shows nothing at all, again. In a quiet test environment, a slow first query can occasionally make the work finish first, which is how this kind of bug ships.

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

The worker in the next section does exactly that, with `IServiceScopeFactory.CreateAsyncScope()` for each item, as the [hosted-services documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services#consuming-a-scoped-service-in-a-background-task) recommends.

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

This version has properties that every other option has to work to recover. The caller learns the outcome: a `200` means the statement was sent, and an error means it was not, or that the caller should check. The request's own scope is valid throughout, so the scoped `DbContext` is fine. An exception reaches the normal ASP.NET Core error handling and logging. A graceful shutdown waits for in-flight requests, and the caller sees a failed request if the process dies, so the work is never silently lost; at worst, the caller does not know whether the email went out and has to ask.

The costs are latency and capacity. The caller waits ten to forty seconds. A proxy, load balancer, or client may time out first, leaving an outcome that neither side knows. Each waiting request holds a connection and server resources, so a burst of month-end requests becomes a burst of open connections. If the client disconnects, the request-aborted token passed here cancels the work. That is usually correct for a report someone stopped waiting for, but decide it on purpose: if a send must not be abandoned halfway, do not pass the request's token to that step.

For work that typically finishes in a second or two, this is often the right answer, and it is the baseline the other options should be measured against.

### Where `Task.Run` still belongs

None of this makes `Task.Run` wrong. It is the right tool for running CPU-bound work on the thread pool when the caller *awaits* the result, for example, inside a hosted service or a desktop application, or for starting several independent computations and awaiting them together with `Task.WhenAll`. In those cases, the code that started the task owns it, waits for it, and observes its exceptions. The problem in the opening example is not `Task.Run`. It is the `_ =`, which throws ownership away. (In an ASP.NET Core request, awaiting `Task.Run` around ordinary asynchronous I/O usually adds a thread hop without making anything faster, because the request is already running on the thread pool.)

---

## Option 2: A Bounded In-Memory Queue Owned by the Host

If the caller should not wait, the next step is to hand the work to something the host owns: a `BackgroundService` that reads from a bounded `Channel<T>`. This is the pattern in the [queued background tasks](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services#queued-background-tasks) section of the hosted-services documentation, with three additions this example needs: a full queue is refused rather than waited on, each item's outcome is recorded, and a graceful shutdown says what it is about to lose.

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

The capacity is the backpressure. Without one, the queue grows until the process runs out of memory, and every item in it is another statement lost in the next restart. With one, the application has to decide what to do when it is full, and there are only a few honest answers:

- **Refuse the request.** `TryWrite` returns `false` when a channel created with `BoundedChannelFullMode.Wait` is full. The endpoint returns `503 Service Unavailable` with a `Retry-After` header, and the caller knows the statement was not accepted.
- **Wait for space.** `WriteAsync` waits until the worker makes room, which is what the documentation's sample does. That pushes the backpressure onto the request, which is reasonable when the queue rarely fills, but under sustained load it reintroduces the long-held requests that Option 1 had.
- **Drop something, deliberately.** The other [`BoundedChannelFullMode`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.channels.boundedchannelfullmode) values discard the newest item already queued, the oldest, or the item being written. That suits telemetry samples or cache refreshes where the newest request supersedes the older ones. It does not suit statements. Note that with `DropWrite`, `TryWrite` still returns `true` for an item it discarded, so an endpoint that checks the return value would report it as accepted.

### The endpoint and the status resource

```csharp
app.MapPost("/reports", (ReportCommand command, ReportQueue queue, ReportStatusStore statuses, HttpResponse response) =>
{
    var request = new ReportRequest(Guid.NewGuid(), command.CustomerId, command.Email);

    statuses.Set(request.ReportId, ReportState.Queued);   // before the worker can see it
    if (!queue.TryEnqueue(request))
    {
        statuses.Remove(request.ReportId);
        response.Headers.RetryAfter = "30";
        return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Too many reports are waiting. Try again shortly.");
    }

    return Results.Accepted($"/reports/{request.ReportId}", new { request.ReportId, Status = "Queued" });
});

app.MapGet("/reports/{reportId:guid}", (Guid reportId, ReportStatusStore statuses) =>
    statuses.Get(reportId) is { } state
        ? Results.Ok(new { reportId, Status = state.ToString() })
        : Results.NotFound());
```

The `202` now carries a `Location` header pointing at a status resource, and the report ID in that URL is a stable identifier for this piece of work. The status is written *before* the item is enqueued, because a worker can pick it up immediately. If the endpoint wrote `Queued` afterward, it could overwrite `Sent`.

`ReportStatusStore` is a singleton wrapping a `ConcurrentDictionary<Guid, ReportState>`. That is enough to show the pattern, and it has the same weakness as the queue: it lives in this process. Behind a load balancer, a status request that lands on a different instance gets `404`, and after a restart every status is gone. Entries also need to be removed eventually, or the dictionary becomes a slow memory leak. If people depend on the status page, the status belongs in shared storage, and at that point you are most of the way to Option 3.

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
        try
        {
            while (!stoppingToken.IsCancellationRequested && await queue.Reader.WaitToReadAsync(stoppingToken))
            {
                // Stop taking new items once shutdown begins; buffered items do not check the token.
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
            // Runs only when the host stops gracefully. A crash skips it.
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
        try
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();   // one scope per work item
            var generator = scope.ServiceProvider.GetRequiredService<IReportGenerator>();
            var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            byte[] pdf = await generator.GenerateAsync(request, stoppingToken);
            await email.SendAsync(request.Email, pdf, stoppingToken);

            statuses.Set(request.ReportId, ReportState.Sent);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            statuses.Set(request.ReportId, ReportState.Interrupted);   // the email may or may not have gone
            logger.LogWarning("Report {ReportId} was interrupted by shutdown", request.ReportId);
        }
        catch (Exception ex)
        {
            statuses.Set(request.ReportId, ReportState.Failed);
            logger.LogError(ex, "Report {ReportId} failed", request.ReportId);
        }
    }
}
```

Register it with `builder.Services.AddSingleton(new ReportQueue(capacity: 100))`, `builder.Services.AddSingleton<ReportStatusStore>()`, and `builder.Services.AddHostedService<ReportWorker>()`.

Four details carry the design:

- **One scope per item.** Each statement gets a fresh scope, so it gets a fresh `DbContext`, and a failure in one statement cannot leave a broken context behind for the next.
- **Every item's failure is caught, recorded, and logged, and the loop continues.** This is deliberate. Since .NET 6, an exception that escapes `ExecuteAsync` is logged and, by default, [stops the whole host](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/hosting-exception-handling). One bad customer record should not take the web application down with it. The worker catches at the item boundary, so a failure becomes an observable `Failed` status instead.
- **The loop checks the stopping token before taking each item.** A channel that already holds items hands them out without waiting, so a loop like `await foreach (var item in reader.ReadAllAsync(stoppingToken))` keeps returning buffered items after shutdown has begun, and each one would start and be interrupted immediately. The first version of this worker had exactly that bug; the shutdown test below found it.
- **A graceful shutdown says what it is losing.** When the stopping token fires, the running item receives it, and the `finally` block closes the queue, so later `TryEnqueue` calls fail, and records every item that never started. Those records go to the log, which usually outlives the process. The in-memory statuses do not.

### What this example guarantees, and what it may lose

Stated plainly, so it can go in the API documentation:

**It guarantees that:**

- at most 100 statements wait at once, and the 101st is refused with `503` rather than accepted;
- each statement runs in its own dependency-injection scope;
- a statement that fails is logged with its exception and marked `Failed`, and the worker continues with the next one;
- during a graceful shutdown, no new statement is started, the running one is signaled to stop and marked `Interrupted`, and every queued statement that never started is logged by ID.

**It may lose:**

- every queued statement, on any shutdown, graceful or not. A graceful shutdown only records the loss;
- the running statement, which may or may not have been emailed when it was interrupted;
- everything, without any record, if the process crashes, is killed, runs out of memory, or exceeds the shutdown timeout;
- all statuses, on any restart, and any status requested from a different instance.

It never retries. A `Failed` statement stays failed until someone asks again.

That is a real improvement over the opening example: losses are bounded, visible, and logged, and the caller has an identifier to ask about. But nothing about it survives the process. Using `BackgroundService` and `Channel<T>` makes the work *host-managed*. It does not make it durable.

---

## Testing the Guarantee

The guarantees above are claims about failures, so the tests have to cause those failures on purpose. They use three small fakes:

- `FakeReportGenerator` is registered as scoped. It can be told to fail for a particular customer, to wait for a signal before finishing, and to signal when it starts, when its scope is disposed, and when it is used after disposal.
- `RecordingEmailSender` records each address it was asked to send to.
- `CapturingLoggerProvider` keeps every log entry in memory.

None of the tests sleep or poll. Each one waits on a signal from a fake, with a timeout that fails the test if the signal never arrives.

### A full queue

The worker is not registered, so nothing drains the queue during the test.

```csharp
[Fact]
public async Task A_full_queue_rejects_the_request_instead_of_accepting_it()
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder();
    builder.WebHost.UseTestServer();
    builder.Services.AddSingleton(new ReportQueue(capacity: 2));
    builder.Services.AddSingleton<ReportStatusStore>();
    await using WebApplication app = builder.Build();
    app.MapQueuedReports();                         // the POST and GET endpoints shown above
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

The worker tests build a plain host with `Host.CreateApplicationBuilder()`, register the queue, the status store, the fakes, and `ReportWorker`, and replace the logging providers with the capturing one. `CreateHost` and `Report` are small helpers that do that and create a `ReportRequest`.

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

Read the assertions as the documentation of a loss, not a success. Three statements were accepted with `202`, and none was sent. The test proves the loss is reported. It also marks the edge of what an in-process test can prove: it exercises the graceful path only. Nothing inside the process can test what happens when the process is killed, because nothing inside the process runs. That case is "everything in memory is gone, with no log line," and it should be written down rather than tested.

[How to Test That a Denied Operation Never Executes](test-denied-operation-never-executes.md#async-fire-and-forget-and-cancellation-hazards) develops the related technique of asserting that a side effect did *not* happen, including for work dispatched asynchronously.

---

## What `202 Accepted` Promises

[RFC 9110](https://www.rfc-editor.org/rfc/rfc9110#section-15.3.3) defines `202 Accepted` as meaning the request has been accepted for processing, but processing has not been completed, and it may or may not eventually be acted upon. The status code itself promises very little. It does not say the work will survive a restart, will be retried, or will ever finish. HTTP leaves that to the application, so the application has to say it.

The opening endpoint and the bounded queue both returned `202`. They meant different things, and neither said which. A useful `202` response, and the documentation behind it, answers four questions:

1. **What identifies this work?** A stable ID, returned in the body and in the `Location` of a status resource.
2. **Where can the caller check on it?** The status resource, and how long that status is kept.
3. **What can still happen to it?** For the bounded queue: "may be lost if the service restarts before it is sent." For a durable job: "will be attempted until it succeeds or fails permanently."
4. **What should the caller do if it never completes?** Ask again, contact support, or wait for a retry the server will make.

If the honest answer to the third question is "it may be lost, and nobody will know," the client deserves to be told that in the API documentation, not discover it on November 3.

Stable identifiers matter in the other direction too. If the client's `POST` times out, it does not know whether the statement was accepted, and retrying creates a second statement. If duplicates matter, let the client send its own identifier, such as an `Idempotency-Key` header or a client-generated report ID, and have the server return the existing report instead of creating a new one. With the in-memory queue, that deduplication only works within one process lifetime. With durable storage, it can be a unique constraint.

---

## Option 3: Record the Job Durably Before Answering

When a lost statement is not acceptable, the request must be remembered somewhere that outlives the process before the endpoint says it was accepted. The simplest durable store is usually the database the application already has:

```csharp
// Illustrative sketch; not part of the tested example.
app.MapPost("/reports", async (ReportCommand command, ReportsDbContext db, CancellationToken cancellationToken) =>
{
    var job = new ReportJob
    {
        Id = Guid.NewGuid(),
        CustomerId = command.CustomerId,
        Email = command.Email,
        Status = ReportJobStatus.Pending,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    db.ReportJobs.Add(job);
    await db.SaveChangesAsync(cancellationToken);   // the 202 is sent only after this commits

    return Results.Accepted($"/reports/{job.Id}", new { ReportId = job.Id, Status = "Pending" });
});
```

A worker, which can be a `BackgroundService` in the same application, finds pending jobs, claims each one with a conditional update so that two instances cannot both take it, generates and sends the statement, and records the outcome on the same row. The status endpoint reads that row, from any instance, after any restart. If the database is unavailable, the endpoint fails before it says `202`, so the caller knows the statement was not accepted.

A durable broker such as a cloud message queue does the same job when the work is already message-shaped or other services need to consume it. The rule is the same: return `202` only after the broker has acknowledged the message. If the application also writes its own record of the job, such as the row behind the status page, make the database write and the publish one decision, usually with a transactional outbox, so that neither can happen without the other.

Durable acceptance moves the hard questions rather than removing them:

- **Work can now run more than once.** A worker that crashes after sending the email but before recording `Sent` leaves a job that looks unfinished. Recovery will run it again, and the customer gets two statements unless the send is idempotent or the duplicate is harmless. Durable queues are almost always at-least-once.
- **Stuck work needs a rule.** A claimed job whose worker died needs a lease or timeout, and a cap on attempts, so it is retried a bounded number of times and then marked failed for a person to look at.
- **The queue still needs a limit.** A table can grow without bound just as a channel can. Decide what happens when the backlog is too large, and alert on its age, not only its length.
- **Graceful shutdown still matters.** It no longer decides whether the work is lost, but it decides how often work is interrupted and retried.

Those are deeper topics with their own articles. [Canceling a Queued Job Can Race With Execution](revoke-job-after-it-enters-queue.md) shows a conditional claim and what recovery after a crash must not do. [A Payment API Timeout Leaves the Outcome Unknown](payment-api-timeout-unknown-outcome.md) covers an external call whose outcome is unknown after an interruption, which is what the statement email is when the worker dies mid-send. [Your Retry Logic May Be Replaying an Authorization Decision](retry-logic-may-replay-authorization-decision.md) covers what each retry layer knows.

---

## Option 4: A Separate Worker or Scheduler

The durable job above can be processed by a `BackgroundService` inside the web application. Moving the work into a separate process, such as a .NET Worker Service, a serverless function triggered by the queue, or a job library with persistent storage and its own servers, is an operational decision, not a correctness one. It is justified when:

- **the work is long or heavy** enough to compete with request handling for CPU, memory, or database connections;
- **the work must scale independently**, for example, ten workers during month-end close and one the rest of the month;
- **web deployments should not interrupt it**, because the web tier is deployed several times a day and statements take minutes;
- **the work needs a schedule**, such as "generate every customer's statement at 02:00 on the first," which belongs to something that runs whether or not anyone made a request;
- **the work has different permissions**, such as access to the mail relay that the web tier should not have.

A separate process adds a deployment, monitoring, configuration, and a contract between the two. It does not add durability by itself. A separate worker reading an in-memory queue it shares with nothing is still Option 2, with more moving parts.

---

## Choosing

| | Caller learns the outcome | Survives a restart | Response time | Backpressure | Failure visibility | Operational cost |
| --- | --- | --- | --- | --- | --- | --- |
| **Await in the request** | Yes, in the response | Not needed; an interrupted request fails visibly | As long as the work | Server and connection limits | Normal error handling | None added |
| **Discarded `Task.Run`** | No | No, and no record of the loss | Milliseconds | None | None by default | None, until the incident |
| **Bounded channel and `BackgroundService`** | Through a status resource, while the process lives | No; a graceful stop can log what is lost | Milliseconds | Queue capacity; refuse with `503` | Logged and recorded per item | Low: one hosted service |
| **Durable job or durable queue** | Through a status resource that survives restarts | Yes, within the store's durability | Milliseconds plus one durable write | Backlog limits and alerts | Recorded in the store, retried, then failed | Moderate: schema or broker, claims, retries, idempotency |
| **Separate worker or scheduler** | Same as the store it reads | Same as the store it reads | Same as the store it reads | Worker count and backlog limits | Same, plus its own monitoring | Higher: another deployable |

Two questions decide most cases. **Must the caller know the outcome before moving on?** Then await it in the request, and make the request fast enough or the client patient enough. **If the process restarts right after `202`, is losing the work acceptable?** If yes, the bounded in-memory queue is enough, and simpler. If no, store the job before answering.

---

## When Losing the Work Is Acceptable

Plenty of background work can disappear without harm: refreshing a cache entry that the next request would rebuild anyway, recalculating a dashboard aggregate that a scheduled job also recalculates, sending a "someone viewed your profile" notification, or warming a search index. For work like that, a durable queue is overhead with no benefit, and recommending one would be bad advice.

The simplest valid design for disposable work is still not `_ = Task.Run(...)`. It is the bounded channel and `BackgroundService` above, possibly without the status resource:

- a **bounded** queue, so a burst cannot exhaust memory, with a full mode chosen on purpose; for a cache refresh, dropping an item may be exactly right;
- **a scope per item**, so nothing uses a disposed service;
- **failures caught, logged, and counted** at the item boundary, so a broken item is visible and does not stop the host;
- **the stopping token honored**, so shutdown is prompt and does not start new work;
- **one sentence of documentation** that says the loss is accepted, next to the code that makes it:

```csharp
// Cache warm-up requests are best-effort. They are lost on restart, and a full queue drops the
// newest request. A miss is rebuilt on the next read, so loss costs latency, not correctness.
```

That sentence is the difference between a tradeoff and an accident. The opening example made the same tradeoff for month-end statements without anyone writing it down, and the first time anyone read it was in a customer's complaint.

---

## Failure Modes

**1. The discarded task.** `_ = Task.Run(...)` or an un-awaited `async` call returns `202` for work that no one owns. A deployment, scale-in, crash, or recycle erases it, and its exceptions are never logged.

**2. A request-scoped service in detached work.** The background code uses a `DbContext`, `HttpContext`, or other scoped service captured from the request, and fails with `ObjectDisposedException` after the response, or worse, works most of the time.

**3. An unbounded queue.** `Channel.CreateUnbounded` or a `ConcurrentQueue` grows during a burst until the process runs out of memory, which then loses everything in it at once.

**4. A worker that dies on the first bad item.** An exception escapes `ExecuteAsync`. Since .NET 6, the default stops the host; with `BackgroundServiceExceptionBehavior.Ignore`, the worker stops silently while the endpoint keeps accepting work.

**5. A shutdown that keeps starting work.** The worker loops over buffered items without checking the stopping token, so items start during shutdown and are interrupted moments later.

**6. Calling host-managed work durable.** The team believes a `BackgroundService` survives restarts because the host "manages" it, and documents a promise the design does not keep.

**7. A `202` that promises nothing in particular.** The response has no identifier and no status resource, and the API documentation does not say what can be lost, so clients cannot tell an accepted request from a forgotten one.

---

## A Short Review Checklist

1. Is every task the request starts either awaited by the request or handed to something the host owns?
2. Does detached work carry only plain data, and resolve its services in a scope created for that work item?
3. Is the in-memory queue bounded, and is the behavior when it is full a deliberate choice, visible to the caller?
4. Is every work item's failure caught at the item boundary, logged with its identifier, and recorded where someone can see it?
5. Does the worker stop taking new items when the stopping token fires, and does it record what was queued but never started?
6. Does the `202` response include a stable identifier and a status location, and does the API documentation say whether the work survives a restart?
7. If losing the work is not acceptable, is the job stored durably *before* the response, and is the work safe to run more than once?
8. If losing the work is acceptable, is that written down next to the code?
9. Are there tests for a full queue, a failing item, and a host stopping with work pending?

---

## Continue Deeper

When the work crosses from the request into a job, the job also needs to carry the right identity. [A Background Job Should Not Inherit Everything the User Could Do](background-job-should-not-inherit-user-authority.md) covers what the job should record about who asked, and how the worker re-checks that before it starts.

Once the job is durable, two workers can race to claim it, and an operator may try to cancel it. [Canceling a Queued Job Can Race With Execution](revoke-job-after-it-enters-queue.md) shows the conditional claim that decides those races and what recovery must not do after a crash.

When a worker dies during an external call, such as sending the statement email, [A Payment API Timeout Leaves the Outcome Unknown](payment-api-timeout-unknown-outcome.md) explains how to record and resolve an outcome nobody observed, and [Your Retry Logic May Be Replaying an Authorization Decision](retry-logic-may-replay-authorization-decision.md) covers what each retry layer can and cannot know.

### Related Work

- [Background tasks with hosted services in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services), in the ASP.NET Core documentation, describes `IHostedService` and `BackgroundService`, the 30-second default shutdown timeout, the fact that `StopAsync` might not be called if the process fails, consuming scoped services from a background task, and a queued background task built on a bounded channel.
- [Dependency injection in ASP.NET Core: Request Services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection#request-services) explains that the framework creates a scope per request and that scoped services are valid only while the request is active.
- [Exception handling in hosting](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/hosting-exception-handling), a .NET 6 breaking-change note, describes how an unhandled exception in `BackgroundService.ExecuteAsync` is logged and stops the host by default, and how `BackgroundServiceExceptionBehavior` changes that.
- [BoundedChannelFullMode](https://learn.microsoft.com/en-us/dotnet/api/system.threading.channels.boundedchannelfullmode), in the .NET API reference, lists what a bounded channel does when it is full.
- [RFC 9110, section 15.3.3: 202 Accepted](https://www.rfc-editor.org/rfc/rfc9110#section-15.3.3) defines what the status code does and does not commit the server to.

---

## The Short Answer

`Task.Run` moves work to another thread. It does not give the work an owner. A discarded task is not awaited at shutdown, its failures are not observed, it may use services the request has already disposed, and it vanishes with the process.

Pick one of three guarantees on purpose. If the caller needs the outcome, finish the work in the request. If losing the work on restart is acceptable, use a bounded in-memory queue drained by a `BackgroundService`, with a scope per item, failures recorded, and the loss written down. If it is not acceptable, store the job durably before you return `202`, and make the work safe to run twice. Then test the guarantee you chose, including the part where it fails.
