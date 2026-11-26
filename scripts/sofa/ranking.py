# SPDX-License-Identifier: MIT
"""Maintainer-only ranked SOFA question listing; never reads Copilot or CI secrets."""
import argparse
import getpass
import json
import re
import sys
import warnings

from research import (RANKING_SORTS, ResearchClient, ResearchError, identifier,
                      validate_public_tag)

MAX_TITLE = 200
MAX_TAGS = 8
MAX_TAG = 50
MAX_RESULTS = 20
MAX_CANDIDATES = 50
ORGANIZATION_WEIGHT = 25
PROJECT_SIGNALS = {
    "AsiBackbone": {
        "tags": {"agent-governance", "audit-logging", "authorization", "policy-as-code"},
        "phrases": {"asibackbone", "accountable systems", "capability grant",
                    "decision evidence", "governed execution", "policy enforcement"},
    },
    "NetCoreApplicationTemplate": {
        "tags": {"asp.net-core", "entity-framework-core", "problem-details",
                 "rate-limiting", "security-headers", "structured-logging"},
        "phrases": {"asp.net core", "forwarded headers", "net core application template",
                    "netcoreapplicationtemplate", "problem details", "rate limiting",
                    "security headers", "structured logging"},
    },
    "ReferenceApplication": {
        "tags": {"benchmarking", "integration-testing", "observability"},
        "phrases": {"audit evidence", "evidence explorer", "governance overhead",
                    "integration walkthrough", "reference application", "referenceapplication"},
    },
    "Learning": {
        "tags": {"design-patterns", "developer-education", "software-architecture"},
        "phrases": {"architecture education", "asibackbone learning", "design pattern",
                    "failure mode", "software architecture"},
    },
}


def _text(value, name, maximum):
    if (not isinstance(value, str) or not value.strip() or len(value) > maximum
            or any(ord(character) < 32 or ord(character) == 127 for character in value)):
        raise ResearchError("Invalid ranked question " + name)
    return value


def _trust_score(summary):
    if summary is None:
        return None
    if not isinstance(summary, dict):
        raise ResearchError("Invalid ranked question trust summary")
    score = summary.get("score")
    if score is None:
        return None
    if isinstance(score, bool) or not isinstance(score, int) or not -100 <= score <= 100:
        raise ResearchError("Invalid ranked question trust score")
    return score


def _project_matches(title, tags):
    normalized_title = " " + re.sub(r"[^a-z0-9+#.]+", " ", title.casefold()).strip() + " "
    normalized_tags = {value.casefold() for value in tags}
    projects = []
    signals = set()
    for project, profile in PROJECT_SIGNALS.items():
        tag_matches = sorted(normalized_tags.intersection(profile["tags"]))
        phrase_matches = sorted(
            phrase for phrase in profile["phrases"]
            if " " + phrase + " " in normalized_title
        )
        if tag_matches or phrase_matches:
            projects.append(project)
            signals.update("tag:" + value for value in tag_matches)
            signals.update("title:" + value for value in phrase_matches)
    return projects, sorted(signals)


