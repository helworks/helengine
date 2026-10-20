# helengine

helengine is the shared engine and editor workspace used to build platform packages from project `.heproj` files.

## Workspace setup

Clone this main repository, then run the bootstrap from its checkout. It clones the
selected repositories into sibling directories, using paths relative to the main
checkout rather than a machine-specific workspace path. Git and access to the
selected repositories are required.

```powershell
git clone https://github.com/helworks/helengine.git
cd helengine
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/setup-workspace.ps1
```

With no selection argument, the script offers an interactive list. For automation:

```powershell
# Clone all platform repositories and the shared HelPhysics repository.
./scripts/setup-workspace.ps1 -All

# Clone only these platforms; comma-separated IDs also work with powershell -File.
./scripts/setup-workspace.ps1 -Platforms ps1,n64,windows

# List IDs or preview the selection without changing checkouts.
./scripts/setup-workspace.ps1 -List
./scripts/setup-workspace.ps1 -Platforms ps1,n64 -WhatIf
```

The catalog is `scripts/workspace-repositories.json`; `gamecube` selects
`helengine-gc`, `dc` selects `helengine-dc`, and `physics` selects HelPhysics.
`-Repositories` is an alias for `-Platforms`. SSH or HTTPS is inferred from the main
repository's origin; `-Protocol Ssh` or `-Protocol Https` overrides that choice, including GitHub
submodule URLs, through process-local Git configuration.

Missing submodule checkouts are initialized at their recorded commits, recursively.
`-SkipSubmodules` limits the operation to sibling repositories. Initialized
submodules and existing checkouts keep their branch, revision and local edits.
Every selected existing destination must be an independent Git checkout with the
expected origin; a conflicting folder or failed Git operation stops the script
with an error. Run the bootstrap again to initialize any still-missing submodules
after an interrupted operation. This script prepares sources; building platforms
continues through the build commands below.

## Editor CLI Platform Builds

Use the shared PowerShell wrapper at [scripts/build-platform.ps1](scripts/build-platform.ps1) to restore and publish the editor CLI, then build the authored project directly.

Example:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\build-platform.ps1 `
  -Project C:\dev\helprojs\city\project.heproj `
  -Platform ds `
  -Output C:\dev\helprojs\city\ds-build `
  -BuildProfile release `
  -CacheRoot D:\helengine-cache
```

Parameters:

- `-Project`: project directory that contains `project.heproj`, or an explicit `.heproj` path
- `-Platform`: supported platform id declared in the project's `settings/platforms.json`; its selected build profile and other build selections come from `user_settings/build_config.json`
- `-Output`: output directory for the generated platform package
- `-Configuration`: optional .NET build configuration for the editor project, defaults to `Debug`
- `-BuildProfile`: platform build profile; defaults to `debug` or `release` when `-Configuration` has that name
- `-EditorProject`: optional override for the editor app `.csproj` path
- `-CacheRoot`: reusable cache root for editor publish artifacts and project/platform build intermediates
- `-WorkspaceRoot`: deprecated alias for `-CacheRoot`; when both are provided they must resolve to the same path
- `-LockTimeout`: maximum time to wait for another build of the same authored project, defaults to two hours
- `-Clean`: removes only the selected project's editor-configuration and platform/configuration/profile cache slices before building
- `-PruneCacheOlderThanDays`: removes unlocked, valid project caches older than the positive number of days; `0` disables pruning
- `-AdditionalArgs`: optional extra editor CLI arguments appended after `--`

### Script Module Build Modes

Project code uses explicit `code.module.json` declarations. Runtime modules may depend only on runtime modules; editor-only modules use `"moduleKind": "editor"` and may depend on runtime modules. A sibling test folder must be named `<module-id>.tests` and has to match a declared production module id.

Interactive editor sessions and project-authored editor commands use `EditorFull`, which includes runtime modules, editor modules, and sibling test projects. Platform cook/package builds use `RuntimeOnly`, which includes runtime production modules only and never discovers test folders or loads editor commands.

Platform build profiles can declare ordered editor prebuild commands in `user_settings/build_config.json` through `editorPrebuildCommandIdsByBuildProfileId`. These run under `EditorFull` before cooking; an omitted profile declaration runs no commands. The generic wrapper contains no project-specific command ids.

The wrapper passes the canonical authored `.heproj` path to the editor and never copies the project. Editor publish artifacts, generated managed code, generated native code, and builder working files use deterministic project/configuration/profile cache paths. Builds of the same authored project wait on one project lock; builds of different projects use different locks and can overlap.

### Cache, Output, and Invocation Contract

The reusable `v2` cache has a compact identity derived from the canonical authored project path. Editor publish artifacts also include the canonical editor-checkout identity, so a different editor checkout cannot reuse another checkout's editor outputs. The requested output must be disjoint from the selected project's cache; the wrapper rejects an output path that is the cache root, contains it, or is contained by it.

