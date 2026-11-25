# SPDX-License-Identifier: MIT
"""Maintainer-only SOFA research; never reads Copilot or CI secrets."""
import argparse
from datetime import datetime, timezone
import getpass
import http.client
import json
import re
import ssl
import sys
from urllib.parse import urlencode
from uuid import UUID
import warnings

HOST = "agents.stackoverflow.com"
TIMEOUT = 15
MAX_RESPONSE = 1024 * 1024
DIGEST = re.compile(r"[0-9a-f]{64}")
HEADER = re.compile(r"[!-~]{1,512}")


class ResearchError(Exception):
    """Safe diagnostics only: never include request/response contents."""


def header(value):
    if not isinstance(value, str) or not HEADER.fullmatch(value):
        raise ResearchError("Invalid or missing credential/metadata")
    return value


def identifier(value):
    try:
        if not isinstance(value, str) or str(UUID(value)) != value:
            raise ValueError()
        return value
    except (ValueError, AttributeError):
        raise ResearchError("Invalid resource identifier") from None


def unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError()
        result[key] = value
    return result


def reject_constant(_):
    raise ValueError()


def decode(raw):
    try:
        result = json.loads(raw.decode("utf-8"), object_pairs_hook=unique,
                            parse_constant=reject_constant)
        if not isinstance(result, (dict, list)):
            raise ValueError()
        return result
    except (ValueError, RecursionError):
        raise ResearchError("Invalid JSON response") from None


def safe_result(data, *secrets):
    # Reject literal credential echoes, including in nested keys and values.
    # This is not a general-purpose privacy or encoded-secret detector.
    try:
        encoded = json.dumps(data, ensure_ascii=True, allow_nan=False)
        if any(s and json.dumps(s, ensure_ascii=True)[1:-1] in encoded
               for s in secrets):
            raise ResearchError("Response contained credential material")
        return data
    except (ValueError, RecursionError):
        raise ResearchError("Invalid response data") from None


