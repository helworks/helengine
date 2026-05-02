# Editor-Owned Generated Core Regeneration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Regenerate `helengine.core` fresh in the editor for every platform build, using builder-supplied codegen metadata for endianness, output language, and platform-specific generation flags.

**Architecture:** The editor loads platform builders dynamically, reads their typed metadata, regenerates the shared core into a clean build workspace, then packages and builds the target player against that fresh output. Platform builders own metadata and native compilation only. The editor persists build, graphics, and codegen selections so future targets such as PS1 and N64 can request a different generated output language without moving regeneration responsibility into the native builder.

**Tech Stack:** C# / .NET 9, xUnit, `helengine.baseplatform`, `helengine.editor`, `helengine-windows`, `helengine-ps2`, `csharpcodegen`, Windows CMake, PS2SDK/PS2DEV

---

### Task 1: Add typed codegen metadata to `helengine.baseplatform`

**Files:**
- Create: `engine/helengine.baseplatform/Definitions/PlatformCodegenLanguage.cs`
- Create: `engine/helengine.baseplatform/Definitions/PlatformCodegenProfileDefinition.cs`
- Modify: `engine/helengine.baseplatform/Definitions/PlatformBuildProfileDefinition.cs`
- Modify: `engine/helengine.baseplatform/Definitions/PlatformDefinition.cs`
- Modify: `engine/helengine.baseplatform/Requests/PlatformBuildRequest.cs`
- Create: `engine/helengine.baseplatform.tests/Definitions/PlatformCodegenProfileDefinitionTests.cs`
- Create: `engine/helengine.baseplatform.tests/Requests/PlatformBuildRequestCodegenTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Platform_definition_exposes_codegen_profiles() {
    PlatformDefinition definition = new(
        "windows",
        "Windows DirectX",
        [
            new PlatformBuildProfileDefinition(
                "debug",
                "Debug",
                "Debug Windows player build",
                "directx11",
                "windows-cpp",
                [
                    new PlatformSettingDefinition(
                        "texture-scale-percent",
                        "Texture Scale Percent",
                        PlatformSettingKind.Text,
                        "100",
                        true,
                        []),
                    new PlatformSettingDefinition(
                        "shader-variant-pruning",
                        "Shader Variant Pruning",
                        PlatformSettingKind.Boolean,
                        "true",
                        true,
                        [])
                ])
        ],
        [
            new PlatformGraphicsProfileDefinition(
                "directx11",
                "DirectX 11",
                "Current Windows rendering backend",
                [
                    new PlatformSettingDefinition(
                        "default-width",
                        "Default Width",
                        PlatformSettingKind.Text,
                        "1280",
                        true,
                        []),
                    new PlatformSettingDefinition(
                        "default-height",
                        "Default Height",
                        PlatformSettingKind.Text,
                        "720",
                        true,
                        []),
                    new PlatformSettingDefinition(
                        "vsync-enabled",
                        "VSync Enabled",
                        PlatformSettingKind.Boolean,
                        "true",
                        true,
                        []),
                    new PlatformSettingDefinition(
                        "fullscreen-enabled",
                        "Fullscreen Enabled",
                        PlatformSettingKind.Boolean,
                        "false",
                        true,
                        [])
                ])
        ],
        [
            new PlatformAssetRequirementDefinition("scene", "Scene", true, ["helen"]),
            new PlatformAssetRequirementDefinition("texture", "Texture", true, ["png", "tga", "jpg"])
        ],
        [
            new PlatformCodegenProfileDefinition(
                "windows-cpp",
                "Windows C++",
                "Little-endian C++ generated core",
                PlatformCodegenLanguage.Cpp,
                PlatformSerializationEndianness.LittleEndian,
                [
                    new PlatformSettingDefinition(
                        "emit-pdb",
                        "Emit PDB",
                        PlatformSettingKind.Boolean,
                        "true",
                        true,
                        [])
                ])
        ]);

    Assert.Equal("windows-cpp", definition.CodegenProfiles[0].ProfileId);
    Assert.Equal("windows-cpp", definition.BuildProfiles[0].CodegenProfileId);
    Assert.Equal(PlatformCodegenLanguage.Cpp, definition.CodegenProfiles[0].OutputLanguage);
}
```

