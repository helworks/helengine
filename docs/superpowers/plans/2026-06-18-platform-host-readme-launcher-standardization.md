# Platform Host README And Launcher Standardization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Standardize the ten platform host repositories around a minimal root `README.md`, a repo-local `docs/Docker.md`, and a canonical `scripts/launch_in_emulator.ps1` entrypoint that always requires `-ArtifactPath`.

**Architecture:** Treat each platform host repo as an independent cutover with the same public contract and platform-specific internals. Preserve the existing emulator/bootstrap logic where it already exists, move it behind `scripts/launch_in_emulator.ps1`, and add script-audit tests so the new contract is enforced by code instead of by README drift.

**Tech Stack:** PowerShell, xUnit, .NET 9 builder test projects, Docker/Makefile native build flows, repo-local README/docs markdown.

---

## File Map

### Existing launchers that must be replaced

- Modify: `C:\dev\helworks\helengine-wii\scripts\launch_wii_iso_in_dolphin.ps1`
- Modify: `C:\dev\helworks\helengine-ps2\scripts\launch_ps2_iso_in_pcsx2.ps1`
- Modify: `C:\dev\helworks\helengine-gc\scripts\launch_gamecube_image_in_dolphin.ps1`
- Modify: `C:\dev\helworks\helengine-ds\artifacts\launch-melonds-rom.ps1`
- Modify: `C:\dev\helworks\helengine-psvita\tools\launch-vita3k.ps1`
- Modify: `C:\dev\helworks\helengine-switch\artifacts\build-and-launch-switch.ps1`
- Modify: `C:\dev\helworks\helengine-wiiu\scripts\launch_wiiu_rpx_in_cemu.ps1`
- Modify: `C:\dev\helworks\helengine-psp\tools\install_psp_output_to_ppsspp.ps1`
- Modify: `C:\dev\helworks\helengine-psp\tools\run_ppsspp_boot_check.ps1`

### New canonical launcher files

- Create: `C:\dev\helworks\helengine-wii\scripts\launch_in_emulator.ps1`
- Create: `C:\dev\helworks\helengine-ps2\scripts\launch_in_emulator.ps1`
- Create: `C:\dev\helworks\helengine-gc\scripts\launch_in_emulator.ps1`
- Create: `C:\dev\helworks\helengine-ds\scripts\launch_in_emulator.ps1`
- Create: `C:\dev\helworks\helengine-psp\scripts\launch_in_emulator.ps1`
- Create: `C:\dev\helworks\helengine-psvita\scripts\launch_in_emulator.ps1`
- Create: `C:\dev\helworks\helengine-switch\scripts\launch_in_emulator.ps1`
- Create: `C:\dev\helworks\helengine-wiiu\scripts\launch_in_emulator.ps1`
- Create: `C:\dev\helworks\helengine-windows\scripts\launch_in_emulator.ps1`
- Create: `C:\dev\helworks\helengine-3ds\scripts\launch_in_emulator.ps1`

### Launcher audit tests to update or add

