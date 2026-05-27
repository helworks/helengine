# Canonical Runtime Asset Paths Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enforce one strict lowercase packaged/runtime asset path contract across shared engine packaging, runtime consumers, and platform builders while keeping PS2 physical-path mapping isolated to the PS2 media boundary.

**Architecture:** Introduce one shared canonical packaged-path utility, then make every shared producer and consumer use it as the single contract. Shared systems will emit and validate lowercase logical paths only, while PS2 will continue mapping those logical paths to uppercase disc-native physical paths at its own export/runtime edge.

**Tech Stack:** C#/.NET 9, existing shared packaging pipeline, xUnit, sibling platform repos `helengine-3ds` and `helengine-ps2`, editor build graph, generated core/runtime manifests.

---

## File Structure

### Shared engine files

- Modify: `C:\dev\helworks\helengine\engine\helengine.baseplatform\Paths\PlatformPackagedAssetPathResolver.cs`
  - Tighten runtime-path normalization into strict canonical validation.
- Create: `C:\dev\helworks\helengine\engine\helengine.baseplatform\Paths\CanonicalPackagedAssetPath.cs`
  - Single shared logical-path normalizer/validator for packaged/runtime paths.
- Modify: `C:\dev\helworks\helengine\engine\helengine.files\assets\RuntimeAssetIdGenerator.cs`
  - Reuse the shared canonical path utility when normalizing path-like runtime asset keys.
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\content\RuntimeSceneCatalogEntry.cs`
  - Validate emitted cooked scene paths against the canonical contract.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\SceneComponentPackagingTransformService.cs`
  - Canonicalize and validate every packaged runtime asset reference emitted from scene packaging.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorWindowsBuildScenePackager.cs`
  - Canonicalize and validate packaged asset outputs such as default font and cooked source fonts.

### Shared engine tests

- Modify: `C:\dev\helworks\helengine\engine\helengine.baseplatform.tests\Paths\PlatformPackagedAssetPathResolverTests.cs`
  - Add strict failure coverage for mixed-case and rooted logical paths.
- Create: `C:\dev\helworks\helengine\engine\helengine.baseplatform.tests\Paths\CanonicalPackagedAssetPathTests.cs`
  - Unit-test the new canonical utility directly.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\utils\RuntimeAssetIdGeneratorTests.cs`
  - Update assertions to lowercase canonical examples and verify slash/case collapse explicitly.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorWindowsBuildScenePackagerTests.cs`
  - Flip mixed-case cooked font expectations to lowercase and add failing-path validation tests.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\RuntimeSceneCatalogTests.cs`
  - Add one strict failure case for non-canonical cooked scene paths.

### Platform repo files

- Modify: `C:\dev\helworks\helengine-3ds\builder\Nintendo3DsPlatformAssetBuilder.cs`
  - Validate builder work-item output relative paths against the canonical contract before staging to package-source and RomFS.
- Modify: `C:\dev\helworks\helengine-3ds\builder.tests\Nintendo3DsPlatformAssetBuilderTests.cs`
  - Add one regression proving `cooked/fonts/default.hefont` is emitted and mixed-case outputs fail.
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlatformAssetBuilderTests.cs`
  - Keep PS2 logical-path expectations lowercase while preserving uppercase disc-path mapping assertions.

## Task 1: Add The Shared Canonical Packaged Path Utility

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.baseplatform\Paths\CanonicalPackagedAssetPath.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.baseplatform.tests\Paths\CanonicalPackagedAssetPathTests.cs`

- [ ] **Step 1: Write the failing canonical-path tests**

```csharp
namespace helengine.baseplatform.tests.Paths;

public sealed class CanonicalPackagedAssetPathTests {
    [Fact]
    public void Normalize_WhenPathUsesBackslashesAndUppercase_ReturnsLowercaseForwardSlashPath() {
        string normalized = CanonicalPackagedAssetPath.Normalize("cooked\\Fonts\\DemoDiscBody.hefont");

        Assert.Equal("cooked/fonts/demodiscbody.hefont", normalized);
    }

    [Fact]
    public void ValidateCanonical_WhenPathContainsUppercase_ThrowsInvalidOperationException() {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => CanonicalPackagedAssetPath.ValidateCanonical("cooked/Fonts/DemoDiscBody.hefont"));

        Assert.Contains("cooked/Fonts/DemoDiscBody.hefont", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateCanonical_WhenPathIsRooted_ThrowsInvalidOperationException() {
        Assert.Throws<InvalidOperationException>(
            () => CanonicalPackagedAssetPath.ValidateCanonical("/cooked/fonts/default.hefont"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```powershell
