# Core Authored Material Schema Extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove shader-authored and texture-slot-authored material semantics from `helengine.core` so authored material meaning lives only in platform schema settings and builder field values.

**Architecture:** `MaterialAsset` becomes a minimal core-authored shell that keeps only generic render-state and shadow flags. Editor material settings, preview resolution, and packaging flows stop mirroring schema data into `MaterialAsset` fields and instead treat `SchemaId` plus `FieldValues` as the source of truth. Shader-capable preview and cook paths move their authored material interpretation into shader/editor/platform-owned helpers without adding any new runtime abstraction layer.

**Tech Stack:** C#, .NET 9, HelEngine editor/runtime content pipeline, `helengine.baseplatform` material schema contracts, xUnit.

---

### Task 1: Lock the Material Boundary With Failing Ownership Tests

**Files:**
- Create: `engine/helengine.editor.tests/CoreAuthoredMaterialSchemaOwnershipTests.cs`
- Inspect: `engine/helengine.core/assets/raw/material/MaterialAsset.cs`
- Inspect: `engine/helengine.core/assets/raw/material/MaterialConstantBufferAsset.cs`
- Inspect: `engine/helengine.editor/managers/asset/MaterialAssetSettingsService.cs`

- [ ] **Step 1: Write source-level ownership tests that define the intended boundary**

Add a focused source test file:

```csharp
using System;
using System.IO;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies core-authored materials no longer own shader-specific schema meaning.
    /// </summary>
    public sealed class CoreAuthoredMaterialSchemaOwnershipTests {
        /// <summary>
        /// Ensures the core material asset no longer declares shader or texture-slot authored fields.
        /// </summary>
        [Fact]
        public void MaterialAsset_does_not_declare_shader_or_texture_slot_fields() {
            string source = File.ReadAllText(@"C:\dev\helworks\helengine\engine\helengine.core\assets\raw\material\MaterialAsset.cs");

            Assert.DoesNotContain("ShaderAssetId", source, StringComparison.Ordinal);
            Assert.DoesNotContain("VertexProgram", source, StringComparison.Ordinal);
            Assert.DoesNotContain("PixelProgram", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Variant", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DiffuseTextureAssetId", source, StringComparison.Ordinal);
            Assert.DoesNotContain("NormalTextureAssetId", source, StringComparison.Ordinal);
            Assert.DoesNotContain("EmissiveTextureAssetId", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ConstantBuffers", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the editor material settings service no longer mirrors schema field values into core material fields.
        /// </summary>
        [Fact]
        public void MaterialAssetSettingsService_does_not_apply_shader_or_texture_fields_to_core_materials() {
            string source = File.ReadAllText(@"C:\dev\helworks\helengine\engine\helengine.editor\managers\asset\MaterialAssetSettingsService.cs");

            Assert.DoesNotContain("ApplyPlatformRuntimeFields", source, StringComparison.Ordinal);
            Assert.DoesNotContain("materialAsset.ShaderAssetId", source, StringComparison.Ordinal);
            Assert.DoesNotContain("materialAsset.VertexProgram", source, StringComparison.Ordinal);
            Assert.DoesNotContain("materialAsset.PixelProgram", source, StringComparison.Ordinal);
            Assert.DoesNotContain("materialAsset.DiffuseTextureAssetId", source, StringComparison.Ordinal);
            Assert.DoesNotContain("materialAsset.ConstantBuffers", source, StringComparison.Ordinal);
        }
    }
}
```

