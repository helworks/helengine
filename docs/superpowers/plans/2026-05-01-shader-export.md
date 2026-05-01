# Referenced Shader Export Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Export only the shaders referenced by packaged scenes into the Windows build output, using the existing editor shader cache and the runtime `shaders/{shaderId}.{target}.shader.asset` layout.

**Architecture:** Keep shader export separate from scene packaging. `EditorWindowsBuildScenePackager` will discover which shader ids are referenced while rewriting the selected scenes, then a new `EditorShaderPackageExportService` will copy the already-compiled packages from the editor shader cache into the deployment root. `EditorWindowsBuildExecutor` remains the orchestrator: clean output, regenerate core, package scenes, export referenced shader packages, then build the Windows host.

**Tech Stack:** C#, xUnit, existing editor build pipeline, `ShaderModulePackageWriter`, `ShaderPackagePaths`, `EditorProjectPaths.ShaderCache`, existing Windows build executor and scene packager.

---

## File Map

### Shader export service and scene-packager result
- Create: `engine/helengine.editor/shaders/EditorShaderPackageExportService.cs`
- Create: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackagerResult.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Create: `engine/helengine.editor.tests/shaders/EditorShaderPackageExportServiceTests.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

### Windows build orchestration
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildExecutor.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildExecutorTests.cs`

### Regression coverage and validation
- Modify: `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs` if the new build flow touches queue integration
- Modify: `engine/helengine.editor.tests/EditorSessionBuildSettingsTests.cs` only if shader export needs the active platform or shader target wired through session state

## Task 1: Add the shader export service and referenced-shader collection result

**Files:**
- Create: `engine/helengine.editor/shaders/EditorShaderPackageExportService.cs`
- Create: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackagerResult.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Create: `engine/helengine.editor.tests/shaders/EditorShaderPackageExportServiceTests.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Write the failing export tests**

Add a direct export test that proves a referenced shader package is copied from the cache to the build root using the runtime naming convention:

```csharp
[Fact]
public void Export_WhenOneReferencedShaderExists_CopiesItIntoBuildShadersFolder() {
    string shaderCacheRootPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "shader-cache");
    string buildRootPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "Build");
    Directory.CreateDirectory(shaderCacheRootPath);
    Directory.CreateDirectory(buildRootPath);

    string shaderId = "EditorDefaultMesh";
    string cachedPackagePath = Path.Combine(shaderCacheRootPath, "EditorDefaultMesh.dx11.shader.asset");
    WriteCompiledShaderPackage(cachedPackagePath, shaderId, ShaderCompileTarget.DirectX11);

    EditorShaderPackageExportService exportService = new EditorShaderPackageExportService(shaderCacheRootPath);
    exportService.Export(new[] { shaderId }, ShaderCompileTarget.DirectX11, buildRootPath);

    Assert.True(File.Exists(Path.Combine(buildRootPath, "shaders", "EditorDefaultMesh.dx11.shader.asset")));
}
```

Add a packager test that proves shader ids are collected and deduplicated from the packaged scene graph:

```csharp
[Fact]
public void Package_WhenTwoMaterialsReferenceTheSameShader_ReportsOneReferencedShaderId() {
    EditorWindowsBuildScenePackager packager = new EditorWindowsBuildScenePackager(TempProjectRootPath);

    EditorWindowsBuildScenePackagerResult result = packager.Package(
        new[] { "scenes/test-same-shader.helen" },
        TempBuildRootPath);

    Assert.Equal(new[] { "EditorDefaultMesh" }, result.ReferencedShaderAssetIds);
}
```

These tests should fail until the packager returns a result object that carries referenced shader ids and the export service stages compiled shader packages into the build root.

- [ ] **Step 2: Run the focused export tests and confirm they fail for the expected missing behavior**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorShaderPackageExportServiceTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests" -p:UseCommonOutputDirectory=true -p:CopyLocalLockFileAssemblies=true -p:UseSharedCompilation=false -p:BuildInParallel=false -m:1 -nr:false -v minimal
```

Expected:
- the export service test fails because `EditorShaderPackageExportService` does not yet exist
- the packager test fails because `EditorWindowsBuildScenePackager` does not yet return referenced shader ids

- [ ] **Step 3: Implement the export service and packager result**

Add the new result type and service so the tests pass:

```csharp
public sealed class EditorWindowsBuildScenePackagerResult {
    public EditorWindowsBuildScenePackagerResult(IReadOnlyList<string> referencedShaderAssetIds) {
        if (referencedShaderAssetIds == null) {
            throw new ArgumentNullException(nameof(referencedShaderAssetIds));
        }

        ReferencedShaderAssetIds = referencedShaderAssetIds;
    }

    public IReadOnlyList<string> ReferencedShaderAssetIds { get; }
}
```

`EditorWindowsBuildScenePackager.Package(...)` should:
- keep rewriting and writing packaged scenes exactly as it does today
- collect every unique `MaterialAsset.ShaderAssetId` encountered while resolving file-backed material references
- include the generated standard material shader id when the packager emits the generated standard material assets
- return `EditorWindowsBuildScenePackagerResult` with a deduplicated ordered list of shader ids

`EditorShaderPackageExportService` should:
- accept the shader cache root path in its constructor
- accept the referenced shader id list, the active `ShaderCompileTarget`, and the build root path in its `Export(...)` method
- use `ShaderPackagePaths.GetPackagePath(...)` to resolve each cached shader package
- copy each resolved package into `buildRootPath/shaders`
- throw when a referenced cached shader package is missing or when the shader package target does not match the requested export target

