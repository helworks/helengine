# Asset Import Settings Split Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the mixed `AssetImportSettings` sidecar schema with separate texture, model, and material sidecar types while preserving existing importer behavior, per-platform processor behavior, and deterministic asset-id generation.

**Architecture:** Introduce three asset-kind-specific settings models and three dedicated binary serializers, then move the import-manager, material-settings service, editor session, and properties-panel flows to typed sidecars. Texture and model source assets keep the existing `<source>.hasset` convention through `AssetImportManager`, while serialized material assets continue using the same sidecar path via `MaterialAssetSettingsService`, but with a material-specific payload. The old generalized serializer and generalized editor apply payload are removed from active use rather than carried as compatibility shims.

**Tech Stack:** C#, xUnit, existing editor asset-import pipeline, binary asset serializers, editor UI components, project content manager.

---

### Task 1: Add Typed Asset-Import Settings Models And Serializers

**Files:**
- Create: `engine/helengine.editor/managers/asset/TextureAssetImportSettings.cs`
- Create: `engine/helengine.editor/managers/asset/TextureAssetProcessorPlatformSettings.cs`
- Create: `engine/helengine.editor/managers/asset/ModelAssetImportSettings.cs`
- Create: `engine/helengine.editor/managers/asset/ModelAssetProcessorPlatformSettings.cs`
- Create: `engine/helengine.editor/managers/asset/MaterialAssetImportSettings.cs`
- Create: `engine/helengine.editor/managers/asset/MaterialAssetProcessorPlatformSettings.cs`
- Create: `engine/helengine.editor/serialization/TextureAssetImportSettingsBinarySerializer.cs`
- Create: `engine/helengine.editor/serialization/ModelAssetImportSettingsBinarySerializer.cs`
- Create: `engine/helengine.editor/serialization/MaterialAssetImportSettingsBinarySerializer.cs`
- Modify: `engine/helengine.editor/tests/BinarySerializationTests.cs`
- Modify: `engine/helengine.editor/content/EditorContentManagerConfiguration.cs`
- Modify: `engine/helengine.editor/content/EditorContentProcessorIds.cs`

- [ ] **Step 1: Write the failing round-trip tests for each typed serializer**

Add these tests near the existing asset-settings coverage in `engine/helengine.editor.tests/BinarySerializationTests.cs`:

```csharp
[Fact]
public void TextureAssetImportSettingsBinarySerializer_RoundTripsPlatformSettings() {
    TextureAssetImportSettings settings = new TextureAssetImportSettings();
    settings.Importer.ImporterId = "pfim";
    settings.Importer.SourceChecksum = "checksum";
    settings.Importer.AssetId = "texture-id";
    settings.Processor.Platforms["windows"] = new TextureAssetProcessorSettings {
        MaxResolution = 512
    };
    settings.Processor.Platforms["android"] = new TextureAssetProcessorSettings {
        MaxResolution = 128
    };

    using MemoryStream stream = new MemoryStream();
    TextureAssetImportSettingsBinarySerializer.Serialize(stream, settings);
    stream.Position = 0;

    TextureAssetImportSettings deserialized = TextureAssetImportSettingsBinarySerializer.Deserialize(stream);

    Assert.Equal("pfim", deserialized.Importer.ImporterId);
    Assert.Equal(512, deserialized.Processor.Platforms["windows"].MaxResolution);
    Assert.Equal(128, deserialized.Processor.Platforms["android"].MaxResolution);
}

[Fact]
public void ModelAssetImportSettingsBinarySerializer_RoundTripsPlatformSettings() {
    ModelAssetImportSettings settings = new ModelAssetImportSettings();
    settings.Importer.ImporterId = "assimp";
    settings.Importer.SourceChecksum = "checksum";
    settings.Importer.AssetId = "model-id";
    settings.Processor.Platforms["windows"] = new ModelAssetProcessorSettings {
        FlipWinding = true
    };
    settings.Processor.Platforms["ps2"] = new ModelAssetProcessorSettings {
        FlipWinding = false
    };

    using MemoryStream stream = new MemoryStream();
    ModelAssetImportSettingsBinarySerializer.Serialize(stream, settings);
    stream.Position = 0;

    ModelAssetImportSettings deserialized = ModelAssetImportSettingsBinarySerializer.Deserialize(stream);

    Assert.True(deserialized.Processor.Platforms["windows"].FlipWinding);
    Assert.False(deserialized.Processor.Platforms["ps2"].FlipWinding);
}

[Fact]
public void MaterialAssetImportSettingsBinarySerializer_RoundTripsSchemaAndFields() {
    MaterialAssetImportSettings settings = new MaterialAssetImportSettings();
    settings.Importer.ImporterId = "helengine.material";
    settings.Importer.SourceChecksum = string.Empty;
    settings.Importer.AssetId = "Materials/Demo.helmat";
    settings.Processor.Platforms["windows"] = new MaterialAssetProcessorSettings {
        SchemaId = "standard-shader",
        FieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["base-color"] = "#ffffffff",
            ["texture-id"] = "Textures/checker"
        }
    };

    using MemoryStream stream = new MemoryStream();
    MaterialAssetImportSettingsBinarySerializer.Serialize(stream, settings);
    stream.Position = 0;

    MaterialAssetImportSettings deserialized = MaterialAssetImportSettingsBinarySerializer.Deserialize(stream);

    Assert.Equal("standard-shader", deserialized.Processor.Platforms["windows"].SchemaId);
    Assert.Equal("#ffffffff", deserialized.Processor.Platforms["windows"].FieldValues["base-color"]);
}
```

