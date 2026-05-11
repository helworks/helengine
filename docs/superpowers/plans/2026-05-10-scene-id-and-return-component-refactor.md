# Scene Id And Return Component Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace path-based scene loading with generic scene ids derived from scene asset names, remove scene-path logic from menu gameplay code, and shrink `DemoDiscReturnToMenuComponent` to temporary direct input plus scene-id loading.

**Architecture:** Shared engine/editor code will own scene-id derivation and editor/runtime scene-id resolution. Runtime scene ids will remain the public lookup key in `RuntimeSceneCatalog`, while editor-mode menu loads will use an injected scene-id-to-authored-path resolver. City gameplay code will only reference scene ids, and desktop keyboard checks will be explicitly gated out of non-desktop builds.

**Tech Stack:** C#/.NET 9, xUnit, helengine core/editor/build pipeline, city gameplay module, generated native C++ verification through the Windows export flow.

---

## File Map

**Shared engine/editor files**

- Create: `engine/helengine.core/content/SceneIdUtility.cs`
  - Generic helper that derives a stable scene id from a scene asset path or file name.
- Create: `engine/helengine.core/content/ISceneIdPathResolver.cs`
  - Generic contract used by core gameplay/menu code to resolve an authored scene path from a scene id when running in editor mode.
- Modify: `engine/helengine.core/CoreInitializationOptions.cs`
  - Inject the optional editor-mode `ISceneIdPathResolver`.
- Modify: `engine/helengine.core/components/2d/menu/MenuComponent.cs`
  - Treat `LoadScene` payloads as scene ids and remove internal scene-path fallback logic.
- Modify: `engine/helengine.editor/managers/project/EditorProjectSceneCatalogService.cs`
  - Enumerate scene ids by file name without extension and resolve a unique authored scene path from a scene id.
- Modify: `engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs`
  - Populate `PlatformBuildScene.SceneId` from the generic scene-id utility instead of project-relative paths.
- Modify: `engine/helengine.editor/managers/project/EditorRuntimeNativeManifestWriter.cs`
  - Preserve the new scene ids in the runtime manifest output.

**Engine/editor tests**

- Create: `engine/helengine.editor.tests/managers/project/EditorProjectSceneCatalogServiceTests.cs`
  - Direct coverage for scene-id derivation and scene-id-to-path resolution.
- Modify: `engine/helengine.editor.tests/managers/project/EditorBuildQueueItemFactoryTests.cs`
  - Expect selected scene ids to use file-name-derived ids.
- Modify: `engine/helengine.editor.tests/managers/project/EditorRuntimeNativeManifestWriterTests.cs`
  - Expect runtime scene manifest ids to be concise scene ids.
- Modify: `engine/helengine.editor.tests/testing/TestMenuDefinitionProvider.cs`
  - Emit scene ids rather than `.helen` paths in menu actions.
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
  - Exercise menu scene loads using scene ids and editor/runtime resolver setup.
- Modify: `engine/helengine.editor.tests/managers/preview/CameraPreviewSourceTests.cs`
  - Keep baked menu preview tests aligned with scene-id payloads.
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
  - Update the generated Windows translation regression once the authored component is simplified and keyboard code becomes desktop-gated.

**Tooling and city files**

- Modify: `tools/demo-disc-scene-writer/DemoDiscSceneWriter.cs`
  - Stop generating path-based `LoadScene` payloads and path-based selected scene ids in build config.
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`
  - Verify generated source/build config now use scene ids.
- Modify: `C:/dev/helprojs/city/assets/codebase/menu/DemoDiscSceneCatalog.cs`
  - Use scene ids rather than authored scene paths.
- Modify: `C:/dev/helprojs/city/assets/codebase/menu/DemoDiscReturnToMenuComponent.cs`
  - Remove scene-path logic and gate keyboard usage to desktop only.

---

### Task 1: Add Generic Scene Id Derivation And Editor Scene Lookup

**Files:**
- Create: `engine/helengine.core/content/SceneIdUtility.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorProjectSceneCatalogServiceTests.cs`
- Modify: `engine/helengine.editor/managers/project/EditorProjectSceneCatalogService.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorBuildQueueItemFactoryTests.cs`

- [ ] **Step 1: Write the failing scene-id derivation tests**

```csharp
[Fact]
public void GetSceneIds_WhenScenesExist_ReturnsFileNamesWithoutExtensions() {
    WriteScene("Scenes/DemoDiscMainMenu.helen");
    WriteScene("Scenes/rendering/cube_test.helen");

    EditorProjectSceneCatalogService service = new EditorProjectSceneCatalogService(TempProjectRootPath);

    Assert.Equal(new[] { "DemoDiscMainMenu", "cube_test" }, service.GetSceneIds());
}

