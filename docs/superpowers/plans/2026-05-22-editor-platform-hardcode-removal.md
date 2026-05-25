# Editor Platform Hardcode Removal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the remaining PS2, Nintendo DS, GameCube, and other external package-owned production policy from `helengine.editor` so the editor only orchestrates generic platform builds.

**Architecture:** This pass is deletion-first. The editor will stop branching on concrete platform ids for build graph behavior, scene expansion, import defaults, and native manifest generation. If external platform repos still depended on these editor-side behaviors, the breakage is intentional and must be fixed in the platform repos or through a future generic plugin contract.

**Tech Stack:** C#, .NET, `helengine.editor`, `helengine.editor.tests`, `helengine.platforms`, xUnit, `rtk` command wrapper

---

## File Structure

### Production files to delete

- `engine/helengine.editor/managers/project/Ps2DepthHandlerMode.cs`
- `engine/helengine.editor/managers/project/RuntimeGraphicsRendererManifest.cs`

### Production files to modify

- `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- `engine/helengine.editor/managers/project/EditorRuntimeGraphicsRendererManifestWriter.cs`
- `engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs`
- `engine/helengine.editor/managers/project/EditorPlatformPreprocessorSymbolService.cs`
- `engine/helengine.editor/managers/asset/AssetImportManager.cs`

### Test files to modify or delete

- `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`
- `engine/helengine.editor.tests/managers/project/EditorBuildQueueItemDocumentTests.cs`
- `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
- `engine/helengine.editor.tests/AssetImportManagerTests.cs`

### Validation targets

- `engine/helengine.editor.tests/helengine.editor.tests.csproj`

---

### Task 1: Remove PS2 Renderer Manifest Ownership From The Editor

**Files:**
- Delete: `engine/helengine.editor/managers/project/Ps2DepthHandlerMode.cs`
- Delete: `engine/helengine.editor/managers/project/RuntimeGraphicsRendererManifest.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Modify: `engine/helengine.editor/managers/project/EditorRuntimeGraphicsRendererManifestWriter.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`

- [ ] **Step 1: Write the failing test edits**

Remove the PS2-manifest assertions and replace them with a focused assertion that the build graph runner no longer emits runtime renderer manifest source as part of the generic editor path.

```csharp
[Fact]
public void Run_does_not_emit_runtime_graphics_renderer_manifest_source() {
    // Arrange a minimal build runner fixture identical to the current helper setup.
    // Execute the runner.
    // Assert the generated-core output does not contain:
    // runtime/runtime_graphics_renderer_manifest.hpp
    // runtime/runtime_graphics_renderer_manifest.cpp
}
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~Run_does_not_emit_runtime_graphics_renderer_manifest_source' 2>&1 | Select-Object -Last 120 | Out-String -Width 260 | Write-Output"
```

Expected: FAIL because the current runner still writes the runtime graphics renderer manifest files.

- [ ] **Step 3: Remove the production manifest path**

Delete `Ps2DepthHandlerMode.cs` and `RuntimeGraphicsRendererManifest.cs`.

In `EditorPlatformBuildGraphRunner.cs`, remove:

```csharp
WriteRuntimeGraphicsRendererManifestSource(workspace.GeneratedCoreRootPath, selectionModel);
```

Remove the supporting methods:

```csharp
void WriteRuntimeGraphicsRendererManifestSource(string generatedCoreRootPath, EditorPlatformBuildSelectionModel selectionModel)
RuntimeGraphicsRendererManifest ResolveRuntimeGraphicsRendererManifest(EditorPlatformBuildSelectionModel selectionModel)
static Ps2DepthHandlerMode ResolvePs2DepthHandlerMode(EditorGraphicsProfileSettingsDocument graphicsSettings)
```

Delete `EditorRuntimeGraphicsRendererManifestWriter.cs` if it becomes unused; otherwise remove its usage and then delete it in the same task.

- [ ] **Step 4: Update the tests to the new generic boundary**

Delete or rewrite any tests in `EditorPlatformBuildGraphRunnerTests.cs` that assert:

- `WriteRuntimeGraphicsRendererManifestSource`
- `HERuntimePs2DepthHandlerMode`
- PS2 depth handler mode selection

Replace them with generic build-graph assertions only.

- [ ] **Step 5: Run the focused build-graph tests**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~EditorPlatformBuildGraphRunnerTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

Expected: PASS with the PS2-manifest tests removed or rewritten.

- [ ] **Step 6: Commit**

```powershell
rtk git add engine/helengine.editor/managers/project/Ps2DepthHandlerMode.cs engine/helengine.editor/managers/project/RuntimeGraphicsRendererManifest.cs engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs engine/helengine.editor/managers/project/EditorRuntimeGraphicsRendererManifestWriter.cs engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs
rtk git commit -m "Remove editor-owned PS2 renderer manifest logic"
```

### Task 2: Remove Platform Repository-Root Environment Variable Special Cases

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`

