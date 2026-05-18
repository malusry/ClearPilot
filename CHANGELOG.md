# Changelog

## v0.2.0 - 2026-05-17

### Added

- Expanded Deep Space Analysis with broader user-controlled scan roots, including Downloads, Desktop, Documents, Pictures, Videos, Music, source, repos, Projects, dev, workspace, workspaces, and code when present.
- Added safe user-scoped cache roots to Deep Space Analysis, including the user temp folder, Windows Error Reporting, DirectX/GPU shader caches, and common developer caches for NuGet, Gradle, npm, pnpm, Yarn, pip, Deno, Go, Cargo, and Maven.
- Added review-only findings for nested large folders, Python virtual environment folders (`.venv`, `venv`), frontend framework outputs, local build caches, test caches, coverage output, vendor folders, and Terraform working directories alongside existing dependency and build-output folders.
- Added file type space summaries so large extension groups such as archives, disk images, videos, logs, or temporary files can be reviewed by scan root.
- Added suggested manual actions to every Deep Space Analysis finding.
- Added a Deep Space Analysis scan summary with scanned roots, scanned directories, scanned files, review item count, and review footprint.
- Grouped Deep Space Analysis results by finding type with per-type totals and top space sources.
- Added interactive Deep Space Analysis filtering by finding type and sorting by size or last modified time.
- Improved Deep Space Analysis explanations and suggested actions for videos, logs, temporary files, backups, archives, disk images, installers, and project dependency folders.
- Localized Deep Space Analysis explanations and suggested actions for the optional Simplified Chinese UI.
- Improved CLI result cards so long explanations and suggested actions wrap across multiple lines instead of being truncated.
- Added Markdown export for Deep Space Analysis reports under the ClearPilot reports folder, with summary tables, type breakdown bars, top sources, grouped findings, localized explanations, and suggested actions.
- After exporting a Deep Space Analysis report, ClearPilot opens File Explorer and selects the generated report file when possible.
- Expanded recommended cleanup scanning with additional conservative S1 targets: Windows Error Reporting files, DirectX/GPU shader caches, Maven/Deno/Bun caches, Python bytecode caches, Electron app UI caches, and additional browser cache folders for Brave, Chromium, Vivaldi, Opera, and Firefox.
- Expanded Deep Space Analysis default project roots with common user-controlled development folders such as `dev`, `workspace`, `workspaces`, and `code`.
- Localized recommended cleanup category names and explanations for the optional Simplified Chinese UI.
- Reduced duplicate Deep Space Analysis large-folder noise by suppressing a parent large-folder finding when a dominant child finding already explains most of the space.
- Improved CLI page transitions so each main action and subpage clears the previous menu before rendering the current view.
- Refined the CLI color system with a unified theme and clearer function-color navigation in the main menu.
- Optimized the development launcher so `ClearPilot.cmd` only rebuilds when the Debug executable is missing or source files are newer than the executable.
- Added compact cleanup previews for Quick Safe Clean and Recommended Cleanup, showing cleanup groups, estimated file count, estimated space, top items, and the relevant safety boundary without adding another workflow.
- Improved cleanup completion pages by separating running and completed views, keeping completion statistics aligned.
- Excluded ClearPilot internal folders, cleanup log file names, reports, and test artifacts from default temporary-file cleanup and Deep Space Analysis results.

### Safety

- Deep Space Analysis remains analysis-only and does not delete files.
- Deep Space Analysis findings are still reported as review-required items and can only open the relevant file or folder location.
- Deep Space Analysis report export writes a Markdown report only; it does not modify analyzed files.
- New recommended cleanup targets remain S1 and require explicit user selection before cleaning.
- Quick Safe Clean preview remains informational only; automatic cleanup is still limited to S0 very-low-risk rules.
- ClearPilot's own settings, logs, reports, and development test artifacts are not treated as cleanup recommendations or Deep Space findings.
- Browser expansion is limited to cache folders and excludes cookies, passwords, bookmarks, history, sessions, profiles, local storage, and identity data.
- Electron app expansion is limited to Cache, Code Cache, and GPUCache folders.
- v0.1.0 release artifacts under `release/ClearPilot-v0.1.0` were not modified.

## v0.1.0 - MVP Release Candidate

### Added

- Windows command-line menu experience with English as the default UI language.
- Optional Simplified Chinese UI language in Settings.
- Quick Safe Clean for S0 very-low-risk cleanup items.
- Recommended Cleanup for S1 low-risk cache items with explicit confirmation.
- Deep Space Analysis for review-only large file and folder findings.
- Cleanup History with log retention.
- Settings for UI language, log retention days, and Recycle Bin behavior.
- Self-contained Windows x64 release package under `release/ClearPilot-v0.1.0`.
- ClearPilot app icon embedded into the release executable.

### Safety

- Quick Safe Clean only runs S0 rules.
- S1 recommended cleanup requires explicit user selection.
- Deep Space Analysis never deletes files.
- Protected system roots are blocked globally.
- Browser identity, cookies, passwords, bookmarks, history, sessions, and profiles are excluded from browser cache rules.
- Registry cleaning, driver cleaning, browser identity/session cleanup, risky user-data deletion, and administrator-only cleanup are out of scope.

### Known Limitations

- The first release is a command-line menu tool, not a desktop GUI.
- Release packaging is a folder-based package, not an installer.
- Deep Space Analysis opens item locations for manual review but does not perform manual deletion.
- The embedded icon is generated from the selected PNG concept; earlier vector icon drafts remain in `assets/icon/drafts`.
