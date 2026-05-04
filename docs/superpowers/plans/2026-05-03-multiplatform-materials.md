# Multiplatform Materials Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace shader-centric material authoring with builder-defined per-platform material settings, while keeping Windows shader materials working as the first compatibility slice and establishing the cook contract fixed-pipeline platforms will use later.

**Architecture:** Material authoring moves out of top-level `MaterialAsset` shader fields and into per-platform processor settings stored beside the asset. Platform builders publish dedicated material schemas and a cook translation contract. The editor renders schema-driven per-platform material UI, stores schema values in the existing processor-settings sidecar path, and the build pipeline cooks target-platform material payloads into runtime assets instead of copying authoring data through unchanged.

**Tech Stack:** C#/.NET, existing HELE asset serialization, custom editor binary serializers, xUnit, platform builder metadata in `helengine.baseplatform`, editor UI in `helengine.editor`.

---

## File Structure

### New Files

- `engine/helengine.baseplatform/Definitions/PlatformMaterialSchemaDefinition.cs`
  Builder-published material schema metadata.
- `engine/helengine.baseplatform/Definitions/PlatformMaterialFieldDefinition.cs`
  One field definition inside a material schema.
- `engine/helengine.baseplatform/Definitions/PlatformMaterialFieldKind.cs`
  Field kinds the editor can render and validate.
- `engine/helengine.baseplatform/Requests/PlatformMaterialCookRequest.cs`
  Typed request sent from the editor cook pipeline into the builder-owned material translator.
- `engine/helengine.baseplatform/Results/PlatformMaterialCookResult.cs`
  Cooked material asset plus dependency metadata returned by the builder contract.
- `engine/helengine.editor/managers/asset/MaterialAssetProcessorSettings.cs`
  Per-platform material settings payload stored in processor settings.
- `engine/helengine.editor/managers/asset/MaterialAssetProcessorFieldValue.cs`
  One persisted material field value.
- `engine/helengine.editor/managers/asset/MaterialAssetProcessorValueKind.cs`
  Typed value discriminator for persisted material field values.
- `engine/helengine.editor/managers/asset/MaterialAssetSettingsService.cs`
  Loads, seeds, validates, and saves per-platform material settings sidecars for `.helmat` assets.
- `engine/helengine.editor.tests/MaterialAssetViewTests.cs`
  Focused schema-driven material editor tests.

### Modified Files

- `engine/helengine.baseplatform/Definitions/PlatformDefinition.cs`
  Publish builder-owned material schemas.
- `engine/helengine.baseplatform/Builders/IPlatformAssetBuilder.cs`
  Add the material cook translation contract.
- `engine/helengine.baseplatform.tests/Definitions/PlatformDefinitionTests.cs`
  Cover schema metadata on platform definitions.
- `engine/helengine.baseplatform.tests/Builders/TestPlatformAssetBuilder.cs`
  Publish one shader-backed material schema and implement the new cook contract.
- `engine/helengine.editor/managers/project/EditorPlatformBuildSelectionModel.cs`
  Surface material schemas to editor UI.
- `engine/helengine.editor.tests/managers/project/EditorPlatformBuildSelectionModelTests.cs`
  Cover schema resolution through the selection model.
- `engine/helengine.editor/managers/asset/AssetPlatformProcessorSettings.cs`
  Add the material settings branch next to `Model`.
- `engine/helengine.editor/serialization/AssetImportSettingsBinarySerializer.cs`
  Persist material processor settings with a version bump.
- `engine/helengine.editor.tests/BinarySerializationTests.cs`
  Verify material processor settings round-trip.
- `engine/helengine.editor.tests/AssetImportManagerTests.cs`
  Verify material settings sidecars are created and migrated cleanly.
- `engine/helengine.editor/components/ui/MaterialAssetView.cs`
  Replace hardcoded shader picking with a schema-driven per-platform editor.
- `engine/helengine.editor/components/ui/PropertiesPanel.cs`
  Pass material settings and schema metadata into `MaterialAssetView`.
- `engine/helengine.editor/EditorSession.cs`
  Load material settings sidecars and supported platform material schemas when selecting a `.helmat`.
- `engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs`
  Route material cooking through the new builder contract.
- `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
  Stop copying raw authored materials through unchanged.
- `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
  Cook file-backed material references per target platform and track cooked dependencies from the result.
- `engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs`
  Load cooked material assets through the compatibility path for Windows shader materials.
- `engine/helengine.core/scene/runtime/RuntimeSceneAssetReferenceResolver.cs`
  Load cooked material assets instead of assuming authored shader-centric material payloads.
