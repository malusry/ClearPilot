# ClearPilot Requirements Specification

## 1. Product Overview

ClearPilot is a Windows disk cleanup assistant focused on safely reclaiming space from the system drive, especially `C:\`. The first version is a command-line menu application designed for personal use, with an architecture that can later support broader distribution and a graphical interface.

The product must never perform cleanup actions that can damage Windows, installed applications, user accounts, or normal day-to-day usage. It should still be useful enough to reclaim meaningful disk space by automatically removing very low-risk temporary data and by recommending additional cleanup targets with clear explanations.

## 2. Product Goals

- Provide a simple numbered command-line menu.
- Scan common low-risk locations on Windows.
- Automatically clean very low-risk items without requiring preview confirmation.
- Present recommended cleanup items with risk levels, size estimates, and plain-language explanations.
- Require confirmation before cleaning items that may have minor side effects.
- Keep all development-facing content, source identifiers, logs, and default UI text in English.
- Support Chinese as an optional UI language.
- Avoid destructive system changes, registry cleanup, driver cleanup, and user-data deletion.

## 3. Target User

The initial target user is the developer-owner of the tool:

- Uses Windows.
- Wants to reclaim space from `C:\`.
- Is comfortable using a command-line menu with numeric choices.
- Does not need a polished desktop UI in the first release.
- Prefers automation for very low-risk cleanup.
- Wants explanations and manual confirmation for anything beyond very low risk.

Future versions may support less technical users, but the MVP should prioritize correctness, transparency, and safe behavior over visual polish.

## 4. Platform

- Operating system: Windows 10/11.
- Runtime: .NET, recommended current LTS version at implementation time.
- First interface: interactive command-line menu.
- Future interface option: desktop UI using a reusable cleanup engine.

## 5. Language And Localization

English is the native language of the project.

- Source code identifiers: English.
- Documentation: English.
- Logs: English by default.
- Default UI language: English.
- Optional UI language: Simplified Chinese.

Localization should be implemented through a resource or message catalog layer instead of hard-coded UI strings. The MVP can include only English and Simplified Chinese text, but the structure should allow more languages later.

## 6. Main Menu

The MVP command-line menu should follow this structure:

```text
ClearPilot

1. Quick Safe Clean
2. Scan Recommended Items
3. Deep Space Analysis
4. Cleanup History
5. Settings
0. Exit
```

Chinese UI mode should provide equivalent labels:

```text
ClearPilot

