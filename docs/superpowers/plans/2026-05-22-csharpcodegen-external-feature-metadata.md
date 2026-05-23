# CSharpCodegen External Feature Metadata Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove all built-in `helengine` feature knowledge from `csharpcodegen` and make `helengine` publish external feature metadata that drives codegen feature detection, reports, config defines, and runtime requirement pruning.

**Architecture:** The migration should first add a generic external metadata path to `csharpcodegen`, switch `helengine` to use it, and then delete the built-in feature catalog and feature enum compatibility path. `csharpcodegen` becomes a generic converter with free-form string feature ids, while `helengine` becomes just one metadata publisher and caller.

**Tech Stack:** C#, .NET, Roslyn, `csharpcodegen`, `helengine.editor`, xUnit, JSON metadata, `rtk` command wrapper

---

## File Structure

### CSharpCodegen production files to create or modify

- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPExternalFeatureDefinition.cs`
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPExternalFeatureRootRule.cs`
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPExternalRuntimeRequirementOwnership.cs`
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPExternalFeatureCatalog.cs`
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPExternalFeatureCatalogLoader.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPFeatureScanner.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPFeatureResolver.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPGeneratedConfigWriter.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPConversionReportWriter.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPRuntimeRequirementCatalog.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPRestrictionValidator.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPFeatureManifestWriter.cs`
- Delete: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPFeatureCatalog.cs`
- Delete or heavily rewrite: `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPFeatureKind.cs`

### CSharpCodegen test files to create or modify

- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPExternalFeatureCatalogLoaderTests.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPFeatureScannerTests.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPGeneratedConfigWriterTests.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPFeatureManifestWriterTests.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPFeatureOwnedRuntimeRequirementTests.cs`

### Helengine production files to create or modify

- Create: `engine/helengine.editor/codegen/features/helengine-feature-catalog.json`
- Modify: `engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`

### Helengine test files to create or modify

- Create: `engine/helengine.editor.tests/managers/project/HelengineFeatureCatalogIntegrityTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`

### Validation targets

- `C:\dev\helworks\csharpcodegen\codegen\codegen.csproj`
- `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj` when the local SDK supports it
- `engine/helengine.editor.tests/helengine.editor.tests.csproj`
- city Windows export to `C:\dev\helprojs\output\windows-city-demo-disc`

---

### Task 1: Add Generic External Feature Catalog Models And Loader

**Files:**
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPExternalFeatureDefinition.cs`
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPExternalFeatureRootRule.cs`
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPExternalRuntimeRequirementOwnership.cs`
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPExternalFeatureCatalog.cs`
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPExternalFeatureCatalogLoader.cs`
- Test: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPExternalFeatureCatalogLoaderTests.cs`

- [ ] **Step 1: Write the failing loader tests**

Create tests that prove the loader accepts valid free-form metadata and rejects invalid references.

```csharp
[Fact]
public void Load_WhenCatalogUsesFreeFormFeatureIds_ParsesDefinitionsAndRootRules() {
    string json = """
{
  "features": [
    { "id": "shaders", "defaultMode": "auto", "conflictPolicy": "error" },
    { "id": "render2d", "defaultMode": "auto", "conflictPolicy": "error" }
  ],
  "rootRules": [
    { "typeName": "example.Graphics.ShaderMaterial", "featureIds": [ "shaders" ] },
    { "typeName": "example.Graphics.Sprite", "featureIds": [ "render2d" ] }
  ],
  "runtimeRequirements": [
    { "requirementId": "Regex", "featureIds": [ "shaders" ] }
  ]
}
""";

    CPPExternalFeatureCatalog catalog = CPPExternalFeatureCatalogLoader.LoadFromJson(json);

    Assert.Contains(catalog.Features, feature => feature.Id == "shaders");
    Assert.Contains(catalog.RootRules, rule => rule.TypeName == "example.Graphics.ShaderMaterial");
    Assert.Contains(catalog.RuntimeRequirements, rule => rule.RequirementId == "Regex");
}

