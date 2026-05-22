# Windows Demo Disc Boot Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the city Windows export boot through `GeneratedBootScene`, then `DemoDiscMainMenu`, then the full demo-disc scene lineup instead of the temporary physics scene.

**Architecture:** The engine-side startup routing already knows how to inject `GeneratedBootScene` for Windows when `DemoDiscMainMenu` is selected. The implementation therefore stays small: add tests that pin the expected Windows demo-disc startup flow, then update the city Windows build configuration so it selects the same demo-disc scene set that the menu exposes.

**Tech Stack:** C#/.NET 9, `helengine.editor`, xUnit, city `user_settings/build_config.json`, RTK, Windows export pipeline.

---

## File Structure Map

### Engine files to modify

- Modify: `engine/helengine.editor.tests/managers/project/EditorBuildQueueItemFactoryTests.cs`
  - Add one focused queue-item test proving Windows demo-disc builds stage `GeneratedBootScene` first and keep `DemoDiscMainMenu` plus the demo scenes after it.
- Modify: `engine/helengine.editor.tests/CityRenderingSceneAuthoringTests.cs`
  - Add one city-source test proving the Windows build config now selects the demo-disc menu plus the authored demo scenes instead of the temporary physics stack test.

### City files to modify

- Modify: `C:/dev/helprojs/city/user_settings/build_config.json`
  - Replace the Windows selected scene list and scene order with the demo-disc lineup: `DemoDiscMainMenu`, `cube_test`, `scaled_cube`, `colored_cube_grid`, `textured_cube_grid`, `axis_test`, `axis_test2`, `directional_shadow_plaza`, `spotlight_street_slice`.

## Task 1: Pin the Windows demo-disc startup flow with failing tests

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorBuildQueueItemFactoryTests.cs`
- Modify: `engine/helengine.editor.tests/CityRenderingSceneAuthoringTests.cs`

- [ ] **Step 1: Add the failing queue-item test for Windows demo-disc startup order**

```csharp
/// <summary>
/// Ensures Windows demo-disc builds boot through the generated boot scene before the main menu and the authored demo scenes.
/// </summary>
[Fact]
public void Create_WhenWindowsBuildTargetsDemoDiscMainMenu_UsesGeneratedBootThenMainMenuAndDemoScenes() {
    WriteScene("Scenes/DemoDiscMainMenu.helen");
    WriteScene("Scenes/rendering/cube_test.helen");
    WriteScene("Scenes/rendering/scaled_cube.helen");
    WriteScene("Scenes/rendering/colored_cube_grid.helen");
    WriteScene("Scenes/rendering/textured_cube_grid.helen");
    WriteScene("Scenes/rendering/axis_test.helen");
    WriteScene("Scenes/rendering/axis_test2.helen");
    WriteScene("Scenes/rendering/directional_shadow_plaza.helen");
    WriteScene("Scenes/rendering/spotlight_street_slice.helen");

    EditorProjectSceneCatalogService sceneCatalogService = new EditorProjectSceneCatalogService(TempProjectRootPath);
    EditorBuildQueueItemFactory factory = new EditorBuildQueueItemFactory(sceneCatalogService);
    EditorBuildPlatformConfigDocument platformConfig = new EditorBuildPlatformConfigDocument {
        PlatformId = "windows",
        SelectedSceneIds = [
            "DemoDiscMainMenu",
            "cube_test",
            "scaled_cube",
            "colored_cube_grid",
            "textured_cube_grid",
            "axis_test",
            "axis_test2",
            "directional_shadow_plaza",
            "spotlight_street_slice"
        ]
    };

    EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(CreateSelectionModel());
    EditorBuildQueueItemDocument queueItem = factory.Create(platformConfig, selectionModel, Path.Combine(TempProjectRootPath, "Build"));

    Assert.Equal(new[] {
        "GeneratedBootScene",
        "DemoDiscMainMenu",
        "cube_test",
        "scaled_cube",
        "colored_cube_grid",
        "textured_cube_grid",
        "axis_test",
        "axis_test2",
        "directional_shadow_plaza",
        "spotlight_street_slice"
    }, queueItem.SelectedSceneIds);
}
```

- [ ] **Step 2: Add the failing city build-config source test**

```csharp
/// <summary>
/// Ensures the city Windows build configuration targets the demo-disc boot flow instead of the temporary physics stack test scene.
/// </summary>
[Fact]
public void ReadCityBuildConfigSource_WindowsPlatformTargetsDemoDiscMenuAndDemoScenes() {
    string buildConfigPath = Path.Combine(CityProjectRootPath, "user_settings", "build_config.json");
    string json = File.ReadAllText(buildConfigPath);
    using JsonDocument document = JsonDocument.Parse(json);

    JsonElement windowsPlatform = document
        .RootElement
        .GetProperty("platforms")
        .EnumerateArray()
        .First(platform => string.Equals(platform.GetProperty("platformId").GetString(), "windows", StringComparison.Ordinal));
    string[] selectedSceneIds = windowsPlatform
        .GetProperty("selectedSceneIds")
        .EnumerateArray()
        .Select(element => element.GetString() ?? string.Empty)
        .ToArray();

    Assert.Equal(new[] {
        "DemoDiscMainMenu",
        "cube_test",
        "scaled_cube",
        "colored_cube_grid",
        "textured_cube_grid",
        "axis_test",
        "axis_test2",
        "directional_shadow_plaza",
        "spotlight_street_slice"
    }, selectedSceneIds);
}
```

- [ ] **Step 3: Run the focused tests to verify RED**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~Create_WhenWindowsBuildTargetsDemoDiscMainMenu_UsesGeneratedBootThenMainMenuAndDemoScenes|FullyQualifiedName~ReadCityBuildConfigSource_WindowsPlatformTargetsDemoDiscMenuAndDemoScenes' 2>&1 | Select-Object -Last 160 | Out-String -Width 240 | Write-Output"
```

