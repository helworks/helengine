# PS2 End-to-End Export Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Docker-only PS2 export path that produces `helengine_ps2.elf` plus loose `cooked/` runtime assets, with the ELF loading the packaged startup scene from generated native startup metadata instead of JSON.

**Architecture:** Reuse the shared editor-owned build graph and runtime manifest generation already established for Windows, then specialize only the PS2 packaging and host/runtime pieces. The editor prepares generated core, cooked artifacts, and generated native runtime metadata; the PS2 builder stages that data into a Docker `make` build, emits the ELF, and ships the same `cooked/` runtime layout the PS2 host resolves at startup.

**Tech Stack:** C# / .NET 9, xUnit, `helengine.editor`, `helengine.baseplatform`, `helengine.core`, `helengine.input`, `helengine-ps2`, Docker, `ps2dev`, PS2SDK, gsKit, generated C++ runtime manifests.

---

## File Structure

### Shared editor/runtime integration

- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorRuntimeNativeManifestWriterTests.cs`

### PS2 builder

- Create: `C:\dev\helworks\helengine-ps2\builder\IPs2NativeBuildExecutor.cs`
- Create: `C:\dev\helworks\helengine-ps2\builder\Ps2BuildWorkspace.cs`
- Create: `C:\dev\helworks\helengine-ps2\builder\Ps2NativeBuildExecutor.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2PlatformAssetBuilder.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Program.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlatformAssetBuilderTests.cs`

### PS2 native host/runtime

- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2InputBackend.hpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2InputBackend.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.hpp`
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\src\main.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\Makefile`

### Docs

- Modify: `C:\dev\helworks\helengine-ps2\README.md`
- Modify: `docs/superpowers/specs/2026-05-03-unified-platform-runtime-build-system-design.md`

---

### Task 1: Lock the editor-side PS2 build graph contract

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorRuntimeNativeManifestWriterTests.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs`

- [ ] **Step 1: Write the failing PS2 build-graph test**

```csharp
[Fact]
public async Task RunBuildAsync_ForPs2_StagesGeneratedRuntimeMetadataAndCookedScenePath() {
    TestEditorBuildExecutor buildExecutor = new();
    EditorPlatformBuildGraphRunner runner = CreateRunner(buildExecutor);

    await runner.RunBuildAsync(CreatePs2BuildRequest());

    PlatformBuildRequest platformRequest = Assert.Single(buildExecutor.PlatformBuildRequests);
    Assert.Equal("ps2", platformRequest.TargetVariants[0].PlatformId);
    Assert.Equal("disc-layout", platformRequest.SelectedStorageProfileId);
    Assert.Contains(platformRequest.Manifest.CookedArtifacts, artifact => artifact.RelativePath == "cooked/scenes/main.hasset");
    Assert.True(File.Exists(Path.Combine(platformRequest.GeneratedCoreCppRootPath, "runtime", "runtime_startup_manifest.cpp")));
    Assert.True(File.Exists(Path.Combine(platformRequest.GeneratedCoreCppRootPath, "runtime", "runtime_code_module_manifest.cpp")));
}
```

- [ ] **Step 2: Run the focused editor tests and verify they fail**

Run:

```powershell
rtk proxy dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildGraphRunnerTests|FullyQualifiedName~EditorRuntimeNativeManifestWriterTests" -v minimal
```

Expected: FAIL because the PS2 build path does not yet preserve the generated runtime manifest files and cooked startup-scene path through the build request.

- [ ] **Step 3: Implement the minimal graph changes**

```csharp
public async Task RunBuildAsync(EditorPlatformBuildRequest request) {
    PlatformBuildManifest cookedManifest = BuildCookedManifest(request);
    RuntimeNativeManifestWriter.Write(generatedCoreRootPath, cookedManifest);

    PlatformBuildRequest platformBuildRequest = new(
        cookedManifest,
        targetVariants,
        cookProfiles,
        outputRootPath,
        workingRootPath,
        selectedBuildProfileId,
        selectedGraphicsProfileId,
        selectedCodegenProfileId,
        selectedBuildOptions,
        selectedGraphicsOptions,
        selectedCodegenOptions,
        generatedCoreRootPath,
        selectedMediaProfileId,
        selectedStorageProfileId);

    await BuildExecutor.ExecuteAsync(platformBuildRequest, cancellationToken);
}
```

