# ClearPilot

ClearPilot is a Windows cleanup assistant focused on safe cache cleanup, clear explanations, logs, and future extensibility.

The current version is a C#/.NET command-line menu tool.

## Project Layout

```text
src\ClearPilot.Cli        Console UI
src\ClearPilot.Core       Cleanup engine, rules, scanning, logging, settings, localization
tests\ClearPilot.Core.Tests
docs                       Product requirements and MVP plan
release\ClearPilot-v0.2.0  Current self-contained Windows release package
release\ClearPilot-v0.1.0  Previous self-contained Windows release package
assets\icon                Icon source and drafts
tools                      Local project scripts
```

## Run

Development build:

```text
ClearPilot.cmd
```

Release build:

```text
release\ClearPilot-v0.2.0\ClearPilot.cmd
```

## Build And Test

```powershell
.\.dotnet\dotnet.exe build .\ClearPilot.sln --no-restore
.\.dotnet\dotnet.exe test .\ClearPilot.sln --no-build --no-restore
```

## Publish

```powershell
.\.dotnet\dotnet.exe publish .\src\ClearPilot.Cli\ClearPilot.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false -o .\release\ClearPilot-v0.2.0
```

After publishing, keep `release\ClearPilot-v0.2.0\ClearPilot.cmd` as the recommended launcher because it configures the terminal window before starting `ClearPilot.exe`.

## Safety Model

- `S0 SAFE`: very-low-risk cleanup. Quick Safe Clean may clean these automatically.
- `S1 CONFIRM`: low-risk cache cleanup. ClearPilot requires explicit user selection.
- `S2 REVIEW`: analysis only. ClearPilot may open locations but will not delete files.
- `S3 MANUAL` and `BLOCKED`: not cleaned automatically.

Verified v0.2.0 boundaries:

- Quick Safe Clean only runs `RiskLevel.S0VeryLowRisk` rules.
- Recommended Cleanup scans and cleans only `RiskLevel.S1LowRisk` rules.
- Deep Space Analysis has no delete operation.
- Protected roots are blocked by `ProtectedPathPolicy`.
- Browser rules target cache folders only: `Cache`, `Code Cache`, and `GPUCache`.
- Browser cookies, passwords, bookmarks, history, sessions, and profiles are excluded.
- The MVP runs without administrator privileges.

ClearPilot v0.2.0 does not implement registry cleaning, driver cleaning, service cleaning, startup item cleaning, browser identity/session cleanup, risky user-data deletion, or automatic deep cleanup.

## v0.2.0 Release Notes

Recommended Cleanup has been expanded conservatively. New v0.2.0 recommended targets remain S1 confirm-first and are not cleaned automatically:

- Windows Error Reporting files under the current user's local app data.
- DirectX and GPU shader caches that drivers and apps can rebuild.
- Additional development caches for Maven, Deno, Bun, and Python bytecode files.
- Electron app UI caches limited to `Cache`, `Code Cache`, and `GPUCache`.
- Additional browser caches for Brave, Chromium, Vivaldi, Opera, and Firefox.

The browser and Electron expansions remain cache-only. They exclude cookies, passwords, bookmarks, history, sessions, profiles, local storage, databases, and identity data.

Quick Safe Clean and Recommended Cleanup now use a compact cleanup preview before any cleanup work:

- Quick Safe Clean still runs automatically, but first shows a short S0-only estimate with cleanup groups, file count, estimated space, and top items.
- Recommended Cleanup still requires explicit S1 selection, with the same compact estimate shown above the selectable list.
- The preview is intentionally lightweight and does not add a separate detail workflow.
- ClearPilot excludes its own settings, logs, reports, and development test artifacts from default cleanup and analysis results.

The current development focus is Deep Space Analysis improvements. This mode remains review-only:

- Scans common user-controlled folders such as Downloads, Desktop, Documents, Pictures, Videos, Music, source, repos, Projects, dev, workspace, workspaces, and code when they exist.
- Also includes safe, user-scoped cache roots when present, such as the user temp folder, Windows Error Reporting, DirectX/GPU shader caches, and common developer caches for NuGet, Gradle, npm, pnpm, Yarn, pip, Deno, Go, Cargo, and Maven.
- Reports top large files, nested large folders, old archives/installers, project dependency folders, framework build outputs, local build caches, and file type space summaries.
- Includes risk level, size, last modified time, explanation, and suggested manual action for each finding.
- Shows a scan summary, per-type totals, top space sources, and grouped findings.
- Supports interactive filtering by finding type and sorting by size or last modified time.
- Reduces duplicate large-folder noise when a dominant child folder already explains most of the space.
- Uses more specific review advice for videos, logs, temporary-looking files, backups, archives, disk images, installers, project dependency folders, build outputs, local build caches, test caches, and coverage output.
- Localizes Deep Space Analysis explanations and suggested actions when the UI language is Simplified Chinese.
- Exports a structured Markdown report with summary tables, type breakdown bars, top sources, and grouped findings.
- Opens File Explorer and selects the generated report after export when possible.
- Opens file or folder locations for review, but does not delete files.
- Uses a more polished CLI color theme with function-color navigation in the main menu while preserving consistent safety colors.

Deep Space Analysis reports are stored under:

```text
%LOCALAPPDATA%\ClearPilot\reports
```

## Release Checklist

Before treating a release folder as ready:

- Run build and tests.
- Start `release\ClearPilot-v0.2.0\ClearPilot.cmd` and exit from the main menu.
- Switch UI language in Settings and confirm Simplified Chinese is readable.
- Run Cleanup History and confirm it handles an empty or inaccessible log folder without crashing.
- Run Deep Space Analysis and confirm it clearly says analysis-only.
- Run Scan Recommended Items and cancel without deleting.
- Run Quick Safe Clean only after reviewing that S0 rules are expected for the test machine.
- Confirm the release folder contains `ClearPilot.exe`, `ClearPilot.cmd`, `README.md`, and intentionally included icon assets.
- Confirm no `Debug`, `obj`, test output, or temporary workspace folders are included.
- Update `CHANGELOG.md` and the release `README.md` when behavior changes.

## Logs

Cleanup logs are stored under:

```text
%LOCALAPPDATA%\ClearPilot\logs
```

The default log retention is 7 days.

## Documentation

- `docs\requirements.md`: product requirements and safety policy.
- `docs\mvp-plan.md`: implementation plan and phases.
- `CHANGELOG.md`: release history.
