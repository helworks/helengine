# helengine

helengine is the shared engine and editor workspace used to build platform packages from project `.heproj` files.

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

### Native Stable-Cache Smoke

The real Windows native smoke requires the sibling platform source at `C:\dev\helworks\helengine-windows`, Visual Studio C++ developer tools, `cmake.exe`, Ninja, the Windows builder assembly, and the published external codegen tool (including its MSBuild BuildHost companion). It copies only the tiny authored-project fixture into a disposable child of `C:\tmp`, configures disposable platform settings, and runs the production wrapper twice. Both invocations must use the same cache, produce a non-empty `helengine_windows.exe`, and leave a current successful build-state file.

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
- `10`: wrapper or internal orchestration failure, including failure to write terminal state after an otherwise successful build

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
