# Texture Per-Platform Max Resolution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a per-asset, per-platform `MaxResolution` texture processor override that resizes imported texture assets during cache generation and is editable from the asset import settings UI.

**Architecture:** Extend the existing `AssetImportSettings -> Processor.Platforms[platformId]` model with a new `TextureAssetProcessorSettings` branch, then thread that value through binary serialization, asset-id generation, texture import caching, and the editor import-settings UI. Keep the behavior isolated to texture source assets so direct and indirect consumers inherit the same processed cached texture automatically.

**Tech Stack:** C#, xUnit, existing editor asset-import pipeline, binary serializer, editor UI components.

---

### Task 1: Add Texture Processor Settings To The Import Settings Model

**Files:**
- Create: `engine/helengine.editor/managers/asset/TextureAssetProcessorSettings.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetPlatformProcessorSettings.cs`
- Modify: `engine/helengine.editor/serialization/AssetImportSettingsBinarySerializer.cs`
- Test: `engine/helengine.editor.tests/BinarySerializationTests.cs`

- [ ] **Step 1: Write the failing serialization test**

Add this test in `engine/helengine.editor.tests/BinarySerializationTests.cs` near the existing asset-import round-trip coverage:

```csharp
[Fact]
public void AssetImportSettingsBinarySerializer_RoundTripsTextureMaxResolutionPerPlatform() {
    AssetImportSettings settings = new AssetImportSettings();
    settings.Importer.ImporterId = "pfim";
    settings.Importer.SourceChecksum = "checksum";
    settings.Importer.AssetId = "asset-id";
    settings.Processor.Platforms["windows"] = new AssetPlatformProcessorSettings {
        Texture = new TextureAssetProcessorSettings {
            MaxResolution = 512
        }
    };
    settings.Processor.Platforms["android"] = new AssetPlatformProcessorSettings {
        Texture = new TextureAssetProcessorSettings {
            MaxResolution = 128
        }
    };

    using MemoryStream stream = new MemoryStream();
    AssetImportSettingsBinarySerializer.Serialize(stream, settings);
    stream.Position = 0;

    AssetImportSettings deserialized = AssetImportSettingsBinarySerializer.Deserialize(stream);

    Assert.Equal(512, deserialized.Processor.Platforms["windows"].Texture.MaxResolution);
    Assert.Equal(128, deserialized.Processor.Platforms["android"].Texture.MaxResolution);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~AssetImportSettingsBinarySerializer_RoundTripsTextureMaxResolutionPerPlatform
```

Expected: FAIL because `AssetPlatformProcessorSettings` does not expose `Texture` yet and the serializer does not persist the value.

- [ ] **Step 3: Write the minimal model and serializer changes**

Create `engine/helengine.editor/managers/asset/TextureAssetProcessorSettings.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Stores one platform-specific texture processor configuration record for a source asset.
    /// </summary>
    public class TextureAssetProcessorSettings {
        /// <summary>
        /// Gets or sets the maximum allowed width or height in pixels for the processed texture, or zero when uncapped.
        /// </summary>
        public int MaxResolution { get; set; }
    }
}
```

Update `engine/helengine.editor/managers/asset/AssetPlatformProcessorSettings.cs`:

```csharp
public AssetPlatformProcessorSettings() {
    Texture = new TextureAssetProcessorSettings();
    Model = new ModelAssetProcessorSettings();
    Material = new MaterialAssetProcessorSettings();
}

/// <summary>
/// Gets or sets the processor settings that affect texture asset generation.
/// </summary>
public TextureAssetProcessorSettings Texture { get; set; }
```

Update `engine/helengine.editor/serialization/AssetImportSettingsBinarySerializer.cs`:

```csharp
public const byte CurrentVersion = 4;
```

Then insert texture validation and write logic before material serialization:

```csharp
} else if (entry.Value.Texture == null) {
    throw new InvalidOperationException($"Asset import settings must include texture processor settings for platform '{entry.Key}'.");
} else if (entry.Value.Texture.MaxResolution < 0) {
    throw new InvalidOperationException($"Asset import settings cannot contain a negative texture max resolution for platform '{entry.Key}'.");
}

writer.WriteString(entry.Key);
writer.WriteByte(entry.Value.Model.FlipWinding ? (byte)1 : (byte)0);
writer.WriteInt32(entry.Value.Texture.MaxResolution);
writer.WriteString(entry.Value.Material.SchemaId ?? string.Empty);
```

