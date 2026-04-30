# Docker Target Builders Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace direct host-native Windows build execution with a Docker-based `windows-directx` builder contract that generates into the deployment root and builds the Windows host from a Windows-capable container image.

**Architecture:** Keep the existing deployment-root layout and target ids, but shift native build orchestration to a target-to-image Docker contract. The editor resolves `windows-directx` to one builder image, validates Docker/host compatibility, invokes `docker run` with the deployment-root mounts, and the Windows builder image runs code generation, CMake configure, native build, staging, and merge through one explicit entrypoint.

**Tech Stack:** C#, xUnit, .NET CLI, Docker CLI, Windows containers, PowerShell, CMake, MSVC toolchain, existing `cs2.cpp` generator, existing `helengine-windows` CMake host project.

---

## File Structure

### `helengine` repo

- Modify: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs`
  - Replace direct `dotnet` and `cmake` orchestration with Docker builder orchestration for `windows-directx`.
- Create: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorDockerBuildContract.cs`
  - Value object that describes image tag, container platform type, bind mounts, and entrypoint arguments.
- Create: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorDockerBuilderCatalog.cs`
  - Maps concrete `TargetId` values to Docker builder image definitions.
- Create: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorDockerHostCompatibilityService.cs`
  - Validates that Docker is available and the local machine can run the required container type for `windows-directx`.
- Modify: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/IEditorBuildProcessRunner.cs`
  - Keep the simple process-launch abstraction, but make sure it is sufficient for Docker command execution and diagnostics.
- Modify: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildProcessRunner.cs`
  - Preserve behavior while ensuring Docker failures surface clearly.
- Test: `/mnt/c/dev/helengine/engine/helengine.editor.tests/EditorPlatformBuildExecutorTests.cs`
  - Update orchestration expectations from `dotnet` + `cmake` to `docker run`.
- Create: `/mnt/c/dev/helengine/engine/helengine.editor.tests/EditorDockerBuilderCatalogTests.cs`
  - Covers target-to-image resolution.
- Create: `/mnt/c/dev/helengine/engine/helengine.editor.tests/EditorDockerHostCompatibilityServiceTests.cs`
  - Covers Windows-container compatibility diagnostics.

### `helengine-windows` repo

- Modify: `/mnt/c/dev/helworks/helengine-windows/CMakeLists.txt`
  - Finalize the deployment-root external input contract and ensure the container build can point CMake at generated source and intermediate roots explicitly.
- Create: `/mnt/c/dev/helworks/helengine-windows/docker/windows-directx/Dockerfile`
  - Defines the Windows builder image with the exact Windows DirectX build toolchain.
- Create: `/mnt/c/dev/helworks/helengine-windows/docker/windows-directx/entrypoint.ps1`
  - Thin builder entrypoint that validates arguments, runs generation, runs CMake configure/build, stages outputs, merges into shared `Build`, and emits logs/manifests.
- Create: `/mnt/c/dev/helworks/helengine-windows/docker/windows-directx/merge-build-output.ps1`
  - Builder-side file merge utility for staged outputs into shared `Build` with identical-file deduplication and conflict failure.
- Create: `/mnt/c/dev/helworks/helengine-windows/docker/windows-directx/write-build-invocation-manifest.ps1`
  - Writes machine-readable builder metadata into the deployment root.
- Create: `/mnt/c/dev/helworks/helengine-windows/docker/windows-directx/README.md`
  - Documents required Docker host mode, image build command, and local execution contract.

### `csharpcodegen` repo

- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPConversionOptions.cs`
  - Remove or deprecate the Windows-repo-specific handoff wording if it conflicts with the Docker deployment-root contract.
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPWindowsHandoffWriter.cs`
  - Keep emitting the deployment-root handoff variables that the Windows host and builder entrypoint consume.
- Test: `/mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPWindowsHandoffWriterTests.cs`
  - Tighten expectations around generated variables used by the Dockerized Windows builder.

## Task 1: Lock the editor-side Docker builder domain model

**Files:**
- Create: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorDockerBuildContract.cs`
- Create: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorDockerBuilderCatalog.cs`
- Create: `/mnt/c/dev/helengine/engine/helengine.editor.tests/EditorDockerBuilderCatalogTests.cs`

- [ ] **Step 1: Write the failing catalog test**