- [ ] **Step 2: Run the serializer tests to verify they fail**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~TextureAssetImportSettingsBinarySerializer_|FullyQualifiedName~ModelAssetImportSettingsBinarySerializer_|FullyQualifiedName~MaterialAssetImportSettingsBinarySerializer_"
```

Expected: FAIL because the typed settings models and serializers do not exist yet.

- [ ] **Step 3: Add the typed settings models**

Create `engine/helengine.editor/managers/asset/TextureAssetImportSettings.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Stores importer metadata and platform-specific texture processor settings for one texture source asset.
    /// </summary>
    public class TextureAssetImportSettings {
        /// <summary>
        /// Initializes nested importer and processor settings containers.
        /// </summary>
        public TextureAssetImportSettings() {
            Importer = new AssetImporterSettings();
            Processor = new TextureAssetProcessorPlatformSettings();
        }

        /// <summary>
        /// Gets or sets importer metadata for the source texture.
        /// </summary>
        public AssetImporterSettings Importer { get; set; }

        /// <summary>
        /// Gets or sets per-platform texture processor settings.
        /// </summary>
        public TextureAssetProcessorPlatformSettings Processor { get; set; }
    }
}
```

Create `engine/helengine.editor/managers/asset/TextureAssetProcessorPlatformSettings.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Stores texture processor settings keyed by platform identifier.
    /// </summary>
    public class TextureAssetProcessorPlatformSettings {
        /// <summary>
        /// Initializes an empty platform map for texture processor settings.
        /// </summary>
        public TextureAssetProcessorPlatformSettings() {
            Platforms = new Dictionary<string, TextureAssetProcessorSettings>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets or sets the platform-specific texture processor settings.
        /// </summary>
        public Dictionary<string, TextureAssetProcessorSettings> Platforms { get; set; }
    }
}
```

Create the model and material equivalents with the same shape, replacing the processor map value type with `ModelAssetProcessorSettings` and `MaterialAssetProcessorSettings`.

- [ ] **Step 4: Add the typed serializers with isolated versions and validation**

Use this structure in `engine/helengine.editor/serialization/TextureAssetImportSettingsBinarySerializer.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Serializes texture asset import settings using the editor binary header format.
    /// </summary>
    public static class TextureAssetImportSettingsBinarySerializer {
        public const EditorBinaryRecordKind RecordKind = EditorBinaryRecordKind.AssetImportSettings;
        public const byte CurrentVersion = 1;
        static readonly EngineBinaryEndianness PayloadEndianness = EngineBinaryEndianness.LittleEndian;

        public static void Serialize(Stream stream, TextureAssetImportSettings settings) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (settings.Importer == null) {
                throw new InvalidOperationException("Texture asset import settings must include importer settings.");
            } else if (settings.Processor == null || settings.Processor.Platforms == null) {
                throw new InvalidOperationException("Texture asset import settings must include processor platform settings.");
            }

            EngineBinaryHeader header = new EngineBinaryHeader(
                PayloadEndianness,
                CurrentVersion,
                EditorAssetBinarySerializer.FormatId,
                (ushort)RecordKind,
                (ushort)AssetImportSettingsBinaryValueKind.TextureAssetImportSettings);
            EngineBinaryHeaderSerializer.Write(stream, header);

            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, PayloadEndianness);
            writer.WriteString(settings.Importer.ImporterId);
            writer.WriteString(settings.Importer.SourceChecksum);
            writer.WriteString(settings.Importer.AssetId);
            writer.WriteInt32(settings.Processor.Platforms.Count);
            foreach (KeyValuePair<string, TextureAssetProcessorSettings> entry in settings.Processor.Platforms) {
                if (string.IsNullOrWhiteSpace(entry.Key)) {
                    throw new InvalidOperationException("Texture asset import settings cannot contain a blank processor platform id.");
                } else if (entry.Value == null) {
                    throw new InvalidOperationException($"Texture asset import settings must include processor settings for platform '{entry.Key}'.");
                } else if (entry.Value.MaxResolution < 0) {
                    throw new InvalidOperationException($"Texture asset import settings cannot contain a negative texture max resolution for platform '{entry.Key}'.");
                }

                writer.WriteString(entry.Key);
                writer.WriteInt32(entry.Value.MaxResolution);
            }
        }
    }
}
```

Mirror that shape for model and material serializers:

- model writes only `FlipWinding`
- material writes only `SchemaId` plus `FieldValues`
- each serializer validates blank platform ids and missing required nested state
- each serializer uses its own `AssetImportSettingsBinaryValueKind` enum value

Update `engine/helengine.editor/serialization/AssetImportSettingsBinaryValueKind.cs`:

```csharp
public enum AssetImportSettingsBinaryValueKind : ushort {
    TextureAssetImportSettings = 1,
    ModelAssetImportSettings = 2,
    MaterialAssetImportSettings = 3
}
```

- [ ] **Step 5: Register typed content processors**

Update `engine/helengine.editor/content/EditorContentProcessorIds.cs`:

```csharp
public const string TextureAssetImportSettings = "editor.texture-asset-import-settings";
public const string ModelAssetImportSettings = "editor.model-asset-import-settings";
public const string MaterialAssetImportSettings = "editor.material-asset-import-settings";
```

Update `engine/helengine.editor/content/EditorContentManagerConfiguration.cs` to register:

```csharp
contentManager.RegisterProcessor(
    EditorContentProcessorIds.TextureAssetImportSettings,
    new BinaryContentProcessor<TextureAssetImportSettings>(TextureAssetImportSettingsBinarySerializer.Deserialize),
    new[] { AssetImportManager.SettingsExtension });
