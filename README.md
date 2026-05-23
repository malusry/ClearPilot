# ClearPilot

ClearPilot is a conservative Windows cleanup assistant. It focuses on cache cleanup, explainable decisions, and strict safety boundaries rather than aggressive system tweaking.

**Latest release:** [v0.4.0](https://github.com/malusry/ClearPilot/releases/tag/v0.4.0)

## Highlights

- Quick Safe Clean: S0-only automatic cleanup with clearer boundary and cleaner summaries.
- Recommended Cleanup: confirmed-S1-only, conclusion-first cards, safer bulk selection behavior.
- Deep Space Analysis: read-only/no-delete, simplified analysis cards, Downloads/Zoom insight only.
- Reports v2: advisor-style output for cleaned/skipped/failed and decision classes.
- Conservative expansion for app cache/log/crash diagnostics, package manager caches, and user diagnostics.
- English UI by default, with optional Simplified Chinese (zh-CN) and mojibake regressions covered.
- No administrator privileges required.

## Download

Download the latest Windows package from:

[ClearPilot v0.4.0 release](https://github.com/malusry/ClearPilot/releases/tag/v0.4.0)

The recommended v0.4.0 Windows package is the `win-x64` self-contained zip. It includes the .NET runtime, so a separate global .NET runtime installation is not required.

Release assets include:

- `ClearPilot-v0.4.0-win-x64-self-contained.zip`
- `SHA256SUMS.txt`

The zip contains:

- `ClearPilot.exe`
- runtime dependencies

To run:

```powershell
.\ClearPilot.exe
```

## v0.4 Packaging

The v0.4.0 release uses:

- `win-x64` self-contained package
- Includes the required .NET runtime
- Can run `ClearPilot.exe` directly on machines without a globally installed .NET runtime

Validated release package:

- Output folder: `artifacts\rc\v0.4.0-win-x64-self-contained`
- File count: `194`
- Total size: `80,885,281 bytes` (~`77.14 MB`)

The larger package size is expected for self-contained distribution.

## Cleanup Modes

ClearPilot separates cleanup into strict risk levels.

| Mode | What it can do |
| --- | --- |
| Quick Safe Clean | Automatically cleans `S0` very-low-risk items only. |
| Recommended Cleanup | Cleans `S1` low-risk items only after explicit confirmation. |
| Deep Space Analysis | Reports `S2` review-only items. It does not delete files. |

Higher-risk or blocked targets are never deleted by ClearPilot.

## Safety Model

| Level | Meaning |
| --- | --- |
| `S0` | Very low risk. Eligible for Quick Safe Clean. |
| `S1` | Low risk. Requires explicit confirmation. |
| `S2` | Review-only. Analysis and reporting only. |
| `S3` | High risk or system-managed. Not cleaned by ClearPilot. |
| `BLOCKED` | Explicitly forbidden. Never cleaned. |

Safety gates are enforced in the cleanup engine. User-facing recommendations do not override them.

## Main Modes (v0.4 Validated Behavior)

### Quick Safe Clean

- S0-only automatic cleanup.
- Focuses on known very-low-risk temporary/cache targets.
- Summarizes cleaned/skipped/failed counts and reclaimed bytes.
- Does not present deep/aggressive language.

### Recommended Cleanup

- Confirmed-S1-only.
- Conclusion-first cards:
  - `Decision`
  - `Reason`
  - `Impact`
  - `Expected reclaim`
  - `Risk`
- `A` selects only eligible recommended S1 items.
- Process-guard-blocked items are excluded from bulk selection.

### Deep Space Analysis

- Strictly read-only/no-delete.
- Simplified cards:
  - `Decision`
  - `Risk`
  - `Path`
  - `Insight`
  - `Boundary`
- Downloads is visible for read-only storage understanding.
- Zoom is visible as read-only evidence profile.
- Desktop/Documents/Pictures/Videos/Music are excluded by default.

### Reports

- Reports v2 advisor model:
  - cleaned
  - skipped
  - failed
  - recommended
  - not recommended
  - analysis-only
  - blocked
  - intentionally untouched

## v0.4 Highlights (Validated, Pre-Release)

### Recommended Cleanup and UI

- Conclusion-first output model in CLI.
- Cleaner confirmation boundary messaging.
- Field-specific semantic colors for labels and values.
- No action-first primary field.

### Deep Space

- Read-only/no-delete framing is explicit.
- Downloads read-only insight boundary.
- Zoom read-only evidence profile.
- Simplified card structure for fast review.

### Reports v2

- Advisor-style report sections and decision breakdown.
- Legacy action-first primary wording removed.
- BLOCKED finality wording clarified.

### Expanded S1 Coverage (Conservative)

#### Application profiles

- Discord
- Slack
- Microsoft Teams
- VS Code
- JetBrains IDEs

Coverage remains limited to conservative cache/log/completed crash-diagnostic patterns, with process guards and age thresholds.

#### Package manager caches

- npm, pnpm, Yarn
- NuGet, pip
- Cargo, Gradle, Maven
- Deno, Bun, Composer, Go

All package-manager cleanup targets remain S1 only. Project-local dependency/build folders are excluded.

#### Windows user diagnostics

- User `CrashDumps`
- WER `ReportArchive`
- WER `Temp`
- WER `ReportQueue` with active/pending/state/session/uploads/attachments exclusions

System-managed areas remain review-only or blocked.

## What ClearPilot Does Not Do

ClearPilot intentionally does not perform:

- Registry cleaning
- Driver cleaning
- Service cleaning or service stop/kill operations
- Browser identity/profile cleanup, including cookies, passwords, bookmarks, history, sessions, local storage, IndexedDB, or session storage
- Game install, save, config, mod, screenshot, recording, manifest, or library metadata cleanup
- Microsoft Defender quarantine, protection history, signatures, engine, or scan-state cleanup
- Service stopping, ACL changes, forced unlocks, or privilege escalation
- Windows servicing/update internals cleanup (`SoftwareDistribution`, Delivery Optimization, CBS, DISM, `Windows.old`)
- System memory dump cleanup (`MEMORY.DMP`, system `Minidump`)
- Personal files as cleanup targets
- Whole-root deletion of `Windows`, `Program Files`, `Program Files (x86)`, `ProgramData`, or user profile directories

## Supported Coverage Overview

### App profile coverage (S1 confirmed cleanup only)

- Discord / Slack / Microsoft Teams
- VS Code / JetBrains IDEs
- Conservative cache/log/completed crash diagnostics patterns only
- Process guard + age threshold + strict exclusions

### Package manager coverage (S1 confirmed cleanup only)

- npm / pnpm / Yarn
- NuGet / pip
- Cargo / Gradle / Maven
- Deno / Bun / Composer / Go
- Exact user-level cache roots only
- Project-local dependency/build folders excluded

### Windows diagnostics coverage

- User diagnostics S1:
  - CrashDumps
  - WER ReportArchive
  - WER Temp
  - WER ReportQueue (strict exclusions)
- System-managed diagnostics:
  - review-only or blocked (not cleanup targets)

## Design Principles

- Prefer skipping over guessing.
- Treat uncertain targets as review-only or blocked.
- Never require administrator privileges for supported cleanup.
- Explain the decision before deleting.
- Keep logs and reports auditable.

## Build From Source

Prerequisites:

- Windows
- .NET SDK matching `global.json`

Build and test:

```powershell
.\.dotnet\dotnet.exe build .\ClearPilot.sln --no-restore
.\.dotnet\dotnet.exe test .\ClearPilot.sln --no-build
```

Run the development launcher:

```powershell
.\ClearPilot.cmd
```

## Project Layout

```text
src\ClearPilot.Cli          Console UI
src\ClearPilot.Core         Cleanup engine, safety, scanning, logging, localization
tests\ClearPilot.Core.Tests Unit and regression tests
docs                         Product notes and requirements
release\ClearPilot-v0.3.0    Published v0.3.0 package
```

## Logs And Reports

By default:

- Cleanup logs: `%LOCALAPPDATA%\ClearPilot\logs`
- Deep Space reports: `%LOCALAPPDATA%\ClearPilot\reports`

ClearPilot records metadata about cleanup decisions and results. It does not log file contents.

## Release Notes

For v0.4.0 release notes, see [RELEASE_NOTES.md](RELEASE_NOTES.md).

## License

No license file is currently included. If you plan to reuse or redistribute the code, check the repository status first.