- `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
  Verify cooked material dependency tracking and target-platform failure modes.

## Task 1: Add Builder-Published Material Schema Metadata

**Files:**
- Create: `engine/helengine.baseplatform/Definitions/PlatformMaterialSchemaDefinition.cs`
- Create: `engine/helengine.baseplatform/Definitions/PlatformMaterialFieldDefinition.cs`
- Create: `engine/helengine.baseplatform/Definitions/PlatformMaterialFieldKind.cs`
- Modify: `engine/helengine.baseplatform/Definitions/PlatformDefinition.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildSelectionModel.cs`
- Modify: `engine/helengine.baseplatform.tests/Definitions/PlatformDefinitionTests.cs`
- Modify: `engine/helengine.baseplatform.tests/Builders/TestPlatformAssetBuilder.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildSelectionModelTests.cs`
- Test: `engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Write the failing metadata tests**

```csharp
[Fact]
public void PlatformDefinition_preserves_material_schema_metadata() {
    PlatformDefinition definition = new(
        "windows",
        "Windows DirectX",
        [],
        [
            new PlatformGraphicsProfileDefinition(
                "directx11",
                "DirectX 11",
                "Default Windows renderer",
                [])
        ],
        [],
        [
            new PlatformMaterialSchemaDefinition(
                "standard-shader",
                "Standard Shader",
                ["directx11"],
                [
                    new PlatformMaterialFieldDefinition(
                        "shader-asset-id",
                        "Shader Asset",
                        PlatformMaterialFieldKind.AssetReference,
                        string.Empty,
                        true,
                        [])
                ])
        ],
        [],
        []);

    Assert.Single(definition.MaterialSchemas);
    Assert.Equal("standard-shader", definition.MaterialSchemas[0].SchemaId);
    Assert.Equal(PlatformMaterialFieldKind.AssetReference, definition.MaterialSchemas[0].Fields[0].FieldKind);
}
```

```csharp
[Fact]
public void ResolveMaterialSchemas_returns_schemas_for_the_requested_graphics_profile() {
    PlatformDefinition definition = new(
        "windows",
        "Windows",
        [],
        [
            new PlatformGraphicsProfileDefinition(
                "directx11",
                "DirectX 11",
                "Windows renderer",
                [])
        ],
        [],
        [
            new PlatformMaterialSchemaDefinition(
                "standard-shader",
                "Standard Shader",
                ["directx11"],
                [
                    new PlatformMaterialFieldDefinition(
                        "variant",
                        "Variant",
                        PlatformMaterialFieldKind.Choice,
                        "default",
                        true,
                        ["default", "skinned"])
                ])
        ],
        [],
        []);

    EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(definition);

    PlatformMaterialSchemaDefinition[] schemas = selectionModel.ResolveMaterialSchemas("directx11");

    Assert.Single(schemas);
    Assert.Equal("standard-shader", schemas[0].SchemaId);
}
```

- [ ] **Step 2: Run the focused tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlatformDefinitionTests"
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildSelectionModelTests"
```

Expected:
- FAIL because `PlatformDefinition` does not expose `MaterialSchemas`
- FAIL because `EditorPlatformBuildSelectionModel` does not resolve material schemas

- [ ] **Step 3: Implement the schema metadata model and surface it through platform selection**

```csharp
public enum PlatformMaterialFieldKind {
    Boolean,
    Text,
    Choice,
    Number,
    AssetReference,
    Color
}
```

```csharp
public class PlatformMaterialSchemaDefinition {
    public PlatformMaterialSchemaDefinition(
        string schemaId,
        string displayName,
        string[] graphicsProfileIds,
        PlatformMaterialFieldDefinition[] fields) {
        // validate required ids, profile list, and field list
    }

    public string SchemaId { get; }
    public string DisplayName { get; }
    public string[] GraphicsProfileIds { get; }
    public PlatformMaterialFieldDefinition[] Fields { get; }
}
```

```csharp
public class PlatformDefinition {
    public PlatformDefinition(
        string platformId,
        string displayName,
        PlatformBuildProfileDefinition[] buildProfiles,
        PlatformGraphicsProfileDefinition[] graphicsProfiles,
        PlatformAssetRequirementDefinition[] assetRequirements,
        PlatformMaterialSchemaDefinition[] materialSchemas,
        PlatformComponentCompatibilityDefinition[] componentCompatibilities,
        PlatformCodegenProfileDefinition[] codegenProfiles,
        PlatformStorageProfileDefinition[] storageProfiles,
        PlatformMediaProfileDefinition[] mediaProfiles) {
        MaterialSchemas = [.. materialSchemas];
    }