def normalize_listing(data, ranking, candidate_limit, result_limit=10, tag=None):
    """Return bounded, allowlisted metadata in the server-provided rank order."""
    if ranking not in RANKING_SORTS:
        raise ResearchError("Unsupported question ranking")
    if (isinstance(candidate_limit, bool) or not isinstance(candidate_limit, int)
            or not 1 <= candidate_limit <= MAX_CANDIDATES):
        raise ResearchError("Ranking candidate limit must be from 1 to 50")
    validate_public_tag(tag)
    if (isinstance(result_limit, bool) or not isinstance(result_limit, int)
            or not 1 <= result_limit <= MAX_RESULTS
            or result_limit > candidate_limit):
        raise ResearchError("Ranking result limit must be from 1 to 20 and not exceed candidates")
    if not isinstance(data, dict) or not isinstance(data.get("items"), list):
        raise ResearchError("Invalid ranked question listing")
    items = data["items"]
    if len(items) > candidate_limit:
        raise ResearchError("Ranked question listing exceeded requested limit")

    ranked = []
    identifiers = set()
    for position, item in enumerate(items, start=1):
        if not isinstance(item, dict) or item.get("content_type") != "question":
            raise ResearchError("Invalid ranked question item")
        post_id = identifier(item.get("id"))
        if post_id in identifiers:
            raise ResearchError("Duplicate ranked question identifier")
        identifiers.add(post_id)
        tags = item.get("tags", [])
        if not isinstance(tags, list) or len(tags) > MAX_TAGS:
            raise ResearchError("Invalid ranked question tags")
        clean_tags = [_text(value, "tag", MAX_TAG) for value in tags]
        title = _text(item.get("title"), "title", MAX_TITLE)
        projects, signals = _project_matches(title, clean_tags)
        organization_weight = ORGANIZATION_WEIGHT if projects else 0
        base_score = len(items) - position + 1
        ranked.append({
            "sofaRank": position,
            "weightedScore": base_score + organization_weight,
            "organizationWeight": organization_weight,
            "matchedProjects": projects,
            "matchedSignals": signals,
            "id": post_id,
            "title": title,
            "tags": clean_tags,
            "trustScore": _trust_score(item.get("trust_summary")),
            "url": "https://agents.stackoverflow.com/questions/" + post_id,
        })

    ranked.sort(key=lambda item: (-item["weightedScore"], item["sofaRank"], item["id"]))
    ranked = ranked[:result_limit]
    for position, item in enumerate(ranked, start=1):
        item["rank"] = position

    return {
        "sort": ranking,
        "contentType": "question",
        "tag": tag,
        "candidateCount": len(items),
        "count": len(ranked),
        "organizationWeight": ORGANIZATION_WEIGHT,
        "items": ranked,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--confirm-observe-only", action="store_true",
                        help="Confirm separate key and dashboard scope were reviewed")
    parser.add_argument("--skill-digest", required=True)
    parser.add_argument("--model-name", required=True, help="Actual requesting agent model")
    parser.add_argument("--diagnostics", action="store_true",
                        help="Emit sanitized operation diagnostics to stderr")
    parser.add_argument("--limit", type=int, default=10,
                        help="Maximum ranked questions to emit (1-20; default 10)")
    parser.add_argument("--candidate-limit", type=int, default=50,
                        help="SOFA candidate pool size (1-50; default 50)")
    parser.add_argument("--tag", help="Optional exact lowercase public tag")
    parser.add_argument("ranking", choices=["hot", "newest", "trust"])
    args = parser.parse_args()
    try:
        validate_public_tag(args.tag)
    except ResearchError:
        print("Use a valid exact lowercase public tag.", file=sys.stderr)
        return 1
    if (not args.confirm_observe_only or not sys.stdin.isatty() or not sys.stderr.isatty()
            or not 1 <= args.limit <= MAX_RESULTS
            or not 1 <= args.candidate_limit <= MAX_CANDIDATES
            or args.limit > args.candidate_limit):
        print("Use a maintainer terminal and separate Observe only key.", file=sys.stderr)
        return 1

    client = None
    try:
        with warnings.catch_warnings():
            warnings.simplefilter("error", getpass.GetPassWarning)
            key = getpass.getpass("Separate Observe only SOFA API key: ")
        client = ResearchClient(key, args.skill_digest, args.model_name, args.diagnostics)
        client.start()
        listing = client.ranked_questions(args.ranking, args.candidate_limit, args.tag)
        result = normalize_listing(listing, args.ranking, args.candidate_limit,
                                   args.limit, args.tag)
        try:
            client.summary()
        except ResearchError:
            print("Session summary unavailable.", file=sys.stderr)
        print(json.dumps({"untrustedRanking": result}, ensure_ascii=True,
                         indent=2, allow_nan=False))
        return 0
    except ResearchError as error:
        print(str(error), file=sys.stderr)
        return 1
    except (getpass.GetPassWarning, EOFError, KeyboardInterrupt, OSError):
        print("SOFA ranking stopped before completion.", file=sys.stderr)
        return 1
    finally:
        if client is not None:
            try:
                client.close()
            except ResearchError:
                print("Session close unconfirmed; allow it to expire.", file=sys.stderr)


if __name__ == "__main__":
    sys.exit(main())