- Modify: `C:\dev\helworks\helengine-wii\builder.tests\WiiDolphinLauncherScriptTests.cs`
- Create: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2Pcsx2LauncherScriptTests.cs`
- Modify: `C:\dev\helworks\helengine-gc\builder.tests\GameCubeDolphinLauncherScriptTests.cs`
- Create: `C:\dev\helworks\helengine-ds\builder.tests\NintendoDsMelonDsLauncherScriptTests.cs`
- Create: `C:\dev\helworks\helengine-psp\builder.tests\PspPpssppLauncherScriptTests.cs`
- Modify: `C:\dev\helworks\helengine-psvita\builder.tests\Vita3KLauncherScriptAuditTests.cs`
- Create: `C:\dev\helworks\helengine-switch\builder.tests\SwitchEmulatorLauncherScriptTests.cs`
- Modify: `C:\dev\helworks\helengine-wiiu\builder.tests\WiiUCemuLauncherScriptTests.cs`
- Create: `C:\dev\helworks\helengine-windows\builder.tests\WindowsLauncherScriptTests.cs`
- Create: `C:\dev\helworks\helengine-3ds\builder.tests\Nintendo3DsLauncherScriptTests.cs`

### Root docs to normalize

- Modify: `C:\dev\helworks\helengine-wii\README.md`
- Modify: `C:\dev\helworks\helengine-ps2\README.md`
- Modify: `C:\dev\helworks\helengine-gc\README.md`
- Modify: `C:\dev\helworks\helengine-ds\README.md`
- Modify: `C:\dev\helworks\helengine-psp\README.md`
- Modify: `C:\dev\helworks\helengine-psvita\README.md`
- Modify: `C:\dev\helworks\helengine-switch\README.md`
- Modify: `C:\dev\helworks\helengine-wiiu\README.md`
- Modify: `C:\dev\helworks\helengine-windows\README.md`
- Modify: `C:\dev\helworks\helengine-3ds\README.md`

### `docs/Docker.md` files to create or replace

- Create: `C:\dev\helworks\helengine-wii\docs\Docker.md`
- Create: `C:\dev\helworks\helengine-ps2\docs\Docker.md`
- Create: `C:\dev\helworks\helengine-gc\docs\Docker.md`
- Create: `C:\dev\helworks\helengine-ds\docs\Docker.md`
- Create: `C:\dev\helworks\helengine-psp\docs\Docker.md`
- Create: `C:\dev\helworks\helengine-psvita\docs\Docker.md`
- Create: `C:\dev\helworks\helengine-switch\docs\Docker.md`
- Create: `C:\dev\helworks\helengine-wiiu\docs\Docker.md`
- Create: `C:\dev\helworks\helengine-windows\docs\Docker.md`
- Create: `C:\dev\helworks\helengine-3ds\docs\Docker.md`

### Old launcher entrypoints to remove after cutover

- Delete: `C:\dev\helworks\helengine-wii\scripts\launch_wii_iso_in_dolphin.ps1`
- Delete: `C:\dev\helworks\helengine-ps2\scripts\launch_ps2_iso_in_pcsx2.ps1`
- Delete: `C:\dev\helworks\helengine-gc\scripts\launch_gamecube_image_in_dolphin.ps1`
- Delete: `C:\dev\helworks\helengine-ds\artifacts\launch-melonds-rom.ps1`
- Delete: `C:\dev\helworks\helengine-psvita\tools\launch-vita3k.ps1`
- Delete: `C:\dev\helworks\helengine-switch\artifacts\build-and-launch-switch.ps1`
- Delete: `C:\dev\helworks\helengine-wiiu\scripts\launch_wiiu_rpx_in_cemu.ps1`
- Delete: `C:\dev\helworks\helengine-psp\tools\install_psp_output_to_ppsspp.ps1`
- Delete: `C:\dev\helworks\helengine-psp\tools\run_ppsspp_boot_check.ps1`

### Platform contract matrix

- `helengine-wii`
  Artifact: `game.iso`
  Emulator: `Dolphin.exe`
  Validation: `.iso`
- `helengine-ps2`
  Artifact: `game.iso`
  Emulator: `pcsx2-qt.exe`
  Validation: `.iso`
- `helengine-gc`
  Artifact: `game.gcm`
  Emulator: `Dolphin.exe`
  Validation: `.gcm`, `.iso`, `.gcz`
- `helengine-ds`
  Artifact: `helengine_ds.nds`
  Emulator: `melonDS.exe`
  Validation: `.nds`
- `helengine-psp`
  Artifact: `EBOOT.PBP`
  Emulator: `PPSSPPWindows64.exe`
  Validation: `EBOOT.PBP`
- `helengine-psvita`
  Artifact: `helengine_psvita.vpk`
  Emulator: `Vita3K.exe`
  Validation: `.vpk`
- `helengine-switch`
  Artifact: `helengine_switch.nro`
  Emulator: `eden-cli.exe`
  Validation: `.nro`
- `helengine-wiiu`
  Artifact: `helengine_wiiu.rpx`
  Emulator: `Cemu.exe`
  Validation: `.rpx`
- `helengine-windows`
  Artifact: `helengine_windows.exe`
  Emulator contract: direct native launch through the same canonical script path
  Validation: `.exe`
- `helengine-3ds`
  Artifact: `helengine_3ds.3dsx`
  Emulator: `Lime3DS` or `Azahar`
  Validation: `.3dsx`

### Task 1: Cut Over Wii, GameCube, And Wii U

**Files:**
- Modify: `C:\dev\helworks\helengine-wii\builder.tests\WiiDolphinLauncherScriptTests.cs`
- Create: `C:\dev\helworks\helengine-wii\scripts\launch_in_emulator.ps1`
- Modify: `C:\dev\helworks\helengine-wii\README.md`
- Create: `C:\dev\helworks\helengine-wii\docs\Docker.md`
- Delete: `C:\dev\helworks\helengine-wii\scripts\launch_wii_iso_in_dolphin.ps1`
- Modify: `C:\dev\helworks\helengine-gc\builder.tests\GameCubeDolphinLauncherScriptTests.cs`
- Create: `C:\dev\helworks\helengine-gc\scripts\launch_in_emulator.ps1`
- Modify: `C:\dev\helworks\helengine-gc\README.md`
- Create: `C:\dev\helworks\helengine-gc\docs\Docker.md`
- Delete: `C:\dev\helworks\helengine-gc\scripts\launch_gamecube_image_in_dolphin.ps1`
- Modify: `C:\dev\helworks\helengine-wiiu\builder.tests\WiiUCemuLauncherScriptTests.cs`
- Create: `C:\dev\helworks\helengine-wiiu\scripts\launch_in_emulator.ps1`
- Modify: `C:\dev\helworks\helengine-wiiu\README.md`
- Create: `C:\dev\helworks\helengine-wiiu\docs\Docker.md`
- Delete: `C:\dev\helworks\helengine-wiiu\scripts\launch_wiiu_rpx_in_cemu.ps1`

- [ ] **Step 1: Rewrite the existing launcher audit tests to describe the canonical contract**

```csharp
// helengine-wii/builder.tests/WiiDolphinLauncherScriptTests.cs
string scriptPath = Path.Combine(repositoryRootPath, "scripts", "launch_in_emulator.ps1");
Assert.True(File.Exists(scriptPath), "Expected scripts/launch_in_emulator.ps1 to exist.");
Assert.Contains("[string]$ArtifactPath", scriptSource, StringComparison.Ordinal);
Assert.Contains("Dolphin.exe", scriptSource, StringComparison.Ordinal);
Assert.Contains("'-u', $userDir, '-e', $resolvedArtifactPath", scriptSource, StringComparison.Ordinal);
Assert.DoesNotContain("[string]$IsoPath", scriptSource, StringComparison.Ordinal);
Assert.Contains("launch_in_emulator.ps1", readmeSource, StringComparison.Ordinal);
Assert.Contains("-ArtifactPath", readmeSource, StringComparison.Ordinal);

// helengine-gc/builder.tests/GameCubeDolphinLauncherScriptTests.cs
string scriptPath = Path.Combine(repositoryRootPath, "scripts", "launch_in_emulator.ps1");
Assert.Contains("[string]$ArtifactPath", scriptSource, StringComparison.Ordinal);
Assert.Contains("Dolphin.exe", scriptSource, StringComparison.Ordinal);
Assert.Contains("'-u', $userDir, '-e', $resolvedArtifactPath", scriptSource, StringComparison.Ordinal);
Assert.DoesNotContain("[string]$ImagePath", scriptSource, StringComparison.Ordinal);

// helengine-wiiu/builder.tests/WiiUCemuLauncherScriptTests.cs
string scriptPath = Path.Combine(repositoryRootPath, "scripts", "launch_in_emulator.ps1");
Assert.Contains("[string]$ArtifactPath", scriptSource, StringComparison.Ordinal);
Assert.Contains("Cemu.exe", scriptSource, StringComparison.Ordinal);
Assert.Contains("'-g', $resolvedArtifactPath", scriptSource, StringComparison.Ordinal);
Assert.DoesNotContain("[string]$RpxPath", scriptSource, StringComparison.Ordinal);
```

- [ ] **Step 2: Run the focused audit tests and confirm they fail against the old filenames and old parameter names**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-wii\builder.tests\helengine.wii.builder.tests.csproj --filter WiiDolphinLauncherScriptTests
rtk dotnet test C:\dev\helworks\helengine-gc\builder.tests\helengine.gamecube.builder.tests.csproj --filter GameCubeDolphinLauncherScriptTests
rtk dotnet test C:\dev\helworks\helengine-wiiu\builder.tests\helengine.wiiu.builder.tests.csproj --filter WiiUCemuLauncherScriptTests
```