    public PlatformMaterialSchemaDefinition[] MaterialSchemas { get; }
}
```

```csharp
public PlatformMaterialSchemaDefinition[] ResolveMaterialSchemas(string graphicsProfileId) {
    string resolvedGraphicsProfileId = ResolveGraphicsProfile(graphicsProfileId)?.ProfileId ?? string.Empty;
    return MaterialSchemas
        .Where(schema => schema.GraphicsProfileIds.Length == 0 ||
            schema.GraphicsProfileIds.Contains(resolvedGraphicsProfileId, StringComparer.OrdinalIgnoreCase))
        .ToArray();
}
```

- [ ] **Step 4: Re-run the focused tests**

Run:

```bash
rtk dotnet test engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlatformDefinitionTests"
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildSelectionModelTests"
```

Expected:
- PASS for the new schema metadata coverage
- PASS for selection-model material schema resolution

- [ ] **Step 5: Commit the metadata foundation**

```bash
git add engine/helengine.baseplatform/Definitions/PlatformMaterialSchemaDefinition.cs engine/helengine.baseplatform/Definitions/PlatformMaterialFieldDefinition.cs engine/helengine.baseplatform/Definitions/PlatformMaterialFieldKind.cs engine/helengine.baseplatform/Definitions/PlatformDefinition.cs engine/helengine.editor/managers/project/EditorPlatformBuildSelectionModel.cs engine/helengine.baseplatform.tests/Definitions/PlatformDefinitionTests.cs engine/helengine.baseplatform.tests/Builders/TestPlatformAssetBuilder.cs engine/helengine.editor.tests/managers/project/EditorPlatformBuildSelectionModelTests.cs
git commit -m "feat: add platform material schema metadata"
```

## Task 2: Persist Per-Platform Material Processor Settings

**Files:**
- Create: `engine/helengine.editor/managers/asset/MaterialAssetProcessorSettings.cs`
- Create: `engine/helengine.editor/managers/asset/MaterialAssetProcessorFieldValue.cs`
- Create: `engine/helengine.editor/managers/asset/MaterialAssetProcessorValueKind.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetPlatformProcessorSettings.cs`
- Modify: `engine/helengine.editor/serialization/AssetImportSettingsBinarySerializer.cs`
- Modify: `engine/helengine.editor.tests/BinarySerializationTests.cs`
- Modify: `engine/helengine.editor.tests/AssetImportManagerTests.cs`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Add failing serializer coverage for material settings**

```csharp
[Fact]
public void AssetImportSettingsBinarySerializer_round_trips_material_processor_settings() {
    AssetImportSettings settings = new AssetImportSettings {
        Importer = new AssetImporterSettings {
            ImporterId = "material-authoring",
            SourceChecksum = "abc123",
            AssetId = "Materials/Test.helmat"
        },
        Processor = new AssetProcessorSettings {
            Platforms = new Dictionary<string, AssetPlatformProcessorSettings> {
                ["windows"] = new AssetPlatformProcessorSettings {
                    Material = new MaterialAssetProcessorSettings {
                        SchemaId = "standard-shader",
                        Fields = [
                            new MaterialAssetProcessorFieldValue("shader-asset-id", MaterialAssetProcessorValueKind.AssetReference) {
                                TextValue = "Shaders/EditorDefaultMesh.hlsl"
                            },
                            new MaterialAssetProcessorFieldValue("variant", MaterialAssetProcessorValueKind.Text) {
                                TextValue = "default"
                            }
                        ]
                    }
                }
            }
        }
    };

    using MemoryStream stream = new MemoryStream();
    AssetImportSettingsBinarySerializer.Serialize(stream, settings);
    stream.Position = 0;
    AssetImportSettings deserialized = AssetImportSettingsBinarySerializer.Deserialize(stream);

    Assert.Equal("standard-shader", deserialized.Processor.Platforms["windows"].Material.SchemaId);
    Assert.Equal("default", deserialized.Processor.Platforms["windows"].Material.Fields[1].TextValue);
}
```

- [ ] **Step 2: Run the serializer-focused tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests"
```

Expected:
- FAIL because `AssetPlatformProcessorSettings` has no `Material`
- FAIL because the binary serializer only knows how to write the model flip-winding byte

- [ ] **Step 3: Add the material processor payload model and serializer version 3**

```csharp
public class MaterialAssetProcessorSettings {
    public MaterialAssetProcessorSettings() {
        SchemaId = string.Empty;
        Fields = Array.Empty<MaterialAssetProcessorFieldValue>();
    }

    public string SchemaId { get; set; }
    public MaterialAssetProcessorFieldValue[] Fields { get; set; }
}
```

