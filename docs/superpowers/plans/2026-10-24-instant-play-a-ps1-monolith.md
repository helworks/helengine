# Instant Play A: PS1 monolithic build, determinism and heap fit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove the PS1 monolithic EXE (scene-code modules off) builds unattended from the editor CLI, builds byte-identically from a clean and a warm cache, keeps ELF + map + build id, and fits the heap through every demodisc PS1 scene in PSoXide.

**Architecture:** Small fixes in helengine-ps1 (a default fixed native run root; two scripts: a determinism verifier and a heap-peak tool), then a measured run: the demodisc's `scene-code-modules` is switched off temporarily (Helena approved, reverted afterwards), the editor CLI builds the monolith, the verifier rebuilds it twice, and PSoXide runs each scene headless while logging the allocator's high-water mark.

**Tech Stack:** C# (helengine-ps1 builder, xunit), PowerShell 5.1 + pwsh 7 (build scripts), Python 3 (tools, unittest), docker image `ghcr.io/grumpycoders/pcsx-redux-build@sha256:4f99ff0d...` (toolchain.lock.json), PSoXide `launch`.

**Spec:** `docs/superpowers/specs/2026-10-24-instant-console-play-design.md` (helengine b718e801), goals 1-4 and "Decided with Helena" (PS1 phase 1 is a monolith; fit numbers).

## Facts this plan relies on (read 2026-10-24)

- Switch: demodisc `C:\dev\helprojs\demodisc\settings\platform.ps1.json:36`, `codegen.selectedOptionValues["scene-code-modules"]` = `"true"`. With `"false"` the build graph writes no scene-module source plan (`EditorPlatformBuildGraphRunner.cs:212-215`) and the PS1 builder/executor take the monolith path (`PlayStation1PlatformAssetBuilder.cs:78-80`, `PlayStation1NativeBuildExecutor.cs:126,155-157`).
- Monolith: `build.ps1` runs docker `make BUILD=Release ... AUTHORED=1 ... all` (build.ps1:443-453); Core is one unity TU (`generated-core/helengine_core_unity.cpp`) at -O3; content is embedded via `authored/authored_assets.s`. Output is `helengine_ps1.{ps-exe,elf,map}` + `manifest.json`; **no disc** in monolith mode. The executor copies these to `--output` and writes `launch-artifact.json` whose `artifactSha256` is the image hash (build id) (executor :89-116). ELF is unstripped (`-g`).
- Run root: `build.ps1:80-81` falls back to `HELENGINE_PS1_BUILD_RUN_ROOT` and throws if both are empty; the executor passes no `-RunRoot` when the env var is unset in monolith mode (executor :124-130). `HELENGINE_PS1_REPOSITORY_ROOT` is required (executor :28-31).
- Editor CLI: `dotnet <helengine.editor.app.dll> --project C:\dev\helprojs\demodisc --build ps1 --build-profile release --output C:\dev\helworks\builds\ps1\package` (`EditorCliArgumentParser.cs:51-202`). Stable cache mode needs `HELENGINE_BUILD_CACHE_ROOT`, `HELENGINE_BUILD_CONFIGURATION`, `HELENGINE_BUILD_PROFILE` (`EditorBuildIsolationPathResolver.cs:288,344-366`); `C:\dev\helworks\builds\ps1\cache\v2` already exists from earlier builds.
- Heap: allocator `third_party/nugget/common/libc/alloc.c` keeps the high-water mark `maximum_heap_end` (updated :346-347) in static `s_heap_metadata` (:102-108); heap spans `[__heap_start, __stack_start = 0x801F0000)` (`linker/retail-memory.ld`).
- PSoXide headless: `target\release\psoxide-emulator.exe launch --path <ps-exe> --steps N --press "tick:button[:hold],..." --route-log P --route-watch-u32 ADDR --dump-display P` (`emu/crates/frontend/src/cli.rs` `LaunchArgs` :230-606).
- PS1 scenes (demodisc `settings/build_config.json`, ps1): `DemoDiscMainMenu` + rendering/axis_test, axis_test2, colored_cube_grid, cube_test, directional_shadow_plaza, pbr_material_gallery, pbr_shadow_theater, pbr_textured_showcase, ray_tracing_soft_shadows, ray_tracing_spheres, ray_tracing_teapot, software_path_tracer, test_scene_matrix_render, textured_cube_grid. Menu: main menu → Rendering panel (4 rows visible, Back item), accept/return = PS1 controls 0/1 (`platform.ps1.json`).

