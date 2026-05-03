# Unified Platform Runtime Build System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current narrower build/export flow with one editor-owned multi-platform runtime build system that regenerates core every build, cooks runtime `*.hasset` artifacts, compiles authored code modules, specializes the runtime by platform plus storage profile, and preserves future hooks for physical duplication, packfiles, segmented containers, and shared-medium aggregation.

**Architecture:** The editor owns one shared build graph with explicit phases for generated-core regeneration, asset cooking, code-module cooking, variant resolution, layout, container writing, and platform packaging. Platform builders expose typed metadata and consume prepared manifests. Runtime/player bootstrap is specialized by `platform + storage/runtime profile`, while media profile remains layout/config data unless later proven to require compiled behavior.

**Tech Stack:** C# / .NET 9, xUnit, `helengine.baseplatform`, `helengine.platforms`, `helengine.editor`, `helengine.files`, `helengine.core`, `csharpcodegen`, `helengine-windows`, `helengine-ps2`, CMake, native Windows/PS2 host code.

---

## File Structure

### Shared contracts

- Create: `engine/helengine.baseplatform/Definitions/PlatformStorageProfileKind.cs`
- Create: `engine/helengine.baseplatform/Definitions/PlatformStorageProfileDefinition.cs`
- Create: `engine/helengine.baseplatform/Manifest/PlatformArtifactPlacement.cs`
- Create: `engine/helengine.baseplatform/Manifest/PlatformContainerArtifact.cs`
- Create: `engine/helengine.baseplatform/Manifest/PlatformContainerWritePlan.cs`
- Modify: `engine/helengine.baseplatform/Definitions/PlatformDefinition.cs`
- Modify: `engine/helengine.baseplatform/Definitions/PlatformMediaProfileDefinition.cs`
- Modify: `engine/helengine.baseplatform/Manifest/PlatformBuildArtifact.cs`
- Modify: `engine/helengine.baseplatform/Manifest/PlatformBuildManifest.cs`
- Modify: `engine/helengine.baseplatform/Requests/PlatformBuildRequest.cs`
- Test: `engine/helengine.baseplatform.tests/Definitions/PlatformStorageProfileDefinitionTests.cs`
- Test: `engine/helengine.baseplatform.tests/Manifest/PlatformBuildManifestTests.cs`
- Test: `engine/helengine.baseplatform.tests/Requests/PlatformBuildRequestMultiTargetTests.cs`

### Editor build graph and config

- Create: `engine/helengine.editor/managers/project/EditorBuildStorageProfileDocument.cs`
- Create: `engine/helengine.editor/managers/project/EditorCodeModuleManifestDocument.cs`
- Create: `engine/helengine.editor/managers/project/EditorCodeModuleManifestService.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformArtifactVariantResolver.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformLayoutPlanService.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformContainerWriter.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformLooseFileContainerWriter.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformCodeCookService.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildSelectionModel.cs`
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Modify: `engine/helengine.editor/components/ui/ProfilesDialog.cs`
- Modify: `engine/helengine.editor/model/BuildDialogAddRequest.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildConfigService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildPlatformConfigDocument.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformCodeCookServiceTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformArtifactVariantResolverTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformLayoutPlanServiceTests.cs`

### Runtime/core/files

- Create: `engine/helengine.core/content/RuntimeStorageProfileId.cs`
- Create: `engine/helengine.core/content/RuntimeStartupManifest.cs`
- Create: `engine/helengine.files/containers/IPlatformContainerWriter.cs`
- Create: `engine/helengine.files/containers/LooseFileContainerWriter.cs`
- Create: `engine/helengine.files/containers/SegmentedPackfileContainerWriter.cs`
- Create: `engine/helengine.files/containers/PackfileWritePlan.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeSceneAssetReferenceResolver.cs`
- Modify: `engine/helengine.core/assets/raw/scene/SceneAsset.cs`
- Modify: `engine/helengine.files/assets/AssetSerializer.cs`
- Test: `engine/helengine.files.tests/containers/LooseFileContainerWriterTests.cs`
- Test: `engine/helengine.files.tests/containers/SegmentedPackfileContainerWriterTests.cs`

### Windows runtime/builder

- Modify: `helengine-windows/builder/WindowsPlatformDefinitionFactory.cs`
- Modify: `helengine-windows/builder/WindowsNativeBuildExecutor.cs`
- Modify: `helengine-windows/builder/WindowsBuildWorkspace.cs`
- Modify: `helengine-windows/builder/WindowsPlatformAssetBuilder.cs`
- Modify: `helengine-windows/builder.tests/WindowsPlatformAssetBuilderTests.cs`
- Modify: `helengine-windows/CMakeLists.txt`
- Modify: `helengine-windows/src/platform/windows/win32/win32_application.cpp`
- Modify: `helengine-windows/src/platform/windows/win32/win32_application.hpp`

### PS2 runtime/builder

- Modify: `helengine-ps2/builder/Ps2PlatformDefinitionFactory.cs`
- Modify: `helengine-ps2/builder/Ps2PlatformAssetBuilder.cs`
- Modify: `helengine-ps2/builder.tests/Ps2PlatformAssetBuilderTests.cs`
- Modify: `helengine-ps2/src/platform/ps2/Ps2BootHost.cpp`