No repository copy is made: the wrapper builds the authored project in place and keeps reusable intermediates under `-CacheRoot`. `-AdditionalArgs` is for extra editor CLI arguments only; it cannot override the wrapper-owned `--project`, `--build`, `--build-profile`, or `--output` switches.

Builds targeting the same output are serialized across projects by an output lock. This is in addition to the project lock, so different projects can overlap only when they use different output paths. `HELENGINE_BUILD_INVOCATION_ID` is a wrapper/waiter internal correlation contract (a canonical GUID), not a normal user setting to place in project or shell configuration.

Windows builds retain CMake/Ninja objects and content-mirrored generated C++ between runs. Builds through the wrapper keep them in the selected `-CacheRoot` platform/profile slice. Direct editor builds use the authored project's ignored `cache/build/windows/<profile>/` directory. Generated files with unchanged contents keep their modification times, so Ninja can skip their compilation on the next build.

### Native Stable-Cache Smoke

The real Windows native smoke requires the sibling platform source at `C:\dev\helworks\helengine-windows`, Visual Studio C++ developer tools, `cmake.exe`, Ninja, the Windows builder assembly, and the published external codegen tool (including its MSBuild BuildHost companion). It copies only the tiny authored-project fixture into a disposable child of `C:\dev\helworks\builds\ncs`, configures disposable platform settings, and runs the production wrapper twice. Both invocations must use the same cache, produce a non-empty `helengine_windows.exe`, and leave a current successful build-state file.

Run it explicitly; it is intentionally not part of the default fast suite because it depends on the external Windows platform repository and native toolchain:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-native-cache-smoke.tests.ps1
```

The editor and platform builder write directly to the authored project and requested output. If a build fails, source mutations, partial output, and reusable cache content remain for diagnosis; the wrapper does not roll them back or stage an atomic output replacement. Use `-Clean` only when the selected reusable cache slices need to be rebuilt.

Exit codes:

The wrapper uses these codes for its own validation and orchestration failures:

- `0`: build completed successfully
- `2`: invalid wrapper arguments such as missing required values
- `3`: editor project `.csproj` path was not found
- `4`: project `.heproj` path was not found
- `5`: the published editor assembly was missing after a successful publish command
- `6`: the csharpcodegen submodule was not initialised, or the published codegen tool was missing after a successful publish command
- `10`: wrapper or internal orchestration failure, including failure to write terminal state after an otherwise successful build

### Codegen comes from the engine

The C# to C++ codegen is a git submodule at `engine/vendor/csharpcodegen`, pinned by the engine commit. `scripts/build-platform.ps1` publishes it in Release into a `codegen/` directory beside the published editor, and the editor resolves that copy for every platform build. Platform entries in the platform registry no longer carry a `codegenToolPath`; an entry that still has one is ignored with a warning. To change which codegen the engine uses, bump the submodule and commit the pin. After cloning or switching branches run `git submodule update --init --recursive`.

Restore, publish, and editor child process failures are propagated unchanged. Those child exit codes can numerically coincide with the wrapper-defined codes above, so callers must inspect emitted diagnostics and any available `.helengine-build-state.json` context to distinguish collisions.

## Verified Build Waiting

Use `tools/build-waiter` whenever a build needs an automatically verified completion result. It launches the child build, forwards its diagnostics, and succeeds only after the child exits with code `0`, the output contains a current successful `.helengine-build-state.json`, and every required artifact is fresh and non-empty.

For a waiter-controlled build targeting an output shared with another build, the wrapper keeps that output serialized until the waiter validates the exact terminal proof and attempts required-artifact verification. The waiter then writes an exact invocation-specific acknowledgment, which the wrapper consumes before releasing the same-output lock. This acknowledgment phase is internal to the wrapper/waiter contract: `HELENGINE_BUILD_INVOCATION_ID` and `HELENGINE_BUILD_WAITER_PROTOCOL` are not user-authored shell or project configuration settings.

Direct calls to the platform wrapper do not use the waiter acknowledgment phase. For waiter-controlled calls, an otherwise successful wrapper build without an exact acknowledgment fails with exit code `10` after a fixed 30-second wait.

Example PS2 build:

```powershell
dotnet run --project C:\dev\helworks\helengine\tools\build-waiter\helengine.buildwaiter.csproj -- `
  --output C:\dev\helprojs\output\ps2 `
  --require game.iso `
  --require disc/SYSTEM.CNF `
  --require disc/HELENGIN.ELF `
  -- powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\build-platform.ps1 `
  -Project C:\dev\helprojs\demodisc\project.heproj `
  -Platform ps2 `
  -Output C:\dev\helprojs\output\ps2
