# Deployment-Root Generated Builds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move HelEngine native build generation to a deployment-root model where `cs2.cpp` emits target-scoped generated source under `GeneratedSource/<target-id>`, native intermediates build under `Intermediate/<target-id>`, and final packaged outputs merge into one shared `Build` tree.

**Architecture:** Split the work across three boundaries: `cs2.cpp` owns target-scoped generation and handoff metadata, `helengine-windows` consumes an external generated source root and intermediate directory, and `helengine.editor` owns deployment-root orchestration and final `Build` merging. Keep the first execution slice Windows DirectX only, but make the path contract target-aware so later Mac/Linux/retro targets can share the same deployment root.

**Tech Stack:** C#, .NET, Roslyn, xUnit, `cs2.core`, `cs2.cpp`, `helengine.editor`, CMake, native C++ host scaffolding

---

## File Structure

### Existing files to modify

- `/mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPConversionOptions.cs`
  - Replace the Windows-specific handoff folder contract with deployment-root and target-id configuration.
- `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPCodeConverter.cs`
  - Emit directly into target-scoped generated source paths and stop mirroring into platform repos.
- `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPWindowsHandoffWriter.cs`
  - Generalize the handoff writer so it describes deployment-root generated output, not repo-local copied output.
- `/mnt/c/dev/helworks/helengine-windows/CMakeLists.txt`
  - Consume external generated source and intermediate/build locations instead of expecting a copied repo-local generated tree.
- `/mnt/c/dev/helworks/helengine-windows/src/platform/windows/directx/directx_feature_bootstrap.cpp`
  - Keep consuming the generated feature manifest through the external generated source root.
- `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildPlatformConfigDocument.cs`
  - Store target-aware local build defaults under the existing per-platform build config.
- `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs`
  - Persist target id and treat `OutputDirectoryPath` as the deployment root.
- `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildConfigService.cs`
  - Seed default target ids and normalize deployment-root oriented queue/build config data.
- `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorPlaceholderBuildExecutor.cs`
  - Replace the placeholder with real orchestration or remove it in favor of a new concrete executor.

### New production files to create

- `/mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPDeploymentBuildLayout.cs`
- `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPDeploymentBuildLayoutFactory.cs`
- `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildTargetDefaults.cs`
- `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildDeploymentLayout.cs`
- `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildDeploymentLayoutFactory.cs`
- `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/IEditorBuildProcessRunner.cs`
- `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildProcessRunner.cs`
- `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs`
- `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildMergeManifestDocument.cs`
- `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildMergeService.cs`

### New test files to create

- `/mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPDeploymentBuildLayoutTests.cs`
- `/mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPWindowsHandoffWriterTests.cs`
- `/mnt/c/dev/helengine/engine/helengine.editor.tests/EditorBuildDeploymentLayoutFactoryTests.cs`
- `/mnt/c/dev/helengine/engine/helengine.editor.tests/EditorPlatformBuildExecutorTests.cs`
- `/mnt/c/dev/helengine/engine/helengine.editor.tests/EditorBuildMergeServiceTests.cs`
- `/mnt/c/dev/helengine/engine/helengine.editor.tests/EditorBuildConfigServiceTests.cs`

## Notes for the implementer

- Work spans three repositories: `/mnt/c/dev/csharpcodegen`, `/mnt/c/dev/helworks/helengine-windows`, and `/mnt/c/dev/helengine`.
- Do not keep generated output inside `helengine-windows`. It must remain source-only.
- `OutputDirectoryPath` already exists in the editor queue/config model. Treat it as the deployment root, not as a final binary-only folder.
- First executable slice is `windows-directx`, but all new path/layout code must take a generic `TargetId` string.
- `GeneratedSource/<target-id>` must always be regenerated from scratch.
- `Intermediate/<target-id>` may be reused for incremental native builds.
- `Build` is shared across targets. Never compile directly into it.
- Keep the initial merge implementation deterministic: identical files can share one copy, conflicting same-path different-byte outputs must fail.

### Task 1: Replace Windows-specific generator output settings with deployment-root layout models