### Docs

- Modify: `docs/superpowers/specs/2026-05-03-unified-platform-runtime-build-system-design.md`
- Modify: `helengine-windows/README.md`
- Modify: `helengine-ps2/README.md`

---

### Task 1: Add storage-profile and layout/container contracts to `helengine.baseplatform`

**Files:**
- Create: `engine/helengine.baseplatform/Definitions/PlatformStorageProfileKind.cs`
- Create: `engine/helengine.baseplatform/Definitions/PlatformStorageProfileDefinition.cs`
- Create: `engine/helengine.baseplatform/Manifest/PlatformArtifactPlacement.cs`
- Create: `engine/helengine.baseplatform/Manifest/PlatformContainerArtifact.cs`
- Create: `engine/helengine.baseplatform/Manifest/PlatformContainerWritePlan.cs`
- Modify: `engine/helengine.baseplatform/Definitions/PlatformDefinition.cs`
- Modify: `engine/helengine.baseplatform/Definitions/PlatformMediaProfileDefinition.cs`
- Modify: `engine/helengine.baseplatform/Manifest/PlatformBuildArtifact.cs`
- Modify: `engine/helengine.baseplatform/Manifest/PlatformBuildManifest.cs`
- Modify: `engine/helengine.baseplatform/Requests/PlatformBuildRequest.cs`
- Test: `engine/helengine.baseplatform.tests/Definitions/PlatformStorageProfileDefinitionTests.cs`
- Test: `engine/helengine.baseplatform.tests/Manifest/PlatformBuildManifestTests.cs`
- Test: `engine/helengine.baseplatform.tests/Requests/PlatformBuildRequestMultiTargetTests.cs`

- [ ] **Step 1: Write the failing storage-profile contract test**

```csharp
[Fact]
public void Platform_definition_exposes_storage_and_media_profiles() {
    PlatformDefinition definition = new(
        "windows",
        "Windows",
        [],
        [],
        [],
        [],
        [],
        [
            new PlatformStorageProfileDefinition(
                "loose-files",
                "Loose Files",
                PlatformStorageProfileKind.LooseFiles,
                runtimeSpecializationId: "windows-loose-files",
                allowContainerSegmentation: false)
        ],
        [
            new PlatformMediaProfileDefinition(
                "windows-install-tree",
                "Windows Install Tree",
                PlatformMediaLayoutKind.InstallTree,
                allowPhysicalDuplication: true,
                preferLocalityOverDeduplication: false)
        ]);

    Assert.Equal("loose-files", definition.StorageProfiles[0].ProfileId);
    Assert.Equal("windows-loose-files", definition.StorageProfiles[0].RuntimeSpecializationId);
    Assert.True(definition.MediaProfiles[0].AllowPhysicalDuplication);
}
```

- [ ] **Step 2: Write the failing placement/container manifest test**

```csharp
[Fact]
public void Build_manifest_tracks_artifact_variants_placements_and_container_plan() {
    PlatformBuildManifest manifest = new(
        3,
        "game",
        "1.0.0",
        "1.0.0-engine",
        "main-menu",
        [],
        [],
        [
            new PlatformBuildArtifact("scenes/main-menu.hasset", "scene:main-menu", "sha256:scene", "scene", "shared")
        ],
        [],
        [
            new PlatformArtifactPlacement("scene:main-menu", "shared", "container-0", offsetBytes: 0, lengthBytes: 4096, repeatIndex: 0, placementPriority: 0)
        ],
        new PlatformContainerWritePlan(
            "windows-loose-files",
            [
                new PlatformContainerArtifact("container-0", "install-tree", maxSizeBytes: 0)
            ]));

    Assert.Equal("scene:main-menu", manifest.CookedArtifacts[0].LogicalArtifactId);
    Assert.Single(manifest.ArtifactPlacements);
    Assert.Equal("windows-loose-files", manifest.ContainerWritePlan.RuntimeSpecializationId);
}
```

- [ ] **Step 3: Run the focused tests and verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlatformStorageProfileDefinitionTests|FullyQualifiedName~PlatformBuildManifestTests|FullyQualifiedName~PlatformBuildRequestMultiTargetTests" -v minimal
```

Expected: fail because storage profiles, logical artifact ids, placement entries, and container plans do not exist yet.

- [ ] **Step 4: Implement the shared contracts**

```csharp
namespace helengine.baseplatform.Definitions;

public enum PlatformStorageProfileKind {
    LooseFiles = 0,
    SinglePackfile = 1,
    SegmentedPackfiles = 2,
    DiscLayout = 3
}

public sealed class PlatformStorageProfileDefinition {
    public PlatformStorageProfileDefinition(
        string profileId,
        string displayName,
        PlatformStorageProfileKind storageKind,
        string runtimeSpecializationId,
        bool allowContainerSegmentation) {
        // validate and assign
    }