[Fact]
public void Load_WhenRootRuleReferencesUnknownFeature_Throws() {
    string json = """
{
  "features": [
    { "id": "render2d", "defaultMode": "auto", "conflictPolicy": "error" }
  ],
  "rootRules": [
    { "typeName": "example.Graphics.ShaderMaterial", "featureIds": [ "shaders" ] }
  ]
}
""";

    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => CPPExternalFeatureCatalogLoader.LoadFromJson(json));

    Assert.Contains("shaders", exception.Message);
}
```

- [ ] **Step 2: Run the failing loader tests**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj' --filter 'FullyQualifiedName~CPPExternalFeatureCatalogLoaderTests' 2>&1 | Select-Object -Last 160 | Out-String -Width 260 | Write-Output"
```

Expected: FAIL because the external catalog types and loader do not exist yet.

- [ ] **Step 3: Write the minimal external catalog model types**

Add small model classes with XML comments and explicit string-based ids.

```csharp
namespace cs2.cpp {
    /// <summary>
    /// Describes one externally supplied feature definition consumed by the C#-to-C++ converter.
    /// </summary>
    public sealed class CPPExternalFeatureDefinition {
        /// <summary>
        /// Initializes a new external feature definition.
        /// </summary>
        public CPPExternalFeatureDefinition(string id, string defaultMode, string conflictPolicy) {
            Id = id;
            DefaultMode = defaultMode;
            ConflictPolicy = conflictPolicy;
        }

        /// <summary>
        /// Gets the free-form caller-owned feature id.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the default mode declared by the caller metadata.
        /// </summary>
        public string DefaultMode { get; }

        /// <summary>
        /// Gets the conflict policy declared by the caller metadata.
        /// </summary>
        public string ConflictPolicy { get; }
    }
}
```

- [ ] **Step 4: Write the loader and validation**

Implement `CPPExternalFeatureCatalogLoader` with hard-fail validation for:

- duplicate ids
- empty ids
- unknown feature references
- malformed arrays

Include a public helper with a direct signature like:

```csharp
public static CPPExternalFeatureCatalog LoadFromJson(string json)
```

and a file-based entry point like:

```csharp
public static CPPExternalFeatureCatalog LoadFromFile(string filePath)
```

- [ ] **Step 5: Run the loader tests to verify they pass**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj' --filter 'FullyQualifiedName~CPPExternalFeatureCatalogLoaderTests' 2>&1 | Select-Object -Last 160 | Out-String -Width 260 | Write-Output"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
rtk git -C C:\dev\helworks\csharpcodegen add cs2.cpp\model\CPPExternalFeatureDefinition.cs cs2.cpp\model\CPPExternalFeatureRootRule.cs cs2.cpp\model\CPPExternalRuntimeRequirementOwnership.cs cs2.cpp\model\CPPExternalFeatureCatalog.cs cs2.cpp\CPPExternalFeatureCatalogLoader.cs cs2.cpp.tests\CPPExternalFeatureCatalogLoaderTests.cs
rtk git -C C:\dev\helworks\csharpcodegen commit -m "Add external feature catalog loading to csharpcodegen"
```

### Task 2: Switch Feature Scanning And Reporting To External String Ids

**Files:**
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPFeatureScanner.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPFeatureResolver.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPGeneratedConfigWriter.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPConversionReportWriter.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPFeatureScannerTests.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPGeneratedConfigWriterTests.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPFeatureManifestWriterTests.cs`

- [ ] **Step 1: Write a failing scanner test using fixture metadata instead of built-in helengine roots**

Replace the current scanner expectation with a generic fixture that passes external metadata to the scanner.

```csharp
[Fact]
public void Scan_WhenExternalCatalogDeclaresShaderRoot_DetectsCallerOwnedFeatureId() {
    CPPExternalFeatureCatalog catalog = CPPExternalFeatureCatalogLoader.LoadFromJson("""
{
  "features": [
    { "id": "shaders", "defaultMode": "auto", "conflictPolicy": "error" }
  ],
  "rootRules": [
    { "typeName": "example.Graphics.ShaderMaterial", "featureIds": [ "shaders" ] }
  ]
}
""");

    IReadOnlyList<CPPFeatureUsageRoot> roots = ScanForFeatureRoots(
        """
namespace example.Graphics {
    public sealed class ShaderMaterial { }
}
""",
        catalog);

    Assert.Contains(roots, root => root.FeatureId == "shaders");
}
```

