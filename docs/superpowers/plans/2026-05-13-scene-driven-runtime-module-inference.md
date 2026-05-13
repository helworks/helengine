# Scene-Driven Runtime Module Inference Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove `selectedCodeModuleIds` from editor build state and make platform builds compile authored runtime modules only when selected scenes reference their components.

**Architecture:** Keep module inference inside the editor build graph. After scene cooking, inspect the cooked selected scenes, resolve referenced scripted component types, derive root module ids from owning assemblies, and feed those inferred roots into the existing manifest dependency traversal in `EditorPlatformCodeCookService`. Remove the obsolete module-selection fields from persisted build config, queue snapshots, and UI summaries.

**Tech Stack:** C# 13 / .NET 9, `helengine.editor`, `helengine.core`, xUnit, System.Text.Json

---

## File Structure

- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorGeneratedCoreRegenerationService.cs`
  - Reuse or extend cooked-scene scripted-component discovery to return referenced runtime module ids.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorPlatformBuildGraphRunner.cs`
  - Infer root module ids from cooked selected scenes and pass them into code compilation.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorPlatformCodeCookService.cs`
  - Rename/reshape root-module selection input from persisted selection to inferred roots while preserving strict manifest dependency resolution.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorBuildPlatformConfigDocument.cs`
  - Remove `SelectedCodeModuleIds`.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorBuildQueueItemDocument.cs`
  - Remove `SelectedCodeModuleIds`.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorBuildConfigService.cs`
  - Stop normalizing or seeding `SelectedCodeModuleIds`; continue loading legacy JSON cleanly.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorBuildQueueItemFactory.cs`
  - Stop copying module ids into queue snapshots.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\BuildDialog.cs`
  - Remove dead sync/reset/summary logic for runtime module counts.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\model\BuildDialogAddRequest.cs`
  - Remove the selected runtime module list from the request model if it is still present.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`
  - Stop persisting queue/platform module selection state.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\EditorBuildConfigServiceTests.cs`
  - Cover legacy config load and rewritten output without `selectedCodeModuleIds`.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\tools\DemoDiscSceneWriterTests.cs`
  - Update existing JSON assertions to stop expecting `selectedCodeModuleIds`.
- Create or Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorPlatformCodeCookServiceTests.cs`
  - Add inference/dependency strictness coverage.
- Create or Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorPlatformBuildGraphRunnerTests.cs`
  - Add cooked-scene module inference coverage at the build-graph level.

## Task 1: Remove Obsolete Build-State Fields

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorBuildPlatformConfigDocument.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorBuildQueueItemDocument.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorBuildConfigService.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorBuildQueueItemFactory.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\BuildDialog.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\model\BuildDialogAddRequest.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\EditorBuildConfigServiceTests.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\tools\DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Write the failing config regression test**

```csharp
[Fact]
public void Save_WithLegacySelectedCodeModuleIds_RewritesConfigWithoutSelectedCodeModuleIds() {
    string projectRootPath = CreateTempProjectRoot();
    string userSettingsPath = Path.Combine(projectRootPath, "user_settings");
    Directory.CreateDirectory(userSettingsPath);
    string configPath = Path.Combine(userSettingsPath, "build_config.json");
    File.WriteAllText(configPath,
        """
        {
          "platforms": [
            {
              "platformId": "windows",
              "selectedSceneIds": [ "scenes/rendering/cube_test.helen" ],
              "selectedCodeModuleIds": [ "gameplay" ]
            }
          ],
          "queueItems": [
            {
              "queueItemId": "queue1",
              "platformId": "windows",
              "selectedSceneIds": [ "scenes/rendering/cube_test.helen" ],
              "selectedCodeModuleIds": [ "gameplay" ]
            }
          ]
        }
        """);

    EditorBuildConfigService service = new(projectRootPath);
    EditorBuildConfigDocument document = service.Load(["windows"], "scenes/rendering/cube_test.helen");
    service.Save(document);

    string rewrittenJson = File.ReadAllText(configPath);
    Assert.DoesNotContain("selectedCodeModuleIds", rewrittenJson, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the config regression test to verify it fails**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~Save_WithLegacySelectedCodeModuleIds_RewritesConfigWithoutSelectedCodeModuleIds"
```

