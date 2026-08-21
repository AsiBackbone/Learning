---
description: Learn to treat ASP.NET Core configuration as a trust boundary with explicit opt-in, startup validation, safer failure behavior, and clear ownership.
---

# Secure-by-Default ASP.NET Core Configuration

**Pattern classification:** General Learning Material

**Difficulty:** Intermediate

**Prerequisites:** Basic familiarity with ASP.NET Core configuration, dependency injection, and the [ASP.NET Core learning area](index.md). The [Middleware Ordering Changes Behavior](middleware-ordering-changes-behavior.md) tutorial is useful context but is not required.

**Learning objective:** Treat configuration as part of application architecture and the trust boundary; recognize insecure fallback behavior; prefer explicit opt-in for consequential features; validate startup-sensitive settings; distinguish fail-fast, fail-closed, and graceful degradation; separate secrets from ordinary settings; and define who owns configuration values versus configuration invariants.

## Pattern Card

> **Problem:** Configuration can silently broaden application behavior when missing, malformed, environment-specific, or overridden by a higher-precedence provider. A system can start successfully while entering a state the application team never intended to permit.
>
> **Pattern:** Give consequential features conservative defaults, require explicit opt-in, bind application-owned settings to typed options, validate critical invariants before serving traffic, keep secrets outside source-controlled configuration, and make failure behavior deliberate.
>
> **Use when:** Configuration controls exposure, external integrations, authentication providers, proxy trust, security headers, data access, background jobs, administrative capabilities, or any other behavior where an unsafe default can expand the application's reachable state-space.
>
> **Prefer something simpler when:** A value is genuinely cosmetic or optional and an ordinary default cannot create a security, data-integrity, availability, or external-side-effect risk.
>
> **Observe:** The final application behavior is produced by the combination of defaults, configuration providers, environment, validation, and runtime code. A safe design constrains that combination before consequential behavior becomes reachable.

## Configuration Is Architecture, Not Just Startup Data

It is easy to think of configuration as a bag of values loaded before the application starts:

```text
appsettings.json
      ↓
Configuration
      ↓
Application
```

That model is incomplete.

Configuration often decides which behaviors exist at all:

```text
Available configuration
      ↓
Validation and constraints
      ↓
Allowed application behavior
```

A setting can determine whether the application:

- Trusts forwarded proxy information.
- Enables an external authentication provider.
- Connects to a production database.
- Sends data to another service.
- Exposes a feature or administrative surface.
- Runs a background worker.
- Applies a security policy.
- Uses a relaxed development-only behavior.

For those concerns, configuration participates in the trust boundary.

The important architectural question is not merely:

> What value should this key have?

It is:

> **What should the application be allowed to become when this key is absent, ambiguous, invalid, or overridden?**

## Configuration Is Composed from Multiple Sources

ASP.NET Core configuration is layered. The default application configuration can include sources such as:

```text
appsettings.json
      ↓
appsettings.{Environment}.json
      ↓
User secrets in Development
      ↓
Environment variables
      ↓
Command-line arguments
```

Later providers can override values supplied by earlier providers.

That is useful because deployment-specific values should not require source-code changes.

It is also why validation matters.

The application does not execute against only the checked-in `appsettings.json` file. It executes against the **final composed configuration**.

For example:

```text
Safe checked-in default
Enabled = false
      ↓
Production environment variable
Enabled = true
      ↓
Final runtime configuration
Enabled = true
```

The configuration system is working correctly in this example.

The remaining question is whether the application validates everything required for the newly enabled behavior.

A secure default is therefore not an immutable policy. It is the safe starting state when nobody has explicitly supplied enough information to enter a broader state.

## Deliberately Unsafe Example: Missing Configuration Enables Behavior

Consider a fictional outbound export feature:

```csharp
bool exportEnabled = builder.Configuration.GetValue(
    "OutboundExport:Enabled",
    true);

string exportEndpoint =
    builder.Configuration["OutboundExport:Endpoint"]
    ?? "http://legacy-export.internal/api/export";
```

The defaults are doing two dangerous things:

1. The feature is enabled when configuration is missing.
2. A consequential destination is silently invented when the endpoint is missing.

The resulting flow is:

```text
Configuration missing
      ↓
Feature silently enabled
      ↓
Fallback endpoint selected
      ↓
Outbound behavior becomes reachable
```

This design makes absence look like permission.

