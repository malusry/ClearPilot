# ClearPilot v0.4.3 Release Notes (Draft)

ClearPilot v0.4.3 is a draft patch release candidate. The latest published release remains v0.4.2 until a GitHub release is created.

## Summary

ClearPilot v0.4.3 keeps the v0.4 cleanup coverage and safety model, preserves the original non-interactive cleanup command, and adds a BubblePet/WebView-safe external-caller mode for desktop app integration.

## Patch Change

- Existing non-interactive desktop integration command remains available and unchanged:
  - `ClearPilot.exe clean --recommended --json`
- Added BubblePet / Tauri / WebView safe mode:
  - `ClearPilot.exe clean --recommended --json --external-caller bubblepet`
- Added dry-run validation for the BubblePet safe path:
  - `ClearPilot.exe clean --recommended --json --external-caller bubblepet --dry-run`
- These commands return exactly one JSON document on stdout, skip the interactive menu, and do not wait for `Console.ReadLine`.
- Quick Safe runs first, then Recommended Cleanup selects only the same safe recommended S1 set as the existing `A/all` behavior.
- BubblePet mode additionally skips protected running desktop-app cache paths and records `protected-running-app-cache` in the recommended cleanup log.

## v0.4 Line Summary

ClearPilot v0.4 focuses on broader visibility, conservative S1 cleanup expansion, clearer advisor-style decisions, and stronger UI/report clarity without relaxing safety gates.

## Highlights

### Quick Safe Clean

- S0-only automatic cleanup boundary clarified.
- Cleaner cleaned/skipped/failed summary output.
- No aggressive cleanup framing.

### Recommended Cleanup

- Confirmed-S1-only model preserved.
- Conclusion-first CLI fields:
  - Decision
  - Reason
  - Impact
  - Expected reclaim
  - Risk
- `A/all` selects only eligible recommended S1 items.
- Process-guard-blocked S1 items are excluded from bulk selection.
- One-step selection confirmation flow: submitting a valid selection starts cleanup; `0` cancels.
- Field-level and field-label semantic color system added.
- Non-interactive desktop integration now supports:
  - `clean --recommended --json`
  - `clean --recommended --json --external-caller bubblepet`
  - `clean --recommended --json --external-caller bubblepet --dry-run`
  - no menu
  - no prompt
  - no stdin wait
  - JSON-only stdout
- BubblePet mode skips WebView/Tauri/Electron/MS Store/GPU shader cache risk paths:
  - `%APPDATA%\com.bubblepet.translator`
  - `%LOCALAPPDATA%\com.bubblepet.translator`
  - `%LOCALAPPDATA%\Packages\*\LocalCache`
  - `GPUCache`, `GrShaderCache`, `ShaderCache`, `D3DSCache`, `DXCache`, `GLCache`, `ComputeCache`

### Deep Space Analysis

- Read-only/no-delete behavior preserved.
- Simplified result cards:
  - Decision
  - Risk
  - Path
  - Insight
  - Boundary
- Downloads included as read-only storage insight.
- Zoom added as read-only evidence profile.
- Desktop/Documents/Pictures/Videos/Music excluded by default.

### Reports v2

- Advisor-style structure:
  - cleaned
  - skipped
  - failed
  - recommended
  - not recommended
  - analysis-only
  - blocked
  - intentionally untouched
- Action-first primary fields removed from report output.
- BLOCKED finality wording made explicit.

## Coverage Expansion (Conservative)

### Application Profiles (S1, confirmed only)

- Discord
- Slack
- Microsoft Teams
- VS Code
- JetBrains IDEs

Constrained to conservative cache/log/completed crash diagnostics patterns with process guards and age thresholds. Identity/session/config/workspace/extensions/project data remain excluded.

### Package Manager Caches (S1, confirmed only)

- npm
- pnpm
- Yarn
- NuGet
- pip
- Cargo
- Gradle
- Maven
- Deno
- Bun
- Composer
- Go

Exact user-level cache roots only. Project-local dependency/build folders remain excluded.

### Windows Diagnostics (S1 user scope only)

- User CrashDumps
- WER ReportArchive
- WER Temp
- WER ReportQueue (active/pending/state/session/uploads/attachments exclusions)

System-managed diagnostics remain read-only or blocked:

- MEMORY.DMP
- Minidump
- Windows.old
- CBS/DISM
- Windows Update / Delivery Optimization

## Safety Guarantees Unchanged

- Quick Safe Clean remains S0-only.
- Recommended Cleanup remains confirmed-S1-only.
- Deep Space remains read-only/no-delete.
- S2/S3/BLOCKED remain non-deletable.
- Cleanup eligibility is not upgraded by wording, expected reclaim, or UI recommendation style.

ClearPilot still does not perform:

- registry cleaning
- driver cleaning
- service stop/kill operations
- ACL or ownership changes
- force-unlock behavior
- browser identity/session/profile cleanup
- game installs/saves/config/manifests/workshop/userdata cleanup
- Defender/security data cleanup

No administrator requirement is introduced for v0.4 cleanup flows.

## Validation Snapshot

- Full test suite: 293 passed, 0 failed, 0 skipped
- NonInteractiveCliCommandTests: 13 passed, 0 failed, 0 skipped

## Packaging Strategy

The recommended primary user-facing artifact is:

- `win-x64` self-contained package

Reason:

- framework-dependent publish works, but direct `ClearPilot.exe` launch requires a globally installed .NET runtime
- self-contained package includes runtime dependencies and runs directly without global .NET installation

Validated local package result:

- output folder: `artifacts\rc\v0.4.3-win-x64-self-contained`
- file count: `194`
- total size: `80,912,181 bytes` (~`77.16 MB`)

Validated v0.4.3 non-interactive smoke:

- `ClearPilot.exe clean --recommended --json --external-caller bubblepet --dry-run`
- JSON stdout only
- exit code `0` on completed flow
- menu text not emitted
- `recommended.skippedCount` includes protected app-cache skips
- recommended cleanup log contains `protected-running-app-cache`
- exit code `2` on invalid argument shape

The larger package size is expected for self-contained distribution.

## Known Follow-ups

- Clean up `MessageCatalog.DeepAnalysisSuggestedAction` legacy key.
- Perform real terminal visual QA before release candidate.
- Add stronger end-to-end `NO_COLOR` / redirected output / Windows Terminal theme visual checks.
- Add deeper real-system validation for Teams/WebView2 process attribution.
- Any future Zoom S1 cleanup requires explicit Safety Reviewer approval.