```

Repeat for model and material settings. Do not keep the old generalized settings processor registered for active paths.

- [ ] **Step 6: Add validation regressions**

Add one negative-path test per serializer:

```csharp
[Fact]
public void TextureAssetImportSettingsBinarySerializer_Serialize_WhenPlatformIdIsBlank_Throws() { }

[Fact]
public void ModelAssetImportSettingsBinarySerializer_Serialize_WhenProcessorMapContainsNullEntry_Throws() { }

[Fact]
public void MaterialAssetImportSettingsBinarySerializer_Serialize_WhenFieldValueIsNull_Throws() { }
```

Populate each test with a concrete invalid payload and assert `InvalidOperationException`.

- [ ] **Step 7: Run the serializer slice**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~TextureAssetImportSettingsBinarySerializer_|FullyQualifiedName~ModelAssetImportSettingsBinarySerializer_|FullyQualifiedName~MaterialAssetImportSettingsBinarySerializer_"
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
rtk git add engine/helengine.editor/managers/asset/TextureAssetImportSettings.cs engine/helengine.editor/managers/asset/TextureAssetProcessorPlatformSettings.cs engine/helengine.editor/managers/asset/ModelAssetImportSettings.cs engine/helengine.editor/managers/asset/ModelAssetProcessorPlatformSettings.cs engine/helengine.editor/managers/asset/MaterialAssetImportSettings.cs engine/helengine.editor/managers/asset/MaterialAssetProcessorPlatformSettings.cs engine/helengine.editor/serialization/TextureAssetImportSettingsBinarySerializer.cs engine/helengine.editor/serialization/ModelAssetImportSettingsBinarySerializer.cs engine/helengine.editor/serialization/MaterialAssetImportSettingsBinarySerializer.cs engine/helengine.editor/serialization/AssetImportSettingsBinaryValueKind.cs engine/helengine.editor/content/EditorContentProcessorIds.cs engine/helengine.editor/content/EditorContentManagerConfiguration.cs engine/helengine.editor.tests/BinarySerializationTests.cs
rtk git commit -m "Add typed asset import settings serializers"
```

### Task 2: Split AssetImportManager Into Typed Texture And Model Sidecar Flows

**Files:**
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Modify: `engine/helengine.editor/tests/AssetImportManagerTests.cs`
- Modify: `engine/helengine.editor/tests/AssetImportManagerModelTests.cs`
- Modify: `engine/helengine.editor/tests/testing/ConfigurableTextureImporter.cs`

- [ ] **Step 1: Write failing texture-sidecar manager coverage**

Add these tests to `engine/helengine.editor.tests/AssetImportManagerTests.cs`:

```csharp
[Fact]
public void LoadOrCreateTextureImportSettings_WhenTextureSidecarMissing_ReturnsTypedDefaults() {
    string sourcePath = WriteSourceTexture("typed-defaults.tga");
    AssetImportManager manager = CreateTgaManager();

    TextureAssetImportSettings settings = manager.LoadOrCreateTextureImportSettings(sourcePath);

    Assert.Equal("pfim", settings.Importer.ImporterId);
    Assert.NotNull(settings.Processor);
    Assert.Empty(settings.Processor.Platforms);
}

[Fact]
public void TryLoadTextureAsset_WhenTextureMaxResolutionChanges_ReimportsWithATypedSidecarAssetId() {
    string sourcePath = WriteSourceTexture("typed-asset-id.tga");
    AssetImportManager manager = CreateTgaManager();
    manager.CurrentPlatformId = "windows";

    TextureAssetImportSettings settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
    settings.Processor.Platforms["windows"] = new TextureAssetProcessorSettings {
        MaxResolution = 512
    };
    manager.SaveTextureImportSettings(sourcePath, settings);
    Assert.True(manager.TryLoadTextureAsset(sourcePath, out _));
    string firstAssetId = manager.LoadOrCreateTextureImportSettings(sourcePath).Importer.AssetId;

    settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
    settings.Processor.Platforms["windows"].MaxResolution = 128;
    manager.SaveTextureImportSettings(sourcePath, settings);
    Assert.True(manager.TryLoadTextureAsset(sourcePath, out _));
    string secondAssetId = manager.LoadOrCreateTextureImportSettings(sourcePath).Importer.AssetId;

    Assert.NotEqual(firstAssetId, secondAssetId);
}
```

- [ ] **Step 2: Write failing model-sidecar manager coverage**

Add these tests to `engine/helengine.editor.tests/AssetImportManagerModelTests.cs`:

```csharp
[Fact]
public void LoadOrCreateModelImportSettings_WhenModelSidecarMissing_ReturnsTypedDefaults() {
    string sourcePath = WriteSourceModel("typed-defaults.obj");
    AssetImportManager manager = CreateManager(new TestModelImporter());

    ModelAssetImportSettings settings = manager.LoadOrCreateModelImportSettings(sourcePath);

    Assert.Equal("test-model", settings.Importer.ImporterId);
    Assert.NotNull(settings.Processor);
    Assert.Empty(settings.Processor.Platforms);
}

[Fact]
public void ImportModel_WhenWindowsProcessorSettingsFlipWinding_UsesTypedModelSettings() {
    string sourcePath = WriteSourceModel("typed-flip.obj");
    AssetImportManager manager = CreateManager(new TestModelImporter());
    ModelAssetImportSettings settings = manager.LoadOrCreateModelImportSettings(sourcePath);
    settings.Processor.Platforms["windows"] = new ModelAssetProcessorSettings {
        FlipWinding = true
    };
    manager.SaveModelImportSettings(sourcePath, settings);

    ModelAsset importedAsset = manager.ImportModel(sourcePath);

    Assert.Equal(new ushort[] { 0, 2, 1 }, importedAsset.Indices16);
}
```

- [ ] **Step 3: Run the focused manager tests to verify they fail**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~LoadOrCreateTextureImportSettings_|FullyQualifiedName~TryLoadTextureAsset_WhenTextureMaxResolutionChanges_ReimportsWithATypedSidecarAssetId|FullyQualifiedName~LoadOrCreateModelImportSettings_|FullyQualifiedName~ImportModel_WhenWindowsProcessorSettingsFlipWinding_UsesTypedModelSettings"
```

Expected: FAIL because the manager still exposes only the generalized sidecar model.

- [ ] **Step 4: Add typed load/save and default creation paths in AssetImportManager**

Add these public entry points in `engine/helengine.editor/managers/asset/AssetImportManager.cs`:

```csharp
public TextureAssetImportSettings LoadOrCreateTextureImportSettings(string sourcePath) { }
public void SaveTextureImportSettings(string sourcePath, TextureAssetImportSettings settings) { }
public bool TryLoadOrCreateTextureImportSettings(string sourcePath, out TextureAssetImportSettings settings) { }
public ModelAssetImportSettings LoadOrCreateModelImportSettings(string sourcePath) { }
public void SaveModelImportSettings(string sourcePath, ModelAssetImportSettings settings) { }
public bool TryLoadOrCreateModelImportSettings(string sourcePath, out ModelAssetImportSettings settings) { }
```

Implement these helpers:

```csharp
TextureAssetImportSettings CreateDefaultTextureSettings(string sourcePath) {
    TextureAssetImportSettings settings = new TextureAssetImportSettings();
    settings.Importer.ImporterId = ResolveDefaultImporterId(sourcePath);
    return settings;
}