Expected: FAIL because each test now looks for `scripts/launch_in_emulator.ps1` and `-ArtifactPath`.

- [ ] **Step 3: Implement the canonical launcher and skeleton docs for the three repos**

```powershell
# helengine-wii/scripts/launch_in_emulator.ps1
param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath
)

$ErrorActionPreference = 'Stop'
$resolvedArtifactPath = [System.IO.Path]::GetFullPath($ArtifactPath)
if (-not (Test-Path -LiteralPath $resolvedArtifactPath -PathType Leaf)) {
    throw "Artifact was not found: $resolvedArtifactPath"
}
if ([System.IO.Path]::GetExtension($resolvedArtifactPath) -ine '.iso') {
    throw "Expected a .iso artifact but got '$resolvedArtifactPath'."
}

# Keep the existing Dolphin profile seeding logic unchanged after the parameter rename.
$process = Start-Process -FilePath $dolphinPath -ArgumentList '-u', $userDir, '-e', $resolvedArtifactPath -PassThru
Write-Output ("ARTIFACT=" + $resolvedArtifactPath)
Write-Output ("PROCESS_ID=" + $process.Id)

# helengine-gc/scripts/launch_in_emulator.ps1
$allowedExtensions = @('.gcm', '.iso', '.gcz')
$artifactExtension = [System.IO.Path]::GetExtension($resolvedArtifactPath).ToLowerInvariant()
if ($allowedExtensions -notcontains $artifactExtension) {
    throw "Expected one of .gcm, .iso, or .gcz but got '$resolvedArtifactPath'."
}
$process = Start-Process -FilePath $dolphinPath -ArgumentList '-u', $userDir, '-e', $resolvedArtifactPath -PassThru

# helengine-wiiu/scripts/launch_in_emulator.ps1
if ([System.IO.Path]::GetExtension($resolvedArtifactPath) -ine '.rpx') {
    throw "Expected a .rpx artifact but got '$resolvedArtifactPath'."
}
$process = Start-Process -FilePath $cemuPath -ArgumentList '-g', $resolvedArtifactPath -WorkingDirectory $userDir -PassThru

# helengine-wii/README.md
# Helengine Wii Host
#
# This repository contains the Wii platform host and builder integration for Helengine.
#
# ## Build
# powershell -NoProfile -ExecutionPolicy Bypass -File ..\helengine\artifacts\build-platform.ps1 `
#   -Project ..\helprojs\city\project.heproj `
#   -Platform wii `
#   -Output ..\helprojs\city\wii-build
#
# ## Run In Emulator
# powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\launch_in_emulator.ps1 `
#   -ArtifactPath ..\helprojs\city\wii-build\game.iso
#
# ## More Docs
# - [Docker Build Notes](docs/Docker.md)

# helengine-gc/README.md
# Use -Platform gamecube and -ArtifactPath ..\helprojs\city\gamecube-build\game.gcm

# helengine-wiiu/README.md
# Use -Platform wiiu and -ArtifactPath ..\helprojs\city\wiiu-build\helengine_wiiu.rpx

# helengine-wii/docs/Docker.md
# Docker build:
# docker build -t helengine-wii .
# docker run --rm -v "$PWD":/workspace -w /workspace helengine-wii make
#
# Keep the existing builder-helper and packaged-disc details here instead of in README.md.

# helengine-gc/docs/Docker.md
# Move the current Docker, generated-core, packed-disc, and verification notes here.
#
# helengine-wiiu/docs/Docker.md
# docker build -t helengine-wiiu .
# docker run --rm -v "$PWD":/workspace -w /workspace helengine-wiiu make
```

- [ ] **Step 4: Run the targeted tests again and verify the README and launcher audits pass**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-wii\builder.tests\helengine.wii.builder.tests.csproj --filter WiiDolphinLauncherScriptTests
rtk dotnet test C:\dev\helworks\helengine-gc\builder.tests\helengine.gamecube.builder.tests.csproj --filter GameCubeDolphinLauncherScriptTests
rtk dotnet test C:\dev\helworks\helengine-wiiu\builder.tests\helengine.wiiu.builder.tests.csproj --filter WiiUCemuLauncherScriptTests
```

Expected: PASS.

- [ ] **Step 5: Commit the Wii, GameCube, and Wii U cutover**

```powershell
rtk proxy git -C C:\dev\helworks\helengine-wii add README.md docs/Docker.md scripts/launch_in_emulator.ps1 builder.tests/WiiDolphinLauncherScriptTests.cs
rtk proxy git -C C:\dev\helworks\helengine-wii rm scripts/launch_wii_iso_in_dolphin.ps1
rtk proxy git -C C:\dev\helworks\helengine-wii commit -m "feat: standardize wii launcher and readme"

rtk proxy git -C C:\dev\helworks\helengine-gc add README.md docs/Docker.md scripts/launch_in_emulator.ps1 builder.tests/GameCubeDolphinLauncherScriptTests.cs
rtk proxy git -C C:\dev\helworks\helengine-gc rm scripts/launch_gamecube_image_in_dolphin.ps1
rtk proxy git -C C:\dev\helworks\helengine-gc commit -m "feat: standardize gamecube launcher and readme"

rtk proxy git -C C:\dev\helworks\helengine-wiiu add README.md docs/Docker.md scripts/launch_in_emulator.ps1 builder.tests/WiiUCemuLauncherScriptTests.cs
rtk proxy git -C C:\dev\helworks\helengine-wiiu rm scripts/launch_wiiu_rpx_in_cemu.ps1
rtk proxy git -C C:\dev\helworks\helengine-wiiu commit -m "feat: standardize wiiu launcher and readme"
```

### Task 2: Add Canonical PS2 And DS Launchers