[Fact]
public void ResolveScenePath_WhenSceneIdMatchesExactlyOneScene_ReturnsProjectRelativePath() {
    WriteScene("Scenes/DemoDiscMainMenu.helen");

    EditorProjectSceneCatalogService service = new EditorProjectSceneCatalogService(TempProjectRootPath);

    Assert.Equal("Scenes/DemoDiscMainMenu.helen", service.ResolveScenePath("DemoDiscMainMenu"));
}
```

- [ ] **Step 2: Run the new tests to verify they fail**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorProjectSceneCatalogServiceTests
```

Expected: FAIL because `EditorProjectSceneCatalogService` still returns project-relative path ids and has no `ResolveScenePath(string sceneId)` API.

- [ ] **Step 3: Add the generic scene-id helper**

```csharp
namespace helengine {
    /// <summary>
    /// Builds stable scene identifiers from authored scene asset names.
    /// </summary>
    public static class SceneIdUtility {
        /// <summary>
        /// Resolves one stable scene id from a scene file path or file name.
        /// </summary>
        /// <param name="scenePath">Scene file path or file name.</param>
        /// <returns>Stable scene id derived from the scene file name.</returns>
        public static string FromPath(string scenePath) {
            if (string.IsNullOrWhiteSpace(scenePath)) {
                throw new ArgumentException("Scene path must be provided.", nameof(scenePath));
            }

            return Path.GetFileNameWithoutExtension(scenePath);
        }
    }
}
```

- [ ] **Step 4: Update `EditorProjectSceneCatalogService` to use scene ids and resolve unique scene paths**

```csharp
public IReadOnlyList<string> GetSceneIds() {
    string[] scenePaths = Directory.GetFiles(AssetsRootPath, "*.helen", SearchOption.AllDirectories);
    List<string> sceneIds = new List<string>(scenePaths.Length);
    HashSet<string> seenSceneIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    for (int index = 0; index < scenePaths.Length; index++) {
        string sceneId = SceneIdUtility.FromPath(scenePaths[index]);
        if (!seenSceneIds.Add(sceneId)) {
            throw new InvalidOperationException($"Duplicate scene id '{sceneId}' was derived from project scenes.");
        }

        sceneIds.Add(sceneId);
    }

    sceneIds.Sort(StringComparer.Ordinal);
    return sceneIds;
}

public string ResolveScenePath(string sceneId) {
    if (string.IsNullOrWhiteSpace(sceneId)) {
        throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
    }

    string[] scenePaths = Directory.GetFiles(AssetsRootPath, "*.helen", SearchOption.AllDirectories);
    string resolvedScenePath = string.Empty;
    for (int index = 0; index < scenePaths.Length; index++) {
        string candidatePath = scenePaths[index];
        if (!string.Equals(SceneIdUtility.FromPath(candidatePath), sceneId, StringComparison.OrdinalIgnoreCase)) {
            continue;
        }
        if (!string.IsNullOrWhiteSpace(resolvedScenePath)) {
            throw new InvalidOperationException($"Scene id '{sceneId}' resolved to multiple authored scenes.");
        }

        resolvedScenePath = Path.GetRelativePath(AssetsRootPath, candidatePath).Replace('\\', '/');
    }

    if (string.IsNullOrWhiteSpace(resolvedScenePath)) {
        throw new InvalidOperationException($"Scene id '{sceneId}' was not found in the project scene catalog.");
    }

    return resolvedScenePath;
}
```

- [ ] **Step 5: Update the queue-item factory test expectations**

```csharp
platformConfig.SelectedSceneIds = [
    "B",
    "A"
];

platformConfig.SceneOrders = [
    new EditorBuildSceneOrderDocument { SceneId = "A", OrderNumber = 2 },
    new EditorBuildSceneOrderDocument { SceneId = "B", OrderNumber = 1 }
];

Assert.Equal(new[] { "B", "A" }, queueItem.SelectedSceneIds);
```

