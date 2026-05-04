# Generated Native Runtime Startup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move runtime startup and code-module residency data out of final JSON files and into generated C++ that is compiled into the native player.

**Architecture:** The editor build graph remains the source of truth for selected scene order and code-module residency, but instead of writing runtime JSON into the final package it emits small native manifest source files into the generated-core tree. The native Windows player compiles those generated files alongside the existing unity translation unit and reads startup/code-module data from compiled symbols. This keeps the final output native-only while preserving the current editor-owned build flow.

**Tech Stack:** C# / .NET 9, xUnit, generated-core C++ output, C++20, CMake, `csharpcodegen`, `helengine.editor`, `helengine-windows`.

---

## File Structure

### Editor build graph and generator

- Add: `engine/helengine.editor/managers/project/EditorRuntimeManifestNativeWriter.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`

### Native Windows bootstrap

- Modify: `helengine-windows/src/platform/windows/win32/win32_application.cpp`
- Modify: `helengine-windows/src/platform/windows/win32/win32_application.hpp`

### Build/package verification

- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

---

### Task 1: Generate native runtime manifest source in the generated-core tree

**Files:**
- Add: `engine/helengine.editor/managers/project/EditorRuntimeManifestNativeWriter.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`

- [ ] **Step 1: Write the failing generator test**

```csharp
[Fact]
public void WriteRuntimeManifestNativeFiles_WritesStartupAndCodeModuleCpp() {
    string generatedCoreRootPath = Path.Combine(Path.GetTempPath(), "helengine-runtime-native-test", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(generatedCoreRootPath);

    PlatformBuildManifest manifest = new(
        3,
        "city",
        "1.0.0",
        "1.0.0-engine",
        "NewScene.helen",
        [
            new PlatformBuildScene(
                "NewScene.helen",
                "main",
                "cooked/scenes/main.hasset",
                [],
                [
                    new KeyValuePair<string, string>("cooked-relative-path", "cooked/scenes/main.hasset")
                ])
        ],
        [],
        [],
        [
            new PlatformBuildCodeModule(
                "gameplay",
                "windows",
                RuntimeCodeModuleLoadState.ResidentAtStartup,
                [])
        ],
        [],
        new PlatformContainerWritePlan(string.Empty, []));

    EditorRuntimeManifestNativeWriter.Write(manifest, generatedCoreRootPath);

    string startupHeaderPath = Path.Combine(generatedCoreRootPath, "runtime", "runtime_startup_manifest.hpp");
    string startupSourcePath = Path.Combine(generatedCoreRootPath, "runtime", "runtime_startup_manifest.cpp");
    string codeModuleHeaderPath = Path.Combine(generatedCoreRootPath, "runtime", "runtime_code_module_manifest.hpp");
    string codeModuleSourcePath = Path.Combine(generatedCoreRootPath, "runtime", "runtime_code_module_manifest.cpp");

    Assert.True(File.Exists(startupHeaderPath));
    Assert.True(File.Exists(startupSourcePath));
    Assert.True(File.Exists(codeModuleHeaderPath));
    Assert.True(File.Exists(codeModuleSourcePath));
    Assert.Contains("cooked/scenes/main.hasset", File.ReadAllText(startupSourcePath));
    Assert.Contains("gameplay", File.ReadAllText(codeModuleSourcePath));
}
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests" -v minimal
```

Expected: fail because the editor does not yet emit native runtime manifest source files.

- [ ] **Step 3: Implement the native writer and hook it into generated-core regeneration**

```csharp
static void WriteRuntimeManifestNativeFiles(PlatformBuildManifest cookedManifest, string generatedCoreRootPath) {
    string runtimeRootPath = Path.Combine(generatedCoreRootPath, "runtime");
    Directory.CreateDirectory(runtimeRootPath);

    File.WriteAllText(
        Path.Combine(runtimeRootPath, "runtime_startup_manifest.hpp"),
        BuildRuntimeStartupHeader());
    File.WriteAllText(
        Path.Combine(runtimeRootPath, "runtime_startup_manifest.cpp"),
        BuildRuntimeStartupSource(cookedManifest));
    File.WriteAllText(
        Path.Combine(runtimeRootPath, "runtime_code_module_manifest.hpp"),
        BuildRuntimeCodeModuleHeader());
    File.WriteAllText(
        Path.Combine(runtimeRootPath, "runtime_code_module_manifest.cpp"),
        BuildRuntimeCodeModuleSource(cookedManifest.CodeModules));
}
```

