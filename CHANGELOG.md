# Changelog

All notable changes to this project are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning: SemVer.

## [1.0.0] - 2026-08-22

First functional release.

### Added

- Arabic RTL WPF application with three tabs: التشغيل (run), الحزم (bundles), الإعدادات (settings).
- Bundle management: create, rename, duplicate (deep clone with new ids), delete with confirmation;
  item rows with code/name/quantity/custom-price, add/delete/reorder; per-cell Arabic validation.
- Deterministic Sahel input sequence: product-code priority with name fallback, Enter after search,
  final quantity = base quantity × whole bundle count, Enter, optional custom price only, Enter,
  global delay between logical steps. Invariant dot-decimal formatting (`2`, `1.25`, `4.5`),
  never scientific notation or trailing zeros.
- Automation engine state machine: Idle → Countdown → Running → Paused → Completed/Stopped/Error,
  single-run enforcement, pause/resume preserving the exact next pending action, immediate stop
  (including during countdown), cancellation-aware async execution off the UI thread.
- Global shortcuts via RegisterHotKey + MOD_NOREPEAT on a message-only window
  (defaults Ctrl+Alt+G / Ctrl+Alt+P / Ctrl+Alt+S), keystroke-capture editors,
  F12 rejection, conflict detection with rollback to last valid configuration.
- Non-activating floating controller (WS_EX_NOACTIVATE, WM_MOUSEACTIVATE=MA_NOACTIVATE, topmost,
  draggable) showing bundle, count, state, progress and countdown; clicking never steals focus.
- Persistence: UTF-8 JSON in `Data/data.json` beside the EXE, atomic temp-file+replace saves,
  corrupt/newer-version files quarantined under timestamped names without overwriting originals,
  last-selected bundle and count persisted, no run history stored anywhere.
- Full backup export/import (single versioned JSON) with complete validation, preview summary,
  confirmation, and automatic pre-import safety copies under `Data/backups`.
- Import from .xlsx and .csv with row-level Arabic validation preview, explicit append-or-replace,
  recognized Arabic+English headers, Arabic-indic digit normalization, comma-decimal rejection;
  blank CSV and Excel template generation (hand-rolled XLSX writer — zero third-party packages).
- Bounded rolling log file under `Data/logs` (daily files, size rotation, retention) storing only
  technical details, never typed products.
- Tests: 84 Core + 28 Infrastructure xUnit tests covering the sequence builder, formatter, hotkey
  parser/validation conflicts, engine pause/resume/stop/countdown/overlap via fakes, JSON round-trip,
  quarantine behavior, backup flows, CSV/XLSX parsing and template round-trips.
- CI on GitHub-hosted Windows runners: locked restore, Release build with warnings-as-errors,
  full test run, self-contained single-file win-x64 publish (no trimming), portable folder with
  Arabic quick-start README and Data placeholder, ZIP + SHA256 artifacts.
- Release workflow for `v*` tags and manual dispatch with idempotent reruns via GitHub CLI.

### Changed

- Pause/Resume removed from the UI and global shortcuts per operator feedback:
  Start (Ctrl+Alt+G) and Stop (Ctrl+Alt+S) only. The engine keeps its tested
  pause capability internally; old data files load unchanged (unknown field ignored).
- Floating controller: live bundle dropdown, bundle-count +/- buttons, hide button
  (DataContext binding fix — dropdown was previously empty).

### Fixed

- Startup crash #2: `ProgressBar.Value` (TwoWay by default) bound to read-only
  `ProgressFraction`; binding is now explicit `Mode=OneWay`.
- First-run crash on real Windows: `CreateWindowExW`/`UnregisterClassW` marshaled the
  window-class name as ANSI into wide-char APIs (`ERROR_CANNOT_FIND_WND_CLASS`).
  Both declarations now use `CharSet.Unicode`; Arabic errors name the failing API
  and Win32 error code; a hotkey-subsystem failure no longer aborts startup.

### Notes / limitations

- Windows integrity restriction: if Sahel runs elevated, this utility must also run as administrator
  (`asInvoker` remains the default). See README troubleshooting.
- Final interactive verification against Sahel must be done manually per `docs/manual-windows-test.md`.

[1.0.0]: ./releases/tag/v1.0.0
