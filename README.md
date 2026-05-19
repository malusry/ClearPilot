# ClearPilot

ClearPilot is a conservative Windows cleanup assistant. It focuses on cache cleanup, explainable decisions, and strict safety boundaries rather than aggressive system tweaking.

**Latest release:** [v0.3.0](https://github.com/malusry/ClearPilot/releases/tag/v0.3.0)

## Highlights

- Quick Safe Clean for very-low-risk temporary files.
- Recommended Cleanup for low-risk cache/log targets, with explicit confirmation.
- Deep Space Analysis for review-only large files and system-managed areas.
- Direct cleanup decisions: `Recommended to clean`, `Not recommended to clean`, `Analysis only, do not clean`, and `Blocked`.
- English UI by default, with optional Simplified Chinese.
- No administrator privileges required.

## Download

Download the latest Windows package from:

[ClearPilot v0.3.0 release](https://github.com/malusry/ClearPilot/releases/tag/v0.3.0)

The release includes:

- `ClearPilot.exe`
- `ClearPilot.cmd`
- SHA256 checksum file

To run:

```powershell
.\ClearPilot.cmd
```

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

## What v0.3.0 Covers

### Windows cache and diagnostics

- Current user temp files
- Windows temp files where accessible without elevation
- Windows Error Reporting user-scope files
- User crash dumps, with diagnostic caution
- INetCache cache-only paths, excluding identity/session data
- Microsoft Store `LocalCache` paths
- Windows Update, Delivery Optimization, CBS/DISM logs, memory dumps, and `Windows.old` as review-only analysis

### Game launcher cache and logs

Conservative launcher-scoped coverage for:

- Steam
- Epic Games Launcher
- Battle.net
- Riot Client
- EA App
- Ubisoft Connect

Launcher targets are process-guarded and limited to known cache/log/dump paths. Installed games, saves, configs, manifests, downloads in progress, and library metadata are excluded.

## What ClearPilot Does Not Do

ClearPilot intentionally does not perform:

- Registry cleaning
- Driver cleaning
- Browser identity/profile cleanup, including cookies, passwords, bookmarks, history, sessions, local storage, IndexedDB, or session storage
- Game install, save, config, mod, screenshot, recording, manifest, or library metadata cleanup
- Microsoft Defender quarantine, protection history, signatures, engine, or scan-state cleanup
- Service stopping, ACL changes, forced unlocks, or privilege escalation
- Whole-root deletion of `Windows`, `Program Files`, `Program Files (x86)`, `ProgramData`, or user profile directories

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

## License

No license file is currently included. If you plan to reuse or redistribute the code, check the repository status first.
