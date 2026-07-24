# Platform README Build Waiter Design

## Purpose

Make verified build waiting the documented default for every active Helengine platform host. Each platform README will show one command that launches its editor build through `helengine.buildwaiter`, then verifies the artifact needed to launch that platform in its emulator.

## Scope

Update the root README in these active host repositories:

- `helengine-3ds`
- `helengine-ds`
- `helengine-gc`
- `helengine-ps2`
- `helengine-psp`
- `helengine-psvita`
- `helengine-switch`
- `helengine-wii`
- `helengine-wiiu`
- `helengine-windows`

Do not modify `helengine-wiiu-gx2-clean`, because it is a separate experimental checkout.

## Standard Command

Each README will replace its direct editor build example with `dotnet run --project ..\helengine\tools\build-waiter\helengine.buildwaiter.csproj`. The waiter receives the output root, one or more required artifacts, then the shared editor build wrapper:

```powershell
dotnet run --project ..\helengine\tools\build-waiter\helengine.buildwaiter.csproj -- `
  --output <platform-output> `
  --require <launch-artifact> `
  -- powershell -NoProfile -ExecutionPolicy Bypass -File ..\helengine\scripts\build-platform.ps1 `
  -Project ..\helprojs\city\project.heproj `
  -Platform <platform-id> `
  -Output <platform-output>
```

The documented shared script path is `..\helengine\scripts\build-platform.ps1`; obsolete `artifacts\build-platform.ps1` paths are removed from these build examples.

## Required Artifact Matrix

| Platform | Platform id | Required artifact |
| --- | --- | --- |
| Nintendo 3DS | `3ds` | `helengine_3ds.3dsx` |
| Nintendo DS | `ds` | `helengine_ds.nds` |
| GameCube | `gamecube` | `game.gcm` |
| PlayStation 2 | `ps2` | `game.iso`, `disc/SYSTEM.CNF`, `disc/HELENGIN.ELF` |
| PlayStation Portable | `psp` | `PSP/GAME/HELENGINE/EBOOT.PBP` |
| PlayStation Vita | `psvita` | `helengine_psvita.vpk` |
| Nintendo Switch | `switch` | `helengine_switch.nro` |
| Nintendo Wii | `wii` | `game.iso` |
| Wii U | `wiiu` | `helengine_wiiu.wuhb` |
| Windows | `windows` | `helengine_windows.exe` |

## Documentation Boundaries

The engine README remains the generic build-waiter reference and implementation documentation. Host READMEs contain only platform-specific build waiting examples and retain their existing emulator-run and platform-notes sections.

## Validation

Validation will compare every README's `--require` values and build output against its emulator launcher artifact path, confirm each waiter command uses the current shared build script, and ensure the experimental Wii U GX2 README is unchanged.