ModelAssetImportSettings CreateDefaultModelSettings(string sourcePath) {
    ModelAssetImportSettings settings = new ModelAssetImportSettings();
    settings.Importer.ImporterId = ResolveDefaultImporterId(sourcePath);
    return settings;
}
```

Do not keep the generalized `LoadOrCreateImportSettings` path for texture and model assets in active code.

- [ ] **Step 5: Move texture and model import code onto typed settings**

Update `TryLoadTextureAsset`, `ImportTexture`, `ImportTexturesMissingCache`, `TryLoadModelAsset`, `ImportModel`, and `ImportModelsMissingCache` so they load typed settings, update typed checksums, and build ids from only the relevant processor settings.

Use this asset-id shape for textures:

```csharp
string identity = string.Concat(
    "texture", "\n",
    sourceChecksum, "\n",
    settings.Importer.ImporterId ?? string.Empty, "\n",
    platformId, "\n",
    processorSettings.MaxResolution.ToString(CultureInfo.InvariantCulture));
```

Use this asset-id shape for models:

```csharp
string identity = string.Concat(
    "model", "\n",
    sourceChecksum, "\n",
    settings.Importer.ImporterId ?? string.Empty, "\n",
    platformId, "\n",
    (processorSettings.FlipWinding ? "1" : "0"));
```

Delete or inline helpers that exist only to navigate `AssetProcessorSettings` or `AssetPlatformProcessorSettings`.

- [ ] **Step 6: Keep texture processing behavior intact through the typed path**

If `engine/helengine.editor/tests/testing/ConfigurableTextureImporter.cs` needs the larger-size constructor for these tests, keep this shape:

```csharp
public ConfigurableTextureImporter(int width, int height, byte[] colors) { ... }
```

Do not change the resize or importer-selection behavior itself here. Only swap the settings payload type that drives it.

- [ ] **Step 7: Run the manager regression slices**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~AssetImportManagerTests
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~AssetImportManagerModelTests
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
rtk git add engine/helengine.editor/managers/asset/AssetImportManager.cs engine/helengine.editor.tests/AssetImportManagerTests.cs engine/helengine.editor.tests/AssetImportManagerModelTests.cs engine/helengine.editor.tests/testing/ConfigurableTextureImporter.cs
rtk git commit -m "Split texture and model import sidecars"
```

### Task 3: Move Material Sidecars To Typed MaterialAssetImportSettings

**Files:**
- Modify: `engine/helengine.editor/managers/asset/MaterialAssetSettingsService.cs`
- Modify: `engine/helengine.editor/tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Modify: `engine/helengine.editor/tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs`
- Modify: `engine/helengine.editor/tests/components/ui/MaterialAssetViewTests.cs`
- Modify: `engine/helengine.editor/tests/components/ui/MaterialAssetViewPointerInteractionTests.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`

- [ ] **Step 1: Write failing material-settings service coverage**

Add this test near the current material settings coverage in `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`:

```csharp
[Fact]
public void MaterialAssetSettingsService_LoadOrCreate_ReturnsTypedMaterialSettings() {
    string materialPath = WriteMaterialAsset("Materials/Demo.helmat");
    MaterialAssetSettingsService service = new MaterialAssetSettingsService();
    MaterialAsset materialAsset = LoadMaterialAsset(materialPath);

    MaterialAssetImportSettings settings = service.LoadOrCreate(
        materialPath,
        materialAsset,
        ["windows"],
        _ => CreateSelectionModel("windows"));

    Assert.Equal("helengine.material", settings.Importer.ImporterId);
    Assert.NotNull(settings.Processor.Platforms["windows"]);
    Assert.False(string.IsNullOrWhiteSpace(settings.Processor.Platforms["windows"].SchemaId));
}
```

- [ ] **Step 2: Run the material-sidecar test to verify it fails**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~MaterialAssetSettingsService_LoadOrCreate_ReturnsTypedMaterialSettings
```

Expected: FAIL because the service still returns `AssetImportSettings`.

- [ ] **Step 3: Change MaterialAssetSettingsService to use the typed payload end-to-end**

Update signatures in `engine/helengine.editor/managers/asset/MaterialAssetSettingsService.cs`:

```csharp
public MaterialAssetImportSettings LoadOrCreate(...) { }
public void Save(string materialAssetPath, MaterialAssetImportSettings settings) { }
public bool TryLoad(string materialAssetPath, out MaterialAssetImportSettings settings) { }
public bool ApplyPlatformMaterialFields(MaterialAsset materialAsset, MaterialAssetImportSettings settings, string platformId) { }
public bool ApplyPlatformRuntimeFields(MaterialAsset materialAsset, MaterialAssetImportSettings settings, string platformId) { }
```

Convert internal helpers similarly:

```csharp
bool TryLoadSettings(string settingsPath, out MaterialAssetImportSettings settings) { }
MaterialAssetImportSettings CreateDefaultSettings(MaterialAsset materialAsset) { }
bool NormalizeSettings(MaterialAssetImportSettings settings, ...) { }
MaterialAssetProcessorSettings ResolvePlatformSettings(MaterialAssetImportSettings settings, string platformId) { }
```