**Files:**
- Create: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2Pcsx2LauncherScriptTests.cs`
- Create: `C:\dev\helworks\helengine-ps2\scripts\launch_in_emulator.ps1`
- Modify: `C:\dev\helworks\helengine-ps2\README.md`
- Create: `C:\dev\helworks\helengine-ps2\docs\Docker.md`
- Delete: `C:\dev\helworks\helengine-ps2\scripts\launch_ps2_iso_in_pcsx2.ps1`
- Create: `C:\dev\helworks\helengine-ds\builder.tests\NintendoDsMelonDsLauncherScriptTests.cs`
- Create: `C:\dev\helworks\helengine-ds\scripts\launch_in_emulator.ps1`
- Modify: `C:\dev\helworks\helengine-ds\README.md`
- Create: `C:\dev\helworks\helengine-ds\docs\Docker.md`
- Delete: `C:\dev\helworks\helengine-ds\artifacts\launch-melonds-rom.ps1`

- [ ] **Step 1: Add failing audit tests for the new PS2 and DS launcher contract**

```csharp
// helengine-ps2/builder.tests/Ps2Pcsx2LauncherScriptTests.cs
public sealed class Ps2Pcsx2LauncherScriptTests {
    [Fact]
    public void Launcher_RequiresArtifactPath_AndKeepsPcsx2FastbootContract() {
        string repositoryRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string scriptPath = Path.Combine(repositoryRootPath, "scripts", "launch_in_emulator.ps1");
        Assert.True(File.Exists(scriptPath));
        string scriptSource = File.ReadAllText(scriptPath);
        Assert.Contains("[string]$ArtifactPath", scriptSource, StringComparison.Ordinal);
        Assert.Contains("pcsx2-qt.exe", scriptSource, StringComparison.Ordinal);
        Assert.Contains("'-fastboot', '-logfile', $logFilePath, '--', $resolvedArtifactPath", scriptSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[string]$IsoPath", scriptSource, StringComparison.Ordinal);
    }
}

// helengine-ds/builder.tests/NintendoDsMelonDsLauncherScriptTests.cs
public sealed class NintendoDsMelonDsLauncherScriptTests {
    [Fact]
    public void Launcher_RequiresArtifactPath_AndLaunchesMelonDs() {
        string repositoryRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string scriptPath = Path.Combine(repositoryRootPath, "scripts", "launch_in_emulator.ps1");
        Assert.True(File.Exists(scriptPath));
        string scriptSource = File.ReadAllText(scriptPath);
        Assert.Contains("[string]$ArtifactPath", scriptSource, StringComparison.Ordinal);
        Assert.Contains("melonDS.exe", scriptSource, StringComparison.Ordinal);
        Assert.Contains("Start-Process -FilePath $ResolvedMelonDsPath -ArgumentList @($ResolvedArtifactPath)", scriptSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[string]$RomPath", scriptSource, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run the new tests first and verify they fail because the canonical script does not exist yet**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter Ps2Pcsx2LauncherScriptTests
rtk dotnet test C:\dev\helworks\helengine-ds\builder.tests\helengine.ds.builder.tests.csproj --filter NintendoDsMelonDsLauncherScriptTests
```

Expected: FAIL on missing `scripts/launch_in_emulator.ps1`.

- [ ] **Step 3: Implement both launchers and shrink the root docs**

```powershell
# helengine-ps2/scripts/launch_in_emulator.ps1
param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath
)

$ErrorActionPreference = 'Stop'
$resolvedArtifactPath = [System.IO.Path]::GetFullPath($ArtifactPath)
if (-not (Test-Path -LiteralPath $resolvedArtifactPath -PathType Leaf)) {
    throw "Artifact was not found: $resolvedArtifactPath"
}
if ([System.IO.Path]::GetExtension($resolvedArtifactPath) -ine '.iso') {
    throw "Expected a .iso artifact but got '$resolvedArtifactPath'."
}

# Keep the existing isolated launcher directory and PCSX2 logfile contract.
$process = Start-Process -FilePath $pcsx2Path -ArgumentList '-fastboot', '-logfile', $logFilePath, '--', $resolvedArtifactPath -WorkingDirectory (Split-Path -Path $pcsx2Path -Parent) -PassThru
Write-Output ("ARTIFACT=" + $resolvedArtifactPath)
Write-Output ("PROCESS_ID=" + $process.Id)

# helengine-ds/scripts/launch_in_emulator.ps1
param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath,

    [Parameter()]
    [string]$MelonDsPath = "C:\dev\helworks\emus\melonDS-1.1-windows-x86_64\melonDS.exe"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ResolvedArtifactPath = [System.IO.Path]::GetFullPath($ArtifactPath)
if (-not (Test-Path -LiteralPath $ResolvedArtifactPath -PathType Leaf)) {
    [Console]::Error.WriteLine("Artifact file was not found at '$ResolvedArtifactPath'.")
    exit 3
}
if ([System.IO.Path]::GetExtension($ResolvedArtifactPath) -ine '.nds') {
    [Console]::Error.WriteLine("Expected a .nds artifact at '$ResolvedArtifactPath'.")
    exit 4
}

$MelonDsProcess = Start-Process -FilePath $ResolvedMelonDsPath -ArgumentList @($ResolvedArtifactPath) -WorkingDirectory (Split-Path $ResolvedMelonDsPath) -PassThru
Write-Host ("ARTIFACT: " + $ResolvedArtifactPath)
Write-Host ("PROCESS_ID: " + $MelonDsProcess.Id)

# helengine-ps2/README.md
# Build with -Platform ps2 and run:
# powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\launch_in_emulator.ps1 `
#   -ArtifactPath ..\helprojs\city\ps2-build\game.iso

# helengine-ds/README.md
# Build with -Platform ds and run:
# powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\launch_in_emulator.ps1 `
#   -ArtifactPath ..\helprojs\city\ds-build\helengine_ds.nds
```

- [ ] **Step 4: Re-run the new audit tests plus one existing DS smoke slice**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter Ps2Pcsx2LauncherScriptTests
rtk dotnet test C:\dev\helworks\helengine-ds\builder.tests\helengine.ds.builder.tests.csproj --filter "NintendoDsMelonDsLauncherScriptTests|NintendoDsBootHostSourceAuditTests"
```

Expected: PASS.

- [ ] **Step 5: Commit the PS2 and DS cutover**

```powershell
rtk proxy git -C C:\dev\helworks\helengine-ps2 add README.md docs/Docker.md scripts/launch_in_emulator.ps1 builder.tests/Ps2Pcsx2LauncherScriptTests.cs
rtk proxy git -C C:\dev\helworks\helengine-ps2 rm scripts/launch_ps2_iso_in_pcsx2.ps1
rtk proxy git -C C:\dev\helworks\helengine-ps2 commit -m "feat: standardize ps2 launcher and readme"

rtk proxy git -C C:\dev\helworks\helengine-ds add README.md docs/Docker.md scripts/launch_in_emulator.ps1 builder.tests/NintendoDsMelonDsLauncherScriptTests.cs
rtk proxy git -C C:\dev\helworks\helengine-ds rm artifacts/launch-melonds-rom.ps1
rtk proxy git -C C:\dev\helworks\helengine-ds commit -m "feat: standardize ds launcher and readme"
```

### Task 3: Cut Over PSP And PS Vita

**Files:**
- Create: `C:\dev\helworks\helengine-psp\builder.tests\PspPpssppLauncherScriptTests.cs`
- Create: `C:\dev\helworks\helengine-psp\scripts\launch_in_emulator.ps1`
- Modify: `C:\dev\helworks\helengine-psp\README.md`
- Create: `C:\dev\helworks\helengine-psp\docs\Docker.md`
- Delete: `C:\dev\helworks\helengine-psp\tools\install_psp_output_to_ppsspp.ps1`
- Delete: `C:\dev\helworks\helengine-psp\tools\run_ppsspp_boot_check.ps1`
- Modify: `C:\dev\helworks\helengine-psvita\builder.tests\Vita3KLauncherScriptAuditTests.cs`
- Create: `C:\dev\helworks\helengine-psvita\scripts\launch_in_emulator.ps1`
- Modify: `C:\dev\helworks\helengine-psvita\README.md`
- Create: `C:\dev\helworks\helengine-psvita\docs\Docker.md`
- Delete: `C:\dev\helworks\helengine-psvita\tools\launch-vita3k.ps1`

- [ ] **Step 1: Add the PSP launcher audit and update the Vita audit to the canonical path**

```csharp
// helengine-psp/builder.tests/PspPpssppLauncherScriptTests.cs
public sealed class PspPpssppLauncherScriptTests {
    [Fact]
    public void Launcher_RequiresArtifactPath_StagesEboot_AndStartsPpsspp() {
        string repositoryRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string scriptPath = Path.Combine(repositoryRootPath, "scripts", "launch_in_emulator.ps1");
        Assert.True(File.Exists(scriptPath));
        string scriptSource = File.ReadAllText(scriptPath);
        Assert.Contains("[string]$ArtifactPath", scriptSource, StringComparison.Ordinal);
        Assert.Contains("PPSSPPWindows64.exe", scriptSource, StringComparison.Ordinal);
        Assert.Contains("EBOOT.PBP", scriptSource, StringComparison.Ordinal);
        Assert.Contains("Copy-Item -LiteralPath $resolvedArtifactPath", scriptSource, StringComparison.Ordinal);
    }
}

// helengine-psvita/builder.tests/Vita3KLauncherScriptAuditTests.cs
string scriptPath = PsVitaRepositoryPathResolver.ResolvePath("scripts", "launch_in_emulator.ps1");
Assert.Contains("ArtifactPath", scriptSource, StringComparison.Ordinal);
Assert.DoesNotContain("VpkPath", scriptSource, StringComparison.Ordinal);
```

- [ ] **Step 2: Run the PSP and Vita script audits first**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-psp\builder.tests\helengine.psp.builder.tests.csproj --filter PspPpssppLauncherScriptTests
rtk dotnet test C:\dev\helworks\helengine-psvita\builder.tests\helengine.psvita.builder.tests.csproj --filter Vita3KLauncherScriptAuditTests
```

Expected: FAIL because the canonical script path does not exist yet.

- [ ] **Step 3: Implement the PSP staging launcher and the renamed Vita launcher**

```powershell
# helengine-psp/scripts/launch_in_emulator.ps1
param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath
)

$ErrorActionPreference = 'Stop'
$resolvedArtifactPath = [System.IO.Path]::GetFullPath($ArtifactPath)
if (-not (Test-Path -LiteralPath $resolvedArtifactPath -PathType Leaf)) {
    throw "Artifact was not found: $resolvedArtifactPath."
}
if ([System.IO.Path]::GetFileName($resolvedArtifactPath) -ine 'EBOOT.PBP') {
    throw "Expected the PSP artifact to be EBOOT.PBP but got '$resolvedArtifactPath'."
}

$ppssppExePath = 'C:\dev\helworks\emus\ppsspp_win\PPSSPPWindows64.exe'
$targetRoot = 'C:\dev\helworks\emus\ppsspp_win\memstick\PSP\GAME\HELENGINE'
$targetEbootPath = Join-Path $targetRoot 'EBOOT.PBP'
if (Test-Path -LiteralPath $targetRoot) {
    Remove-Item -LiteralPath $targetRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null
Copy-Item -LiteralPath $resolvedArtifactPath -Destination $targetEbootPath -Force
$process = Start-Process -FilePath $ppssppExePath -ArgumentList $targetEbootPath -PassThru
Write-Output ("ARTIFACT=" + $resolvedArtifactPath)
Write-Output ("TARGET_EBOOT=" + $targetEbootPath)
Write-Output ("PROCESS_ID=" + $process.Id)

# helengine-psvita/scripts/launch_in_emulator.ps1
param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath,
    [switch]$KeepInstalledTitle
)

$ResolvedArtifactPath = (Resolve-Path -LiteralPath $ArtifactPath).ProviderPath
if ([System.IO.Path]::GetExtension($ResolvedArtifactPath) -ine '.vpk') {
    throw "Expected a .vpk artifact but got '$ResolvedArtifactPath'."
}

# Keep the existing delete/install/relaunch flow; only rename the public parameter and script path.
Start-Process -FilePath $Vita3KPath -ArgumentList @('-r', $InstalledTitleId, '-S', 'eboot.bin') -WindowStyle Normal
Write-Output "ARTIFACT=$ResolvedArtifactPath"

# helengine-psp/README.md
# Build with -Platform psp and run:
# powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\launch_in_emulator.ps1 `
#   -ArtifactPath ..\helprojs\city\psp-build\PSP\GAME\HELENGINE\EBOOT.PBP

# helengine-psvita/README.md
# Build with -Platform psvita and run:
# powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\launch_in_emulator.ps1 `
#   -ArtifactPath ..\helprojs\city\psvita-build\helengine_psvita.vpk
```

- [ ] **Step 4: Re-run the launcher audits and one adjacent build test per repo**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-psp\builder.tests\helengine.psp.builder.tests.csproj --filter "PspPpssppLauncherScriptTests|PspNativeBuildExecutorTests"
rtk dotnet test C:\dev\helworks\helengine-psvita\builder.tests\helengine.psvita.builder.tests.csproj --filter "Vita3KLauncherScriptAuditTests|PsVitaPlatformPluginManifestTests"
```

Expected: PASS.

- [ ] **Step 5: Commit the PSP and PS Vita cutover**

```powershell
rtk proxy git -C C:\dev\helworks\helengine-psp add README.md docs/Docker.md scripts/launch_in_emulator.ps1 builder.tests/PspPpssppLauncherScriptTests.cs
rtk proxy git -C C:\dev\helworks\helengine-psp rm tools/install_psp_output_to_ppsspp.ps1 tools/run_ppsspp_boot_check.ps1
rtk proxy git -C C:\dev\helworks\helengine-psp commit -m "feat: standardize psp launcher and readme"

rtk proxy git -C C:\dev\helworks\helengine-psvita add README.md docs/Docker.md scripts/launch_in_emulator.ps1 builder.tests/Vita3KLauncherScriptAuditTests.cs
rtk proxy git -C C:\dev\helworks\helengine-psvita rm tools/launch-vita3k.ps1
rtk proxy git -C C:\dev\helworks\helengine-psvita commit -m "feat: standardize psvita launcher and readme"
```

### Task 4: Cut Over Switch, 3DS, And Windows

**Files:**
- Create: `C:\dev\helworks\helengine-switch\builder.tests\SwitchEmulatorLauncherScriptTests.cs`
- Create: `C:\dev\helworks\helengine-switch\scripts\launch_in_emulator.ps1`
- Modify: `C:\dev\helworks\helengine-switch\README.md`
- Create: `C:\dev\helworks\helengine-switch\docs\Docker.md`
- Delete: `C:\dev\helworks\helengine-switch\artifacts\build-and-launch-switch.ps1`
- Create: `C:\dev\helworks\helengine-3ds\builder.tests\Nintendo3DsLauncherScriptTests.cs`
- Create: `C:\dev\helworks\helengine-3ds\scripts\launch_in_emulator.ps1`
- Modify: `C:\dev\helworks\helengine-3ds\README.md`
- Create: `C:\dev\helworks\helengine-3ds\docs\Docker.md`
- Create: `C:\dev\helworks\helengine-windows\builder.tests\WindowsLauncherScriptTests.cs`
- Create: `C:\dev\helworks\helengine-windows\scripts\launch_in_emulator.ps1`
- Modify: `C:\dev\helworks\helengine-windows\README.md`
- Create: `C:\dev\helworks\helengine-windows\docs\Docker.md`

- [ ] **Step 1: Add launcher audit tests for the three remaining repos**

```csharp
// helengine-switch/builder.tests/SwitchEmulatorLauncherScriptTests.cs
Assert.Contains("[string]$ArtifactPath", scriptSource, StringComparison.Ordinal);
Assert.Contains(".nro", scriptSource, StringComparison.Ordinal);
Assert.Contains("eden-cli.exe", scriptSource, StringComparison.Ordinal);
Assert.DoesNotContain("[string]$Project", scriptSource, StringComparison.Ordinal);

// helengine-3ds/builder.tests/Nintendo3DsLauncherScriptTests.cs
Assert.Contains("[string]$ArtifactPath", scriptSource, StringComparison.Ordinal);
Assert.Contains(".3dsx", scriptSource, StringComparison.Ordinal);
Assert.Contains("Lime3DS", scriptSource, StringComparison.Ordinal);

// helengine-windows/builder.tests/WindowsLauncherScriptTests.cs
Assert.Contains("[string]$ArtifactPath", scriptSource, StringComparison.Ordinal);
Assert.Contains(".exe", scriptSource, StringComparison.Ordinal);
Assert.Contains("Start-Process -FilePath $resolvedArtifactPath", scriptSource, StringComparison.Ordinal);
Assert.Contains("launch_in_emulator.ps1", readmeSource, StringComparison.Ordinal);
```

- [ ] **Step 2: Run the new tests and observe the expected missing-script failures**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-switch\builder.tests\helengine.switch.builder.tests.csproj --filter SwitchEmulatorLauncherScriptTests
rtk dotnet test C:\dev\helworks\helengine-3ds\builder.tests\helengine.3ds.builder.tests.csproj --filter Nintendo3DsLauncherScriptTests
rtk dotnet test C:\dev\helworks\helengine-windows\builder.tests\helengine.windows.builder.tests.csproj --filter WindowsLauncherScriptTests
```

Expected: FAIL.

- [ ] **Step 3: Implement the canonical launchers and minimal root docs**

```powershell
# helengine-switch/scripts/launch_in_emulator.ps1
param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath,
    [Parameter()]
    [string]$EmulatorPath = "C:\dev\helworks\emus\Eden-Windows-v0.2.0-rc2-amd64-msvc-standard\eden-cli.exe"
)

$ResolvedArtifactPath = [System.IO.Path]::GetFullPath($ArtifactPath)
if ([System.IO.Path]::GetExtension($ResolvedArtifactPath) -ine '.nro') {
    [Console]::Error.WriteLine("Expected a .nro artifact at '$ResolvedArtifactPath'.")
    exit 8
}
$EmulatorProcess = Start-Process -FilePath $ResolvedEmulatorPath -ArgumentList @($ResolvedArtifactPath) -WorkingDirectory (Split-Path $ResolvedEmulatorPath) -PassThru
Write-Host ("ARTIFACT: " + $ResolvedArtifactPath)
Write-Host ("PROCESS_ID: " + $EmulatorProcess.Id)

# helengine-3ds/scripts/launch_in_emulator.ps1
param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath,
    [Parameter()]
    [string]$EmulatorPath = "C:\dev\helworks\emus\Lime3DS\lime3ds-qt.exe"
)