    public string ProfileId { get; }
    public string DisplayName { get; }
    public PlatformStorageProfileKind StorageKind { get; }
    public string RuntimeSpecializationId { get; }
    public bool AllowContainerSegmentation { get; }
}
```

```csharp
public sealed class PlatformArtifactPlacement {
    public PlatformArtifactPlacement(
        string logicalArtifactId,
        string variantId,
        string containerId,
        long offsetBytes,
        long lengthBytes,
        int repeatIndex,
        int placementPriority) {
        // validate and assign
    }
}
```

```csharp
public class PlatformBuildManifest {
    public PlatformBuildManifest(
        int manifestVersion,
        string projectId,
        string projectVersion,
        string requiredEngineVersion,
        string startupSceneId,
        PlatformBuildScene[] scenes,
        PlatformBuildAsset[] looseAssets,
        PlatformBuildArtifact[] cookedArtifacts,
        PlatformBuildCodeModule[] codeModules,
        PlatformArtifactPlacement[] artifactPlacements,
        PlatformContainerWritePlan containerWritePlan) {
        // validate and assign
    }

    public PlatformArtifactPlacement[] ArtifactPlacements { get; }
    public PlatformContainerWritePlan ContainerWritePlan { get; }
}
```

```csharp
public class PlatformBuildRequest {
    public PlatformBuildRequest(
        PlatformBuildManifest manifest,
        PlatformBuildTargetVariant[] targetVariants,
        PlatformCookProfile[] cookProfiles,
        string outputRoot,
        string workingRoot,
        string selectedBuildProfileId,
        string selectedGraphicsProfileId,
        string selectedCodegenProfileId,
        IReadOnlyDictionary<string, string> selectedBuildOptionValues,
        IReadOnlyDictionary<string, string> selectedGraphicsOptionValues,
        IReadOnlyDictionary<string, string> selectedCodegenOptionValues,
        string generatedCoreCppRootPath = "",
        string selectedMediaProfileId = "",
        string selectedStorageProfileId = "") {
        // validate and assign
    }

    public string SelectedStorageProfileId { get; }
}
```

- [ ] **Step 5: Run the focused tests and verify they pass**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlatformStorageProfileDefinitionTests|FullyQualifiedName~PlatformBuildManifestTests|FullyQualifiedName~PlatformBuildRequestMultiTargetTests" -v minimal
```

Expected: pass.

- [ ] **Step 6: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine add engine/helengine.baseplatform engine/helengine.baseplatform.tests
rtk git -C /mnt/c/dev/helworks/helengine commit -m "feat: add storage and layout build contracts"
```

---

### Task 2: Make platform metadata expose storage profiles, duplication-capable media profiles, and runtime specialization ids

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildSelectionModel.cs`
- Modify: `engine/helengine.platforms/AvailablePlatformDescriptor.cs`
- Modify: `helengine-windows/builder/WindowsPlatformDefinitionFactory.cs`
- Modify: `helengine-ps2/builder/Ps2PlatformDefinitionFactory.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorBuildQueueItemFactoryTests.cs`
- Test: `helengine-windows/builder.tests/WindowsPlatformAssetBuilderTests.cs`
- Test: `helengine-ps2/builder.tests/Ps2PlatformAssetBuilderTests.cs`

- [ ] **Step 1: Write the failing selection-model test**

```csharp
[Fact]
public void Selection_model_exposes_storage_and_media_profile_defaults() {
    PlatformDefinition definition = new(
        "windows",
        "Windows",
        [],
        [],
        [],
        [],
        [],
        [
            new PlatformStorageProfileDefinition("loose-files", "Loose Files", PlatformStorageProfileKind.LooseFiles, "windows-loose-files", false)
        ],
        [
            new PlatformMediaProfileDefinition("windows-install-tree", "Windows Install Tree", PlatformMediaLayoutKind.InstallTree, true, false)
        ]);

    EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(definition);

    Assert.Equal("loose-files", selectionModel.ResolveStorageProfile(string.Empty)?.ProfileId);
    Assert.Equal("windows-install-tree", selectionModel.ResolveMediaProfile(string.Empty)?.ProfileId);
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorBuildQueueItemFactoryTests -v minimal
rtk dotnet test /mnt/c/dev/helworks/helengine-windows/builder.tests/helengine.windows.builder.tests.csproj --filter WindowsPlatformAssetBuilderTests -v minimal
rtk dotnet test /mnt/c/dev/helworks/helengine-ps2/builder.tests/helengine.ps2.builder.tests.csproj --filter Ps2PlatformAssetBuilderTests -v minimal
```

Expected: fail because storage profiles are not part of the selection model and the builders do not fully describe runtime specialization plus duplication-capable media settings.

- [ ] **Step 3: Implement metadata exposure**

```csharp
public sealed class EditorPlatformBuildSelectionModel {
    public PlatformStorageProfileDefinition[] StorageProfiles { get; }
    public PlatformMediaProfileDefinition[] MediaProfiles { get; }

    public PlatformStorageProfileDefinition ResolveStorageProfile(string profileId) {
        // same fallback pattern as codegen/media
    }

    public PlatformMediaProfileDefinition ResolveMediaProfile(string profileId) {
        // same fallback pattern as build/graphics/codegen
    }
}
```

```csharp
new PlatformDefinition(
    "windows",
    "Windows DirectX",
    buildProfiles,
    graphicsProfiles,
    assetRequirements,
    componentCompatibilities,
    codegenProfiles,
    [
        new PlatformStorageProfileDefinition(
            "loose-files",
            "Loose Files",
            PlatformStorageProfileKind.LooseFiles,
            "windows-loose-files",
            allowContainerSegmentation: false)
    ],
    [
        new PlatformMediaProfileDefinition(
            "windows-install-tree",
            "Windows Install Tree",
            PlatformMediaLayoutKind.InstallTree,
            allowPhysicalDuplication: true,
            preferLocalityOverDeduplication: false)
    ]);
```

