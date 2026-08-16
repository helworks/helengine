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
- `-Platform`: platform id already configured in the project's `user_settings`
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

The editor and platform builder write directly to the authored project and requested output. If a build fails, source mutations, partial output, and reusable cache content remain for diagnosis; the wrapper does not roll them back or stage an atomic output replacement. Use `-Clean` only when the selected reusable cache slices need to be rebuilt.

Exit codes:

- `0`: build completed successfully
- `2`: invalid wrapper arguments such as missing required values
- `3`: editor project `.csproj` path was not found
- `4`: project `.heproj` path was not found
- any other non-zero value: propagated editor or platform build failure exit code

## Verified Build Waiting

Use `tools/build-waiter` whenever a build needs an automatically verified completion result. It launches the child build, forwards its diagnostics, and succeeds only after the child exits with code `0`, the output contains a current successful `.helengine-build-state.json`, and every required artifact is fresh and non-empty.

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
