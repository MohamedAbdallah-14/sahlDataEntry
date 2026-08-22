# Architecture

English internal documentation. The user-facing UI is Arabic/RTL.

## Overview

SahelBundleKeyboard is a portable WPF utility that "types" saved product bundles into
whatever window currently owns keyboard focus (normally Sahel's product-search field).
It deliberately treats Sahel as a black box: no process detection, no database, no APIs —
only standard `SendInput` keyboard events.

```text
┌─────────────────────────── src/SahelBundleKeyboard.App (WPF, net10.0-windows)
│  MainWindow (3 tabs) · FloatingControllerWindow (non-activating) · ImportPreview
│  ViewModels: Main / Bundles / Settings   ← manual composition root in App.xaml.cs
│        │ uses ▼
├─────────────────────────── src/SahelBundleKeyboard.Windows (net10.0-windows)
│  SendInputKeystrokeSender · GlobalHotkeyManager · MessageOnlyWindow
│  FloatingWindowBehavior (WS_EX_NOACTIVATE + MA_NOACTIVATE)
│        │ implements Core abstractions ▼
├─────────────────────────── src/SahelBundleKeyboard.Core (net10.0, no OS deps)
│  Models · BundleValidator/SettingsValidator · SequenceBuilder · QuantityFormatter
│  AutomationEngine (state machine) · AsyncGate · HotkeyParser · ILog/IKeystrokeSender/IDelayService
│        ▲ used by
├─────────────────────────── src/SahelBundleKeyboard.Infrastructure (net10.0)
│  JsonDataStore (atomic) · BackupService · CsvParser/XlsxReader/Writers · RowMapper
│  RollingFileLogger
└── tests/: 84 core tests + 28 infrastructure tests (xUnit), all engine timing via fakes
```

Dependency rule: **Core knows nobody; Infrastructure and Windows depend on Core; App depends on all.**
All Win32 usage is wrapped behind small interfaces so the automation engine is fully testable
without a desktop session.

## Exact action sequence

For every bundle item, in saved order (`SequenceBuilder.Build`):

| # | Action | Notes |
|---|--------|-------|
| 1 | Type product **code** if present, else **name** | Unicode key events; whitespace-only code falls back to name |
| 2 | Enter | Sahel moves to quantity field |
| 3 | Wait global delay | one configurable value, default 120 ms |
| 4 | Type final quantity = `base × bundleCount` | invariant culture, dot decimal, trailing zeros trimmed (`2`, `1.25`) — no clearing keystrokes |
| 5 | Enter | Sahel moves to price field |
| 6 | Wait global delay | |
| 7 | If custom price exists: type it | otherwise type nothing (Sahel keeps its default price) |
| 8 | Enter | next product-search field |
| 9 | Wait global delay | |

The sequence is a deterministic list of `InputAction` records (`TypeTextAction`,
`PressEnterAction`, `WaitAction`); the engine merely walks it.

## State machine

```text
            TryStart (validates; rejected when busy)
 Idle ───────────────► Countdown ─────► Running ──(all actions done)──► Completed
                          │  Stop          │  Pause                ▲
                          ▼                ▼                       │ Resume
                        Stopped ◄──────── Paused ─────────────────┘
                          ▲                │ Stop
                          └────────────────┘
 Running/Paused/Completed/Stopped/Idle --(executor throws)--> Error
```

- Only one run at a time: `TryStartAsync` guards with a semaphore plus run-task completion check.
- Pause closes an `AsyncGate`; the gate is checked **between individual input actions**, so the exact
  next pending action is preserved.
- Stop opens the paused gate *and* cancels the `CancellationTokenSource`; waits are
  `IDelayService.DelayAsync(ms, ct)` so cancellation interrupts mid-delay.
- Countdown = N × 1-second cancellable delays with state messages (`سيبدأ الإدخال خلال 3 ثانية…`).
- All execution runs on the thread pool (`Task.Run`), never blocking the UI thread; events are
  marshaled to the dispatcher by the composition root.

## Focus strategy

Two start paths:

1. **Global hotkeys** registered via `RegisterHotKey` on a message-only window
   (`HWND_MESSAGE`), with `MOD_NOREPEAT`. Registration failure rolls back to the previous
   valid set and reports an Arabic error; F12 is rejected at parse level.
2. **Floating controller**: borderless topmost tool-window styled with
   `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST`, created with `ShowActivated=false`,
   handling `WM_MOUSEACTIVATE → MA_NOACTIVATE`. Clicking Go/Pause/Stop therefore never moves
   keyboard focus away from Sahel. Buttons set `Focusable=False`.

Foreground application is intentionally unrestricted (works in Notepad for first testing).

## Data format

`Data/data.json`, UTF-8 (no BOM), indented, Arabic preserved via relaxed JSON escaping:

```json
{
  "schemaVersion": 1,
  "settings": {
    "delayMilliseconds": 120,
    "countdownSeconds": 2,
    "startShortcut": "Ctrl+Alt+G",
    "pauseResumeShortcut": "Ctrl+Alt+P",
    "stopShortcut": "Ctrl+Alt+S",
    "lastSelectedBundleId": "...",
    "lastBundleCount": 1
  },
  "bundles": [
    {
      "id": "guid",
      "name": "عرض رمضان",
      "items": [
        { "id": "guid", "productCode": "6290100000001", "productName": "زيت عافية 1 لتر",
          "baseQuantity": 1.5, "customPrice": 42.50, "order": 0 }
      ]
    }
  ]
}
```

- Saves are atomic: temp file → flush-to-disk → `File.Replace`.
- Loading is case-insensitive; unknown newer schema versions or corrupt JSON quarantine the file
  as `data.json.corrupt-yyyyMMdd-HHmmss.json` and continue empty (original bytes preserved).
- Full backup export/import reuses this envelope; import validates everything before replacing,
  and writes `Data/backups/pre-import-*.json` safety copy first.
- Run history is never persisted. Logs contain counts/errors only, never typed text.

## Import/export

- CSV: RFC-4180 parser (quotes, escaped quotes, embedded newlines). UTF-8 with BOM expected;
  legacy encodings are rejected with an Arabic message rather than mojibake.
- XLSX: hand-rolled reader/writer over `ZipArchive` + `XmlReader` (shared strings, inline strings,
  numeric cells). Zero third-party packages. Header row must match known Arabic/English column names;
  otherwise the generated template must be used.
- Rows validate individually (name required, quantity > 0 dot-decimal, optional price ≥ 0);
  invalid rows are shown red in the preview and skipped on commit; append vs replace is explicit.

## Windows integrity limitation

`SendInput` cannot inject into processes running at a higher integrity level. If Sahel runs
elevated, this app must be started elevated too (documented; `asInvoker` remains the default).

## Testing strategy

CI (GitHub Actions, windows-latest) restores in locked mode, builds Release with warnings-as-errors,
runs all xUnit tests, publishes single-file win-x64, zips the portable folder with SHA256.
Interactive focus/injection behavior is covered by `docs/manual-windows-test.md` only — CI never
depends on real keystrokes reaching a desktop.