- [ ] **Step 2: Run the scanner/config/report test slice to verify it fails**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj' --filter 'FullyQualifiedName~CPPFeatureScannerTests|FullyQualifiedName~CPPGeneratedConfigWriterTests|FullyQualifiedName~CPPFeatureManifestWriterTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

Expected: FAIL because the scanner and writers still depend on built-in enum-driven feature ids.

- [ ] **Step 3: Replace enum-driven feature identities with external string ids**

Refactor the feature flow so usage roots, reports, and generated config entries carry string ids.

For example, the usage-root model should shift toward:

```csharp
public sealed class CPPFeatureUsageRoot {
    /// <summary>
    /// Gets or sets the caller-owned feature id detected for the scanned symbol.
    /// </summary>
    public string FeatureId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the fully qualified type name that triggered the feature.
    /// </summary>
    public string RootTypeName { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Update config/report emission to sanitize free-form ids generically**

Implement one shared sanitization rule so:

```text
debug_overlay -> HE_CPP_FEATURE_DEBUG_OVERLAY
host-file-system -> HE_CPP_FEATURE_HOST_FILE_SYSTEM
```

Do not special-case any `helengine` vocabulary.

- [ ] **Step 5: Run the scanner/config/report tests to verify they pass**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj' --filter 'FullyQualifiedName~CPPFeatureScannerTests|FullyQualifiedName~CPPGeneratedConfigWriterTests|FullyQualifiedName~CPPFeatureManifestWriterTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

Expected: PASS with fixture-manifest-driven assertions.

- [ ] **Step 6: Commit**

```powershell
rtk git -C C:\dev\helworks\csharpcodegen add cs2.cpp\CPPFeatureScanner.cs cs2.cpp\CPPFeatureResolver.cs cs2.cpp\CPPGeneratedConfigWriter.cs cs2.cpp\CPPConversionReportWriter.cs cs2.cpp.tests\CPPFeatureScannerTests.cs cs2.cpp.tests\CPPGeneratedConfigWriterTests.cs cs2.cpp.tests\CPPFeatureManifestWriterTests.cs
rtk git -C C:\dev\helworks\csharpcodegen commit -m "Use external feature ids for codegen reports and scanning"
```

### Task 3: Move Runtime Requirement Ownership To External Metadata

**Files:**
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPRuntimeRequirementCatalog.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPRestrictionValidator.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPFeatureOwnedRuntimeRequirementTests.cs`

- [ ] **Step 1: Write the failing runtime requirement ownership test**

Rewrite the ownership test so it supplies requirement ownership via the external catalog.

```csharp
[Fact]
public void Build_WhenExternalCatalogOwnsRegexByShaders_PrunesRequirementFromExternalMetadata() {
    CPPExternalFeatureCatalog catalog = CPPExternalFeatureCatalogLoader.LoadFromJson("""
{
  "features": [
    { "id": "shaders", "defaultMode": "auto", "conflictPolicy": "error" }
  ],
  "runtimeRequirements": [
    { "requirementId": "Regex", "featureIds": [ "shaders" ] }
  ]
}
""");

    CPPBuildUsageReport report = BuildUsageReport(catalog, enabledFeatureIds: new[] { "shaders" });
    CPPRuntimeRequirementDefinition regex = ResolveRequirement("Regex", report, catalog);

    Assert.Contains("shaders", regex.OwningFeatureIds);
}
```

