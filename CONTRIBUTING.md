# Contributing

## The golden rule for Mac contributors

**You never build or test locally.** All compilation, tests, packaging and releases run on
GitHub-hosted Windows runners via GitHub Actions. On the Mac you only edit code in Cursor
and push with Cursor's Git interface.

## Day-to-day workflow

1. Edit code/docs in Cursor.
2. Stage and commit from Cursor's Source Control panel (conventional messages, see below).
3. Push to `develop` (or open a PR into `develop`).
4. Open the **Actions** tab on GitHub and confirm the **CI** workflow is green.
   - CI runs on `windows-latest`: locked restore → Release build (warnings as errors) → all tests
     → publish win-x64 single file → portable ZIP + SHA256 artifacts.
5. Fix anything red by pushing again. Never merge red branches.

## Branching model (gitflow)

- `main` — release-ready code only; protected in spirit: merge via `release/*` branches, tag there.
- `develop` — integration branch for features.
- `feature/<topic>` — branched from `develop`, merged back with `--no-ff`.
- `release/<x.y.z>` — stabilization before a release; merges to `main` **and** back to `develop`.
- `hotfix/<desc>` — urgent fixes off `main`, merged to `main` + `develop`.

## Releases (no local commands needed)

Option A — web UI: Actions tab → **Release** → *Run workflow* → enter `v1.2.3` → Run.
The workflow validates the version, builds/tests/packaging, creates the tag if missing,
and publishes the GitHub Release with the ZIP + SHA256 (reruns replace assets, no duplicates).

Option B — push a tag: create tag `v1.2.3` on `main` and push it.

Releases are impossible if build or tests fail.

## Commit message style

```text
feat(core): add pause gate between individual actions
fix(app): correct RTL binding path in bundles tab
docs: expand manual windows test checklist
ci: pin setup-dotnet major version
chore: regenerate lock files
```

## Changelog

Update `CHANGELOG.md` under the *Unreleased* heading as part of the same PR that changes
behavior. Release branches move those entries under the version heading.

## Code conventions

- C# nullable enabled; warnings are errors (`TreatWarningsAsErrors`) — keep it that way.
- User-facing strings are Arabic; identifiers/comments English.
- `decimal` only for quantities/prices; invariant formatting via `QuantityFormatter`.
- No new NuGet dependencies without an explicit pinned version + license note in
  `THIRD-PARTY-NOTICES.md`. Prefer zero dependencies.
- Every engine/sequence change ships with tests using the existing fakes
  (`FakeKeystrokeSender`, `ScriptedDelayService`) — no real keystrokes in CI.

## Local verification on the Mac (optional, advanced)

Not required. If you insist: install the .NET SDK user-locally
(`dotnet-install.sh --channel 10.0`), then:

```bash
dotnet build SahelBundleKeyboard.sln -c Release     # works thanks to EnableWindowsTargeting
dotnet test  tests/SahelBundleKeyboard.Core.Tests          \
            tests/SahelBundleKeyboard.Infrastructure.Tests # pure net10.0, runs on macOS
```

WPF execution still requires Windows; rely on CI and `docs/manual-windows-test.md`.
