"""Exercise the PowerShell rehearsal poller against a local HTTP boundary (stdlib only)."""

import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


HELPER = Path(__file__).resolve().parents[1] / "Wait-DurableRecovery.ps1"
PWSH = shutil.which("pwsh")


@unittest.skipUnless(PWSH, "PowerShell 7 is required")
class DurableRecoveryPollingTests(unittest.TestCase):
    def poll(self, responses, state="AwaitingApproval", timeout=5):
        calls = []

        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                calls.append(self.path)
                code, body = responses[min(len(calls) - 1, len(responses) - 1)]
                self.send_response(code)
                self.send_header("Content-Type", "application/json")
                self.end_headers()
                self.wfile.write(json.dumps(body).encode())

            def log_message(self, *_):
                pass

        with ThreadingHTTPServer(("127.0.0.1", 0), Handler) as server:
            thread = threading.Thread(target=server.serve_forever, daemon=True)
            thread.start()
            try:
                with tempfile.TemporaryDirectory() as directory:
                    evidence = Path(directory) / "status.json"
                    env = dict(os.environ, CAESAREA_POLL_TEST_HELPER=str(HELPER),
                               CAESAREA_POLL_TEST_EVIDENCE=str(evidence),
                               CAESAREA_POLL_TEST_URI=f"http://127.0.0.1:{server.server_port}/api",
                               CAESAREA_POLL_TEST_STATE=state,
                               CAESAREA_POLL_TEST_TIMEOUT=str(timeout))
                    command = (
                        "$ErrorActionPreference='Stop'; "
                        "& $env:CAESAREA_POLL_TEST_HELPER -OperationId repair-test "
                        "-State $env:CAESAREA_POLL_TEST_STATE "
                        "-Headers @{'X-Caesarea-Key'='synthetic-test-key'} "
                        "-BaseUri $env:CAESAREA_POLL_TEST_URI "
                        "-TimeoutSeconds $env:CAESAREA_POLL_TEST_TIMEOUT "
                        "-EvidencePath $env:CAESAREA_POLL_TEST_EVIDENCE | ConvertTo-Json -Depth 20"
                    )
                    result = subprocess.run([PWSH, "-NoProfile", "-NonInteractive", "-Command", command],
                                            env=env, capture_output=True, text=True, timeout=20)
                    saved = json.loads(evidence.read_text(encoding="utf-8-sig")) if evidence.exists() else None
                    return result, calls, saved
            finally:
                server.shutdown()
                thread.join()

    def test_waits_for_valid_review_and_retries_transient_read(self):
        from datetime import datetime, timedelta, timezone

        review = {"ExpiresAt": (datetime.now(timezone.utc) + timedelta(minutes=5)).isoformat()}
        result, calls, saved = self.poll([
            (503, {}),
            (200, {"runtimeStatus": "Pending", "progress": None}),
            (200, {"runtimeStatus": "Running", "progress": {"State": "AwaitingApproval", "Review": None}}),
            (200, {"runtimeStatus": "Running", "progress": {"State": "AwaitingApproval", "Review": review}}),
        ])
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(4, len(calls))
        # PowerShell may serialize the same instant using the machine's local offset.
        self.assertEqual(datetime.fromisoformat(review["ExpiresAt"]),
                         datetime.fromisoformat(saved["progress"]["Review"]["ExpiresAt"]))
        self.assertNotIn("synthetic-test-key", json.dumps(saved))

    def test_polls_terminal_and_deserializes_business_outcome(self):
        outcome = {"State": "Recovered", "CommandExecuted": True, "Resolved": True}
        result, calls, saved = self.poll([
            (200, {"runtimeStatus": "Running", "progress": {"State": "Reconciling"}}),
            (200, {"runtimeStatus": "Completed", "outcome": json.dumps(outcome)}),
        ], "Terminal")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(2, len(calls))
        self.assertEqual(outcome, saved["outcome"])

    def test_unknown_outcome_remains_null(self):
        outcome = {"State": "OutcomeUnknown", "CommandExecuted": None, "Resolved": None}
        result, _, saved = self.poll([(200, {"runtimeStatus": "Completed", "outcome": json.dumps(outcome)})], "Terminal")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIsNone(saved["outcome"]["CommandExecuted"])

    def test_terminal_before_approval_fails(self):
        result, calls, saved = self.poll([(200, {"runtimeStatus": "Failed"})])
        self.assertNotEqual(0, result.returncode)
        self.assertIn("terminal state", result.stderr)
        self.assertEqual(1, len(calls))
        self.assertIsNone(saved)

    def test_authorization_error_is_not_retried(self):
        result, calls, saved = self.poll([(401, {})])
        self.assertNotEqual(0, result.returncode)
        self.assertEqual(1, len(calls))
        self.assertIsNone(saved)

    def test_missing_review_times_out(self):
        result, _, saved = self.poll([(200, {"runtimeStatus": "Running", "progress": {"State": "AwaitingApproval", "Review": None}})], timeout=1)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("Timed out", result.stderr)
        self.assertIsNone(saved)


if __name__ == "__main__":
    unittest.main()
