# Dynamic Platform Builder Assemblies Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove all platform-specific build knowledge from the editor and move Windows and PS2 discovery, metadata, and native build orchestration behind dynamically loaded builder assemblies.

**Architecture:** `engine/helengine.baseplatform` defines the shared typed contract. Each platform repo ships a builder assembly that describes its own build profiles, graphics profiles, options, and asset requirements, then executes the native build for that platform. The editor discovers installed platforms from `user_settings/platforms.json`, loads the builder assembly for each platform, builds the UI from the builder-provided metadata, and routes queued builds through one generic platform-agnostic execution path.

**Tech Stack:** C# / .NET 10, xUnit, HelEngine editor UI, in-tree `helengine.baseplatform`, Windows CMake, PS2 Docker/PS2SDK, `user_settings/platforms.json`

---

### Task 1: Add the typed platform metadata contract to `helengine.baseplatform`

**Files:**
- Create: `engine/helengine.baseplatform/Definitions/PlatformDefinition.cs`
- Create: `engine/helengine.baseplatform/Definitions/PlatformBuildProfileDefinition.cs`
- Create: `engine/helengine.baseplatform/Definitions/PlatformGraphicsProfileDefinition.cs`
- Create: `engine/helengine.baseplatform/Definitions/PlatformSettingDefinition.cs`
- Create: `engine/helengine.baseplatform/Definitions/PlatformSettingKind.cs`
- Create: `engine/helengine.baseplatform/Definitions/PlatformAssetRequirementDefinition.cs`
- Modify: `engine/helengine.baseplatform/Builders/IPlatformAssetBuilder.cs`
- Create: `engine/helengine.baseplatform.tests/Definitions/PlatformDefinitionTests.cs`
- Create: `engine/helengine.baseplatform.tests/Builders/IPlatformAssetBuilderMetadataTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void PlatformDefinition_preserves_build_and_graphics_metadata() {
    PlatformDefinition definition = new(
        "windows",
        "Windows DirectX",
        [
            new PlatformBuildProfileDefinition(
                "debug",
                "Debug",
                "Debug player build",
                "directx11",
                [
                    new PlatformSettingDefinition(
                        "emit-pdb",
                        "Emit PDB",
                        PlatformSettingKind.Boolean,
                        "true",
                        false,
                        [])
                ])
        ],
        [
            new PlatformGraphicsProfileDefinition(
                "directx11",
                "DirectX 11",
                "Default Windows renderer",
                [])
        ],
        [
            new PlatformAssetRequirementDefinition(
                "texture",
                "Texture",
                true,
                ["png", "tga"])
        ]);

    Assert.Equal("windows", definition.PlatformId);
    Assert.Equal("debug", definition.BuildProfiles[0].ProfileId);
    Assert.Equal("directx11", definition.GraphicsProfiles[0].ProfileId);
}
```

```csharp
[Fact]
public void Builder_contract_exposes_platform_definition() {
    IPlatformAssetBuilder builder = new TestPlatformAssetBuilder();

    Assert.Equal("windows", builder.Definition.PlatformId);
    Assert.Equal("debug", builder.Definition.BuildProfiles[0].ProfileId);
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlatformDefinitionTests|FullyQualifiedName~IPlatformAssetBuilderMetadataTests" -v minimal
```

Expected: fail because the new definition types and `IPlatformAssetBuilder.Definition` do not exist yet.

- [ ] **Step 3: Add the minimal contract implementation**

```csharp
public interface IPlatformAssetBuilder {
    PlatformBuilderDescriptor Descriptor { get; }
    PlatformDefinition Definition { get; }
    Task<PlatformBuildReport> BuildAsync(
        PlatformBuildRequest request,
        IPlatformBuildProgressReporter progressReporter,
        IPlatformBuildDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken);
}
```

