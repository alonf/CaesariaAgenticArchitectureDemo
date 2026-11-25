# Lab 14 — evaluate observed behavior

Outcome: run a small real comparison and distinguish enforcement tests, behavioral checks, semantic scores and infrastructure errors. Allow 30 minutes. Code baseline `59017d7`; book chapter 13 pages 172–179. Agent and judge make real model calls against existing authorized resources; city data is synthetic. Results are samples, not guarantees.

## Setup and commands

Follow [ASSERT setup](../../evaluation/assert_demo/README.md). Use a dedicated running local demo; do not interact with the switchboard while the harness owns it. Read `cases.json` first. The Windows launcher uses `.venv-assert/Scripts/python.exe`; Mac uses `.venv-assert/bin/python` directly.

```powershell
./scripts/Invoke-AssertDemo.ps1 -PrepareOnly -Arm both
./scripts/Invoke-AssertDemo.ps1 -Arm both -Case current-state -Captured
./scripts/Invoke-AssertDemo.ps1 -Arm both -Case forgotten-override -Captured
```

```bash
.venv-assert/bin/python -m evaluation.assert_demo.run --arm both --case current-state --captured
.venv-assert/bin/python -m evaluation.assert_demo.run --arm both --case forgotten-override --captured
```

`-Captured` labels reports presented after the run. Preparation does not invoke models. The inference and judge connections are separate; configure the judge as described in the evaluation README.

## Recorded evidence and interpretation

Fresh run `20261125T044722Z-5fd3b0`: current-state PASS in baseline and governed; forgotten-override FAIL in baseline and PASS in governed. Only these two cases, once per arm, were measured in this recording. Local artifacts: `evaluation/assert_demo/artifacts/20261125T044722Z-5fd3b0/report.html`, `summary.json`, `manifest.json`, and per-case logs/observations. Keep original artifacts; do not edit scores.

Open the final report and expand each row. Baseline lacks completed `search_work_knowledge`; its honest limitation still fails the requested capability. Governed completes the required state and knowledge reads and distinguishes evidence from recommendation. The report requires both semantic judgment and concrete checks. It compares compositions, not a controlled experiment isolating one policy.

Two earlier attempts reported ERROR because the judge received mutually exclusive authentication inputs. The agent read succeeded and deterministic checks passed, but a missing judge score did not become a PASS. The successful retry removed key/token environment variables in a child Python process and used AAD only. No agent or requirement change was made between these runs. Do not print credentials or modify shared cloud settings to troubleshoot this.

The included `run-with-aad.py` reproduces that isolated environment and forwards arguments to the existing harness. It preserves your endpoint/version settings, removes conflicting key/token variables only in its process, and disables automatic `.env` loading. Configure `AZURE_API_BASE` and `AZURE_API_VERSION` first, then:

```powershell
.venv-assert/Scripts/python.exe video-course/unit-14-evaluation/run-with-aad.py --arm both --case current-state --case forgotten-override --captured
```

```bash
.venv-assert/bin/python video-course/unit-14-evaluation/run-with-aad.py --arm both --case current-state --case forgotten-override --captured
```

## Trace the implementation

- `evaluation/assert_demo/target.py`, `chat`: reset fixture, preserve session, before/after Hub observations and correlation.
- `checks`: completed tool status, forbidden direct write, approvals and correlated outcome checks.
- `project_observation`: reconstructed public execution evidence, not raw timing or hidden reasoning.
- `run.py`, `collect_result`: incomplete evidence becomes ERROR; baseline/governed scores remain distinct.

## Pause, verify, recover and reset

Predict current-state tool evidence, run it, and inspect the report plus Hub snapshot. Repeat forgotten-override and explain differences; model/ judge wording and scores can vary. On infrastructure ERROR, inspect the corresponding log and resolve connectivity/authentication before a new run. Do not count partial output as a completed evaluation.

After the harness exits and releases its lease, Director → Lights On Reported by a Client resets the incident for another lesson. Never reset during evaluation or a pending repair.

Investigate: Which checks are deterministic but depend on stochastic model behavior? Could the judge miss a specification blind spot? Why do repeated and held-out cases matter? What evidence distinguishes a proposed repair from a completed command? Add a reviewed missing-evidence or denial regression; do not promote generated cases or proposed fixes automatically.
