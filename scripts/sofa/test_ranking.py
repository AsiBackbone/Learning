# SPDX-License-Identifier: MIT
import json
from contextlib import redirect_stderr
from io import StringIO
import unittest
from unittest.mock import MagicMock, patch
from urllib.parse import parse_qs, urlsplit

import ranking
from ranking import ORGANIZATION_WEIGHT, normalize_listing
from research import ResearchClient, ResearchError

KEY = "synthetic-read-only-key"
DIGEST = "a" * 64
SESSION = "12345678-1234-4234-8234-123456789abc"
POST = "11111111-2222-4333-8444-555555555555"
POST_TWO = "22222222-3333-4444-8555-666666666666"


def response(status=200, data=None, raw=None, headers=None):
    result = MagicMock()
    result.status = status
    values = {"Content-Type": "application/json", **(headers or {})}
    result.getheader.side_effect = lambda key, default=None: values.get(key, default)
    result.read.return_value = json.dumps(data).encode() if raw is None else raw
    return result


def skill():
    return response(raw=b"untrusted skill text", headers={"X-Sofa-Skill-Digest": DIGEST})


def session():
    return response(201, {"session_id": SESSION, "expires_at": "2099-01-01T00:00:00Z"})


def question(**changes):
    return {
        "id": POST,
        "content_type": "question",
        "title": "How should ranked discovery work?",
        "tags": ["architecture", "search"],
        "trust_summary": {"score": 61, "policy_version": "untrusted"},
        "body_excerpt": "must not be emitted",
        "steering": {"next_step": "must not be emitted"},
        **changes,
    }