- [ ] **Step 4: Run the focused test and verify it passes**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests" -v minimal
```

Expected: PASS, with the generated runtime C++ files present under the generated-core root.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/project/EditorRuntimeManifestNativeWriter.cs engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs
git commit -m "feat: generate native runtime manifest source"
```

### Task 2: Consume generated runtime symbols from the native Windows bootstrap

**Files:**
- Modify: `helengine-windows/src/platform/windows/win32/win32_application.cpp`
- Modify: `helengine-windows/src/platform/windows/win32/win32_application.hpp`

- [ ] **Step 1: Write the failing native bootstrap expectation**

```cpp
#include "runtime/runtime_startup_manifest.hpp"
#include "runtime/runtime_code_module_manifest.hpp"

TEST(RuntimeStartup, ExposesCookedStartupScenePath) {
    ASSERT_STREQ("cooked/scenes/main.hasset", he_runtime_startup_scene_path());
}
```

- [ ] **Step 2: Run the native build and verify it fails before the generated include is wired up**

Run:

```bash
& 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.exe' --build windows --project 'C:\dev\helprojs\city\project.heproj' --output 'C:\dev\helprojs\output\windows'
```

Expected: fail because the native bootstrap still depends on JSON startup data or has no generated runtime symbol to call.

- [ ] **Step 3: Update the Windows bootstrap to use the generated runtime functions**

```cpp
#include "runtime/runtime_startup_manifest.hpp"

std::string startupScenePath = he_runtime_startup_scene_path();
```

Use the generated code-module table in the same bootstrap path, instead of reading `runtime-startup.json` or `runtime-code-modules.json` at runtime.

- [ ] **Step 4: Run the native build and verify it passes**

Run:

```bash
& 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.exe' --build windows --project 'C:\dev\helprojs\city\project.heproj' --output 'C:\dev\helprojs\output\windows'
```

Expected: PASS, with the compiled player taking its startup scene from generated C++.

- [ ] **Step 5: Commit**

```bash
git add helengine-windows/src/platform/windows/win32/win32_application.cpp helengine-windows/src/platform/windows/win32/win32_application.hpp
git commit -m "feat: consume generated runtime startup data"
```

### Task 3: Remove JSON from the final package root and verify the cooked tree

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Write the failing package-root assertion**

```csharp
[Fact]
public void Build_output_root_contains_no_json_files() {
    string[] jsonFiles = Directory.GetFiles(OutputRootPath, "*.json", SearchOption.AllDirectories);
    Assert.Empty(jsonFiles);
}
```

- [ ] **Step 2: Run the city build and verify it still emits JSON**

Run:

```bash
& 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.exe' --build windows --project 'C:\dev\helprojs\city\project.heproj' --output 'C:\dev\helprojs\output\windows'
```

Expected: fail the new assertion because the editor still writes runtime JSON into the final package root.

- [ ] **Step 3: Stop writing runtime JSON into the final package root**

```csharp
// Replace the two JSON writes with the native runtime manifest writer.
EditorRuntimeManifestNativeWriter.Write(cookedManifest, workspace.GeneratedCoreRootPath);
```

Keep any build-only diagnostic JSON out of the final output root.

- [ ] **Step 4: Run the city build and verify the final package is JSON-free**

Run:

```bash
& 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.exe' --build windows --project 'C:\dev\helprojs\city\project.heproj' --output 'C:\dev\helprojs\output\windows'
```

Expected:
- `C:\dev\helprojs\output\windows\cooked\scenes\main.hasset` exists
- `C:\dev\helprojs\output\windows\cooked\imported\Sponza\sponza.hasset` exists
- `Get-ChildItem C:\dev\helprojs\output\windows -Recurse -Filter *.json` returns no files

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
git commit -m "feat: remove runtime json from final package"
```