```csharp
static PlatformBuildManifest BuildCookedManifest(EditorPlatformBuildRequest request) {
    PlatformBuildArtifact[] cookedArtifacts = request.CookedArtifacts
        .Select(artifact => new PlatformBuildArtifact(
            artifact.RelativePath.Replace('\\', '/'),
            artifact.LogicalArtifactId,
            artifact.ContentHash,
            artifact.ArtifactKind,
            artifact.VariantId))
        .ToArray();

    return new PlatformBuildManifest(
        3,
        request.ProjectId,
        request.ProjectVersion,
        request.RequiredEngineVersion,
        request.StartupSceneId,
        request.Scenes,
        request.LooseAssets,
        cookedArtifacts,
        request.CodeModules,
        request.ArtifactPlacements,
        request.ContainerWritePlan);
}
```

- [ ] **Step 4: Run the focused editor tests and verify they pass**

Run:

```powershell
rtk proxy dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildGraphRunnerTests|FullyQualifiedName~EditorRuntimeNativeManifestWriterTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the editor-side PS2 contract work**

```powershell
rtk proxy git -C C:\dev\helworks\helengine add engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs engine/helengine.editor.tests/managers/project/EditorRuntimeNativeManifestWriterTests.cs
rtk proxy git -C C:\dev\helworks\helengine commit -m "feat: prepare PS2 build graph runtime handoff"
```

### Task 2: Replace the PS2 builder’s staging-only path with a real Docker package step

**Files:**
- Create: `C:\dev\helworks\helengine-ps2\builder\IPs2NativeBuildExecutor.cs`
- Create: `C:\dev\helworks\helengine-ps2\builder\Ps2BuildWorkspace.cs`
- Create: `C:\dev\helworks\helengine-ps2\builder\Ps2NativeBuildExecutor.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2PlatformAssetBuilder.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Program.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlatformAssetBuilderTests.cs`

- [ ] **Step 1: Write the failing PS2 builder test for ELF plus cooked output**

```csharp
[Fact]
public async Task BuildAsync_WhenGivenGeneratedCoreAndCookedArtifacts_ProducesElfAndCookedTree() {
    FakePs2NativeBuildExecutor nativeBuildExecutor = new();
    Ps2PlatformAssetBuilder builder = new(nativeBuildExecutor);
    PlatformBuildRequest request = CreatePs2Request(
        generatedCoreCppRootPath: generatedCoreRoot,
        cookedArtifacts: [
            new PlatformBuildArtifact("cooked/scenes/main.hasset", "scene:main", "sha256:scene", "scene", "shared"),
            new PlatformBuildArtifact("cooked/imported/box_a.hasset", "model:box_a", "sha256:model", "model", "shared")
        ]);

    PlatformBuildReport report = await builder.BuildAsync(request, new RecordingProgressReporter(), new RecordingDiagnosticReporter(), CancellationToken.None);

    Assert.True(report.Succeeded);
    Assert.True(File.Exists(Path.Combine(request.OutputRoot, "helengine_ps2.elf")));
    Assert.True(File.Exists(Path.Combine(request.OutputRoot, "cooked", "scenes", "main.hasset")));
    Assert.True(File.Exists(Path.Combine(request.OutputRoot, "cooked", "imported", "box_a.hasset")));
    Assert.False(File.Exists(Path.Combine(request.OutputRoot, "ps2-build-manifest.json")));
    Assert.Equal(request.GeneratedCoreCppRootPath, nativeBuildExecutor.LastWorkspace.GeneratedCoreRootPath);
}
```

- [ ] **Step 2: Run the PS2 builder tests and verify they fail**

Run:

```powershell
rtk proxy dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj -v minimal
```

Expected: FAIL because the current builder only copies payloads and writes `ps2-build-manifest.json`.

- [ ] **Step 3: Add a native build executor boundary and workspace model**

```csharp
namespace helengine.ps2.builder;

public interface IPs2NativeBuildExecutor {
    void Build(Ps2BuildWorkspace workspace, CancellationToken cancellationToken);
}
```

```csharp
namespace helengine.ps2.builder;

public sealed class Ps2BuildWorkspace {
    public Ps2BuildWorkspace(
        string repositoryRootPath,
        string generatedCoreRootPath,
        string stagingRootPath,
        string outputRootPath,
        string executableOutputPath) {
        RepositoryRootPath = repositoryRootPath;
        GeneratedCoreRootPath = generatedCoreRootPath;
        StagingRootPath = stagingRootPath;
        OutputRootPath = outputRootPath;
        ExecutableOutputPath = executableOutputPath;
    }

    public string RepositoryRootPath { get; }
    public string GeneratedCoreRootPath { get; }
    public string StagingRootPath { get; }
    public string OutputRootPath { get; }
    public string ExecutableOutputPath { get; }
}
```

- [ ] **Step 4: Update the PS2 builder to stage cooked output and invoke Docker**

```csharp
public sealed class Ps2PlatformAssetBuilder : IPlatformAssetBuilder {
    readonly IPs2NativeBuildExecutor NativeBuildExecutor;

