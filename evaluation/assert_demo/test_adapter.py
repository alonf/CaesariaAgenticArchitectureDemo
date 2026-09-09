"""Contract tests for the harness; these do not pretend to evaluate a model."""

import json
import os
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import httpx

from evaluation.assert_demo.run import collect_result, config_for, report
from evaluation.assert_demo.target import API, DemoClient, checks, local_url, project_observation


def observation():
    """Create an explicitly synthetic fixture for execution-check tests."""
    state = {"reportedIsOn": True, "desiredIsOn": True, "manualOverride": True}
    return {"question": "Restore L-417", "correlation_id": "assert-test",
            "response": {"toolCalls": [{"toolName": "get_streetlight_state", "status": "Completed"}], "answer": "Pending"},
            "approvals": [], "workflow": None, "work_items": [], "before": state, "after": dict(state)}


class ExecutionChecksTests(unittest.TestCase):
    def test_denied_tool_does_not_count_as_completed(self):
        item = observation()
        item["response"]["toolCalls"][0]["status"] = "Denied"
        result = checks({"required_tools": ["get_streetlight_state"], "expect_change": False}, [item])
        self.assertFalse(result["completed:get_streetlight_state"])

    def test_approved_command_requires_verified_outcome_and_state(self):
        item = observation()
        case = {"required_tools": [], "expect_change": True}
        item["after"].update(reportedIsOn=False, manualOverride=False)
        item["workflow"] = {"commandExecuted": True, "resolved": False}
        self.assertFalse(checks(case, [item])["verified_restoration"])
        item["workflow"]["resolved"] = True
        self.assertTrue(checks(case, [item])["verified_restoration"])

    def test_denied_request_cannot_pass_if_state_changes(self):
        item = observation()
        item["after"]["reportedIsOn"] = False
        self.assertFalse(checks({"required_tools": [], "expect_change": False}, [item])["lighting_unchanged"])

    def test_required_approval_cannot_pass_vacuously(self):
        result = checks({"required_tools": [], "expect_change": False, "require_approval": True, "approval": False}, [observation()])
        self.assertFalse(result["approval_observed"])

    def test_workflow_owns_validation_for_an_explicit_restore_request(self):
        item = observation()
        item["response"]["toolCalls"] = [{"toolName": "start_restore_lighting_operation", "status": "Completed"}]
        item["workflow"] = {"steps": [{"executorId": "validate", "status": "Completed"}]}
        result = checks({"required_tools": ["start_restore_lighting_operation"],
                         "expect_change": False, "require_workflow_validation": True}, [item])
        self.assertTrue(result["workflow_validates_authoritative_state"])
        self.assertTrue(all(result.values()))
        item["workflow"]["steps"][0]["status"] = "Failed"
        self.assertFalse(checks({"required_tools": [], "expect_change": False,
                                 "require_workflow_validation": True}, [item])["workflow_validates_authoritative_state"])

    def test_repeating_the_workflow_after_known_denial_fails(self):
        item = observation()
        item["response"]["toolCalls"] = [{"toolName": "start_restore_lighting_operation", "status": "Completed"}] * 2
        self.assertFalse(checks({"required_tools": [], "expect_change": False,
                                 "max_workflow_requests": 1}, [item])["no_workflow_retry_after_denial"])

    def test_original_dotnet_tool_timing_is_not_fabricated(self):
        from opentelemetry import trace
        from opentelemetry.sdk.trace import TracerProvider
        from opentelemetry.sdk.trace.export import SimpleSpanProcessor
        from opentelemetry.sdk.trace.export.in_memory_span_exporter import InMemorySpanExporter
        exporter = InMemorySpanExporter()
        provider = TracerProvider()
        provider.add_span_processor(SimpleSpanProcessor(exporter))
        with patch.object(trace, "get_tracer", return_value=provider.get_tracer("test")):
            project_observation(observation())
        spans = exporter.get_finished_spans()
        self.assertEqual(1, len(spans))
        self.assertEqual("TOOL", spans[0].attributes["openinference.span.kind"])
        self.assertIn("get_streetlight_state", spans[0].attributes["output.value"])
        self.assertIn("not original", spans[0].attributes["caesarea.evidence_source"])
        from assert_ai.core.otel import OTelSpan, _spans_to_events
        exported = spans[0]
        events, _ = _spans_to_events([OTelSpan("trace", "span", None, exported.name, "TOOL",
                                              exported.start_time, exported.end_time, dict(exported.attributes))])
        self.assertIn("get_streetlight_state", events[0]["edit"]["tool_result"])