- [ ] **Step 1: Write the failing test edits**

Add one focused test proving the runner no longer sets platform-id-specific repository-root environment variables.

```csharp
[Fact]
public void Run_does_not_set_platform_specific_repository_root_environment_variables() {
    // Arrange a platform descriptor with id "ps2", "ds", or "gamecube".
    // Execute the runner.
    // Assert:
    // Environment.GetEnvironmentVariable("HELENGINE_PS2_REPOSITORY_ROOT") is null or unchanged
    // Environment.GetEnvironmentVariable("HELENGINE_DS_REPOSITORY_ROOT") is null or unchanged
    // Environment.GetEnvironmentVariable("HELENGINE_GAMECUBE_REPOSITORY_ROOT") is null or unchanged
}
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~Run_does_not_set_platform_specific_repository_root_environment_variables' 2>&1 | Select-Object -Last 120 | Out-String -Width 260 | Write-Output"
```

Expected: FAIL because the runner still sets those environment variables based on platform id.

- [ ] **Step 3: Remove the environment-variable branches**

In `EditorPlatformBuildGraphRunner.cs`, remove:

```csharp
const string Ps2RepositoryRootEnvironmentVariableName = "HELENGINE_PS2_REPOSITORY_ROOT";
const string DsRepositoryRootEnvironmentVariableName = "HELENGINE_DS_REPOSITORY_ROOT";
const string GameCubeRepositoryRootEnvironmentVariableName = "HELENGINE_GAMECUBE_REPOSITORY_ROOT";
```

Remove the platform-specific staging and restoration branches:

```csharp
ApplyBuilderEnvironmentOverrides(...)
RestoreBuilderEnvironmentOverrides(...)
```

Replace them by deleting the call sites entirely if no generic override path remains.

- [ ] **Step 4: Remove or rewrite the old tests**

Delete or rewrite the tests that explicitly validate:

- PS2 repository root exposure
- Nintendo DS repository root exposure
- GameCube repository root exposure

Keep only generic runner behavior assertions.

- [ ] **Step 5: Run the focused build-graph tests**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~EditorPlatformBuildGraphRunnerTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

Expected: PASS with the repository-root special cases gone.

- [ ] **Step 6: Commit**

```powershell
rtk git add engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs
rtk git commit -m "Remove platform repository root special cases from editor"
```

### Task 3: Remove DS Companion-Scene Expansion From Queue Construction

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorBuildQueueItemDocumentTests.cs`

- [ ] **Step 1: Write the failing test edits**

Replace the DS-specific expectation with a generic selected-scene preservation assertion.

```csharp
[Fact]
public void Create_preserves_selected_scene_order_without_platform_scene_expansion() {
    // Arrange selected scene ids where one scene previously had a DS companion.
    // Execute EditorBuildQueueItemDocument.Create(...).
    // Assert the resulting scene list matches the authored selected scene ids exactly.
}
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~EditorBuildQueueItemDocumentTests' 2>&1 | Select-Object -Last 160 | Out-String -Width 260 | Write-Output"
```

Expected: FAIL because `EditorBuildQueueItemDocument` still appends DS companion scenes.

- [ ] **Step 3: Remove DS expansion from production**

In `EditorBuildQueueItemDocument.cs`, remove:

```csharp
const string NintendoDsPlatformId = "ds";
ExpandNintendoDsSceneSet(...)
ResolveNintendoDsCompanionSceneId(...)
IsNintendoDsGeneratedCompanionScenePath(...)
```

Change the queue construction path so it simply preserves the selected scene order:

```csharp
OrderedSceneIds = selectedSceneIds.ToArray();
```

Use the actual existing data flow in the file rather than introducing a new helper abstraction.

- [ ] **Step 4: Update the tests**

Delete DS-specific queue tests and keep generic assertions such as:

- selected scene order is preserved
- no implicit scene insertion occurs
- non-DS platforms also preserve authored ordering

- [ ] **Step 5: Run the focused queue-document tests**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~EditorBuildQueueItemDocumentTests' 2>&1 | Select-Object -Last 160 | Out-String -Width 260 | Write-Output"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
rtk git add engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs engine/helengine.editor.tests/managers/project/EditorBuildQueueItemDocumentTests.cs
rtk git commit -m "Remove DS scene expansion from editor queue items"
```