- [ ] **Step 6: Run the focused tests to verify they pass**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorProjectSceneCatalogServiceTests|FullyQualifiedName~EditorBuildQueueItemFactoryTests"
```

Expected: PASS.

- [ ] **Step 7: Commit the shared scene-id groundwork**

```powershell
rtk git add engine/helengine.core/content/SceneIdUtility.cs engine/helengine.editor/managers/project/EditorProjectSceneCatalogService.cs engine/helengine.editor.tests/managers/project/EditorProjectSceneCatalogServiceTests.cs engine/helengine.editor.tests/managers/project/EditorBuildQueueItemFactoryTests.cs
rtk git commit -m "Add generic scene id derivation"
```

### Task 2: Propagate Scene Ids Through Cooked Build Metadata And The Demo-Disc Writer

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorRuntimeNativeManifestWriter.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorRuntimeNativeManifestWriterTests.cs`
- Modify: `tools/demo-disc-scene-writer/DemoDiscSceneWriter.cs`
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Write the failing manifest and scene-writer tests**

```csharp
Assert.Contains("\"NewScene\"", sceneCatalogSource);
Assert.DoesNotContain("\"Scenes/NewScene.helen\"", sceneCatalogSource, StringComparison.Ordinal);

Assert.Contains("DemoDiscMainMenu", ReadSelectedSceneIds());
Assert.DoesNotContain("Scenes/DemoDiscMainMenu.helen", ReadSelectedSceneIds(), StringComparer.Ordinal);
```

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorRuntimeNativeManifestWriterTests|FullyQualifiedName~DemoDiscSceneWriterTests"
```

Expected: FAIL because cooked scenes and generated build config still preserve path-based scene ids.

- [ ] **Step 3: Update cooked scene entry generation to use `SceneIdUtility.FromPath(...)`**

```csharp
PlatformBuildScene[] BuildSceneEntries(IReadOnlyList<string> orderedSceneIds, string outputRootPath) {
    PlatformBuildScene[] scenes = new PlatformBuildScene[orderedSceneIds.Count];
    for (int index = 0; index < orderedSceneIds.Count; index++) {
        string authoredScenePath = orderedSceneIds[index];
        string sceneId = SceneIdUtility.FromPath(authoredScenePath);
        string cookedRelativePath = BuildCookedSceneRelativePath(authoredScenePath, index);
        scenes[index] = new PlatformBuildScene(
            sceneId,
            sceneId,
            cookedRelativePath,
            [ new PlatformBuildPayloadReference(cookedRelativePath, cookedRelativePath) ],
            [
                new KeyValuePair<string, string>("build-order-index", index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.CookedRelativePath, cookedRelativePath)
            ]);
    }

    return scenes;
}
```

- [ ] **Step 4: Update the demo-disc writer to emit scene ids instead of authored paths**

```csharp
const string MenuScenePath = "Scenes/DemoDiscMainMenu.helen";

static readonly string[] CuratedScenePaths = new[] {
    MenuScenePath,
    "scenes/physics/test_scene_dynamic_stack_boxes.helen",
    "scenes/physics/test_scene_dynamic_sphere_ramp.helen"
};

static readonly string[] CuratedSceneIds = CuratedScenePaths
    .Select(SceneIdUtility.FromPath)
    .ToArray();

new MenuItemDefinition(
    "scene-stack-boxes",
    "Stack Boxes",
    "Physics stress test with stacked dynamic boxes.",
    true,
    new MenuActionDefinition(MenuActionKind.LoadScene, SceneIdUtility.FromPath("scenes/physics/test_scene_dynamic_stack_boxes.helen")))
```

- [ ] **Step 5: Keep runtime manifest writer assertions aligned with concise ids**

```csharp
PlatformBuildScene startupScene = new(
    "NewScene",
    "NewScene",
    "Scenes/NewScene.helen",
    Array.Empty<PlatformBuildPayloadReference>(),
    [
        new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.CookedRelativePath, "cooked/scenes/NewScene.hasset"),
        new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.Physics3DSceneFeatureFlags, "33")
    ]);
```

- [ ] **Step 6: Run the focused tests to verify they pass**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorRuntimeNativeManifestWriterTests|FullyQualifiedName~DemoDiscSceneWriterTests"
```

Expected: PASS.

- [ ] **Step 7: Commit cooked scene-id propagation and writer updates**

