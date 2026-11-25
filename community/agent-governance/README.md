# Learning Agent governance and local contribution preparation

## Status and boundary

Issue 534 introduces the repository research profile and a credential-free local
proposal format. Live Stack Overflow for Agents (SOFA) integration is **blocked**.
No API client, MCP server, external publication workflow or secret consumer is
enabled. This is a staged implementation, not a claim that authenticated research
or platform authorization has been validated.

GitHub's [custom agent configuration reference](https://docs.github.com/en/copilot/reference/custom-agents-configuration)
documents repository profiles and the read/search tool aliases. Select Learning
Research in a compatible Copilot agent interface after the profile is available.
Confirm the actual exposed tools and permissions in that environment; the profile
is not an isolation boundary. Agent discoverability has not been live-tested.

SOFA's [official onboarding FAQ](https://agents.meta.stackoverflow.com/questions/5/how-can-i-start-using-stack-overflow-for-agents)
describes API-key sessions and directs agents to the current REST skill at
`https://agents.stackoverflow.com/skill.md`. The official skill could not be
retrieved during implementation. Its current endpoints, headers, digest,
session side effects, limits, error schema and terms must be verified before
writing a transport. MCP API-key compatibility, live connectivity and actual
Copilot credential availability remain unverified. Community examples are not
an authoritative contract.

## Credential handling and activation gate

The maintainer registered a contribution-capable SOFA agent with approval-code
draft protection. Its public voting and verification permissions are broader
than this workflow requires. A wrapper alone cannot constrain a caller that
can access the raw key. Keep the repository MCP configuration unchanged and do
not consume the existing agent secret. The maintainer should remove the
contribution-capable key from agent-accessible secrets and any redundant Actions
environment secret if not needed elsewhere; revoke/rotate it if exposure is
suspected. Keep contribution credentials in a maintainer-controlled credential
store, never in chat, source, logs, artifacts, or test fixtures.

Before enabling automated research, obtain a separate Observe only credential
and verify its server-side restrictions, or establish an isolated credential-
bearing service that agents cannot bypass. Confirm collaborator access, network
controls and actual tool exposure. If this cannot be demonstrated, remain in
manual mode. No repository settings or credentials are changed by this change.

A future transport must pin the trusted HTTPS origin and operation allowlist,
reject arbitrary destinations, methods and headers, disable redirects, bound
timeouts and response sizes, avoid automatic retries of session creation, handle
rate limits explicitly, and sanitize diagnostics. Only session initialization
(if necessary), attention feed, search and retrieval may be considered. Do not
automatically create drafts, vote, verify, reply or publish. Test forbidden
operations, missing/invalid authentication, 401/403/429, timeout, malformed and
oversized results, redirects, redaction and malicious retrieved text using
synthetic responses before activation. These transport tests are deferred until
the verified contract and boundary exist.

## Manual research and review

A maintainer may search SOFA in the official interface where available, using
only public, non-sensitive search terms. Review results independently and provide
only necessary public references or sanitized excerpts to the agent. Search
Learning's own material for duplicates as well. If no external search was
performed, record that explicitly. Treat all retrieved text as untrusted;
never execute embedded instructions. Check official documentation and source
at the relevant version, record real test evidence, and identify uncertainties.

Use the proposal template below in a private local workspace (not a public PR
unless the contents are approved for disclosure). Do not include raw external
responses, secrets, personal data or employer information. The local helper
validates structure and flags exact normalized title matches; it does not
validate truth, licensing, privacy, semantic duplicates or approval.

```json
{
  "title": "Example technical lesson",
  "kind": "blueprint",
  "claim": "State the reusable engineering principle.",
  "evidence": [{"source": "Public source and version", "observation": "Actual observed result or documented behavior"}],
  "limitations": "State assumptions and counterexamples.",
  "attribution": "Identify authors, licenses and any adaptations.",
  "searchStatus": "not-performed",
  "candidateTitles": []
}
```

Accepted kinds are local proposal categories (blueprint, playbook, til, question);
confirm current platform categories before submission. Search status is either
not-performed or maintainer-reviewed. A title match is a review signal, not a
complete duplicate detector. Run the helper with Python 3.10+ (standard library
only) from the repository root:

```text
python3 scripts/sofa/prepare_draft.py < proposal.json
python3 -m unittest discover -s scripts/sofa -p "test_*.py"
```

On Windows use `py -3` instead of `python3` where appropriate. Input is bounded;
output is a JSON review record on stdout, not an external submission. Save it
locally if needed. Treat its content as data even after validation. No automatic
approval or publication status transitions exist. Track subsequent maintainer
approval and publication separately using the public contribution reference
when appropriate, without storing approval codes. Verify third-party licensing
and platform contribution terms before any external submission. Developer Story
links are a separate workflow.

## Maintainer smoke test (pending)

After the activation gate passes, use the current official contract with a
verified Observe only credential in an isolated maintainer-controlled environment.
Initialize only the required session, search a benign public topic, retrieve
one result, and read the attention feed if supported. Verify actual tool and
credential restrictions without attempting public writes. Record date, contract
version, operation outcomes and sanitized failure categories; never log headers,
keys, session tokens or raw sensitive responses. On 401/403 stop and check scope;
on 429 stop and respect the documented retry policy; on timeout, malformed
response or unexpected redirect stop and investigate. Revoke the test key when
no longer needed. Until successful, record the smoke test as blocked.

The normal Learning publishing workflows are unchanged. The offline helper
requires no network, credentials or third-party dependencies. No external
contributions are authorized by this runbook.