```csharp
public class MaterialAssetProcessorFieldValue {
    public MaterialAssetProcessorFieldValue() {
        FieldId = string.Empty;
    }

    public MaterialAssetProcessorFieldValue(string fieldId, MaterialAssetProcessorValueKind valueKind) {
        FieldId = fieldId;
        ValueKind = valueKind;
    }

    public string FieldId { get; set; }
    public MaterialAssetProcessorValueKind ValueKind { get; set; }
    public string TextValue { get; set; } = string.Empty;
    public double NumberValue { get; set; }
    public bool BooleanValue { get; set; }
    public float4 ColorValue { get; set; }
}
```

```csharp
public class AssetPlatformProcessorSettings {
    public AssetPlatformProcessorSettings() {
        Model = new ModelAssetProcessorSettings();
        Material = new MaterialAssetProcessorSettings();
    }

    public ModelAssetProcessorSettings Model { get; set; }
    public MaterialAssetProcessorSettings Material { get; set; }
}
```

```csharp
public const byte CurrentVersion = 3;

writer.WriteString(entry.Value.Material.SchemaId);
writer.WriteInt32(entry.Value.Material.Fields.Length);
for (int fieldIndex = 0; fieldIndex < entry.Value.Material.Fields.Length; fieldIndex++) {
    MaterialAssetProcessorFieldValue field = entry.Value.Material.Fields[fieldIndex];
    writer.WriteString(field.FieldId);
    writer.WriteByte((byte)field.ValueKind);
    writer.WriteString(field.TextValue ?? string.Empty);
    writer.WriteInt64(BitConverter.DoubleToInt64Bits(field.NumberValue));
    writer.WriteByte(field.BooleanValue ? (byte)1 : (byte)0);
    writer.WriteSingle(field.ColorValue.X);
    writer.WriteSingle(field.ColorValue.Y);
    writer.WriteSingle(field.ColorValue.Z);
    writer.WriteSingle(field.ColorValue.W);
}
```

- [ ] **Step 4: Re-run the serializer tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests"
```

Expected:
- PASS for asset-import-settings round-trip
- PASS for version validation with the new serializer version

- [ ] **Step 5: Commit the material settings persistence layer**

```bash
git add engine/helengine.editor/managers/asset/MaterialAssetProcessorSettings.cs engine/helengine.editor/managers/asset/MaterialAssetProcessorFieldValue.cs engine/helengine.editor/managers/asset/MaterialAssetProcessorValueKind.cs engine/helengine.editor/managers/asset/AssetPlatformProcessorSettings.cs engine/helengine.editor/serialization/AssetImportSettingsBinarySerializer.cs engine/helengine.editor.tests/BinarySerializationTests.cs engine/helengine.editor.tests/AssetImportManagerTests.cs
git commit -m "feat: persist per-platform material settings"
```

## Task 3: Load Material Sidecars And Seed Legacy Shader Materials

**Files:**
- Create: `engine/helengine.editor/managers/asset/MaterialAssetSettingsService.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Modify: `engine/helengine.editor.tests/AssetImportManagerTests.cs`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Add failing coverage for legacy material seeding**

```csharp
[Fact]
public void LoadOrCreateMaterialSettings_when_legacy_shader_fields_exist_seeds_windows_schema_values() {
    string materialPath = WriteMaterialAsset(
        "Materials/TestMaterial.helmat",
        shaderAssetId: "EditorDefaultMesh",
        vertexProgram: "EditorDefaultMesh.vs",
        pixelProgram: "EditorDefaultMesh.ps",
        variant: "default");

    MaterialAssetSettingsService service = CreateMaterialSettingsService();

    AssetImportSettings settings = service.LoadOrCreateMaterialSettings(materialPath, "windows", ["windows"]);

    Assert.Equal("standard-shader", settings.Processor.Platforms["windows"].Material.SchemaId);
    Assert.Contains(
        settings.Processor.Platforms["windows"].Material.Fields,
        field => field.FieldId == "shader-asset-id" && field.TextValue == "EditorDefaultMesh");
}
```

- [ ] **Step 2: Run the material-settings service tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportManagerTests"
```

Expected:
- FAIL because there is no material settings service
- FAIL because material selection does not create processor settings for `.helmat` assets

- [ ] **Step 3: Implement material sidecar loading and legacy seeding**

```csharp
public sealed class MaterialAssetSettingsService {
    const string MaterialSettingsImporterId = "material-authoring";