### Task 4: Remove Hardcoded External Platform Symbol Special Cases

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorPlatformPreprocessorSymbolService.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`

- [ ] **Step 1: Write the failing test edits**

Replace the external-platform-specific symbol assertion with a generic assertion that the service does not inject platform-id-specific symbols on its own.

```csharp
[Fact]
public void ResolveSymbols_does_not_inject_external_platform_symbol() {
    // Arrange one external platform selection using the existing test fixture style.
    // Resolve symbols.
    // Assert the hardcoded external symbol is absent.
}
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests' 2>&1 | Select-Object -Last 180 | Out-String -Width 260 | Write-Output"
```

Expected: FAIL because the current symbol service still emits a hardcoded platform-specific symbol.

- [ ] **Step 3: Remove the hardcoded platform branch**

In `EditorPlatformPreprocessorSymbolService.cs`, delete the branch that adds:

```csharp
"EXTERNAL_PLATFORM"
```

Do not replace it with another hardcoded platform symbol. Leave only generic symbol generation that is not based on explicit platform ids.

- [ ] **Step 4: Update the tests**

Delete or rewrite the tests that assert the editor injects one named external platform symbol. Keep any generic codegen symbol assertions that are still valid.

- [ ] **Step 5: Run the focused symbol/codegen tests**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests' 2>&1 | Select-Object -Last 180 | Out-String -Width 260 | Write-Output"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
rtk git add engine/helengine.editor/managers/project/EditorPlatformPreprocessorSymbolService.cs engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs
rtk git commit -m "Remove hardcoded platform symbol injection from editor"
```

### Task 5: Remove DS Texture Import Defaults From Shared Editor Code

**Files:**
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Modify: `engine/helengine.editor.tests/AssetImportManagerTests.cs`

- [ ] **Step 1: Write the failing test edits**

Replace the Nintendo DS-specific default import tests with generic default-behavior assertions.

```csharp
[Fact]
public void ResolveDefaultTextureProcessorSettings_uses_shared_defaults_without_platform_id_special_cases() {
    // Arrange platform id "ds".
    // Resolve defaults.
    // Assert the result matches the shared default branch rather than a DS-specific compact branch.
}
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~AssetImportManagerTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

Expected: FAIL because `AssetImportManager` still returns DS-specific defaults.

- [ ] **Step 3: Remove the DS-specific default branch**

In `AssetImportManager.cs`, remove the branch:

```csharp
if (string.Equals(platformId, "ds", StringComparison.OrdinalIgnoreCase)) {
    return new TextureAssetProcessorSettings {
        MaxResolution = 128,
        ColorFormatId = TextureAssetColorFormat.Rgba4444.ToString(),
        AlphaPrecision = TextureAssetAlphaPrecision.A4
    };
}
```

Leave only the shared default path:

```csharp
return new TextureAssetProcessorSettings {
    MaxResolution = 0,
    ColorFormatId = TextureAssetColorFormat.Rgba32.ToString(),
    AlphaPrecision = TextureAssetAlphaPrecision.A8
};
```

- [ ] **Step 4: Update the tests**

Delete or rewrite the DS-specific default texture and font import tests so they assert only generic editor behavior.

- [ ] **Step 5: Run the focused import-manager tests**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~AssetImportManagerTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

Expected: PASS for the focused asset-import slice.

- [ ] **Step 6: Commit**

```powershell
rtk git add engine/helengine.editor/managers/asset/AssetImportManager.cs engine/helengine.editor.tests/AssetImportManagerTests.cs
rtk git commit -m "Remove DS import defaults from shared editor code"
```

### Task 6: Final Focused Verification And Cleanup

**Files:**
- Modify: any touched file from Tasks 1-5 if final cleanup is required
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Run a repo search for remaining production platform hardcodes**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "rg -n '\bps2\b|\bPS2\b|\bds\b|\bDS\b|\bpsp\b|\bPSP\b|\bgamecube\b|GameCube' 'engine/helengine.editor' 2>&1 | Select-Object -Last 200 | Out-String -Width 260 | Write-Output"
```

Expected: no remaining production-code hits for the removed hardcodes, or only generic platform-descriptor/UI text that is intentionally still generic.

- [ ] **Step 2: Run the focused editor validation slice**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~EditorPlatformBuildGraphRunnerTests|FullyQualifiedName~EditorBuildQueueItemDocumentTests|FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests|FullyQualifiedName~AssetImportManagerTests' 2>&1 | Select-Object -Last 260 | Out-String -Width 260 | Write-Output"
```

Expected: PASS.

- [ ] **Step 3: Record the boundary cleanup in Graphiti**

Use the Graphiti durable handoff note flow and record that:

- editor no longer owns PS2 manifest/depth behavior
- editor no longer owns platform repository-root env-var overrides
- editor no longer expands DS companion scenes
- editor no longer injects hardcoded external platform symbols
- editor no longer applies DS-specific texture defaults

- [ ] **Step 4: Commit the final cleanup if needed**

```powershell
rtk git add -A
rtk git commit -m "Remove remaining platform-specific editor behavior"
```

Skip this commit if Tasks 1-5 already left the branch clean with no additional file changes.
