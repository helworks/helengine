# Experimental Vulkan Backend Gate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve the managed Vulkan renderer for future development while requiring explicit experimental opt-in and keeping DirectX 11 as the editor and packaged-Windows default.

**Architecture:** The editor host remains the only managed renderer-selection composition root. Its existing `HELENGINE_RENDER_BACKEND` selector will be combined with a new `HELENGINE_ENABLE_EXPERIMENTAL_VULKAN=1` gate; packaged Windows remains governed by its existing `HELENGINE_WINDOWS_RENDER_BACKEND=DirectX11` CMake default and continues rejecting the unimplemented native Vulkan target. Texture-region uploads remain a normal renderer capability because the CPU `SoftwareModelComponent` needs them on DirectX 11.

**Tech Stack:** C# 13 / .NET 9, WinForms editor host, xUnit source-contract tests, CMake native Windows player

---

## File map

- Modify `helengine.ui/helengine.editor.app/MainForm.cs`: declare and enforce the experimental Vulkan environment gate at the editor renderer composition root.
- Modify `engine/helengine.editor.tests/EditorAppShaderBackendRegistrySourceTests.cs`: add source-contract coverage proving Vulkan requires both environment variables and the unconditional Vulkan-disable assignment is gone.
- Verify only `CMakeLists.txt` in `helengine-windows-texture-region`: confirm the packaged player still defaults to DirectX 11 and fails fast for Vulkan. Do not modify this repository for this task.

### Task 1: Gate managed editor Vulkan selection explicitly

**Files:**
- Modify: `engine/helengine.editor.tests/EditorAppShaderBackendRegistrySourceTests.cs`
- Modify: `helengine.ui/helengine.editor.app/MainForm.cs`

- [ ] **Step 1: Write the failing source-contract test**

Add this test to `EditorAppShaderBackendRegistrySourceTests`:

```csharp
/// <summary>
/// Ensures Vulkan cannot become the editor renderer unless the experimental opt-in is explicitly enabled.
/// </summary>
[Fact]
public void Editor_app_host_requires_explicit_experimental_opt_in_for_vulkan() {
    string sourcePath = Path.Combine(
        ResolveRepositoryRootPath(),
        "helengine.ui",
        "helengine.editor.app",
        "MainForm.cs");

    string source = File.ReadAllText(sourcePath);

    Assert.Contains(
        "const string ExperimentalVulkanEnvironmentVariable = \"HELENGINE_ENABLE_EXPERIMENTAL_VULKAN\";",
        source,
        StringComparison.Ordinal);
    Assert.Contains(
        "Environment.GetEnvironmentVariable(ExperimentalVulkanEnvironmentVariable, EnvironmentVariableTarget.Process)",
        source,
        StringComparison.Ordinal);
    Assert.Contains(
        "string.Equals(experimentalVulkan, \"1\", StringComparison.Ordinal)",
        source,
        StringComparison.Ordinal);
    Assert.Contains("if (!experimentalVulkanEnabled)", source, StringComparison.Ordinal);
    Assert.Contains(
        "Vulkan rendering requires HELENGINE_ENABLE_EXPERIMENTAL_VULKAN=1.",
        source,
        StringComparison.Ordinal);
    Assert.DoesNotContain("useVulkan = false;", source, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorAppShaderBackendRegistrySourceTests.Editor_app_host_requires_explicit_experimental_opt_in_for_vulkan" -v:minimal
```

Expected: FAIL because `MainForm.cs` does not yet declare `HELENGINE_ENABLE_EXPERIMENTAL_VULKAN` and still contains `useVulkan = false;`.

- [ ] **Step 3: Implement the minimal editor-host gate**

In `MainForm.cs`, add the constant beside `RendererBackendEnvironmentVariable`:

```csharp
/// <summary>
/// Environment variable that explicitly opts into the experimental Vulkan renderer.
/// </summary>
const string ExperimentalVulkanEnvironmentVariable = "HELENGINE_ENABLE_EXPERIMENTAL_VULKAN";
```

Replace the current backend-selection block, including the unconditional `useVulkan = false;`, with:

```csharp
string rendererBackend = Environment.GetEnvironmentVariable(RendererBackendEnvironmentVariable, EnvironmentVariableTarget.Process);
string experimentalVulkan = Environment.GetEnvironmentVariable(ExperimentalVulkanEnvironmentVariable, EnvironmentVariableTarget.Process);
bool experimentalVulkanEnabled = string.Equals(experimentalVulkan, "1", StringComparison.Ordinal);
bool useVulkan = false;
if (!string.IsNullOrWhiteSpace(rendererBackend)) {
    rendererBackend = rendererBackend.Trim();
    if (string.Equals(rendererBackend, "vulkan", StringComparison.OrdinalIgnoreCase)) {
        if (!experimentalVulkanEnabled) {
            throw new InvalidOperationException("Vulkan rendering requires HELENGINE_ENABLE_EXPERIMENTAL_VULKAN=1.");
        }

        useVulkan = true;
    } else if (!string.Equals(rendererBackend, "directx11", StringComparison.OrdinalIgnoreCase)) {
        throw new InvalidOperationException($"Unsupported renderer backend '{rendererBackend}'. Use 'vulkan' or 'directx11'.");
    }
}
```

Do not change renderer construction, shader backend registration, texture-region APIs, Vulkan renderer internals, or any packaged-player source.

- [ ] **Step 4: Run the focused tests and verify GREEN**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorAppShaderBackendRegistrySourceTests" -v:minimal
```

Expected: both tests PASS with zero failures.

- [ ] **Step 5: Build the editor host**

Run:

```powershell
rtk dotnet build helengine.ui\helengine.editor.app\helengine.editor.app.csproj --no-restore -v:minimal
```

Expected: build exits 0 with no compiler errors.

- [ ] **Step 6: Commit the implementation**

```powershell
rtk git add -- engine/helengine.editor.tests/EditorAppShaderBackendRegistrySourceTests.cs helengine.ui/helengine.editor.app/MainForm.cs
rtk git commit -m "Gate experimental Vulkan editor rendering"
```

### Task 2: Verify the packaged Windows boundary remains DirectX 11

**Files:**
- Verify: `CMakeLists.txt` in `C:\dev\helprojs\.worktrees\helengine-windows-texture-region`
- Verify: the existing successful DemoDisc package at `C:\dev\helprojs\demodisc-windows-package-cooked-hash-fix-20260831\helengine_windows.exe`

- [ ] **Step 1: Verify the native backend default and guard**

Run from the Windows worktree:

```powershell
rtk rg -n "HELENGINE_WINDOWS_RENDER_BACKEND|Only the DirectX11 bootstrap scaffold exists today" CMakeLists.txt
```

Expected: `HELENGINE_WINDOWS_RENDER_BACKEND` defaults to `DirectX11`, accepts `DirectX11`/`Vulkan` as future-facing cache values, and currently fails configuration for any non-DirectX11 selection.

- [ ] **Step 2: Verify the successful package artifact still exists**

Run:

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath 'C:\dev\helprojs\demodisc-windows-package-cooked-hash-fix-20260831\helengine_windows.exe'
```

Expected SHA-256:

```text
3746FF74257BD9F295DF4CE9867C57FD67C76FC0D24F3B0963BF8C9FA157EEAB
```

- [ ] **Step 3: Confirm no packaged-player files changed**

Run from the Windows worktree:

```powershell
rtk git status --short
```

Expected: no tracked modifications from this task. The pre-existing untracked `.validation/c7d0f54` directory may remain and must not be committed.

### Task 3: Final regression verification

**Files:**
- Verify: `engine/helengine.editor.tests/helengine.editor.tests.csproj`
- Verify: `helengine.ui/helengine.editor.app/helengine.editor.app.csproj`

- [ ] **Step 1: Run all source-contract tests for the editor app host**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorAppShaderBackendRegistrySourceTests" -v:minimal
```

Expected: all matching tests PASS with zero failures.

- [ ] **Step 2: Rebuild the editor host from the committed tree**

```powershell
rtk dotnet build helengine.ui\helengine.editor.app\helengine.editor.app.csproj --no-restore -v:minimal
```

Expected: exit code 0 with no compiler errors.

- [ ] **Step 3: Audit the final diff**

```powershell
rtk git show --stat --oneline HEAD
rtk git diff HEAD^ -- engine/helengine.editor.tests/EditorAppShaderBackendRegistrySourceTests.cs helengine.ui/helengine.editor.app/MainForm.cs
```

Expected: the commit contains only the new source-contract test and the editor composition-root gate; it does not alter Vulkan internals, texture uploads, generated engine code, or packaged Windows code.