```powershell
rtk git add engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs engine/helengine.editor/managers/project/EditorRuntimeNativeManifestWriter.cs engine/helengine.editor.tests/managers/project/EditorRuntimeNativeManifestWriterTests.cs tools/demo-disc-scene-writer/DemoDiscSceneWriter.cs engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs
rtk git commit -m "Propagate scene ids through build manifests"
```

### Task 3: Inject Editor Scene-Id Resolution Into Core And Remove Menu Path Logic

**Files:**
- Create: `engine/helengine.core/content/ISceneIdPathResolver.cs`
- Modify: `engine/helengine.core/CoreInitializationOptions.cs`
- Modify: `engine/helengine.editor/managers/project/EditorProjectSceneCatalogService.cs`
- Modify: `engine/helengine.core/components/2d/menu/MenuComponent.cs`
- Modify: `engine/helengine.editor.tests/testing/TestMenuDefinitionProvider.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/preview/CameraPreviewSourceTests.cs`

- [ ] **Step 1: Write the failing menu tests using scene ids**

```csharp
new MenuItemDefinition(
    "scene-one",
    "Downtown Morning",
    "Opens the sample city scene.",
    true,
    new MenuActionDefinition(MenuActionKind.LoadScene, "TestPlayableScene"))
```

```csharp
Core core = new Core(new CoreInitializationOptions {
    ContentRootPath = buildRootPath,
    SceneCatalog = new RuntimeSceneCatalog(new[] {
        new RuntimeSceneCatalogEntry("TestPlayableScene", "cooked/scenes/TestPlayableScene.hasset")
    }),
    ScenePathResolver = new TestSceneIdPathResolver(new Dictionary<string, string> {
        ["TestPlayableScene"] = "Scenes/TestPlayableScene.helen"
    })
});
```

- [ ] **Step 2: Run the menu-focused tests to verify they fail**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~CameraPreviewSourceTests"
```

Expected: FAIL because `MenuComponent` still expects path payloads and `CoreInitializationOptions` does not yet expose a scene-id path resolver.

- [ ] **Step 3: Add the generic scene-id path resolver contract**

```csharp
namespace helengine {
    /// <summary>
    /// Resolves one authored scene path from a stable scene id.
    /// </summary>
    public interface ISceneIdPathResolver {
        /// <summary>
        /// Resolves one authored scene path from the supplied stable scene id.
        /// </summary>
        /// <param name="sceneId">Stable scene id to resolve.</param>
        /// <returns>Authored scene path relative to the active content root.</returns>
        string ResolveScenePath(string sceneId);
    }
}
```

- [ ] **Step 4: Inject the optional resolver into `CoreInitializationOptions` and let `EditorProjectSceneCatalogService` implement it**

```csharp
public ISceneIdPathResolver ScenePathResolver { get; set; }
```

```csharp
public sealed class EditorProjectSceneCatalogService : ISceneIdPathResolver {
    public string ResolveScenePath(string sceneId) {
        // Reuse the unique scene-id lookup from Task 1.
    }
}
```

- [ ] **Step 5: Refactor `MenuComponent` to treat `TargetId` as a scene id**

```csharp
void LoadScene(string sceneId) {
    if (string.IsNullOrWhiteSpace(sceneId)) {
        throw new InvalidOperationException("Scene-loading baked menu items must provide a scene id.");
    }
    if (Core.Instance == null) {
        throw new InvalidOperationException("A core instance must exist before loading a scene from the baked menu.");
    }

    if (ComponentExecutionContext.CurrentMode == ComponentExecutionMode.Editor) {
        if (Core.Instance.SceneLoadService == null) {
            throw new InvalidOperationException("Core scene loading services must be initialized before loading a scene from the baked menu.");
        }
        if (Core.Instance.InitializationOptions.ScenePathResolver == null) {
            throw new InvalidOperationException("An editor scene-id path resolver must be configured before editor menu scene loading can occur.");
        }

        string authoredScenePath = Core.Instance.InitializationOptions.ScenePathResolver.ResolveScenePath(sceneId);
        SceneAsset sceneAsset = Core.Instance.ContentManager.Load<SceneAsset>(authoredScenePath, RuntimeContentProcessorIds.SceneAsset);
        Core.Instance.SceneLoadService.Load(sceneAsset);
        if (Parent != null) {
            Parent.Enabled = false;
        }
    } else if (Core.Instance.SceneManager == null) {
        throw new InvalidOperationException("Core scene manager must be initialized before runtime menu scene loading can occur.");
    } else {
        Core.Instance.SceneManager.LoadScene(sceneId, SceneLoadMode.Single);
    }
}
```

Also delete:

```csharp
ResolveSceneContentPath(...)
BuildPackagedSceneContentPath(...)
DoesContentFileExist(...)
NormalizeRelativeContentPath(...)
```

- [ ] **Step 6: Run the focused tests to verify they pass**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~CameraPreviewSourceTests"
```

