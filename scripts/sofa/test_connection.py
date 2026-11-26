# SPDX-License-Identifier: MIT
"""Maintainer-only SOFA connection test; performs no content or contribution action."""
import argparse
import getpass
import json
import sys
import warnings

from research import ResearchClient, ResearchError


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--confirm-connection-only", action="store_true",
                        help="Confirm this is a manual connection-only check")
    parser.add_argument("--skill-digest", required=True)
    parser.add_argument("--model-name", required=True, help="Actual requesting agent model")
    parser.add_argument("--diagnostics", action="store_true",
                        help="Emit sanitized operation diagnostics to stderr")
    args = parser.parse_args()
    if (not args.confirm_connection_only or not sys.stdin.isatty()
            or not sys.stderr.isatty()):
        print("Use a trusted maintainer terminal for a connection-only check.",
              file=sys.stderr)
        return 1

    client = None
    try:
        with warnings.catch_warnings():
            warnings.simplefilter("error", getpass.GetPassWarning)
            key = getpass.getpass("SOFA Learning Agent API key: ")
        client = ResearchClient(key, args.skill_digest, args.model_name,
                                args.diagnostics)
        activation_handoff = client.start(allow_activation_handoff=True)
        client.research("identity")  # Authenticate only; never emit the response.
        summary_read = True
        try:
            client.summary()  # Advisory lifecycle read; never emit the response.
        except ResearchError:
            summary_read = False
            print("Session summary unavailable.", file=sys.stderr)
        print(json.dumps({
            "connectionTest": {
                "sessionInitialized": True,
                "identityRead": True,
                "activationHandoffPresent": activation_handoff,
                "summaryRead": summary_read,
            }
        }, ensure_ascii=True, indent=2, allow_nan=False))
        return 0
    except ResearchError as error:
        print(str(error), file=sys.stderr)
        return 1
    except (getpass.GetPassWarning, EOFError, KeyboardInterrupt, OSError):
        print("SOFA connection test stopped before completion.", file=sys.stderr)
        return 1
    finally:
        if client is not None:
            try:
                client.close()
            except ResearchError:
                print("Session close unconfirmed; allow it to expire.", file=sys.stderr)


if __name__ == "__main__":
    sys.exit(main())