- [ ] **Step 4: Run the focused tests and verify they pass**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorBuildQueueItemFactoryTests -v minimal
rtk dotnet test /mnt/c/dev/helworks/helengine-windows/builder.tests/helengine.windows.builder.tests.csproj --filter WindowsPlatformAssetBuilderTests -v minimal
rtk dotnet test /mnt/c/dev/helworks/helengine-ps2/builder.tests/helengine.ps2.builder.tests.csproj --filter Ps2PlatformAssetBuilderTests -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine add engine/helengine.editor engine/helengine.platforms
rtk git -C /mnt/c/dev/helworks/helengine-windows add builder builder.tests
rtk git -C /mnt/c/dev/helworks/helengine-ps2 add builder builder.tests
rtk git -C /mnt/c/dev/helworks/helengine commit -m "feat: expose storage-aware platform metadata"
rtk git -C /mnt/c/dev/helworks/helengine-windows commit -m "feat: add windows storage and media metadata"
rtk git -C /mnt/c/dev/helworks/helengine-ps2 commit -m "feat: add ps2 storage and media metadata"
```

---

### Task 3: Persist per-target storage profile, media profile, and module-layout config in the editor

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorBuildStorageProfileDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildPlatformConfigDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs`
- Modify: `engine/helengine.editor/model/BuildDialogAddRequest.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildConfigService.cs`
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Modify: `engine/helengine.editor/components/ui/ProfilesDialog.cs`
- Test: `engine/helengine.editor.tests/BuildDialogTests.cs`
- Test: `engine/helengine.editor.tests/ProfilesDialogTests.cs`

- [ ] **Step 1: Write the failing persistence test**

```csharp
[Fact]
public void Build_config_round_trips_storage_media_and_scene_order() {
    EditorBuildPlatformConfigDocument document = new(
        "windows",
        selectedBuildProfileId: "debug",
        selectedGraphicsProfileId: "directx11",
        selectedCodegenProfileId: "default",
        selectedStorageProfileId: "loose-files",
        selectedMediaProfileId: "windows-install-tree",
        selectedSceneIds: ["Scenes/MainMenu.helen", "Scenes/Level01.helen"]);

    Assert.Equal("loose-files", document.SelectedStorageProfileId);
    Assert.Equal("windows-install-tree", document.SelectedMediaProfileId);
    Assert.Equal("Scenes/MainMenu.helen", document.SelectedSceneIds[0]);
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "BuildDialogTests|ProfilesDialogTests" -v minimal
```

Expected: fail because storage profile and media profile are not persisted in the queue/platform config path.

- [ ] **Step 3: Implement the new persisted fields and UI plumbing**

```csharp
public sealed class EditorBuildPlatformConfigDocument {
    public EditorBuildPlatformConfigDocument(
        string platformId,
        string selectedBuildProfileId,
        string selectedGraphicsProfileId,
        string selectedCodegenProfileId,
        string selectedStorageProfileId,
        string selectedMediaProfileId,
        string[] selectedSceneIds) {
        // validate and assign
    }

    public string SelectedStorageProfileId { get; }
    public string SelectedMediaProfileId { get; }
}
```

```csharp
public sealed class EditorBuildQueueItemDocument {
    public string SelectedStorageProfileId { get; init; } = string.Empty;
    public string SelectedMediaProfileId { get; init; } = string.Empty;
}
```

- [ ] **Step 4: Run the focused tests and verify they pass**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "BuildDialogTests|ProfilesDialogTests" -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine add engine/helengine.editor engine/helengine.editor.tests
rtk git -C /mnt/c/dev/helworks/helengine commit -m "feat: persist storage and media profile selection"
```

---

### Task 4: Add folder-scoped authored code module manifests with nested boundaries

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorCodeModuleManifestDocument.cs`
- Create: `engine/helengine.editor/managers/project/EditorCodeModuleManifestService.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorCodeModuleManifestServiceTests.cs`

- [ ] **Step 1: Write the failing nested-manifest test**

```csharp
[Fact]
public void Load_discovers_nested_code_module_boundaries() {
    EditorCodeModuleManifestService service = new(projectRootPath);

    EditorCodeModuleManifestDocument document = service.Load();

    Assert.Contains(document.Modules, module => module.ModuleId == "gameplay");
    Assert.Contains(document.Modules, module => module.ModuleId == "gameplay.ui");
}
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorCodeModuleManifestServiceTests -v minimal
```

Expected: fail because folder manifests and nested ownership rules are not implemented.

- [ ] **Step 3: Implement authored module discovery**

```csharp
public sealed class EditorCodeModuleManifestEntry {
    public EditorCodeModuleManifestEntry(
        string moduleId,
        string folderPath,
        string[] dependencies,
        string[] defaultLoadScopes) {
        // validate and assign
    }
}
```

```csharp
public sealed class EditorCodeModuleManifestService {
    public EditorCodeModuleManifestDocument Load() {
        // discover manifest files in assets tree
        // nested manifest creates new boundary
        // default one-module fallback when scripts exist but no manifest files do
    }
}
```

