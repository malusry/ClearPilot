# ClearPilot

ClearPilot is a Windows cleanup assistant focused on conservative cache cleanup, clear user guidance, and strong safety boundaries.

Current release: **v0.2.0**  
Current development line: **v0.3.0 (pre-release hardening)**

## What ClearPilot Does

- `Quick Safe Clean`: automatic cleanup for strictly `S0` very-low-risk targets only.
- `Recommended Cleanup`: user-confirmed cleanup for `S1` low-risk targets only.
- `Deep Space Analysis`: `S2` review-only analysis. No deletion.
- Cleanup history and structured logs for auditability.
- Optional Simplified Chinese UI (`zh-CN`), English default.

## Safety Model (v0.3.0)

- `S0` Very low risk: auto-clean eligible in Quick mode.
- `S1` Low risk: cleanup allowed only after explicit confirmation.
- `S2` Review-only: analyze and report only, never delete.
- `S3` High risk/manual: never deleted by ClearPilot.
- `BLOCKED`: explicitly forbidden, never deleted.

Safety gates are enforced in the cleanup engine; user-facing recommendations never override these gates.

## Direct Cleanup Decision Labels (v0.3.0)

Primary user-facing decisions:

- `Recommended to clean`
- `Not recommended to clean`
- `Analysis only, do not clean`
- `Blocked`

Simplified Chinese:

- `建议清理`
- `不建议清理`
- `仅分析，不清理`
- `已阻止`

## Core Safety Enforcement

- Canonical path validation with `PathSafetyEngine`.
- Known-safe root constraints with `KnownSafeCacheRootWhitelist`.
- Protected root and hard-deny protection via `ProtectedPathPolicy`.
- Deletion-time revalidation before each delete action.
- Reparse point/symlink/junction blocking on deletion targets.
- Process guards for launcher-specific cleanup targets.

## Coverage in v0.3.0 Development

### Windows cache coverage

- User temp (S0/S1 policy-constrained by mode and age rules)
- Windows temp (S1, no elevation)
- Windows Error Reporting user scope (S1)
- User crash dumps (S1, with diagnostic caution)
- INetCache cache-only paths (S1, identity/session exclusions)
- Microsoft Store `LocalCache` safe paths (S1)
- Windows Update / Delivery Optimization / CBS / DISM / memory dump / `Windows.old` as `S2` analysis-only

### Game launcher cache/log coverage

Launcher-scoped cache/log coverage for:

- Steam
- Epic Games Launcher
- Battle.net
- Riot Client
- EA App
- Ubisoft Connect

These targets are conservative, process-guarded, and still pass the same path-safety checks.

## Explicit Non-Goals / Blocked Areas

ClearPilot does **not** perform:

- Registry cleaning
- Driver cleaning
- Browser identity/profile cleanup (cookies/passwords/bookmarks/history/sessions/local storage/indexeddb/session storage)
- Game installs/saves/configs/mods/screenshots/recordings/manifests/library metadata cleanup
- Microsoft Defender quarantine/protection/signature/engine/state cleanup
- Service stopping, ACL changes, forced unlocks, or privilege escalation
- Whole-root deletion of `Windows`, `Program Files`, `Program Files (x86)`, `ProgramData`, or full user profile roots

## Project Layout

```text
src\ClearPilot.Cli         Console UI
src\ClearPilot.Core        Cleanup engine, safety, scanning, logging, localization
tests\ClearPilot.Core.Tests
docs                       Product requirements and MVP plan
release\ClearPilot-v0.2.0  Current published release package
release\ClearPilot-v0.1.0  Previous release package
```

## Run (Development)

```text
ClearPilot.cmd
```

## Build And Test

```powershell
.\.dotnet\dotnet.exe build .\ClearPilot.sln --no-restore
.\.dotnet\dotnet.exe test .\ClearPilot.sln --no-build
```

## Logs And Reports

- Cleanup logs: `%LOCALAPPDATA%\ClearPilot\logs`
- Deep Space reports: `%LOCALAPPDATA%\ClearPilot\reports`

## Release Status

v0.3.0 is under pre-release hardening. Final packaging, version finalization, tag creation, and GitHub release publishing are handled in the final release chapter.