    public Ps2PlatformAssetBuilder()
        : this(new Ps2NativeBuildExecutor()) {
    }

    public Ps2PlatformAssetBuilder(IPs2NativeBuildExecutor nativeBuildExecutor) {
        NativeBuildExecutor = nativeBuildExecutor ?? throw new ArgumentNullException(nameof(nativeBuildExecutor));
        Descriptor = new PlatformBuilderDescriptor(
            "helengine.ps2.builder",
            "1.0.0",
            "ps2",
            new EngineCompatibilityRange("1.0.0", "999.0.0"),
            new ManifestCompatibilityRange(1, 3),
            ["ps2"],
            ["ps2"]);
        Definition = Ps2PlatformDefinitionFactory.Create();
    }

    public Task<PlatformBuildReport> BuildAsync(
        PlatformBuildRequest request,
        IPlatformBuildProgressReporter progressReporter,
        IPlatformBuildDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken) {
        StageCookedArtifacts(request.Manifest.CookedArtifacts, request.OutputRoot);
        Ps2BuildWorkspace workspace = CreateWorkspace(request);
        NativeBuildExecutor.Build(workspace, cancellationToken);
        return Task.FromResult(new PlatformBuildReport(true, [], [], []));
    }
}
```

```csharp
public sealed class Ps2NativeBuildExecutor : IPs2NativeBuildExecutor {
    public void Build(Ps2BuildWorkspace workspace, CancellationToken cancellationToken) {
        ProcessStartInfo startInfo = new("docker", "run --rm -v \"" + workspace.RepositoryRootPath + "\":/workspace -w /workspace helengine-ps2 make HELENGINE_CORE_CPP_ROOT=" + Quote(workspace.GeneratedCoreRootPath)) {
            WorkingDirectory = workspace.RepositoryRootPath
        };

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the PS2 Docker build.");
        process.WaitForExit();
        if (process.ExitCode != 0) {
            throw new InvalidOperationException("The PS2 Docker build exited with a non-zero status.");
        }
    }
}
```

- [ ] **Step 5: Re-run the PS2 builder tests and verify they pass**

Run:

```powershell
rtk proxy dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj -v minimal
```

Expected: PASS.

- [ ] **Step 6: Commit the PS2 builder changes**

```powershell
rtk proxy git -C C:\dev\helworks\helengine-ps2 add builder\IPs2NativeBuildExecutor.cs builder\Ps2BuildWorkspace.cs builder\Ps2NativeBuildExecutor.cs builder\Ps2PlatformAssetBuilder.cs builder\Program.cs builder.tests\Ps2PlatformAssetBuilderTests.cs
rtk proxy git -C C:\dev\helworks\helengine-ps2 commit -m "feat: build PS2 exports through docker packaging"
```

### Task 3: Move the PS2 host to the portable input/runtime startup path

**Files:**
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2InputBackend.hpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2InputBackend.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.hpp`
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\src\main.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\Makefile`

- [ ] **Step 1: Replace the old input classes with a PS2 `IInputBackend`**

```cpp
#pragma once

#include "IInputBackend.hpp"
#include "InputFrameState.hpp"
#include "KeyboardState.hpp"
#include "MouseState.hpp"
#include "platform/ps2/Ps2PadInputMapper.hpp"

namespace helengine::ps2 {
    class Ps2InputBackend final : public IInputBackend {
    public:
        Ps2InputBackend();

        bool Initialize();
        InputFrameState CaptureFrame();

    private:
        KeyboardState CaptureKeyboardState() const;
        MouseState CaptureMouseState() const;

        Ps2PadButtons CurrentButtons;
        Ps2PadButtons PreviousButtons;
    };
}
```

- [ ] **Step 2: Wire the PS2 host into `Core::Initialize` and compiled startup metadata**

```cpp
#include "runtime/runtime_startup_manifest.hpp"