- [ ] **Step 4: Run the focused test and verify it passes**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorCodeModuleManifestServiceTests -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine add engine/helengine.editor engine/helengine.editor.tests
rtk git -C /mnt/c/dev/helworks/helengine commit -m "feat: add folder-scoped code module manifests"
```

---

### Task 5: Cook authored code modules through `codegen` and emit runtime-specialized module artifacts

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorPlatformCodeCookService.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorPlatformCodeCookServiceTests.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`

- [ ] **Step 1: Write the failing code-cook test**

```csharp
[Fact]
public void Compile_modules_invokes_codegen_per_module_and_emits_runtime_specialized_records() {
    EditorPlatformCodeCookService service = BuildCodeCookService(recordingRunner);

    PlatformBuildCodeModule[] modules = service.CompileModules(
        manifestDocument,
        platformId: "windows",
        storageProfileId: "loose-files",
        codegenProfile,
        selectedOptions,
        outputRootPath: tempRoot);

    Assert.Single(modules);
    Assert.Equal("gameplay", modules[0].ModuleId);
    Assert.Equal("windows-loose-files", recordingRunner.RuntimeSpecializationId);
}
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformCodeCookServiceTests -v minimal
```

Expected: fail because the code cook service does not yet exist and runtime specialization is not threaded into code generation.

- [ ] **Step 3: Implement the code cook service**

```csharp
internal sealed class EditorPlatformCodeCookService {
    public PlatformBuildCodeModule[] CompileModules(
        EditorCodeModuleManifestDocument manifestDocument,
        string platformId,
        string storageProfileId,
        PlatformCodegenProfileDefinition codegenProfile,
        IReadOnlyDictionary<string, string> selectedOptionValues,
        string codegenToolPath,
        string outputRootPath) {
        // write per-module temp csproj
        // call codegen CLI with platform + storage/runtime specialization
        // emit PlatformBuildCodeModule records
    }
}
```

- [ ] **Step 4: Run the focused test and verify it passes**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformCodeCookServiceTests -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine add engine/helengine.editor engine/helengine.editor.tests
rtk git -C /mnt/c/dev/helworks/helengine commit -m "feat: cook authored code modules"
```

---

### Task 6: Finish runtime `*.hasset` asset cooking and startup-scene metadata

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformCookedArtifactPool.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor/shaders/EditorBuiltInShaderAssetLibrary.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs`

- [ ] **Step 1: Write the failing cook test**

```csharp
[Fact]
public void Cook_scene_build_outputs_runtime_hasset_and_sets_startup_scene_from_order() {
    EditorPlatformAssetCookService service = BuildCookService();

    PlatformBuildManifest manifest = service.Cook(
        platformDefinition,
        orderedSceneIds: ["Scenes/MainMenu.helen", "Scenes/Level01.helen"],
        outputRootPath: tempRoot,
        targetIds: ["windows"]);

    Assert.Equal("Scenes/MainMenu.helen", manifest.StartupSceneId);
    Assert.Contains(manifest.CookedArtifacts, artifact => artifact.RelativePath.EndsWith(".hasset", StringComparison.OrdinalIgnoreCase));
    Assert.DoesNotContain(manifest.CookedArtifacts, artifact => artifact.RelativePath.EndsWith(".obj", StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformAssetCookServiceTests -v minimal
```

Expected: fail because the cook service either does not exist or still depends on raw staging and unstable shader path assumptions.

- [ ] **Step 3: Implement runtime cooking to logical artifacts**

```csharp
internal sealed class EditorPlatformAssetCookService {
    public PlatformBuildManifest Cook(
        PlatformDefinition platformDefinition,
        IReadOnlyList<string> orderedSceneIds,
        string outputRootPath,
        IReadOnlyList<string> targetIds) {
        // package scenes to *.hasset
        // import dependent assets
        // compute logical artifact ids and hashes
        // set startup scene to orderedSceneIds[0]
        // emit empty placement/container placeholders for now
    }
}
```

```csharp
public sealed class PlatformBuildArtifact {
    public PlatformBuildArtifact(
        string relativePath,
        string logicalArtifactId,
        string contentHash,
        string artifactKind,
        string variantId) {
        // validate and assign
    }
}
```

- [ ] **Step 4: Run the focused test and verify it passes**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformAssetCookServiceTests -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine add engine/helengine.editor engine/helengine.editor.tests
rtk git -C /mnt/c/dev/helworks/helengine commit -m "feat: cook runtime assets to hasset"
```

---

### Task 7: Add logical variant resolution and preserve future physical duplication hooks

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorPlatformArtifactVariantResolver.cs`
- Create: `engine/helengine.editor/tests/managers/project/EditorPlatformArtifactVariantResolverTests.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`

- [ ] **Step 1: Write the failing variant-resolution test**