**Files:**
- Create: `/mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPDeploymentBuildLayout.cs`
- Create: `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPDeploymentBuildLayoutFactory.cs`
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPConversionOptions.cs`
- Test: `/mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPDeploymentBuildLayoutTests.cs`

- [ ] **Step 1: Write the failing deployment-layout tests**

```csharp
[Fact]
public void Create_WhenDeploymentRootAndTargetIdProvided_DerivesGeneratedSourceIntermediateAndBuildPaths() {
    CPPConversionOptions options = new CPPConversionOptions {
        DeploymentRoot = @"C:\\Builds\\MyGame",
        TargetId = "windows-directx"
    };

    CPPDeploymentBuildLayout layout = CPPDeploymentBuildLayoutFactory.Create(options);

    Assert.Equal(Path.Combine(options.DeploymentRoot, "GeneratedSource", "windows-directx"), layout.GeneratedSourceRoot);
    Assert.Equal(Path.Combine(options.DeploymentRoot, "Intermediate", "windows-directx"), layout.IntermediateRoot);
    Assert.Equal(Path.Combine(options.DeploymentRoot, "Build"), layout.BuildRoot);
}
```

- [ ] **Step 2: Run the focused tests and verify failure**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPDeploymentBuildLayoutTests" -v minimal`
Expected: FAIL because the deployment-root layout types and option surface do not exist yet.

- [ ] **Step 3: Add deployment-root option properties and the layout factory**

```csharp
public sealed class CPPConversionOptions {
    public string DeploymentRoot { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
}

public sealed class CPPDeploymentBuildLayout {
    public string DeploymentRoot { get; }
    public string GeneratedSourceRoot { get; }
    public string IntermediateRoot { get; }
    public string BuildRoot { get; }
}
```

- [ ] **Step 4: Run the focused tests and verify pass**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPDeploymentBuildLayoutTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/csharpcodegen add cs2.cpp/model/CPPConversionOptions.cs cs2.cpp/model/CPPDeploymentBuildLayout.cs cs2.cpp/CPPDeploymentBuildLayoutFactory.cs cs2.cpp.tests/CPPDeploymentBuildLayoutTests.cs
git -C /mnt/c/dev/csharpcodegen commit -m "Add deployment-root layout options to cs2.cpp"
```

### Task 2: Emit generated output directly into `GeneratedSource/<target-id>` and generalize the handoff manifest

**Files:**
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPCodeConverter.cs`
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPWindowsHandoffWriter.cs`
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPWindowsHandoffWriterTests.cs`
- Test: `/mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPWindowsHandoffWriterTests.cs`

- [ ] **Step 1: Extend the existing handoff-writer tests to the new target-scoped layout**

```csharp
[Fact]
public void Write_WhenDeploymentLayoutProvided_EmitsGeneratedSourceAndIntermediateContract() {
    CPPDeploymentBuildLayout layout = new CPPDeploymentBuildLayout(
        @"C:\\Builds\\MyGame",
        @"C:\\Builds\\MyGame\\GeneratedSource\\windows-directx",
        @"C:\\Builds\\MyGame\\Intermediate\\windows-directx",
        @"C:\\Builds\\MyGame\\Build");

    string cmake = CPPWindowsHandoffWriter.Write(layout);

    Assert.Contains("HELENGINE_GENERATED_CORE_ROOT", cmake);
    Assert.Contains("HELENGINE_NATIVE_INTERMEDIATE_ROOT", cmake);
    Assert.DoesNotContain("generated/helengine.core", cmake);
}
```

- [ ] **Step 2: Run the focused tests and verify failure**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPWindowsHandoffWriterTests" -v minimal`
Expected: FAIL because the writer still assumes repo-local mirrored output.

- [ ] **Step 3: Update converter emission to clear and regenerate only the target generated-source folder**

```csharp
CPPDeploymentBuildLayout layout = CPPDeploymentBuildLayoutFactory.Create(Options);
DirectoryUtil.ClearDirectory(layout.GeneratedSourceRoot);
WriteOutput(layout.GeneratedSourceRoot);
CPPWindowsHandoffWriter.Write(layout, layout.GeneratedSourceRoot);
```

- [ ] **Step 4: Run the focused tests and verify pass**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPWindowsHandoffWriterTests|FullyQualifiedName~CPPDeploymentBuildLayoutTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/csharpcodegen add cs2.cpp/CPPCodeConverter.cs cs2.cpp/CPPWindowsHandoffWriter.cs cs2.cpp.tests/CPPWindowsHandoffWriterTests.cs
git -C /mnt/c/dev/csharpcodegen commit -m "Emit target-scoped generated source from cs2.cpp"
```

### Task 3: Make `helengine-windows` consume external generated source and intermediate roots only

**Files:**
- Modify: `/mnt/c/dev/helworks/helengine-windows/CMakeLists.txt`
- Modify: `/mnt/c/dev/helworks/helengine-windows/src/platform/windows/directx/directx_feature_bootstrap.cpp`

- [ ] **Step 1: Update the CMake contract to require external generated roots**

```cmake
set(HELENGINE_CORE_CPP_ROOT "" CACHE PATH "Generated HelEngine core source root")
set(HELENGINE_NATIVE_INTERMEDIATE_ROOT "" CACHE PATH "Native intermediate build root")

