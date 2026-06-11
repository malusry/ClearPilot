# Changelog

## Unreleased - v0.4.3

### Summary

- Adds a BubblePet/WebView-safe variant of the non-interactive recommended cleanup command.

### Added

- Added external-caller safe mode:
  - `ClearPilot.exe clean --recommended --json --external-caller bubblepet`
- Added dry-run validation support for the external caller path:
  - `ClearPilot.exe clean --recommended --json --external-caller bubblepet --dry-run`
- Added explicit skipped logging with reason:
  - `protected-running-app-cache`

### Safety

- Existing `ClearPilot.exe clean --recommended --json` behavior is unchanged.
- BubblePet mode remains non-interactive, JSON-only, and does not wait for stdin.
- BubblePet mode skips protected desktop-app runtime cache roots, including:
  - `%APPDATA%\com.bubblepet.translator`
  - `%LOCALAPPDATA%\com.bubblepet.translator`
  - `%LOCALAPPDATA%\Packages\*\LocalCache`
  - `GPUCache`, `GrShaderCache`, `ShaderCache`, `D3DSCache`, `DXCache`, `GLCache`, `ComputeCache`
- Skipped protected app-cache paths are counted as skipped, not failed.
- No cleanup rule coverage, risk classification, path safety, or deletion-time revalidation is relaxed.

### Validation

- NonInteractiveCliCommandTests: 13 passed, 0 failed, 0 skipped.
- Full tests: 293 passed, 0 failed, 0 skipped.
- `win-x64` self-contained RC package generated at `artifacts\rc\v0.4.3-win-x64-self-contained`.
- BubblePet dry-run smoke passed with JSON-only stdout, exit code 0, and `protected-running-app-cache` in the recommended cleanup log.

## v0.4.2 - 2026-06-07

### Summary

- Patch release for desktop-app integration via a stable non-interactive cleanup command.

### Added

- Added non-interactive CLI entry support for:
  - `ClearPilot.exe clean --recommended --json`
- The command:
  - does not open the interactive menu
  - does not wait for keyboard input
  - writes a single JSON document to stdout
  - runs Quick Safe first, then the safe `A/all` equivalent of Recommended Cleanup
- Added argument-error JSON responses for invalid `--json` command usage.
- Added CLI regression coverage for non-interactive routing, JSON structure, safe selection boundaries, and partial-failure behavior.

### Safety

- No cleanup eligibility, risk classification, rule coverage, path safety, process guard semantics, or deletion-time revalidation changes.
- Quick Safe remains S0-only.
- Recommended Cleanup remains confirmed-S1-only.
- Non-interactive recommended cleanup selects only the same safe recommended S1 set as the existing `A/all` path.

### Validation

- Full tests: 289 passed, 0 failed, 0 skipped.
- Self-contained `win-x64` publish validated with non-interactive JSON smoke coverage.

## v0.4.1 - 2026-05-24

### Summary

- Patch release for Quick Safe Clean CLI layout polish.

### Changed

- Moved the no-elevation / no service stop / no ACL change / no forced unlock message to the bottom of the Quick Safe Clean result page as a final safety hint.
- Updated package metadata version to `0.4.1`.

### Safety

- No cleanup eligibility, rule, scanner, path safety, process guard, or deletion execution changes.
- Quick Safe Clean remains S0-only.

## v0.4.0 - 2026-05-23

### Summary

- Released v0.4.0 with broader conservative cleanup coverage, improved advisor-style CLI output, and self-contained Windows packaging.

### Highlights

- Quick Safe Clean: S0-only boundary clarified with better summaries.
- Recommended Cleanup: confirmed-S1-only, conclusion-first fields, safer `A/all` selection, field-specific label/value colors.
- Deep Space Analysis: read-only/no-delete, simplified cards, Downloads read-only insight, Zoom read-only evidence profile.
- Reports v2: advisor-style structure with decision classes and explicit BLOCKED finality wording.
- Conservative S1 coverage expansion for:
  - App profiles (Discord/Slack/Teams/VS Code/JetBrains)
  - Package manager caches (npm/pnpm/Yarn/NuGet/pip/Cargo/Gradle/Maven/Deno/Bun/Composer/Go)
  - Windows user diagnostics (CrashDumps/WER ReportArchive/WER Temp/WER ReportQueue under strict exclusions)

### Safety

- Safety gates and deletion-time controls are unchanged.
- Quick: S0 only.
- Recommended: confirmed S1 only.
- Deep Space: no delete.
- S2/S3/BLOCKED: non-deletable.
- System-managed diagnostics and Windows servicing/update internals remain read-only or blocked.

### Validation

- Full tests: 280 passed, 0 failed, 0 skipped.
- RC package adjustment validation:
  - framework-dependent publish works but requires global .NET runtime for direct EXE launch
  - `win-x64` self-contained publish validated for direct EXE launch without global runtime
  - validated local self-contained output size: 80,885,281 bytes (~77.14 MB), 194 files

### Follow-ups