- [ ] **Step 2: Run the ownership tests to verify they fail**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~CoreAuthoredMaterialSchemaOwnershipTests' 2>&1 | Select-Object -Last 120 | Out-String -Width 240 | Write-Output"
```

Expected: FAIL because `MaterialAsset` and `MaterialAssetSettingsService` still expose shader and texture-slot authored fields.

- [ ] **Step 3: Commit the red test checkpoint**

```bash
git add engine/helengine.editor.tests/CoreAuthoredMaterialSchemaOwnershipTests.cs
git commit -m "test: lock core authored material boundary"
```

### Task 2: Shrink `MaterialAsset` and Remove Shader Authored Serialization From Core

**Files:**
- Modify: `engine/helengine.core/assets/raw/material/MaterialAsset.cs`
- Delete: `engine/helengine.core/assets/raw/material/MaterialConstantBufferAsset.cs`
- Modify: `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
- Modify: `engine/helengine.files/assets/EditorAssetBinarySerializer.cs`
- Modify: `engine/helengine.editor.tests/BinarySerializationTests.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeSceneAssetReferenceResolver.cs`

- [ ] **Step 1: Write focused binary serialization expectations for the reduced core material**

Add or update one binary serialization test so it only expects generic core fields:

```csharp
[Fact]
public void MaterialAsset_binary_serialization_round_trips_generic_core_fields_only() {
    MaterialAsset asset = new MaterialAsset {
        Id = "materials/test",
        CastsShadows = false,
        ReceivesShadows = true,
        RenderState = new MaterialRenderState {
            DoubleSided = true
        }
    };

    byte[] bytes = AssetSerializer.SerializeToBytes(asset);
    MaterialAsset deserialized = AssetSerializer.DeserializeFromBytes<MaterialAsset>(bytes);

    Assert.Equal(asset.Id, deserialized.Id);
    Assert.Equal(asset.CastsShadows, deserialized.CastsShadows);
    Assert.Equal(asset.ReceivesShadows, deserialized.ReceivesShadows);
    Assert.Equal(asset.RenderState.DoubleSided, deserialized.RenderState.DoubleSided);
}
```

- [ ] **Step 2: Run the focused material serialization test and existing ownership test**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~MaterialAsset_binary_serialization_round_trips_generic_core_fields_only|FullyQualifiedName~CoreAuthoredMaterialSchemaOwnershipTests' 2>&1 | Select-Object -Last 140 | Out-String -Width 240 | Write-Output"
```

Expected: FAIL because the current serializers still read and write shader-specific fields.

- [ ] **Step 3: Reduce `MaterialAsset` to generic render-state data only**

Change `engine/helengine.core/assets/raw/material/MaterialAsset.cs` so it keeps only the generic shell:

```csharp
namespace helengine {
    /// <summary>
    /// Represents one generic authored material shell that stores cross-platform render-state values only.
    /// </summary>
    public class MaterialAsset : Asset {
        /// <summary>
        /// Initializes a new material asset with default render state and generic shadow flags.
        /// </summary>
        public MaterialAsset() {
            RenderState = new MaterialRenderState();
            CastsShadows = true;
            ReceivesShadows = true;
        }

        /// <summary>
        /// Gets or sets the fixed-function render state used while drawing the material.
        /// </summary>
        public MaterialRenderState RenderState;

        /// <summary>
        /// Gets or sets whether the material contributes geometry to shadow-map passes.
        /// </summary>
        public bool CastsShadows;

        /// <summary>
        /// Gets or sets whether the material receives shadow attenuation during lighting.
        /// </summary>
        public bool ReceivesShadows;
    }
}
```

- [ ] **Step 4: Remove `MaterialConstantBufferAsset` from core and stop serializing shader-authored fields**

Delete the core `MaterialConstantBufferAsset` file and remove the old field IO from both serializers.

The core material read path should become:

```csharp
static MaterialAsset ReadMaterialAsset(EngineBinaryReader reader, byte version) {
    MaterialAsset materialAsset = new MaterialAsset();
    materialAsset.Id = reader.ReadString();
    materialAsset.RenderState = ReadMaterialRenderState(reader);
    materialAsset.CastsShadows = reader.ReadBoolean();
    materialAsset.ReceivesShadows = reader.ReadBoolean();
    return materialAsset;
}
```

The write path in `engine/helengine.files/assets/EditorAssetBinarySerializer.cs` should likewise only write:

```csharp
writer.WriteString(asset.Id);
WriteMaterialRenderState(writer, asset.RenderState);
writer.WriteBoolean(asset.CastsShadows);
writer.WriteBoolean(asset.ReceivesShadows);
```

Also remove constant-buffer release logic from `RuntimeSceneAssetReferenceResolver` because core-authored materials no longer own that payload.

- [ ] **Step 5: Run the focused serializer and ownership tests again**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~CoreAuthoredMaterialSchemaOwnershipTests|FullyQualifiedName~MaterialAsset_binary_serialization_round_trips_generic_core_fields_only' 2>&1 | Select-Object -Last 140 | Out-String -Width 240 | Write-Output"
```