```csharp
[Fact]
public void Platform_build_request_carries_codegen_selection() {
    PlatformBuildRequest request = new(
        manifest,
        targetVariants,
        cookProfiles,
        outputRoot,
        workingRoot,
        "debug",
        "directx11",
        "windows-cpp",
        selectedBuildOptions,
        selectedGraphicsOptions,
        selectedCodegenOptions,
        generatedCoreCppRootPath);

    Assert.Equal("windows-cpp", request.SelectedCodegenProfileId);
    Assert.Equal("true", request.SelectedCodegenOptionValues["emit-pdb"]);
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlatformCodegenProfileDefinitionTests|FullyQualifiedName~PlatformBuildRequestCodegenTests" -v minimal
```

Expected: fail because the codegen language, codegen profile, and request fields do not exist yet.

- [ ] **Step 3: Add the minimal contract implementation**

```csharp
public enum PlatformCodegenLanguage {
    Cpp = 0,
    C = 1
}
```

```csharp
public sealed class PlatformCodegenProfileDefinition {
    public PlatformCodegenProfileDefinition(
        string profileId,
        string displayName,
        string description,
        PlatformCodegenLanguage outputLanguage,
        PlatformSerializationEndianness endianness,
        PlatformSettingDefinition[] settings) {
        // validate arguments, copy arrays, expose read-only properties
    }

    public string ProfileId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public PlatformCodegenLanguage OutputLanguage { get; }
    public PlatformSerializationEndianness Endianness { get; }
    public PlatformSettingDefinition[] Settings { get; }
}
```

```csharp
public class PlatformBuildProfileDefinition {
    public string CodegenProfileId { get; }
}
```

```csharp
public class PlatformDefinition {
    public PlatformCodegenProfileDefinition[] CodegenProfiles { get; }
}
```

```csharp
public class PlatformBuildRequest {
    public string SelectedCodegenProfileId { get; }
    public IReadOnlyDictionary<string, string> SelectedCodegenOptionValues { get; }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlatformCodegenProfileDefinitionTests|FullyQualifiedName~PlatformBuildRequestCodegenTests" -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.baseplatform engine/helengine.baseplatform.tests
rtk git commit -m "feat: add typed codegen metadata"
```

---

### Task 2: Publish codegen metadata from the Windows and PS2 builder assemblies

**Files:**
- Modify: `helengine-windows/builder/WindowsPlatformDefinitionFactory.cs`
- Modify: `helengine-windows/builder/WindowsPlatformAssetBuilder.cs`
- Modify: `helengine-windows/builder.tests/WindowsPlatformAssetBuilderTests.cs`
- Modify: `helengine-ps2/builder/Ps2PlatformDefinitionFactory.cs`
- Modify: `helengine-ps2/builder/Ps2PlatformAssetBuilder.cs`
- Modify: `helengine-ps2/builder.tests/Ps2PlatformAssetBuilderTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Windows_builder_exposes_codegen_profile_metadata() {
    WindowsPlatformAssetBuilder builder = new();

    Assert.Contains(builder.Definition.CodegenProfiles, profile => profile.ProfileId == "windows-cpp");
    Assert.Equal("windows-cpp", builder.Definition.BuildProfiles[0].CodegenProfileId);
    Assert.Equal(PlatformCodegenLanguage.Cpp, builder.Definition.CodegenProfiles[0].OutputLanguage);
}
```

```csharp
[Fact]
public void Ps2_builder_exposes_codegen_profile_metadata() {
    Ps2PlatformAssetBuilder builder = new();

    Assert.Contains(builder.Definition.CodegenProfiles, profile => profile.ProfileId == "ps2-cpp");
    Assert.Equal("ps2-cpp", builder.Definition.BuildProfiles[0].CodegenProfileId);
    Assert.Equal(PlatformCodegenLanguage.Cpp, builder.Definition.CodegenProfiles[0].OutputLanguage);
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine-windows/builder.tests/helengine.windows.builder.tests.csproj -v minimal
rtk dotnet test /mnt/c/dev/helworks/helengine-ps2/builder.tests/helengine.ps2.builder.tests.csproj -v minimal
```