Expected:

```text
FAIL Create_WhenWindowsBuildTargetsDemoDiscMainMenu_UsesGeneratedBootThenMainMenuAndDemoScenes
FAIL ReadCityBuildConfigSource_WindowsPlatformTargetsDemoDiscMenuAndDemoScenes
```

- [ ] **Step 4: Commit the failing-test checkpoint**

```bash
rtk git add engine/helengine.editor.tests/managers/project/EditorBuildQueueItemFactoryTests.cs engine/helengine.editor.tests/CityRenderingSceneAuthoringTests.cs
rtk git commit -m "test: pin windows demo disc boot flow"
```

## Task 2: Align the city Windows build selection with the demo disc

**Files:**
- Modify: `C:/dev/helprojs/city/user_settings/build_config.json`

- [ ] **Step 1: Replace the Windows selected scene list with the demo-disc lineup**

Update the Windows platform block in `build_config.json` from the temporary physics scene:

```json
"selectedSceneIds": [
  "test_scene_dynamic_stack_boxes"
]
```

to the demo-disc scene list:

```json
"selectedSceneIds": [
  "DemoDiscMainMenu",
  "cube_test",
  "scaled_cube",
  "colored_cube_grid",
  "textured_cube_grid",
  "axis_test",
  "axis_test2",
  "directional_shadow_plaza",
  "spotlight_street_slice"
]
```

- [ ] **Step 2: Replace the Windows scene order with the demo-disc order**

Update the Windows `sceneOrders` block to keep the menu first and preserve the authored demo-scene order:

```json
"sceneOrders": [
  { "sceneId": "DemoDiscMainMenu", "orderNumber": 1 },
  { "sceneId": "cube_test", "orderNumber": 2 },
  { "sceneId": "scaled_cube", "orderNumber": 3 },
  { "sceneId": "colored_cube_grid", "orderNumber": 4 },
  { "sceneId": "textured_cube_grid", "orderNumber": 5 },
  { "sceneId": "axis_test", "orderNumber": 6 },
  { "sceneId": "axis_test2", "orderNumber": 7 },
  { "sceneId": "directional_shadow_plaza", "orderNumber": 8 },
  { "sceneId": "spotlight_street_slice", "orderNumber": 9 }
]
```

- [ ] **Step 3: Re-run the focused tests to verify GREEN**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~Create_WhenWindowsBuildTargetsDemoDiscMainMenu_UsesGeneratedBootThenMainMenuAndDemoScenes|FullyQualifiedName~ReadCityBuildConfigSource_WindowsPlatformTargetsDemoDiscMenuAndDemoScenes' 2>&1 | Select-Object -Last 160 | Out-String -Width 240 | Write-Output"
```

Expected:

```text
PASS Create_WhenWindowsBuildTargetsDemoDiscMainMenu_UsesGeneratedBootThenMainMenuAndDemoScenes
PASS ReadCityBuildConfigSource_WindowsPlatformTargetsDemoDiscMenuAndDemoScenes
```

- [ ] **Step 4: Commit the Windows build-config alignment in both repos**

```bash
rtk git add engine/helengine.editor.tests/managers/project/EditorBuildQueueItemFactoryTests.cs engine/helengine.editor.tests/CityRenderingSceneAuthoringTests.cs
rtk git commit -m "feat: pin windows demo disc boot flow"
rtk git -C "C:\dev\helprojs\city" add user_settings/build_config.json
rtk git -C "C:\dev\helprojs\city" commit -m "feat: align windows build with demo disc startup"
```

## Task 3: Validate the Windows export boots through the demo-disc flow

**Files:**
- Modify: `C:/dev/helprojs/city/user_settings/build_config.json`

- [ ] **Step 1: Build the Windows export using the aligned city configuration**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "Get-Process helengine_windows -ErrorAction SilentlyContinue | Stop-Process -Force; dotnet run --project 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\helengine.editor.app.csproj' -- --project 'C:\dev\helprojs\city\project.heproj' --build windows --output 'C:\dev\helprojs\output\windows-city-demo-disc' 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

Expected:

```text
Build completed for platform 'windows': C:\dev\helprojs\output\windows-city-demo-disc
```

- [ ] **Step 2: Launch the exported Windows executable**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "`$outputRoot = 'C:\dev\helprojs\output\windows-city-demo-disc'; `$exe = Get-ChildItem -Path `$outputRoot -Recurse -Filter '*.exe' | Where-Object { `$_.Name -like '*helengine*' -or `$_.Name -like '*city*' } | Select-Object -First 1; if (`$null -eq `$exe) { throw 'No exported executable found.' }; Get-Process helengine_windows -ErrorAction SilentlyContinue | Stop-Process -Force; `$process = Start-Process -FilePath `$exe.FullName -WorkingDirectory `$exe.DirectoryName -PassThru; Write-Output ('EXE=' + `$exe.FullName); Write-Output ('PID=' + `$process.Id)"
```

Expected:

```text
EXE=C:\dev\helprojs\output\windows-city-demo-disc\helengine_windows.exe
PID=<number>
```

- [ ] **Step 3: Confirm the runtime reaches the demo-disc main menu through boot**

Manual verification:

```text
Expected on launch:
1. The exported app no longer opens directly into the temporary stack-box physics scene.
2. Startup passes through the generated boot scene.
3. The visible destination becomes the demo-disc main menu.
4. Menu navigation exposes the authored demo scene list.
```

- [ ] **Step 4: Commit any remaining follow-up only if validation required edits**

```bash
rtk git status --short
rtk git -C "C:\dev\helprojs\city" status --short
```