if(NOT HELENGINE_CORE_CPP_ROOT)
    message(FATAL_ERROR "HELENGINE_CORE_CPP_ROOT must be provided by the deployment build orchestrator.")
endif()
```

- [ ] **Step 2: Run a configure command and verify the old in-repo default path fails fast**

Run: `cmake -S /mnt/c/dev/helworks/helengine-windows -B /tmp/helengine-windows-cmake-test`
Expected: FAIL with a clear message that `HELENGINE_CORE_CPP_ROOT` must be supplied.

- [ ] **Step 3: Keep feature-manifest consumption rooted in the external generated tree**

```cmake
include(${HELENGINE_CORE_CPP_ROOT}/helengine_windows_handoff.cmake)
target_sources(helengine-windows PRIVATE ${HELENGINE_GENERATED_UNITY_SOURCE})
```

- [ ] **Step 4: Re-run configure with explicit generated and intermediate roots**

Run: `cmake -S /mnt/c/dev/helworks/helengine-windows -B /tmp/helengine-windows-cmake-test -DHELENGINE_CORE_CPP_ROOT=/tmp/generated-core -DHELENGINE_NATIVE_INTERMEDIATE_ROOT=/tmp/generated-intermediate`
Expected: CONFIGURE completes or progresses to the next missing generated-file/toolchain validation instead of assuming repo-local generated output.

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/helworks/helengine-windows add CMakeLists.txt src/platform/windows/directx/directx_feature_bootstrap.cpp
git -C /mnt/c/dev/helworks/helengine-windows commit -m "Require external generated source roots in helengine-windows"
```

### Task 4: Persist deployment-root and target defaults in editor-local build config

**Files:**
- Create: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildTargetDefaults.cs`
- Modify: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildPlatformConfigDocument.cs`
- Modify: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs`
- Modify: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildConfigService.cs`
- Modify: `/mnt/c/dev/helengine/engine/helengine.editor.tests/EditorBuildConfigServiceTests.cs`
- Test: `/mnt/c/dev/helengine/engine/helengine.editor.tests/EditorBuildConfigServiceTests.cs`

- [ ] **Step 1: Add failing tests for target-id seeding and queue-item persistence**

```csharp
[Fact]
public void Load_WhenPlatformEntryIsCreated_SeedsDefaultTargetId() {
    EditorBuildConfigService service = new EditorBuildConfigService(ProjectRootPath);

    EditorBuildConfigDocument document = service.Load(new[] { "windows" }, "scenes/main.scene");

    Assert.Equal("windows-directx", document.Platforms[0].TargetId);
}

[Fact]
public void SaveAndLoad_WhenQueueItemHasTargetId_PreservesDeploymentRootAndTargetId() {
    EditorBuildConfigDocument document = new EditorBuildConfigDocument {
        QueueItems = {
            new EditorBuildQueueItemDocument {
                PlatformId = "windows",
                TargetId = "windows-directx",
                OutputDirectoryPath = @"C:\\Builds\\MyGame"
            }
        }
    };

    service.Save(document);

    EditorBuildConfigDocument reloaded = service.Load(new[] { "windows" }, string.Empty);
    Assert.Equal("windows-directx", reloaded.QueueItems[0].TargetId);
    Assert.Equal(@"C:\\Builds\\MyGame", reloaded.QueueItems[0].OutputDirectoryPath);
}
```

- [ ] **Step 2: Run the focused tests and verify failure**

Run: `rtk dotnet test /mnt/c/dev/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorBuildConfigServiceTests" -v minimal`
Expected: FAIL because target id is not part of the build config model yet.

- [ ] **Step 3: Add `TargetId` to the persisted build config and seed defaults through one dedicated defaults class**