Then insert texture read logic before material schema id:

```csharp
platformSettings.Model.FlipWinding = ReadBooleanByte(reader);
platformSettings.Texture.MaxResolution = reader.ReadInt32();
if (platformSettings.Texture.MaxResolution < 0) {
    throw new InvalidOperationException($"Asset import settings cannot contain a negative texture max resolution for platform '{platformId}'.");
}

platformSettings.Material.SchemaId = reader.ReadString();
```

- [ ] **Step 4: Run the test to verify it passes**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~AssetImportSettingsBinarySerializer_RoundTripsTextureMaxResolutionPerPlatform
```

Expected: PASS.

- [ ] **Step 5: Add a negative-value regression**

Add this test in `engine/helengine.editor.tests/BinarySerializationTests.cs`:

```csharp
[Fact]
public void AssetImportSettingsBinarySerializer_Serialize_WhenTextureMaxResolutionIsNegative_Throws() {
    AssetImportSettings settings = new AssetImportSettings();
    settings.Importer.ImporterId = "pfim";
    settings.Processor.Platforms["windows"] = new AssetPlatformProcessorSettings {
        Texture = new TextureAssetProcessorSettings {
            MaxResolution = -1
        }
    };

    using MemoryStream stream = new MemoryStream();

    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => AssetImportSettingsBinarySerializer.Serialize(stream, settings));
    Assert.Contains("negative texture max resolution", exception.Message);
}
```

- [ ] **Step 6: Run the focused serialization slice**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~AssetImportSettingsBinarySerializer_
```

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
rtk git add engine/helengine.editor/managers/asset/TextureAssetProcessorSettings.cs engine/helengine.editor/managers/asset/AssetPlatformProcessorSettings.cs engine/helengine.editor/serialization/AssetImportSettingsBinarySerializer.cs engine/helengine.editor.tests/BinarySerializationTests.cs
rtk git commit -m "Add texture processor max-resolution settings"
```

### Task 2: Apply Max Resolution During Texture Import And Cache Identity Generation

**Files:**
- Create: `engine/helengine.editor/managers/asset/TextureAssetProcessor.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Modify: `engine/helengine.editor.tests/testing/ConfigurableTextureImporter.cs`
- Test: `engine/helengine.editor.tests/AssetImportManagerTests.cs`

- [ ] **Step 1: Write the failing import-manager tests**

Add these tests in `engine/helengine.editor.tests/AssetImportManagerTests.cs`:

```csharp
[Fact]
public void TryLoadTextureAsset_WhenTextureMaxResolutionIsCapped_DownsizesWhilePreservingAspectRatio() {
    string sourcePath = WriteSourceTexture("checker.tga");
    ContentManager contentManager = new ContentManager(AssetsRootPath);
    AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
    manager.RegisterTextureImporter(new TextureImporterRegistration("pfim", new ConfigurableTextureImporter(1024, 512, new byte[1024 * 512 * 4]), new[] { ".tga" }));
    manager.CurrentPlatformId = "windows";

    AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);
    settings.Importer.ImporterId = "pfim";
    settings.Processor.Platforms["windows"] = new AssetPlatformProcessorSettings {
        Texture = new TextureAssetProcessorSettings {
            MaxResolution = 256
        }
    };
    manager.SaveImportSettings(sourcePath, settings);

    bool loaded = manager.TryLoadTextureAsset(sourcePath, out TextureAsset asset);

    Assert.True(loaded);
    Assert.Equal((ushort)256, asset.Width);
    Assert.Equal((ushort)128, asset.Height);
}

[Fact]
public void TryLoadTextureAsset_WhenTextureMaxResolutionChanges_ReimportsWithANewAssetId() {
    string sourcePath = WriteSourceTexture("checker-id.tga");
    ContentManager contentManager = new ContentManager(AssetsRootPath);
    AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
    manager.RegisterTextureImporter(new TextureImporterRegistration("pfim", new ConfigurableTextureImporter(1024, 512, new byte[1024 * 512 * 4]), new[] { ".tga" }));
    manager.CurrentPlatformId = "windows";

    AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);
    settings.Importer.ImporterId = "pfim";
    settings.Processor.Platforms["windows"] = new AssetPlatformProcessorSettings {
        Texture = new TextureAssetProcessorSettings {
            MaxResolution = 512
        }
    };
    manager.SaveImportSettings(sourcePath, settings);
    Assert.True(manager.TryLoadTextureAsset(sourcePath, out _));
    string firstAssetId = manager.LoadOrCreateImportSettings(sourcePath).Importer.AssetId;

    settings = manager.LoadOrCreateImportSettings(sourcePath);
    settings.Processor.Platforms["windows"].Texture.MaxResolution = 128;
    manager.SaveImportSettings(sourcePath, settings);
    Assert.True(manager.TryLoadTextureAsset(sourcePath, out _));
    string secondAssetId = manager.LoadOrCreateImportSettings(sourcePath).Importer.AssetId;

    Assert.NotEqual(firstAssetId, secondAssetId);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~TryLoadTextureAsset_WhenTextureMaxResolution
```