## Global Constraints

- Real checkouts on main; no worktrees/branches. helengine-ps1 files this plan edits are clean today (`builder/PlayStation1NativeBuildExecutor.cs`, `scripts/*` it creates); other sessions' dirty files there (`src/platform/ps1/*`, `tests/host/*Tests.cpp`, `scripts/build-scene-modules.py`) are never staged. If a file to edit becomes dirty, stop and ask Helena.
- demodisc `settings/platform.ps1.json`: changed to `"false"` only during Task 4 and restored byte-exact (`git diff --quiet -- settings/platform.ps1.json` must succeed afterwards). Never committed.
- One build location per platform, overwritten: output `C:\dev\helworks\builds\ps1\package`, native run root `C:\dev\helworks\builds\ps1\native`, cache `C:\dev\helworks\builds\ps1\cache`. No dated/attempt folders.
- Temporary files under `C:\temp\ps1-monolith\`, deleted at the end except the report.
- Headless PSoXide runs are approved for this plan; no windows, no screen capture.
- Small commits ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; push only when Helena asks.
- Use PowerShell with the full `dotnet.exe` path (rtk shim breaks Bash dotnet); Windows PowerShell 5.1: capture native stderr with Start-Process redirects.

## Review Focus

1. Warm cache keeps a stale object after a header-only change (the monolith fingerprint excludes Core content, build.ps1:412-413): the verifier's warm run must touch nothing and still match, and Task 2 adds a header-touch case. Test in Task 2.
2. A map without `s_heap_metadata` (symbol renamed, static stripped, LTO): heap tool fails loudly, never reports 0. Test in Task 3.
3. A scene run whose button script lands on the wrong scene: each run's final display is checked to be a non-menu frame, and the report lists the scene as unverified rather than counting it. Covered in Task 4.
4. Run root env set to a path overlapping the repo or generated core: the default path logic must not override an explicit env value, and build.ps1's overlap check still guards it. Test in Task 1.
5. Peak heap within 16 KB of the ceiling: reported as a fail (no margin), not a pass. Covered in Task 3's threshold test.

---

### Task 1: Default fixed native run root

**Files:**
- Modify: `C:\dev\helworks\helengine-ps1\builder\PlayStation1NativeBuildExecutor.cs:124-130` (`ResolveBuildRunRoot`)
- Test: `C:\dev\helworks\helengine-ps1\tests\helengine.ps1.platformbuilder.tests\PlayStation1NativeBuildRunRootTests.cs`

**Interfaces:**
- Produces: when `HELENGINE_PS1_BUILD_RUN_ROOT` is unset or blank, the executor uses `Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outputRootPath)), "native")` and always passes `-RunRoot` to build.ps1, in both monolith and scene-module modes (the scene-module "must be set" exception goes away). An explicit env value wins unchanged.

- [ ] **Step 1:** Add tests imitating the file's existing fixture pattern (fake build.ps1 that records its arguments, env restored in `finally`), with fixtures under `C:\temp\ps1-runroot-tests\` (not a dated builds folder):
  - `Monolith_without_env_passes_sibling_native_run_root`: output `<fixture>\out\package` → recorded `-RunRoot` equals `<fixture>\out\native`.
  - `Scene_modules_without_env_pass_sibling_native_run_root` (replaces any test asserting the old throw).
  - `Explicit_env_run_root_wins`: env `<fixture>\custom` → `-RunRoot <fixture>\custom`.
- [ ] **Step 2:** `& "C:\Program Files\dotnet\dotnet.exe" test tests\helengine.ps1.platformbuilder.tests --filter FullyQualifiedName~PlayStation1NativeBuildRunRootTests` → new tests fail.
- [ ] **Step 3:** Implement in `ResolveBuildRunRoot` with an XML comment explaining the default (one fixed native folder per platform, next to the output).
- [ ] **Step 4:** Same command → all pass; then the whole project's tests pass (compare by name with a pre-change run; record pre-existing failures).
- [ ] **Step 5:** Commit "Default the PS1 native run root to a fixed folder beside the output".

### Task 2: Determinism verifier

**Files:**
- Create: `C:\dev\helworks\helengine-ps1\scripts\verify-deterministic-build.ps1`

**Interfaces:**
- Produces: `pwsh -File scripts/verify-deterministic-build.ps1 -InputsRoot <dir> -RunRoot <dir> [-TouchHeader <relative path>]`.
  - `<InputsRoot>` holds `generated-core\`, `content\` and `declarations\` (whatever build.ps1's `-GeneratedCoreRoot`, `-AuthoredContentRoot`, `-AuthoredDeclarations` take), copied from a finished monolith run root.
  - Run 1: delete the native object cache (`<parent of RunRoot>\native-objects`), run `scripts/build.ps1 -Configuration Release -Authored -RunRoot <RunRoot> ...`, hash `out\helengine_ps1.ps-exe` and `out\helengine_ps1.elf` (SHA-256).
  - Run 2: same, warm cache, no deletion.
  - Run 3 (only with `-TouchHeader`): bump that file's mtime under a copy of the inputs and rebuild warm; hashes must still match (content unchanged).
  - Prints one line per run `run=<n> ps-exe=<sha> elf=<sha>`, then `DETERMINISTIC` and exit 0, or `NOT DETERMINISTIC: <file> differs between run <a> and <b>` and exit 1.

- [ ] **Step 1:** Write the script with a `-WhatIf`-free, straight-line flow; reuse build.ps1 unmodified.
- [ ] **Step 2:** Self-check without docker: run with a non-existent `-InputsRoot` → exits non-zero with a message naming the missing folder.
- [ ] **Step 3:** Commit "Add a clean-versus-warm PS1 determinism verifier". (It is exercised for real in Task 4.)

### Task 3: Heap-peak tool

**Files:**
- Create: `C:\dev\helworks\helengine-ps1\scripts\heap_peak.py`
- Test: `C:\dev\helworks\helengine-ps1\tests\host\test_heap_peak.py`

**Interfaces:**
- Produces (Python, stdlib only):
  - `find_symbol_address(map_text: str, name: str) -> int` — address of a symbol in a GNU ld map; raises `LookupError(name)` when absent.
  - `HEAP_END_FIELD_OFFSET: int` — byte offset of `maximum_heap_end` inside `s_heap_metadata`, taken from the struct at alloc.c:102-108 (document the field order in a comment).
  - `peak_from_route_log(csv_text: str, column: str) -> int` — max of a hex/decimal column in a PSoXide `--route-log` with `--route-watch-u32`.
  - `report(peak_heap_end: int, heap_start: int, ceiling: int = 0x801F0000, margin: int = 16 * 1024) -> dict` → `{"peak_bytes", "ceiling_bytes", "free_bytes", "ok"}`, `ok` false when `free_bytes < margin`.
  - CLI: `python -I -B scripts/heap_peak.py address --map <map>` prints the watch address (hex) of `s_heap_metadata + HEAP_END_FIELD_OFFSET` and `__heap_start`; `python -I -B scripts/heap_peak.py peak --map <map> --route-log <csv>...` prints the JSON report over all logs.

- [ ] **Step 1:** Tests (unittest, like `tests/host/test_retail_memory.py`): `test_finds_symbol_in_map` (a 6-line map excerpt with `s_heap_metadata` at `0x80123450`), `test_missing_symbol_raises`, `test_peak_takes_max_of_column`, `test_report_fails_inside_margin` (free 8 KB → `ok` false), `test_report_passes_with_margin` (free 100 KB → true).
- [ ] **Step 2:** `python -I -B -m unittest tests/host/test_heap_peak.py -v` (host Python, from the repo root) → fails.
- [ ] **Step 3:** Implement. Read the route-log column format from PSoXide's `--route-watch-u32` writer (cli.rs) before parsing.
- [ ] **Step 4:** Same command → passes.
- [ ] **Step 5:** Commit "Add a PS1 heap high-water tool for PSoXide route logs".

### Task 4: Measured run (monolith build, determinism, heap through every scene)

No product code; produces `C:\temp\ps1-monolith\report.md`, then the findings go into the spec.

- [ ] **Step 1: Preconditions.** helengine-ps1 at Task 3's commit; the editor app is built (`dotnet build` of `helengine.editor.app`, noting that the helengine checkout carries other sessions' uncommitted work); `helengine\user_settings\platforms.json` registers ps1 with the current engine version. If ps1 is not registered or the editor cannot load the PS1 builder, stop and ask Helena how her editor Build resolves PS1.
- [ ] **Step 2: Switch modules off.** In demodisc `settings/platform.ps1.json`, set `"scene-code-modules": "false"`.
- [ ] **Step 3: Build.** Env: `HELENGINE_PS1_REPOSITORY_ROOT=C:\dev\helworks\helengine-ps1`, `HELENGINE_BUILD_CACHE_ROOT=C:\dev\helworks\builds\ps1\cache`, `HELENGINE_BUILD_CONFIGURATION=release`, `HELENGINE_BUILD_PROFILE=release` (no run-root env: Task 1's default applies). Run the editor CLI build to `--output C:\dev\helworks\builds\ps1\package`. Expected: `helengine_ps1.ps-exe`, `.elf`, `.map`, `launch-artifact.json` (record `artifactSha256`), retail-memory check passed. Record wall time per stage from the build-graph phase durations.
- [ ] **Step 4: Restore the switch.** Set it back to `"true"`; `git -C C:\dev\helprojs\demodisc diff --quiet -- settings/platform.ps1.json` succeeds.
- [ ] **Step 5: Determinism.** Copy the run root's inputs to `C:\temp\ps1-monolith\inputs`; run Task 2's verifier with `-RunRoot C:\dev\helworks\builds\ps1\native` and `-TouchHeader` on one generated header. Expected `DETERMINISTIC`. Also compare run 1's ps-exe hash with Step 3's `artifactSha256`. On `NOT DETERMINISTIC`, find the differing ELF sections (`readelf`/`objdump` in the toolchain image) and fix the cause in build flags or scripts as a separate commit with its own reasoning, then rerun.
- [ ] **Step 6: Heap.** `heap_peak.py address --map C:\dev\helworks\builds\ps1\package\helengine_ps1.map`. For the main menu and each of the 14 rendering scenes, run `psoxide-emulator.exe launch --path <ps-exe> --steps 400000000 --press "<menu script>" --route-log C:\temp\ps1-monolith\<scene>.csv --route-watch-u32 <addr> --dump-display C:\temp\ps1-monolith\<scene>.ppm`, where the press script opens Rendering and walks to the scene (derive row order from `DemoDiscMenuDefinitionProvider.cs` in the demodisc codebase and the PS1 accept/down mapping from `tests/host/Ps1GamepadButtonMappingTests.cpp`), leaving at least 1200 route ticks inside the scene. Look at each PPM: a scene still showing the menu is marked unverified.
- [ ] **Step 7: Report.** `heap_peak.py peak` over all logs. Write `report.md`: build stage times, image hash, determinism verdict, per-scene peak heap and verified flag, overall `ok`, and the heap number vs the spec's ~530 KB estimate.
- [ ] **Step 8:** Add a short "Phase 1 measurements (2026-10-24)" subsection to the spec with the report's numbers; commit it in helengine ("Record PS1 monolith measurements"); delete `C:\temp\ps1-monolith\`. If the heap does not fit, stop and bring Helena the numbers before Plan C.