dotnet test 'C:\dev\helworks\helengine\engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj' --filter FullyQualifiedName~CanonicalPackagedAssetPathTests -v q
```

Expected: FAIL because `CanonicalPackagedAssetPath` does not exist yet.

- [ ] **Step 3: Add the shared canonical utility**

```csharp
namespace helengine.baseplatform.Paths;

/// <summary>
/// Normalizes and validates canonical packaged/runtime logical asset paths.
/// </summary>
public static class CanonicalPackagedAssetPath {
    /// <summary>
    /// Normalizes one packaged/runtime logical path into canonical lowercase forward-slash form.
    /// </summary>
    /// <param name="path">Logical packaged/runtime path to normalize.</param>
    /// <returns>Canonical lowercase forward-slash path.</returns>
    public static string Normalize(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Packaged asset path must be provided.", nameof(path));
        }

        string normalized = path.Replace('\\', '/').Trim();
        if (Path.IsPathRooted(normalized) || normalized.StartsWith("/", StringComparison.Ordinal)) {
            throw new InvalidOperationException($"Packaged asset path '{path}' must be relative.");
        }

        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int index = 0; index < segments.Length; index++) {
            if (segments[index] == "." || segments[index] == "..") {
                throw new InvalidOperationException($"Packaged asset path '{path}' must not contain traversal segments.");
            }
        }

        return normalized.ToLowerInvariant();
    }

    /// <summary>
    /// Validates that one packaged/runtime logical path is already canonical.
    /// </summary>
    /// <param name="path">Logical packaged/runtime path to validate.</param>
    /// <returns>Original path when canonical.</returns>
    public static string ValidateCanonical(string path) {
        string normalized = Normalize(path);
        if (!string.Equals(normalized, path, StringComparison.Ordinal)) {
            throw new InvalidOperationException($"Packaged asset path '{path}' is not canonical. Expected '{normalized}'.");
        }

        return normalized;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run:

```powershell
dotnet test 'C:\dev\helworks\helengine\engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj' --filter FullyQualifiedName~CanonicalPackagedAssetPathTests -v q
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C C:\dev\helworks\helengine add engine/helengine.baseplatform/Paths/CanonicalPackagedAssetPath.cs engine/helengine.baseplatform.tests/Paths/CanonicalPackagedAssetPathTests.cs
git -C C:\dev\helworks\helengine commit -m "Add canonical packaged asset path utility"
```

## Task 2: Enforce Canonical Paths In Shared Runtime Producers And Consumers

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.baseplatform\Paths\PlatformPackagedAssetPathResolver.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.files\assets\RuntimeAssetIdGenerator.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\content\RuntimeSceneCatalogEntry.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.baseplatform.tests\Paths\PlatformPackagedAssetPathResolverTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\utils\RuntimeAssetIdGeneratorTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\RuntimeSceneCatalogTests.cs`

- [ ] **Step 1: Add the failing shared-runtime tests**

```csharp
[Fact]
public void ResolveRuntimeReferencePath_WhenContentRelativePathUsesUppercase_ThrowsInvalidOperationException() {
    RuntimeGenerationContract contract = new RuntimeGenerationContract(
        RuntimeMaterialResolutionMode.RawShaderBacked,
        true,
        PackagedPathPolicy.ContentRelativeOnly);

    Assert.Throws<InvalidOperationException>(
        () => PlatformPackagedAssetPathResolver.ResolveRuntimeReferencePath("windows", contract, "cooked/Fonts/default.hefont"));
}

[Fact]
public void RuntimeSceneCatalogEntry_WhenCookedRelativePathUsesUppercase_ThrowsInvalidOperationException() {
    Assert.Throws<InvalidOperationException>(
        () => new RuntimeSceneCatalogEntry("cube_test", "cooked/Scenes/cube_test.hasset"));
}

[Fact]
public void Generate_WhenCanonicalKeyCaseDiffers_ReturnsSameId() {
    ulong lower = RuntimeAssetIdGenerator.Generate("cooked/fonts/demodiscbody.hefont#atlas");
    ulong upper = RuntimeAssetIdGenerator.Generate("cooked/Fonts/DemoDiscBody.hefont#atlas");

    Assert.Equal(lower, upper);
}
```

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run:

```powershell
dotnet test 'C:\dev\helworks\helengine\engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj' --filter FullyQualifiedName~PlatformPackagedAssetPathResolverTests -v q
dotnet test 'C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter "FullyQualifiedName~RuntimeAssetIdGeneratorTests|FullyQualifiedName~RuntimeSceneCatalogTests" -v q
```

Expected: FAIL because mixed-case content-relative paths are still accepted.

- [ ] **Step 3: Update shared runtime normalization to use the canonical utility**

```csharp
// PlatformPackagedAssetPathResolver.cs
string canonicalPackagedAssetPath = CanonicalPackagedAssetPath.ValidateCanonical(packagedAssetPath);
if (runtimeGenerationContract.PackagedPathPolicy == PackagedPathPolicy.ContentRelativeOnly) {
    return canonicalPackagedAssetPath;
}
if (runtimeGenerationContract.PackagedPathPolicy == PackagedPathPolicy.RootedOrContentRelative) {
    if (string.Equals(platformId, "ps2", StringComparison.OrdinalIgnoreCase)) {
        return Ps2DiscPathResolver.ResolveRuntimePath(canonicalPackagedAssetPath);
    }
}

// RuntimeSceneCatalogEntry.cs
static string NormalizeCookedRelativePath(string cookedRelativePath) {
    return CanonicalPackagedAssetPath.ValidateCanonical(cookedRelativePath);
}

// RuntimeAssetIdGenerator.cs
static string NormalizeCanonicalKey(string canonicalKey) {
    if (canonicalKey == null) {
        throw new ArgumentNullException(nameof(canonicalKey));
    }

    return CanonicalPackagedAssetPath.Normalize(canonicalKey);
}
```

- [ ] **Step 4: Run the targeted tests to verify they pass**

Run:

```powershell
dotnet test 'C:\dev\helworks\helengine\engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj' --filter FullyQualifiedName~PlatformPackagedAssetPathResolverTests -v q
dotnet test 'C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter "FullyQualifiedName~RuntimeAssetIdGeneratorTests|FullyQualifiedName~RuntimeSceneCatalogTests" -v q
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C C:\dev\helworks\helengine add engine/helengine.baseplatform/Paths/PlatformPackagedAssetPathResolver.cs engine/helengine.files/assets/RuntimeAssetIdGenerator.cs engine/helengine.core/content/RuntimeSceneCatalogEntry.cs engine/helengine.baseplatform.tests/Paths/PlatformPackagedAssetPathResolverTests.cs engine/helengine.editor.tests/utils/RuntimeAssetIdGeneratorTests.cs engine/helengine.editor.tests/RuntimeSceneCatalogTests.cs
git -C C:\dev\helworks\helengine commit -m "Enforce canonical runtime paths in shared runtime helpers"
```

## Task 3: Make Shared Scene Packaging Emit Lowercase Canonical Paths Only

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\SceneComponentPackagingTransformService.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorWindowsBuildScenePackager.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Add failing packaging tests for source fonts and default font paths**

```csharp
[Fact]
public void Package_WhenSceneContainsSourceFontReference_RewritesPayloadToLowercaseCookedFontPath() {
    EditorWindowsBuildScenePackager packager = CreatePackager();

    EditorWindowsBuildScenePackagerResult result = packager.Package(new[] { "DemoScene" }, BuildRootPath);

    SceneAsset packagedScene = LoadPackagedScene("DemoScene");
    Assert.Contains(
        packagedScene.AssetReferences,
        reference => string.Equals(reference.RelativePath, "cooked/fonts/demodisctitle.hefont", StringComparison.Ordinal));
}

[Fact]
public void Package_WhenGeneratedFontOutputWouldUseMixedCase_ThrowsInvalidOperationException() {
    SceneComponentPackagingTransformService service = CreatePackagingTransformService();

    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
        () => service.PackageScene(CreateSceneThatResolvesMixedCaseCookedFontPath(), BuildRootPath));

    Assert.Contains("cooked/Fonts/", exception.Message, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the packaging tests to verify they fail**

Run:

```powershell
dotnet test 'C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter FullyQualifiedName~EditorWindowsBuildScenePackagerTests -v q
```

Expected: FAIL because current cooked source-font expectations still use `cooked/Fonts/...`.

- [ ] **Step 3: Canonicalize and validate packaged asset references at emission time**

```csharp
// SceneComponentPackagingTransformService.cs
const string EditorFontRelativePath = "cooked/fonts/default.hefont";

string ResolveRuntimeReferencePath(string relativePath) {
    string canonicalRelativePath = CanonicalPackagedAssetPath.Normalize(relativePath);
    string runtimePath = PlatformPackagedAssetPathResolver.ResolveRuntimeReferencePath(
        targetPlatform.PlatformId,
        targetPlatform.RuntimeGenerationContract,
        canonicalRelativePath);
    return runtimePath;
}

string BuildCookedFontRelativePath(string relativePath) {
    string changedExtensionPath = Path.ChangeExtension(NormalizeRelativePath(relativePath), ".hefont");
    string cookedPath = "cooked/" + changedExtensionPath;
    return CanonicalPackagedAssetPath.ValidateCanonical(cookedPath);
}

// EditorWindowsBuildScenePackager.cs
const string EditorFontRelativePath = "cooked/fonts/default.hefont";
WriteFontAsset(Path.Combine(buildRootPath, CanonicalPackagedAssetPath.ValidateCanonical(EditorFontRelativePath)), DefaultFontAsset);
```

- [ ] **Step 4: Run the packaging tests to verify they pass**

Run:

```powershell
dotnet test 'C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter FullyQualifiedName~EditorWindowsBuildScenePackagerTests -v q
```

Expected: PASS with lowercase cooked font and atlas expectations.

- [ ] **Step 5: Commit**

```bash
git -C C:\dev\helworks\helengine add engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
git -C C:\dev\helworks\helengine commit -m "Canonicalize packaged scene asset paths"
```

## Task 4: Enforce Canonical Paths In Platform Builders While Preserving PS2 Physical Mapping

**Files:**
- Modify: `C:\dev\helworks\helengine-3ds\builder\Nintendo3DsPlatformAssetBuilder.cs`
- Modify: `C:\dev\helworks\helengine-3ds\builder.tests\Nintendo3DsPlatformAssetBuilderTests.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlatformAssetBuilderTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.baseplatform\Paths\Ps2DiscPathResolver.cs`

- [ ] **Step 1: Add failing 3DS and PS2 builder tests**

```csharp
// Nintendo3DsPlatformAssetBuilderTests.cs
[Fact]
public void Build_WhenCookedFontOutputRelativePathUsesMixedCase_ThrowsInvalidOperationException() {
    Nintendo3DsPlatformAssetBuilder builder = CreateBuilder();

    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
        () => builder.Build(CreateRequestWithCookWorkItem("cooked/Fonts/default.hefont")));

    Assert.Contains("cooked/Fonts/default.hefont", exception.Message, StringComparison.Ordinal);
}

// Ps2PlatformAssetBuilderTests.cs
[Fact]
public void Ps2DiscPathResolver_WhenLogicalPathIsCanonical_ReturnsUppercasePhysicalPath() {
    string resolved = Ps2DiscPathResolver.ResolveRuntimePath("cooked/fonts/default.hefont");

    Assert.Equal("cdrom0:\\COOKED\\FONTS\\DEFAULT.HEF;1", resolved);
}
```

- [ ] **Step 2: Run the builder tests to verify they fail where expected**

Run:

```powershell
dotnet test 'C:\dev\helworks\helengine-3ds\builder.tests\helengine.3ds.builder.tests.csproj' --filter FullyQualifiedName~Nintendo3DsPlatformAssetBuilderTests -v q
dotnet test 'C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj' --filter FullyQualifiedName~Ps2PlatformAssetBuilderTests -v q
```

Expected: 3DS fails because mixed-case output still stages; PS2 remains green or needs expectation updates only.

- [ ] **Step 3: Validate canonical logical paths before platform-specific staging or mapping**

```csharp
// Nintendo3DsPlatformAssetBuilder.cs
static string NormalizeRelativePath(string path) {
    return CanonicalPackagedAssetPath.ValidateCanonical(path.Replace('\\', '/'));
}

// Ps2DiscPathResolver.cs
public static string ResolveRuntimePath(string logicalRelativePath) {
    string canonicalLogicalPath = CanonicalPackagedAssetPath.ValidateCanonical(logicalRelativePath);
    string discRelativePath = ResolveDiscRelativePath(canonicalLogicalPath).Replace('/', '\\');
    return RuntimeRootPrefix + discRelativePath + RuntimeVersionSuffix;
}
```

- [ ] **Step 4: Run the platform-builder tests to verify they pass**

Run:

```powershell
dotnet test 'C:\dev\helworks\helengine-3ds\builder.tests\helengine.3ds.builder.tests.csproj' --filter FullyQualifiedName~Nintendo3DsPlatformAssetBuilderTests -v q
dotnet test 'C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj' --filter FullyQualifiedName~Ps2PlatformAssetBuilderTests -v q
```

Expected: PASS with lowercase logical path enforcement and unchanged uppercase PS2 physical mapping.

- [ ] **Step 5: Commit**

```bash
git -C C:\dev\helworks\helengine add engine/helengine.baseplatform/Paths/Ps2DiscPathResolver.cs
git -C C:\dev\helworks\helengine-3ds add builder/Nintendo3DsPlatformAssetBuilder.cs builder.tests/Nintendo3DsPlatformAssetBuilderTests.cs
git -C C:\dev\helworks\helengine-ps2 add builder.tests/Ps2PlatformAssetBuilderTests.cs
git -C C:\dev\helworks\helengine commit -m "Require canonical logical paths before PS2 mapping"
git -C C:\dev\helworks\helengine-3ds commit -m "Validate canonical 3DS staged asset paths"
git -C C:\dev\helworks\helengine-ps2 commit -m "Keep PS2 runtime path mapping on canonical logical paths"
```

## Task 5: Verify The End-To-End 3DS And PS2 Behavior

**Files:**
- Modify: `C:\dev\helworks\helengine\docs\superpowers\specs\2026-05-27-canonical-runtime-asset-paths-design.md` only if implementation reveals a spec contradiction
- No planned source additions beyond verification unless new failing tests expose a missing enforcement seam

- [ ] **Step 1: Rebuild the city 3DS export with the DS scene set**

Run:

```powershell
dotnet run --project 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\helengine.editor.app.csproj' -- --project 'C:\dev\helprojs\city\project.heproj' --build 3ds --output 'C:\dev\helprojs\output\3ds'
```

Expected: build succeeds and staged RomFS contains `cooked/fonts/default.hefont`, not `cooked/Fonts/default.hefont`.

- [ ] **Step 2: Verify the staged package contains lowercase cooked paths only**

Run:

```powershell
Get-ChildItem 'C:\Users\beatriz\AppData\Local\Temp\helengine-platform-build\3ds' -Directory |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1 -ExpandProperty FullName |
  ForEach-Object {
    Get-ChildItem "$_\\builder\\3ds\\romfs\\cooked" -Recurse -File |
      Where-Object { $_.FullName -match 'Fonts|Materials|Scenes|Imported' } |
      Select-Object -ExpandProperty FullName
  }
```

Expected: no uppercase logical cooked directories in the staged logical package tree.

- [ ] **Step 3: Launch the 3DS package and open `cube_test_ds`**

Run:

```powershell
Get-Process azahar -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Process 'C:\dev\helworks\emus\azahar-windows-msvc-2125.1.1\azahar.exe' 'C:\dev\helprojs\output\3ds\helengine_3ds.3dsx'
```

Expected: `cube_test_ds` no longer dies with `phase=core-draw` and `romfs:/cooked/fonts/default.hefont` missing.

- [ ] **Step 4: Run the smallest PS2 logical-path verification**

Run:

```powershell
dotnet test 'C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj' --filter "FullyQualifiedName~Ps2PlatformAssetBuilderTests|FullyQualifiedName~PlatformPackagedAssetPathResolverTests" -v q
```

Expected: PASS with canonical lowercase logical paths still mapping to uppercase PS2 runtime paths.

- [ ] **Step 5: Commit verification-only follow-up only if code changed**

```bash
git -C C:\dev\helworks\helengine status --short
git -C C:\dev\helworks\helengine-3ds status --short
git -C C:\dev\helworks\helengine-ps2 status --short
```

Expected: no new source diffs beyond intentional implementation work. If verification required no extra code, do not create a verification-only commit.

## Self-Review

- Spec coverage:
  - canonical lowercase contract: Tasks 1-4
  - strict immediate failure policy: Tasks 1-4
  - shared emit/consume enforcement: Tasks 2-3
  - PS2 physical mapping boundary: Task 4
  - 3DS runtime regression proof: Task 5
- Placeholder scan:
  - no `TODO`, `TBD`, or “appropriate handling” placeholders remain
  - each task includes concrete files, commands, and example code
- Type consistency:
  - shared helper name is `CanonicalPackagedAssetPath`
  - validation entry point is `ValidateCanonical`
  - normalization entry point is `Normalize`
  - all later tasks reference those same names