- [ ] **Step 2: Run the focused requirement test to verify it fails**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj' --filter 'FullyQualifiedName~CPPFeatureOwnedRuntimeRequirementTests' 2>&1 | Select-Object -Last 180 | Out-String -Width 260 | Write-Output"
```

Expected: FAIL because ownership is still encoded in the built-in requirement catalog.

- [ ] **Step 3: Refactor runtime requirement ownership to external ids**

Move ownership association out of built-in feature enums and into externally supplied ownership records. `CPPRuntimeRequirementDefinition` should carry string ids rather than enum members.

```csharp
public List<string> OwningFeatureIds { get; } = new List<string>();
```

Keep the runtime requirement definitions themselves generic. Only the caller-supplied ownership should attach them to features.

- [ ] **Step 4: Run the requirement tests to verify they pass**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj' --filter 'FullyQualifiedName~CPPFeatureOwnedRuntimeRequirementTests' 2>&1 | Select-Object -Last 180 | Out-String -Width 260 | Write-Output"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk git -C C:\dev\helworks\csharpcodegen add cs2.cpp\CPPRuntimeRequirementCatalog.cs cs2.cpp\CPPRestrictionValidator.cs cs2.cpp.tests\CPPFeatureOwnedRuntimeRequirementTests.cs
rtk git -C C:\dev\helworks\csharpcodegen commit -m "Drive runtime requirement ownership from external metadata"
```

### Task 4: Publish Helengine Feature Metadata And Pass It Into Codegen

**Files:**
- Create: `engine/helengine.editor/codegen/features/helengine-feature-catalog.json`
- Modify: `engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Create: `engine/helengine.editor.tests/managers/project/HelengineFeatureCatalogIntegrityTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`

- [ ] **Step 1: Write the failing helengine metadata integrity test**

Create a focused test that reads the checked-in helengine catalog and verifies it declares current expected ids such as shaders and render2d.

```csharp
[Fact]
public void HelengineFeatureCatalog_declares_expected_feature_ids() {
    string filePath = Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "engine",
        "helengine.editor",
        "codegen",
        "features",
        "helengine-feature-catalog.json");

    string json = File.ReadAllText(Path.GetFullPath(filePath));

    Assert.Contains("\"shaders\"", json);
    Assert.Contains("\"render2d\"", json);
    Assert.Contains("\"host_file_system\"", json);
}
```

- [ ] **Step 2: Run the focused helengine metadata tests to verify they fail**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~HelengineFeatureCatalogIntegrityTests|FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

Expected: FAIL because the metadata file and codegen invocation path do not exist yet.

- [ ] **Step 3: Add the helengine feature catalog**

Create `engine/helengine.editor/codegen/features/helengine-feature-catalog.json` with the current feature meanings published as caller-owned metadata.

```json
{
  "features": [
    { "id": "render2d", "defaultMode": "auto", "conflictPolicy": "error" },
    { "id": "sprites", "defaultMode": "auto", "conflictPolicy": "error" },
    { "id": "text2d", "defaultMode": "auto", "conflictPolicy": "error" },
    { "id": "shaders", "defaultMode": "auto", "conflictPolicy": "error" },
    { "id": "debug_overlay", "defaultMode": "auto", "conflictPolicy": "error" },
    { "id": "runtime_json", "defaultMode": "auto", "conflictPolicy": "error" },
    { "id": "reflection_like_runtime", "defaultMode": "auto", "conflictPolicy": "error" },
    { "id": "host_file_system", "defaultMode": "auto", "conflictPolicy": "error" },
    { "id": "text_processing", "defaultMode": "auto", "conflictPolicy": "error" }
  ]
}
```

Extend the same file with root rules and runtime requirement ownership that reflect the current helengine behavior.

- [ ] **Step 4: Pass the metadata file into codegen invocation**

Update the generated-core codegen call path so it includes the external metadata file path. The implementation should thread the path from `helengine.editor` into the `csharpcodegen` command line or invocation object explicitly rather than relying on an implicit working-directory convention.

- [ ] **Step 5: Run the focused helengine metadata tests to verify they pass**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~HelengineFeatureCatalogIntegrityTests|FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
rtk git add engine/helengine.editor/codegen/features/helengine-feature-catalog.json engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs engine/helengine.editor.tests/managers/project/HelengineFeatureCatalogIntegrityTests.cs engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs
rtk git commit -m "Publish helengine codegen feature metadata"
```

### Task 5: Remove The Built-In Feature Catalog And Compatibility Path