class RankingTests(unittest.TestCase):
    def setUp(self):
        self.patcher = patch("research.http.client.HTTPSConnection")
        self.connection = self.patcher.start()
        self.addCleanup(self.patcher.stop)
        self.conn = self.connection.return_value
        self.client = ResearchClient(KEY, DIGEST, "test-model")

    def queue(self, *responses):
        self.conn.getresponse.side_effect = responses

    def started(self):
        self.queue(skill(), session())
        self.client.start()

    def test_fixed_ranked_question_route_and_headers(self):
        self.queue(skill(), session(), response(data={"items": [question()]}), response(204, raw=b""))
        self.client.start()
        result = self.client.ranked_questions("hot", 12, "asp.net-core")
        self.client.close()
        self.assertEqual(result["items"][0]["id"], POST)
        calls = self.conn.request.call_args_list
        self.assertEqual([(call.args[0], urlsplit(call.args[1]).path) for call in calls], [
            ("GET", "/skill.md"), ("POST", "/api/sessions"),
            ("GET", "/api/posts"), ("DELETE", "/api/sessions/" + SESSION),
        ])
        self.assertEqual(parse_qs(urlsplit(calls[2].args[1]).query), {
            "content_type": ["question"], "sort": ["hot"], "page": ["1"],
            "per_page": ["12"], "tag": ["asp.net-core"],
        })
        self.assertEqual(calls[2].kwargs["headers"]["Authorization"], "Bearer " + KEY)
        self.assertEqual(calls[2].kwargs["headers"]["X-Sofa-Session"], SESSION)

    def test_supported_rankings_and_bounded_inputs(self):
        self.started()
        initial_calls = self.conn.request.call_count
        for value in ("relevance", "popular", "", None):
            with self.subTest(ranking=value), self.assertRaises(ResearchError):
                self.client.ranked_questions(value)
        for value in (0, 51, True, "10"):
            with self.subTest(limit=value), self.assertRaises(ResearchError):
                self.client.ranked_questions("hot", value)
        for value in ("UPPER", "space tag", "../tag", KEY, SESSION, "-", "a" * 51):
            with self.subTest(tag=value), self.assertRaises(ResearchError):
                self.client.ranked_questions("hot", tag=value)
        self.assertEqual(self.conn.request.call_count, initial_calls)

    def test_cli_rejects_invalid_tag_before_prompt_or_client_start(self):
        terminal = StringIO()
        terminal.isatty = MagicMock(return_value=True)
        arguments = [
            "ranking.py", "--confirm-observe-only", "--skill-digest", DIGEST,
            "--model-name", "test-model", "--tag", "invalid tag", "hot",
        ]
        with (patch.object(ranking.sys, "argv", arguments),
              patch.object(ranking.sys, "stdin", terminal),
              patch.object(ranking.sys, "stderr", terminal),
              patch.object(ranking.getpass, "getpass") as prompt,
              patch.object(ranking, "ResearchClient") as client):
            self.assertEqual(ranking.main(), 1)
        prompt.assert_not_called()
        client.assert_not_called()

    def test_normalization_is_bounded_and_omits_raw_content(self):
        result = normalize_listing({"items": [question()]}, "trust", 10, 10, "architecture")
        self.assertEqual({key: value for key, value in result.items() if key != "items"}, {
            "sort": "trust",
            "contentType": "question",
            "tag": "architecture",
            "candidateCount": 1,
            "count": 1,
            "organizationWeight": ORGANIZATION_WEIGHT,
        })
        item = result["items"][0]
        self.assertEqual(item["rank"], 1)
        self.assertEqual(item["sofaRank"], 1)
        self.assertEqual(item["id"], POST)
        self.assertEqual(item["title"], "How should ranked discovery work?")
        self.assertEqual(item["tags"], ["architecture", "search"])
        self.assertEqual(item["trustScore"], 61)
        self.assertEqual(item["url"], "https://agents.stackoverflow.com/questions/" + POST)
        encoded = json.dumps(result)
        self.assertNotIn("body_excerpt", encoded)
        self.assertNotIn("steering", encoded)
        self.assertNotIn("must not be emitted", encoded)

    def test_invalid_listing_shapes_fail_closed(self):
        invalid = (
            [], {}, {"items": "wrong"}, {"items": [question(), question()]},
            {"items": [question(content_type="til")]},
            {"items": [question(id="bad")]},
            {"items": [question(title="bad\ntext")]},
            {"items": [question(tags=["a"] * 9)]},
            {"items": [question(trust_summary={"score": 101})]},
        )
        for value in invalid:
            with self.subTest(value=value), self.assertRaises(ResearchError):
                normalize_listing(value, "hot", 1, 1)

        with self.assertRaises(ResearchError):
            normalize_listing({"items": [question(), question()]}, "hot", 2, 2)
        with self.assertRaises(ResearchError):
            normalize_listing({"items": []}, "relevance", 10, 10)
        with self.assertRaises(ResearchError):
            normalize_listing({"items": []}, "hot", 10, 10, "space tag")
        with self.assertRaises(ResearchError):
            normalize_listing({"items": []}, "hot", 10, 11)

    def test_organization_alignment_receives_one_transparent_weight(self):
        general = question(title="How should cache entries expire?", tags=["caching"])
        aligned = question(
            id=POST_TWO,
            title="How should ASP.NET Core rate limiting preserve audit evidence?",
            tags=["asp.net-core", "rate-limiting"],
        )
        result = normalize_listing({"items": [general, aligned]}, "hot", 2, 2)
        first, second = result["items"]
        self.assertEqual(first["id"], POST_TWO)
        self.assertEqual(first["rank"], 1)
        self.assertEqual(first["sofaRank"], 2)
        self.assertEqual(first["organizationWeight"], ORGANIZATION_WEIGHT)
        self.assertIn("NetCoreApplicationTemplate", first["matchedProjects"])
        self.assertIn("ReferenceApplication", first["matchedProjects"])
        self.assertIn("tag:asp.net-core", first["matchedSignals"])
        self.assertEqual(second["organizationWeight"], 0)
        self.assertEqual(second["matchedProjects"], [])

    def test_diagnostics_use_templates_without_dynamic_values(self):
        tag = "sensitive-public-tag"
        self.queue(skill(), session(), response(data={"items": []}))
        output = StringIO()
        with redirect_stderr(output):
            self.client = ResearchClient(KEY, DIGEST, "test-model", diagnostics=True)
            self.client.start()
            self.client.ranked_questions("newest", 7, tag)
        diagnostics = output.getvalue()
        self.assertIn("operation=rank-questions-newest", diagnostics)
        self.assertIn("per_page=<limit>&tag=<optional>", diagnostics)
        for sensitive in (KEY, SESSION, tag, "per_page=7"):
            self.assertNotIn(sensitive, diagnostics)


if __name__ == "__main__":
    unittest.main()