Use `MaterialAssetImportSettingsBinarySerializer` for disk I/O.

- [ ] **Step 4: Update material consumers to expect the typed settings**

Replace all `AssetImportSettings` material-sidecar loads in:

- `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- `engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs`
- `engine/helengine.editor/components/ui/MaterialAssetView.cs`
- `engine/helengine.editor/tests/...` material helper factories

with `MaterialAssetImportSettings`.

Use this pattern where the code previously deserialized generic settings directly:

```csharp
using FileStream stream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
MaterialAssetImportSettings settings = MaterialAssetImportSettingsBinarySerializer.Deserialize(stream);
```

- [ ] **Step 5: Run the material regression slices**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~MaterialAssetViewTests|FullyQualifiedName~MaterialAssetViewPointerInteractionTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests|FullyQualifiedName~EditorSceneAssetReferenceResolverTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
rtk git add engine/helengine.editor/managers/asset/MaterialAssetSettingsService.cs engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs engine/helengine.editor/components/ui/MaterialAssetView.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs engine/helengine.editor.tests/components/ui/MaterialAssetViewTests.cs engine/helengine.editor.tests/components/ui/MaterialAssetViewPointerInteractionTests.cs
rtk git commit -m "Split material import sidecars"
```

### Task 4: Replace The Generalized Asset Import Settings UI And Session Apply Payload

**Files:**
- Create: `engine/helengine.editor/managers/asset/TextureAssetImportSettingsApplyRequest.cs`
- Create: `engine/helengine.editor/managers/asset/ModelAssetImportSettingsApplyRequest.cs`
- Create: `engine/helengine.editor/managers/asset/MaterialAssetImportSettingsApplyRequest.cs`
- Modify: `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/tests/AssetImportSettingsViewTests.cs`
- Modify: `engine/helengine.editor/tests/EditorSessionAssetImportSettingsTests.cs`

- [ ] **Step 1: Write failing typed UI tests for texture, model, and material**

Add these tests to `engine/helengine.editor.tests/AssetImportSettingsViewTests.cs`:

```csharp
[Fact]
public void Show_WhenTextureSettingsAreProvided_ShowsOnlyTextureProcessorControls() {
    AssetImportSettingsView view = new AssetImportSettingsView(CreateFont(), 1);
    TextureAssetImportSettings settings = new TextureAssetImportSettings();
    settings.Importer.ImporterId = "pfim";
    settings.Processor.Platforms["windows"] = new TextureAssetProcessorSettings {
        MaxResolution = 256
    };

    view.ShowTextureSettings(["pfim"], settings, ["windows"], "windows");

    Assert.True(view.IsTextureProcessorVisible);
    Assert.False(view.IsModelProcessorVisible);
    Assert.Equal("256", view.CurrentTextureMaxResolutionText);
}

[Fact]
public void Show_WhenModelSettingsAreProvided_ShowsOnlyModelProcessorControls() {
    AssetImportSettingsView view = new AssetImportSettingsView(CreateFont(), 1);
    ModelAssetImportSettings settings = new ModelAssetImportSettings();
    settings.Importer.ImporterId = "assimp";
    settings.Processor.Platforms["windows"] = new ModelAssetProcessorSettings {
        FlipWinding = true
    };

    view.ShowModelSettings(["assimp"], settings, ["windows"], "windows");

    Assert.True(view.IsModelProcessorVisible);
    Assert.False(view.IsTextureProcessorVisible);
    Assert.True(view.CurrentFlipWindingValue);
}
```

Add one material-focused assertion that `AssetImportSettingsView` does not expose dead model or texture controls when the entry kind is material.

- [ ] **Step 2: Write failing session-forwarding tests for typed apply requests**

Add this test to `engine/helengine.editor.tests/EditorSessionAssetImportSettingsTests.cs`:

```csharp
[Fact]
public void HandleImportSettingsApplyRequested_WhenTextureSettingsApplied_PersistsTypedTextureSidecar() {
    string sourcePath = WriteSourceTexture(Path.Combine("Textures", "checker.png"));
    EditorSession session = CreateSession();
    AssetImportManager manager = GetPrivateField<AssetImportManager>(session, "assetImportManager");
    manager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), new[] { ".png" }));

    TextureAssetImportSettingsApplyRequest request = new TextureAssetImportSettingsApplyRequest(
        "test-texture",
        "windows",
        new TextureAssetProcessorPlatformSettings {
            Platforms = new Dictionary<string, TextureAssetProcessorSettings>(StringComparer.OrdinalIgnoreCase) {
                ["windows"] = new TextureAssetProcessorSettings { MaxResolution = 256 }
            }
        });

    InvokePrivate(session, "HandleTextureImportSettingsApplyRequested", CreateTextureEntry(sourcePath), request);

    TextureAssetImportSettings saved = manager.LoadOrCreateTextureImportSettings(sourcePath);
    Assert.Equal(256, saved.Processor.Platforms["windows"].MaxResolution);
}
```