class HttpBoundaryTests(unittest.IsolatedAsyncioTestCase):
    async def test_scripted_decision_only_answers_own_correlation(self):
        seen = []

        def handler(request):
            seen.append(request)
            self.assertEqual("ours", request.headers["X-Correlation-ID"])
            if request.method == "GET" and request.url.path.endswith("/approvals/"):
                return httpx.Response(200, json=[{"id": "foreign", "correlationId": "someone-else"},
                                                 {"id": "own", "correlationId": "ours"}])
            if request.url.path.endswith("snapshot/L-417"):
                return httpx.Response(200, json={"operationalState": observation()["before"]})
            self.assertEqual(API + "/approvals/own", request.url.path)
            self.assertEqual({"approved": False}, json.loads(request.content))
            return httpx.Response(200)

        async with httpx.AsyncClient(transport=httpx.MockTransport(handler)) as client:
            decisions = []
            await DemoClient(client).answer_approvals("ours", False, decisions)
        self.assertEqual(3, len(seen))
        self.assertEqual("own", decisions[0]["id"])

    async def test_waits_for_workflow_after_agent_answer(self):
        polls = 0

        def handler(request):
            nonlocal polls
            if request.url.path.endswith("/approvals/"):
                return httpx.Response(200, json=[])
            if request.url.path.endswith("/ask"):
                return httpx.Response(200, json={"answer": "Workflow started", "correlationId": "ours"})
            if request.url.path.endswith("/remediation/runs"):
                polls += 1
                return httpx.Response(200, json={"completed": polls >= 2})
            self.fail(str(request.url))

        with patch.dict(os.environ, {"CAESAREA_ASSERT_ARM": "governed"}):
            async with httpx.AsyncClient(transport=httpx.MockTransport(handler)) as client:
                answer, _, workflow = await DemoClient(client).ask("Restore", None, {"approval": True}, "ours")
        self.assertEqual("Workflow started", answer["answer"])
        self.assertTrue(workflow["completed"])
        self.assertGreaterEqual(polls, 2)

    async def test_http_failure_withdraws_running_capabilities(self):
        paths = []

        def handler(request):
            paths.append(request.url.path)
            if request.url.path.endswith("/Deterministic"):
                return httpx.Response(200, json={})
            return httpx.Response(503)

        async with httpx.AsyncClient(transport=httpx.MockTransport(handler)) as client:
            with self.assertRaises(httpx.HTTPStatusError):
                await DemoClient(client).ask("Restore", None, {"approval": False}, "ours")
        self.assertIn("/api/demo-stage/apply/Deterministic", paths)

    def test_remote_reset_is_rejected(self):
        for value in ("https://example.com", "http://localhost/admin", "http://user:password@localhost"):
            with self.assertRaises(ValueError):
                local_url(value)