$ResolvedArtifactPath = [System.IO.Path]::GetFullPath($ArtifactPath)
if ([System.IO.Path]::GetExtension($ResolvedArtifactPath) -ine '.3dsx') {
    throw "Expected a .3dsx artifact but got '$ResolvedArtifactPath'."
}
$process = Start-Process -FilePath $ResolvedEmulatorPath -ArgumentList @($ResolvedArtifactPath) -PassThru

# helengine-windows/scripts/launch_in_emulator.ps1
param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath
)

$ErrorActionPreference = 'Stop'
$resolvedArtifactPath = [System.IO.Path]::GetFullPath($ArtifactPath)
if (-not (Test-Path -LiteralPath $resolvedArtifactPath -PathType Leaf)) {
    throw "Artifact was not found: $resolvedArtifactPath"
}
if ([System.IO.Path]::GetExtension($resolvedArtifactPath) -ine '.exe') {
    throw "Expected a .exe artifact but got '$resolvedArtifactPath'."
}
$process = Start-Process -FilePath $resolvedArtifactPath -WorkingDirectory (Split-Path $resolvedArtifactPath -Parent) -PassThru
Write-Output ("ARTIFACT=" + $resolvedArtifactPath)
Write-Output ("PROCESS_ID=" + $process.Id)