Expected: PASS.

- [ ] **Step 7: Commit the shared menu scene-id refactor**

```powershell
rtk git add engine/helengine.core/content/ISceneIdPathResolver.cs engine/helengine.core/CoreInitializationOptions.cs engine/helengine.core/components/2d/menu/MenuComponent.cs engine/helengine.editor/managers/project/EditorProjectSceneCatalogService.cs engine/helengine.editor.tests/testing/TestMenuDefinitionProvider.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs engine/helengine.editor.tests/managers/preview/CameraPreviewSourceTests.cs
rtk git commit -m "Load menu scenes by scene id"
```

### Task 4: Simplify `DemoDiscReturnToMenuComponent` And Gate Desktop Keyboard Input

**Files:**
- Modify: `C:/dev/helprojs/city/assets/codebase/menu/DemoDiscReturnToMenuComponent.cs`
- Modify: `C:/dev/helprojs/city/assets/codebase/menu/DemoDiscSceneCatalog.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`

- [ ] **Step 1: Write the failing translation/regression expectation**

```csharp
Assert.DoesNotContain("ResolveSceneContentPath", normalizedSource, StringComparison.Ordinal);
Assert.DoesNotContain("BuildPackagedSceneContentPath", normalizedSource, StringComparison.Ordinal);
Assert.Contains("Core::get_Instance()->get_SceneManager()->LoadScene(MainMenuSceneId, SceneLoadMode::Single);", normalizedSource);
```

- [ ] **Step 2: Run the focused generator regression to verify it fails**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~Normalize_generated_native_sources_fixes_demo_disc_return_to_menu_component_windows_translation
```

Expected: FAIL because the authored component still generates path-resolution code and unconditional keyboard references.

- [ ] **Step 3: Rewrite `DemoDiscReturnToMenuComponent` to only detect input and load the main menu by id**

```csharp
public const string MainMenuSceneId = "DemoDiscMainMenu";

void ReturnToMainMenu() {
    if (Core.Instance == null) {
        throw new InvalidOperationException("A core instance must exist before returning to the demo-disc main menu.");
    }

    if (ComponentExecutionContext.CurrentMode == ComponentExecutionMode.Editor) {
        if (Core.Instance.SceneLoadService == null) {
            throw new InvalidOperationException("Core scene loading services must be initialized before returning to the demo-disc main menu.");
        }
        if (Core.Instance.InitializationOptions.ScenePathResolver == null) {
            throw new InvalidOperationException("An editor scene-id path resolver must be configured before returning to the demo-disc main menu.");
        }

        string scenePath = Core.Instance.InitializationOptions.ScenePathResolver.ResolveScenePath(MainMenuSceneId);
        SceneAsset sceneAsset = Core.Instance.ContentManager.Load<SceneAsset>(scenePath, RuntimeContentProcessorIds.SceneAsset);
        Core.Instance.SceneLoadService.Load(sceneAsset);
        if (Parent != null) {
            Parent.Enabled = false;
        }
    } else if (Core.Instance.SceneManager == null) {
        throw new InvalidOperationException("Core scene manager must be initialized before returning to the demo-disc main menu.");
    } else {
        Core.Instance.SceneManager.LoadScene(MainMenuSceneId, SceneLoadMode.Single);
    }
}
```

- [ ] **Step 4: Gate keyboard checks to desktop only and keep direct gamepad polling**

```csharp
bool WasReturnPressed(InputSystem inputSystem) {
#if DESKTOP_PLATFORM
    if (inputSystem.WasKeyPressed(Keys.Escape) || inputSystem.WasKeyPressed(Keys.Back)) {
        return true;
    }
#endif

    InputGamepadState currentGamepadState = ReadPrimaryGamepadState(inputSystem);
    if (!currentGamepadState.Connected) {
        PreviousGamepadState = currentGamepadState;
        return false;
    }

    return WasGamepadButtonPressed(currentGamepadState, PreviousGamepadState, InputGamepadButton.East)
        || WasGamepadButtonPressed(currentGamepadState, PreviousGamepadState, InputGamepadButton.North)
        || WasGamepadButtonPressed(currentGamepadState, PreviousGamepadState, InputGamepadButton.Select);
}
```

- [ ] **Step 5: Replace city menu action payloads with scene ids**

```csharp
new MenuItemDefinition(
    "scene-cube-test",
    "Cube Test",
    "Minimal one-cube rendering validation scene.",
    true,
    new MenuActionDefinition(MenuActionKind.LoadScene, "cube_test"))