Expected: FAIL because the current document model still serializes `selectedCodeModuleIds`.

- [ ] **Step 3: Remove `SelectedCodeModuleIds` from persisted document models and normalization**

Code targets:

```csharp
public sealed class EditorBuildPlatformConfigDocument {
    public string PlatformId { get; set; } = string.Empty;
    public List<string> SelectedSceneIds { get; set; } = [];
    public List<EditorBuildSceneOrderDocument> SceneOrders { get; set; } = [];
    public string OutputDirectoryPath { get; set; } = string.Empty;
    public bool DebugBuild { get; set; }
    public string SelectedBuildProfileId { get; set; } = string.Empty;
    public string SelectedGraphicsProfileId { get; set; } = string.Empty;
    public Dictionary<string, string> SelectedBuildOptionValues { get; set; } = [];
    public Dictionary<string, string> SelectedGraphicsOptionValues { get; set; } = [];
    public string SelectedCodegenProfileId { get; set; } = string.Empty;
    public string SelectedStorageProfileId { get; set; } = string.Empty;
    public string SelectedMediaProfileId { get; set; } = string.Empty;
    public Dictionary<string, string> SelectedCodegenOptionValues { get; set; } = [];
}
```

```csharp
public sealed class EditorBuildQueueItemDocument {
    public string QueueItemId { get; set; } = string.Empty;
    public string PlatformId { get; set; } = string.Empty;
    public List<string> SelectedSceneIds { get; set; } = [];
    public string OutputDirectoryPath { get; set; } = string.Empty;
    public EditorBuildQueueItemStatus Status { get; set; } = EditorBuildQueueItemStatus.Pending;
    public string StatusMessage { get; set; } = string.Empty;
    public bool DebugBuild { get; set; }
    public string SelectedBuildProfileId { get; set; } = string.Empty;
    public string SelectedGraphicsProfileId { get; set; } = string.Empty;
    public Dictionary<string, string> SelectedBuildOptionValues { get; set; } = [];
    public Dictionary<string, string> SelectedGraphicsOptionValues { get; set; } = [];
    public string SelectedCodegenProfileId { get; set; } = string.Empty;
    public string SelectedStorageProfileId { get; set; } = string.Empty;
    public string SelectedMediaProfileId { get; set; } = string.Empty;
    public Dictionary<string, string> SelectedCodegenOptionValues { get; set; } = [];
}
```

Also remove all `SelectedCodeModuleIds ??= []`, copy assignments, and queue-summary text that mentions runtime module counts.

- [ ] **Step 4: Run the focused config and UI regression tests**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~Save_WithLegacySelectedCodeModuleIds_RewritesConfigWithoutSelectedCodeModuleIds|FullyQualifiedName~DemoDiscSceneWriter"
```

Expected: PASS after updating the affected tests to stop expecting `selectedCodeModuleIds`.

## Task 2: Add Cooked-Scene Runtime Module Inference

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorGeneratedCoreRegenerationService.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorPlatformBuildGraphRunnerTests.cs`

- [ ] **Step 1: Write the failing inference tests**

```csharp
[Fact]
public void DiscoverReferencedRuntimeModuleIdsFromCookedScenes_WithScriptedGameplayComponent_ReturnsOwningAssemblyName() {
    string cookedScenePath = WriteCookedSceneWithScriptComponent("gameplay.rendering.AxisRotationComponent, gameplay");

    IReadOnlyList<string> moduleIds = EditorGeneratedCoreRegenerationService
        .DiscoverReferencedRuntimeModuleIdsFromCookedScenes([cookedScenePath], CreateScriptTypeResolver());

    Assert.Equal(["gameplay"], moduleIds);
}

[Fact]
public void DiscoverReferencedRuntimeModuleIdsFromCookedScenes_WithNoScriptedComponents_ReturnsEmptyList() {
    string cookedScenePath = WriteCookedSceneWithoutScriptComponents();

    IReadOnlyList<string> moduleIds = EditorGeneratedCoreRegenerationService
        .DiscoverReferencedRuntimeModuleIdsFromCookedScenes([cookedScenePath], CreateScriptTypeResolver());

    Assert.Empty(moduleIds);
}
```

