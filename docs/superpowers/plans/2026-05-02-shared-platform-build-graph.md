# Shared Platform Build Graph Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move Windows and PS2 onto one editor-owned build graph that regenerates generated core every build, cooks all runtime content into `*.hasset`, compiles authored code modules, resolves shared versus platform-specific artifacts, and packages per-target outputs with explicit startup-scene metadata.

**Architecture:** The editor becomes the single orchestrator for a multi-target build graph. `helengine.baseplatform` carries the shared target, media, artifact, and startup-scene contracts. `helengine.editor` owns phase execution, logging, cooked artifact pooling, asset cooking, and authored-code module compilation. Platform builders consume prepared manifests and packaged inputs instead of owning codegen or editor-side cooking. The first implementation moves Windows and PS2 onto the same graph, but only Windows-native packaging needs to be considered complete in this rollout.

**Tech Stack:** C# / .NET 9, xUnit, `helengine.baseplatform`, `helengine.editor`, `helengine.files`, `helengine.core`, `csharpcodegen`, Windows builder, PS2 builder, existing dynamic platform metadata system.

---

### Task 1: Extend `helengine.baseplatform` for the shared build graph contract

**Files:**
- Create: `engine/helengine.baseplatform/Definitions/PlatformMediaLayoutKind.cs`
- Create: `engine/helengine.baseplatform/Definitions/PlatformMediaProfileDefinition.cs`
- Create: `engine/helengine.baseplatform/Manifest/PlatformBuildArtifact.cs`
- Create: `engine/helengine.baseplatform/Manifest/PlatformBuildCodeModule.cs`
- Modify: `engine/helengine.baseplatform/Definitions/PlatformDefinition.cs`
- Modify: `engine/helengine.baseplatform/Manifest/PlatformBuildManifest.cs`
- Modify: `engine/helengine.baseplatform/Manifest/PlatformBuildScene.cs`
- Modify: `engine/helengine.baseplatform/Requests/PlatformBuildRequest.cs`
- Create: `engine/helengine.baseplatform.tests/Manifest/PlatformBuildManifestTests.cs`
- Create: `engine/helengine.baseplatform.tests/Requests/PlatformBuildRequestMultiTargetTests.cs`

- [ ] **Step 1: Write the failing contract tests**

```csharp
[Fact]
public void Platform_definition_exposes_media_profiles() {
    PlatformDefinition definition = new(
        "windows",
        "Windows",
        buildProfiles,
        graphicsProfiles,
        assetRequirements,
        codegenProfiles,
        [
            new PlatformMediaProfileDefinition(
                "install-tree",
                "Install Tree",
                PlatformMediaLayoutKind.InstallTree,
                allowPhysicalDuplication: false,
                preferLocalityOverDeduplication: false)
        ],
        componentCompatibilities);

    Assert.Equal("install-tree", definition.MediaProfiles[0].ProfileId);
    Assert.Equal(PlatformMediaLayoutKind.InstallTree, definition.MediaProfiles[0].LayoutKind);
}
```

```csharp
[Fact]
public void Build_manifest_tracks_startup_scene_artifacts_and_code_modules() {
    PlatformBuildManifest manifest = new(
        2,
        "game",
        "1.0.0",
        "1.0.0-engine",
        startupSceneId: "main-menu",
        scenes: [
            new PlatformBuildScene(
                "main-menu",
                "Main Menu",
                "scenes/main-menu",
                [new PlatformBuildPayloadReference("scenes/main-menu", "scenes/main-menu")],
                [new KeyValuePair<string, string>("build-order-index", "0")])
        ],
        looseAssets: [],
        cookedArtifacts: [
            new PlatformBuildArtifact("fonts/default.hasset", "sha256:abc", "font", "shared")
        ],
        codeModules: [
            new PlatformBuildCodeModule("gameplay", "gameplay", ["always-loaded"], ["core"])
        ]);

    Assert.Equal("main-menu", manifest.StartupSceneId);
    Assert.Single(manifest.CookedArtifacts);
    Assert.Single(manifest.CodeModules);
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlatformBuildManifestTests|FullyQualifiedName~PlatformBuildRequestMultiTargetTests" -v minimal
```