# helengine-switch/README.md
# Build with -Platform switch and run:
# powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\launch_in_emulator.ps1 `
#   -ArtifactPath ..\helprojs\city\switch-build\helengine_switch.nro

# helengine-3ds/README.md
# Build with -Platform 3ds and run:
# powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\launch_in_emulator.ps1 `
#   -ArtifactPath ..\helprojs\city\3ds-build\helengine_3ds.3dsx

# helengine-windows/README.md
# Build with -Platform windows and run:
# powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\launch_in_emulator.ps1 `
#   -ArtifactPath ..\helprojs\city\windows-build\helengine_windows.exe
```

- [ ] **Step 4: Re-run the launcher audits and one stable neighboring test per repo**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-switch\builder.tests\helengine.switch.builder.tests.csproj --filter "SwitchEmulatorLauncherScriptTests|SwitchPlatformPluginManifestTests"
rtk dotnet test C:\dev\helworks\helengine-3ds\builder.tests\helengine.3ds.builder.tests.csproj --filter "Nintendo3DsLauncherScriptTests|Nintendo3DsBuildWorkspaceTests"
rtk dotnet test C:\dev\helworks\helengine-windows\builder.tests\helengine.windows.builder.tests.csproj --filter "WindowsLauncherScriptTests|WindowsNativeBuildExecutorTests"
```