```


## Build Numeric Precision

Numeric precision belongs to each resolved build request. `real-storage-bits` selects the physical width of engine `Real` fields; `real-arithmetic-bits` selects `RealEvaluation` expression precision. The queue captures both values, and the same versioned identity is required by cooked assets, generated Core, scene modules, native receipts and cache keys. A mismatched identity fails before the dependent payload is consumed.

For example, select a Windows double build without changing the authored project's default or another queued build:

```powershell
& C:\dev\helworks\helengine\scripts\build-platform.ps1 `
  -Project C:\dev\helprojs\demodisc `
  -Platform windows -BuildProfile profiler -Configuration Debug `
  -Output C:\temp\numeric-precision\windows\package `
  -CacheRoot C:\temp\numeric-precision\windows\cache `
  -AdditionalArgs @('--environment=debug', '--real-storage-bits=64', '--real-arithmetic-bits=64')
```

Use `32` for both options for a float build. Both selections use the same output and overwrite the previous package. The editor also exposes the two settings through each backend's existing codegen profile. Unsupported combinations are rejected rather than approximated by another evaluator.

The authoring host uses 64/64, independently of the build selection. Stored engine vectors, matrices and migrated animation values use `Real`; an expression retains evaluation precision until its result reaches a stored destination. Ordinary C# `float`/`double`, fixed-width binary IO, SDK/GPU buffers and monotonic clocks keep their explicit contracts.

Binary16 storage occupies two bytes per `Real`. In 16/32, `(2048 + 1) - 2048` evaluates to `1` unless an explicit intermediate store rounds it. In 16/16, each primitive result is rounded to binary16 and the same expression yields `0`. Portable conversion does not require a target half instruction. Half math uses float primitives followed by explicit half rounding; it does not expose unrounded float intermediates as binary16 evaluation results.

Current target validation:

| Target | Storage/arithmetic | Validation |
| --- | --- | --- |
| Windows | 32/32, 64/64 | DemoDisc menu and all 14 rendering scenes, 300 frames each; Tracy menu captures |
| PS1 | 32/32 | Main menu loads in DuckStation; six steady 600-frame CPU windows |
| PS1 | 16/32 | Menu runs in DuckStation; six steady 600-frame CPU windows; guest tests pass all 65,536 codec patterns, 40 arithmetic cases and quaternion normalization |
| PS1 | 16/16 | Guest numeric self-test passes and retail menu runs; the full profiler build fails allocation during menu loading. Experimental selection remains a local candidate |

These results do not establish half support on other installed backends or every Cartesian combination of the three widths. The 16-bit scalar/codec tests pass in both 16/32 and 16/16. The host native differential test covers 2,048 input pairs: primitive arithmetic and square roots match the independent `System.Half` oracle, while the tested trigonometric outputs allow at most one binary16 ULP. This is not a bound for every input or for an untested target math library.

The latest Windows 64/64 scene rerun completed all 15 scenes without presentation failures. Its subsequent 1,800-frame menu recapture exceeded the 80-second test timeout; that attempt is not a passing Tracy validation. The table's Tracy evidence comes from the earlier successful captures. The right-aligned Windows menu was also checked by Helena.

PS1 menu measurements use medians of six 600-frame windows:

| Measurement | 32/32 | 16/32 |
| --- | ---: | ---: |
| Update | 2.2745 ms | 2.786 ms |
| Draw | 19.147 ms | 22.7845 ms |
| AnimationPlayer update | 1.099 ms | 1.519 ms |
| Cooked content | 471,926 bytes | 439,560 bytes |
| Published disc image | 1,853,440 bytes | 1,869,824 bytes |
| Profiled static RAM footprint (includes startup self-test) | 1,370,112 bytes | 1,421,312 bytes |
| Profiled heap high-water mark | 537,624 bytes | 510,088 bytes |

Binary16 saves cooked data and heap high-water space in this menu, but increases executable/static memory and elapsed update/draw time. The profiled static footprint includes the half startup self-test, which is absent from retail builds; it is not an isolated codec-size measurement. It is not a speed improvement here. The 16/16 retail image is 1,910,784 bytes with 439,560 bytes of cooked content and 528,384 bytes of heap capacity. Its menu runs without render failures and reaches the same 496,480-byte heap high-water mark as 16/32 retail. The instrumented 16/16 image leaves only 505,856 bytes for heap and fails a 1,536-byte allocation during menu loading, before any comparable component capture. Its guest numeric self-test passes all 65,536 codec patterns, 40 arithmetic cases and quaternion normalization. Actual MIPS sizes are 13,504 bytes for the profiler sink and 12,392 bytes for its timing window; the profiler reserves 128 component rows while this menu updates five components. Reducing component capacity to 32 would save 6,912 bytes, but that configuration change is awaiting architectural feedback. PS1 rendering scenes and visible half-precision differences remain unverified.

The clock quantum is 64 microseconds. Timings are elapsed guest time, including waits inside native calls; they are not instruction-exclusive CPU time. Stage scopes nest, so their rows must not be summed. A reported `0.000 ms` is below measurement resolution, not proof of zero work. The allocator's heap end is a high-water mark, not retained live RAM; retained heap usage has not been measured. The previous 32/32 capture measured update 2.304 ms and draw 19.413 ms. The current comparison report is `C:\temp\numeric-precision\ps1\precision-comparison.json`.