- [ ] **Step 2: Run the inference tests to verify they fail**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~DiscoverReferencedRuntimeModuleIdsFromCookedScenes_"
```

Expected: FAIL because the module-oriented API does not exist yet.

- [ ] **Step 3: Implement the shared module-inference helper**

Code target:

```csharp
internal static IReadOnlyList<string> DiscoverReferencedRuntimeModuleIdsFromCookedScenes(
    IReadOnlyList<string> cookedSceneAssetPaths,
    IScriptTypeResolver scriptTypeResolver) {
    IReadOnlyList<Type> componentTypes = DiscoverAutomaticRuntimeComponentTypesFromCookedScenes(
        cookedSceneAssetPaths,
        scriptTypeResolver);

    SortedSet<string> moduleIds = new(StringComparer.OrdinalIgnoreCase);
    for (int index = 0; index < componentTypes.Count; index++) {
        Type componentType = componentTypes[index];
        string moduleId = componentType.Assembly.GetName().Name ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(moduleId)) {
            moduleIds.Add(moduleId);
        }
    }

    return [.. moduleIds];
}
```

- [ ] **Step 4: Re-run the inference tests**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~DiscoverReferencedRuntimeModuleIdsFromCookedScenes_"
```

Expected: PASS.

## Task 3: Feed Inferred Roots into Code Compilation

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorPlatformBuildGraphRunner.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorPlatformCodeCookService.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorPlatformCodeCookServiceTests.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorPlatformBuildGraphRunnerTests.cs`

- [ ] **Step 1: Write the failing dependency-resolution tests**

```csharp
[Fact]
public void ResolveModulesToCompile_WithInferredRuntimeRoots_IncludesDependenciesInOrder() {
    EditorCodeModuleManifestDocument manifest = new(
    [
        new EditorCodeModuleManifestEntry("shared", "assets/codebase/shared", [], [], EditorCodeModuleKind.Runtime),
        new EditorCodeModuleManifestEntry("gameplay", "assets/codebase/gameplay", ["shared"], [], EditorCodeModuleKind.Runtime)
    ]);

    EditorCodeModuleManifestEntry[] modules = EditorPlatformCodeCookService.ResolveModulesToCompile(
        manifest,
        ["gameplay"]);

    Assert.Equal(["shared", "gameplay"], modules.Select(module => module.ModuleId).ToArray());
}