    public AssetImportSettings LoadOrCreateMaterialSettings(
        string materialPath,
        string activePlatformId,
        IReadOnlyList<string> supportedPlatforms,
        MaterialAsset materialAsset,
        IReadOnlyDictionary<string, PlatformMaterialSchemaDefinition[]> schemasByPlatform) {
        AssetImportSettings settings = LoadExistingOrCreateDefaults(materialPath);
        EnsureSupportedPlatformsExist(settings, supportedPlatforms);
        SeedLegacyShaderFields(settings, materialAsset, schemasByPlatform);
        return settings;
    }
}
```

```csharp
void SeedLegacyShaderFields(
    AssetImportSettings settings,
    MaterialAsset materialAsset,
    IReadOnlyDictionary<string, PlatformMaterialSchemaDefinition[]> schemasByPlatform) {
    if (string.IsNullOrWhiteSpace(materialAsset.ShaderAssetId)) {
        return;
    }

    if (!settings.Processor.Platforms.TryGetValue("windows", out AssetPlatformProcessorSettings platformSettings)) {
        return;
    }

    if (!string.IsNullOrWhiteSpace(platformSettings.Material.SchemaId)) {
        return;
    }

    platformSettings.Material.SchemaId = "standard-shader";
    platformSettings.Material.Fields = [
        new MaterialAssetProcessorFieldValue("shader-asset-id", MaterialAssetProcessorValueKind.Text) { TextValue = materialAsset.ShaderAssetId },
        new MaterialAssetProcessorFieldValue("vertex-program", MaterialAssetProcessorValueKind.Text) { TextValue = materialAsset.VertexProgram },
        new MaterialAssetProcessorFieldValue("pixel-program", MaterialAssetProcessorValueKind.Text) { TextValue = materialAsset.PixelProgram },
        new MaterialAssetProcessorFieldValue("variant", MaterialAssetProcessorValueKind.Text) { TextValue = materialAsset.Variant }
    ];
}
```

```csharp
MaterialAsset materialAsset = LoadMaterialAsset(entry.FullPath);
AssetImportSettings settings = MaterialAssetSettingsService.LoadOrCreateMaterialSettings(
    entry.FullPath,
    CurrentProjectPlatform,
    SupportedPlatforms,
    materialAsset,
    BuildMaterialSchemaLookup());
propertiesPanel.ShowMaterialSettings(entry, materialAsset, settings, BuildMaterialSchemaLookup(), CurrentProjectPlatform);
```

- [ ] **Step 4: Re-run the targeted tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportManagerTests"
```

Expected:
- PASS for sidecar creation and legacy shader seeding
- PASS for settings-version rewrite behavior remaining intact

- [ ] **Step 5: Commit the material settings access layer**

```bash
git add engine/helengine.editor/managers/asset/MaterialAssetSettingsService.cs engine/helengine.editor/managers/asset/AssetImportManager.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor.tests/AssetImportManagerTests.cs
git commit -m "feat: load and seed material platform settings"
```

## Task 4: Replace The Hardcoded Shader Picker With A Schema-Driven Material Editor

**Files:**
- Modify: `engine/helengine.editor/components/ui/MaterialAssetView.cs`
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Create: `engine/helengine.editor.tests/MaterialAssetViewTests.cs`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Add failing material editor tests**

```csharp
[Fact]
public void Show_when_multiple_platforms_are_available_selects_the_active_platform_schema() {
    MaterialAssetView view = new MaterialAssetView(CreateFont(), 1);
    AssetImportSettings settings = CreateMaterialSettings();

    view.Show(
        CreateMaterialEntry(),
        CreateMaterialAsset(),
        settings,
        CreateSchemasByPlatform(),
        ["windows", "ps2"],
        "ps2");

    Assert.Equal("ps2", view.SelectedPlatformId);
    Assert.Equal("fixed-unlit-texture", view.SelectedSchemaId);
}
```

```csharp
[Fact]
public void Apply_when_schema_field_changes_updates_the_target_platform_only() {
    MaterialAssetView view = new MaterialAssetView(CreateFont(), 1);
    AssetImportSettings settings = CreateMaterialSettings();
    AssetImportSettingsApplyRequest raisedRequest = null;
    view.ApplyRequested += request => raisedRequest = request;

    view.Show(
        CreateMaterialEntry(),
        CreateMaterialAsset(),
        settings,
        CreateSchemasByPlatform(),
        ["windows", "ps2"],
        "windows");

    view.SetSelectedSchema("standard-shader");
    view.SetTextFieldValue("variant", "skinned");
    view.ApplyChanges();

    Assert.NotNull(raisedRequest);
    Assert.Equal("skinned", raisedRequest.ProcessorSettings.Platforms["windows"].Material.Fields.Single(field => field.FieldId == "variant").TextValue);
    Assert.Equal("fixed-unlit-texture", raisedRequest.ProcessorSettings.Platforms["ps2"].Material.SchemaId);
}
```

