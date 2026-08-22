# Sahel Bundle Keyboard — code review and fix handoff

Review date: 2026-08-22  
Review baseline: `6ba387c25ae8` (`main`)  
Reviewer scope: source code, WPF interaction paths, persistence, backup/import, Win32 integration, tests, GitHub Actions, release workflow, and the original implementation brief.

The repository was still changing while this review was prepared. Revalidate each path and line number against the latest `main` before editing. Do not mark a finding resolved because an adjacent hotfix landed; reproduce the exact behavior and add a regression test.

## Executive assessment

This is a strong alpha with broad specification coverage, a well-separated automation core, useful documentation, and a real Windows build/release pipeline. It is not production-ready because several cross-layer WPF and persistence paths can lose edits, leave the UI disconnected from imported data, or report success after a failed operation.

The current automated suite covers Core and Infrastructure well, but it does not exercise the application/view-model integration paths where the highest-priority defects are located.

## Severity definitions

- **P1 — production blocker:** data loss, a required cashier workflow is unavailable, or the UI reports a false successful result.
- **P2 — important:** recovery, status, release integrity, or specification behavior is unreliable but has a workaround.
- **P3 — maintenance:** does not block the cashier workflow today but should be corrected before the codebase grows.

## P1 findings

### P1-1: Grid and bundle edits are not reliably committed or autosaved

Locations:

- `src/SahelBundleKeyboard.App/MainWindow.xaml:169,197-206`
- `src/SahelBundleKeyboard.App/ViewModels/EditableBundle.cs:25-34`
- `src/SahelBundleKeyboard.App/ViewModels/EditableBundleItem.cs:27-66,124-167`
- `src/SahelBundleKeyboard.App/ViewModels/MainViewModel.cs:291-346`
- `src/SahelBundleKeyboard.App/App.xaml.cs:173-180`

Root cause:

- Bundle name, product code, and product name setters mutate the in-memory model but do not request a save.
- Quantity and custom-price fields are stored only as wrapper strings. The underlying `BundleItem` is updated by `TryCommit`, which is called when starting automation and during duplication, not as part of normal editing.
- Application exit does not commit pending valid edits or save the document.

Reproduction:

1. Open or create a bundle.
2. Change its name, product name/code, quantity, and custom price.
3. Do not press Start and do not perform another operation that happens to call `_data.Save()`.
4. Close and reopen the application.
5. Some text edits may be missing and quantity/price changes revert to the previous model values.

Required fix:

- Define one explicit edit-commit path for a bundle row.
- On a valid row/cell commit, update the underlying model and call the atomic persistence service.
- Invalid text must remain visible for correction but must never silently replace the last valid stored decimal.
- Commit all valid pending edits before bundle selection changes and before normal application exit.
- Avoid saving on every keystroke if that harms responsiveness; row commit, focus loss with validation, or a short debounced save is acceptable.
- Surface save failures without claiming the edit was persisted.

Required regression tests:

- Editing name/code/product name persists without starting a run.
- Editing quantity/custom price commits the exact decimal and persists it.
- Invalid quantity/price does not overwrite the last valid model value.
- Closing after valid edits saves them; closing with invalid edits follows an explicit, tested policy.

### P1-2: Item and bundle commands do not re-evaluate when selection changes

Locations:

- `src/SahelBundleKeyboard.App/ViewModels/BundlesViewModel.cs:23-29,46`
- `src/SahelBundleKeyboard.App/Infrastructure/RelayCommand.cs:21-28`

Root cause:

`SelectedItem` is a plain auto-property. Changing it does not raise `CanExecuteChanged` for Delete, Move Up, or Move Down. Commands that depend on `MainViewModel.SelectedBundle` also are not refreshed when the first bundle is created from an initially empty installation.

Reproduction:

1. Start with no saved bundles.
2. Create the first bundle.
3. Check Add Item, Duplicate Bundle, and Delete Bundle.
4. Select an item row and check Delete Item, Move Up, and Move Down.
5. Commands that were initially disabled can remain disabled despite a valid selection.

Required fix:

- Make `SelectedItem` observable.
- Raise `CanExecuteChanged` for Delete/Move commands whenever selection or collection order changes.
- Notify Add/Duplicate/Delete bundle commands whenever `SelectedBundle` changes.
- Re-evaluate Move Up/Down after moving or deleting a row.

Required regression tests:

- Creating the first bundle enables all applicable bundle/item commands.
- Selecting a row enables Delete and the correct move direction.
- Selecting the first/last row disables only the impossible move direction.
- Deleting or moving a row refreshes command state immediately.

### P1-3: Backup import replaces the document but leaves the UI bound to the old graph

Locations:

- `src/SahelBundleKeyboard.App/ViewModels/SettingsViewModel.cs:218-230`
- `src/SahelBundleKeyboard.App/Services/AppDataService.cs:45-50`
- `src/SahelBundleKeyboard.App/ViewModels/MainViewModel.cs:51-60,399-435`

Root cause:

`ReplaceDocument` swaps `AppDataService.Document`, but `MainViewModel.Bundles`, `SelectedBundle`, and the editable item wrappers continue referencing the previous document's objects. Only settings fields are refreshed after import.

Reproduction:

1. Start with bundle A in the UI.
2. Import a validated backup containing a different bundle B.
3. Confirm the import.
4. The dialog reports success, but the run and bundles tabs can still show A.
5. Further edits can mutate stale objects that are no longer present in `AppDataService.Document`.

Required fix:

- Add one `ReloadFromDocument`/rehydration path that rebuilds bundle wrappers, selection, summaries, item selection, settings, and floating-controller text from the imported document.
- Invoke it only after the imported document has been saved successfully.
- Ensure event subscriptions and command states are refreshed without duplicating handlers.

Required regression tests:

- Importing B over A makes B immediately visible and selectable in all tabs.
- Editing after import changes the imported document, not the old graph.
- Last-selected bundle/count and summaries reflect imported settings.

### P1-4: Backup replacement can show success even when persistence fails

Locations:

- `src/SahelBundleKeyboard.App/Services/AppDataService.cs:45-63`
- `src/SahelBundleKeyboard.App/ViewModels/SettingsViewModel.cs:218-234`

Root cause:

`ReplaceDocument` assigns the new in-memory document before saving. `AppDataService.Save` catches `PersistenceException`, shows an error, and returns normally. `SettingsViewModel` then continues and displays the successful-import message.

Reproduction:

1. Make the Data location unwritable or force `JsonDataStore.Save` to fail.
2. Import and confirm a valid backup.
3. The save error is shown, followed by an import-success message; memory and disk can disagree.

Required fix:

- Make persistence success/failure explicit: return a result or allow the exception to reach the coordinating workflow.
- Save the candidate imported document first; update `AppDataService.Document` and rehydrate the UI only after the atomic disk write succeeds.
- On failure, keep the previous document and UI graph active.

Required regression tests:

- A failed backup save leaves the previous document and UI untouched.
- No success dialog is shown after a persistence failure.
- A successful import creates the safety copy, writes the new document, swaps memory state, and refreshes the UI in that order.

### P1-5: A shortcut conflict is saved before Windows accepts the shortcut

Locations:

- `src/SahelBundleKeyboard.App/ViewModels/SettingsViewModel.cs:129-154`
- `src/SahelBundleKeyboard.App/App.xaml.cs:129-163`
- `src/SahelBundleKeyboard.Windows/Hotkeys/GlobalHotkeyManager.cs:45-101`

Root cause:

The candidate shortcut is validated syntactically, persisted, and only then passed to `RegisterHotKey`. If Windows reports a conflict, `GlobalHotkeyManager` restores the previously registered set, but the view model and JSON document retain the failed candidate.

Consequences:

- The displayed/persisted shortcut can differ from the shortcut that is actually registered.
- After restart, the failed candidate is tried again and the complete global shortcut set may remain unavailable until the user edits it.
- Clearing the capture box also leaves the old persisted/registered shortcut active without a clear error.

Required fix:

- Treat shortcut changes as a transaction: parse and validate the complete candidate set, register it, then persist and update the UI only on success.
- Return a result from the apply callback; do not use a `void` callback that hides registration failure.
- On failure, restore the three previous UI values, document values, and registered hotkeys.
- Either reject an empty shortcut with an Arabic validation message or explicitly support disabling a shortcut.

Required regression tests:

- Registration conflict leaves UI, JSON, and active registrations on the previous set.
- Successful registration persists the canonical values.
- Empty/cleared values follow the documented behavior.
- Restart uses the last successfully registered configuration.

## P2 findings

### P2-1: Structurally invalid but parseable JSON can crash startup or backup preview

Locations:

- `src/SahelBundleKeyboard.Infrastructure/Persistence/JsonDataStore.cs:58-83`
- `src/SahelBundleKeyboard.Infrastructure/Backup/BackupService.cs:75-115`
- `src/SahelBundleKeyboard.Core/Validation/BundleValidator.cs:41-55,61-90`

Examples such as `{"schemaVersion":1,"settings":null,"bundles":null}` parse without a `JsonException`. Later calls to `document.Bundles.Count`, settings validation, or item validation dereference null.

Required fix:

- Validate and normalize the entire deserialized graph before use: document, settings, bundle list, each bundle, item list, strings, IDs, orders, and configured limits.
- Treat invalid structure like other corrupt/unsupported data without overwriting the source file.
- Apply the same graph validator to primary data and backup preview.

Required regression tests:

- Null settings, bundles, bundle items, item strings, and null list entries are rejected safely.
- The primary data file is quarantined/preserved; a backup preview is rejected without changing current data.

### P2-2: The “continue without global shortcuts” fallback still throws

Locations:

- `src/SahelBundleKeyboard.App/App.xaml.cs:52-82`
- `src/SahelBundleKeyboard.App/ViewModels/SettingsViewModel.cs:16-33`

`App` catches message-window creation failure, sets `_hotkeys = null`, and says the application can continue. It then passes null to `SettingsViewModel`, whose constructor rejects it with `ArgumentNullException`.

Required fix:

- Make shortcut availability an explicit optional capability.
- When unavailable, keep the run window and floating controls usable, disable shortcut editing/testing, and show one persistent Arabic explanation.
- Add a composition/startup test for a failing hotkey-manager factory.

### P2-3: Progress and summary multiply item count by bundle count

Locations:

- `src/SahelBundleKeyboard.App/ViewModels/MainViewModel.cs:130-140,330-332`

The automation enters every saved item once and multiplies its quantity by the whole-bundle count. It does not repeat every item once per bundle. Therefore, `items × bundleCount` is not the number of product entries or progress total.

Example: a 30-item bundle sold with count 10 should show 30 entries and progress `0 / 30`, not `0 / 300` during countdown before switching to `1 / 30` after the first engine event.

Required fix:

- Use `Items.Count` for expected entries and progress total.
- Keep bundle count only in quantity calculations and descriptive copy.
- Add a view-model test using bundle count greater than one.

### P2-4: The floating controller does not visibly show all required states

Locations:

- `src/SahelBundleKeyboard.App/ViewModels/MainViewModel.cs:236-240`
- `src/SahelBundleKeyboard.App/Views/FloatingControllerWindow.xaml`
- `src/SahelBundleKeyboard.App/Views/FloatingControllerWindow.xaml.cs:33-65`

`ControllerStateBadge` exists but is not bound in the controller. Status handling extracts countdown digits but does not render a persistent Completed, Stopped, or Error message. The user may see the Go button re-enable without knowing why the run ended.

Required fix:

- Bind a compact state/status label using Arabic text.
- Show Countdown, Running, Paused, Stopped, Completed, and Error without activating the window.
- Test property-change notifications for controller title, count, progress, state, and status.

### P2-5: Corrupt-data recovery does not offer the workflow requested by the brief

Locations:

- `src/SahelBundleKeyboard.Infrastructure/Persistence/JsonDataStore.cs:86-105`
- `src/SahelBundleKeyboard.App/App.xaml.cs:104-107`

The application automatically quarantines the file and starts with empty in-memory data after showing a warning. The brief requires an Arabic error and an explicit choice to start empty or restore a backup. Automatic quarantine is safe for bytes, but the recovery decision is missing.

Required fix:

- Return a recovery result without silently selecting the empty-data path.
- Offer Start Empty, Restore Backup, and Exit/Cancel where appropriate.
- Never overwrite or delete the broken file.
- Test each recovery choice at the application coordinator level.

### P2-6: Manual release dispatch force-moves existing version tags

Location:

- `.github/workflows/release.yml:46-55`

`git tag -f` plus `git push --force` changes the commit represented by an already-published version. During this review, `v1.0.0` moved as release assets were rebuilt. That makes checksums and historical source identity mutable and conflicts with normal GitFlow/SemVer expectations.

Required fix:

- If the requested tag does not exist, create it at the selected release commit.
- If it exists at the same commit, allow an intentional asset rerun with `--clobber`.
- If it exists at a different commit, fail with a clear message and require a new version such as `v1.0.1`.
- Do not force-push published version tags.

### P2-7: GitHub Actions use action majors that now emit runtime-deprecation warnings