class ResearchClient:
    def __init__(self, key, reviewed_digest, model_name):
        self._key = header(key)
        if not isinstance(reviewed_digest, str) or not DIGEST.fullmatch(reviewed_digest):
            raise ResearchError("A reviewed skill response digest is required")
        self._digest = reviewed_digest
        self._model = header(model_name)
        self._session = None

    def _call(self, operation, value=None):
        # No caller-supplied method, origin, path, headers or request body.
        if operation == "skill":
            method, path, expected = "GET", "/skill.md", (200,)
        elif operation == "start":
            method, path, expected = "POST", "/api/sessions", (201,)
            if self._session is not None:
                raise ResearchError("Session already initialized")
        else:
            if self._session is None:
                raise ResearchError("No active session")
            routes = {
                "identity": "/api/me/agents",
                "attention": "/api/me/attention",
                "summary": "/api/sessions/current/summary",
            }
            method, expected = "GET", (200,)
            if operation in routes:
                path = routes[operation]
            elif operation == "search":
                if (not isinstance(value, str) or not value.strip()
                        or len(value) > 300 or any(ord(c) < 32 or ord(c) == 127 for c in value)):
                    raise ResearchError("Invalid public search term")
                path = "/api/posts?" + urlencode({"search": value, "page": 1, "per_page": 20})
            elif operation == "post":
                path = "/api/posts/" + identifier(value)
            elif operation == "close":
                method, path, expected = "DELETE", "/api/sessions/" + self._session, (200, 204)
            else:
                raise ResearchError("Operation not allowed")

        headers = {"Accept": "text/plain" if operation == "skill" else "application/json",
                   "Accept-Encoding": "identity"}
        if operation != "skill":
            headers.update({"Authorization": "Bearer " + self._key,
                            "X-Sofa-Skill-Digest": self._digest})
            if operation == "start":
                headers.update({"X-Sofa-Client-Name": "asibackbone-learning-maintainer",
                                "X-Sofa-Model-Name": self._model})
            else:
                headers["X-Sofa-Session"] = self._session

        connection = None
        try:
            # Direct verified TLS, no redirect following, proxy discovery or retry.
            connection = http.client.HTTPSConnection(
                HOST, 443, timeout=TIMEOUT, context=ssl.create_default_context())
            connection.request(method, path, headers=headers)
            response = connection.getresponse()
            if response.status not in expected:
                if response.status in (401, 403, 429):
                    raise ResearchError("SOFA rejected request (HTTP %d); stop and review" % response.status)
                if 300 <= response.status < 400:
                    raise ResearchError("Unexpected redirect rejected")
                raise ResearchError("Unexpected SOFA HTTP status")
            if response.getheader("Content-Encoding", "identity").lower() != "identity":
                raise ResearchError("Unexpected response encoding")
            length = response.getheader("Content-Length")
            if length is not None and (not length.isascii() or not length.isdecimal()
                                       or int(length) > MAX_RESPONSE):
                raise ResearchError("Invalid or oversized response length")
            raw = response.read(MAX_RESPONSE + 1)
            if len(raw) > MAX_RESPONSE:
                raise ResearchError("Response too large")
            if operation == "skill":
                if response.getheader("X-Sofa-Skill-Digest") != self._digest:
                    raise ResearchError("Skill digest changed or missing; review current contract")
                return None
            if operation == "close":
                return None
            if response.getheader("Content-Type", "").split(";")[0].strip().lower() != "application/json":
                raise ResearchError("Unexpected response content type")
            data = decode(raw)
            if operation == "start":
                # Session ID is runtime state only; never return this response to the CLI.
                if not isinstance(data, dict):
                    raise ResearchError("Invalid session response")
                self._session = identifier(data.get("session_id"))
                try:
                    expiry = datetime.fromisoformat(data["expires_at"].replace("Z", "+00:00"))
                    if expiry.tzinfo is None or expiry <= datetime.now(timezone.utc):
                        raise ValueError()
                except (KeyError, TypeError, AttributeError, ValueError, OverflowError):
                    raise ResearchError("Invalid session expiry") from None
                if data.get("activation_next_step"):
                    raise ResearchError("Unexpected activation handoff; verify agent scope")
                return None
            return safe_result(data, self._key, self._session)
        except (OSError, http.client.HTTPException, ValueError, OverflowError):
            raise ResearchError("SOFA transport or response failure") from None
        finally:
            if connection is not None:
                connection.close()

    def start(self):
        self._call("skill")
        self._call("start")

    def research(self, operation, value=None):
        if operation not in {"identity", "attention", "search", "post"}:
            raise ResearchError("Research operation not allowed")
        return self._call(operation, value)

    def summary(self):
        return self._call("summary")

    def close(self):
        if self._session is not None:
            try:
                self._call("close")
            finally:
                self._session = None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--confirm-observe-only", action="store_true",
                        help="Confirm separate key and dashboard scope were reviewed")
    parser.add_argument("--skill-digest", required=True)
    parser.add_argument("--model-name", required=True, help="Actual requesting agent model")
    parser.add_argument("operation", choices=["identity", "attention", "search", "post"])
    parser.add_argument("value", nargs="?")
    args = parser.parse_args()
    if (not args.confirm_observe_only or not sys.stdin.isatty() or not sys.stderr.isatty()
            or (args.operation in {"search", "post"}) != (args.value is not None)):
        print("Use a maintainer terminal, separate Observe only key and valid operation.", file=sys.stderr)
        return 1
    client = None
    try:
        with warnings.catch_warnings():
            warnings.simplefilter("error", getpass.GetPassWarning)
            key = getpass.getpass("Separate Observe only SOFA API key: ")
        client = ResearchClient(key, args.skill_digest, args.model_name)
        client.start()
        result = client.research(args.operation, args.value)
        try:
            summary = client.summary()
        except ResearchError:
            summary = None
            print("Session summary unavailable.", file=sys.stderr)
        print(json.dumps({"untrustedResearch": result, "sessionSummary": summary},
                         ensure_ascii=True, indent=2, allow_nan=False))
        return 0
    except (ResearchError, getpass.GetPassWarning, EOFError, KeyboardInterrupt, OSError):
        print("SOFA research stopped; review scope, digest, authentication or transport.", file=sys.stderr)
        return 1
    finally:
        if client is not None:
            try:
                client.close()
            except ResearchError:
                print("Session close unconfirmed; allow it to expire.", file=sys.stderr)


if __name__ == "__main__":
    sys.exit(main())
