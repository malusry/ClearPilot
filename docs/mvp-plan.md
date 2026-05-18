# ClearPilot MVP Development Plan

## 1. MVP Objective

Build a Windows command-line cleanup assistant that can safely reclaim disk space from the current user's environment, explain recommended cleanup actions, and avoid system-damaging or user-data-damaging behavior.

The MVP should prove the cleanup engine, risk model, logging model, and menu interaction before any desktop UI is considered.

## 2. Recommended Technical Stack

- Language: C#
- Runtime: .NET LTS
- Application type: Console application
- Initial architecture: reusable core library plus console UI

Recommended project layout:

```text
ClearPilot/
  src/
    ClearPilot.Cli/
    ClearPilot.Core/
  tests/
    ClearPilot.Core.Tests/
  docs/
    requirements.md
    mvp-plan.md
```

`ClearPilot.Core` should contain scanning, classification, cleanup, logging, settings, and localization logic. `ClearPilot.Cli` should only handle user interaction.

## 3. Architecture Components

### 3.1 Console UI

Responsibilities:

- Display menus.
- Read numeric choices.
- Show progress and summaries.
- Ask for confirmation for S1 cleanup.
- Let the user change settings.

### 3.2 Cleanup Engine

Responsibilities:

- Execute cleanup plans.
- Delete S0 files directly.
- Clean S1 files only after confirmation.
- Skip locked, inaccessible, or uncertain files.
- Return structured results instead of printing directly.

### 3.3 Scanner

Responsibilities:

- Scan known cleanup locations.
- Estimate file counts and total sizes.
- Detect risk levels.
- Produce cleanup candidates with explanations.

### 3.4 Rule Catalog

Responsibilities:

- Define cleanup rules.
- Store rule ID, category, paths, include patterns, exclude patterns, age thresholds, risk level, and explanation.
- Keep rules deterministic and auditable.

### 3.5 Settings

Responsibilities:

- Store selected language.
- Store log retention days.
- Store advanced Recycle Bin behavior.
- Store dry-run preference if implemented.

### 3.6 Logging

Responsibilities:

- Write cleanup run logs.
- Keep logs in the user's local application data directory.
- Retain logs for 7 days by default.
- Record skipped files and errors without exposing unnecessary noise in the main UI.

### 3.7 Localization

Responsibilities:

- Provide English default strings.
- Provide Simplified Chinese strings.
- Keep UI text outside business logic.

## 4. Development Phases

### Phase 0 - Project Setup

Deliverables:

- Create .NET solution.
- Create CLI project.
- Create Core project.
- Create Core test project.
- Add basic build and test commands.

Acceptance criteria:

- Solution builds.
- Empty CLI runs.
- Test project executes.

### Phase 1 - Menu And Settings

Deliverables:

- Main numeric menu.
- Settings menu.
- Language selection.
- Log retention setting.
- Auto-empty Recycle Bin setting with warning text.

Acceptance criteria:

- User can navigate menu with `1`, `2`, `3`, `4`, `5`, and `0`.
- English is default.
- Chinese can be selected.
- Settings persist between runs.

### Phase 2 - Scan Model And Rule Catalog

Deliverables:

- Cleanup candidate data model.
- Risk level enum.
- Rule definition model.
- Initial S0 and S1 rule catalog.

Acceptance criteria:

- Scanner can produce structured candidates.
- Each candidate has category, path, size, risk level, reason, and rule ID.
- Rules can be tested independently.

### Phase 3 - Quick Safe Clean

Deliverables:

- Implement S0 scanning.
- Implement direct deletion for S0 candidates.
- Skip locked or inaccessible files.
- Show summary after cleanup.
- Write cleanup log.

Initial S0 rules:

- Current user's temp directory.
- Old crash dump files in user-accessible locations.
- Old disposable cache files in known safe user locations.

Acceptance criteria:

- Quick Safe Clean runs directly after selection.
- It does not ask for confirmation.
- It skips unsafe or uncertain files.
- It logs deleted, skipped, and failed items.

### Phase 4 - Recommended Cleanup

Deliverables:

- Implement S1 scan.
- Show grouped results.
- Ask for confirmation before cleanup.
- Allow cleaning selected categories or all recommended categories.

Initial S1 rules:

- Browser cache files excluding profile, cookie, password, history, bookmark, and session data.
- Package manager caches owned by the current user.
- User-owned installer leftovers where rules are clear.

Acceptance criteria:

- User sees estimated reclaimable size before confirming.
- No S1 item is deleted without confirmation.
- Side effects are explained clearly.

### Phase 5 - Deep Space Analysis

Deliverables:

- Large file scan for selected user-controlled folders.
- Large folder summary.
- Old archive and installer report.

Acceptance criteria:

- Analysis mode deletes nothing.
- Results are sorted by size.
- Results clearly state that manual review is required.

### Phase 6 - History And Log Retention

Deliverables:

- History menu.
- Recent cleanup summaries.
- Automatic removal of logs older than 7 days.

Acceptance criteria:

- User can view recent cleanup runs.
- Old logs are removed according to configured retention.

### Phase 7 - Hardening And Packaging

Deliverables:

- Unit tests for rule classification.
- Unit tests for protected path handling.
- Dry-run test mode.
- Release build.
- Basic usage documentation.

Acceptance criteria:

- Tests pass.
- Protected paths are never selected for cleanup.
- Application can be run as a standalone command-line tool.

## 5. Initial Rule Safety Policy

The MVP must treat cleanup as opt-in by rule, not opt-out by path.

This means:

- Only paths explicitly covered by known rules can produce cleanup candidates.
- Unknown locations are ignored.
- Blocked paths are checked globally.
- User-data folders are never automatically cleaned.

## 6. Testing Strategy

### Unit Tests

Required coverage:

- Risk level assignment.
- Protected path detection.
- Rule matching.
- Age threshold handling.
- Settings persistence.
- Log retention cleanup.

### Integration Tests

Recommended coverage:

- Temporary test folder cleanup.
- Locked file skip behavior.
- Dry-run behavior.
- CLI menu flow where practical.

### Manual Tests

Required before using on the real `C:\` drive:

- Run against a synthetic test directory.
- Verify deletion summary.
- Verify logs.
- Verify protected paths are skipped.
- Verify Chinese UI mode displays correctly.

## 7. Future Roadmap

Possible post-MVP features:

- Administrator mode for selected system cleanup tasks.
- ClearPilot-managed quarantine folder.
- Scheduled cleanup reminders.
- Desktop UI.
- Exportable scan reports.
- More browser support.
- More development cache rules.
- Duplicate file analysis.
- Plugin-like rule packs.

Features that should remain out of scope unless explicitly redesigned:

- Registry cleaning.
- Driver deletion.
- Browser identity/session cleanup.
- Automatic deletion of user documents.
- Fully automatic deep cleaning.

## 8. First Implementation Milestone

The first coding milestone should produce:

- A runnable console app.
- Main menu.
- Settings persistence.
- English and Chinese UI switching.
- A mock scanner that returns sample cleanup candidates.

This milestone proves the interaction model before real deletion code is added.

After that, real S0 scanning and deletion can be implemented behind the same interfaces.