Expected: FAIL because the import pipeline does not resize textures or encode the cap into the asset id yet.

- [ ] **Step 3: Extend the configurable texture test importer**

Update `engine/helengine.editor.tests/testing/ConfigurableTextureImporter.cs`:

```csharp
readonly byte[] Colors;
readonly ushort Width;
readonly ushort Height;

public ConfigurableTextureImporter(byte[] colors) : this(1, 1, colors) {
}

public ConfigurableTextureImporter(int width, int height, byte[] colors) {
    if (width < 1) {
        throw new ArgumentOutOfRangeException(nameof(width));
    } else if (height < 1) {
        throw new ArgumentOutOfRangeException(nameof(height));
    } else if (colors == null) {
        throw new ArgumentNullException(nameof(colors));
    }

    Width = (ushort)width;
    Height = (ushort)height;
    Colors = colors;
}

public TextureAsset ImportTexture(Stream stream) {
    if (stream == null) {
        throw new ArgumentNullException(nameof(stream));
    }

    return new TextureAsset {
        Width = Width,
        Height = Height,
        Colors = (byte[])Colors.Clone()
    };
}
```

- [ ] **Step 4: Add the texture processor and integrate it into the import manager**

Create `engine/helengine.editor/managers/asset/TextureAssetProcessor.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Applies editor-authored texture processor settings to imported texture assets.
    /// </summary>
    public sealed class TextureAssetProcessor {
        /// <summary>
        /// Applies the supplied settings to the imported texture asset.
        /// </summary>
        /// <param name="asset">Imported texture asset to transform.</param>
        /// <param name="settings">Platform-specific texture processor settings.</param>
        /// <returns>Processed texture asset.</returns>
        public TextureAsset Apply(TextureAsset asset, TextureAssetProcessorSettings settings) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (asset.Width < 1 || asset.Height < 1) {
                throw new InvalidOperationException("Texture assets must have positive dimensions.");
            } else if (settings.MaxResolution < 0) {
                throw new InvalidOperationException("Texture max resolution cannot be negative.");
            }

            if (settings.MaxResolution == 0 || (asset.Width <= settings.MaxResolution && asset.Height <= settings.MaxResolution)) {
                return asset;
            }

            double largestDimension = Math.Max(asset.Width, asset.Height);
            double scale = settings.MaxResolution / largestDimension;
            int newWidth = Math.Max(1, (int)Math.Round(asset.Width * scale));
            int newHeight = Math.Max(1, (int)Math.Round(asset.Height * scale));
            byte[] resizedColors = new byte[newWidth * newHeight * 4];

            for (int y = 0; y < newHeight; y++) {
                int sourceY = Math.Min(asset.Height - 1, (int)Math.Floor(y / scale));
                for (int x = 0; x < newWidth; x++) {
                    int sourceX = Math.Min(asset.Width - 1, (int)Math.Floor(x / scale));
                    int sourceIndex = ((sourceY * asset.Width) + sourceX) * 4;
                    int targetIndex = ((y * newWidth) + x) * 4;
                    Buffer.BlockCopy(asset.Colors, sourceIndex, resizedColors, targetIndex, 4);
                }
            }

            return new TextureAsset {
                Id = asset.Id,
                Width = (ushort)newWidth,
                Height = (ushort)newHeight,
                Colors = resizedColors
            };
        }
    }
}
```

Update `engine/helengine.editor/managers/asset/AssetImportManager.cs`:

- add a `TextureAssetProcessor` field and initialize it in the constructor
- in `TryLoadTextureAsset`, after the importer returns `TextureAsset`, resolve current platform texture settings and run `TextureAssetProcessor.Apply`
- add `ResolveTextureProcessorPlatformId(AssetImportSettings settings)` and `GetCurrentPlatformTextureProcessorSettings(AssetImportSettings settings)` mirroring the model helpers
- update `BuildAssetId` so texture assets use a processor-aware identity:

```csharp
TextureAssetProcessorSettings processorSettings = GetCurrentPlatformTextureProcessorSettings(settings);
string platformId = ResolveTextureProcessorPlatformId(settings);
string identity = string.Concat(
    "texture", "\n",
    sourceChecksum, "\n",
    settings.Importer.ImporterId ?? string.Empty, "\n",
    platformId, "\n",
    processorSettings.MaxResolution.ToString(CultureInfo.InvariantCulture));
byte[] identityBytes = Encoding.UTF8.GetBytes(identity);
byte[] hashBytes = SHA256.HashData(identityBytes);
return Convert.ToHexString(hashBytes).ToLowerInvariant();
```

- [ ] **Step 5: Run the focused import-manager tests**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~TryLoadTextureAsset_WhenTextureMaxResolution
```

Expected: PASS.

- [ ] **Step 6: Run the broader import-manager regression slice**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~AssetImportManagerTests
```

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
rtk git add engine/helengine.editor/managers/asset/TextureAssetProcessor.cs engine/helengine.editor/managers/asset/AssetImportManager.cs engine/helengine.editor.tests/testing/ConfigurableTextureImporter.cs engine/helengine.editor.tests/AssetImportManagerTests.cs
rtk git commit -m "Apply per-platform texture max resolution during import"
```

### Task 3: Add Texture Max Resolution Editing To The Asset Import Settings UI

**Files:**
- Modify: `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`
- Test: `engine/helengine.editor.tests/AssetImportSettingsViewTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionAssetImportSettingsTests.cs`

- [ ] **Step 1: Write the failing UI tests**

Add these tests in `engine/helengine.editor.tests/AssetImportSettingsViewTests.cs`:

```csharp
[Fact]
public void Show_WhenTextureProcessorSettingsExist_UsesTheActivePlatformMaxResolutionValue() {
    AssetImportSettingsView view = new AssetImportSettingsView(CreateFont(), 1);
    AssetImportSettings settings = CreateSettings(false, false);
    settings.Processor.Platforms["windows"].Texture.MaxResolution = 256;

    view.Show(
        ["pfim"],
        settings,
        ["windows", "android"],
        "windows",
        AssetEntryKind.Texture);

    Assert.Equal("256", view.CurrentTextureMaxResolutionText);
    Assert.True(view.IsTextureProcessorVisible);
}

