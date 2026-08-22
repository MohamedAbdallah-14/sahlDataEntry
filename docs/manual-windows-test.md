# Manual Windows acceptance test

Run this checklist on a real Windows 10 x64 and a Windows 11 x64 machine before trusting the
release in production. CI cannot verify interactive focus or real keyboard injection.

Preparation: download `Sahel-Bundle-Keyboard-win-x64.zip` from the latest GitHub Release,
verify the checksum (`sha256sum -c Sahel-Bundle-Keyboard-win-x64.zip.sha256` or
`Get-FileHash` on Windows), and extract it.

## 1. Clean run without installing anything

- [ ] Machine has no .NET SDK/runtime installed (or use a fresh user account).
- [ ] Copy the extracted folder to `Desktop` (writable location — NOT `Program Files`).
- [ ] Run `SahelBundleKeyboard.exe`. The main window opens with Arabic RTL UI.
      Expected: SmartScreen may warn (unsigned); choose "More info" → "Run anyway".
- [ ] Confirm `Data\data.json` is created automatically next to the EXE after first change/save.
- [ ] Close the window; confirm the process exits fully (Task Manager) — no tray icon expected.

## 2. Create a sample bundle

Create bundle "عرض تجريبي" with these items:

| # | Code | Name | Quantity | Custom price |
|---|------|------|----------|--------------|
| 1 | 6290100000001 | زيت عافية 1 لتر | 2.5 | 45 |
| 2 | *(empty)* | أرز أبو كاس ٥ كيلو | 1 | *(empty)* |
| 3 | 6290200000002 | Tea Pack Gold | 0.25 | 10.5 |
| 4 | 6290300000003 | شاي فتلة × 100 | 3 | *(empty)* |

- [ ] Arabic + English names accepted, decimals accepted, empty code falls back to name.
- [ ] Reorder rows with ▲/▼ and confirm the order persists after restart.
- [ ] Try an invalid row: quantity `-1`, then `abc`, then price `-5`.
      Expected: red cell validation, Start refuses with an understandable Arabic message.
- [ ] Duplicate the bundle; rename the copy; delete the original (confirmation dialog appears).

## 3. Calculation with multiple counts

For count = 2 expect typed quantities: `5`, `2`, `0.5`, `6`.
- [ ] Summary shows final quantities and total entries (items × count).
- [ ] Count rejects `0`, `-3`, `2.5`, and Arabic digits are normalized (٣ → works as 3).

## 4. Generated sequence in Notepad

- [ ] Open Notepad, click inside it (so Notepad has focus).
- [ ] Select the bundle, count = 1, press the global Start shortcut `Ctrl+Alt+G`.
- [ ] After the countdown, Notepad receives exactly:
      `6290100000001 ⏎ 2.5 ⏎ 45 ⏎` then `أرز أبو كاس ٥ كيلو ⏎ 1 ⏎ ⏎` etc.
- [ ] Items with no custom price show a bare Enter where the price would go,
      followed by **five** confirmation Enters (blank lines in Notepad) that dismiss
      any intermittent Sahel popup before the next product.
- [ ] Verify dot decimal separator always (never `2,5`).

## 5. Floating Go button does not steal focus

- [ ] Show the floating controller; click its drag area and move it around.
- [ ] Focus Notepad again, then click **ابدأ ▶** on the controller.
- [ ] Typing goes into Notepad — the controller must NOT become the active window
      (its buttons never take keyboard focus).
- [ ] Pause/Resume/Stop from the controller behave like the main window buttons.

## 6. Global shortcuts while other apps focused

With Notepad/Sahel focused:
- [ ] `Ctrl+Alt+G` starts (countdown appears on controller + status in header).
- [ ] Mid-run press `Ctrl+Alt+P`: state becomes "موقوف مؤقتاً", typing stops immediately.
- [ ] Press `Ctrl+Alt+P` again: resumes at exactly the next pending action (no repeat/skip —
      compare final text against expectation).
- [ ] Press `Ctrl+Alt+S`: immediate stop; state "تم الإيقاف"; nothing further typed.
- [ ] Double-start: pressing Start while running is ignored/reported, never overlaps.
- [ ] Change all three shortcuts in Settings (click box, press combo), restart app,
      confirm new shortcuts work and persist. Assigning the same combo twice must be rejected
      in Arabic while keeping previous valid values. F12 must be rejected.

## 7. Countdown values

- [ ] Set countdown 0: start begins typing immediately.
- [ ] Set countdown 3: big numbers 3→2→1 visible on controller; Stop during countdown cancels
      cleanly (state Stopped, nothing typed).

## 8. Import/export backup and corruption handling

- [ ] Export CSV template + Excel template; open both in Excel; Arabic headers readable.
- [ ] Fill the template with valid + invalid rows; import via preview window:
      invalid rows shown red with per-row Arabic errors; commit only valid rows;
      test both "append" and "replace" choices.
- [ ] Import an XLSX saved by Excel (shared strings) containing Arabic names and decimal quantities.
- [ ] Export full backup JSON; inspect it contains bundles/items/order/settings.
- [ ] Modify data, import the backup back: summary appears, confirmation required,
      data replaced, and `Data/backups/pre-import-*.json` safety copy created.
- [ ] Corruption: close app, replace `Data/data.json` content with `{ broken`,
      run app: Arabic error, file preserved as `data.json.corrupt-*`, fresh start works,
      original corrupt file still readable.

## 9. Testing with Sahel at normal privilege level

- [ ] Run Sahel normally (not elevated). Run this utility normally.
- [ ] Click into Sahel's product-search field; start via hotkey and via floating button.
- [ ] Verify every product lands in the search field, quantities/prices land correctly,
      and the final state shows "اكتمل".
- [ ] Repeat with delay 120 ms and 250 ms; pick the stable value for production settings.

## 10. Elevated Sahel workaround

- [ ] Run Sahel as administrator. Run this utility normally: keystrokes do NOT reach Sahel
      (documented UIPI limitation).
- [ ] Right-click `SahelBundleKeyboard.exe` → Run as administrator; repeat step 9.
- [ ] Confirm success and document the customer instruction: "شغّل البرنامج كمسؤول إذا كان سهل يعمل كمسؤول".

## Sign-off

| Machine | Windows version | Result | Tester / date |
|---|---|---|---|
| … | Windows 10 x64 | ☐ Pass ☐ Fail | |
| … | Windows 11 x64 | ☐ Pass ☐ Fail | |