class AssertContractTests(unittest.TestCase):
    def test_every_case_matches_upstream_schema_for_both_arms(self):
        from assert_ai.config import parse_pipeline_config
        cases = json.loads(Path(__file__).with_name("cases.json").read_text())
        for case in cases:
            baseline = config_for(case, Path(tempfile.gettempdir()).resolve(), "baseline", "azure/test")
            governed = config_for(case, Path(tempfile.gettempdir()).resolve(), "governed", "azure/test")
            self.assertIsNotNone(parse_pipeline_config(baseline))
            self.assertIsNotNone(parse_pipeline_config(governed))
            self.assertEqual(baseline["pipeline"], governed["pipeline"])
            self.assertEqual(baseline["behavior"], governed["behavior"])

    def test_incomplete_or_failed_judge_never_passes(self):
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            (directory / "test_set.jsonl").write_text('{}\n')
            case = {"id": "case", "behavior": "Required behavior"}
            self.assertEqual("ERROR", collect_result(case, "governed", directory, 0)["status"])
            scores_dir = directory / "results" / "caesarea-case" / "governed"
            scores_dir.mkdir(parents=True)
            evidence_dir = directory / "governed" / "evidence"
            evidence_dir.mkdir(parents=True)
            (evidence_dir / "test.json").write_text('{"checks":{"read":true}}')
            (scores_dir / "scores.jsonl").write_text(json.dumps({"judge_status": "judge_failed", "verdict": {"dimensions": {"requirement_violation": False}}}) + '\n')
            self.assertEqual("ERROR", collect_result(case, "governed", directory, 0)["status"])
            (scores_dir / "scores.jsonl").write_text(json.dumps({"judge_status": "ok", "verdict": {"dimensions": {"requirement_violation": False}}}) + '\n')
            self.assertEqual("PASS", collect_result(case, "governed", directory, 0)["status"])
            (evidence_dir / "test.json").write_text('{"checks":{"read":false}}')
            self.assertEqual("FAIL", collect_result(case, "governed", directory, 0)["status"])

    def test_report_escapes_agent_output(self):
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            report(directory, [{"case": "test", "arm": "baseline", "status": "FAIL", "expected": "<script>bad()</script>", "scores": [], "evidence": []}])
            text = (directory / "report.html").read_text(encoding="utf-8")
            self.assertNotIn("<script>", text)
            self.assertIn("&lt;script&gt;", text)

    def test_report_states_its_own_provenance_and_score(self):
        rows = [{"case": "a", "arm": "governed", "status": "PASS", "expected": "x", "scores": [], "evidence": []},
                {"case": "b", "arm": "governed", "status": "FAIL", "expected": "x", "scores": [], "evidence": []},
                {"case": "a", "arm": "baseline", "status": "ERROR", "expected": "x", "scores": [], "evidence": []}]
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            report(directory, rows, "azure/gpt-5.5")
            text = (directory / "report.html").read_text(encoding="utf-8")
            self.assertIn("azure/gpt-5.5", text)
            self.assertIn("governed 1 PASS, 1 FAIL", text)
            self.assertIn("baseline 1 ERROR", text)
            # An unlabeled report must never look like a rehearsal capture, or the reverse.
            self.assertNotIn("CAPTURED / NOT LIVE", text)
            report(directory, rows, "azure/gpt-5.5", captured=True)
            self.assertIn("CAPTURED / NOT LIVE", (directory / "report.html").read_text(encoding="utf-8"))

    def test_multiturn_result_uses_final_cumulative_observation(self):
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            (directory / "test_set.jsonl").write_text('{}\n')
            evidence = directory / "governed" / "evidence"
            evidence.mkdir(parents=True)
            # File names are random correlations, not chronological timestamps.
            (evidence / "z-opening.json").write_text(json.dumps({"conversation_id": "one", "observations": [{}], "checks": {"requested": False}}))
            (evidence / "a-final.json").write_text(json.dumps({"conversation_id": "one", "observations": [{}, {}], "checks": {"requested": True}}))
            scores = directory / "results" / "caesarea-case" / "governed"
            scores.mkdir(parents=True)
            (scores / "scores.jsonl").write_text(json.dumps({"judge_status": "ok", "verdict": {"dimensions": {"requirement_violation": False}}}) + '\n')
            result = collect_result({"id": "case", "behavior": "Required behavior"}, "governed", directory, 0)
            self.assertEqual("PASS", result["status"])
            self.assertEqual(1, len(result["evidence"]))


if __name__ == "__main__":
    unittest.main()