Expected: fail because media profiles, startup-scene metadata, cooked artifact records, and code-module records do not exist yet.

- [ ] **Step 3: Implement the shared build-graph contract**

Add the new media-profile types:

```csharp
namespace helengine.baseplatform.Definitions;

public enum PlatformMediaLayoutKind {
    InstallTree = 0,
    DiscImage = 1
}

public sealed class PlatformMediaProfileDefinition {
    public PlatformMediaProfileDefinition(
        string profileId,
        string displayName,
        PlatformMediaLayoutKind layoutKind,
        bool allowPhysicalDuplication,
        bool preferLocalityOverDeduplication) {
        // validate and assign
    }

    public string ProfileId { get; }
    public string DisplayName { get; }
    public PlatformMediaLayoutKind LayoutKind { get; }
    public bool AllowPhysicalDuplication { get; }
    public bool PreferLocalityOverDeduplication { get; }
}
```

Extend the manifest contract:

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
        PlatformBuildCodeModule[] codeModules) {
        // validate and assign
    }

    public string StartupSceneId { get; }
    public PlatformBuildArtifact[] CookedArtifacts { get; }
    public PlatformBuildCodeModule[] CodeModules { get; }
}
```

Add a media-profile selection to `PlatformBuildRequest` and expose multi-target artifact metadata cleanly instead of forcing builders to rediscover it from the staging tree.

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlatformBuildManifestTests|FullyQualifiedName~PlatformBuildRequestMultiTargetTests" -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine add engine/helengine.baseplatform engine/helengine.baseplatform.tests
rtk git -C /mnt/c/dev/helworks/helengine commit -m "feat: add shared build graph contracts"
```

---