```csharp
[Fact]
public void Resolve_variants_shares_identical_bytes_but_keeps_platform_specific_variants() {
    EditorPlatformArtifactVariantResolver resolver = new();

    EditorResolvedArtifactSet resolved = resolver.Resolve(
        [
            new PlatformBuildArtifact("fonts/default.hasset", "font:default", "sha256:same", "font", "windows"),
            new PlatformBuildArtifact("fonts/default.hasset", "font:default", "sha256:same", "font", "ps2"),
            new PlatformBuildArtifact("textures/ui.hasset", "texture:ui", "sha256:win", "texture", "windows"),
            new PlatformBuildArtifact("textures/ui.hasset", "texture:ui", "sha256:ps2", "texture", "ps2")
        ]);

    Assert.Single(resolved.SharedArtifacts.Where(a => a.LogicalArtifactId == "font:default"));
    Assert.Equal(2, resolved.PlatformVariants.Count(a => a.LogicalArtifactId == "texture:ui"));
}
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformArtifactVariantResolverTests -v minimal
```

Expected: fail because logical identity and variant grouping are not implemented.

- [ ] **Step 3: Implement logical sharing resolution**

```csharp
internal sealed class EditorPlatformArtifactVariantResolver {
    public EditorResolvedArtifactSet Resolve(IReadOnlyList<PlatformBuildArtifact> artifacts) {
        // group by logicalArtifactId + artifactKind + contentHash
        // shared when multiple targets reference identical bytes
        // distinct variant records when bytes differ
    }
}
```

- [ ] **Step 4: Run the focused test and verify it passes**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformArtifactVariantResolverTests -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine add engine/helengine.editor engine/helengine.editor.tests
rtk git -C /mnt/c/dev/helworks/helengine commit -m "feat: resolve shared cooked variants"
```

---

### Task 8: Add physical layout planning and loose-file container writing as the first container mode

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorPlatformLayoutPlanService.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformContainerWriter.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformLooseFileContainerWriter.cs`
- Create: `engine/helengine.files/containers/IPlatformContainerWriter.cs`
- Create: `engine/helengine.files/containers/LooseFileContainerWriter.cs`
- Create: `engine/helengine.files/containers/SegmentedPackfileContainerWriter.cs`
- Create: `engine/helengine.files/containers/PackfileWritePlan.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformLayoutPlanServiceTests.cs`
- Test: `engine/helengine.files.tests/containers/LooseFileContainerWriterTests.cs`
- Test: `engine/helengine.files.tests/containers/SegmentedPackfileContainerWriterTests.cs`

- [ ] **Step 1: Write the failing loose-file layout test**

```csharp
[Fact]
public void Layout_loose_files_creates_one_placement_per_selected_variant() {
    EditorPlatformLayoutPlanService service = new();

    PlatformBuildManifest manifest = service.Plan(
        startupSceneId: "Scenes/MainMenu.helen",
        storageProfileId: "loose-files",
        mediaProfileId: "windows-install-tree",
        cookedArtifacts: [
            new PlatformBuildArtifact("scenes/Scenes/MainMenu.hasset", "scene:main-menu", "sha256:scene", "scene", "shared")
        ]);

    Assert.Single(manifest.ArtifactPlacements);
    Assert.Equal("windows-loose-files", manifest.ContainerWritePlan.RuntimeSpecializationId);
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformLayoutPlanServiceTests -v minimal
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.files.tests/helengine.files.tests.csproj --filter "LooseFileContainerWriterTests|SegmentedPackfileContainerWriterTests" -v minimal
```

Expected: fail because no explicit placement or container-write layer exists.

- [ ] **Step 3: Implement loose-file planning and future packfile hooks**

```csharp
internal sealed class EditorPlatformLayoutPlanService {
    public PlatformBuildManifest Plan(
        PlatformBuildManifest cookedManifest,
        PlatformStorageProfileDefinition storageProfile,
        PlatformMediaProfileDefinition mediaProfile) {
        // loose-files: create one placement per chosen variant
        // segmented-packfiles: create placeholder container plan structure only
        // no real duplication heuristics yet
    }
}
```

```csharp
public sealed class LooseFileContainerWriter : IPlatformContainerWriter {
    public void Write(PlatformContainerWritePlan plan, IReadOnlyList<PlatformArtifactPlacement> placements, string stagingRoot, string outputRoot) {
        // map each placement to one loose runtime file
    }
}
```

- [ ] **Step 4: Run the focused tests and verify they pass**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformLayoutPlanServiceTests -v minimal
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.files.tests/helengine.files.tests.csproj --filter "LooseFileContainerWriterTests|SegmentedPackfileContainerWriterTests" -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine add engine/helengine.editor engine/helengine.files engine/helengine.editor.tests engine/helengine.files.tests
rtk git -C /mnt/c/dev/helworks/helengine commit -m "feat: add layout and container writing layer"
```

---

### Task 9: Specialize generated runtime and Windows host bootstrap by platform plus storage profile

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs`
- Create: `engine/helengine.core/content/RuntimeStorageProfileId.cs`
- Create: `engine/helengine.core/content/RuntimeStartupManifest.cs`
- Modify: `helengine-windows/builder/WindowsNativeBuildExecutor.cs`
- Modify: `helengine-windows/builder/WindowsBuildWorkspace.cs`
- Modify: `helengine-windows/CMakeLists.txt`
- Modify: `helengine-windows/src/platform/windows/win32/win32_application.cpp`
- Modify: `helengine-windows/src/platform/windows/win32/win32_application.hpp`
- Test: `helengine-windows/builder.tests/WindowsPlatformAssetBuilderTests.cs`

- [ ] **Step 1: Write the failing Windows startup test**

