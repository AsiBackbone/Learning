# X Publication Runbook

Learning can optionally announce newly deployed `feed: true` documents through
the Jackdaw Patio X account. The `Publish Documentation` workflow invokes the
separate `Publish New Learning Content to X` reusable workflow only after the
Pages deployment succeeds. X availability therefore cannot block or roll back
the documentation deployment, RSS feed, sitemap, or IndexNow notification.

## Publication boundary

The publisher fetches the public `deployment-revision.txt` and `sitemap.xml`,
then verifies that the deployed revision is a repository commit reachable from
`main`. It compares that revision with the cursor stored on
`automation/x-publisher-state`.

A document is selected when it is Markdown under `docs/`, has a canonical URL
in the deployed sitemap, and either changes from missing or `feed: false` to
valid `feed: true` metadata or adds `x_post` after a missing-copy deferral.
Other content edits, metadata updates, deletions, and Git renames of already
eligible documents do not create another post. RSS and X are sibling consumers
of source frontmatter; the X publisher does not read or modify `feed.xml`.

Feed-enabled articles may provide announcement copy with frontmatter:

```yaml
x_post: A timeout ends the caller's observation, not the provider's work. Keep the operation pending until evidence resolves the outcome.
```

`x_post` is the author-written engineering takeaway that leads the announcement.
Keep it concrete and useful without the article, on one line, with no hashtags,
mentions, or links. The documentation build validates the exact composed post
against X's weighted limit. A missing `x_post` does not block deployment or later
announcements: the publisher records a durable deferral and reports the page for
manual review. Adding `x_post` later selects that page again without making other
metadata edits eligible for reposting.

Feed-enabled articles may also select hashtags with optional frontmatter:

```yaml
x_hashtags:
  - DotNet
  - AISecurity
```

Omitting `x_hashtags` uses `#DotNet`. An explicit list must contain one or two
unique values, must include `DotNet`, and may add one of `AspNetCore`,
`SoftwareArchitecture`, `CyberSecurity`, `AISecurity`, `AIGovernance`, or
`DevSecOps`. Values omit the leading `#`; the publisher adds it. The documentation
build and publisher both reject malformed, unsupported, duplicate, or excessive
hashtags before any X API create call.

Feed-enabled pages may also ask readers a question:

```yaml
x_question: Has a correlation ID search ever told you the wrong story?
```

The question remains optional and author-written, so it is reviewed in the same
pull request as the page. Write one that readers can answer from their own
experience, rather than a teaser for the article. It must be one line, end with
`?`, and contain no hashtags, mentions, or links. Neither the takeaway, title,
nor question is truncated: the documentation build fails when the complete post
does not fit and reports how many characters to remove.

## Initial setup

1. Create a protected GitHub Environment named `jackdaw-patio-x`. Restrict it to
   trusted `main` deployments and add required reviewers if another maintainer
   is available.
2. Configure OAuth 1.0a user-context credentials as environment secrets:
   `X_API_KEY`, `X_API_KEY_SECRET`, `X_ACCESS_TOKEN`, and
   `X_ACCESS_TOKEN_SECRET`.
3. Configure the expected numeric account identifier as the environment variable
   `X_ACCOUNT_USER_ID`.
4. Give the X application only the read/write permissions needed to inspect the
   account timeline and create a post.
5. Manually run the workflow with `dry_run` left enabled. This job has read-only
   GitHub permissions, no protected environment, and no X credentials.
6. Manually run with `dry_run` disabled to initialize
   `automation/x-publisher-state` at the currently deployed revision. That first
   run intentionally does not backfill the existing archive.

When all five settings are absent, automatic publication reports a notice and
exits successfully so the optional integration does not make documentation
publication appear to fail. A partially configured environment fails with the
names of the missing settings; it never silently disables a broken live setup.

Do not place OAuth credentials in repository variables, source, workflow input,
logs, issues, or pull requests. The production API root is fixed in the tool and
cannot be overridden by repository or workflow input.

## Delivery and retry behavior

The post text is deterministic and keeps the canonical URL in the main post:

```text
{x_post}

{title}

[{x_question}]

#DotNet [#topicalHashtag]

{canonicalUrl}
```

The question block appears only when `x_question` is present. Length follows
X's weighted count: the link counts as 23 characters because X shortens it,
emoji sequences count 2, and characters outside X's Latin and punctuation
ranges count 2. No authored copy is truncated; a post that does not fit fails
the documentation build before merge, and the publisher refuses it as a
backstop. Before every create call, the publisher searches recent account posts
for that URL. A successful create or reconciliation writes its post ID and
source blob SHA immediately. The cursor advances only after every publishable
candidate succeeds and every missing-copy candidate has a durable deferral.

HTTP `400` is treated as a publisher/content defect; `401` and `403` indicate a
credential or application-permission failure. `429`, `5xx`, and pre-response
connection failures receive bounded retries. An ambiguous timeout is reconciled
against recent posts before the run fails. X does not provide an application
idempotency key for this operation, so receipts plus URL reconciliation provide
effectively-once behavior with a small residual duplicate risk if X accepts a
post but neither its response nor the new post is observable during recovery.

The workflow stops on the first unresolved delivery failure. Missing `x_post`
copy is different: the publisher writes the canonical URL, source path, source
blob SHA, and first deferral time to `deferredPublications`, continues with later candidates,
and advances the cursor after all publishable candidates succeed. When reviewed
copy is added, the missing-to-present `x_post` transition selects the article
again; a successful create or URL reconciliation writes the normal receipt and
removes the deferral. A later successful Pages deployment or controlled manual
live run still retries unresolved delivery failures because their durable cursor
has not advanced.

## Manual validation and recovery

Run the complete offline contract suite without secrets or network access:

```bash
dotnet run --file tools/publish-x.cs -- --self-test
```

Use the workflow's default manual dry run to inspect each candidate and its exact
post text. New and previously recorded candidates without `x_post` appear as
`DEFERRED` instead of being silently skipped. Add reviewed copy in a normal pull request; do not edit the
state branch by hand merely to clear a deferral. If a post succeeded but state
persistence failed, rerun the live workflow: recent-post reconciliation restores
the missing receipt before attempting a create.

For credential rotation, replace all four environment secrets as one coordinated
change, keep the account ID fixed, then run a dry run followed by a controlled
live run. For a wrong cursor or malformed manifest, do not delete receipts or
force-push the state branch. Repair it through a reviewed commit that preserves
valid receipts, then rerun manually.

## Disabling publication

Disable the `Publish New Learning Content to X` workflow or remove the
`jackdaw-patio-x` environment approval. Documentation deployment remains
independent. Retain the state branch so re-enabling the publisher does not
reannounce previously recorded content. Revoke X credentials when the publisher
is retired rather than merely deleting the workflow secrets.
