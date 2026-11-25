# SPDX-License-Identifier: MIT
"""Offline proposal formatting. No credentials, network or subprocesses."""
import json
import sys
import unicodedata

MAX_BYTES = 65536
KINDS = {"blueprint", "playbook", "til", "question"}
FIELDS = {"title", "kind", "claim", "evidence", "limitations", "attribution",
          "searchStatus", "candidateTitles"}


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON field")
        result[key] = value
    return result


def required_text(value, limit=8000):
    if not isinstance(value, str) or not value.strip() or len(value) > limit:
        raise ValueError("Invalid text field")
    # Reject control characters other than ordinary line breaks and tabs.
    if any(unicodedata.category(c) in {"Cc", "Cs"} and c not in "\n\r\t"
           for c in value):
        raise ValueError("Invalid text character")
    return value.strip()


def title_key(value):
    return " ".join(unicodedata.normalize("NFKC", value).casefold().split())


def prepare(raw):
    if len(raw) > MAX_BYTES:
        raise ValueError("Input too large")
    data = json.loads(raw.decode("utf-8"), object_pairs_hook=unique_object)
    if not isinstance(data, dict) or set(data) != FIELDS:
        raise ValueError("Unexpected proposal fields")
    title = required_text(data["title"], 240)
    kind = required_text(data["kind"], 32)
    status = required_text(data["searchStatus"], 32)
    if kind not in KINDS or status not in {"not-performed", "maintainer-reviewed"}:
        raise ValueError("Invalid proposal category or search status")
    evidence = data["evidence"]
    if not isinstance(evidence, list) or not 1 <= len(evidence) <= 20:
        raise ValueError("Evidence required")
    checked_evidence = []
    for entry in evidence:
        if not isinstance(entry, dict) or set(entry) != {"source", "observation"}:
            raise ValueError("Invalid evidence entry")
        checked_evidence.append({
            "source": required_text(entry["source"], 2000),
            "observation": required_text(entry["observation"]),
        })
    candidates = data["candidateTitles"]
    if not isinstance(candidates, list) or len(candidates) > 100:
        raise ValueError("Invalid candidate list")
    matches = [candidate for candidate in
               (required_text(c, 240) for c in candidates)
               if title_key(candidate) == title_key(title)]
    return {
        "status": "local-proposal-unreviewed",
        "externalWrite": False,
        "title": title,
        "kind": kind,
        "claim": required_text(data["claim"]),
        "evidence": checked_evidence,
        "limitations": required_text(data["limitations"]),
        "attribution": required_text(data["attribution"]),
        "searchStatus": status,
        "possibleDuplicateTitles": matches,
        "reviewRequired": True,
    }


def main():
    try:
        result = prepare(sys.stdin.buffer.read(MAX_BYTES + 1))
        print(json.dumps(result, ensure_ascii=True, indent=2))
        return 0
    except (ValueError, RecursionError):
        # Never echo input or parser diagnostics, which may contain sensitive data.
        print("Invalid local proposal; check the documented schema and size.",
              file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