```

- [ ] **Step 6: Run the focused generator regression to verify it passes**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~Normalize_generated_native_sources_fixes_demo_disc_return_to_menu_component_windows_translation
```

Expected: PASS.

- [ ] **Step 7: Commit the city return-component cleanup**

```powershell
rtk git -C C:\dev\helprojs\city add -- assets/codebase/menu/DemoDiscReturnToMenuComponent.cs assets/codebase/menu/DemoDiscSceneCatalog.cs
rtk git -C C:\dev\helworks\helengine add -- engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs
rtk git -C C:\dev\helprojs\city commit -m "Use scene ids in demo menu"
rtk git -C C:\dev\helworks\helengine commit -m "Update return component codegen regression"
```

### Task 5: End-To-End Verification On The Windows Export

**Files:**
- Modify: `tools/demo-disc-scene-writer/DemoDiscSceneWriter.cs` if Task 2 scene-action or selected-scene output still needs adjustment after live verification
- Verify: `C:/dev/helprojs/city/user_settings/build_config.json`
- Verify: `C:/tmp/city-windows-export-sceneid`

- [ ] **Step 1: Rebuild any generated demo-disc sources if the city menu is tool-owned**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~DemoDiscSceneWriterTests
```

Expected: PASS, proving the generator emits scene-id-based menu/build-config output.

- [ ] **Step 2: Run the focused shared engine/editor test suite**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorProjectSceneCatalogServiceTests|FullyQualifiedName~EditorRuntimeNativeManifestWriterTests|FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~CameraPreviewSourceTests|FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests"
```

Expected: PASS for the targeted scene-id and return-component regressions.

- [ ] **Step 3: Build a fresh Windows export against the city project**

Run:

```powershell
rtk dotnet run --project helengine.ui\helengine.editor.app\helengine.editor.app.csproj -- --project C:\dev\helprojs\city --build windows --output C:\tmp\city-windows-export-sceneid
```

Expected: Build completed for platform `windows`.

- [ ] **Step 4: Launch the player and verify menu scene transitions stay alive**

Run:

```powershell
$exe = 'C:\tmp\city-windows-export-sceneid\helengine_windows.exe'
$log = 'C:\tmp\city-windows-export-sceneid\helengine_windows.startup.log'
$process = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 4
$shell = New-Object -ComObject WScript.Shell
[void]$shell.AppActivate($process.Id)
$shell.SendKeys('{ENTER}')
Start-Sleep -Seconds 8
Get-Content $log
```

Expected:

- no fatal `Player builds do not support serialized component type ...` exception
- no fatal `Core scene manager must be initialized ...` exception
- no fatal scene-path resolution errors
- process remains alive after entering a scene from the menu

- [ ] **Step 5: Commit any final engine-side verification fixes**

```powershell
rtk git status --short
rtk git add <verified engine files>
rtk git commit -m "Refactor scene loading to use scene ids"
```

---

## Self-Review

- Spec coverage check:
  - generic scene-id derivation: Task 1
  - runtime scene-id propagation: Task 2
  - editor-side scene-id resolution: Task 3
  - menu and return-component path removal: Tasks 3 and 4
  - desktop-only keyboard gating: Task 4
  - end-to-end Windows verification: Task 5
- Placeholder scan: no `TODO`, `TBD`, or “similar to above” references remain.
- Type consistency check:
  - helper name stays `SceneIdUtility`
  - injected editor resolver stays `ISceneIdPathResolver`
  - editor resolver method stays `ResolveScenePath(string sceneId)`