[Fact]
public void Apply_WhenTextureMaxResolutionChanges_RaisesTheUpdatedProcessorSettings() {
    AssetImportSettingsView view = new AssetImportSettingsView(CreateFont(), 1);
    AssetImportSettingsApplyRequest raisedRequest = null;
    view.ApplyRequested += request => raisedRequest = request;

    view.Show(
        ["pfim"],
        CreateSettings(false, false),
        ["windows"],
        "windows",
        AssetEntryKind.Texture);

    InvokePrivate(view, "HandleTextureMaxResolutionTextChanged", "512");
    InvokePrivate(view, "HandleApplyClicked");

    Assert.NotNull(raisedRequest);
    Assert.Equal(512, raisedRequest.ProcessorSettings.Platforms["windows"].Texture.MaxResolution);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~TextureMaxResolution
```

Expected: FAIL because the texture processor controls do not exist yet.

- [ ] **Step 3: Add the texture processor controls and binding**

Update `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`:

- add fields for the texture processor host, label, and text box
- add these public accessors for tests:

```csharp
public bool IsTextureProcessorVisible => CurrentEntryKind == AssetEntryKind.Texture;
public string CurrentTextureMaxResolutionText => TextureMaxResolutionTextBox.Text;
```

- in the initialization path, create a `TextBoxComponent` labeled `Max Resolution`
- in `Show`, when `CurrentEntryKind == AssetEntryKind.Texture`, load the selected platform value from `ResolvePlatformSettings(PendingProcessorSettings, CurrentPlatformId).Texture.MaxResolution`
- add a handler:

```csharp
void HandleTextureMaxResolutionTextChanged(string text) {
    if (!int.TryParse(text, out int maxResolution) || maxResolution < 0) {
        throw new InvalidOperationException("Texture max resolution must be zero or greater.");
    }

    AssetPlatformProcessorSettings platformSettings = GetPendingPlatformSettings(CurrentPlatformId);
    platformSettings.Texture.MaxResolution = maxResolution;
}
```

- include `Texture.MaxResolution` in processor-setting clone and equality helpers so apply-state tracking stays correct

- [ ] **Step 4: Add the failing session-forwarding regression**

Add this test in `engine/helengine.editor.tests/EditorSessionAssetImportSettingsTests.cs`:

```csharp
[Fact]
public void HandleImportSettingsApplyRequested_WhenTexturePlatformSettingsApplied_PersistsAndReloadsTextureMaxResolution() {
    string sourcePath = WriteSourceModel(Path.Combine("Textures", "checker.png"));
    EditorSession session = CreateSession();
    AssetImportManager manager = GetPrivateField<AssetImportManager>(session, "assetImportManager");
    PropertiesPanel panel = GetPrivateField<PropertiesPanel>(session, "propertiesPanel");
    EditorProjectLocalSettingsService localSettingsService = GetPrivateField<EditorProjectLocalSettingsService>(session, "ProjectLocalSettingsService");
    manager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), new[] { ".png" }));
    AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile(
        "checker.png",
        "Textures/checker.png",
        sourcePath,
        ".png",
        AssetEntryKind.Texture);
    AssetProcessorSettings processorSettings = new AssetProcessorSettings();
    processorSettings.Platforms["windows"] = new AssetPlatformProcessorSettings {
        Texture = new TextureAssetProcessorSettings {
            MaxResolution = 256
        }
    };
    processorSettings.Platforms["android"] = new AssetPlatformProcessorSettings {
        Texture = new TextureAssetProcessorSettings {
            MaxResolution = 128
        }
    };
    AssetImportSettingsApplyRequest request = new AssetImportSettingsApplyRequest("test-texture", "windows", processorSettings);

    InvokePrivate(session, "HandleImportSettingsApplyRequested", entry, request);

    AssetImportSettings savedSettings = manager.LoadOrCreateImportSettings(sourcePath);
    AssetImportSettingsView view = GetPrivateField<AssetImportSettingsView>(panel, "importSettingsView");
    Assert.Equal("windows", session.CurrentProjectPlatform);
    Assert.Equal("windows", manager.CurrentPlatformId);
    Assert.Equal("windows", localSettingsService.LoadActivePlatform());
    Assert.Equal("test-texture", savedSettings.Importer.ImporterId);
    Assert.Equal(256, savedSettings.Processor.Platforms["windows"].Texture.MaxResolution);
    Assert.Equal("windows", view.SelectedPlatformId);
    Assert.Equal("256", view.CurrentTextureMaxResolutionText);
}
```

- [ ] **Step 5: Run the UI regression slice**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~AssetImportSettingsViewTests
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorSessionAssetImportSettingsTests
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
rtk git add engine/helengine.editor/components/ui/AssetImportSettingsView.cs engine/helengine.editor.tests/AssetImportSettingsViewTests.cs engine/helengine.editor.tests/EditorSessionAssetImportSettingsTests.cs
rtk git commit -m "Add texture max-resolution import settings UI"
```

### Task 4: Prove Indirect Texture Consumers Reuse The Capped Imported Asset

**Files:**
- Modify: `engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs`

- [ ] **Step 1: Write the failing indirect-consumer regression**

Add this test in `engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs`:

```csharp
[Fact]
public void ResolveTextureImportCache_WhenReferencedTextureHasPerPlatformMaxResolution_UsesTheCappedImportedTextureAsset() {
    string textureRelativePath = "Textures/checker.tga";
    string textureFullPath = Path.Combine(TempProjectRootPath, "assets", textureRelativePath.Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(textureFullPath));
    File.WriteAllBytes(textureFullPath, new byte[] { 1, 2, 3, 4 });

    AssetImportManager assetImportManager = CreateAssetImportManager();
    assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("pfim", new ConfigurableTextureImporter(1024, 512, new byte[1024 * 512 * 4]), new[] { ".tga" }));
    assetImportManager.CurrentPlatformId = "windows";

    AssetImportSettings textureSettings = assetImportManager.LoadOrCreateImportSettings(textureFullPath);
    textureSettings.Importer.ImporterId = "pfim";
    textureSettings.Processor.Platforms["windows"] = new AssetPlatformProcessorSettings {
        Texture = new TextureAssetProcessorSettings {
            MaxResolution = 64
        }
    };
    assetImportManager.SaveImportSettings(textureFullPath, textureSettings);

    Assert.True(assetImportManager.TryLoadTextureAsset(textureFullPath, out TextureAsset importedTexture));
    Assert.Equal((ushort)64, importedTexture.Width);
    Assert.Equal((ushort)32, importedTexture.Height);

    string importedTexturePath = Path.Combine(TempProjectRootPath, "cache", textureSettings.Importer.AssetId);
    ContentManager contentManager = new ContentManager(TempProjectRootPath);
    EditorContentManagerConfiguration.ConfigureEditorContentManager(contentManager);
    EditorSceneAssetReferenceResolver resolver = new EditorSceneAssetReferenceResolver(
        contentManager,
        TempProjectRootPath,
        new EditorFileSystemModelResolver(assetImportManager),
        new EditorFileSystemFontResolver(assetImportManager));

    TextureAsset resolvedTexture = contentManager.Load<TextureAsset>(importedTexturePath, EditorContentProcessorIds.TextureAsset);

    Assert.NotNull(resolver);
    Assert.Equal((ushort)64, resolvedTexture.Width);
    Assert.Equal((ushort)32, resolvedTexture.Height);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~ResolveTextureImportCache_WhenReferencedTextureHasPerPlatformMaxResolution_UsesTheCappedImportedTextureAsset
```

Expected: FAIL until the imported texture cache consistently reflects the new processor state through downstream loading.

- [ ] **Step 3: Patch only the narrow bypass if the test exposes one**

If the test already passes after Tasks 1-3, make no production change here. If it fails because a downstream path reloads raw source data instead of the imported cache, patch only that narrow path with this shape:

```csharp
AssetImportSettings settings = AssetImportSettingsBinarySerializer.Deserialize(stream);
string importedTextureAssetPath = ResolveImportedTextureAssetPath(settings.Importer.AssetId);
TextureAsset textureAsset = ProjectContentManager.Load<TextureAsset>(importedTextureAssetPath, EditorContentProcessorIds.TextureAsset);
```

Do not add material-side or scene-side duplicate max-resolution settings.

- [ ] **Step 4: Run the resolver regression slice**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorSceneAssetReferenceResolverTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk git add engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs
rtk git commit -m "Verify indirect texture consumers inherit max resolution"
```

### Task 5: Final Verification And Cleanup

**Files:**
- Modify: `engine/helengine.editor/managers/asset/TextureAssetProcessor.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Modify: `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Review for codebase conventions**

Check the changed files for:

- substantive XML comments on all new classes, properties, methods, and constructors
- PascalCase private fields
- no local helper functions
- no silent fallback behavior for invalid settings
- one class per file

- [ ] **Step 2: Run the focused verification bundle**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportSettingsBinarySerializer_|FullyQualifiedName~AssetImportManagerTests|FullyQualifiedName~AssetImportSettingsViewTests|FullyQualifiedName~EditorSessionAssetImportSettingsTests|FullyQualifiedName~EditorSceneAssetReferenceResolverTests"
```

Expected: PASS.

- [ ] **Step 3: Run the full editor test project**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj
```

Expected: PASS.

- [ ] **Step 4: Run a compile verification pass**

Run:

```powershell
rtk dotnet build .\engine\helengine.editor.tests\helengine.editor.tests.csproj
```

Expected: PASS.

- [ ] **Step 5: Make the final integration commit**

```powershell
rtk git add engine/helengine.editor/managers/asset/TextureAssetProcessorSettings.cs engine/helengine.editor/managers/asset/TextureAssetProcessor.cs engine/helengine.editor/managers/asset/AssetPlatformProcessorSettings.cs engine/helengine.editor/managers/asset/AssetImportManager.cs engine/helengine.editor/serialization/AssetImportSettingsBinarySerializer.cs engine/helengine.editor/components/ui/AssetImportSettingsView.cs engine/helengine.editor.tests/testing/ConfigurableTextureImporter.cs engine/helengine.editor.tests/BinarySerializationTests.cs engine/helengine.editor.tests/AssetImportManagerTests.cs engine/helengine.editor.tests/AssetImportSettingsViewTests.cs engine/helengine.editor.tests/EditorSessionAssetImportSettingsTests.cs engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs
rtk git commit -m "Add per-platform texture max resolution overrides"
```

- [ ] **Step 6: Record follow-up only if verification exposes a real gap**

Allowed follow-up topics:

- higher-quality resize filters
- texture compression
- mip generation

Do not create follow-up work for behavior already covered by this plan.