- [ ] **Step 2: Run the material editor tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~MaterialAssetViewTests"
```

Expected:
- FAIL because `MaterialAssetView` only exposes one shader picker row
- FAIL because it does not accept material settings or schema metadata

- [ ] **Step 3: Implement the schema-driven editor UI**

```csharp
public void Show(
    AssetBrowserEntry entry,
    MaterialAsset materialAsset,
    AssetImportSettings settings,
    IReadOnlyDictionary<string, PlatformMaterialSchemaDefinition[]> schemasByPlatform,
    IReadOnlyList<string> supportedPlatforms,
    string activePlatformId) {
    CurrentEntry = entry;
    CurrentAsset = materialAsset;
    ActiveSettings = CloneProcessorSettings(settings.Processor);
    PendingSettings = CloneProcessorSettings(settings.Processor);
    SchemasByPlatform = schemasByPlatform;
    SupportedPlatformIds = [.. supportedPlatforms];
    CurrentPlatformId = ResolveSelectedPlatformId(activePlatformId);
    RebuildSchemaControls();
}
```

```csharp
void RebuildSchemaControls() {
    PlatformMaterialSchemaDefinition[] schemas = ResolveSchemasForPlatform(CurrentPlatformId);
    MaterialAssetProcessorSettings platformSettings = GetPendingMaterialSettings(CurrentPlatformId);
    EnsureSelectedSchemaExists(platformSettings, schemas);
    RebuildFieldEditors(platformSettings.SchemaId, schemas);
}
```

```csharp
AssetImportSettingsApplyRequest BuildApplyRequest() {
    return new AssetImportSettingsApplyRequest(
        "material-authoring",
        CurrentPlatformId,
        new AssetProcessorSettings {
            Platforms = ClonePlatformSettings(PendingSettings.Platforms)
        });
}
```

- [ ] **Step 4: Re-run the focused material editor tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~MaterialAssetViewTests"
```

Expected:
- PASS for platform tab selection
- PASS for per-platform schema/value editing isolation

- [ ] **Step 5: Commit the schema-driven material editor**

```bash
git add engine/helengine.editor/components/ui/MaterialAssetView.cs engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor.tests/MaterialAssetViewTests.cs
git commit -m "feat: add schema-driven material authoring UI"
```

## Task 5: Add The Builder-Owned Material Cook Contract And Windows Shader Adapter

**Files:**
- Create: `engine/helengine.baseplatform/Requests/PlatformMaterialCookRequest.cs`
- Create: `engine/helengine.baseplatform/Results/PlatformMaterialCookResult.cs`
- Modify: `engine/helengine.baseplatform/Builders/IPlatformAssetBuilder.cs`
- Modify: `engine/helengine.baseplatform.tests/Builders/TestPlatformAssetBuilder.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs`
- Test: `engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Add failing cook-contract tests**

```csharp
[Fact]
public async Task BuildAsync_material_cook_contract_maps_windows_shader_schema_into_runtime_material_asset() {
    TestPlatformAssetBuilder builder = new TestPlatformAssetBuilder();
    PlatformMaterialCookRequest request = new(
        "windows",
        "directx11",
        new MaterialAsset { Id = "Materials/Test.helmat" },
        new MaterialAssetProcessorSettings {
            SchemaId = "standard-shader",
            Fields = [
                new MaterialAssetProcessorFieldValue("shader-asset-id", MaterialAssetProcessorValueKind.Text) { TextValue = "EditorDefaultMesh" },
                new MaterialAssetProcessorFieldValue("vertex-program", MaterialAssetProcessorValueKind.Text) { TextValue = "EditorDefaultMesh.vs" },
                new MaterialAssetProcessorFieldValue("pixel-program", MaterialAssetProcessorValueKind.Text) { TextValue = "EditorDefaultMesh.ps" },
                new MaterialAssetProcessorFieldValue("variant", MaterialAssetProcessorValueKind.Text) { TextValue = "default" }
            ]
        });

    PlatformMaterialCookResult result = builder.CookMaterial(request);

    MaterialAsset cookedAsset = Assert.IsType<MaterialAsset>(result.CookedMaterialAsset);
    Assert.Equal("EditorDefaultMesh", cookedAsset.ShaderAssetId);
    Assert.Contains("EditorDefaultMesh", result.ReferencedShaderAssetIds);
}
```

- [ ] **Step 2: Run the contract-focused tests**

Run:

```bash
rtk dotnet test engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~TestPlatformAssetBuilder"
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformAssetCookServiceTests"
```

Expected:
- FAIL because `IPlatformAssetBuilder` has no material cook method
- FAIL because the editor cook service cannot request a cooked material from the builder

- [ ] **Step 3: Implement the cook contract and Windows shader compatibility translator**

```csharp
public sealed class PlatformMaterialCookRequest {
    public PlatformMaterialCookRequest(
        string platformId,
        string graphicsProfileId,
        MaterialAsset sourceMaterialAsset,
        MaterialAssetProcessorSettings processorSettings) {
        PlatformId = platformId;
        GraphicsProfileId = graphicsProfileId;
        SourceMaterialAsset = sourceMaterialAsset;
        ProcessorSettings = processorSettings;
    }