```csharp
[Fact]
public async Task Build_async_embeds_runtime_specialization_and_startup_scene_path() {
    WindowsPlatformAssetBuilder builder = new(recordingNativeExecutor);
    PlatformBuildRequest request = BuildCookedRequest(
        startupSceneId: "Scenes/MainMenu.helen",
        storageProfileId: "loose-files",
        runtimeSpecializationId: "windows-loose-files");

    PlatformBuildReport report = await builder.BuildAsync(request, progress, diagnostics, CancellationToken.None);

    Assert.True(report.Succeeded);
    Assert.Equal("scenes/Scenes/MainMenu.hasset", recordingNativeExecutor.StartupSceneRelativePath);
}
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine-windows/builder.tests/helengine.windows.builder.tests.csproj --filter WindowsPlatformAssetBuilderTests -v minimal
```

Expected: fail because startup still depends on hardcoded paths or runtime specialization is not carried into the host compile.

- [ ] **Step 3: Implement runtime specialization plumbing**

```csharp
public sealed class RuntimeStartupManifest {
    public RuntimeStartupManifest(string runtimeSpecializationId, string startupSceneRelativePath) {
        // validate and assign
    }
}
```

```csharp
internal interface IWindowsNativeBuildExecutor {
    string Build(
        string repositoryRoot,
        string buildRoot,
        string generatedCoreCppRootPath,
        string startupSceneRelativePath,
        string runtimeSpecializationId,
        CancellationToken cancellationToken);
}
```

```cpp
#ifndef HELENGINE_WINDOWS_STARTUP_SCENE_RELATIVE_PATH
#define HELENGINE_WINDOWS_STARTUP_SCENE_RELATIVE_PATH ""
#endif

#ifndef HELENGINE_WINDOWS_RUNTIME_SPECIALIZATION_ID
#define HELENGINE_WINDOWS_RUNTIME_SPECIALIZATION_ID ""
#endif
```

- [ ] **Step 4: Run the focused test and verify it passes**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine-windows/builder.tests/helengine.windows.builder.tests.csproj --filter WindowsPlatformAssetBuilderTests -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine add engine/helengine.core engine/helengine.editor
rtk git -C /mnt/c/dev/helworks/helengine-windows add builder builder.tests CMakeLists.txt src/platform/windows/win32
rtk git -C /mnt/c/dev/helworks/helengine commit -m "feat: specialize runtime by storage profile"
rtk git -C /mnt/c/dev/helworks/helengine-windows commit -m "feat: embed startup scene and runtime specialization"
```

---

### Task 10: Move PS2 onto the same cooked-manifest, startup-scene, and storage-profile contract

**Files:**
- Modify: `helengine-ps2/builder/Ps2PlatformAssetBuilder.cs`
- Modify: `helengine-ps2/builder/Ps2PlatformDefinitionFactory.cs`
- Modify: `helengine-ps2/builder.tests/Ps2PlatformAssetBuilderTests.cs`
- Modify: `helengine-ps2/src/platform/ps2/Ps2BootHost.cpp`

- [ ] **Step 1: Write the failing PS2 contract test**

```csharp
[Fact]
public async Task Ps2_builder_accepts_storage_profile_and_startup_scene_from_manifest() {
    Ps2PlatformAssetBuilder builder = new();
    PlatformBuildRequest request = BuildCookedPs2Request(
        startupSceneId: "Scenes/MainMenu.helen",
        storageProfileId: "disc-layout",
        mediaProfileId: "ps2-install-tree");

    PlatformBuildReport report = await builder.BuildAsync(request, progress, diagnostics, CancellationToken.None);

    Assert.True(report.Succeeded);
    Assert.Contains(builder.Definition.StorageProfiles, profile => profile.ProfileId == "disc-layout");
}
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine-ps2/builder.tests/helengine.ps2.builder.tests.csproj --filter Ps2PlatformAssetBuilderTests -v minimal
```

Expected: fail because PS2 is still not fully aligned to the new storage/runtime contract.

- [ ] **Step 3: Implement the PS2 alignment**

```csharp
new PlatformDefinition(
    "ps2",
    "PlayStation 2",
    buildProfiles,
    graphicsProfiles,
    assetRequirements,
    componentCompatibilities,
    codegenProfiles,
    [
        new PlatformStorageProfileDefinition(
            "disc-layout",
            "Disc Layout",
            PlatformStorageProfileKind.DiscLayout,
            "ps2-disc-layout",
            allowContainerSegmentation: true)
    ],
    mediaProfiles);
```

- [ ] **Step 4: Run the focused test and verify it passes**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine-ps2/builder.tests/helengine.ps2.builder.tests.csproj --filter Ps2PlatformAssetBuilderTests -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine-ps2 add builder builder.tests src/platform/ps2
rtk git -C /mnt/c/dev/helworks/helengine-ps2 commit -m "refactor: align ps2 with runtime storage profiles"
```

---