```csharp
public sealed class PlatformDefinition {
    public PlatformDefinition(
        string platformId,
        string displayName,
        PlatformBuildProfileDefinition[] buildProfiles,
        PlatformGraphicsProfileDefinition[] graphicsProfiles,
        PlatformAssetRequirementDefinition[] assetRequirements) {
        if (string.IsNullOrWhiteSpace(platformId)) {
            throw new ArgumentException("Platform id is required.", nameof(platformId));
        }
        if (string.IsNullOrWhiteSpace(displayName)) {
            throw new ArgumentException("Platform display name is required.", nameof(displayName));
        }
        if (buildProfiles == null) {
            throw new ArgumentNullException(nameof(buildProfiles));
        }
        if (graphicsProfiles == null) {
            throw new ArgumentNullException(nameof(graphicsProfiles));
        }
        if (assetRequirements == null) {
            throw new ArgumentNullException(nameof(assetRequirements));
        }

        PlatformId = platformId;
        DisplayName = displayName;
        BuildProfiles = [.. buildProfiles];
        GraphicsProfiles = [.. graphicsProfiles];
        AssetRequirements = [.. assetRequirements];
    }

    public string PlatformId { get; }
    public string DisplayName { get; }
    public PlatformBuildProfileDefinition[] BuildProfiles { get; }
    public PlatformGraphicsProfileDefinition[] GraphicsProfiles { get; }
    public PlatformAssetRequirementDefinition[] AssetRequirements { get; }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlatformDefinitionTests|FullyQualifiedName~IPlatformAssetBuilderMetadataTests" -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.baseplatform engine/helengine.baseplatform.tests
rtk git commit -m "feat: add typed platform builder metadata"
```

---

### Task 2: Add a Windows builder assembly in `helengine-windows`