1. 快速安全清理
2. 扫描推荐清理项
3. 深度空间分析
4. 清理历史
5. 设置
0. 退出
```

## 7. Cleanup Modes

### 7.1 Quick Safe Clean

Quick Safe Clean automatically removes very low-risk data without preview confirmation.

Allowed cleanup targets include:

- Current user's temporary files.
- Old temporary files in known safe temp directories.
- Application crash dump files.
- Safely removable thumbnail caches where applicable.
- Clearly disposable cache files owned by the current user.

Behavior:

- Executes directly after the menu option is selected.
- Shows a final summary after completion.
- Logs all actions.
- Skips locked files, inaccessible files, and uncertain files.
- Must not require administrator privileges in the MVP.

### 7.2 Scan Recommended Items

This mode scans for low-risk cleanup opportunities and presents them to the user before deletion.

Examples:

- Browser caches, excluding cookies, passwords, bookmarks, history, sessions, and profiles.
- Common package manager caches.
- Installer caches owned by the current user.
- Large temporary download leftovers.

Behavior:

- Shows category, estimated size, file count, risk level, and side-effect explanation.
- Requires user confirmation before cleanup.
- Allows selecting all recommended items or choosing categories individually.
- Logs accepted and skipped items.

### 7.3 Deep Space Analysis

This mode analyzes disk usage and identifies candidates that may be worth reviewing.

Examples:

- Large files.
- Large folders.
- Old archives or installers in user-controlled folders.
- Duplicate-file candidates in a future version.
- Project dependency folders such as `node_modules`, `bin`, `obj`, `.gradle`, or `target`.

Behavior:

- Must not delete anything automatically.
- Must present findings as analysis only.
- May allow manual cleanup in a later version after stronger safeguards are added.

### 7.4 Cleanup History

Cleanup History shows recent cleanup logs.

Requirements:

- Keep logs for 7 days by default.
- Automatically remove logs older than the retention period.
- Show timestamp, mode, cleaned size, item count, skipped count, and errors.
- Logs should be stored under the user's local application data directory.

### 7.5 Settings

Settings should include:

- UI language: English or Simplified Chinese.
- Log retention days: default 7.
- Auto-empty Recycle Bin after cleanup: disabled by default.
- Dry-run mode for testing: optional but recommended.

The Recycle Bin setting must display a clear warning:

```text
Warning: Emptying the Recycle Bin may permanently delete files that were already there before ClearPilot ran. This action affects the entire Recycle Bin, not only files cleaned by ClearPilot.
```

## 8. Risk Levels

ClearPilot should classify cleanup targets with explicit risk levels.

### S0 - Very Low Risk

Can be cleaned automatically in Quick Safe Clean.

Expected impact:

- No meaningful effect on normal usage.
- Files are temporary, disposable, or safely regenerated.

Examples:

- Current user temp files.
- Old crash dumps.
- Disposable cache files.

### S1 - Low Risk

Can be cleaned after user confirmation.

Expected impact:

- Minor inconvenience only.
- Applications may recreate caches or redownload temporary data.

Examples:

- Browser caches excluding user identity/session data.
- Development package caches.
- User-owned installer leftovers.

### S2 - Review Required

Should only be reported or manually reviewed.

Expected impact:

- May remove useful user files or project state.
- Needs context from the user.

Examples:

- Large archives.
- Large installers.
- Old downloads.
- Project dependency folders.

### S3 - Do Not Clean Automatically

Must not be automatically cleaned.

Examples:

- Documents.
- Pictures.
- Videos.
- Desktop files.
- Source code.
- Application profiles.
- Configuration databases.

### Blocked

Must never be cleaned by MVP rules.

Examples:

- `C:\Windows\System32`
- `C:\Program Files`
- `C:\Program Files (x86)`
- Registry entries.
- Drivers.
- Services.
- Browser passwords, cookies, bookmarks, history, sessions, and profiles.

## 9. Administrator Privileges

The MVP should run without requiring administrator privileges.

Behavior:

- Detect whether the process is elevated.
- Continue normally when not elevated.
- Skip privileged locations.
- Explain that some system-level cleanup targets are unavailable in non-admin mode.

Administrator mode may be added later as an advanced feature. It must use stricter prompts and must not expand automatic cleanup into high-risk areas.

## 10. Deletion Policy

For the MVP:

- S0 items may be permanently deleted directly.
- S1 items require confirmation before deletion.
- S2 items are analysis-only unless a future version adds stronger manual controls.
- S3 and Blocked items must not be deleted.

Recycle Bin support:

- The application may support moving selected items to the Recycle Bin for confirmed cleanup actions.
- Auto-emptying the Recycle Bin is an advanced setting and must be disabled by default.
- If enabled, the UI must clearly warn that Windows may empty the entire Recycle Bin.

Future extension:

- A ClearPilot-managed quarantine folder may be added later.
- The quarantine feature may support automatic cleanup after a retention period.

## 11. Safety Requirements

ClearPilot must:

- Default to skipping uncertain files.
- Skip locked files without failing the whole cleanup run.
- Avoid following risky reparse points or symbolic links unless explicitly supported.
- Avoid cleaning files recently modified unless the rule specifically allows it.
- Never clean user documents or identity/session data.
- Keep a log of every cleanup run.
- Provide enough explanation for the user to understand recommended cleanup actions.
- Prefer deterministic rules over vague file-name matching.

## 12. Non-Goals For MVP

The MVP will not include:

- Registry cleaning.
- Driver cleaning.
- Startup item removal.
- Service removal.
- Duplicate file deletion.
- Browser password, cookie, bookmark, history, or session cleanup.
- Full desktop GUI.
- Fully automatic deep cleanup.
- System-wide cleanup requiring administrator privileges.

## 13. Success Criteria

The MVP is successful when:

- The user can run ClearPilot from a terminal.
- The main menu works with numeric choices.
- Quick Safe Clean reclaims space from S0 locations without confirmation.
- Recommended cleanup scans show understandable S1 findings and require confirmation.
- Deep Space Analysis reports useful large-space findings without deletion.
- Logs are generated and retained for 7 days.
- English UI works by default.
- Chinese UI can be selected from settings.
- The tool never deletes blocked or user-critical data during normal operation.