### Task 11: Integrate the full shared graph in the editor and expose per-phase outputs

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildExecutionResult.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildExecutorGraphTests.cs`

- [ ] **Step 1: Write the failing orchestration test**

```csharp
[Fact]
public void Execute_runs_regen_cook_code_variants_layout_container_and_package_in_order() {
    FakeBuildGraphRunner runner = new();
    EditorPlatformBuildExecutor executor = BuildExecutor(runner);

    EditorBuildExecutionResult result = executor.Execute(queueItem);

    Assert.True(result.Succeeded);
    Assert.Equal(
        ["regenerate-core", "cook-assets", "cook-code", "resolve-variants", "compute-layout", "write-container", "package-platform"],
        runner.CompletedPhaseIds);
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "EditorPlatformBuildGraphRunnerTests|EditorPlatformBuildExecutorGraphTests" -v minimal
```

Expected: fail because the graph runner does not yet model the full phase sequence and result data.

- [ ] **Step 3: Finish the graph runner and result model**

```csharp
internal enum EditorPlatformBuildPhase {
    RegenerateCore,
    CookAssets,
    CookCode,
    ResolveVariants,
    ComputeLayout,
    WriteContainer,
    PackagePlatform
}
```

```csharp
public sealed class EditorBuildExecutionResult {
    public string OutputRootPath { get; init; } = string.Empty;
    public string StartupSceneId { get; init; } = string.Empty;
    public string RuntimeSpecializationId { get; init; } = string.Empty;
    public IReadOnlyDictionary<EditorPlatformBuildPhase, string> PhaseLogPaths { get; init; }
        = new Dictionary<EditorPlatformBuildPhase, string>();
}
```

- [ ] **Step 4: Run the focused tests and verify they pass**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "EditorPlatformBuildGraphRunnerTests|EditorPlatformBuildExecutorGraphTests" -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine add engine/helengine.editor engine/helengine.editor.tests
rtk git -C /mnt/c/dev/helworks/helengine commit -m "feat: integrate unified platform build graph"
```

---

### Task 12: Preserve future packfile, segmented-container, duplication, and shared-medium hooks without implementing streaming

**Files:**
- Modify: `docs/superpowers/specs/2026-05-03-unified-platform-runtime-build-system-design.md`
- Modify: `helengine-windows/README.md`
- Modify: `helengine-ps2/README.md`
- Test: `engine/helengine.files.tests/containers/SegmentedPackfileContainerWriterTests.cs`

- [ ] **Step 1: Write the failing future-hook documentation check**

```bash
rtk sh -lc 'rg -n "startup.helen|never duplicate|only loose files|single-target only" /mnt/c/dev/helworks/helengine/docs/superpowers/specs/2026-05-03-unified-platform-runtime-build-system-design.md /mnt/c/dev/helworks/helengine-windows/README.md /mnt/c/dev/helworks/helengine-ps2/README.md'
```

Expected: fail if any documentation still presents obsolete assumptions.

- [ ] **Step 2: Add explicit future-hook tests for segmented container planning**

```csharp
[Fact]
public void Segmented_packfile_writer_exposes_chunked_plan_shape_without_streaming_policy() {
    PackfileWritePlan plan = new("container-0", maxSegmentSizeBytes: 1_073_741_824);

    Assert.Equal(1_073_741_824, plan.MaxSegmentSizeBytes);
}
```

- [ ] **Step 3: Update docs and verify**

Run:

```bash
rtk sh -lc 'rg -n "storage/runtime profile|packfile|segmented packfiles|physical duplication|shared-medium aggregation" /mnt/c/dev/helworks/helengine/docs/superpowers/specs/2026-05-03-unified-platform-runtime-build-system-design.md /mnt/c/dev/helworks/helengine-windows/README.md /mnt/c/dev/helworks/helengine-ps2/README.md'
```

Expected: the docs reflect the new runtime-storage, duplication, and future-container model.

- [ ] **Step 4: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine add docs/superpowers/specs/2026-05-03-unified-platform-runtime-build-system-design.md
rtk git -C /mnt/c/dev/helworks/helengine-windows add README.md
rtk git -C /mnt/c/dev/helworks/helengine-ps2 add README.md
rtk git -C /mnt/c/dev/helworks/helengine commit -m "docs: clarify unified runtime build rollout"
rtk git -C /mnt/c/dev/helworks/helengine-windows commit -m "docs: update windows runtime build notes"
rtk git -C /mnt/c/dev/helworks/helengine-ps2 commit -m "docs: update ps2 runtime build notes"
```

---

## Spec Coverage Check

- Shared multi-platform build graph: covered by Tasks 1, 2, 3, 11.
- Runtime specialization by `platform + storage/runtime profile`: covered by Tasks 1, 2, 5, 9, 10.
- Stable logical asset ids and cooked variants: covered by Tasks 1, 6, 7.
- Physical placement and container abstraction: covered by Tasks 1 and 8.
- Scene order driving startup and future layout: covered by Tasks 3, 6, 8, 9.
- Folder-scoped code module manifests with nested boundaries: covered by Task 4.
- Per-platform module configuration and runtime code cooking: covered by Tasks 3 and 5.
- Windows and PS2 unified under one contract: covered by Tasks 2, 9, 10, 11.
- Future hooks for duplication, segmented packfiles, and shared-medium aggregation: covered by Tasks 1, 8, 12.

## Notes

- This plan intentionally separates **first useful rollout** work from **future-preserving architecture hooks**.
- Streaming heuristics, DLC, and full dynamic code unloading are not implemented by this plan, but the contract and container/layout layers they require are introduced early so later work does not require another architecture reset.