**Files:**
- Create: `helengine-windows/builder/helengine.windows.builder.csproj`
- Create: `helengine-windows/builder/Program.cs`
- Create: `helengine-windows/builder/WindowsPlatformAssetBuilder.cs`
- Create: `helengine-windows/builder/WindowsPlatformDefinitionFactory.cs`
- Create: `helengine-windows/builder/WindowsBuildWorkspace.cs`
- Create: `helengine-windows/builder.tests/helengine.windows.builder.tests.csproj`
- Create: `helengine-windows/builder.tests/WindowsPlatformAssetBuilderTests.cs`
- Modify: `helengine-windows/README.md`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Windows_builder_exposes_windows_metadata() {
    WindowsPlatformAssetBuilder builder = new();

    Assert.Equal("windows", builder.Descriptor.TargetPlatformId);
    Assert.Equal("windows", builder.Definition.PlatformId);
    Assert.Contains(builder.Definition.BuildProfiles, profile => profile.ProfileId == "debug");
    Assert.Contains(builder.Definition.GraphicsProfiles, profile => profile.ProfileId == "directx11");
}
```

```csharp
[Fact]
public async Task Windows_builder_smoke_test_copies_request_payloads() {
    WindowsPlatformAssetBuilder builder = new();
    PlatformBuildReport report = await builder.BuildAsync(request, new NullProgressReporter(), new NullDiagnosticReporter(), CancellationToken.None);

    Assert.True(report.Succeeded);
    Assert.True(File.Exists(Path.Combine(outputRoot, "scenes", "startup.helen")));
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine-windows/builder.tests/helengine.windows.builder.tests.csproj -v minimal
```

Expected: fail because the builder project does not exist yet.

- [ ] **Step 3: Move the Windows build logic out of the editor and into the builder assembly**

```csharp
public sealed class WindowsPlatformAssetBuilder : IPlatformAssetBuilder {
    public PlatformBuilderDescriptor Descriptor { get; }
    public PlatformDefinition Definition { get; }

    public Task<PlatformBuildReport> BuildAsync(
        PlatformBuildRequest request,
        IPlatformBuildProgressReporter progressReporter,
        IPlatformBuildDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken) {
        return WindowsBuildWorkspace.BuildAsync(request, progressReporter, diagnosticReporter, cancellationToken);
    }
}
```

```csharp
public static class WindowsPlatformDefinitionFactory {
    public static PlatformDefinition Create() {
        return new PlatformDefinition(
            "windows",
            "Windows DirectX",
            [
                new PlatformBuildProfileDefinition("debug", "Debug", "Debug player build", "directx11", [])
            ],
            [
                new PlatformGraphicsProfileDefinition("directx11", "DirectX 11", "Default Windows renderer", [])
            ],
            []);
    }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine-windows/builder.tests/helengine.windows.builder.tests.csproj -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git add helengine-windows/builder helengine-windows/builder.tests helengine-windows/README.md
rtk git commit -m "feat: add dynamic windows builder assembly"
```

---

### Task 3: Update the PS2 builder assembly to expose the same metadata contract

**Files:**
- Modify: `helengine-ps2/builder/Ps2PlatformAssetBuilder.cs`
- Modify: `helengine-ps2/builder/Program.cs`
- Create: `helengine-ps2/builder/Ps2PlatformDefinitionFactory.cs`
- Modify: `helengine-ps2/builder.tests/Ps2PlatformAssetBuilderTests.cs`
- Modify: `helengine-ps2/README.md`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Ps2_builder_exposes_ps2_metadata() {
    Ps2PlatformAssetBuilder builder = new();

    Assert.Equal("ps2", builder.Descriptor.TargetPlatformId);
    Assert.Equal("ps2", builder.Definition.PlatformId);
    Assert.Contains(builder.Definition.BuildProfiles, profile => profile.ProfileId == "ps2-default");
    Assert.Contains(builder.Definition.GraphicsProfiles, profile => profile.ProfileId == "ps2");
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine-ps2/builder.tests/helengine.ps2.builder.tests.csproj -v minimal
```

Expected: fail until the builder exposes `Definition`.

- [ ] **Step 3: Update the PS2 builder to publish typed metadata**

```csharp
public sealed class Ps2PlatformAssetBuilder : IPlatformAssetBuilder {
    public Ps2PlatformAssetBuilder() {
        Descriptor = new PlatformBuilderDescriptor(
            "helengine.ps2.builder",
            "1.0.0",
            "ps2",
            new EngineCompatibilityRange("1.0.0", "999.0.0"),
            new ManifestCompatibilityRange(1, 1),
            ["ps2"],
            ["ps2"]);
        Definition = Ps2PlatformDefinitionFactory.Create();
    }

    public PlatformBuilderDescriptor Descriptor { get; }
    public PlatformDefinition Definition { get; }
}
```

```csharp
public static class Ps2PlatformDefinitionFactory {
    public static PlatformDefinition Create() {
        return new PlatformDefinition(
            "ps2",
            "PS2",
            [
                new PlatformBuildProfileDefinition("ps2-default", "PS2 Default", "Default PS2 build", "ps2", [])
            ],
            [
                new PlatformGraphicsProfileDefinition("ps2", "PS2 Graphics", "Default PS2 renderer", [])
            ],
            []);
    }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine-ps2/builder.tests/helengine.ps2.builder.tests.csproj -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git add helengine-ps2/builder helengine-ps2/builder.tests helengine-ps2/README.md
rtk git commit -m "feat: add typed ps2 builder metadata"
```

---

### Task 4: Make the editor load builder metadata dynamically and stop hardcoding platforms

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Create: `engine/helengine.editor/managers/project/EditorLoadedPlatformBuilder.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformCatalogService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformAssetBuilderLoader.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformBuildRequestFactory.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildQueueService.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildExecutorTests.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorPlatformCatalogServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Editor_platform_catalog_loads_builder_metadata_from_platforms_json() {
    string settingsRoot = Path.Combine(Path.GetTempPath(), "helengine-platforms-test");
    EditorPlatformCatalogService catalog = new(settingsRoot, new EditorPlatformAssetBuilderLoader());
    IReadOnlyList<EditorLoadedPlatformBuilder> platforms = catalog.LoadAvailablePlatforms();

    Assert.Contains(platforms, platform => platform.PlatformId == "windows");
    Assert.Contains(platforms, platform => platform.PlatformId == "ps2");
    Assert.Contains(platforms, platform => platform.Definition.BuildProfiles.Length > 0);
}
```

```csharp
[Fact]
public void Editor_session_uses_dynamic_platform_build_executor_only() {
    Assembly editorAssembly = typeof(EditorSession).Assembly;

    Assert.DoesNotContain(editorAssembly.GetTypes(), type => type.Name == "EditorWindowsBuildExecutor");
    Assert.DoesNotContain(editorAssembly.GetTypes(), type => type.Name == "EditorPs2BuildExecutor");
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformCatalogServiceTests|FullyQualifiedName~EditorPlatformBuildExecutorTests" -v minimal
```

Expected: fail until the editor can load builder metadata generically.

- [ ] **Step 3: Replace hardcoded executors with a generic platform build path**

```csharp
public sealed class EditorLoadedPlatformBuilder {
    public EditorLoadedPlatformBuilder(string assemblyPath, IPlatformAssetBuilder builder) {
        AssemblyPath = assemblyPath;
        Builder = builder;
        Descriptor = builder.Descriptor;
        Definition = builder.Definition;
    }

    public string AssemblyPath { get; }
    public IPlatformAssetBuilder Builder { get; }
    public PlatformBuilderDescriptor Descriptor { get; }
    public PlatformDefinition Definition { get; }
}
```

```csharp
public sealed class EditorPlatformBuildExecutor : IEditorBuildExecutor {
    readonly EditorPlatformCatalogService PlatformCatalog;

    public EditorPlatformBuildExecutor(EditorPlatformCatalogService platformCatalog) {
        PlatformCatalog = platformCatalog;
    }

    public EditorBuildExecutionResult Execute(EditorBuildQueueItemDocument queueItem) {
        EditorLoadedPlatformBuilder platform = PlatformCatalog.Resolve(queueItem.PlatformId);
        PlatformBuildReport report = platform.Builder.BuildAsync(
            EditorPlatformBuildRequestFactory.Create(queueItem, platform.Definition),
            new EditorPlatformBuildProgressReporter(),
            new EditorPlatformBuildDiagnosticCollector(),
            CancellationToken.None).GetAwaiter().GetResult();

        return report.Succeeded
            ? EditorBuildExecutionResult.Success("Build completed.")
            : EditorBuildExecutionResult.Failure("Build failed.");
    }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformCatalogServiceTests|FullyQualifiedName~EditorPlatformBuildExecutorTests" -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor
rtk git commit -m "feat: load platform builders dynamically in editor"
```

---

### Task 5: Make build dialogs and persisted settings entirely metadata-driven

**Files:**
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Modify: `engine/helengine.editor/components/ui/BuildSettingsDialog.cs`
- Modify: `engine/helengine.editor/components/ui/ProfilesDialog.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildConfigService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildPlatformConfigDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildProfileSettingsDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGraphicsProfileSettingsDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformProfileSettingsDocument.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformBuildSelectionModel.cs`
- Modify: `engine/helengine.editor.tests/BuildDialogTests.cs`
- Modify: `engine/helengine.editor.tests/BuildSettingsDialogTests.cs`
- Modify: `engine/helengine.editor.tests/ProfilesDialogTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionBuildSettingsTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Build_dialog_lists_profiles_from_loaded_platform_definition() {
    PlatformDefinition definition = new(
        "windows",
        "Windows DirectX",
        [
            new PlatformBuildProfileDefinition("debug", "Debug", "Debug player build", "directx11", [])
        ],
        [
            new PlatformGraphicsProfileDefinition("directx11", "DirectX 11", "Default Windows renderer", [])
        ],
        []);
    EditorPlatformBuildSelectionModel model = EditorPlatformBuildSelectionModel.From(definition);

    Assert.Contains(model.BuildProfiles, profile => profile.ProfileId == "debug");
    Assert.Contains(model.GraphicsProfiles, profile => profile.ProfileId == "directx11");
}
```

```csharp
[Fact]
public void Build_queue_item_snapshots_dynamic_option_values() {
    EditorBuildQueueItemDocument item = new(
        "queue-id",
        "ps2",
        ["startup"],
        false,
        "build/out",
        "debug",
        "ps2",
        new Dictionary<string, string> { { "emit-pdb", "true" } });

    Assert.Equal("debug", item.SelectedBuildProfileId);
    Assert.Equal("true", item.SelectedOptionValues["emit-pdb"]);
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests|FullyQualifiedName~BuildSettingsDialogTests|FullyQualifiedName~ProfilesDialogTests|FullyQualifiedName~EditorSessionBuildSettingsTests" -v minimal
```

Expected: fail because the dialogs and documents still assume the old hardcoded settings shape.

- [ ] **Step 3: Make the settings flow generic**

```csharp
public sealed class EditorBuildQueueItemDocument {
    public string PlatformId { get; }
    public string SelectedBuildProfileId { get; }
    public string SelectedGraphicsProfileId { get; }
    public IReadOnlyDictionary<string, string> SelectedOptionValues { get; }
}
```

```csharp
public sealed class EditorBuildConfigService {
    readonly EditorPlatformCatalogService PlatformCatalog;

    public EditorBuildConfigService(EditorPlatformCatalogService platformCatalog) {
        PlatformCatalog = platformCatalog;
    }

    public PlatformDefinition ResolveDefinition(string platformId) {
        return PlatformCatalog.Resolve(platformId).Definition;
    }
}
```

```csharp
public sealed class EditorPlatformBuildSelectionModel {
    public static EditorPlatformBuildSelectionModel From(PlatformDefinition definition) {
        return new EditorPlatformBuildSelectionModel(definition.PlatformId, definition.DisplayName, definition.BuildProfiles, definition.GraphicsProfiles);
    }

    public EditorPlatformBuildSelectionModel(
        string platformId,
        string displayName,
        PlatformBuildProfileDefinition[] buildProfiles,
        PlatformGraphicsProfileDefinition[] graphicsProfiles) {
        PlatformId = platformId;
        DisplayName = displayName;
        BuildProfiles = buildProfiles;
        GraphicsProfiles = graphicsProfiles;
    }

    public string PlatformId { get; }
    public string DisplayName { get; }
    public PlatformBuildProfileDefinition[] BuildProfiles { get; }
    public PlatformGraphicsProfileDefinition[] GraphicsProfiles { get; }
}
```

The dialogs should read the loaded `PlatformDefinition` and render generic controls for the selected platform’s build profiles, graphics profiles, and option definitions. No dialog logic should branch on a specific platform id or a specific renderer name.

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests|FullyQualifiedName~BuildSettingsDialogTests|FullyQualifiedName~ProfilesDialogTests|FullyQualifiedName~EditorSessionBuildSettingsTests" -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/components/ui engine/helengine.editor/managers/project engine/helengine.editor.tests
rtk git commit -m "feat: drive build dialogs from platform metadata"
```

---

### Task 6: Remove obsolete platform-specific editor executors and finish the migration

**Files:**
- Delete: `engine/helengine.editor/managers/project/EditorWindowsBuildExecutor.cs`
- Delete: `engine/helengine.editor/managers/project/EditorPs2BuildExecutor.cs`
- Delete: `engine/helengine.editor/managers/project/EditorBuildExecutorRouter.cs`
- Modify: `user_settings/platforms.json`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildExecutorTests.cs`
- Modify: `engine/helengine.platforms.tests/PlatformInstallationResolverTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Editor_session_does_not_reference_platform_specific_executor_types() {
    Assembly editorAssembly = typeof(EditorSession).Assembly;
    Assert.DoesNotContain(editorAssembly.GetTypes(), type => type.Name == "EditorWindowsBuildExecutor");
    Assert.DoesNotContain(editorAssembly.GetTypes(), type => type.Name == "EditorPs2BuildExecutor");
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildExecutorTests|FullyQualifiedName~EditorSessionBuildSettingsTests" -v minimal
```

Expected: fail until the old executor types are removed.

- [ ] **Step 3: Delete the obsolete editor-side executor classes and keep only the generic builder path**

```csharp
// Remove the old platform-specific executors entirely.
// The editor should only have the generic metadata-driven builder loader and executor path.
```

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildExecutorTests|FullyQualifiedName~EditorSessionBuildSettingsTests" -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor user_settings/platforms.json
rtk git commit -m "refactor: remove platform-specific editor executors"
```