**Files:**
- Delete: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPFeatureCatalog.cs`
- Delete or rewrite: `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPFeatureKind.cs`
- Modify: any remaining `csharpcodegen` files still referencing built-in feature ids
- Modify: affected `csharpcodegen` tests

- [ ] **Step 1: Write the failing source audit**

Add a test or source audit proving `csharpcodegen` no longer contains hardcoded `helengine` feature roots.

```csharp
[Fact]
public void Source_does_not_hardcode_helengine_feature_roots() {
    string source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "cs2.cpp", "CPPFeatureCatalog.cs"));

    Assert.DoesNotContain("helengine.", source, StringComparison.Ordinal);
}
```

If the file is being deleted entirely, rewrite the assertion to verify the file no longer exists.

- [ ] **Step 2: Run the source-audit and affected generic test slice to verify it fails**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj' --filter 'FullyQualifiedName~CPPFeatureScannerTests|FullyQualifiedName~CPPFeatureOwnedRuntimeRequirementTests|FullyQualifiedName~CPPGeneratedConfigWriterTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

Expected: FAIL while built-in catalog references still exist.

- [ ] **Step 3: Delete the built-in catalog and remove remaining enum-driven assumptions**

Delete `CPPFeatureCatalog.cs` and remove remaining dependencies on built-in product vocabulary. If `CPPFeatureKind` is still present after the previous tasks, delete it now and finish converting all feature-bearing models to string ids.

- [ ] **Step 4: Run the focused csharpcodegen test slices to verify they pass**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj' --filter 'FullyQualifiedName~CPPExternalFeatureCatalogLoaderTests|FullyQualifiedName~CPPFeatureScannerTests|FullyQualifiedName~CPPGeneratedConfigWriterTests|FullyQualifiedName~CPPFeatureManifestWriterTests|FullyQualifiedName~CPPFeatureOwnedRuntimeRequirementTests' 2>&1 | Select-Object -Last 260 | Out-String -Width 260 | Write-Output"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk git -C C:\dev\helworks\csharpcodegen add -A
rtk git -C C:\dev\helworks\csharpcodegen commit -m "Remove built-in engine feature knowledge from csharpcodegen"
```

### Task 6: End-To-End Verification With Helengine Windows Export

**Files:**
- Modify: any file from previous tasks only if end-to-end verification exposes a real contract bug

- [ ] **Step 1: Build the codegen project**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet build 'C:\dev\helworks\csharpcodegen\codegen\codegen.csproj' 2>&1 | Select-Object -Last 180 | Out-String -Width 260 | Write-Output"
```

Expected: PASS.

- [ ] **Step 2: Run the focused helengine editor test slice**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~HelengineFeatureCatalogIntegrityTests|FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests' 2>&1 | Select-Object -Last 240 | Out-String -Width 260 | Write-Output"
```

Expected: PASS.

- [ ] **Step 3: Build the city Windows export**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "Get-Process helengine_windows -ErrorAction SilentlyContinue | Stop-Process -Force; dotnet run --project 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\helengine.editor.app.csproj' -- --project 'C:\dev\helprojs\city\project.heproj' --build windows --output 'C:\dev\helprojs\output\windows-city-demo-disc' 2>&1 | Select-Object -Last 260 | Out-String -Width 260 | Write-Output"
```

Expected:

- build completes successfully
- summary reports shader/runtime features through external metadata rather than built-in codegen knowledge

- [ ] **Step 4: Confirm generated outputs reflect external metadata**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "Get-Content 'C:\dev\helprojs\output\windows-city-demo-disc\generated-core\cpp-conversion-report.json' -TotalCount 220 | Out-String -Width 260 | Write-Output"
```

and

```powershell
rtk proxy powershell -NoProfile -Command "Get-Content 'C:\dev\helprojs\output\windows-city-demo-disc\generated-core\helcpp_config.hpp' -TotalCount 220 | Out-String -Width 260 | Write-Output"
```

Expected:

- external feature ids appear in the report
- shader-related define is enabled through metadata-driven detection

- [ ] **Step 5: Record the migration in Graphiti**

Record that:

- `csharpcodegen` no longer hardcodes `helengine` feature ids or roots
- `helengine` now publishes checked-in external feature metadata
- Windows export feature detection is now driven through that metadata contract

- [ ] **Step 6: Commit final cleanup if needed**

```powershell
rtk git add -A
rtk git commit -m "Complete external codegen feature metadata migration"
```

Skip this commit if the prior task commits already leave all involved repos clean.
