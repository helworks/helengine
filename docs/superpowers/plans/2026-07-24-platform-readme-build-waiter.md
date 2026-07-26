# Platform README Build Waiter Implementation Plan

> **For Helena:** Execute this plan either with the subagent-driven workflow or inline. Work directly on each repository's `main` checkout; do not create worktrees.

**Goal:** Make the documented build command for every supported platform host use the verified Build Waiter, with the platform's launch artifact as the completion contract.

**Architecture:** The Build Waiter remains a host-side wrapper in `helengine`. Each platform README supplies its own output directory and the artifact paths it needs verified, then invokes the standard engine build script as the child process. Documentation-only changes stay in the platform host repositories; the engine contains the already-committed shared tool and design documents.

**Tech Stack:** Markdown, PowerShell, .NET Build Waiter console application

---

## 1. Confirm repository state and README ownership

**Files:**
- Modify: `C:\dev\helworks\helengine-3ds\README.md`
- Modify: `C:\dev\helworks\helengine-ds\README.md`
- Modify: `C:\dev\helworks\helengine-gc\README.md`
- Modify: `C:\dev\helworks\helengine-ps2\README.md`
- Modify: `C:\dev\helworks\helengine-psp\README.md`
- Modify: `C:\dev\helworks\helengine-psvita\README.md`
- Modify: `C:\dev\helworks\helengine-switch\README.md`
- Modify: `C:\dev\helworks\helengine-wii\README.md`
- Modify: `C:\dev\helworks\helengine-wiiu\README.md`
- Modify: `C:\dev\helworks\helengine-windows\README.md`
- Do not modify: `C:\dev\helworks\helengine-wiiu-gx2-clean\README.md`

**Step 1: Inspect each checkout before editing.**

Run from `C:\dev\helworks`:

```powershell
rtk git -C helengine-3ds status --short
rtk git -C helengine-ds status --short
rtk git -C helengine-gc status --short
rtk git -C helengine-ps2 status --short
rtk git -C helengine-psp status --short
rtk git -C helengine-psvita status --short
rtk git -C helengine-switch status --short
rtk git -C helengine-wii status --short
rtk git -C helengine-wiiu status --short
rtk git -C helengine-windows status --short
```

**Step 2: Preserve unrelated user changes.**

If a target README has pre-existing changes, inspect the diff and avoid overwriting it. Stop for direction if the existing change overlaps the Build section.

## 2. Standardize the eight single-artifact relative-path host READMEs

**Files:**
- Modify: `C:\dev\helworks\helengine-3ds\README.md`
- Modify: `C:\dev\helworks\helengine-ds\README.md`
- Modify: `C:\dev\helworks\helengine-gc\README.md`
- Modify: `C:\dev\helworks\helengine-psp\README.md`
- Modify: `C:\dev\helworks\helengine-switch\README.md`
- Modify: `C:\dev\helworks\helengine-wii\README.md`
- Modify: `C:\dev\helworks\helengine-wiiu\README.md`
- Modify: `C:\dev\helworks\helengine-windows\README.md`

**Step 1: Replace each direct build command with the Build Waiter wrapper.**

Use this shape in every Build section, preserving the existing project and output paths:

```powershell
dotnet run --project ..\helengine\tools\build-waiter\helengine.buildwaiter.csproj -- `
  --output <output-directory> `
  --require <launch-artifact> `
  -- powershell -NoProfile -ExecutionPolicy Bypass -File ..\helengine\scripts\build-platform.ps1 `
  -Project ..\helprojs\city\project.heproj `
  -Platform <platform-id> `
  -Output <output-directory>
```

Use these values:

| README | Platform | Output | Required artifact |
| --- | --- | --- | --- |
| `helengine-3ds` | `3ds` | `..\helprojs\city\3ds-build` | `helengine_3ds.3dsx` |
| `helengine-ds` | `ds` | `..\helprojs\city\ds-build` | `helengine_ds.nds` |
| `helengine-gc` | `gamecube` | `..\helprojs\city\gamecube-build` | `game.gcm` |
| `helengine-psp` | `psp` | `..\helprojs\city\psp-build` | `PSP/GAME/HELENGINE/EBOOT.PBP` |
| `helengine-switch` | `switch` | `..\helprojs\city\switch-build` | `helengine_switch.nro` |
| `helengine-wii` | `wii` | `..\helprojs\city\wii-build` | `game.iso` |
| `helengine-wiiu` | `wiiu` | `..\helprojs\city\wiiu-build` | `helengine_wiiu.wuhb` |
| `helengine-windows` | `windows` | `..\helprojs\city\windows-build` | `helengine_windows.exe` |

**Step 2: Normalize the standard script location.**

Ensure all eight child commands reference `..\helengine\scripts\build-platform.ps1`, not the obsolete `artifacts` location.

**Step 3: Keep the run/emulator instructions intact.**

Only change the build invocation and any nearby wording needed to explain that the command returns only after a fresh launch artifact exists.

## 3. Document the PS2 multi-artifact contract

**Files:**
- Modify: `C:\dev\helworks\helengine-ps2\README.md`

**Step 1: Wrap the PS2 build command in Build Waiter.**

Replace the direct invocation with this completion contract:

```powershell
dotnet run --project ..\helengine\tools\build-waiter\helengine.buildwaiter.csproj -- `
  --output ..\helprojs\city\ps2-build `
  --require game.iso `
  --require disc/SYSTEM.CNF `
  --require disc/HELENGIN.ELF `
  -- powershell -NoProfile -ExecutionPolicy Bypass -File ..\helengine\scripts\build-platform.ps1 `
  -Project ..\helprojs\city\project.heproj `
  -Platform ps2 `
  -Output ..\helprojs\city\ps2-build
```

**Step 2: Preserve emulator documentation.**

Keep the existing PCSX2 launch artifact reference to `game.iso` and describe the additional two required files as the disc boot contract verified before success.

## 4. Document the PS Vita editor-build contract

**Files:**
- Modify: `C:\dev\helworks\helengine-psvita\README.md`

**Step 1: Leave the native Docker build instructions unchanged.**

They are a separate workflow and are not a call to the common engine platform build script.

**Step 2: Update only the editor-build invocation.**

Replace the direct absolute-path editor build command with an absolute-path Build Waiter wrapper. Use:

```powershell
dotnet run --project C:\dev\helworks\helengine\tools\build-waiter\helengine.buildwaiter.csproj -- `
  --output C:\dev\helprojs\city\vita-build `
  --require helengine_psvita.vpk `
  -- powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\build-platform.ps1 `
  -Project C:\dev\helprojs\city\project.heproj `
  -Platform psvita `
  -Output C:\dev\helprojs\city\vita-build
```

**Step 3: Retain Vita3K launch guidance.**

The VPK path continues to be the artifact opened by Vita3K.

## 5. Validate the documented standard and commit repository-local changes

**Files:**
- Verify: the ten supported README files above
- Verify unchanged: `C:\dev\helworks\helengine-wiiu-gx2-clean\README.md`

**Step 1: Check the documentation diff.**

Run `git diff --check` in each touched repository and inspect each README diff for only the documented Build section updates.

**Step 2: Check standard tokens and contracts.**

Use `rg` to confirm each supported README contains `build-waiter\helengine.buildwaiter.csproj` and `scripts\build-platform.ps1`. Confirm the required-artifact strings match the table above, including all three PS2 requirements.

**Step 3: Confirm the experimental checkout stayed untouched.**

Run `git -C C:\dev\helworks\helengine-wiiu-gx2-clean diff -- README.md`; it must produce no changes from this work.

**Step 4: Commit only the README updates in their owning repositories.**

Create a focused documentation commit in each repository that has a changed README, such as `docs: standardize verified platform builds`. Do not stage any unrelated working-tree changes.