Expected: fail because the builders do not yet expose codegen profiles or profile-to-codegen mapping.

- [ ] **Step 3: Add the minimal builder metadata implementation**

```csharp
return new PlatformDefinition(
    "windows",
    "Windows DirectX",
    [
                new PlatformBuildProfileDefinition(
                    "debug",
                    "Debug",
                    "Debug Windows player build",
                    "directx11",
                    "windows-cpp",
                    [
                        new PlatformSettingDefinition(
                            "texture-scale-percent",
                            "Texture Scale Percent",
                            PlatformSettingKind.Text,
                            "100",
                            true,
                            []),
                        new PlatformSettingDefinition(
                            "shader-variant-pruning",
                            "Shader Variant Pruning",
                            PlatformSettingKind.Boolean,
                            "true",
                            true,
                            [])
                    ])
            ],
            [
                new PlatformGraphicsProfileDefinition(
                    "directx11",
                    "DirectX 11",
                    "Current Windows rendering backend",
                    [
                        new PlatformSettingDefinition(
                            "default-width",
                            "Default Width",
                            PlatformSettingKind.Text,
                            "1280",
                            true,
                            []),
                        new PlatformSettingDefinition(
                            "default-height",
                            "Default Height",
                            PlatformSettingKind.Text,
                            "720",
                            true,
                            []),
                        new PlatformSettingDefinition(
                            "vsync-enabled",
                            "VSync Enabled",
                            PlatformSettingKind.Boolean,
                            "true",
                            true,
                            []),
                        new PlatformSettingDefinition(
                            "fullscreen-enabled",
                            "Fullscreen Enabled",
                            PlatformSettingKind.Boolean,
                            "false",
                            true,
                            [])
                    ])
            ],
            [
                new PlatformAssetRequirementDefinition("scene", "Scene", true, ["helen"]),
                new PlatformAssetRequirementDefinition("texture", "Texture", true, ["png", "tga", "jpg"])
            ],
            [
                new PlatformCodegenProfileDefinition(
                    "windows-cpp",
                    "Windows C++",
                    "Little-endian C++ generated core",
                    PlatformCodegenLanguage.Cpp,
                    PlatformSerializationEndianness.LittleEndian,
                    [
                        new PlatformSettingDefinition(
                            "write-conversion-report",
                            "Write Conversion Report",
                            PlatformSettingKind.Boolean,
                            "true",
                            true,
                            [])
                    ])
    ]);
```

