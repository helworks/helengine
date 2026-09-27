# Editor test suite cleanup

> Execute inline using superpowers:executing-plans; no independent review or delegation without the user's opt-in.

**Goal:** Remove literal production-source checks and fix the remaining editor-test failures without weakening behavioral coverage.

**Architecture:** Keep tests of runtime behavior, serialized data, reflection contracts and generated outputs. Delete tests whose result depends only on spelling, indentation or source layout. Repair stale fixtures and environmental assumptions before changing production behavior.

**Tech stack:** C# / .NET 9, xUnit, Roslyn for bounded test inventory.

**Spec:** User approved removing source string matches and continuing fixes after the full-suite audit on 2026-09-27.

## Constraints

- Preserve the existing dirty workspace and prior changes. No blanket reset, fixture byte patching, or native generated-code rewrites.
- Use workspace build directories, not explicit temporary output paths.
- No independent review, commit, or external publication was requested.
- Keep unsupported asset versions rejected; re-author stale test scenes through current authoring code.

## Tasks

- [x] Inventory and remove production-source text tests in `engine/helengine.editor.tests`, retaining mixed-file behavioral cases. Compile the project to catch orphan helpers and references.
- [x] Make `EditorRendererShaderDependencyTests` and platform integration fixtures use explicit test-owned dependencies; inspect the existing authoring commands before refreshing old scene fixtures.
- [x] Reproduce and fix scene-open null dependencies, asset-authoring/cache expectations, DS/audio packaging failures and DemoDisc determinism. Use the failing cases from `builds/editor-full-tests/editor-final-recheck.trx` as the initial red baseline; add focused behavior tests where needed.
- [x] Run affected groups, then the full editor suite. Record failures/skips honestly and inspect the final diff.

## Evidence and rulings

- Baseline: full suite 3033 passed / 108 failed / 16 skipped. Re-running the failures at the expected source-relative binary location yielded 63 passed / 45 failed.
- Ruling: work in the current checkout because the baseline includes existing uncommitted changes; a new worktree would not test the user’s actual state.
- Ruling: generated code/output assertions are retained; they test the generator's public output, unlike reading implementation source.
- Removed 97 editor source-text test methods, retained compiled-metadata tests in mixed files, removed source-text portions from two mixed methods, and changed feature-catalog checks to parse JSON. Removed one analogous source-text test from the fixture generator project.
- Automatic approval review rejected the initial whole-file deletion because mixed files could contain behavioral coverage. Switched to exact method removals and preserved remaining behavior/reflection tests.
- Renderer fixtures now own their output directory without requiring a historical task environment variable; scene-open fixtures initialize their workspace coordinator; build-request fixtures pass the native-cache argument.
- DS fixtures explicitly publish naming and audio limits. Fixed the packager constructor to retain builder metadata and material seeding to use the published texture naming policy. The DS material test now supplies a source texture for builder-owned cooking.
- Added a red/green regression for material loading with an unavailable builder: saved schema and fields remain unchanged and files are not rewritten.
- DemoDisc determinism still compares authored files, identities, hashes, timestamps, and write generations. It excludes only the disposable last-scan acceleration snapshot, which intentionally lags incremental writes; dedicated identity-snapshot tests pass.
- Regenerated rendering scenes and their material dependencies using the maintained tool and current public authoring session/transaction API. All 8 generator tests pass.
- Six integration tests now declare their installed Windows-builder prerequisite explicitly. This machine's catalog is for engine `1.0.0+7d5e78dcc7a5948cf8f3f23e9e6ffcd5f578390f`, while the committed test project requests `1.0.0+13db86b8a91031015e3d0475799b6e6b1a56b309`; no catalog/version configuration was changed to hide that mismatch.
- Build succeeds; one existing CS0114 warning remains in `GeneratedSessionIsolationBehaviorTests.AuthoringSessionProjection.Dispose`.
- The first cleaned full-suite run exposed four order-dependent hierarchy keyboard failures. A standalone sequential reproduction proved `TextBoxComponentKeyboardFocusTests` leaked its focused textbox into the following fixture. Its teardown now disposes root entities before its core. The same reproduction passes afterward, and all 20 textbox/hierarchy keyboard tests pass.
- Repeating the full suite with a different class order exposed the same focus failures from another fixture. Added a two-live-core regression: a focused textbox in the other core incorrectly suppressed the current core's Delete shortcut. The router now checks textbox input ownership; the regression fails before that change and passes afterward, along with all 90 keyboard/focus/overlay tests. Stopped the now-outdated second full run and restarted the suite with the ownership fix.
- Final full editor run: **3035 passed, 0 failed, 22 skipped, 3057 total**, completed in 18.52 minutes. All eight hierarchy keyboard tests and the absent-builder save/load/save regression passed in the full run. Generator suite: **8 passed, 0 failed**. Final `git diff --check` is clean. Evidence: `C:/dev/helworks/builds/editor-test-cleanup/summary.md` and `editor-cleanup-final.trx`.