Expected: PASS.

- [ ] **Step 5: Commit the Switch, 3DS, and Windows cutover**

```powershell
rtk proxy git -C C:\dev\helworks\helengine-switch add README.md docs/Docker.md scripts/launch_in_emulator.ps1 builder.tests/SwitchEmulatorLauncherScriptTests.cs
rtk proxy git -C C:\dev\helworks\helengine-switch rm artifacts/build-and-launch-switch.ps1
rtk proxy git -C C:\dev\helworks\helengine-switch commit -m "feat: standardize switch launcher and readme"

rtk proxy git -C C:\dev\helworks\helengine-3ds add README.md docs/Docker.md scripts/launch_in_emulator.ps1 builder.tests/Nintendo3DsLauncherScriptTests.cs
rtk proxy git -C C:\dev\helworks\helengine-3ds commit -m "feat: standardize 3ds launcher and readme"

rtk proxy git -C C:\dev\helworks\helengine-windows add README.md docs/Docker.md scripts/launch_in_emulator.ps1 builder.tests/WindowsLauncherScriptTests.cs
rtk proxy git -C C:\dev\helworks\helengine-windows commit -m "feat: standardize windows launcher and readme"
```

### Task 5: Final Consistency Sweep Across All Ten Repos

**Files:**
- Modify: all ten root `README.md` files
- Modify: all ten `docs/Docker.md` files
- Modify: all ten `scripts/launch_in_emulator.ps1` files
- Modify: all ten launcher audit test files

- [ ] **Step 1: Run a text audit to confirm every repo exposes the same public entrypoint**

Run:

```powershell
rtk proxy rg -n "launch_in_emulator\\.ps1|ArtifactPath|docs/Docker\\.md" C:\dev\helworks\helengine-wii C:\dev\helworks\helengine-ps2 C:\dev\helworks\helengine-gc C:\dev\helworks\helengine-ds C:\dev\helworks\helengine-psp C:\dev\helworks\helengine-psvita C:\dev\helworks\helengine-switch C:\dev\helworks\helengine-wiiu C:\dev\helworks\helengine-windows C:\dev\helworks\helengine-3ds --glob README.md --glob *.ps1 --glob *.cs
```

Expected: every repo references `scripts/launch_in_emulator.ps1` and `-ArtifactPath`, and no root README points at the old filenames.

- [ ] **Step 2: Remove stale references and align the final README skeleton text**

```markdown
# Helengine Wii Host
Build: `powershell -NoProfile -ExecutionPolicy Bypass -File ..\helengine\artifacts\build-platform.ps1 -Project ..\helprojs\city\project.heproj -Platform wii -Output ..\helprojs\city\wii-build`
Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\launch_in_emulator.ps1 -ArtifactPath ..\helprojs\city\wii-build\game.iso`

# Helengine PS2 Host
Build: `powershell -NoProfile -ExecutionPolicy Bypass -File ..\helengine\artifacts\build-platform.ps1 -Project ..\helprojs\city\project.heproj -Platform ps2 -Output ..\helprojs\city\ps2-build`
Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\launch_in_emulator.ps1 -ArtifactPath ..\helprojs\city\ps2-build\game.iso`

# Helengine GameCube Host
Build: `powershell -NoProfile -ExecutionPolicy Bypass -File ..\helengine\artifacts\build-platform.ps1 -Project ..\helprojs\city\project.heproj -Platform gamecube -Output ..\helprojs\city\gamecube-build`
Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\launch_in_emulator.ps1 -ArtifactPath ..\helprojs\city\gamecube-build\game.gcm`