A deployment typo, missing secret injection, incorrect environment name, or omitted configuration section can therefore broaden behavior instead of stopping it.

The problem is not that every default is bad.

The problem is that the defaults answer a consequential question on behalf of the operator:

> Yes, perform the export, and if the destination is unknown, use this one anyway.

## Safer Design: Disabled Until Explicitly Enabled

A safer options model begins from a narrower state:

```csharp
public sealed class OutboundExportOptions
{
    public const string SectionName = "OutboundExport";

    public bool Enabled { get; set; } = false;

    public Uri? Endpoint { get; set; }
}
```

Now the missing section produces:

```text
Configuration missing
      ↓
Enabled remains false
      ↓
Outbound export remains unavailable
```

That is explicit opt-in.

The feature can still be enabled through configuration, but doing so should activate stronger validation.

## Validate the State You Actually Intend to Permit

Strongly typed options make application-owned configuration easier to reason about because the application can define the accepted shape and invariants in one place.

For the export example:

```csharp
builder.Services
    .AddOptions<OutboundExportOptions>()
    .Bind(builder.Configuration.GetSection(OutboundExportOptions.SectionName))
    .Validate(
        options => !options.Enabled || options.Endpoint is not null,
        "OutboundExport:Endpoint is required when outbound export is enabled.")
    .Validate(
        options =>
            !options.Enabled ||
            options.Endpoint?.Scheme == Uri.UriSchemeHttps,
        "OutboundExport:Endpoint must use HTTPS when outbound export is enabled.")
    .ValidateOnStart();
```

The important relationship is:

```text
Enabled = false
      ↓
Endpoint may be absent
      ↓
Application can start with export unavailable
```

but:

```text
Enabled = true
      ↓
Endpoint becomes required
      ↓
Endpoint must satisfy the application-owned invariant
      ↓
Invalid configuration prevents normal startup
```

`ValidateOnStart()` is valuable for startup-sensitive settings because validation occurs before the application begins normal operation rather than waiting for the first request that happens to resolve the options.

This moves a configuration error earlier in the lifecycle:

```text
Invalid critical configuration
      ↓
Startup validation failure
      ↓
No traffic served in the invalid state
```

That is usually easier to detect and safer to operate than discovering the problem only after a request reaches the affected feature.

## Required and Optional Configuration Are Different Categories

Do not classify every missing value as a fatal error.

The correct response depends on what the value controls.

A useful classification is:

| Configuration concern | Missing value can mean | Typical safer response |
| --- | --- | --- |
| Database connection required for the application's core function | The application cannot perform its primary work | Fail startup. |
| Signing or authentication material required to validate authority | The application cannot establish a required trust boundary | Fail startup or keep the protected subsystem unavailable. |
| Optional outbound export integration | A non-core capability is unavailable | Keep the feature disabled and continue. |
| Optional telemetry exporter | Reduced observability | Degrade deliberately, emit a local diagnostic, and continue if operational policy allows it. |
| Cosmetic display setting | A presentation preference is absent | Use a harmless default. |

The lesson is not:

> Missing configuration must always crash the process.

It is:

> **Missing configuration must not silently create more authority, exposure, or side effects than the application can justify.**

## Explicit Opt-In Versus Implicit Exposure

Explicit opt-in is most useful when enabling a feature changes the trust or execution surface.

Examples include:

- External authentication providers.
- Forwarded-header trust.
- Public administrative endpoints.
- Cross-origin access.
- External data export.
- Background jobs that mutate data.
- Remote command or tool execution.
- Debug or diagnostic surfaces that reveal implementation detail.

A safer relationship looks like:

```text
Feature configuration absent
      ↓
Feature unavailable
      ↓
Operator explicitly enables feature
      ↓
Required dependent settings validated
      ↓
Feature becomes reachable
```

An unsafe relationship looks like:

```text
Feature configuration absent
      ↓
Application guesses intent
      ↓
Feature becomes reachable anyway
```

This does not mean every boolean should default to `false`.

Some controls are protections that should normally default to enabled, such as application-owned security headers or other conservative safeguards. The architectural rule is broader:

> **Choose the default that leaves the system in the safer admissible state when intent is unknown.**

For a protection, that may mean enabled by default.

For a consequential capability, that often means disabled by default.

## Environment-Specific Behavior Should Be Deliberate

Development and production legitimately need different configuration.

A development environment may use:

- Local databases.
- Local-only authentication test values.
- More verbose diagnostics.
- Developer tooling.
- Reduced retention or local storage.

Production may require:

- Real host names.
- Trusted proxy addresses and networks.
- Production database providers.
- External authentication configuration.
- Stronger retention and observability expectations.
- Secrets supplied by the deployment platform.

The risk appears when a development convenience becomes an implicit production fallback.

For example, avoid reasoning like:

```text
Production value missing
      ↓
Reuse development value
      ↓
Application starts
```

when the development value represents a weaker trust assumption.

Prefer:

```text
Development
      ↓
Development-specific value is explicitly supplied

Production
      ↓
Production-specific value is explicitly supplied
      ↓
Production invariant is validated
```

Environment-specific files are override layers, not permission to bypass application invariants.

The application should still define what values are admissible.

## Do Not Let Development Conveniences Leak into Production

A useful production review question is:

> If every development-only configuration source disappeared, would the production application still have everything it needs to start safely?

Watch for patterns such as:

- A development database string becoming an unintended production fallback.
- A local test identity provider remaining enabled.
- Permissive CORS or origin values copied into production.
- Debug-detail settings exposed outside Development.
- Placeholder credentials or tokens being treated as real values.
- A local filesystem path silently becoming production persistence.

The problem is not environment-specific behavior itself.

The problem is **unreviewed inheritance of weaker assumptions**.

## Fail Fast, Fail Closed, and Graceful Degradation Are Different Decisions

These terms are related but should not be collapsed into one rule.

### Fail Fast

Fail fast means detecting an invalid state early and refusing normal startup.

Use it when the process cannot safely perform its primary responsibility without the configuration.

```text
Required signing key missing
      ↓
Trust invariant cannot be satisfied
      ↓
Startup fails
```

### Fail Closed

Fail closed means refusing the affected action or capability when required preconditions are not satisfied.

The whole application may still remain available.

```text
Optional export endpoint missing
      ↓
Export capability unavailable
      ↓
Other application features continue
```

Fail closed is especially useful when a subsystem is optional but consequential.

### Graceful Degradation

Graceful degradation means continuing with reduced capability when that reduction is understood, observable, and acceptable.

```text
Optional telemetry exporter unavailable
      ↓
Remote telemetry disabled
      ↓
Local diagnostics preserved
      ↓
Application continues under documented degraded mode
```

Graceful degradation is not the same as silently ignoring a failure.

A degraded state should usually be visible to operators through logs, health information, metrics, startup diagnostics, or another appropriate signal.

### There Is No Universal "Always Fail Closed" Rule

An optional telemetry destination and an authentication signing key are not equivalent.

A secure architecture classifies the consequence before choosing the failure mode.

Ask:

1. Does the missing setting remove a protection?
2. Does it broaden authority or exposure?
3. Does it risk data loss or corruption?
4. Does it merely remove an optional capability?
5. Can the degraded state be observed and operated safely?
6. Would termination create a greater availability or safety risk than disabling the affected subsystem?

The answer determines whether fail-fast, fail-closed, or graceful degradation is appropriate.

## Secrets Are Configuration Inputs with Different Custody Requirements

A secret may enter the ASP.NET Core configuration system, but that does not make an ordinary configuration file an appropriate place to store it.

Sensitive values can include:

- Database credentials.
- Authentication client secrets.
- API keys.
- Signing material.
- Access tokens.
- Certificate passwords.
- Private service credentials.

For local development, ASP.NET Core Secret Manager keeps secrets outside the project tree and out of source control.

It is a development convenience, not a production secret store and not an encrypted vault.

Production secrets should come from a protected deployment mechanism or secret manager appropriate to the hosting environment.

Environment variables are useful configuration sources, but they should not automatically be treated as a secret vault. They can be visible to processes, administrators, diagnostics, or platform tooling depending on the environment.

A useful separation is:

```text
Application code
      ↓
Defines the key and invariant

Deployment / secret platform
      ↓
Supplies the sensitive value

Application startup
      ↓
Validates presence and shape without logging the secret
```

Do not log secret values merely to prove that configuration succeeded.

## Configuration Ownership Boundaries Matter

A secure configuration model separates **who chooses a value** from **who defines what values are acceptable**.

For example:

| Concern | Typical owner | Responsibility |
| --- | --- | --- |
| Option shape and validation rules | Application team | Define admissible states and dependent requirements. |
| Production host names, proxy ranges, endpoints | Deployment / operations | Supply environment-specific values within the application's accepted constraints. |
| Secret values | Secret-management / deployment boundary | Store and deliver sensitive material without committing it to source. |
| Environment identity | Host / deployment platform | Select Development, Staging, Production, or another intentional environment. |
| Feature enablement policy | Application and operations together | Decide whether a capability may be activated and what additional configuration becomes mandatory. |

This produces a useful rule:

> **Operators choose deployment values; the application owns the invariants that make those values safe to consume.**

A deployment system should not have to rediscover every invariant from comments in `appsettings.json`.

The application should encode important invariants where they can be validated and tested.

## Avoid Insecure Fallback Values

Fallbacks are attractive because they make startup easy.

They become dangerous when they conceal missing intent.

Review defaults such as:

```text
Missing host list
      ↓
Use wildcard
```

```text
Missing external endpoint
      ↓
Use legacy endpoint
```

```text
Missing provider selection
      ↓
Use a provider intended only for development
```

```text
Missing authentication configuration
      ↓
Enable anonymous or mock behavior
```

Any of these might be acceptable in a narrowly controlled development scenario.

The problem is using them as silent production fallbacks.

Prefer one of three explicit outcomes:

```text
Safe default
```

```text
Validation failure
```

```text
Documented degraded mode
```

The worst outcome is often an undocumented fourth state:

```text
Configuration was wrong, but the application guessed.
```

## Feature Flags Do Not Replace Authorization or Policy

A feature setting can decide whether code is available.

It should not be mistaken for proof that a particular actor is allowed to use the feature.

For example:

```text
OutboundExport:Enabled = true
```

can mean:

> The deployment has enabled the export capability.

It does not mean:

> Every caller is authorized to export data.

A complete path may still require:

```text
Feature enabled
      ↓
Authenticated actor
      ↓
Authorization / policy evaluation
      ↓
Validated request
      ↓
Execution
```

Configuration and authorization protect different boundaries.

## Test Configuration Invariants

If a configuration rule matters enough to block an unsafe state, test it.

Useful invariants for the export example include:

| Scenario | Expected result |
| --- | --- |
| Section absent | Application starts; export remains disabled. |
| `Enabled=false`, endpoint absent | Application starts; export remains disabled. |
| `Enabled=true`, endpoint absent | Startup validation fails. |
| `Enabled=true`, HTTP endpoint | Startup validation fails. |
| `Enabled=true`, HTTPS endpoint | Startup succeeds. |

A focused host-level test can prove that `ValidateOnStart()` is part of startup behavior:

```csharp
[Fact]
public async Task Enabled_export_without_endpoint_fails_startup()
{
    HostApplicationBuilder builder = Host.CreateApplicationBuilder();

    builder.Configuration.AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["OutboundExport:Enabled"] = "true"
        });

    builder.Services
        .AddOptions<OutboundExportOptions>()
        .Bind(builder.Configuration.GetSection(OutboundExportOptions.SectionName))
        .Validate(
            options => !options.Enabled || options.Endpoint is not null,
            "OutboundExport:Endpoint is required when outbound export is enabled.")
        .ValidateOnStart();

    using IHost host = builder.Build();

    await Assert.ThrowsAsync<OptionsValidationException>(
        () => host.StartAsync());
}
```

This test protects an architectural invariant:

```text
Feature explicitly enabled
      +
Required dependent configuration missing
      ↓
Application cannot enter the enabled-but-invalid state
```

Add complementary tests for the valid disabled and enabled cases so a future refactor cannot accidentally turn the feature back into implicit exposure.

## Validate the Final Behavior, Not Only the Options Object

Options tests are useful but sometimes insufficient.

If configuration controls an externally observable capability, add an integration test for the resulting behavior where practical.

Examples:

```text
Feature disabled
      ↓
Route not mapped or operation rejected
```

```text
External provider not configured
      ↓
Provider-specific execution unavailable
```

```text
Production trust configuration invalid
      ↓
Host does not start
```

This catches the case where options validation is correct but runtime code ignores the validated option.

## Working Implementation References

Learning keeps the examples deliberately small.

The current `NetCoreApplicationTemplate` repository provides a fuller ASP.NET Core specimen for configuration layering, startup validation, trust-sensitive options, and secret-handling guidance.