- Clean up `MessageCatalog.DeepAnalysisSuggestedAction` legacy key.
- Perform real terminal visual QA before release candidate.
- Add stronger `NO_COLOR` / redirected output / Windows Terminal theme end-to-end checks.
- Future Zoom S1 cleanup requires explicit Safety Reviewer approval.

## v0.3.0 - 2026-05-19

### Highlights

- Finalized the safety foundation with strict S0/S1/S2/S3/BLOCKED gates and path-safety enforcement.
- Added direct cleanup decision labels:
  - Recommended to clean
  - Not recommended to clean
  - Analysis only, do not clean
  - Blocked
- Expanded conservative Windows cache coverage and game launcher cache/log coverage with process guards.
- Improved Deep Space Analysis UX, localized guidance (including zh-CN), and CJK wrapping readability.
- Updated the app icon and regenerated Windows icon assets for packaging.

### Safety Guarantees

- Quick Safe Clean cleans S0 only.
- Recommended Cleanup requires explicit confirmation and cleans S1 only.
- Deep Space Analysis never deletes files.
- S2/S3/BLOCKED remain non-deletable.

### Explicit Non-Goals

- No registry cleaning.
- No driver cleaning.
- No browser identity/password/cookie/history/session cleanup.
- No game saves/configs/installed game cleanup.
- No Defender protected data cleanup.
- No administrator privilege requirement.

## Unreleased - v0.3.0 Chapter 5A (Pre-release Hardening)

### Added/Improved

- Pre-release QA pass for build quality, localization quality, and CLI consistency.
- zh-CN catalog cleanup for user-facing menu, cleanup, analysis, history, and settings surfaces.
- Documentation readiness refresh for v0.3.0 safety model and scope boundaries.

### Safety

- No cleanup mode semantics changed.
- No risk-gate behavior relaxed.
- No new cleanup targets added in this hardening pass.

## Unreleased - v0.3.0 Chapter 4.6 (Development Only)

### UX

- Tuned Deep Space Analysis console visual hierarchy:
  - kept decision colors stable
  - reduced cyan overuse in section headings
  - added a restrained size/space accent color

### Safety

- Presentation-only change; cleanup behavior unchanged.

## Unreleased - v0.3.0 Chapter 4.5 (Development Only)

### Added

- Deterministic direct cleanup decision model:
  - `RecommendedToClean`
  - `NotRecommendedToClean`
  - `AnalysisOnlyDoNotClean`
  - `Blocked`
- Decision metadata in scan/execution/log/report models.

### UX

- Decision-first cards in Recommended Cleanup and Deep Space.
- Primary Chinese decision wording:
  - `结论`
  - `建议清理`
  - `不建议清理`
  - `仅分析，不清理`
  - `已阻止`

### Safety

- Decision labels remain advisory only and do not override risk gates.

## Unreleased - v0.3.0 Chapter 4 (Development Only)

### Added

- Recommendation layer (`Recommended`/`Optional`/`Not Recommended`/`Review Only`/`Blocked`).
- Target advice metadata: reason, impact, action, safety note.
- Recommendation/advice fields in logs and reports.

### Safety

- `S2`/`S3`/`BLOCKED` remain non-deletable regardless of recommendation.

## Unreleased - v0.3.0 Chapter 3 (Development Only)

### Added

- Conservative S1 launcher cache/log coverage for:
  - Steam
  - Epic Games Launcher
  - Battle.net
  - Riot Client
  - EA App
  - Ubisoft Connect
- Process guard integration with structured skip results.
- Steam shader/depot review-only targets in Deep Space.

### Safety

- No process termination, no elevation, no service manipulation.
- Game installs/library/manifests/identity-sensitive data remain blocked.

## Unreleased - v0.3.0 Chapter 2 (Development Only)

### Added

- Conservative Windows cache coverage for S1 cleanup.
- System-managed Windows areas reported as S2 analysis-only.

### Safety

- Windows Update/Delivery Optimization/CBS/DISM/memory-dump areas remain analysis-only.

## Unreleased - v0.3.0 Chapter 1 (Development Only)

### Added

- Formal S0/S1/S2/S3/BLOCKED risk model and mode gates.
- `PathSafetyEngine` + deletion-time revalidation.
- `KnownSafeCacheRootWhitelist` enforcement.
- Structured safety-decision logging.

### Safety

- Quick: S0 only
- Recommended: confirmed S1 only
- Deep Space: no delete
- S3/BLOCKED: never deleted

## v0.2.0 - 2026-05-17

### Added

- Expanded conservative recommended-cache coverage and Deep Space analysis/reporting.
- Improved CLI cards, summaries, and report export workflows.

### Safety

- Browser cleanup remained cache-only (identity/session data excluded).
- Deep Space remained analysis-only.

## v0.1.0 - MVP Release Candidate

### Added

- Initial CLI experience with optional Simplified Chinese UI.
- Quick Safe Clean, Recommended Cleanup, Deep Space Analysis, Cleanup History, Settings.

### Safety

- Baseline protected-root and risk-mode separation established.