    public string PlatformId { get; }
    public string GraphicsProfileId { get; }
    public MaterialAsset SourceMaterialAsset { get; }
    public MaterialAssetProcessorSettings ProcessorSettings { get; }
}
```

```csharp
public sealed class PlatformMaterialCookResult {
    public PlatformMaterialCookResult(Asset cookedMaterialAsset, string[] referencedShaderAssetIds) {
        CookedMaterialAsset = cookedMaterialAsset;
        ReferencedShaderAssetIds = referencedShaderAssetIds ?? Array.Empty<string>();
    }

    public Asset CookedMaterialAsset { get; }
    public string[] ReferencedShaderAssetIds { get; }
}
```

```csharp
public interface IPlatformAssetBuilder {
    PlatformBuilderDescriptor Descriptor { get; }
    PlatformDefinition Definition { get; }
    PlatformMaterialCookResult CookMaterial(PlatformMaterialCookRequest request);
    Task<PlatformBuildReport> BuildAsync(...);
}
```

```csharp
public PlatformMaterialCookResult CookMaterial(PlatformMaterialCookRequest request) {
    if (!string.Equals(request.ProcessorSettings.SchemaId, "standard-shader", StringComparison.Ordinal)) {
        throw new InvalidOperationException("Unsupported Windows material schema.");
    }

    MaterialAsset cookedAsset = new MaterialAsset {
        Id = request.SourceMaterialAsset.Id,
        ShaderAssetId = GetRequiredField(request.ProcessorSettings, "shader-asset-id"),
        VertexProgram = GetRequiredField(request.ProcessorSettings, "vertex-program"),
        PixelProgram = GetRequiredField(request.ProcessorSettings, "pixel-program"),
        Variant = GetRequiredField(request.ProcessorSettings, "variant"),
        RenderState = new MaterialRenderState(),
        ConstantBuffers = Array.Empty<MaterialConstantBufferAsset>()
    };

    return new PlatformMaterialCookResult(cookedAsset, [cookedAsset.ShaderAssetId]);
}
```

- [ ] **Step 4: Re-run the cook-contract tests**

Run:

```bash
rtk dotnet test engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~TestPlatformAssetBuilder"
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformAssetCookServiceTests"
```

Expected:
- PASS for Windows shader schema translation
- PASS for the editor cook service being able to request cooked material outputs

- [ ] **Step 5: Commit the cook contract**

```bash
git add engine/helengine.baseplatform/Requests/PlatformMaterialCookRequest.cs engine/helengine.baseplatform/Results/PlatformMaterialCookResult.cs engine/helengine.baseplatform/Builders/IPlatformAssetBuilder.cs engine/helengine.baseplatform.tests/Builders/TestPlatformAssetBuilder.cs engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs
git commit -m "feat: add platform material cook contract"
```

## Task 6: Cook Target-Platform Materials During Scene Packaging And Runtime Load

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeSceneAssetReferenceResolver.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Add failing packaging tests that assert cooked material dependencies come from platform settings**

```csharp
[Fact]
public void Package_when_material_schema_cooks_to_windows_shader_reports_referenced_shader_id_from_cooked_result() {
    string sceneId = "Scenes/TestScene.helen";
    string materialRelativePath = "Materials/TestMaterial.helmat";

    WriteMaterialAsset(materialRelativePath, shaderAssetId: string.Empty);
    WriteMaterialSettings(materialRelativePath, "windows", "standard-shader", new Dictionary<string, string> {
        ["shader-asset-id"] = "EditorDefaultMesh",
        ["vertex-program"] = "EditorDefaultMesh.vs",
        ["pixel-program"] = "EditorDefaultMesh.ps",
        ["variant"] = "default"
    });
    WriteSceneAsset(sceneId, materialRelativePath);

    EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(ProjectRootPath, Array.Empty<IAssetImporterRegistration>(), CreateWindowsDefinition());
    EditorPlatformBuildScenePackagerResult result = packager.Package(new[] { sceneId }, BuildRootPath);

    Assert.Equal(new[] { "EditorDefaultMesh" }, result.ReferencedShaderAssetIds);
}
```

```csharp
[Fact]
public void Package_when_target_platform_material_settings_are_missing_fails_clearly() {
    string sceneId = "Scenes/TestScene.helen";
    string materialRelativePath = "Materials/TestMaterial.helmat";

    WriteMaterialAsset(materialRelativePath, shaderAssetId: string.Empty);
    WriteSceneAsset(sceneId, materialRelativePath);

    EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(ProjectRootPath, Array.Empty<IAssetImporterRegistration>(), CreateWindowsDefinition());

    InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => packager.Package(new[] { sceneId }, BuildRootPath));

    Assert.Contains("material settings", ex.Message);
    Assert.Contains("windows", ex.Message);
}
```

- [ ] **Step 2: Run the packaging tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildScenePackagerTests"
```