Locations:

- `.github/workflows/ci.yml:16-20`
- `.github/workflows/release.yml:33-39`
- `.github/actions/build-portable/action.yml:81-92`

The latest successful run warns that the v4 actions target deprecated Node.js 20 and are being forced onto Node.js 24. The brief asked for current stable major versions.

Required fix:

- Check the official repositories for the current stable majors of `actions/checkout`, `actions/setup-dotnet`, and `actions/upload-artifact`.
- Update all workflows consistently.
- Rerun CI and confirm the deprecation annotations are gone.

### P2-8: Stop and completion do not return to Idle as specified

Locations:

- `src/SahelBundleKeyboard.Core/Automation/AutomationEngine.cs:190-214`
- `src/SahelBundleKeyboard.App/ViewModels/MainViewModel.cs:364-379`

The engine remains in `Stopped`, `Completed`, or `Error` indefinitely. The brief asks for a visible completion/stop status followed by a reset to Idle.

Required fix:

- Define the intended transition timing explicitly.
- Preserve Completed/Stopped/Error long enough for the operator to read it, then transition to Idle without stealing focus.
- Ensure a new run cannot overlap the previous task during the transition.
- Add deterministic state-transition tests using an injectable status-duration service or coordinator-level timer.

### P2-9: Windows acceptance is still an open release gate

Location:

- `docs/manual-windows-test.md:107-112`

The Windows 10 and Windows 11 sign-off rows are empty. CI cannot verify foreground focus, `SendInput`, global hotkeys, no-activate mouse behavior, DPI behavior, UIPI, or interaction with Sahel.

Required action:

- Do not describe the project as production-ready until the checklist is executed on Windows 10 x64 and Windows 11 x64.
- Record machine/version, tester, date, failures, and follow-up commits.
- Publish a new immutable patch release after the checklist and all P1 findings are resolved.

## P3 findings

### P3-1: Application-level behavior has no automated test project

The 112 tests validate Core and Infrastructure, but the P1 defects live in `SahelBundleKeyboard.App`. Add a Windows-targeted application/view-model test project or refactor coordination logic into testable non-WPF classes.

Minimum coverage:

- Edit commit and autosave coordination.
- Command enablement after selection changes.
- Backup import transaction and UI rehydration.
- Shortcut registration transaction and rollback.
- Startup with unavailable hotkey capability.
- Progress/state projection for the main and floating windows.

Do not attempt to automate real keyboard injection in CI; keep that in the manual Windows checklist.

### P3-2: Release notes and version history should describe patch releases, not rewrite v1.0.0

The Unicode marshaling and progress-binding fixes landed after the original v1.0.0 release and the release assets/tag were updated in place. Future fixes should increment the patch version and preserve the history of what users downloaded.

## Recommended implementation order

1. Add an application/view-model test project or extract testable coordinators.
2. Fix edit commit/autosave and persistence result handling.
3. Make backup import a disk-first transaction and rehydrate all UI state.
4. Fix command invalidation for bundle/item selection and ordering.
5. Make shortcut registration transactional and repair the no-hotkey fallback.
6. Harden JSON graph validation and implement the recovery-choice workflow.
7. Correct progress totals and floating-controller status projection.
8. Make release tags immutable and update official action majors.
9. Run the full Windows 10/11 manual checklist and publish a new patch release.

## Definition of done for the fix pass

- Every P1 reproduction fails on the pre-fix code and passes after the fix.
- New regression tests cover the application/view-model coordination paths.
- All existing Core and Infrastructure tests still pass.
- Windows CI completes with zero build warnings, zero test failures, and no deprecated-action annotations.
- No released version tag is force-moved.
- Backup/persistence failure paths never show a success message.
- The UI and persisted document always reference the same post-import object graph.
- Valid edits survive restart without requiring Start, Duplicate, Add, Delete, or bundle selection as an accidental save trigger.
- The complete manual Windows checklist is signed off on Windows 10 x64 and Windows 11 x64.
- Sahel testing is recorded separately, including the Administrator/UIPI case.

## Handoff instruction for the implementation agent

Work from the latest `develop` according to the repository's GitFlow policy. Fix one root cause at a time with a failing regression test first. Do not bundle unrelated refactors, do not force-update `v1.0.0`, and do not claim a finding resolved from code inspection alone. Push the fixes through Windows CI, merge through the normal branch flow, update `CHANGELOG.md`, then create a new patch release only after the manual Windows gates are complete.