# Helengine Nintendo DS Host
Build: `powershell -NoProfile -ExecutionPolicy Bypass -File ..\helengine\artifacts\build-platform.ps1 -Project ..\helprojs\city\project.heproj -Platform ds -Output ..\helprojs\city\ds-build`
Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\launch_in_emulator.ps1 -ArtifactPath ..\helprojs\city\ds-build\helengine_ds.nds`

# Helengine PSP Host
Build: `powershell -NoProfile -ExecutionPolicy Bypass -File ..\helengine\artifacts\build-platform.ps1 -Project ..\helprojs\city\project.heproj -Platform psp -Output ..\helprojs\city\psp-build`
Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\launch_in_emulator.ps1 -ArtifactPath ..\helprojs\city\psp-build\PSP\GAME\HELENGINE\EBOOT.PBP`

# Helengine PS Vita Host
Build: `powershell -NoProfile -ExecutionPolicy Bypass -File ..\helengine\artifacts\build-platform.ps1 -Project ..\helprojs\city\project.heproj -Platform psvita -Output ..\helprojs\city\psvita-build`
Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\launch_in_emulator.ps1 -ArtifactPath ..\helprojs\city\psvita-build\helengine_psvita.vpk`

# Helengine Switch Host
Build: `powershell -NoProfile -ExecutionPolicy Bypass -File ..\helengine\artifacts\build-platform.ps1 -Project ..\helprojs\city\project.heproj -Platform switch -Output ..\helprojs\city\switch-build`
Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\launch_in_emulator.ps1 -ArtifactPath ..\helprojs\city\switch-build\helengine_switch.nro`

# Helengine Wii U Host
Build: `powershell -NoProfile -ExecutionPolicy Bypass -File ..\helengine\artifacts\build-platform.ps1 -Project ..\helprojs\city\project.heproj -Platform wiiu -Output ..\helprojs\city\wiiu-build`
Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\launch_in_emulator.ps1 -ArtifactPath ..\helprojs\city\wiiu-build\helengine_wiiu.rpx`

# Helengine Windows Host
Build: `powershell -NoProfile -ExecutionPolicy Bypass -File ..\helengine\artifacts\build-platform.ps1 -Project ..\helprojs\city\project.heproj -Platform windows -Output ..\helprojs\city\windows-build`
Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\launch_in_emulator.ps1 -ArtifactPath ..\helprojs\city\windows-build\helengine_windows.exe`

# Helengine Nintendo 3DS Host
Build: `powershell -NoProfile -ExecutionPolicy Bypass -File ..\helengine\artifacts\build-platform.ps1 -Project ..\helprojs\city\project.heproj -Platform 3ds -Output ..\helprojs\city\3ds-build`
Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\launch_in_emulator.ps1 -ArtifactPath ..\helprojs\city\3ds-build\helengine_3ds.3dsx`
```
```

- [ ] **Step 3: Run the full launcher-focused verification pass**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-wii\builder.tests\helengine.wii.builder.tests.csproj --filter WiiDolphinLauncherScriptTests
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter Ps2Pcsx2LauncherScriptTests
rtk dotnet test C:\dev\helworks\helengine-gc\builder.tests\helengine.gamecube.builder.tests.csproj --filter GameCubeDolphinLauncherScriptTests
rtk dotnet test C:\dev\helworks\helengine-ds\builder.tests\helengine.ds.builder.tests.csproj --filter NintendoDsMelonDsLauncherScriptTests
rtk dotnet test C:\dev\helworks\helengine-psp\builder.tests\helengine.psp.builder.tests.csproj --filter PspPpssppLauncherScriptTests
rtk dotnet test C:\dev\helworks\helengine-psvita\builder.tests\helengine.psvita.builder.tests.csproj --filter Vita3KLauncherScriptAuditTests
rtk dotnet test C:\dev\helworks\helengine-switch\builder.tests\helengine.switch.builder.tests.csproj --filter SwitchEmulatorLauncherScriptTests
rtk dotnet test C:\dev\helworks\helengine-wiiu\builder.tests\helengine.wiiu.builder.tests.csproj --filter WiiUCemuLauncherScriptTests
rtk dotnet test C:\dev\helworks\helengine-windows\builder.tests\helengine.windows.builder.tests.csproj --filter WindowsLauncherScriptTests
rtk dotnet test C:\dev\helworks\helengine-3ds\builder.tests\helengine.3ds.builder.tests.csproj --filter Nintendo3DsLauncherScriptTests
```

Expected: PASS across all ten repos.

- [ ] **Step 4: Smoke-test the generated docs and stale filename removal**

Run:

```powershell
rtk proxy rg -n "launch_wii_iso_in_dolphin|launch_ps2_iso_in_pcsx2|launch_gamecube_image_in_dolphin|launch-melonds-rom|launch-vita3k|build-and-launch-switch|launch_wiiu_rpx_in_cemu|IsoPath|ImagePath|RpxPath|VpkPath|RomPath" C:\dev\helworks\helengine-wii C:\dev\helworks\helengine-ps2 C:\dev\helworks\helengine-gc C:\dev\helworks\helengine-ds C:\dev\helworks\helengine-psp C:\dev\helworks\helengine-psvita C:\dev\helworks\helengine-switch C:\dev\helworks\helengine-wiiu --glob README.md --glob *.ps1 --glob *.cs
```

Expected: no matches in active scripts or root READMEs. Any remaining matches must live only in old plan/spec history under `docs/superpowers/`.

- [ ] **Step 5: Commit the final consistency sweep**

```powershell
rtk proxy git -C C:\dev\helworks\helengine-wii status --short
rtk proxy git -C C:\dev\helworks\helengine-ps2 status --short
rtk proxy git -C C:\dev\helworks\helengine-gc status --short
rtk proxy git -C C:\dev\helworks\helengine-ds status --short
rtk proxy git -C C:\dev\helworks\helengine-psp status --short
rtk proxy git -C C:\dev\helworks\helengine-psvita status --short
rtk proxy git -C C:\dev\helworks\helengine-switch status --short
rtk proxy git -C C:\dev\helworks\helengine-wiiu status --short
rtk proxy git -C C:\dev\helworks\helengine-windows status --short
rtk proxy git -C C:\dev\helworks\helengine-3ds status --short
```

Then commit any remaining README or launcher follow-up adjustments inside the affected repo, one repo at a time.