### Task 2: Add an editor-owned build graph runner with per-phase logs and workspaces

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorPlatformBuildPhase.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphWorkspace.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphWorkspaceFactory.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildExecutionResult.cs`

- [ ] **Step 1: Write the failing orchestration test**

```csharp
[Fact]
public void Execute_runs_regen_cook_package_and_compile_in_order() {
    FakeBuildGraphRunner runner = new();
    EditorPlatformBuildExecutor executor = BuildExecutor(runner);

    EditorBuildExecutionResult result = executor.Execute(queueItem);

    Assert.True(result.Succeeded);
    Assert.Equal(
        ["regenerate-core", "cook-assets", "compile-code", "package-platform"],
        runner.CompletedPhaseIds);
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformBuildGraphRunnerTests -v minimal
```

Expected: fail because no shared graph runner or phase model exists yet.

- [ ] **Step 3: Implement the graph runner and workspace**

Create a small phase model:

```csharp
internal enum EditorPlatformBuildPhase {
    RegenerateCore,
    CookAssets,
    CompileCode,
    ResolveVariants,
    LayoutMedia,
    PackagePlatform
}
```

Create a workspace object that owns predictable per-phase directories and logs:

```csharp
internal sealed class EditorPlatformBuildGraphWorkspace {
    public string ExecutionRootPath { get; }
    public string GeneratedCoreRootPath { get; }
    public string CookRootPath { get; }
    public string CodeRootPath { get; }
    public string VariantRootPath { get; }
    public string LayoutRootPath { get; }
    public string PackageRootPath { get; }
    public string GetLogPath(EditorPlatformBuildPhase phase) { /* map to regen.log, cook.log, etc. */ }
}
```

Refactor `EditorPlatformBuildExecutor` so it delegates phase sequencing to `EditorPlatformBuildGraphRunner` instead of directly regenerating and packaging inline.

- [ ] **Step 4: Run the test and verify it passes**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformBuildGraphRunnerTests -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine add engine/helengine.editor/managers/project engine/helengine.editor.tests/managers/project
rtk git -C /mnt/c/dev/helworks/helengine commit -m "feat: add editor build graph runner"
```

---

### Task 3: Replace raw staging with a real `*.hasset` cook pipeline and startup-scene metadata

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformCookedArtifactPool.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackagerResult.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildSceneOrderDocument.cs`

- [ ] **Step 1: Write the failing cook test**

```csharp
[Fact]
public void Cook_scene_build_outputs_runtime_hasset_and_sets_startup_scene_from_order() {
    EditorPlatformAssetCookService service = BuildCookService();

    PlatformBuildManifest manifest = service.Cook(
        sceneOrder: ["main-menu.scene", "level-1.scene"],
        outputRootPath: tempRoot,
        targetIds: ["windows"]);

    Assert.Equal("main-menu.scene", manifest.StartupSceneId);
    Assert.Contains(manifest.CookedArtifacts, artifact => artifact.RelativePath.EndsWith(".hasset"));
    Assert.DoesNotContain(manifest.CookedArtifacts, artifact => artifact.RelativePath.EndsWith(".obj"));
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformAssetCookServiceTests -v minimal
```

Expected: fail because there is no cook service and scene packaging still depends on the old staging flow.

- [ ] **Step 3: Implement cooking to `*.hasset`**

Introduce a cook service that:

- reads the build-scene order document
- marks the first scene as startup scene
- serializes cooked scene outputs to `*.hasset`
- imports source assets into cooked runtime assets
- records cooked artifact metadata in the manifest

The service should expose a narrow entry point:

```csharp
internal sealed class EditorPlatformAssetCookService {
    public PlatformBuildManifest Cook(
        IReadOnlyList<string> orderedSceneIds,
        string outputRootPath,
        IReadOnlyList<string> targetIds) {
        // cook scenes and dependent assets into *.hasset
    }
}
```

Update `EditorWindowsBuildScenePackager` so it becomes a helper used by the cook service instead of the top-level build driver.

- [ ] **Step 4: Run the test and verify it passes**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformAssetCookServiceTests -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine add engine/helengine.editor/managers/project engine/helengine.editor.tests/managers/project
rtk git -C /mnt/c/dev/helworks/helengine commit -m "feat: cook runtime assets to hasset"
```

---

### Task 4: Add variant resolution and shared artifact pooling across targets

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorPlatformArtifactVariantResolver.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorPlatformArtifactVariantResolverTests.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformCookedArtifactPool.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`

- [ ] **Step 1: Write the failing sharing test**

```csharp
[Fact]
public void Resolve_variants_shares_identical_cooked_bytes_and_splits_different_bytes() {
    EditorPlatformArtifactVariantResolver resolver = new();

    EditorResolvedArtifactSet resolved = resolver.Resolve(
        [
            CookedArtifact("windows", "fonts/default.hasset", "sha256:same", "font"),
            CookedArtifact("ps2", "fonts/default.hasset", "sha256:same", "font"),
            CookedArtifact("windows", "textures/ui.hasset", "sha256:win", "texture"),
            CookedArtifact("ps2", "textures/ui.hasset", "sha256:ps2", "texture")
        ]);

    Assert.Single(resolved.SharedArtifacts.Where(a => a.ContentHash == "sha256:same"));
    Assert.Equal(2, resolved.PlatformVariants.Count(a => a.ArtifactKind == "texture"));
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformArtifactVariantResolverTests -v minimal
```

Expected: fail because the variant resolver does not exist yet.

- [ ] **Step 3: Implement the resolver**

Add a small resolver that groups cooked artifacts by:

- artifact kind
- runtime format identity
- content hash

Suggested shape:

```csharp
internal sealed class EditorPlatformArtifactVariantResolver {
    public EditorResolvedArtifactSet Resolve(IReadOnlyList<PlatformBuildArtifact> artifacts) {
        // shared if format + bytes match, variant otherwise
    }
}
```

Wire the graph runner so `ResolveVariants` becomes a real phase between cooking and packaging.

- [ ] **Step 4: Run the test and verify it passes**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformArtifactVariantResolverTests -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine add engine/helengine.editor/managers/project engine/helengine.editor.tests/managers/project
rtk git -C /mnt/c/dev/helworks/helengine commit -m "feat: resolve shared and platform-specific artifacts"
```

---

### Task 5: Add authored code-module manifest support and initial code compilation phase

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorCodeModuleManifestDocument.cs`
- Create: `engine/helengine.editor/managers/project/EditorCodeModuleManifestService.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformCodeCookService.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorPlatformCodeCookServiceTests.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`

- [ ] **Step 1: Write the failing module test**

```csharp
[Fact]
public void Compile_code_modules_reads_manifest_and_emits_module_records() {
    EditorPlatformCodeCookService service = BuildCodeCookService();

    PlatformBuildCodeModule[] modules = service.CompileModules(
        new EditorCodeModuleManifestDocument(
            [
                new EditorCodeModuleManifestEntry("gameplay", ["Scripts"], ["always-loaded"], [])
            ]),
        outputRootPath: tempRoot,
        codegenProfileId: "windows-cpp");

    Assert.Single(modules);
    Assert.Equal("gameplay", modules[0].ModuleId);
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformCodeCookServiceTests -v minimal
```

Expected: fail because no module manifest or code cook service exists yet.

- [ ] **Step 3: Implement the initial code-module phase**

Create a project-local manifest document:

```csharp
public sealed class EditorCodeModuleManifestDocument {
    public EditorCodeModuleManifestDocument(EditorCodeModuleManifestEntry[] modules) {
        Modules = modules ?? [];
    }

    public EditorCodeModuleManifestEntry[] Modules { get; }
}
```

Add a code cook service that:

- reads the manifest
- maps code roots to logical modules
- invokes `csharpcodegen` for the selected target contract
- emits `PlatformBuildCodeModule` metadata for packaging

The first implementation may emit one coarse runtime artifact per manifest entry. It does not need to implement dynamic load/unload yet.

- [ ] **Step 4: Run the test and verify it passes**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformCodeCookServiceTests -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine add engine/helengine.editor/managers/project engine/helengine.editor.tests/managers/project
rtk git -C /mnt/c/dev/helworks/helengine commit -m "feat: add authored code module cooking"
```

---

### Task 6: Update the Windows builder to consume cooked manifests instead of raw staging

**Files:**
- Modify: `helengine-windows/builder/WindowsPlatformDefinitionFactory.cs`
- Modify: `helengine-windows/builder/WindowsPlatformAssetBuilder.cs`
- Modify: `helengine-windows/builder/WindowsBuildWorkspace.cs`
- Modify: `helengine-windows/builder/WindowsNativeBuildExecutor.cs`
- Create: `helengine-windows/builder.tests/WindowsBuildWorkspaceCookedManifestTests.cs`

- [ ] **Step 1: Write the failing Windows builder test**

```csharp
[Fact]
public async Task Build_async_copies_only_cooked_hasset_outputs_and_uses_manifest_startup_scene() {
    WindowsPlatformAssetBuilder builder = new();
    PlatformBuildRequest request = BuildCookedRequest(
        startupSceneId: "main-menu.scene",
        cookedArtifacts: [
            new PlatformBuildArtifact("scenes/main-menu.hasset", "sha256:scene", "scene", "shared"),
            new PlatformBuildArtifact("models/sponza.hasset", "sha256:model", "model", "shared")
        ]);

    PlatformBuildReport report = await builder.BuildAsync(request, progress, diagnostics, CancellationToken.None);

    Assert.True(report.Succeeded);
    Assert.True(File.Exists(Path.Combine(request.OutputRoot, "scenes", "main-menu.hasset")));
    Assert.False(File.Exists(Path.Combine(request.OutputRoot, "Models", "Sponza.obj")));
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine-windows/builder.tests/helengine.windows.builder.tests.csproj -v minimal --filter WindowsBuildWorkspaceCookedManifestTests
```

Expected: fail because the Windows builder still assumes staged payload trees instead of cooked artifact manifests.

- [ ] **Step 3: Update the Windows builder**

Refactor the Windows builder to:

- consume `Manifest.CookedArtifacts`
- read `Manifest.StartupSceneId`
- stop copying raw source payloads into the final package
- package only cooked `*.hasset` outputs and the native executable artifacts

The package root should be driven from the cooked manifest, not from directory walks over editor staging payloads.

- [ ] **Step 4: Run the test and verify it passes**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine-windows/builder.tests/helengine.windows.builder.tests.csproj -v minimal --filter WindowsBuildWorkspaceCookedManifestTests
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine-windows add builder builder.tests
rtk git -C /mnt/c/dev/helworks/helengine-windows commit -m "refactor: consume cooked build manifests on windows"
```

---

### Task 7: Move PS2 onto the same graph contract and media-profile selection

**Files:**
- Modify: `helengine-ps2/builder/Ps2PlatformDefinitionFactory.cs`
- Modify: `helengine-ps2/builder/Ps2PlatformAssetBuilder.cs`
- Create: `helengine-ps2/builder.tests/Ps2PlatformAssetBuilderCookedManifestTests.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildSelectionModel.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs`

- [ ] **Step 1: Write the failing PS2 graph test**

```csharp
[Fact]
public async Task Ps2_builder_accepts_cooked_manifest_and_ps2_media_profile() {
    Ps2PlatformAssetBuilder builder = new();
    PlatformBuildRequest request = BuildCookedPs2Request(mediaProfileId: "ps2-install-tree");

    PlatformBuildReport report = await builder.BuildAsync(request, progress, diagnostics, CancellationToken.None);

    Assert.True(report.Succeeded);
    Assert.Contains(builder.Definition.MediaProfiles, profile => profile.ProfileId == "ps2-install-tree");
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine-ps2/builder.tests/helengine.ps2.builder.tests.csproj -v minimal --filter Ps2PlatformAssetBuilderCookedManifestTests
```

Expected: fail because the PS2 builder does not yet expose or consume the shared media-profile contract.

- [ ] **Step 3: Update the PS2 builder contract**

Add media profile metadata in `Ps2PlatformDefinitionFactory` and update the builder so it accepts the same cooked manifest and startup-scene contract as Windows. The first implementation does not need a disc-layout phase inside PS2 yet, but it must no longer rely on editor-specific staging assumptions.

- [ ] **Step 4: Run the test and verify it passes**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine-ps2/builder.tests/helengine.ps2.builder.tests.csproj -v minimal --filter Ps2PlatformAssetBuilderCookedManifestTests
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine-ps2 add builder builder.tests
rtk git -C /mnt/c/dev/helworks/helengine-ps2 commit -m "refactor: align ps2 builder with shared build graph"
```

---

### Task 8: Add end-to-end editor tests for Windows and PS2 on the shared graph

**Files:**
- Create: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildExecutorGraphTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorBuildQueueItemFactoryTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformCodeCookServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`

- [ ] **Step 1: Write the failing end-to-end test**

```csharp
[Fact]
public void Execute_builds_windows_and_ps2_through_the_same_graph_with_separate_phase_logs() {
    EditorPlatformBuildExecutor windowsExecutor = BuildExecutor("windows");
    EditorPlatformBuildExecutor ps2Executor = BuildExecutor("ps2");

    EditorBuildExecutionResult windowsResult = windowsExecutor.Execute(windowsQueueItem);
    EditorBuildExecutionResult ps2Result = ps2Executor.Execute(ps2QueueItem);

    Assert.True(windowsResult.Succeeded);
    Assert.True(ps2Result.Succeeded);
    Assert.Contains("regen.log", windowsResult.Message);
    Assert.Contains("regen.log", ps2Result.Message);
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformBuildExecutorGraphTests -v minimal
```

Expected: fail because the executor result and graph wiring do not yet expose the complete shared-graph behavior.

- [ ] **Step 3: Finish the result model and summaries**

Update `EditorBuildExecutionResult` and the executor result path so each completed target reports:

- output root
- startup scene id
- generated-core path
- per-phase log paths

The test does not need a real native Windows or PS2 toolchain. It can assert on the orchestrated build graph and output model.

- [ ] **Step 4: Run the test and verify it passes**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter EditorPlatformBuildExecutorGraphTests -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine add engine/helengine.editor.tests engine/helengine.editor/managers/project
rtk git -C /mnt/c/dev/helworks/helengine commit -m "test: verify shared platform build graph"
```

---

### Task 9: Verify the full graph and update platform installation metadata

**Files:**
- Modify: `user_settings/platforms.json`
- Modify: `engine/helengine.platforms/AvailablePlatformDescriptor.cs`
- Modify: `engine/helengine.platforms/PlatformInstallationEntry.cs`
- Modify: `engine/helengine.platforms/PlatformInstallationStore.cs`
- Modify: `engine/helengine.platforms/PlatformInstallationResolver.cs`

- [ ] **Step 1: Add the failing metadata test**

```csharp
[Fact]
public void Platform_installation_entry_tracks_codegen_and_media_ready_tool_paths() {
    PlatformInstallationEntry entry = new(
        "windows",
        "Windows",
        "builder.dll",
        "generated-core",
        "codegen.exe");

    Assert.Equal("windows", entry.PlatformId);
    Assert.Equal("codegen.exe", entry.CodegenToolPath);
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.platforms.tests/helengine.platforms.tests.csproj --filter PlatformInstallationEntryTests -v minimal
```

Expected: fail if the current installation metadata cannot represent the final graph inputs cleanly.

- [ ] **Step 3: Finalize metadata and run full verification**

Make sure the installation metadata exposes everything the graph needs for Windows and PS2:

- builder assembly path
- generated-core root
- codegen tool path
- any media-profile-sensitive tool paths needed later

Run:

```bash
rtk dotnet build /mnt/c/dev/helworks/helengine/engine/helengine.editor/helengine.editor.csproj --no-restore
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj -v minimal
rtk dotnet test /mnt/c/dev/helworks/helengine-windows/builder.tests/helengine.windows.builder.tests.csproj -v minimal
rtk dotnet test /mnt/c/dev/helworks/helengine-ps2/builder.tests/helengine.ps2.builder.tests.csproj -v minimal
```

Expected: all editor and builder tests pass on the shared graph.

- [ ] **Step 4: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine add user_settings/platforms.json engine/helengine.platforms engine/helengine.editor engine/helengine.editor.tests
rtk git -C /mnt/c/dev/helworks/helengine commit -m "feat: wire shared platform build graph metadata"
```

---

### Task 10: Document the first implementation limits and rollout policy

**Files:**
- Modify: `docs/superpowers/specs/2026-05-02-shared-platform-build-graph-design.md`
- Modify: `helengine-windows/README.md`
- Modify: `helengine-ps2/README.md`

- [ ] **Step 1: Write the docs update**

Document the first implementation boundary explicitly:

- Windows and PS2 both use the shared graph
- Windows packaging is the required fully working target
- PS2 integration must consume the same graph contract, but advanced disc-layout duplication can remain future work
- authored code modules are manifest-driven but may still be coarse-grained in the first rollout

- [ ] **Step 2: Verify the docs reflect the implementation**

Run:

```bash
rtk sh -lc 'rg -n "startup.helen|\\.obj|shared graph|code module" /mnt/c/dev/helworks/helengine/docs /mnt/c/dev/helworks/helengine-windows/README.md /mnt/c/dev/helworks/helengine-ps2/README.md'
```

Expected: the docs describe the new graph and no longer present `startup.helen` or raw `.obj` as runtime outputs.

- [ ] **Step 3: Commit**

```bash
rtk git -C /mnt/c/dev/helworks/helengine add docs/superpowers/specs/2026-05-02-shared-platform-build-graph-design.md
rtk git -C /mnt/c/dev/helworks/helengine-windows add README.md
rtk git -C /mnt/c/dev/helworks/helengine-ps2 add README.md
rtk git -C /mnt/c/dev/helworks/helengine commit -m "docs: document shared build graph rollout"
```
