# Platform Material and Shader Artifact Contract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve platform-cooked material files and platform-staged shader files as separate manifest artifacts without classifying either by path or payload.

**Architecture:** Material cooking returns material bytes and shader dependency IDs. The editor resolves each ID through the generated shader `.hasset` and cache metadata, declares a material artifact at its write boundary, invokes an optional platform shader-artifact builder with resolved source entries, and declares its one versioned Vita shader bundle. The bundle indexes shader asset IDs, source hashes, program metadata, and compiled Vita stages. Manifest collection consumes declarations before scanning undeclared assets.

**Tech Stack:** C#/.NET, `helengine.baseplatform`, editor cook pipeline, platform builders, xUnit.

## Global Constraints

- Materials and shaders are separate files and separate manifest artifacts.
- Shader association is platform-specific and optional; PSP/PS2 can emit materials with no shader artifacts.
- Never infer a material or shader kind from its directory or bytes.
- Platform-owned serializer bytes remain platform-owned.
- Preserve unrelated shared-worktree changes and never modify generated code.

---

## File Structure

- Create `engine/helengine.baseplatform/Manifest/PlatformCookedArtifactDeclaration.cs`: immutable material/shader output identity.
- Create `engine/helengine.baseplatform/Requests/PlatformShaderArtifactCookRequest.cs`: resolved shader source entries and cook-root context.
- Create `engine/helengine.baseplatform/Results/PlatformShaderArtifactCookResult.cs`: declarations emitted by a shader bundle staging run.
- Create `engine/helengine.baseplatform/Builders/IPlatformShaderArtifactBuilder.cs`: optional shader staging capability.
- Modify `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`: record material outputs at every write.
- Modify `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`: report transformed-component material writes.
- Modify `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackagerResult.cs`: expose material declarations and shader dependency IDs separately.
- Modify `engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs`: invoke shader staging and consume declarations.
- Modify `engine/helengine.editor/managers/project/EditorPlatformCookedArtifactPool.cs`: add declared files with their declared identity.
- Modify `engine/helengine.editor/shaders/EditorShaderPackageExportService.cs`: return declarations for copied generic shader packages.
- Modify `C:/dev/helworks/helengine-psvita/builder/PsVitaPlatformAssetBuilder.cs`: implement Vita shader artifact staging through the device compiler exchange.

### Task 1: Add the explicit artifact contracts

**Files:**

- Create `engine/helengine.baseplatform/Manifest/PlatformCookedArtifactDeclaration.cs`.
- Create `engine/helengine.baseplatform/Requests/PlatformShaderArtifactCookRequest.cs`.
- Create `engine/helengine.baseplatform/Results/PlatformShaderArtifactCookResult.cs`.
- Create `engine/helengine.baseplatform/Builders/IPlatformShaderArtifactBuilder.cs`.
- Create `engine/helengine.baseplatform.tests/Manifest/PlatformCookedArtifactDeclarationTests.cs`.

**Consumes:** Existing `PlatformMaterialCookResult` and `PlatformBuildArtifact`.

**Produces:** An optional `IPlatformShaderArtifactBuilder.CookShaderArtifacts(PlatformShaderArtifactCookRequest)` capability.

- [ ] **Step 1: Write a failing constructor test**

```csharp
[Fact]
public void Constructor_whenMaterialDeclarationIsValid_preservesIdentity() {
    PlatformCookedArtifactDeclaration declaration = new(
        "cooked/materials/standard.hasset", "engine:material:standard", "material", "shared");

    Assert.Equal("material", declaration.ArtifactKind);
    Assert.Equal("engine:material:standard", declaration.LogicalArtifactId);
}
```

- [ ] **Step 2: Verify the test is red**

Run: `rtk dotnet test engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlatformCookedArtifactDeclarationTests" --no-restore`

Expected: unresolved declaration type.

- [ ] **Step 3: Implement minimal immutable contracts**

```csharp
public sealed class PlatformCookedArtifactDeclaration {
    public PlatformCookedArtifactDeclaration(string relativePath, string logicalArtifactId, string artifactKind, string variantId) {
        if (string.IsNullOrWhiteSpace(relativePath)) {
            throw new ArgumentException("Artifact relative path is required.", nameof(relativePath));
        } else if (string.IsNullOrWhiteSpace(logicalArtifactId)) {
            throw new ArgumentException("Artifact logical id is required.", nameof(logicalArtifactId));
        } else if (!string.Equals(artifactKind, "material", StringComparison.Ordinal) && !string.Equals(artifactKind, "shader", StringComparison.Ordinal)) {
            throw new ArgumentException("Artifact kind must be either 'material' or 'shader'.", nameof(artifactKind));
        } else if (string.IsNullOrWhiteSpace(variantId)) {
            throw new ArgumentException("Artifact variant id is required.", nameof(variantId));
        }

        RelativePath = relativePath.Replace('\\', '/');
        LogicalArtifactId = logicalArtifactId;
        ArtifactKind = artifactKind;
        VariantId = variantId;
    }

    public string RelativePath { get; }
    public string LogicalArtifactId { get; }
    public string ArtifactKind { get; }
    public string VariantId { get; }
}
```