```csharp
[Fact]
public void Resolve_WhenWindowsDirectxTargetIsRequested_ReturnsWindowsDirectxBuilderImage() {
    EditorDockerBuildContract contract = EditorDockerBuilderCatalog.Resolve("windows", "windows-directx");

    Assert.Equal("helengine-builder:windows-directx", contract.ImageTag);
    Assert.Equal("windows", contract.ContainerPlatform);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test /mnt/c/dev/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorDockerBuilderCatalogTests.Resolve_WhenWindowsDirectxTargetIsRequested_ReturnsWindowsDirectxBuilderImage" -v minimal`
Expected: FAIL because `EditorDockerBuilderCatalog` and `EditorDockerBuildContract` do not exist.

- [ ] **Step 3: Write the minimal catalog and contract implementation**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Describes one Docker builder image contract for one concrete target build.
    /// </summary>
    public sealed class EditorDockerBuildContract {
        /// <summary>
        /// Gets or sets the Docker image tag used for the target build.
        /// </summary>
        public string ImageTag { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the required container platform family.
        /// </summary>
        public string ContainerPlatform { get; set; } = string.Empty;
    }

    /// <summary>
    /// Resolves concrete editor build targets to Docker builder image contracts.
    /// </summary>
    public static class EditorDockerBuilderCatalog {
        /// <summary>
        /// Resolves one concrete builder contract for the requested platform and target ids.
        /// </summary>
        public static EditorDockerBuildContract Resolve(string platformId, string targetId) {
            if (platformId == "windows" && targetId == "windows-directx") {
                return new EditorDockerBuildContract {
                    ImageTag = "helengine-builder:windows-directx",
                    ContainerPlatform = "windows"
                };
            }

            throw new InvalidOperationException($"Unsupported Docker builder target '{platformId}/{targetId}'.");
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `rtk dotnet test /mnt/c/dev/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorDockerBuilderCatalogTests.Resolve_WhenWindowsDirectxTargetIsRequested_ReturnsWindowsDirectxBuilderImage" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/helengine add \
  engine/helengine.editor/managers/project/EditorDockerBuildContract.cs \
  engine/helengine.editor/managers/project/EditorDockerBuilderCatalog.cs \
  engine/helengine.editor.tests/EditorDockerBuilderCatalogTests.cs

git -C /mnt/c/dev/helengine commit -m "Add Docker builder catalog for windows-directx"
```

## Task 2: Add Docker host compatibility validation

**Files:**
- Create: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorDockerHostCompatibilityService.cs`
- Create: `/mnt/c/dev/helengine/engine/helengine.editor.tests/EditorDockerHostCompatibilityServiceTests.cs`
- Modify: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/IEditorBuildProcessRunner.cs`
- Modify: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildProcessRunner.cs`

- [ ] **Step 1: Write the failing compatibility tests**

```csharp
[Fact]
public void Validate_WhenWindowsTargetRunsOnNonWindowsHost_ReturnsFailure() {
    EditorDockerBuildContract contract = new EditorDockerBuildContract {
        ImageTag = "helengine-builder:windows-directx",
        ContainerPlatform = "windows"
    };
    FakeDockerProbe probe = new FakeDockerProbe {
        DockerAvailable = true,
        HostPlatform = "linux"
    };
    EditorDockerHostCompatibilityService service = new EditorDockerHostCompatibilityService(probe);

    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.Validate(contract));

    Assert.Contains("Windows containers", exception.Message);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `rtk dotnet test /mnt/c/dev/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorDockerHostCompatibilityServiceTests" -v minimal`
Expected: FAIL because the compatibility service and Docker probe abstraction do not exist.

- [ ] **Step 3: Implement the compatibility service and probe usage**

```csharp
public sealed class EditorDockerHostCompatibilityService {
    readonly IEditorBuildProcessRunner ProcessRunner;

    public EditorDockerHostCompatibilityService(IEditorBuildProcessRunner processRunner) {
        ProcessRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public void Validate(EditorDockerBuildContract contract) {
        if (contract == null) {
            throw new ArgumentNullException(nameof(contract));
        }

        if (!OperatingSystem.IsWindows() && contract.ContainerPlatform == "windows") {
            throw new InvalidOperationException("The selected target requires Windows containers and must run on a Windows Docker host.");
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `rtk dotnet test /mnt/c/dev/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorDockerHostCompatibilityServiceTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/helengine add \
  engine/helengine.editor/managers/project/EditorDockerHostCompatibilityService.cs \
  engine/helengine.editor/managers/project/IEditorBuildProcessRunner.cs \
  engine/helengine.editor/managers/project/EditorBuildProcessRunner.cs \
  engine/helengine.editor.tests/EditorDockerHostCompatibilityServiceTests.cs

git -C /mnt/c/dev/helengine commit -m "Add Docker host compatibility validation"
```

## Task 3: Cut the editor executor over to `docker run`

**Files:**
- Modify: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs`
- Modify: `/mnt/c/dev/helengine/engine/helengine.editor.tests/EditorPlatformBuildExecutorTests.cs`
- Reuse: `/mnt/c/dev/helengine/engine/helengine.editor/managers/project/EditorBuildDeploymentLayoutFactory.cs`

- [ ] **Step 1: Rewrite the failing executor test around Docker orchestration**

```csharp
[Fact]
public void Execute_WhenWindowsDirectxQueueItemIsProvided_InvokesDockerRunWithDeploymentRootArguments() {
    FakeEditorBuildProcessRunner runner = new FakeEditorBuildProcessRunner();
    EditorPlatformBuildExecutor executor = new EditorPlatformBuildExecutor(
        runner,
        "/mnt/c/dev/csharpcodegen",
        "/mnt/c/dev/helworks/helengine-windows");
    EditorBuildQueueItemDocument queueItem = new EditorBuildQueueItemDocument {
        PlatformId = "windows",
        TargetId = "windows-directx",
        OutputDirectoryPath = @"C:\Builds\MyGame"
    };

    EditorBuildExecutionResult result = executor.Execute(queueItem);

    Assert.True(result.Succeeded);
    Assert.Single(runner.Commands);
    Assert.Equal("docker", runner.Commands[0].FileName);
    Assert.Contains("helengine-builder:windows-directx", runner.Commands[0].Arguments);
    Assert.Contains("C:\\Builds\\MyGame", runner.Commands[0].Arguments);
    Assert.Contains("windows-directx", runner.Commands[0].Arguments);
}
```

- [ ] **Step 2: Run the executor tests to verify they fail**

Run: `rtk dotnet test /mnt/c/dev/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildExecutorTests" -v minimal`
Expected: FAIL because the executor still launches `dotnet` and `cmake` directly.

- [ ] **Step 3: Implement the Dockerized executor path**

```csharp
EditorDockerBuildContract contract = EditorDockerBuilderCatalog.Resolve(queueItem.PlatformId, queueItem.TargetId);
EditorBuildDeploymentLayout layout = EditorBuildDeploymentLayoutFactory.Create(queueItem);
EditorDockerHostCompatibilityService compatibilityService = new EditorDockerHostCompatibilityService(ProcessRunner);
compatibilityService.Validate(contract);

string dockerArguments =
    $"run --rm " +
    $"-v \"{CodegenRepositoryRootPath}:C:/src/csharpcodegen\" " +
    $"-v \"{WindowsHostRepositoryRootPath}:C:/src/helengine-windows\" " +
    $"-v \"{queueItem.OutputDirectoryPath}:C:/deploy\" " +
    $"{contract.ImageTag} " +
    $"-DeploymentRoot \"C:/deploy\" -TargetId \"{queueItem.TargetId}\"";

ProcessRunner.Run("docker", dockerArguments, WindowsHostRepositoryRootPath);
```

- [ ] **Step 4: Run the executor tests to verify they pass**

Run: `rtk dotnet test /mnt/c/dev/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildExecutorTests|FullyQualifiedName~EditorDockerBuilderCatalogTests|FullyQualifiedName~EditorDockerHostCompatibilityServiceTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/helengine add \
  engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs \
  engine/helengine.editor.tests/EditorPlatformBuildExecutorTests.cs

git -C /mnt/c/dev/helengine commit -m "Run windows-directx builds through Docker"
```

## Task 4: Finalize the generated-core handoff contract for the Docker builder

**Files:**
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPConversionOptions.cs`
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPWindowsHandoffWriter.cs`
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPWindowsHandoffWriterTests.cs`

- [ ] **Step 1: Add the failing handoff-writer test**

```csharp
[Fact]
public void Write_WithDeploymentLayout_WritesGeneratedAndIntermediateVariablesUsedByDockerBuilder() {
    CPPDeploymentBuildLayout layout = new CPPDeploymentBuildLayout(
        @"C:\Builds\MyGame",
        @"C:\Builds\MyGame\GeneratedSource\windows-directx",
        @"C:\Builds\MyGame\Intermediate\windows-directx",
        @"C:\Builds\MyGame\Build");

    string fileText = CPPWindowsHandoffWriter.Write(layout);

    Assert.Contains("HELENGINE_GENERATED_CORE_ROOT", fileText);
    Assert.Contains("HELENGINE_NATIVE_INTERMEDIATE_ROOT", fileText);
    Assert.Contains("HELENGINE_GENERATED_UNITY_SOURCE", fileText);
}
```

- [ ] **Step 2: Run the focused test to verify it fails if contract changes are still needed**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPWindowsHandoffWriterTests" -v minimal`
Expected: FAIL only if the current handoff text is missing the Docker builder contract variables; otherwise adjust the test to lock current behavior and skip implementation changes.

- [ ] **Step 3: Implement or normalize the handoff contract**

```csharp
return $$"""
set(HELENGINE_GENERATED_CORE_ROOT "${CMAKE_CURRENT_LIST_DIR}")
set(HELENGINE_NATIVE_INTERMEDIATE_ROOT "{{intermediateRoot}}")
set(HELENGINE_GENERATED_CONFIG_HEADER "${HELENGINE_GENERATED_CORE_ROOT}/helcpp_config.hpp")
set(HELENGINE_GENERATED_UNITY_SOURCE "${HELENGINE_GENERATED_CORE_ROOT}/helengine_core_unity.cpp")
set(HELENGINE_GENERATED_FEATURE_MANIFEST_HEADER "${HELENGINE_GENERATED_CORE_ROOT}/runtime/feature_manifest.hpp")
""";
```

- [ ] **Step 4: Run the focused tests to verify they pass**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPWindowsHandoffWriterTests|FullyQualifiedName~CPPDeploymentBuildLayoutTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/csharpcodegen add \
  cs2.cpp/model/CPPConversionOptions.cs \
  cs2.cpp/CPPWindowsHandoffWriter.cs \
  cs2.cpp.tests/CPPWindowsHandoffWriterTests.cs

git -C /mnt/c/dev/csharpcodegen commit -m "Stabilize generated-core handoff for Docker builders"
```

## Task 5: Add the Windows builder image and entrypoint

**Files:**
- Create: `/mnt/c/dev/helworks/helengine-windows/docker/windows-directx/Dockerfile`
- Create: `/mnt/c/dev/helworks/helengine-windows/docker/windows-directx/entrypoint.ps1`
- Create: `/mnt/c/dev/helworks/helengine-windows/docker/windows-directx/write-build-invocation-manifest.ps1`
- Create: `/mnt/c/dev/helworks/helengine-windows/docker/windows-directx/README.md`

- [ ] **Step 1: Write a failing contract test or fixture assertion for the builder scripts**

```text
Expected builder files:
- docker/windows-directx/Dockerfile
- docker/windows-directx/entrypoint.ps1
- docker/windows-directx/write-build-invocation-manifest.ps1
```

If no existing test project exists in `helengine-windows`, add a minimal smoke assertion to the editor-side test suite that checks the expected builder paths exist before invoking Docker.

- [ ] **Step 2: Run the failing check**

Run: `rtk test -f /mnt/c/dev/helworks/helengine-windows/docker/windows-directx/Dockerfile || true`
Expected: FAIL because the Docker builder files do not exist yet.

- [ ] **Step 3: Create the Windows builder image and entrypoint**

```powershell
param(
    [Parameter(Mandatory = $true)]
    [string]$DeploymentRoot,

    [Parameter(Mandatory = $true)]
    [string]$TargetId
)

$GeneratedRoot = Join-Path $DeploymentRoot (Join-Path "GeneratedSource" $TargetId)
$IntermediateRoot = Join-Path $DeploymentRoot (Join-Path "Intermediate" $TargetId)
$BuildRoot = Join-Path $DeploymentRoot "Build"

& dotnet run --project C:\src\csharpcodegen\cs2.cpp\cs2.cpp.csproj -- --deployment-root $DeploymentRoot --target-id $TargetId
& cmake -S C:\src\helengine-windows -B $IntermediateRoot -DHELENGINE_CORE_CPP_ROOT=$GeneratedRoot -DHELENGINE_NATIVE_INTERMEDIATE_ROOT=$IntermediateRoot
& cmake --build $IntermediateRoot --config Release
```

- [ ] **Step 4: Run a static verification of the created files**

Run: `rtk rg --files /mnt/c/dev/helworks/helengine-windows/docker/windows-directx`
Expected: lists `Dockerfile`, `entrypoint.ps1`, `write-build-invocation-manifest.ps1`, and `README.md`

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/helworks/helengine-windows add \
  docker/windows-directx/Dockerfile \
  docker/windows-directx/entrypoint.ps1 \
  docker/windows-directx/write-build-invocation-manifest.ps1 \
  docker/windows-directx/README.md

git -C /mnt/c/dev/helworks/helengine-windows commit -m "Add windows-directx Docker builder image"
```

## Task 6: Add builder-side merge and invocation metadata

**Files:**
- Create: `/mnt/c/dev/helworks/helengine-windows/docker/windows-directx/merge-build-output.ps1`
- Modify: `/mnt/c/dev/helworks/helengine-windows/docker/windows-directx/entrypoint.ps1`
- Modify: `/mnt/c/dev/helworks/helengine-windows/docker/windows-directx/write-build-invocation-manifest.ps1`

- [ ] **Step 1: Write a failing builder-merge smoke check**

```text
Expected behavior:
- identical files at the same relative path are shared
- different bytes at the same relative path fail the build
- build invocation manifest records image tag and target id
```

If there is no PowerShell test harness yet, document the manual verification script in the README and add a deterministic sample merge fixture under the Docker folder for future automation.

- [ ] **Step 2: Run the failing smoke check**

Run: `rtk test -f /mnt/c/dev/helworks/helengine-windows/docker/windows-directx/merge-build-output.ps1 || true`
Expected: FAIL because the merge helper does not exist yet.

- [ ] **Step 3: Implement builder-side merge and manifest writing**

```powershell
if (Test-Path $DestinationPath) {
    $existingBytes = [System.IO.File]::ReadAllBytes($DestinationPath)
    $incomingBytes = [System.IO.File]::ReadAllBytes($SourcePath)

    if (-not ($existingBytes.SequenceEqual($incomingBytes))) {
        throw "Build merge conflict at '$RelativePath'."
    }

    return
}

Copy-Item $SourcePath $DestinationPath
```

- [ ] **Step 4: Run the smoke check to verify the scripts now exist and can be invoked**

Run: `rtk rg --files /mnt/c/dev/helworks/helengine-windows/docker/windows-directx | rg 'merge-build-output|write-build-invocation-manifest|entrypoint'`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/helworks/helengine-windows add \
  docker/windows-directx/merge-build-output.ps1 \
  docker/windows-directx/entrypoint.ps1 \
  docker/windows-directx/write-build-invocation-manifest.ps1

git -C /mnt/c/dev/helworks/helengine-windows commit -m "Add Docker builder merge and manifests"
```

## Task 7: Finalize `helengine-windows` for external generated-source Docker builds

**Files:**
- Modify: `/mnt/c/dev/helworks/helengine-windows/CMakeLists.txt`
- Optional modify if needed: `/mnt/c/dev/helworks/helengine-windows/src/main.cpp`
- Optional modify if needed: `/mnt/c/dev/helworks/helengine-windows/src/platform/windows/directx/directx_feature_bootstrap.cpp`

- [ ] **Step 1: Add or tighten a failing configure contract check**

```cmake
if("${HELENGINE_CORE_CPP_ROOT}" STREQUAL "")
    message(FATAL_ERROR "HELENGINE_CORE_CPP_ROOT must point at the generated helengine.core C++ output folder.")
endif()

if("${HELENGINE_NATIVE_INTERMEDIATE_ROOT}" STREQUAL "")
    message(FATAL_ERROR "HELENGINE_NATIVE_INTERMEDIATE_ROOT must point at the target-specific intermediate build folder.")
endif()
```

- [ ] **Step 2: Run the configure path to verify the current contract is incomplete**

Run: `rtk cmake -S /mnt/c/dev/helworks/helengine-windows -B /tmp/helengine-windows-cmake-smoke -DHELENGINE_CORE_CPP_ROOT=/tmp/missing -DHELENGINE_NATIVE_INTERMEDIATE_ROOT=/tmp/helengine-windows-cmake-smoke`
Expected: FAIL with a direct contract or missing-generated-output diagnostic.

- [ ] **Step 3: Implement the minimal CMake contract updates**

```cmake
set(HELENGINE_NATIVE_INTERMEDIATE_ROOT "" CACHE PATH "Target-specific intermediate build folder.")

if("${HELENGINE_NATIVE_INTERMEDIATE_ROOT}" STREQUAL "")
    message(FATAL_ERROR "HELENGINE_NATIVE_INTERMEDIATE_ROOT must point at the target-specific intermediate build folder.")
endif()

set_target_properties(helengine_windows PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${HELENGINE_NATIVE_INTERMEDIATE_ROOT}/stage"
    LIBRARY_OUTPUT_DIRECTORY "${HELENGINE_NATIVE_INTERMEDIATE_ROOT}/stage"
    ARCHIVE_OUTPUT_DIRECTORY "${HELENGINE_NATIVE_INTERMEDIATE_ROOT}/stage"
)
```

- [ ] **Step 4: Run the configure verification again**

Run: `rtk cmake -S /mnt/c/dev/helworks/helengine-windows -B /tmp/helengine-windows-cmake-smoke -DHELENGINE_CORE_CPP_ROOT=/tmp/missing -DHELENGINE_NATIVE_INTERMEDIATE_ROOT=/tmp/helengine-windows-cmake-smoke`
Expected: FAIL only on the missing generated core, not on contract ambiguity.

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/helworks/helengine-windows add CMakeLists.txt src/main.cpp src/platform/windows/directx/directx_feature_bootstrap.cpp

git -C /mnt/c/dev/helworks/helengine-windows commit -m "Finalize external generated-core Windows host contract"
```

## Task 8: End-to-end Docker builder verification

**Files:**
- Verify: `/mnt/c/dev/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj`
- Verify: `/mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj`
- Verify: `/mnt/c/dev/helworks/helengine-windows/docker/windows-directx/*`

- [ ] **Step 1: Run focused editor tests**

Run: `rtk dotnet test /mnt/c/dev/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorDockerBuilderCatalogTests|FullyQualifiedName~EditorDockerHostCompatibilityServiceTests|FullyQualifiedName~EditorPlatformBuildExecutorTests|FullyQualifiedName~EditorBuildDeploymentLayoutFactoryTests|FullyQualifiedName~EditorBuildMergeServiceTests" -v minimal`
Expected: PASS

- [ ] **Step 2: Run focused generator tests**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPWindowsHandoffWriterTests|FullyQualifiedName~CPPDeploymentBuildLayoutTests" -v minimal`
Expected: PASS

- [ ] **Step 3: Build the Windows Docker image**

Run: `rtk docker build -t helengine-builder:windows-directx /mnt/c/dev/helworks/helengine-windows/docker/windows-directx`
Expected: PASS on a Windows Docker host capable of Windows containers.

- [ ] **Step 4: Run one local Windows DirectX Docker build against a disposable deployment root**

Run: `rtk docker run --rm -v C:\\dev\\helworks\\helengine-windows:C:\\src\\helengine-windows -v C:\\dev\\csharpcodegen:C:\\src\\csharpcodegen -v C:\\temp\\helengine-docker-build:C:\\deploy helengine-builder:windows-directx -DeploymentRoot C:\\deploy -TargetId windows-directx`
Expected: PASS and create:
- `C:\temp\helengine-docker-build\GeneratedSource\windows-directx`
- `C:\temp\helengine-docker-build\Intermediate\windows-directx`
- `C:\temp\helengine-docker-build\Build`

- [ ] **Step 5: Commit verification-complete updates**

```bash
git -C /mnt/c/dev/helengine add engine/helengine.editor docs/superpowers/plans/2026-04-29-docker-target-builders.md

git -C /mnt/c/dev/helengine commit -m "Wire Docker builder execution for windows-directx"
```

## Notes for the implementing agent

- Keep XML comments substantive on every new C# class, field, constructor, property, and method.
- Do not use tuples in new C# code.
- Keep one class per file in `helengine.editor`.
- Do not add fallback host-native build execution. If Docker or Windows-container compatibility is missing, fail directly.
- Keep the first implementation slice limited to `windows-directx`. Do not add speculative support for `linux`, `mac`, or retro targets in code yet.
- If `helengine-windows` lacks an automated PowerShell test harness, prefer deterministic smoke checks and explicit README verification steps over inventing a large new test subsystem.
