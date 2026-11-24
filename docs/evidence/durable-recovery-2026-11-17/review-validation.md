# Review-fix validation

November 17, 2026; local implementer verification on the uncommitted update branch. This is a
regression-test record, not a v2 live worker-restart capture or independent review.

| Check | Observed result |
| --- | --- |
| `dotnet restore` | Exit 0 |
| `dotnet format` | Exit 0 |
| `dotnet build` | Exit 0; 0 warnings, 0 errors on Windows |
| `dotnet test --project Tests/Caesarea.Deterministic.Tests/Caesarea.Deterministic.Tests.csproj --no-build` | 447 passed, 4 existing live-IDE skips, 0 failed |
| `scripts/Test-LinuxContainer.ps1` | Clean restore/build/test; 447 passed, 4 existing live-IDE skips, 0 failed |
| `python scripts/tests/test_durable_recovery_polling.py` | 6 passed |
| PowerShell AST parsing of initializer, launcher and poller | No syntax errors |
| `git diff --check` | Exit 0 |

The Linux clean build emitted SDK warning `AZFW0108`: the generated Functions extensions project
had not been restored before build and successfully used restore-during-build. No warning was
suppressed. The Windows incremental build had zero warnings/errors.

The added coverage exercises:

- One initial Hub/stage read failure recovering under the supplied activity retry policy;
  exhaustion before dispatch; explicit retry admission and its cap.
- Read-only reconciliation, bounded unknown outcomes, and pre-dispatch stale-review refusal.
- A real Hub HTTP request remaining in flight while its activity is interrupted, then replay
  observing the pending receipt, then the original command completing; only one device command.
- Polling readiness and terminal status across transient failures, authorization errors,
  missing review, serialized outcomes and null execution fields.

`RecoveryOrchestrationTests` uses a deterministic scheduler double that honors the supplied retry
policy. It tests our orchestration policy/branches, not the DTS engine. The command integration
test uses the actual Energy Hub HTTP endpoint with a controlled device gateway. No tests here
claim durability across Hub/storage restart or exactly-once physical effects.

The original temporary validation logs are identified in [validation hashes](validation-hashes.md).
Hashes identify the implementer's local logs; they do not establish independent provenance.