The request validates a cook root and non-null shader-ID list. The result copies non-null declarations. The optional interface has exactly this member:

```csharp
PlatformShaderArtifactCookResult CookShaderArtifacts(PlatformShaderArtifactCookRequest request);
```

- [ ] **Step 4: Verify the contract tests are green**

Run: `rtk dotnet test engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlatformCookedArtifactDeclarationTests" --no-restore`

Expected: PASS.

- [ ] **Step 5: Commit only Task 1 files**

```powershell
git add engine/helengine.baseplatform/Manifest/PlatformCookedArtifactDeclaration.cs engine/helengine.baseplatform/Requests/PlatformShaderArtifactCookRequest.cs engine/helengine.baseplatform/Results/PlatformShaderArtifactCookResult.cs engine/helengine.baseplatform/Builders/IPlatformShaderArtifactBuilder.cs engine/helengine.baseplatform.tests/Manifest/PlatformCookedArtifactDeclarationTests.cs
git commit -m "feat: declare cooked material and shader artifacts explicitly"
```

### Task 2: Preserve material outputs at their write boundaries

**Files:**

- Modify `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`.
- Modify `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`.
- Modify `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackagerResult.cs`.
- Test `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`.
- Test `engine/helengine.editor.tests/managers/project/SceneComponentPackagingTransformServiceTests.cs`.

**Consumes:** Task 1 declaration type.

**Produces:** `CookedArtifactDeclarations` containing every material file plus the already-written Windows generated-standard shader file; `ReferencedShaderAssetIds` remains a dependency list, not a list of files.

- [ ] **Step 1: Write failing separate-identity tests**

```csharp
[Fact]
public void Package_whenMaterialReferencesShader_reportsMaterialAndDependencySeparately() {
    EditorPlatformBuildScenePackagerResult result = PackageSceneWithOneMaterial("ForwardStandardShader");

    PlatformCookedArtifactDeclaration material = Assert.Single(result.CookedArtifactDeclarations);
    Assert.Equal("material", material.ArtifactKind);
    Assert.Equal(new[] { "ForwardStandardShader" }, result.ReferencedShaderAssetIds);
}
```

Add an equivalent component-transform test, because `SceneComponentPackagingTransformService` has its own material-write path.

- [ ] **Step 2: Verify these tests are red**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~Package_whenMaterialReferencesShader_reportsMaterialAndDependencySeparately|FullyQualifiedName~SceneComponentPackagingTransformServiceTests" --no-restore`

Expected: absent or empty `CookedArtifactDeclarations`.

- [ ] **Step 3: Record every material write explicitly**

Give the packager a deduplicated declaration list and normalized-path lookup. Pass an `Action<PlatformCookedArtifactDeclaration>` sink to the transform service. Immediately after every material `WriteAsset` or `WriteBytes`, add:

```csharp
RememberCookedArtifact(new PlatformCookedArtifactDeclaration(
    NormalizeRuntimeReferencePath(cookedRelativePath),
    materialAssetId,
    "material",
    "shared"));
```

The helper ignores an identical duplicate and throws for a duplicate path with different kind, logical ID, or variant. When the existing Windows default path writes `StandardGeneratedShaderRelativePath`, record it as a `shader` declaration using the shader asset ID. Copy the list into `EditorPlatformBuildScenePackagerResult` and clear it at the start of each packaging run.

- [ ] **Step 4: Verify focused packager tests are green**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~Package_whenMaterialReferencesShader_reportsMaterialAndDependencySeparately|FullyQualifiedName~SceneComponentPackagingTransformServiceTests" --no-restore`

Expected: PASS.

- [ ] **Step 5: Commit only Task 2 files**

```powershell
git add engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor/managers/project/EditorWindowsBuildScenePackagerResult.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs engine/helengine.editor.tests/managers/project/SceneComponentPackagingTransformServiceTests.cs
git commit -m "feat: preserve cooked material output declarations"
```

### Task 3: Stage shader files through an optional platform capability

**Files:**

- Modify `engine/helengine.editor/shaders/EditorShaderPackageExportService.cs`.
- Modify `engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs`.
- Modify `C:/dev/helworks/helengine-psvita/builder/PsVitaPlatformAssetBuilder.cs`.
- Test `engine/helengine.editor.tests/shaders/EditorShaderPackageExportServiceTests.cs`.
- Test `C:/dev/helworks/helengine-psvita/builder.tests/PsVitaPlatformAssetBuilderTests.cs`.