bool Ps2BootHost::InitializeRuntime() {
    EngineCore = new Core();
    EngineOptions = EngineCore->get_InitializationOptions();
    EngineOptions->ContentRootPath = ResolveApplicationDirectoryPath();
    EngineRenderManager2D = new Ps2RenderManager2D();
    EngineRenderManager3D = new Ps2RenderManager3D();
    EngineInputBackend = new Ps2InputBackend();

    if (!EngineInputBackend->Initialize()) {
        return false;
    }

    EngineCore->Initialize(EngineRenderManager3D, EngineRenderManager2D, EngineInputBackend, EngineOptions);
    LoadPackagedStartupScene();
    return true;
}
```

```cpp
void Ps2BootHost::LoadPackagedStartupScene() {
    const char* startupSceneRelativePath = he_get_runtime_startup_scene_relative_path();
    if (startupSceneRelativePath == nullptr || startupSceneRelativePath[0] == '\0') {
        return;
    }

    Asset* startupAsset = LoadPackagedAsset(startupSceneRelativePath);
    SceneAsset* startupScene = static_cast<SceneAsset*>(startupAsset);
    EngineCore->get_SceneLoadService()->Load(startupScene);
}
```

- [ ] **Step 3: Remove the old includes and host-side `InputManager` shim**

```cpp
#include "InputManager.hpp"
#include "Keyboard.hpp"
#include "KeyboardState.hpp"
#include "Mouse.hpp"
#include "MouseState.hpp"
```

Delete the private `Ps2Keyboard`, `Ps2Mouse`, and `Ps2InputManager` classes from `Ps2BootHost.cpp`, then replace them with the new `Ps2InputBackend`.

- [ ] **Step 4: Compile the native host inside Docker**

Run:

```powershell
rtk proxy docker build -t helengine-ps2 C:\dev\helworks\helengine-ps2
rtk proxy docker run --rm -v C:\dev\helworks\helengine-ps2:/workspace -w /workspace -e HELENGINE_CORE_CPP_ROOT=/workspace/build/generated-core helengine-ps2 make
```

Expected: PASS and `C:\dev\helworks\helengine-ps2\build\helengine_ps2.elf` exists.

- [ ] **Step 5: Commit the PS2 runtime host migration**

```powershell
rtk proxy git -C C:\dev\helworks\helengine-ps2 add src\platform\ps2\Ps2InputBackend.hpp src\platform\ps2\Ps2InputBackend.cpp src\platform\ps2\Ps2BootHost.hpp src\platform\ps2\Ps2BootHost.cpp src\main.cpp Makefile
rtk proxy git -C C:\dev\helworks\helengine-ps2 commit -m "feat: load PS2 startup scene through portable runtime path"
```

### Task 4: Prove the end-to-end export on the `city` project

**Files:**
- Modify: `C:\dev\helworks\helengine-ps2\README.md`
- Modify: `docs/superpowers/specs/2026-05-03-unified-platform-runtime-build-system-design.md`

- [ ] **Step 1: Update the PS2 README with the real export contract**

```markdown
## End-to-end export

The PS2 builder is invoked by the shared HelEngine editor build graph.

Expected export layout:

- `helengine_ps2.elf`
- `cooked/scenes/main.hasset`
- `cooked/imported/...`

Runtime startup is compiled into generated native source. The final export does not ship JSON startup manifests.
```

- [ ] **Step 2: Build the editor app if needed**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd build C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\helengine.editor.app.csproj -c Debug -p:UseSharedCompilation=false -m:1 -v:minimal"
```

Expected: PASS.

- [ ] **Step 3: Run the real PS2 export for `C:\dev\helprojs\city`**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.exe --build ps2 --project C:\dev\helprojs\city\project.heproj --output C:\dev\helprojs\output\ps2"
```

Expected: PASS and the output contains:

```text
C:\dev\helprojs\output\ps2\helengine_ps2.elf
C:\dev\helprojs\output\ps2\cooked\scenes\main.hasset
```

- [ ] **Step 4: Perform the final verification pass**

Run:

```powershell
rtk proxy dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildGraphRunnerTests|FullyQualifiedName~EditorRuntimeNativeManifestWriterTests" -v minimal
rtk proxy dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the docs and final verification changes**

```powershell
rtk proxy git -C C:\dev\helworks\helengine add docs/superpowers/specs/2026-05-03-unified-platform-runtime-build-system-design.md
rtk proxy git -C C:\dev\helworks\helengine-ps2 add README.md
rtk proxy git -C C:\dev\helworks\helengine commit -m "docs: document PS2 export verification"
rtk proxy git -C C:\dev\helworks\helengine-ps2 commit -m "docs: document PS2 end-to-end export"
```

## Self-Review

- Spec coverage: this plan covers the Docker-only builder rule, generated native startup/module metadata, loose `cooked/` output, PS2 portable input migration, and the `city` end-to-end validation project.
- Placeholder scan: no `TODO`, `TBD`, or deferred implementation markers appear inside the task steps.
- Type consistency: the plan consistently uses `GeneratedCoreCppRootPath`, `PlatformBuildArtifact.RelativePath`, `he_get_runtime_startup_scene_relative_path()`, and a PS2 `IInputBackend` instead of the removed old input manager classes.