| Learning concept | Working reference | What to inspect |
| --- | --- | --- |
| Configuration strategy and precedence | [Configuration](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/articles/configuration.md) | Review shared defaults, environment overrides, environment variables, secrets guidance, provider-specific configuration, and the production review checklist. |
| Startup validation for security-sensitive options | [`SecurityHeadersExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/SecurityHeadersExtensions.cs) | Observe conditional validation such as requiring CSP text when CSP is enabled, followed by `ValidateOnStart()`. |
| Trust-boundary configuration | [`ForwardedHeadersExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/ForwardedHeadersExtensions.cs) | Inspect validation for proxy addresses, networks, forwarded-header values, and allowed hosts before forwarded-header behavior is enabled. |
| Option defaults | [`ApplicationSecurityHeadersOptions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Options/ApplicationSecurityHeadersOptions.cs) | Compare a protection that defaults to enabled with consequential capabilities that may be safer as explicit opt-in. |

Use the implementation repository as a specimen, not as a universal configuration schema.

The reusable idea is:

> **Make unsafe configuration states difficult to represent, easy to detect, and impossible to mistake for intentional enablement.**

## Configuration Review Checklist

Before adding or changing a consequential configuration option, ask:

1. What happens when the key is completely absent?
2. Does absence disable behavior, preserve a protection, fail startup, or invent a fallback?
3. Can a higher-precedence provider broaden behavior?
4. If a feature is enabled, which dependent values become mandatory?
5. Are those dependencies validated before the feature becomes reachable?
6. Is the configuration ordinary data or a secret with different custody requirements?
7. Is a development convenience able to leak into production?
8. Should invalid configuration fail the whole process, disable one subsystem, or produce a documented degraded mode?
9. Who owns the value, and who owns the invariant?
10. Is the failure observable without exposing sensitive information?
11. Are the important invariants covered by tests?
12. Does runtime behavior actually honor the validated configuration?

If these questions cannot be answered, the configuration contract is not yet fully designed.

## Tradeoffs

### Benefits of Secure Defaults and Startup Validation

- Missing configuration is less likely to broaden behavior accidentally.
- Production mistakes are detected earlier.
- Option contracts become easier to review.
- Deployment responsibilities become clearer.
- Environment-specific settings remain flexible without abandoning application-owned invariants.
- Consequential feature enablement becomes intentional.
- Tests can protect configuration behavior as an architectural invariant.

### Costs

- More validation can make startup stricter and deployments less forgiving.
- Environment-specific rules require explicit operational ownership.
- Conditional validation can become complex when many options depend on one another.
- A fail-fast choice can reduce availability when graceful degradation would have been acceptable.
- Secret-management integration adds deployment complexity.
- Typed option models and tests add code that small applications may not need.

The goal is not maximum strictness.

The goal is to prevent configuration ambiguity from silently creating an unsafe application state.

## Official ASP.NET Core References

- [Configuration in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0)
- [Options pattern in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/options?view=aspnetcore-10.0)
- [Safe storage of app secrets in development in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/app-secrets?view=aspnetcore-10.0)
- [Use multiple environments in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/environments?view=aspnetcore-10.0)

## Review Questions

Before moving on, you should be able to answer:

1. Why is configuration part of the application architecture rather than only startup data?
2. Why can configuration precedence turn a safe checked-in default into broader runtime behavior?
3. What makes a fallback value insecure or ambiguous?
4. When should a feature use explicit opt-in?
5. Why does `ValidateOnStart()` matter for startup-sensitive options?
6. How do required and optional configuration differ?
7. What is the difference between fail-fast and fail-closed behavior?
8. When can graceful degradation be safer than terminating the application?
9. Why should development configuration not become an implicit production fallback?
10. Why are user secrets appropriate for development but not a production secret store?
11. What is the difference between configuration ownership and invariant ownership?
12. Why does enabling a feature not replace authorization or policy evaluation?
13. Which configuration invariants should be protected with tests?

## Related Content

- [ASP.NET Core learning area](index.md)
- [Middleware Ordering Changes Behavior](middleware-ordering-changes-behavior.md)
- [Trust Boundaries and Least Privilege](../security/trust-boundaries-and-least-privilege.md)
- [Decision Before Execution](../tutorials/decision-before-execution.md)
- [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md)
- [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

---

> **Read it. Run it. Question it. Improve it.**
