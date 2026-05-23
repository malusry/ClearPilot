# ClearPilot v0.4.0 Release Notes

ClearPilot v0.4.0 is the current stable release.

## Summary

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

- Full test suite: 280 passed, 0 failed, 0 skipped

## Packaging Strategy

The recommended primary user-facing artifact is:

- `win-x64` self-contained package

Reason:

- framework-dependent publish works, but direct `ClearPilot.exe` launch requires a globally installed .NET runtime
- self-contained package includes runtime dependencies and runs directly without global .NET installation

Validated local package result:

- output folder: `artifacts\rc\v0.4.0-win-x64-self-contained`
- file count: `194`
- total size: `80,885,281 bytes` (~`77.14 MB`)

The larger package size is expected for self-contained distribution.

## Known Follow-ups

- Clean up `MessageCatalog.DeepAnalysisSuggestedAction` legacy key.
- Perform real terminal visual QA before release candidate.
- Add stronger end-to-end `NO_COLOR` / redirected output / Windows Terminal theme visual checks.
- Add deeper real-system validation for Teams/WebView2 process attribution.
- Any future Zoom S1 cleanup requires explicit Safety Reviewer approval.