- [ ] **Step 3: Run the UI/session slice to verify it fails**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportSettingsViewTests|FullyQualifiedName~EditorSessionAssetImportSettingsTests"
```

Expected: FAIL because the view and session still traffic in generalized processor settings.

- [ ] **Step 4: Split the apply request types**

Create `engine/helengine.editor/managers/asset/TextureAssetImportSettingsApplyRequest.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Carries pending importer and texture processor changes from the properties panel.
    /// </summary>
    public class TextureAssetImportSettingsApplyRequest {
        /// <summary>
        /// Initializes a new request with the pending texture settings.
        /// </summary>
        public TextureAssetImportSettingsApplyRequest(string importerId, string selectedPlatformId, TextureAssetProcessorPlatformSettings processorSettings) {
            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            } else if (string.IsNullOrWhiteSpace(selectedPlatformId)) {
                throw new ArgumentException("Selected platform id must be provided.", nameof(selectedPlatformId));
            } else if (processorSettings == null) {
                throw new ArgumentNullException(nameof(processorSettings));
            }

            ImporterId = importerId;
            SelectedPlatformId = selectedPlatformId;
            ProcessorSettings = processorSettings;
        }

        public string ImporterId { get; }
        public string SelectedPlatformId { get; }
        public TextureAssetProcessorPlatformSettings ProcessorSettings { get; }
    }
}
```

Create matching model and material request types with the corresponding processor container types.

- [ ] **Step 5: Change AssetImportSettingsView to expose typed show/apply paths**

Replace the single generalized `Show(...)` entry point with explicit methods:

```csharp
public void ShowTextureSettings(IReadOnlyList<string> importerIds, TextureAssetImportSettings settings, IReadOnlyList<string> supportedPlatforms, string activePlatformId) { }
public void ShowModelSettings(IReadOnlyList<string> importerIds, ModelAssetImportSettings settings, IReadOnlyList<string> supportedPlatforms, string activePlatformId) { }
public void ShowMaterialSettings(IReadOnlyList<string> importerIds, MaterialAssetImportSettings settings, IReadOnlyList<string> supportedPlatforms, string activePlatformId) { }
```

Internal state should become typed:

```csharp
TextureAssetProcessorPlatformSettings ActiveTextureProcessorSettings;
TextureAssetProcessorPlatformSettings PendingTextureProcessorSettings;
ModelAssetProcessorPlatformSettings ActiveModelProcessorSettings;
ModelAssetProcessorPlatformSettings PendingModelProcessorSettings;
MaterialAssetProcessorPlatformSettings ActiveMaterialProcessorSettings;
MaterialAssetProcessorPlatformSettings PendingMaterialProcessorSettings;
```

Keep the UI behavior:

- texture assets show only texture controls
- model assets show only flip-winding controls
- material assets show only material controls

Do not keep `AssetPlatformProcessorSettings` as a hidden compatibility wrapper in the view.

- [ ] **Step 6: Update PropertiesPanel and EditorSession to route typed apply events**

In `engine/helengine.editor/components/ui/PropertiesPanel.cs`, expose separate events:

```csharp
public event Action<AssetBrowserEntry, TextureAssetImportSettingsApplyRequest> TextureImportSettingsApplyRequested;
public event Action<AssetBrowserEntry, ModelAssetImportSettingsApplyRequest> ModelImportSettingsApplyRequested;
public event Action<AssetBrowserEntry, MaterialAssetImportSettingsApplyRequest> MaterialImportSettingsApplyRequested;
```

In `engine/helengine.editor/EditorSession.cs`, split handling into asset-kind-specific private methods that call:

```csharp
assetImportManager.LoadOrCreateTextureImportSettings(entry.FullPath);
assetImportManager.SaveTextureImportSettings(entry.FullPath, settings);
assetImportManager.LoadOrCreateModelImportSettings(entry.FullPath);
assetImportManager.SaveModelImportSettings(entry.FullPath, settings);
materialAssetSettingsService.LoadOrCreate(entry.FullPath, materialAsset, ...);
materialAssetSettingsService.Save(entry.FullPath, settings);
```

- [ ] **Step 7: Run the UI/session regressions**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportSettingsViewTests|FullyQualifiedName~EditorSessionAssetImportSettingsTests"
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
rtk git add engine/helengine.editor/managers/asset/TextureAssetImportSettingsApplyRequest.cs engine/helengine.editor/managers/asset/ModelAssetImportSettingsApplyRequest.cs engine/helengine.editor/managers/asset/MaterialAssetImportSettingsApplyRequest.cs engine/helengine.editor/components/ui/AssetImportSettingsView.cs engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/AssetImportSettingsViewTests.cs engine/helengine.editor.tests/EditorSessionAssetImportSettingsTests.cs
rtk git commit -m "Split asset import settings UI payloads"
```