```csharp
public sealed class EditorBuildPlatformConfigDocument {
    public string PlatformId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public List<string> SelectedSceneIds { get; set; } = [];
    public string OutputDirectoryPath { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Run the focused tests and verify pass**

Run: `rtk dotnet test /mnt/c/dev/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorBuildConfigServiceTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/helengine add engine/helengine.editor/managers/project/EditorBuildTargetDefaults.cs engine/helengine.editor/managers/project/EditorBuildPlatformConfigDocument.cs engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs engine/helengine.editor/managers/project/EditorBuildConfigService.cs engine/helengine.editor.tests/EditorBuildConfigServiceTests.cs
git -C /mnt/c/dev/helengine commit -m "Persist deployment target defaults in editor build config"
```

### Task 5: Add editor-side deployment-root layout derivation and a real platform build executor

**Files:**
- Create: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildDeploymentLayout.cs`
- Create: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildDeploymentLayoutFactory.cs`
- Create: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/IEditorBuildProcessRunner.cs`
- Create: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildProcessRunner.cs`
- Create: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs`
- Modify: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/IEditorBuildExecutor.cs`
- Modify: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorPlaceholderBuildExecutor.cs`
- Test: `/mnt/c/dev/helengine/engine/helengine.editor.tests/EditorBuildDeploymentLayoutFactoryTests.cs`
- Test: `/mnt/c/dev/helengine/engine/helengine.editor.tests/EditorPlatformBuildExecutorTests.cs`

- [ ] **Step 1: Write failing layout and executor tests**

```csharp
[Fact]
public void Create_WhenQueueItemHasDeploymentRootAndTargetId_DerivesTargetScopedPaths() {
    EditorBuildQueueItemDocument queueItem = new EditorBuildQueueItemDocument {
        OutputDirectoryPath = @"C:\\Builds\\MyGame",
        TargetId = "windows-directx"
    };

    EditorBuildDeploymentLayout layout = EditorBuildDeploymentLayoutFactory.Create(queueItem);

    Assert.Equal(Path.Combine(queueItem.OutputDirectoryPath, "GeneratedSource", "windows-directx"), layout.GeneratedSourceRoot);
    Assert.Equal(Path.Combine(queueItem.OutputDirectoryPath, "Intermediate", "windows-directx"), layout.IntermediateRoot);
    Assert.Equal(Path.Combine(queueItem.OutputDirectoryPath, "Build"), layout.BuildRoot);
}

[Fact]
public void Execute_WhenWindowsDirectxQueueItemIsProvided_InvokesCodegenThenCMakeWithDerivedPaths() {
    FakeEditorBuildProcessRunner runner = new FakeEditorBuildProcessRunner();
    EditorPlatformBuildExecutor executor = new EditorPlatformBuildExecutor(runner, ...);

    EditorBuildExecutionResult result = executor.Execute(queueItem);

    Assert.True(result.Succeeded);
    Assert.Collection(runner.Commands,
        command => Assert.Contains("csharpcodegen", command.FileName),
        command => Assert.Contains("cmake", command.FileName));
}
```

- [ ] **Step 2: Run the focused tests and verify failure**

Run: `rtk dotnet test /mnt/c/dev/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorBuildDeploymentLayoutFactoryTests|FullyQualifiedName~EditorPlatformBuildExecutorTests" -v minimal`
Expected: FAIL because no deployment-layout factory or concrete executor exists.

- [ ] **Step 3: Implement one Windows DirectX orchestration executor behind a process-runner abstraction**

```csharp
public sealed class EditorPlatformBuildExecutor : IEditorBuildExecutor {
    public EditorBuildExecutionResult Execute(EditorBuildQueueItemDocument queueItem) {
        EditorBuildDeploymentLayout layout = EditorBuildDeploymentLayoutFactory.Create(queueItem);
        RunCodeGeneration(queueItem, layout);
        RunNativeConfigureAndBuild(queueItem, layout);
        return EditorBuildExecutionResult.Success("Build completed successfully.");
    }
}
```

- [ ] **Step 4: Re-run the focused tests and verify pass**

Run: `rtk dotnet test /mnt/c/dev/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorBuildDeploymentLayoutFactoryTests|FullyQualifiedName~EditorPlatformBuildExecutorTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/helengine add engine/helengine.editor/managers/project/EditorBuildDeploymentLayout.cs engine/helengine.editor/managers/project/EditorBuildDeploymentLayoutFactory.cs engine/helengine.editor/managers/project/IEditorBuildProcessRunner.cs engine/helengine.editor/managers/project/EditorBuildProcessRunner.cs engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs engine/helengine.editor/managers/project/IEditorBuildExecutor.cs engine/helengine.editor/managers/project/EditorPlaceholderBuildExecutor.cs engine/helengine.editor.tests/EditorBuildDeploymentLayoutFactoryTests.cs engine/helengine.editor.tests/EditorPlatformBuildExecutorTests.cs
git -C /mnt/c/dev/helengine commit -m "Add deployment-root platform build executor"
```