**Consumes:** Task 1 optional capability, Task 2 shader dependency IDs, and generated shader `.hasset` cache metadata.

**Produces:** One `shader` declaration for the versioned Vita shader bundle, and no shader declaration for a shaderless platform.

- [ ] **Step 1: Write failing generic shader-export and Vita staging tests**

```csharp
[Fact]
public void Export_whenOneReferencedShaderExists_returnsShaderDeclaration() {
    PlatformCookedArtifactDeclaration declaration = Assert.Single(
        exportService.Export(new[] { "ForwardStandardShader" }, ShaderCompileTarget.DirectX11, BuildRootPath));

    Assert.Equal("shader", declaration.ArtifactKind);
    Assert.Equal("ForwardStandardShader", declaration.LogicalArtifactId);
}
```

Add a Vita test that batches one requested shader through the existing device compiler exchange and asserts both bundle existence and a `shader` declaration. Add a user-shader test that verifies the bundle entry preserves shader asset ID and source hash. Keep the existing empty-dependency material test.

- [ ] **Step 2: Verify focused staging tests are red**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorShaderPackageExportServiceTests" --no-restore`

Run: `rtk dotnet test C:/dev/helworks/helengine-psvita/builder.tests/helengine.psvita.builder.tests.csproj --filter "FullyQualifiedName~PsVitaPlatformAssetBuilderTests" --no-restore`

Expected: export returns `void`; Vita builder lacks optional staging capability.

- [ ] **Step 3: Implement file staging without changing material bytes**

Add an editor shader-source resolver that receives dependency IDs, opens generated shader `.hasset` files, uses their cache metadata to recover authored source paths, and returns explicit source entries. Make `EditorShaderPackageExportService.Export` return declarations for files it copies under `cooked/shaders/`. Implement `IPlatformShaderArtifactBuilder` on `PsVitaPlatformAssetBuilder`: empty entries produce an empty result; resolved entries are compiled together through the existing device exchange into one versioned Vita shader bundle written beneath the request cook root; return one `shader` declaration for the bundle; unresolved IDs throw. The bundle index must map every requested shader ID to source hash, program/variant metadata, and compiled VP/FP bytes.

In `EditorPlatformAssetCookService.Cook`, append declarations from the optional capability after the packager returns and before manifest artifact collection:

```csharp
if (effectiveMaterialBuilder is IPlatformShaderArtifactBuilder shaderArtifactBuilder) {
    PlatformShaderArtifactCookResult shaderResult = shaderArtifactBuilder.CookShaderArtifacts(
        new PlatformShaderArtifactCookRequest(
            effectiveCookRootPath,
            ResolvePlatformName(platformDefinition, materialBuilder),
            selectedBuildProfileId,
            selectedGraphicsProfileId,
            packagerResult.ReferencedShaderAssetIds));
    declaredArtifacts.AddRange(shaderResult.CookedArtifactDeclarations);
}
```

When `effectiveMaterialBuilder` is null, use the existing Windows default `ShaderCompileTarget.DirectX11` with `EditorShaderPackageExportService` and append its returned declarations. When a material builder exists but does not implement `IPlatformShaderArtifactBuilder`, stage no shaders. Do not add shader behavior to PSP or PS2.

- [ ] **Step 4: Verify focused staging tests are green**

Run the two commands from Step 2.

Expected: PASS.

- [ ] **Step 5: Commit Task 3 changes by repository**

```powershell
git add engine/helengine.editor/shaders/EditorShaderPackageExportService.cs engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs engine/helengine.editor.tests/shaders/EditorShaderPackageExportServiceTests.cs
git commit -m "feat: stage shaders as explicit build artifacts"
```

```powershell
git add builder/PsVitaPlatformAssetBuilder.cs builder.tests/PsVitaPlatformAssetBuilderTests.cs
git commit -m "feat: declare staged Vita shader artifacts"
```

### Task 4: Build manifests from declarations, never material/shader guesses

**Files:**

- Modify `engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs`.
- Modify `engine/helengine.editor/managers/project/EditorPlatformCookedArtifactPool.cs`.
- Test `engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs`.

**Consumes:** Tasks 2–3 declarations.

**Produces:** Manifest artifacts whose kind, logical ID, and variant are declared at production time.

- [ ] **Step 1: Write failing contract regression tests**

```csharp
[Fact]
public void Cook_whenDeclaredPlatformMaterialHeaderCollidesWithShaderFormat_listsMaterialWithoutDeserializingPayload() {
    PlatformBuildManifest manifest = CookWithDeclarations(
        new PlatformCookedArtifactDeclaration("cooked/engine/materials/standard.hasset", "engine:material:standard", "material", "shared"));

    Assert.Contains(manifest.CookedArtifacts, artifact => artifact.ArtifactKind == "material");
}

[Fact]
public void Cook_whenDeclaredMaterialHasNoShaderOutput_succeeds() {
    PlatformBuildManifest manifest = CookWithDeclarations(
        new PlatformCookedArtifactDeclaration("cooked/materials/vu.hasset", "material:vu", "material", "shared"));

    Assert.DoesNotContain(manifest.CookedArtifacts, artifact => artifact.ArtifactKind == "shader");
}
```

