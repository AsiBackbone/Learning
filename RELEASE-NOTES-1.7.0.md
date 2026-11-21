# AsiBackbone Learning 1.7.0

AsiBackbone Learning 1.7.0 is an archival and citation snapshot of the educational material published since 1.6.0. It adds three problem-first practitioner articles on ASP.NET Core work, service, and connection lifetimes, clearer separation between the published archive and the `main`-published site, package source mapping for the samples, and hardened release and publishing runners.

## Highlights

- Publishes **Task.Run Does Not Make ASP.NET Core Work Durable**, separating detached work, host-managed execution, and durable acceptance, and building a bounded channel processed by a background service with scoped services per work item.
- Publishes **Your Dependency Injection Lifetime Is an Ownership Decision**, choosing transient, scoped, and singleton lifetimes from state ownership, concurrency, and disposal rather than habit, and explaining captive dependencies and scope validation.
- Publishes **Stop Creating an HttpClient for Every Request**, separating client, handler, connection, DNS, and request lifetimes, and comparing a long-lived client with an intentional `PooledConnectionLifetime` against `IHttpClientFactory`.

## Documentation, Samples, and Publication

- The home page, Getting Started, and README link the exact published archive tag, state that the documentation site follows `main`, and describe the retained implementation pins as teaching snapshots rather than production installation advice. A release-marker validator checks those links in source, in rendered HTML, and on the live site after deployment.
- The 2026-11-18 documentation currency review records the inventory and decisions behind those entry-page changes.
- Samples restore through package source mapping that routes every package ID to NuGet.org, and the samples document their central package-management choices. Sample test dependencies are refreshed.
- Release-evidence and X publishing jobs run with runner hardening in egress audit mode; GitHub Actions workflows are included in CodeQL analysis.
- Zenodo DOIs are resolved by a dedicated retry-bounded check instead of Lychee, so archive-service outages warn on pull requests and still fail scheduled runs.
- The repository adopts the AsiBackbone organization Code of Conduct.

## Compatibility

Learning releases are citable documentation and sample snapshots, not runtime or package support lines. Learning 1.7 retains the reviewed AsiBackbone 7.0.0 and NetCoreApplicationTemplate 2.11.2 implementation boundaries. The 2026-11-21 currentness review confirms that the foundational material and production-host bridge remain accurate within those explicit pins after reviewing AsiBackbone 7.5.4 and NetCoreApplicationTemplate 2.11.11.

NetCoreApplicationTemplate 2.11.11 services dependencies and container image digests and adds package source mapping to generated scaffolds without changing its public application surface. Production consumers should follow the owning repositories' current release and migration guidance rather than infer package or template compatibility from this Learning snapshot.

## Known Limitations

- Learning is educational documentation, not a compliance certification, legal standard, security guarantee, or production architecture approval.
- Executable samples and implementation references are teaching artifacts and do not prove integration compatibility with released `AsiBackbone.*` packages.
- External links and upstream products remain independently operated and can change after this snapshot.
- Final tag identity, GitHub Release notes, evidence assets, Sigstore bundle, and provenance are verified only after the approved release candidate is merged and `v1.7.0` is published. The entry pages' published-archive pointer advances to 1.7.0 only after that publication.

## Release Evidence

The GitHub Release will include an SPDX 2.3 inventory of sample source and locked dependencies, these exact release notes, a SHA-256 evidence manifest, and the Sigstore bundle for GitHub provenance attestations over those three files. See the [Stable Release Evidence Runbook](RELEASE.md) for scope, trust boundaries, and verification commands.
