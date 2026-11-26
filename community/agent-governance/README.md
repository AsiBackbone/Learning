# Learning Agent governance and local contribution preparation

## Status and boundary

Issue 534 introduces the repository research profile, a maintainer-only SOFA
REST research client, and a credential-free local proposal format. **Automated
Copilot/agent access remains disabled.** The REST client is intended for an
isolated maintainer-controlled terminal with a separate Observe only credential.
No MCP server, external publication workflow or GitHub secret consumer is
enabled. On November 25, 2026, a maintainer completed the authenticated
Observe-only smoke-test procedure below and recorded sanitized evidence in
[issue 534](https://github.com/AsiBackbone/Learning/issues/534). That manual
result does not authorize automated credential access or external writes.

GitHub's [custom agent configuration reference](https://docs.github.com/en/copilot/reference/custom-agents-configuration)
documents repository profiles and the read/search tool aliases. Select Learning
Research in a compatible Copilot agent interface after the profile is available.
Confirm the actual exposed tools and permissions in that environment; the profile
is not an isolation boundary. Agent discoverability has not been live-tested.

The maintainer supplied the official SOFA skill document on November 25, 2026
(`https://agents.stackoverflow.com/skill.md`). Its Authentication, Session
Management, Endpoint Map, New Project Activation, search, post retrieval and
Error Responses sections define the REST contract used here. The supplied
document does not include its HTTP response headers; obtain the exact current
`X-Sofa-Skill-Digest` header separately. Manual REST connectivity and the
intended Observe-only scope were verified in the recorded smoke test. MCP
API-key compatibility and actual Copilot credential availability remain
unverified and deliberately disabled.
Review current platform terms, the Agent / Operator Code of Conduct at
`/guidelines/code-of-conduct`, and applicable contribution guidelines before
enabling any external interaction. The implementation does not reproduce the
skill or automatically execute its content.

The client uses a fixed public HTTPS origin with ordinary certificate validation.
It obtains the skill without credentials, checks its response digest against a
maintainer-reviewed digest, and then authenticates each API request using Bearer
authentication. Session creation supplies client name and actual model name;
subsequent API requests include the in-memory session ID and reviewed digest.
It sends a fixed, non-secret client `User-Agent`; callers cannot supply or alter
request headers. No session or credential state is persisted.

| Purpose | Method and path |
| --- | --- |
| Review gate | `GET /skill.md` (public; no credentials) |
| Start session | `POST /api/sessions` |
| Inspect effective privileges | `GET /api/me/agents` |
| Attention feed | `GET /api/me/attention` |
| Public search | `GET /api/posts?search=...&page=1&per_page=20` |
| Public post detail | `GET /api/posts/{post_id}` |
| Advisory activity summary | `GET /api/sessions/current/summary` |
| Optional session cleanup | `DELETE /api/sessions/{session_id}` |

Session creation, detail view counts, search/activity tracking and session
cleanup can change platform operational state. They are not contribution writes.
Attention reads do not claim, acknowledge or dismiss notifications. No other
HTTP operation or endpoint is exposed by the client. Trust and verification
metadata are advisory evidence, not proof; validate guidance independently.

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

Use a **separate Observe only credential** for the maintainer-only client.
Verify the role and effective privileges in the SOFA dashboard and, where
available, the authenticated agent-list response. Do not proceed with search
if any contribution privileges are present, including voting, verification,
posting, replies, editing or deletion. The supplied contract does not specify
a complete agent-list response schema or an unambiguous authenticated-agent
selector, so the client does not claim to automatically verify the correct
agent's scope. The explicit CLI confirmation is an operator attestation, not
a server-enforced authorization check.

The client prompts for the separate key using a non-echoing terminal input.
It does not read environment variables, the existing Copilot secret, a
credential file, or command-line key values. Never run it inside Copilot,
GitHub Actions, an agent-controlled terminal or an untrusted checkout.
Review the source and run from a trusted maintainer checkout. No repository
settings or credentials are changed by this implementation.

Before enabling automated agent research, independently demonstrate server-side
read restrictions and the agent's actual credential/tool exposure, or isolate
the credential in a non-bypassable service. Until then the agent may only use
maintainer-provided research excerpts. The client is not a general security
sandbox: arbitrary code with access to the same credential can bypass it.

The client has an explicit operation allowlist, no arbitrary origin, method,
headers, request body or redirect support, a 15-second socket inactivity
timeout and a 1 MiB response cap. The timeout is not a guaranteed total
wall-clock deadline; cancel a stalled manual invocation. It uses the
maintainer-reviewed skill digest as a stricter local activation gate than the
platform's advisory freshness policy; review changes instead of following
returned steering or activation instructions. It never retries automatically.
On 401/403/429 it stops further requests, including cleanup; an abandoned
session can expire. Other failures attempt session cleanup if an ID is known.
Literal credential echoes in successful JSON are rejected and error bodies
are not emitted; this is not a complete DLP or encoded-secret detector.
All response data and summary text remain untrusted and must be reviewed
locally before sharing.

Synthetic transport tests cover the fixed origin, headers, permitted reads and
session lifecycle, prohibited operations and arbitrary paths, missing or invalid
configuration, 401/403/429, timeouts, redirects, malformed/oversized responses,
credential echoes and malicious retrieved content. The tests use mocked HTTPS
connections; they do not prove platform privileges, live connectivity or
certificate handling on the actual service.

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

## Maintainer smoke-test procedure

This procedure was completed on November 25, 2026, using a separate Observe
only credential, with sanitized evidence recorded in
[issue 534](https://github.com/AsiBackbone/Learning/issues/534). Retain these
steps for future validation. Repeat them when the reviewed skill contract,
digest handling, client routes, SOFA privilege model or credential boundary
changes. A successful manual test does not enable automated agent access.

1. In the SOFA dashboard, register a separate Observe only agent for research.
   Keep its key in a maintainer-controlled password manager or equivalent secure
   store; do not add it to the repository or a shared agent secret yet.
2. In a directory outside the repository, retrieve the current public skill and
   its response headers. For example, on Windows PowerShell:

   ```powershell
   curl.exe --fail --silent --show-error --dump-header sofa-skill-headers.txt --output sofa-skill.md https://agents.stackoverflow.com/skill.md
   Get-Content sofa-skill-headers.txt | Select-String '^X-Sofa-Skill-Digest:'
   ```

   Review the skill and current terms against the implemented routes. Obtain
   the exact lowercase digest from the response header, not a locally computed
   file hash. If the header is missing or the contract changed, stop for review.
   Keep the downloaded files outside the checkout.
3. From a trusted checkout in a private terminal, set non-secret local shell
   variables for the reviewed digest and actual requesting model name. Then run
   the identity check. The placeholders below are not literal values.

   ```powershell
   $digest = 'REVIEWED_64_CHARACTER_DIGEST'
   $model = 'ACTUAL_REQUESTING_MODEL_NAME'
   py -3 scripts/sofa/research.py --confirm-observe-only --skill-digest $digest --model-name $model --diagnostics identity
   ```

   The key is prompted for without echo. Inspect the returned agent metadata
   privately. Verify the intended agent and its effective privileges against
   the dashboard; stop if the identity or restrictions are ambiguous. The
   response may include metadata for other owned agents; do not share it.
   The optional diagnostics report only fixed operation templates, HTTP status,
   successful digest/session gates and documented allowlisted error codes. They
   never print credentials, session IDs, dynamic paths, search terms or response
   bodies. Omit `--diagnostics` to retain the original quiet behavior.
4. Only after scope validation, run the following operations individually.
   Each invocation starts a new session, fetches an advisory summary when
   possible, and attempts cleanup. Use a real public post UUID from the search
   results instead of the placeholder:

   ```powershell
   py -3 scripts/sofa/research.py --confirm-observe-only --skill-digest $digest --model-name $model attention
   py -3 scripts/sofa/research.py --confirm-observe-only --skill-digest $digest --model-name $model search 'ASP.NET Core authorization'
   py -3 scripts/sofa/research.py --confirm-observe-only --skill-digest $digest --model-name $model post 'PUBLIC_POST_UUID'
   ```

   On Linux/macOS use `python3` and the equivalent shell variable syntax.
   Do not attempt public writes to test the restriction. Independently inspect
   the results, verify sources, and provide sanitized public evidence to the
   Learning Research agent as needed. Never pipe returned data into an executor.
5. Record date, reviewed contract digest, identity/scope verification outcome,
   operation outcomes and sanitized failure categories. Never log authorization
   headers, keys, session tokens, or raw sensitive responses. If authentication,
   rate limiting, network, contract or privilege checks fail, stop and
   investigate; do not automatically retry. Revoke the test credential when it
   is no longer needed. Record later runs separately; do not treat an earlier
   result as evidence for a changed contract, client or privilege boundary.

The normal Learning publishing workflows are unchanged. The offline proposal
helper and all CI tests require no SOFA network access, credentials or
third-party Python dependencies. No external contributions are authorized by
this runbook. If automated research is later enabled, review that change
separately and retain the maintainer-controlled publication boundary.

## Validation commands

From the repository root:

```text
pwsh ./scripts/Validate-AgentGovernance.ps1 -SelfTest
pwsh ./scripts/Validate-AgentGovernance.ps1
python3 -B -m unittest discover -s scripts/sofa -p "test_*.py" -v
```

The fixture-based validator self-test rejects missing or malformed profiles,
expanded tools, MCP configuration, and weakened repository or runbook guidance.
This is configuration validation, not a runtime permission test. The dedicated
Learning Agent Validation workflow runs both governance checks and the same
offline Python test suite without live credentials. Standard documentation,
link and sample validation workflows continue to apply. A synthetic pass is not
an authenticated smoke-test result.
