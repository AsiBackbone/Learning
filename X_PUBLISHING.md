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

A document is selected only when it is Markdown under `docs/`, changes from
missing or `feed: false` to valid `feed: true` metadata, and has a canonical URL
in the deployed sitemap. Content edits, metadata updates, deletions, and Git
renames of already eligible documents do not create another post. RSS and X are
sibling consumers of source frontmatter; the X publisher does not read or
modify `feed.xml`.

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

Do not place OAuth credentials in repository variables, source, workflow input,
logs, issues, or pull requests. The production API root is fixed in the tool and
cannot be overridden by repository or workflow input.

## Delivery and retry behavior

The post text is deterministic:

```text
New from ASI Backbone Learning:

{title}

{canonicalUrl}
```

Long titles are truncated at a Unicode text-element boundary while preserving
the canonical URL. Before every create call, the publisher searches recent
account posts for that URL. A successful create or reconciliation writes its
post ID and source blob SHA immediately. The cursor advances only after every
candidate succeeds.

HTTP `400` is treated as a publisher/content defect; `401` and `403` indicate a
credential or application-permission failure. `429`, `5xx`, and pre-response
connection failures receive bounded retries. An ambiguous timeout is reconciled
against recent posts before the run fails. X does not provide an application
idempotency key for this operation, so receipts plus URL reconciliation provide
effectively-once behavior with a small residual duplicate risk if X accepts a
post but neither its response nor the new post is observable during recovery.

The workflow stops on the first unresolved candidate. A later successful Pages
deployment or a controlled manual live run retries it because the durable cursor
has not advanced.

## Manual validation and recovery

Run the complete offline contract suite without secrets or network access:

```bash
dotnet run --file tools/publish-x.cs -- --self-test
```

Use the workflow's default manual dry run to inspect the candidates and exact
post text for the deployed revision. If a post succeeded but state persistence
failed, rerun the live workflow: recent-post reconciliation restores the missing
receipt before attempting a create.

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