[Fact]
public void ResolveModulesToCompile_WithEditorOnlyInferredRoot_ThrowsInvalidOperationException() {
    EditorCodeModuleManifestDocument manifest = new(
    [
        new EditorCodeModuleManifestEntry("tools", "assets/codebase/tools", [], [], EditorCodeModuleKind.Editor)
    ]);

    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        EditorPlatformCodeCookService.ResolveModulesToCompile(manifest, ["tools"]));

    Assert.Contains("runtime", exception.Message, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run the dependency-resolution tests to verify they fail**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~ResolveModulesToCompile_WithInferredRuntimeRoots_IncludesDependenciesInOrder|FullyQualifiedName~ResolveModulesToCompile_WithEditorOnlyInferredRoot_ThrowsInvalidOperationException"
```

Expected: FAIL because the code still uses the selected-module naming/behavior and may not expose the strict editor-only assertion path cleanly.

- [ ] **Step 3: Update the build graph and code cook service to use inferred roots**

Code targets:

```csharp
PlatformBuildCodeModule[] RunCompileCode(
    PlatformBuildManifest cookedManifest,
    PlatformCodegenProfileDefinition selectedCodegenProfile,
    PlatformStorageProfileDefinition selectedStorageProfile,
    EditorBuildQueueItemDocument queueItem,
    EditorPlatformBuildGraphWorkspace workspace) {
    EditorCodeModuleManifestDocument manifestDocument = CodeModuleManifestService.Load();
    IReadOnlyList<string> inferredRootModuleIds = DiscoverReferencedRuntimeModuleIdsFromCookedScenes(
        cookedManifest,
        workspace.CookRootPath);

    return CodeCookService.CompileModules(
        manifestDocument,
        PlatformDescriptor.Id,
        selectedStorageProfile?.RuntimeSpecializationId ?? string.Empty,
        PlatformDescriptor.CodegenToolPath,
        selectedCodegenProfile,
        inferredRootModuleIds,
        queueItem.SelectedCodegenOptionValues,
        workspace.CodeRootPath);
}
```

```csharp
static EditorCodeModuleManifestEntry[] ResolveModulesToCompile(
    EditorCodeModuleManifestDocument manifestDocument,
    IReadOnlyList<string> inferredRootModuleIds) {
    HashSet<string> inferredRootModuleIdSet = BuildSelectedModuleIdSet(inferredRootModuleIds);
    // Preserve dependency traversal, but error text should refer to inferred runtime module ids.
}
```

- [ ] **Step 4: Add the missing-root build failure test**

```csharp
[Fact]
public void ResolveModulesToCompile_WithMissingInferredRoot_ThrowsInvalidOperationException() {
    EditorCodeModuleManifestDocument manifest = new([]);

    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        EditorPlatformCodeCookService.ResolveModulesToCompile(manifest, ["gameplay"]));

    Assert.Contains("gameplay", exception.Message, StringComparison.Ordinal);
}
```

- [ ] **Step 5: Re-run the focused build-graph and code-cook tests**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~ResolveModulesToCompile_|FullyQualifiedName~DiscoverReferencedRuntimeModuleIdsFromCookedScenes_"
```

Expected: PASS.

## Task 4: Verify End-to-End Build Behavior

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorPlatformBuildGraphRunnerTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\tools\DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Add the end-to-end queue/build regression test**

```csharp
[Fact]
public void Execute_WithSceneReferencedGameplayComponent_CompilesOnlyReferencedRuntimeModules() {
    // Arrange a cooked-scene fixture containing gameplay component refs,
    // a manifest with runtime and editor modules,
    // and a recording codegen runner.
    // Assert that only the scene-referenced runtime module roots and their runtime dependencies are compiled.
}
```

- [ ] **Step 2: Run the end-to-end regression test to verify it fails**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~Execute_WithSceneReferencedGameplayComponent_CompilesOnlyReferencedRuntimeModules"
```

Expected: FAIL until the build graph is wired to scene-driven inference and the test fixture assertions are updated.

- [ ] **Step 3: Update affected JSON and queue-summary assertions**

Code targets:

```csharp
Assert.DoesNotContain("selectedCodeModuleIds", persistedJson, StringComparison.Ordinal);
Assert.DoesNotContain("runtime modules", queueSummaryText, StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 4: Run the focused regression suite**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~Execute_WithSceneReferencedGameplayComponent_CompilesOnlyReferencedRuntimeModules|FullyQualifiedName~Save_WithLegacySelectedCodeModuleIds_RewritesConfigWithoutSelectedCodeModuleIds|FullyQualifiedName~DiscoverReferencedRuntimeModuleIdsFromCookedScenes_|FullyQualifiedName~ResolveModulesToCompile_"
```

Expected: PASS.

- [ ] **Step 5: Run a broader verification pass**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorBuildConfigServiceTests|FullyQualifiedName~EditorPlatformCodeCookServiceTests|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests|FullyQualifiedName~DemoDiscSceneWriterTests"
```

Expected: PASS for the touched editor build/config test surfaces.

## Notes

- Do not add compatibility fallbacks that compile all runtime modules when inference returns an empty set.
- Do not reintroduce `SelectedCodeModuleIds` under a different name in UI state or queue documents.
- Keep error messages explicit when a cooked scene references a component whose owning assembly name cannot be resolved to a runtime module.
- Do not commit during implementation in this workspace unless the user explicitly changes that instruction.