- [ ] **Step 4: Re-run the focused export tests and verify they pass**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorShaderPackageExportServiceTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests" -p:UseCommonOutputDirectory=true -p:CopyLocalLockFileAssemblies=true -p:UseSharedCompilation=false -p:BuildInParallel=false -m:1 -nr:false -v minimal
```

Expected:
- both export-focused test classes pass

- [ ] **Step 5: Commit the shader export service slice**

```bash
git add engine/helengine.editor/shaders/EditorShaderPackageExportService.cs engine/helengine.editor/managers/project/EditorWindowsBuildScenePackagerResult.cs engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor.tests/shaders/EditorShaderPackageExportServiceTests.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
git commit -m "Add referenced shader export service"
```

## Task 2: Wire shader export into the Windows build executor

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildExecutor.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildExecutorTests.cs`

- [ ] **Step 1: Write the failing executor test**

Add an end-to-end build test that proves a referenced shader package reaches the final build root:

```csharp
[Fact]
public void Execute_WhenSceneReferencesAShader_CopiesTheShaderPackageIntoTheFinalBuildRoot() {
    string projectRootPath = CreateTempProjectRoot();
    string buildRootPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "output");
    WriteSceneThatReferencesMaterial(projectRootPath, "scenes/test.helen", "engine:material:test");
    WriteMaterialAsset(projectRootPath, "assets/materials/test.helmat", "EditorDefaultMesh");
    WriteCompiledShaderPackage(projectRootPath, "shader-cache", "EditorDefaultMesh", ShaderCompileTarget.DirectX11);

    EditorWindowsBuildExecutor executor = new EditorWindowsBuildExecutor(projectRootPath, "1.0.0-test");
    EditorBuildExecutionResult result = executor.Execute(CreateQueueItem(buildRootPath, "scenes/test.helen"));

    Assert.True(result.Success);
    Assert.True(File.Exists(Path.Combine(buildRootPath, "Build", "shaders", "EditorDefaultMesh.dx11.shader.asset")));
}
```

This test should fail until the executor invokes the new shader export service after the scene packager finishes.

- [ ] **Step 2: Run the executor test and confirm it fails for the expected missing pipeline step**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorWindowsBuildExecutorTests" -p:UseCommonOutputDirectory=true -p:CopyLocalLockFileAssemblies=true -p:UseSharedCompilation=false -p:BuildInParallel=false -m:1 -nr:false -v minimal
```

Expected:
- the build succeeds far enough to reach the executor test
- the test fails because shader export is not yet part of the Windows build flow

- [ ] **Step 3: Implement executor integration**

Update `EditorWindowsBuildExecutor.Execute(...)` to:

```csharp
EditorWindowsBuildScenePackagerResult packageResult = packager.Package(queueItem.SelectedSceneIds, buildPaths.BuildRootPath);
EditorShaderPackageExportService shaderExportService = new EditorShaderPackageExportService(Path.Combine(ProjectRootPath, "shader-cache"));
shaderExportService.Export(packageResult.ReferencedShaderAssetIds, ShaderCompileTarget.DirectX11, buildPaths.BuildRootPath);
```

The executor should keep the current build order intact:
- clean the deployment root
- regenerate generated core
- package scenes
- export referenced shader packages
- build the native Windows host
- copy the native artifacts into the final build root

The executor should continue to fail fast on missing shader cache entries instead of silently shipping a broken build.

- [ ] **Step 4: Re-run the executor test and verify it passes**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorWindowsBuildExecutorTests" -p:UseCommonOutputDirectory=true -p:CopyLocalLockFileAssemblies=true -p:UseSharedCompilation=false -p:BuildInParallel=false -m:1 -nr:false -v minimal
```

Expected:
- the executor test passes and the final build root contains the referenced shader package under `Build/shaders`

- [ ] **Step 5: Commit the Windows executor slice**

```bash
git add engine/helengine.editor/managers/project/EditorWindowsBuildExecutor.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildExecutorTests.cs
git commit -m "Wire referenced shader export into Windows builds"
```

## Task 3: Run the broader Windows build validation

**Files:**
- No new files expected
- May touch: `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs`
- May touch: `engine/helengine.editor.tests/EditorSessionBuildSettingsTests.cs`

- [ ] **Step 1: Run the focused regression set**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorShaderPackageExportServiceTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests|FullyQualifiedName~EditorWindowsBuildExecutorTests|FullyQualifiedName~BuildSettingsDialogTests|FullyQualifiedName~EditorSessionBuildSettingsTests" -p:UseCommonOutputDirectory=true -p:CopyLocalLockFileAssemblies=true -p:UseSharedCompilation=false -p:BuildInParallel=false -m:1 -nr:false -v minimal
```

Expected:
- the shader export tests pass
- the Windows executor test passes
- the preexisting build-settings and session tests remain green

- [ ] **Step 2: Rebuild the full editor test project if the focused set passes**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj -p:UseCommonOutputDirectory=true -p:CopyLocalLockFileAssemblies=true -p:UseSharedCompilation=false -p:BuildInParallel=false -m:1 -nr:false -v minimal
```

Expected:
- the editor test assembly builds cleanly
- any failure should be a real regression in shader export, not a missing test scaffold

- [ ] **Step 3: Commit the validated implementation**

```bash
git add engine/helengine.editor/managers/project/EditorWindowsBuildExecutor.cs engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor/managers/project/EditorWindowsBuildScenePackagerResult.cs engine/helengine.editor/shaders/EditorShaderPackageExportService.cs engine/helengine.editor.tests
git commit -m "Export referenced shaders in Windows builds"
```