Expected:
- FAIL because the packager still copies raw authored materials and reads top-level `ShaderAssetId`
- FAIL because missing target-platform material settings are not validated

- [ ] **Step 3: Integrate cooked material outputs into the asset cook and packaging path**

```csharp
SceneAssetReference RewriteFileSystemMaterialReference(SceneAssetReference reference, string buildRootPath) {
    string fullPath = ResolveProjectAssetPath(reference.RelativePath);
    MaterialAsset sourceMaterial = ProjectContentManager.Load<MaterialAsset>(fullPath, EditorContentProcessorIds.MaterialAsset);
    AssetImportSettings settings = MaterialAssetSettingsService.LoadExistingMaterialSettings(fullPath);
    MaterialAssetProcessorSettings platformSettings = ResolveRequiredPlatformMaterialSettings(settings, PlatformId);
    PlatformMaterialCookResult cookResult = Builder.CookMaterial(
        new PlatformMaterialCookRequest(PlatformId, ResolveGraphicsProfileId(), sourceMaterial, platformSettings));

    string cookedRelativePath = BuildCookedMaterialRelativePath(reference.RelativePath);
    WriteAsset(Path.Combine(buildRootPath, cookedRelativePath), cookResult.CookedMaterialAsset);
    RememberReferencedShaderIds(cookResult.ReferencedShaderAssetIds);
    return CreateFileSystemReference(cookedRelativePath);
}
```

```csharp
static string ResolveArtifactKind(string relativePath) {
    if (relativePath.StartsWith("cooked/materials/", StringComparison.OrdinalIgnoreCase)) {
        return "material";
    }

    // keep existing scene/font/shader/model resolution rules
}
```

```csharp
RuntimeMaterial ResolveMaterial(SceneAssetReference reference) {
    string fullPath = ResolveFileBackedAssetPath(reference);
    MaterialAsset materialAsset = AssetContentManager.Load<MaterialAsset>(fullPath, RuntimeContentProcessorIds.MaterialAsset);
    ShaderAsset shaderAsset = AssetContentManager.Load<ShaderAsset>(
        ResolveShaderPackagePath(materialAsset.ShaderAssetId),
        RuntimeContentProcessorIds.ShaderAsset);
    return Core.Instance.RenderManager3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
}
```

The runtime/editor resolver stays on the current `MaterialAsset` + `ShaderAsset` compatibility path for the first Windows slice. The key change is that the asset being loaded is now the cooked target-platform material, not the authored source material.

- [ ] **Step 4: Re-run the packaging and runtime compatibility tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildScenePackagerTests"
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformAssetCookServiceTests"
```

Expected:
- PASS for shader dependency tracking from cooked material outputs
- PASS for clear failure when target-platform material settings are missing
- PASS for existing Windows shader-backed material loading behavior

- [ ] **Step 5: Commit the cook-path integration**

```bash
git add engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs engine/helengine.core/scene/runtime/RuntimeSceneAssetReferenceResolver.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
git commit -m "feat: cook platform materials during packaging"
```

## Task 7: Full Regression Pass

**Files:**
- Test only

- [ ] **Step 1: Run the metadata, serializer, UI, and packaging suites together**

Run:

```bash
rtk dotnet test engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~AssetImportManagerTests|FullyQualifiedName~MaterialAssetViewTests|FullyQualifiedName~EditorPlatformBuildSelectionModelTests|FullyQualifiedName~EditorPlatformAssetCookServiceTests|FullyQualifiedName~EditorPlatformBuildScenePackagerTests"
```

Expected:
- PASS for all focused multiplatform material coverage

- [ ] **Step 2: Run the full editor test project**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj
```

Expected:
- PASS with no regressions in existing material, properties panel, or build graph tests

- [ ] **Step 3: Run the baseplatform test project**

Run:

```bash
rtk dotnet test engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj
```

Expected:
- PASS with no regressions in builder metadata and request contracts

- [ ] **Step 4: Inspect the git diff for accidental scope creep**

Run:

```bash
rtk git diff --stat
rtk git diff -- engine/helengine.baseplatform engine/helengine.editor engine/helengine.editor.tests
```

Expected:
- only multiplatform material metadata, settings, UI, and cook-path files changed

- [ ] **Step 5: Commit the final regression pass if any cleanup changes were needed**

```bash
git add engine/helengine.baseplatform engine/helengine.editor engine/helengine.editor.tests
git commit -m "test: verify multiplatform material integration"
```
