# Unit 00 source baseline

- Production branch: `video/maf-course-introduction` (local; not published).
- Committed .NET 11 base: `89d5a513be97301857dd4f156ce0773103712d51`.
- Parent: `upgrade/net11-foundry-insights-routines`, not GitHub main.
- global.json: SDK 11.0.100, latestFeature, no prerelease; application targets net11.0.
- AppHost SDK 13.6.5; Microsoft.Testing.Platform / xunit.v3.
- Recording/rehearsal worktree: `video-course/.build/unit00-lab`, detached at that exact base;
  `git status --short` is empty. Pending durable UI edits in the main workspace are preserved
  and excluded from this baseline.
- Fresh clean-worktree checks on November 24: restore succeeded; build succeeded with one
  AZFW0108 warning (Functions extensions restored during build); tests **447 passed, 4 skipped**.
  The earlier dirty working-tree result of 469 passes is not this baseline's result.
- `dotnet format --verify-no-changes --no-restore` also passed on that clean baseline.

Record the full revision and dirty status with the execution evidence. A released course
needs a reachable pinned revision or instructor-supplied local Git bundle. A local branch name
alone is not a reproducible remote-clone instruction.

Merging .NET 11 to main is unnecessary for this pilot and does not replace a pinned revision.
Review the upgrade/recovery changes and their checks before that separate integration decision.
This course task performs no merge, push, cloud deployment or shared-setting change.