Expected: PASS.

- [ ] **Step 6: Commit the reduced core material contract**

```bash
git add engine/helengine.core engine/helengine.files engine/helengine.editor.tests
git commit -m "refactor: reduce core material asset to generic state"
```

### Task 3: Remove Material-Settings Mirroring and Make Schema Settings the Source of Truth

**Files:**
- Modify: `engine/helengine.editor/managers/asset/MaterialAssetSettingsService.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Modify: `engine/helengine.editor/tests/AssetImportManagerModelTests.cs`
- Modify: `engine/helengine.baseplatform.tests/Definitions/AssetImportSettingsMaterialSerializationTests.cs`
- Modify: `engine/helengine.editor/tests/components/ui/MaterialAssetViewTests.cs`

- [ ] **Step 1: Replace tests that expect mirrored shader or texture fields on `MaterialAsset`**

Convert tests that currently assert `LoadMaterialAsset(...).DiffuseTextureAssetId` or `ApplyPlatformMaterialFields(...)` into schema-source-of-truth tests.

Use expectations like:

```csharp
[Fact]
public void LoadOrCreate_preserves_texture_field_inside_platform_schema_settings() {
    MaterialAssetImportSettings settings = settingsService.LoadOrCreate(materialPath, materialAsset, new[] { "windows" }, ResolveSelectionModel);

    MaterialAssetProcessorSettings windowsSettings = settings.Processor.Platforms["windows"];

    Assert.Equal("standard-shader", windowsSettings.SchemaId);
    Assert.Equal("Textures/Fabric.png", windowsSettings.FieldValues["texture-id"]);
}
```

And:

```csharp
[Fact]
public void ApplyPlatformMaterialFields_is_removed_in_favor_of_schema_settings() {
    string source = File.ReadAllText(@"C:\dev\helworks\helengine\engine\helengine.editor\managers\asset\MaterialAssetSettingsService.cs");
    Assert.DoesNotContain("ApplyPlatformMaterialFields(", source, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the updated settings-focused tests to verify they fail**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~AssetImportManagerModelTests|FullyQualifiedName~MaterialAssetViewTests|FullyQualifiedName~CoreAuthoredMaterialSchemaOwnershipTests' 2>&1 | Select-Object -Last 180 | Out-String -Width 240 | Write-Output"
```

Expected: FAIL because the editor still loads mirrored runtime-facing `MaterialAsset` payloads.

- [ ] **Step 3: Remove `LoadMaterialAsset`, `ApplyPlatformMaterialFields`, and `ApplyPlatformRuntimeFields` from `MaterialAssetSettingsService`**

Delete the runtime-facing mirrored material methods and keep the service focused on schema settings documents only.

After the cut, the public surface should stay on methods like:

```csharp
public MaterialAssetImportSettings LoadOrCreate(...)
public void Save(string materialAssetPath, MaterialAssetImportSettings settings)
public bool TryLoad(string materialAssetPath, out MaterialAssetImportSettings settings)
public bool TryLoadPlatformSettings(string materialAssetPath, string platformId, out MaterialAssetProcessorSettings platformSettings)
public bool TryLoadMaterialAssetId(string materialAssetPath, out string assetId)
```

Any helper that seeds field values from legacy `MaterialAsset` fields should be removed or reduced to generic render-state seeding only.

- [ ] **Step 4: Update `AssetImportManager` and material settings seeding to write directly into schema field values**

Keep the importer seeding logic, but remove its dependence on `MaterialAsset` shader fields as a durable storage shape.

The seeding pattern should remain:

```csharp
settings.SchemaId = "standard-shader";
settings.FieldValues["texture-id"] = importedTextureAssetId;
settings.FieldValues["casts-shadow"] = "true";
settings.FieldValues["receives-shadow"] = "true";
settings.FieldValues["base-color"] = "#FFFFFFFF";
```

If custom shader support remains on shader-capable platforms, it should persist through schema field values only:

```csharp
settings.FieldValues["use-custom-shader"] = "true";
settings.FieldValues["shader-asset-id"] = shaderAssetId;
settings.FieldValues["vertex-program"] = vertexProgram;
settings.FieldValues["pixel-program"] = pixelProgram;
```

- [ ] **Step 5: Run the settings, import, and ownership tests again**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~AssetImportManagerModelTests|FullyQualifiedName~MaterialAssetViewTests|FullyQualifiedName~CoreAuthoredMaterialSchemaOwnershipTests' 2>&1 | Select-Object -Last 180 | Out-String -Width 240 | Write-Output"
```

Expected: PASS.

- [ ] **Step 6: Commit the schema-settings source-of-truth cut**

```bash
git add engine/helengine.editor engine/helengine.editor.tests engine/helengine.baseplatform.tests
git commit -m "refactor: make material schema settings authoritative"
```

### Task 4: Repair Editor Preview and Scene Resolution Without Shader-Shaped Core Materials

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Modify: `engine/helengine.editor/components/ui/MaterialAssetView.cs`
- Modify: `engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs`
- Modify: `engine/helengine.shader/assets/ShaderRuntimeMaterialLoader.cs`
- Modify: `engine/helengine.editor.tests/AssetImportManagerTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Write one focused regression for schema-driven material preview resolution**

Add a test that resolves a material through editor preview without reading shader fields from `MaterialAsset`:

```csharp
[Fact]
public void Editor_scene_material_resolution_uses_platform_schema_settings_instead_of_core_material_fields() {
    string source = File.ReadAllText(@"C:\dev\helworks\helengine\engine\helengine.editor\serialization\scene\EditorSceneAssetReferenceResolver.cs");

    Assert.DoesNotContain(".LoadMaterialAsset(", source, StringComparison.Ordinal);
    Assert.Contains("TryLoadPlatformSettings", source, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the preview-resolution regression to verify it fails**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~Editor_scene_material_resolution_uses_platform_schema_settings_instead_of_core_material_fields' 2>&1 | Select-Object -Last 120 | Out-String -Width 240 | Write-Output"
```

Expected: FAIL because preview still rebuilds a runtime-facing `MaterialAsset` from settings.

- [ ] **Step 3: Replace editor preview material loading with schema-driven resolution**

Update editor preview call sites so they resolve:

- the generic `MaterialAsset` shell from disk when generic render-state is needed
- the effective `MaterialAssetProcessorSettings` via `TryLoadPlatformSettings(...)`
- shader/platform-specific runtime material interpretation in editor/shader-owned code instead of core-authored fields

The new call shape should look like:

```csharp
MaterialAsset materialAsset = AssetSerializer.Load<MaterialAsset>(path);
MaterialAssetProcessorSettings platformSettings;
if (!settingsService.TryLoadPlatformSettings(path, platformId, out platformSettings)) {
    throw new InvalidOperationException($"Material settings for platform '{platformId}' could not be loaded from '{path}'.");
}

RuntimeMaterial runtimeMaterial = previewResolver.BuildRuntimeMaterial(materialAsset, platformSettings, platformId, path);
```

Keep the preview resolver outside `helengine.core`. If a helper type is needed, add it under `helengine.editor` or `helengine.shader`, not core.

- [ ] **Step 4: Run the preview-resolution regression and the existing preview/material tests**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~Editor_scene_material_resolution_uses_platform_schema_settings_instead_of_core_material_fields|FullyQualifiedName~AssetImportManagerTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests' 2>&1 | Select-Object -Last 180 | Out-String -Width 240 | Write-Output"
```

Expected: PASS.

- [ ] **Step 5: Commit the preview-resolution repair**

```bash
git add engine/helengine.editor engine/helengine.shader engine/helengine.editor.tests
git commit -m "refactor: resolve editor preview materials from schema settings"
```

### Task 5: Remove Packaging Fallbacks to Old Material Fields and Validate Windows Build Flow

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.baseplatform.tests/Builders/IPlatformAssetBuilderMetadataTests.cs`
- Modify: `engine/helengine.baseplatform.tests/Definitions/EditorPlatformBuildScenePackagerMaterialCookTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Replace cook-path tests that rely on `MaterialAsset` shader fallbacks**

Update cook-path tests so they assert builder inputs and outputs are driven by schema field values only.

Use expectations like:

```csharp
[Fact]
public void BuildMaterialCookFieldValues_uses_schema_field_values_without_core_shader_fallbacks() {
    Dictionary<string, string> fieldValues = BuildMaterialCookFieldValues(new MaterialAsset(), settings);

    Assert.Equal("ForwardStandardShader", fieldValues["shader-asset-id"]);
    Assert.Equal("ForwardStandardShader.vs", fieldValues["vertex-program"]);
    Assert.Equal("ForwardStandardShader.ps", fieldValues["pixel-program"]);
    Assert.Equal("default", fieldValues["variant"]);
}
```

And ensure no test still expects:

```csharp
materialAsset.ShaderAssetId
materialAsset.VertexProgram
materialAsset.PixelProgram
materialAsset.DiffuseTextureAssetId
materialAsset.ConstantBuffers
```

- [ ] **Step 2: Run the packaging-focused tests to verify they fail**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~EditorWindowsBuildScenePackagerTests' 2>&1 | Select-Object -Last 180 | Out-String -Width 240 | Write-Output"
```

And:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj' --filter 'FullyQualifiedName~IPlatformAssetBuilderMetadataTests|FullyQualifiedName~EditorPlatformBuildScenePackagerMaterialCookTests|FullyQualifiedName~AssetImportSettingsMaterialSerializationTests' 2>&1 | Select-Object -Last 180 | Out-String -Width 240 | Write-Output"
```

Expected: FAIL because the packagers still fall back to `MaterialAsset` shader and texture fields.

- [ ] **Step 3: Remove `MaterialAsset`-field fallback logic from the build packagers**

In both packager implementations, keep field-value normalization but remove methods that inspect removed material fields.

Delete or rewrite logic equivalent to:

```csharp
ResolveCustomShaderCookField(fieldValues, materialAsset, ShaderAssetIdFieldId, StandardShaderAssetId);
ResolveCustomShaderCookField(fieldValues, materialAsset, VertexProgramFieldId, StandardVertexProgramName);
ResolveCustomShaderCookField(fieldValues, materialAsset, PixelProgramFieldId, StandardPixelProgramName);
ApplyImportedTextureCookField(fieldValues, materialAsset);
```

Replace it with schema-only normalization:

```csharp
if (IsStandardShaderSchema(materialSettings.SchemaId) && !useCustomShader) {
    fieldValues[VariantFieldId] = StandardShaderVariantName;
    fieldValues[ShaderAssetIdFieldId] = resolvedStandardShaderId;
    fieldValues[VertexProgramFieldId] = StandardVertexProgramName;
    fieldValues[PixelProgramFieldId] = StandardPixelProgramName;
}

if (!fieldValues.TryGetValue("texture-relative-path", out string textureRelativePath) || string.IsNullOrWhiteSpace(textureRelativePath)) {
    string textureAssetId = fieldValues.TryGetValue(TextureAssetIdFieldId, out string value) ? value : string.Empty;
    if (!string.IsNullOrWhiteSpace(textureAssetId)) {
        fieldValues["texture-relative-path"] = BuildImportedTextureCookedRelativePath(textureAssetId);
    }
}
```

Generated standard material packaging should also build from schema settings alone:

```csharp
MaterialAssetProcessorSettings standardMaterialSettings = new MaterialAssetProcessorSettings();
schemaSettingsService.EnsureSelectedSchema(standardMaterialSettings, MaterialBuilder.Definition.MaterialSchemas);
Dictionary<string, string> standardMaterialFieldValues = BuildMaterialCookFieldValues(new MaterialAsset(), standardMaterialSettings);
```

Where `new MaterialAsset()` is now just the generic render-state shell, not a shader field carrier.

- [ ] **Step 4: Run the packaging tests again**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~EditorWindowsBuildScenePackagerTests' 2>&1 | Select-Object -Last 180 | Out-String -Width 240 | Write-Output"
```

And:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj' --filter 'FullyQualifiedName~IPlatformAssetBuilderMetadataTests|FullyQualifiedName~EditorPlatformBuildScenePackagerMaterialCookTests|FullyQualifiedName~AssetImportSettingsMaterialSerializationTests' 2>&1 | Select-Object -Last 180 | Out-String -Width 240 | Write-Output"
```

Expected: PASS.

- [ ] **Step 5: Run the smallest end-to-end Windows validation for the city project**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "Get-Process helengine_windows -ErrorAction SilentlyContinue | Stop-Process -Force; dotnet run --project 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\helengine.editor.app.csproj' -- --project 'C:\dev\helprojs\city\project.heproj' --build windows --output 'C:\dev\helprojs\output\windows-city-demo-disc' 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

Expected: `Build completed for platform 'windows': C:\dev\helprojs\output\windows-city-demo-disc`

- [ ] **Step 6: Commit the packaging cut**

```bash
git add engine/helengine.editor engine/helengine.baseplatform.tests engine/helengine.editor.tests
git commit -m "refactor: cook materials from schema settings only"
```

### Task 6: Final Focused Verification and Cleanup

**Files:**
- Verify: `engine/helengine.editor.tests/*`
- Verify: `engine/helengine.baseplatform.tests/*`
- Verify: `engine/helengine.core/*`

- [ ] **Step 1: Run the focused full validation slice for this migration**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~CoreAuthoredMaterialSchemaOwnershipTests|FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~AssetImportManagerModelTests|FullyQualifiedName~AssetImportManagerTests|FullyQualifiedName~MaterialAssetViewTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 240 | Write-Output"
```

And:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj' --filter 'FullyQualifiedName~IPlatformAssetBuilderMetadataTests|FullyQualifiedName~EditorPlatformBuildScenePackagerMaterialCookTests|FullyQualifiedName~AssetImportSettingsMaterialSerializationTests|FullyQualifiedName~MaterialAssetSchemaSettingsServiceTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 240 | Write-Output"
```

Expected: PASS for both focused test slices.

- [ ] **Step 2: Run a final core-boundary search to confirm the old authored fields are gone from core**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "rg --line-number --glob 'engine/helengine.core/**' 'ShaderAssetId|VertexProgram|PixelProgram|Variant|DiffuseTextureAssetId|NormalTextureAssetId|EmissiveTextureAssetId|MaterialConstantBufferAsset' engine\helengine.core 2>&1 | Out-String -Width 240 | Write-Output"
```

Expected: no matches in `engine/helengine.core`.

- [ ] **Step 3: Commit the final verification checkpoint**

```bash
git add .
git commit -m "test: verify core authored material schema extraction"
```