The first fixture must use the reproduced PS2 `HELE` header collision and must fail before the fix with generic shader-material deserialization.

- [ ] **Step 2: Verify the test is red**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformAssetCookServiceTests" --no-restore`

Expected: generic deserialization error such as `String length cannot be negative`.

- [ ] **Step 3: Add declared files before directory scanning**

Extend `BuildCookedArtifacts` with declaration input. Validate every declaration path is under the cook root and exists. Add a pool method that preserves declaration identity:

```csharp
public void AddDeclaredFile(string fullPath, PlatformCookedArtifactDeclaration declaration) {
    string contentHash = string.Concat("sha256:", FileHasher.ComputeHash(fullPath));
    Artifacts.Add(new PlatformBuildArtifact(
        declaration.RelativePath,
        declaration.LogicalArtifactId,
        contentHash,
        declaration.ArtifactKind,
        declaration.VariantId));
}
```

Build a normalized declared-path set and skip it in the later scan. Remove the added `ShaderMaterialAssetBinarySerializer.FormatId` generic-classification branch. Remove the test that relies on a `materials/` directory fallback. Retain generic model/audio classification only for undeclared assets.

- [ ] **Step 4: Verify cook and build graph regressions**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformAssetCookServiceTests|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests" --no-restore`

Expected: PASS for all new declaration tests; report unrelated existing failures without modifying them.

- [ ] **Step 5: Reproduce PS2 cooking through the editor CLI**

Run: `rtk dotnet C:/dev/helworks/helengine/.codex-temp/editor-ps2-b15/helengine.editor.app.dll --project C:/dev/helprojs/demodisc --build ps2 --build-profile ps2-default --output C:/dev/helworks/helengine-psvita/diagnostics/ps2-material-artifact-contract`

Expected: `standard.hasset` is a manifest `material` without `ShaderMaterialAssetBinarySerializer` invocation. Record later native compilation failures separately.

- [ ] **Step 6: Commit only Task 4 files**

```powershell
git add engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs engine/helengine.editor/managers/project/EditorPlatformCookedArtifactPool.cs engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs
git commit -m "fix: preserve material and shader artifact separation in manifests"
```

### Task 5: Verify shader-capable and shaderless platform boundaries

**Files:**

- Modify `engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs`.
- Modify `C:/dev/helworks/helengine-psvita/builder.tests/PsVitaPlatformAssetBuilderTests.cs`.

**Consumes:** Tasks 1–4.

**Produces:** Regression coverage that absence of shader staging means no shader artifacts, not a fallback shader or material reinterpretation.

- [ ] **Step 1: Write a failing shaderless-platform test**

```csharp
[Fact]
public void Cook_whenBuilderDoesNotImplementShaderArtifactBuilder_emitsDeclaredMaterialsWithoutShaders() {
    PlatformBuildManifest manifest = CookWithMaterialBuilder(new Ps2StyleMaterialBuilder());

    Assert.Contains(manifest.CookedArtifacts, artifact => artifact.ArtifactKind == "material");
    Assert.DoesNotContain(manifest.CookedArtifacts, artifact => artifact.ArtifactKind == "shader");
}
```

- [ ] **Step 2: Verify it is red before completing the optional-capability branch**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~Cook_whenBuilderDoesNotImplementShaderArtifactBuilder_emitsDeclaredMaterialsWithoutShaders" --no-restore`

Expected: failure caused by scanner fallback or shader-stage assumptions.

- [ ] **Step 3: Complete only the branch required by the failing test**

Absence of `IPlatformShaderArtifactBuilder` must mean “no shader artifacts for this platform.” It must not synthesize shaders, change material bytes, or inspect material payloads.

- [ ] **Step 4: Run final targeted verification**

```powershell
rtk dotnet test engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlatformCookedArtifactDeclarationTests" --no-restore
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformAssetCookServiceTests|FullyQualifiedName~EditorShaderPackageExportServiceTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests" --no-restore
rtk dotnet test C:/dev/helworks/helengine-psvita/builder.tests/helengine.psvita.builder.tests.csproj --filter "FullyQualifiedName~PsVitaPlatformAssetBuilderTests" --no-restore
```

Expected: all new boundary tests pass; unrelated failures are reported separately.

- [ ] **Step 5: Commit only the final boundary test and its required change**

```powershell
git add engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs
git commit -m "test: cover shaderless platform material artifacts"
```
