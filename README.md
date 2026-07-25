# helengine

helengine is the shared engine and editor workspace used to build platform packages from project `.heproj` files.

## Editor CLI Platform Builds

Use the shared PowerShell wrapper at [scripts/build-platform.ps1](scripts/build-platform.ps1) to run platform builds through the editor CLI with `dotnet run`.

Example:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\build-platform.ps1 `
  -Project C:\dev\helprojs\city\project.heproj `
  -Platform ds `
  -Output C:\dev\helprojs\city\ds-build
```

Parameters:

- `-Project`: project directory that contains `project.heproj`, or an explicit `.heproj` path
- `-Platform`: platform id already configured in the project's `user_settings`
- `-Output`: output directory for the generated platform package
- `-Configuration`: optional .NET build configuration for the editor project, defaults to `Debug`
- `-EditorProject`: optional override for the editor app `.csproj` path
- `-AdditionalArgs`: optional extra editor CLI arguments appended after `--`

### Script Module Build Modes

Project code uses explicit `code.module.json` declarations. Runtime modules may depend only on runtime modules; editor-only modules use `"moduleKind": "editor"` and may depend on runtime modules. A sibling test folder must be named `<module-id>.tests` and has to match a declared production module id.

Interactive editor sessions and project-authored editor commands use `EditorFull`, which includes runtime modules, editor modules, and sibling test projects. Platform cook/package builds use `RuntimeOnly`, which includes runtime production modules only and never discovers test folders or loads editor commands.

Platform build profiles can declare ordered editor prebuild commands in `user_settings/build_config.json` through `editorPrebuildCommandIdsByBuildProfileId`. These run under `EditorFull` before cooking; an omitted profile declaration runs no commands. The generic wrapper contains no project-specific command ids.

Every wrapper invocation uses an isolated copied project, generated-code workspace, editor publish directory, and output path, so concurrent platform builds do not share generated artifacts.

Exit codes:

- `0`: build completed successfully
- `2`: invalid wrapper arguments such as missing required values
- `3`: editor project `.csproj` path was not found
- `4`: project `.heproj` path was not found
- any other non-zero value: propagated editor or platform build failure exit code

## Verified Build Waiting

Use `tools/build-waiter` whenever a build needs an automatically verified completion result. It launches the child build, forwards its diagnostics, and succeeds only after the child exits with code `0` and every required artifact is fresh and non-empty.

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