```csharp
return new PlatformDefinition(
    "ps2",
    "PlayStation 2",
    [
                new PlatformBuildProfileDefinition(
                    "ps2-default",
                    "PS2 Default",
                    "Standard PS2 player build",
                    "gs-kit",
                    "ps2-cpp",
                    [
                        new PlatformSettingDefinition(
                            "texture-scale-percent",
                            "Texture Scale Percent",
                            PlatformSettingKind.Text,
                            "100",
                            true,
                            []),
                        new PlatformSettingDefinition(
                            "shader-variant-pruning",
                            "Shader Variant Pruning",
                            PlatformSettingKind.Boolean,
                            "true",
                            true,
                            [])
                    ])
            ],
            [
                new PlatformGraphicsProfileDefinition(
                    "gs-kit",
                    "GSKit",
                    "GSKit framebuffer backend",
                    [
                        new PlatformSettingDefinition(
                            "default-width",
                            "Default Width",
                            PlatformSettingKind.Text,
                            "640",
                            true,
                            []),
                        new PlatformSettingDefinition(
                            "default-height",
                            "Default Height",
                            PlatformSettingKind.Text,
                            "448",
                            true,
                            []),
                        new PlatformSettingDefinition(
                            "vsync-enabled",
                            "VSync Enabled",
                            PlatformSettingKind.Boolean,
                            "true",
                            true,
                            []),
                        new PlatformSettingDefinition(
                            "fullscreen-enabled",
                            "Fullscreen Enabled",
                            PlatformSettingKind.Boolean,
                            "false",
                            true,
                            [])
                    ])
            ],
            [
                new PlatformAssetRequirementDefinition("scene", "Scene", true, ["helen"]),
                new PlatformAssetRequirementDefinition("texture", "Texture", true, ["png", "tga", "jpg"]),
                new PlatformAssetRequirementDefinition("font", "Font", false, ["font.asset", "ttf", "otf"])
            ],
            [
                new PlatformCodegenProfileDefinition(
                    "ps2-cpp",
                    "PS2 C++",
                    "Little-endian C++ generated core",
                    PlatformCodegenLanguage.Cpp,
                    PlatformSerializationEndianness.LittleEndian,
                    [
                        new PlatformSettingDefinition(
                            "write-conversion-report",
                            "Write Conversion Report",
                            PlatformSettingKind.Boolean,
                            "true",
                            true,
                            [])
                    ])
    ]);
```

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine-windows/builder.tests/helengine.windows.builder.tests.csproj -v minimal
rtk dotnet test /mnt/c/dev/helworks/helengine-ps2/builder.tests/helengine.ps2.builder.tests.csproj -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git add helengine-windows/builder helengine-windows/builder.tests helengine-ps2/builder helengine-ps2/builder.tests
rtk git commit -m "feat: publish platform codegen metadata"
```

---

### Task 3: Persist and surface codegen selections in the editor build UI

**Files:**
- Modify: `engine/helengine.editor/model/BuildDialogAddRequest.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildPlatformConfigDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildConfigService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildSelectionModel.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs`
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Modify: `engine/helengine.editor/components/ui/ProfilesDialog.cs`
- Modify: `engine/helengine.editor/components/ui/EditorPlatformSettingsSection.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor.tests/model/BuildDialogAddRequestTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorBuildConfigServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildExecutorTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Build_dialog_add_request_carries_codegen_selections() {
    BuildDialogAddRequest request = new(
        "windows",
        ["scenes/startup.helen"],
        "C:/tmp/out",
        false,
        "debug",
        "directx11",
        "windows-cpp",
        new Dictionary<string, string> { ["emit-pdb"] = "true" },
        new Dictionary<string, string> { ["default-width"] = "1280" },
        new Dictionary<string, string> { ["output-language"] = "cpp" });

    Assert.Equal("windows-cpp", request.SelectedCodegenProfileId);
    Assert.Equal("cpp", request.SelectedCodegenOptionValues["output-language"]);
}
```

```csharp
[Fact]
public void Build_config_service_seeds_codegen_fields_for_new_platforms() {
    EditorBuildConfigDocument document = service.Load(["windows"], "scenes/startup.helen");
    EditorBuildPlatformConfigDocument platform = document.Platforms.Single(entry => entry.PlatformId == "windows");

    Assert.Equal(string.Empty, platform.SelectedCodegenProfileId);
    Assert.Empty(platform.SelectedCodegenOptionValues);
}
```

```csharp
[Fact]
public void Editor_platform_build_executor_threads_codegen_profile_into_request() {
    FakeGeneratedCoreRegenerationService regenerationService = new();
    FakePlatformBuilder builder = new();
    EditorPlatformBuildExecutor executor = new(
        projectRootPath,
        requiredEngineVersion,
        projectId,
        projectVersion,
        importers,
        platformDescriptor,
        regenerationService);

    EditorBuildExecutionResult result = executor.Execute(queueItem);

    Assert.True(result.Succeeded);
    Assert.Equal("windows-cpp", regenerationService.SelectedCodegenProfileId);
    Assert.Equal("windows-cpp", builder.LastRequest.SelectedCodegenProfileId);
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogAddRequestTests|FullyQualifiedName~EditorBuildConfigServiceTests|FullyQualifiedName~EditorPlatformBuildExecutorTests" -v minimal
```

Expected: fail because codegen fields do not yet exist in the editor request, persistence, or UI.

- [ ] **Step 3: Add the minimal persistence and UI implementation**

```csharp
public class BuildDialogAddRequest {
    public string SelectedCodegenProfileId { get; }
    public IReadOnlyDictionary<string, string> SelectedCodegenOptionValues { get; }
}
```

```csharp
public sealed class EditorBuildQueueItemDocument {
    public string SelectedCodegenProfileId { get; set; } = string.Empty;
    public Dictionary<string, string> SelectedCodegenOptionValues { get; set; } = [];
}
```

```csharp
public sealed class EditorBuildPlatformConfigDocument {
    public string SelectedCodegenProfileId { get; set; } = string.Empty;
    public Dictionary<string, string> SelectedCodegenOptionValues { get; set; } = [];
}
```

Add a third codegen settings section to both dialogs using the existing dynamic settings renderer:

```csharp
EditorPlatformSettingsSection codegenSection = new(
    "Generated Core",
    ActivePlatformSelectionModel.ResolveCodegenProfileSettings(selectedCodegenProfileId),
    platformConfig.SelectedCodegenOptionValues);
```

`ProfilesDialog` should use the same renderer for the persisted per-platform defaults so the codegen profile and its options are available both in the platform profile editor and in the queued build dialog.

```csharp
PlatformBuildRequest request = new(
    manifest,
    targetVariants,
    cookProfiles,
    outputRoot,
    builderWorkingRoot,
    selectedBuildProfileId,
    selectedGraphicsProfileId,
    selectedCodegenProfileId,
    queueItem.SelectedBuildOptionValues,
    queueItem.SelectedGraphicsOptionValues,
    queueItem.SelectedCodegenOptionValues,
    generatedCoreRoot);
```

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogAddRequestTests|FullyQualifiedName~EditorBuildConfigServiceTests|FullyQualifiedName~EditorPlatformBuildExecutorTests" -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/model engine/helengine.editor/managers/project engine/helengine.editor/components/ui engine/helengine.editor.tests
rtk git commit -m "feat: persist codegen selections in editor builds"
```

---

### Task 4: Add an editor-owned generated-core regeneration service and wire it into build execution

**Files:**
- Create: `engine/helengine.editor/managers/project/IEditorGeneratedCoreRegenerationService.cs`
- Create: `engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs`
- Create: `engine/helengine.editor/managers/project/IEditorDotNetProjectBuildTool.cs`
- Create: `engine/helengine.editor/managers/project/EditorDotNetProjectBuildTool.cs`
- Modify: `engine/helengine.editor/managers/project/EditorSourceBuildWorkspaceLocator.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildExecutorTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Generated_core_regeneration_uses_the_selected_codegen_contract() {
    FakeGeneratedCoreProjectBuildTool buildTool = new();
    EditorGeneratedCoreRegenerationService service = new(buildTool, new EditorSourceBuildWorkspaceLocator());

    string generatedCoreRoot = service.Regenerate(
        builderDefinition,
        "windows-cpp",
        options,
        generatedCoreRootPath,
        cancellationToken);

    Assert.True(File.Exists(Path.Combine(generatedCoreRoot, "helcpp_config.hpp")));
    Assert.Equal("windows-cpp", buildTool.CapturedCodegenProfileId);
}
```

```csharp
[Fact]
public void Platform_build_executor_regenerates_core_before_building_the_native_player() {
    FakeGeneratedCoreRegenerationService regenerationService = new();
    FakePlatformBuilder builder = new();
    EditorPlatformBuildExecutor executor = new(
        projectRootPath,
        requiredEngineVersion,
        projectId,
        projectVersion,
        importers,
        platformDescriptor,
        regenerationService);

    executor.Execute(queueItem);

    Assert.True(regenerationService.WasCalled);
    Assert.Equal(regenerationService.OutputRoot, builder.LastRequest.GeneratedCoreCppRootPath);
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests|FullyQualifiedName~EditorPlatformBuildExecutorTests" -v minimal
```

Expected: fail because the editor does not yet own a codegen regeneration service or create a fresh generated-core workspace.

- [ ] **Step 3: Add the minimal regeneration implementation**

```csharp
public interface IEditorDotNetProjectBuildTool {
    EditorBuildExecutionResult Build(
        string projectPath,
        string baseIntermediateOutputPath,
        string baseOutputPath);
}
```

```csharp
public interface IEditorGeneratedCoreRegenerationService {
    string Regenerate(
        PlatformDefinition platformDefinition,
        string selectedCodegenProfileId,
        IReadOnlyDictionary<string, string> selectedCodegenOptionValues,
        string generatedCoreRootPath,
        CancellationToken cancellationToken);
}
```

```csharp
public sealed class EditorGeneratedCoreRegenerationService : IEditorGeneratedCoreRegenerationService {
    public string Regenerate(
        PlatformDefinition platformDefinition,
        string selectedCodegenProfileId,
        IReadOnlyDictionary<string, string> selectedCodegenOptionValues,
        string generatedCoreRootPath,
        CancellationToken cancellationToken) {
        // Resolve helengine root and csharpcodegen root from EditorSourceBuildWorkspaceLocator.
        // Build cs2.core and cs2.cpp into temporary output roots.
        // Load the fresh DLLs and invoke CPPCodeConverter into generatedCoreRootPath.
        // Return the absolute generatedCoreRootPath after regeneration.
    }
}
```

```csharp
public sealed class EditorDotNetProjectBuildTool : IEditorDotNetProjectBuildTool {
    public EditorBuildExecutionResult Build(string projectPath, string baseIntermediateOutputPath, string baseOutputPath) {
        // Run dotnet build with a temporary working directory and captured stdout/stderr.
    }
}
```

`EditorPlatformBuildExecutor.Execute` should:

1. create a fresh workspace-local generated-core root under the build execution root
2. call `IEditorGeneratedCoreRegenerationService.Regenerate`
3. package scenes and assets
4. pass the fresh generated-core root into `PlatformBuildRequest`
5. dispatch the request to the platform builder

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests|FullyQualifiedName~EditorPlatformBuildExecutorTests" -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/managers/project engine/helengine.editor.tests
rtk git commit -m "feat: regenerate generated core in the editor"
```

---

### Task 5: Remove generated-core regeneration from native builders and verify end-to-end freshness

**Files:**
- Modify: `helengine-windows/builder/WindowsBuildWorkspace.cs`
- Modify: `helengine-windows/builder/WindowsPlatformAssetBuilder.cs`
- Delete: `helengine-windows/builder/WindowsGeneratedCoreExporter.cs`
- Modify: `helengine-windows/builder.tests/WindowsPlatformAssetBuilderTests.cs`
- Modify: `helengine-windows/builder/WindowsNativeBuildExecutor.cs` if any signature or log text still refers to ownership of regeneration
- Modify: `helengine-ps2/builder/Ps2PlatformAssetBuilder.cs` if it still needs to validate that the request carries a fresh generated-core root
- Modify: `helengine-ps2/builder.tests/Ps2PlatformAssetBuilderTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task Windows_builder_consumes_a_fresh_generated_core_root_from_the_request() {
    WindowsPlatformAssetBuilder builder = new();
    PlatformBuildRequest request = new(
        manifest,
        targetVariants,
        cookProfiles,
        outputRoot,
        workingRoot,
        "debug",
        "directx11",
        "windows-cpp",
        selectedBuildOptions,
        selectedGraphicsOptions,
        selectedCodegenOptions,
        generatedCoreRoot);

    PlatformBuildReport report = await builder.BuildAsync(request, progressReporter, diagnosticReporter, CancellationToken.None);

    Assert.True(report.Succeeded);
    Assert.Equal(generatedCoreRoot, nativeBuildExecutor.LastGeneratedCoreRootPath);
}
```

```csharp
[Fact]
public void Ps2_builder_accepts_a_generated_core_root_in_the_request() {
    Ps2PlatformAssetBuilder builder = new();

    Assert.True(builder.BuildAsync(request, progressReporter, diagnosticReporter, CancellationToken.None).Result.Succeeded);
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine-windows/builder.tests/helengine.windows.builder.tests.csproj -v minimal
rtk dotnet test /mnt/c/dev/helworks/helengine-ps2/builder.tests/helengine.ps2.builder.tests.csproj -v minimal
```

Expected: fail because the Windows builder still owns core regeneration and the updated request wiring is not complete.

- [ ] **Step 3: Remove builder-owned regeneration and use the editor-supplied path**

```csharp
public sealed class WindowsPlatformAssetBuilder : IPlatformAssetBuilder {
    public Task<PlatformBuildReport> BuildAsync(
        PlatformBuildRequest request,
        IPlatformBuildProgressReporter progressReporter,
        IPlatformBuildDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken) {
        return WindowsBuildWorkspace.BuildAsync(request, progressReporter, diagnosticReporter, NativeBuildExecutor, cancellationToken);
    }
}
```

```csharp
// WindowsBuildWorkspace should no longer call a generated-core exporter.
// It should validate request.GeneratedCoreCppRootPath and pass that path into the native build executor.
```

Delete `WindowsGeneratedCoreExporter.cs` once the editor owns the regeneration step.

`WindowsNativeBuildExecutor` continues to consume `generatedCoreCppRootPath` only as input to the native build; it does not regenerate anything.

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine-windows/builder.tests/helengine.windows.builder.tests.csproj -v minimal
rtk dotnet test /mnt/c/dev/helworks/helengine-ps2/builder.tests/helengine.ps2.builder.tests.csproj -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git add helengine-windows/builder helengine-windows/builder.tests helengine-ps2/builder helengine-ps2/builder.tests
rtk git commit -m "refactor: move generated core ownership to editor"
```

---

### Task 6: Add end-to-end freshness and future-output-language regression coverage

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildExecutorTests.cs`
- Modify: `engine/helengine.baseplatform.tests/Definitions/PlatformCodegenProfileDefinitionTests.cs`
- Modify: `engine/helengine.windows.builder.tests/WindowsPlatformAssetBuilderTests.cs`
- Modify: `engine/helengine.ps2.builder.tests/Ps2PlatformAssetBuilderTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Generated_core_regeneration_is_fresh_for_each_build() {
    string firstRoot = service.Regenerate(
        builderDefinition,
        "windows-cpp",
        options,
        firstGeneratedCoreRoot,
        CancellationToken.None);
    File.WriteAllText(Path.Combine(helengineCoreRoot, "EditorAssetBinarySerializer.cs"), "version = 4");
    string secondRoot = service.Regenerate(
        builderDefinition,
        "windows-cpp",
        options,
        secondGeneratedCoreRoot,
        CancellationToken.None);

    Assert.NotEqual(firstRoot, secondRoot);
    Assert.True(File.Exists(Path.Combine(secondRoot, "EditorAssetBinarySerializer.cpp")));
}
```

```csharp
[Fact]
public void Codegen_language_metadata_can_describe_future_c_targets() {
    PlatformCodegenProfileDefinition profile = new(
        "n64-c",
        "N64 C",
        "Big-endian C generated core",
        PlatformCodegenLanguage.C,
        PlatformSerializationEndianness.BigEndian,
        []);

    Assert.Equal(PlatformCodegenLanguage.C, profile.OutputLanguage);
    Assert.Equal(PlatformSerializationEndianness.BigEndian, profile.Endianness);
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests|FullyQualifiedName~EditorPlatformBuildExecutorTests" -v minimal
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlatformCodegenProfileDefinitionTests" -v minimal
```

Expected: fail until the fresh workspace regeneration and output-language contract are fully wired.

- [ ] **Step 3: Add the minimal verification coverage**

Keep the generated-core output root under the editor's execution workspace so each build starts from a clean directory.

Verify the selected codegen profile name, output language, and endianness are visible in build logs and request objects.

Add explicit assertions that the platform builder never regenerates the core itself.

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj -v minimal
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor.tests engine/helengine.baseplatform.tests
rtk git commit -m "test: cover editor-owned generated core freshness"
```