### Task 5: Remove The Old Mixed Schema From Active Use And Verify Full Regression Coverage

**Files:**
- Modify: `engine/helengine.editor/managers/asset/AssetImportSettings.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetProcessorSettings.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetPlatformProcessorSettings.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetImportSettingsApplyRequest.cs`
- Modify: `engine/helengine.editor/serialization/AssetImportSettingsBinarySerializer.cs`
- Modify: `engine/helengine.editor/tests/BinarySerializationTests.cs`
- Modify: `engine/helengine.editor/tests/CoreContentManagerTests.cs`

- [ ] **Step 1: Remove the generalized settings classes from active references**

After Tasks 1 through 4 pass, search for the old mixed types:

```powershell
rtk rg -n "AssetImportSettings|AssetProcessorSettings|AssetPlatformProcessorSettings|AssetImportSettingsApplyRequest|AssetImportSettingsBinarySerializer" engine/helengine.editor engine/helengine.editor.tests
```

Expected before cleanup: only legacy-type definitions and tests still reference them.

- [ ] **Step 2: Delete or demote the old generalized path**

Choose one of these cleanup shapes and keep it consistent:

```csharp
[Obsolete("Use TextureAssetImportSettings, ModelAssetImportSettings, or MaterialAssetImportSettings instead.")]
public class AssetImportSettings { }
```

or delete the old files outright if no remaining callers exist.

Do the same for:

- `AssetProcessorSettings`
- `AssetPlatformProcessorSettings`
- `AssetImportSettingsApplyRequest`
- `AssetImportSettingsBinarySerializer`

The preferred outcome is deletion once the repo builds cleanly.

- [ ] **Step 3: Rewrite content-manager smoke tests to assert typed registration**

Update `engine/helengine.editor.tests/CoreContentManagerTests.cs`:

```csharp
Assert.True(contentManager.IsProcessorRegistered(EditorContentProcessorIds.TextureAssetImportSettings));
Assert.True(contentManager.IsProcessorRegistered(EditorContentProcessorIds.ModelAssetImportSettings));
Assert.True(contentManager.IsProcessorRegistered(EditorContentProcessorIds.MaterialAssetImportSettings));
```

Stop asserting registration of the generalized settings processor.

- [ ] **Step 4: Run the focused verification bundle**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~AssetImportManagerTests|FullyQualifiedName~AssetImportManagerModelTests|FullyQualifiedName~AssetImportSettingsViewTests|FullyQualifiedName~EditorSessionAssetImportSettingsTests|FullyQualifiedName~MaterialAssetViewTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests|FullyQualifiedName~EditorSceneAssetReferenceResolverTests|FullyQualifiedName~CoreContentManagerTests"
```

Expected: PASS.

- [ ] **Step 5: Run full project verification**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj
rtk dotnet build .\engine\helengine.editor.tests\helengine.editor.tests.csproj
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
rtk git add engine/helengine.editor/managers/asset/AssetImportSettings.cs engine/helengine.editor/managers/asset/AssetProcessorSettings.cs engine/helengine.editor/managers/asset/AssetPlatformProcessorSettings.cs engine/helengine.editor/managers/asset/AssetImportSettingsApplyRequest.cs engine/helengine.editor/serialization/AssetImportSettingsBinarySerializer.cs engine/helengine.editor.tests/BinarySerializationTests.cs engine/helengine.editor.tests/CoreContentManagerTests.cs
rtk git commit -m "Remove mixed asset import settings schema"
```

### Task 6: Final Conventions Review

**Files:**
- Modify: `engine/helengine.editor/managers/asset/*.cs`
- Modify: `engine/helengine.editor/serialization/*.cs`
- Modify: `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`

- [ ] **Step 1: Review against repository conventions**

Check the changed files for:

- substantive XML comments on every class, constructor, property, and method
- one class per file
- PascalCase private fields
- no tuples
- no local helper functions
- no `Mathf`
- no silent compatibility fallback for old mixed sidecars

- [ ] **Step 2: Sanity-scan for placeholder behavior**

Run:

```powershell
rtk rg -n "TODO|TBD|compat|best effort|fallback" engine/helengine.editor engine/helengine.editor.tests
```

Expected: no new placeholder or compatibility-path text added for this feature.

- [ ] **Step 3: Make the final integration commit**

```powershell
rtk git add engine/helengine.editor engine/helengine.editor.tests
rtk git commit -m "Complete asset import settings split"
```