### Task 6: Add staged merge into the shared `Build` tree with deterministic conflict handling

**Files:**
- Create: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildMergeManifestDocument.cs`
- Create: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildMergeService.cs`
- Test: `/mnt/c/dev/helengine/engine/helengine.editor.tests/EditorBuildMergeServiceTests.cs`

- [ ] **Step 1: Write failing merge tests for identical-file sharing and conflicting-output failure**

```csharp
[Fact]
public void Merge_WhenFilesAreByteIdentical_KeepsOneSharedCopy() {
    EditorBuildMergeService service = new EditorBuildMergeService();

    service.Merge(stagingRootA, buildRoot, "windows-directx");
    service.Merge(stagingRootB, buildRoot, "linux");

    Assert.Single(Directory.GetFiles(buildRoot, "shared.dat", SearchOption.AllDirectories));
}

[Fact]
public void Merge_WhenSameRelativePathDiffers_ThrowsInvalidOperationException() {
    EditorBuildMergeService service = new EditorBuildMergeService();

    service.Merge(stagingRootA, buildRoot, "windows-directx");

    Assert.Throws<InvalidOperationException>(() => service.Merge(stagingRootB, buildRoot, "mac"));
}
```

- [ ] **Step 2: Run the focused tests and verify failure**

Run: `rtk dotnet test /mnt/c/dev/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorBuildMergeServiceTests" -v minimal`
Expected: FAIL because no merge service exists.

- [ ] **Step 3: Implement the shared-build merge service and manifest writer**

```csharp
public sealed class EditorBuildMergeService {
    public void Merge(string stagingRoot, string buildRoot, string targetId) {
        // Copy missing files, skip byte-identical files, and throw on conflicting same-path different-byte files.
    }
}
```

- [ ] **Step 4: Run the focused tests and verify pass**

Run: `rtk dotnet test /mnt/c/dev/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorBuildMergeServiceTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/helengine add engine/helengine.editor/managers/project/EditorBuildMergeManifestDocument.cs engine/helengine.editor/managers/project/EditorBuildMergeService.cs engine/helengine.editor.tests/EditorBuildMergeServiceTests.cs
git -C /mnt/c/dev/helengine commit -m "Add shared deployment build merge service"
```

### Task 7: Verify the end-to-end deployment-root contract across generator, editor, and Windows host

**Files:**
- Modify: `/mnt/c/dev/helengine/docs/superpowers/plans/2026-04-29-deployment-root-generated-builds.md` if verification reveals any required plan correction.

- [ ] **Step 1: Run the focused `cs2.cpp` deployment-layout tests**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPDeploymentBuildLayoutTests|FullyQualifiedName~CPPWindowsHandoffWriterTests" -v minimal`
Expected: PASS

- [ ] **Step 2: Run the focused editor deployment/build tests**

Run: `rtk dotnet test /mnt/c/dev/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorBuildConfigServiceTests|FullyQualifiedName~EditorBuildDeploymentLayoutFactoryTests|FullyQualifiedName~EditorPlatformBuildExecutorTests|FullyQualifiedName~EditorBuildMergeServiceTests" -v minimal`
Expected: PASS

- [ ] **Step 3: Run the Windows host configure verification with explicit generated-source and intermediate roots**

Run: `cmake -S /mnt/c/dev/helworks/helengine-windows -B /tmp/helengine-windows-cmake-test -DHELENGINE_CORE_CPP_ROOT=/tmp/helengine-generated/windows-directx -DHELENGINE_NATIVE_INTERMEDIATE_ROOT=/tmp/helengine-intermediate/windows-directx`
Expected: configure completes or fails only on expected missing generated payload/toolchain details, not on repo-local generated-path assumptions.

- [ ] **Step 4: Perform one manual smoke run of the real executor against a temp deployment root**

Run: `rtk dotnet test /mnt/c/dev/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildExecutorTests.Execute_WhenWindowsDirectxQueueItemIsProvided_InvokesCodegenThenCMakeWithDerivedPaths" -v minimal`
Expected: PASS and command capture shows generation under `GeneratedSource/windows-directx`, staging under `Intermediate/windows-directx`, and shared output root under `Build`.

- [ ] **Step 5: Commit final verification or follow-up adjustments**

```bash
git -C /mnt/c/dev/csharpcodegen status --short
git -C /mnt/c/dev/helworks/helengine-windows status --short
git -C /mnt/c/dev/helengine status --short
```
