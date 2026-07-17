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

Exit codes:

- `0`: build completed successfully
- `2`: invalid wrapper arguments such as missing required values
- `3`: editor project `.csproj` path was not found
- `4`: project `.heproj` path was not found
- any other non-zero value: propagated editor or platform build failure exit code
