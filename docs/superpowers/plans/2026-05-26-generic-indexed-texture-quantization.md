# Generic Indexed Texture Quantization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add one shared editor-side indexed-texture quantization pipeline so any platform that selects `Indexed4` or `Indexed8` can persist an indexing method, quantize real images into a bounded palette, and preserve semi-transparent UI edges more aggressively than opaque color fidelity.

**Architecture:** Keep the feature entirely in the shared editor pipeline. Extend `TextureAssetProcessorSettings` with one generic `IndexingMethodId`, route all persistence and cache identity through that field, and move indexed-color reduction into one dedicated quantizer class that `TextureAssetProcessor` calls only for indexed formats. Expose the setting in `AssetImportSettingsView` only when the active color format is indexed, and default blank legacy indexed settings to `QuantizedIndexed`.

**Tech Stack:** C#, xUnit, helengine editor asset pipeline, existing binary import-settings serializers, existing editor UI controls (`ComboBoxComponent`, `TextComponent`).

---

## File Map

- Create: `engine/helengine.editor/managers/asset/TextureAssetIndexingMethod.cs`
  - Shared editor enum for generic indexing methods. First value: `QuantizedIndexed`.
- Create: `engine/helengine.editor/managers/asset/TextureAssetIndexedQuantizer.cs`
  - Alpha-aware palette builder and pixel remapper for `Indexed4` / `Indexed8`.
- Modify: `engine/helengine.editor/managers/asset/TextureAssetProcessorSettings.cs`
  - Add `IndexingMethodId`, indexed-format helpers, and default-resolution helpers for legacy blank settings.
- Modify: `engine/helengine.editor/serialization/TextureAssetImportSettingsBinarySerializer.cs`
  - Persist `IndexingMethodId`, version the payload, and keep older versions readable.
- Modify: `engine/helengine.editor/managers/asset/TextureAssetProcessor.cs`
  - Route indexed conversion through the new quantizer and stop requiring exact palette fit.
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
  - Include `IndexingMethodId` in texture cache identity and default settings normalization.
- Modify: `engine/helengine.editor/managers/project/EditorPlatformCookWorkItemFactory.cs`
  - Include `indexingMethod` in serialized builder-owned texture settings JSON.
- Modify: `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`
  - Add one indexed-only `Indexing Method` row, clone/compare/apply that state, and default it when indexed formats are chosen.
- Modify: `engine/helengine.editor.tests/serialization/TextureAssetImportSettingsBinarySerializerTests.cs`
  - Add round-trip coverage for `IndexingMethodId`.
- Modify: `engine/helengine.editor.tests/BinarySerializationTests.cs`
  - Keep generic asset import serialization expectations aligned with the new field.
- Modify: `engine/helengine.editor.tests/managers/asset/TextureAssetProcessorTests.cs`
  - Add quantized indexed conversion and alpha-aware edge preservation regressions.
- Modify: `engine/helengine.editor.tests/AssetImportManagerTests.cs`
  - Prove saved indexed settings drive cache identity and real import behavior.
- Create: `engine/helengine.editor.tests/managers/project/EditorPlatformCookWorkItemFactoryTests.cs`
  - Lock the builder-owned work item JSON contract on `indexingMethod`.
- Modify: `engine/helengine.editor.tests/AssetImportSettingsViewTests.cs`
  - Verify indexed-only UI visibility, defaulting, and apply payload propagation.

### Task 1: Settings Contract And Binary Persistence

**Files:**
- Create: `engine/helengine.editor/managers/asset/TextureAssetIndexingMethod.cs`
- Modify: `engine/helengine.editor/managers/asset/TextureAssetProcessorSettings.cs`
- Modify: `engine/helengine.editor/serialization/TextureAssetImportSettingsBinarySerializer.cs`
- Test: `engine/helengine.editor.tests/serialization/TextureAssetImportSettingsBinarySerializerTests.cs`
- Test: `engine/helengine.editor.tests/BinarySerializationTests.cs`

- [ ] **Step 1: Write the failing serializer tests**

```csharp
[Fact]
public void SerializeDeserialize_WhenIndexedTextureSettingsUseQuantizedIndexed_RoundTripsIndexingMethod() {
    TextureAssetImportSettings settings = new TextureAssetImportSettings();
    settings.Importer.ImporterId = "pfim";
    settings.Importer.SourceChecksum = "sha256:test";
    settings.Importer.AssetId = "asset/test";
    settings.Processor.Platforms["ds"] = new TextureAssetProcessorSettings {
        MaxResolution = 256,
        ColorFormat = TextureAssetColorFormat.Indexed8,
        AlphaPrecision = TextureAssetAlphaPrecision.A4,
        IndexingMethodId = TextureAssetIndexingMethod.QuantizedIndexed.ToString()
    };

    using MemoryStream stream = new MemoryStream();
    TextureAssetImportSettingsBinarySerializer.Serialize(stream, settings);
    stream.Position = 0;

    TextureAssetImportSettings roundTripped = TextureAssetImportSettingsBinarySerializer.Deserialize(stream);
    TextureAssetProcessorSettings dsSettings = Assert.Single(roundTripped.Processor.Platforms).Value;

    Assert.Equal(TextureAssetIndexingMethod.QuantizedIndexed.ToString(), dsSettings.IndexingMethodId);
}

[Fact]
public void AssetImportSettingsBinarySerializer_RoundTripsTextureIndexingMethodPerPlatform() {
    AssetImportSettings settings = CreateAssetImportSettings();
    settings.Processor.Platforms["android"].Texture = new TextureAssetProcessorSettings {
        MaxResolution = 256,
        ColorFormat = TextureAssetColorFormat.Indexed8,
        AlphaPrecision = TextureAssetAlphaPrecision.A8,
        IndexingMethodId = TextureAssetIndexingMethod.QuantizedIndexed.ToString()
    };

    using MemoryStream stream = new MemoryStream();
    AssetImportSettingsBinarySerializer.Serialize(stream, settings);
    stream.Position = 0;

    AssetImportSettings deserialized = AssetImportSettingsBinarySerializer.Deserialize(stream);

    Assert.Equal(
        TextureAssetIndexingMethod.QuantizedIndexed.ToString(),
        deserialized.Processor.Platforms["android"].Texture.IndexingMethodId);
}
```

- [ ] **Step 2: Run the targeted serializer tests to verify they fail**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~TextureAssetImportSettingsBinarySerializerTests|FullyQualifiedName~AssetImportSettingsBinarySerializer_RoundTripsTextureIndexingMethodPerPlatform" -v minimal`

Expected: FAIL because `TextureAssetProcessorSettings` and the serializers do not expose `IndexingMethodId` yet.

- [ ] **Step 3: Add the shared indexing-method contract and binary persistence**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Identifies the shared editor-side palette indexing strategy used when one platform selects an indexed texture format.
    /// </summary>
    public enum TextureAssetIndexingMethod : byte {
        /// <summary>
        /// Reduces the source image into the target palette size using alpha-aware color quantization.
        /// </summary>
        QuantizedIndexed = 1
    }
}
```

```csharp
public class TextureAssetProcessorSettings {
    /// <summary>
    /// Gets or sets the generic indexed-texture method identifier used when the selected color format is palette-backed.
    /// </summary>
    public string IndexingMethodId { get; set; } = string.Empty;

    /// <summary>
    /// Returns whether the selected color format is one generic indexed format.
    /// </summary>
    /// <returns><c>true</c> when the selected color format is <c>Indexed4</c> or <c>Indexed8</c>.</returns>
    public bool UsesIndexedColorFormat() {
        if (!TryResolveGenericColorFormat(ColorFormatId, out TextureAssetColorFormat colorFormat)) {
            return false;
        }

        return colorFormat == TextureAssetColorFormat.Indexed4
            || colorFormat == TextureAssetColorFormat.Indexed8;
    }

    /// <summary>
    /// Resolves the effective indexing method for the current texture settings.
    /// </summary>
    /// <returns>The configured indexing method, or the legacy indexed default when none was authored yet.</returns>
    public TextureAssetIndexingMethod ResolveIndexingMethod() {
        if (!UsesIndexedColorFormat()) {
            throw new InvalidOperationException("Indexing methods are only valid for indexed texture formats.");
        } else if (string.IsNullOrWhiteSpace(IndexingMethodId)
            || string.Equals(IndexingMethodId, TextureAssetIndexingMethod.QuantizedIndexed.ToString(), StringComparison.Ordinal)) {
            return TextureAssetIndexingMethod.QuantizedIndexed;
        }

        throw new InvalidOperationException($"Unsupported texture indexing method id '{IndexingMethodId}'.");
    }
}
```

```csharp
public const byte CurrentVersion = 5;

foreach (KeyValuePair<string, TextureAssetProcessorSettings> entry in settings.Processor.Platforms) {
    writer.WriteString(entry.Key);
    writer.WriteInt32(entry.Value.MaxResolution);
    writer.WriteString(entry.Value.ColorFormatId);
    writer.WriteByte((byte)entry.Value.AlphaPrecision);
    writer.WriteString(entry.Value.IndexingMethodId ?? string.Empty);
}

platformSettings.ColorFormatId = header.Version >= 4
    ? reader.ReadString()
    : ReadLegacyTextureAssetColorFormat(reader).ToString();
platformSettings.AlphaPrecision = header.Version >= 3
    ? ReadTextureAssetAlphaPrecision(reader)
    : TextureAssetAlphaPrecision.A8;
platformSettings.IndexingMethodId = header.Version >= 5
    ? reader.ReadString()
    : string.Empty;
```

- [ ] **Step 4: Run the serializer tests to verify they pass**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~TextureAssetImportSettingsBinarySerializerTests|FullyQualifiedName~AssetImportSettingsBinarySerializer_RoundTripsTextureIndexingMethodPerPlatform" -v minimal`

Expected: PASS.

- [ ] **Step 5: Commit the contract and serializer work**

```bash
git add engine/helengine.editor/managers/asset/TextureAssetIndexingMethod.cs engine/helengine.editor/managers/asset/TextureAssetProcessorSettings.cs engine/helengine.editor/serialization/TextureAssetImportSettingsBinarySerializer.cs engine/helengine.editor.tests/serialization/TextureAssetImportSettingsBinarySerializerTests.cs engine/helengine.editor.tests/BinarySerializationTests.cs
git commit -m "feat: persist texture indexing methods"
```

### Task 2: Shared Alpha-Aware Indexed Quantizer

**Files:**
- Create: `engine/helengine.editor/managers/asset/TextureAssetIndexedQuantizer.cs`
- Modify: `engine/helengine.editor/managers/asset/TextureAssetProcessor.cs`
- Test: `engine/helengine.editor.tests/managers/asset/TextureAssetProcessorTests.cs`

- [ ] **Step 1: Write the failing texture-processor tests**

```csharp
[Fact]
public void Apply_WhenIndexed8QuantizedIsRequestedOnMoreThan256Colors_Succeeds() {
    byte[] colors = new byte[17 * 17 * 4];
    for (int pixelIndex = 0; pixelIndex < 289; pixelIndex++) {
        int colorIndex = pixelIndex * 4;
        colors[colorIndex] = (byte)pixelIndex;
        colors[colorIndex + 1] = (byte)(255 - pixelIndex);
        colors[colorIndex + 2] = (byte)((pixelIndex * 37) % 255);
        colors[colorIndex + 3] = 255;
    }

    TextureAsset source = new TextureAsset {
        Id = "ui/logo",
        Width = 17,
        Height = 17,
        ColorFormat = TextureAssetColorFormat.Rgba32,
        AlphaPrecision = TextureAssetAlphaPrecision.A8,
        Colors = colors
    };

    TextureAsset processed = new TextureAssetProcessor().Apply(source, new TextureAssetProcessorSettings {
        ColorFormat = TextureAssetColorFormat.Indexed8,
        AlphaPrecision = TextureAssetAlphaPrecision.A8,
        IndexingMethodId = TextureAssetIndexingMethod.QuantizedIndexed.ToString()
    });

    Assert.Equal(TextureAssetColorFormat.Indexed8, processed.ColorFormat);
    Assert.Equal(256 * 4, processed.PaletteColors.Length);
}

[Fact]
public void Apply_WhenIndexed4QuantizedIsRequestedOnMoreThan16Colors_Succeeds() {
    byte[] colors = new byte[20 * 4];
    for (int pixelIndex = 0; pixelIndex < 20; pixelIndex++) {
        int colorIndex = pixelIndex * 4;
        colors[colorIndex] = (byte)(pixelIndex * 12);
        colors[colorIndex + 1] = (byte)(255 - (pixelIndex * 7));
        colors[colorIndex + 2] = (byte)((pixelIndex * 17) % 255);
        colors[colorIndex + 3] = 255;
    }

    TextureAsset source = new TextureAsset {
        Id = "ui/badge",
        Width = 5,
        Height = 4,
        ColorFormat = TextureAssetColorFormat.Rgba32,
        AlphaPrecision = TextureAssetAlphaPrecision.A8,
        Colors = colors
    };

    TextureAsset processed = new TextureAssetProcessor().Apply(source, new TextureAssetProcessorSettings {
        ColorFormat = TextureAssetColorFormat.Indexed4,
        AlphaPrecision = TextureAssetAlphaPrecision.A8,
        IndexingMethodId = TextureAssetIndexingMethod.QuantizedIndexed.ToString()
    });

    Assert.Equal(TextureAssetColorFormat.Indexed4, processed.ColorFormat);
    Assert.Equal(16 * 4, processed.PaletteColors.Length);
}

[Fact]
public void Apply_WhenIndexed8QuantizedIsRequested_PreservesSemiTransparentEdgePaletteEntries() {
    TextureAsset source = new TextureAsset {
        Id = "ui/antialias",
        Width = 4,
        Height = 2,
        ColorFormat = TextureAssetColorFormat.Rgba32,
        AlphaPrecision = TextureAssetAlphaPrecision.A8,
        Colors = [
            8, 8, 8, 255,
            16, 16, 16, 255,
            255, 255, 255, 96,
            250, 250, 250, 80,
            24, 24, 24, 255,
            32, 32, 32, 255,
            248, 248, 248, 64,
            240, 240, 240, 48
        ]
    };

    TextureAsset processed = new TextureAssetProcessor().Apply(source, new TextureAssetProcessorSettings {
        ColorFormat = TextureAssetColorFormat.Indexed4,
        AlphaPrecision = TextureAssetAlphaPrecision.A8,
        IndexingMethodId = TextureAssetIndexingMethod.QuantizedIndexed.ToString()
    });

    Assert.Contains(processed.PaletteColors.Chunk(4), color => color[3] > 0 && color[3] < 255);
}
```

- [ ] **Step 2: Run the targeted texture-processor tests to verify they fail**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~TextureAssetProcessorTests" -v minimal`

Expected: FAIL because `ConvertToIndexed(...)` still throws once the palette is full.

- [ ] **Step 3: Implement the quantizer and route indexed conversion through it**

```csharp
public sealed class TextureAssetIndexedQuantizer {
    /// <summary>
    /// Converts one RGBA32 texture asset into a palette-backed payload using the requested indexed format.
    /// </summary>
    /// <param name="asset">Texture asset to quantize.</param>
    /// <param name="paletteCapacity">Maximum palette size supported by the target format.</param>
    /// <param name="targetFormat">Indexed texture format to produce.</param>
    /// <param name="alphaPrecision">Alpha precision to store in palette entries.</param>
    /// <returns>Palette-backed texture payload.</returns>
    public TextureAsset Quantize(TextureAsset asset, int paletteCapacity, TextureAssetColorFormat targetFormat, TextureAssetAlphaPrecision alphaPrecision) {
        Dictionary<uint, double> histogram = BuildHistogram(asset, alphaPrecision);
        List<uint> rankedColors = RankHistogram(histogram);
        byte[] palette = BuildPalette(rankedColors, paletteCapacity);
        byte[] indices = BuildIndexPayload(asset, palette, targetFormat, alphaPrecision);

        return new TextureAsset {
            Id = asset.Id,
            RuntimeAssetId = asset.RuntimeAssetId,
            Width = asset.Width,
            Height = asset.Height,
            ColorFormat = targetFormat,
            AlphaPrecision = alphaPrecision,
            Colors = indices,
            PaletteColors = palette
        };
    }

    Dictionary<uint, double> BuildHistogram(TextureAsset asset, TextureAssetAlphaPrecision alphaPrecision) {
        Dictionary<uint, double> histogram = new Dictionary<uint, double>();
        for (int colorIndex = 0; colorIndex < asset.Colors.Length; colorIndex += 4) {
            byte alpha = QuantizeAlpha(asset.Colors[colorIndex + 3], alphaPrecision);
            uint key = PackPaletteKey(asset.Colors[colorIndex], asset.Colors[colorIndex + 1], asset.Colors[colorIndex + 2], alpha);
            double weight = alpha > 0 && alpha < byte.MaxValue ? 8d : 1d;
            histogram[key] = histogram.TryGetValue(key, out double currentWeight)
                ? currentWeight + weight
                : weight;
        }

        return histogram;
    }

    List<uint> RankHistogram(Dictionary<uint, double> histogram) {
        return histogram
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .Select(pair => pair.Key)
            .ToList();
    }

    byte[] BuildPalette(List<uint> rankedColors, int paletteCapacity) {
        byte[] palette = new byte[paletteCapacity * 4];
        int paletteEntries = Math.Min(rankedColors.Count, paletteCapacity);
        for (int paletteIndex = 0; paletteIndex < paletteEntries; paletteIndex++) {
            uint packedColor = rankedColors[paletteIndex];
            int colorIndex = paletteIndex * 4;
            palette[colorIndex] = (byte)(packedColor & 0xFF);
            palette[colorIndex + 1] = (byte)((packedColor >> 8) & 0xFF);
            palette[colorIndex + 2] = (byte)((packedColor >> 16) & 0xFF);
            palette[colorIndex + 3] = (byte)((packedColor >> 24) & 0xFF);
        }

        return palette;
    }

    byte[] BuildIndexPayload(TextureAsset asset, byte[] palette, TextureAssetColorFormat targetFormat, TextureAssetAlphaPrecision alphaPrecision) {
        int pixelCount = asset.Width * asset.Height;
        byte[] indices = targetFormat == TextureAssetColorFormat.Indexed4
            ? new byte[(pixelCount + 1) / 2]
            : new byte[pixelCount];
        int paletteEntries = palette.Length / 4;
        for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++) {
            int sourceIndex = pixelIndex * 4;
            byte alpha = QuantizeAlpha(asset.Colors[sourceIndex + 3], alphaPrecision);
            int paletteIndex = FindClosestPaletteIndex(
                palette,
                paletteEntries,
                asset.Colors[sourceIndex],
                asset.Colors[sourceIndex + 1],
                asset.Colors[sourceIndex + 2],
                alpha);
            WritePackedIndex(indices, pixelIndex, paletteIndex, targetFormat);
        }

        return indices;
    }

    int FindClosestPaletteIndex(byte[] palette, int paletteEntries, byte red, byte green, byte blue, byte alpha) {
        double bestDistance = double.MaxValue;
        int bestIndex = 0;
        for (int paletteIndex = 0; paletteIndex < paletteEntries; paletteIndex++) {
            int colorIndex = paletteIndex * 4;
            double redDistance = red - palette[colorIndex];
            double greenDistance = green - palette[colorIndex + 1];
            double blueDistance = blue - palette[colorIndex + 2];
            double alphaDistance = (alpha - palette[colorIndex + 3]) * 4d;
            double distance = (redDistance * redDistance)
                + (greenDistance * greenDistance)
                + (blueDistance * blueDistance)
                + (alphaDistance * alphaDistance);
            if (distance < bestDistance) {
                bestDistance = distance;
                bestIndex = paletteIndex;
            }
        }

        return bestIndex;
    }

    void WritePackedIndex(byte[] payload, int pixelIndex, int paletteIndex, TextureAssetColorFormat targetFormat) {
        if (targetFormat == TextureAssetColorFormat.Indexed8) {
            payload[pixelIndex] = (byte)paletteIndex;
            return;
        }

        int targetIndex = pixelIndex / 2;
        if ((pixelIndex & 1) == 0) {
            payload[targetIndex] = (byte)((payload[targetIndex] & 0xF0) | (paletteIndex & 0x0F));
        } else {
            payload[targetIndex] = (byte)((payload[targetIndex] & 0x0F) | ((paletteIndex & 0x0F) << 4));
        }
    }

    byte QuantizeAlpha(byte alpha, TextureAssetAlphaPrecision alphaPrecision) {
        if (alphaPrecision == TextureAssetAlphaPrecision.Opaque) {
            return byte.MaxValue;
        } else if (alphaPrecision == TextureAssetAlphaPrecision.Binary) {
            return alpha >= 128 ? byte.MaxValue : (byte)0;
        } else if (alphaPrecision == TextureAssetAlphaPrecision.A4) {
            return (byte)((alpha & 0xF0) | (alpha >> 4));
        }

        return alpha;
    }

    uint PackPaletteKey(byte red, byte green, byte blue, byte alpha) {
        return red
            | ((uint)green << 8)
            | ((uint)blue << 16)
            | ((uint)alpha << 24);
    }
}
```

```csharp
readonly TextureAssetIndexedQuantizer IndexedQuantizer = new TextureAssetIndexedQuantizer();

TextureAsset ConvertColorFormat(TextureAsset asset, TextureAssetProcessorSettings settings) {
    TextureAssetColorFormat targetFormat = settings.ColorFormat;
    TextureAssetAlphaPrecision alphaPrecision = settings.AlphaPrecision;

    if (targetFormat == TextureAssetColorFormat.Rgba32) {
        return ApplyAlphaPrecision(asset, alphaPrecision);
    } else if (targetFormat == TextureAssetColorFormat.Rgba4444) {
        return ConvertToRgba4444(asset, alphaPrecision);
    } else if (targetFormat == TextureAssetColorFormat.Indexed4) {
        return IndexedQuantizer.Quantize(asset, 16, targetFormat, alphaPrecision);
    } else if (targetFormat == TextureAssetColorFormat.Indexed8) {
        return IndexedQuantizer.Quantize(asset, 256, targetFormat, alphaPrecision);
    }

    throw new InvalidOperationException($"Unsupported texture color format '{targetFormat}'.");
}
```

```csharp
public TextureAsset Apply(TextureAsset asset, TextureAssetProcessorSettings settings) {
    TextureAsset processedAsset = asset;
    if (settings.MaxResolution > 0 && (processedAsset.Width > settings.MaxResolution || processedAsset.Height > settings.MaxResolution)) {
        processedAsset = ResizeToMaxResolution(processedAsset, settings.MaxResolution);
    }

    if (processedAsset.ColorFormat == settings.ColorFormat && processedAsset.AlphaPrecision == settings.AlphaPrecision) {
        return processedAsset;
    }

    if (settings.UsesIndexedColorFormat()) {
        settings.ResolveIndexingMethod();
    }

    return ConvertColorFormat(processedAsset, settings);
}
```

- [ ] **Step 4: Run the texture-processor tests to verify they pass**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~TextureAssetProcessorTests" -v minimal`

Expected: PASS.

- [ ] **Step 5: Commit the quantizer implementation**

```bash
git add engine/helengine.editor/managers/asset/TextureAssetIndexedQuantizer.cs engine/helengine.editor/managers/asset/TextureAssetProcessor.cs engine/helengine.editor.tests/managers/asset/TextureAssetProcessorTests.cs
git commit -m "feat: add shared indexed texture quantization"
```

### Task 3: Cache Identity And Builder-Owned Settings Plumbing

**Files:**
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformCookWorkItemFactory.cs`
- Modify: `engine/helengine.editor.tests/AssetImportManagerTests.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorPlatformCookWorkItemFactoryTests.cs`

- [ ] **Step 1: Write the failing cache and work-item tests**

```csharp
// engine/helengine.editor.tests/AssetImportManagerTests.cs
[Fact]
public void TryLoadTextureAsset_WhenLegacyIndexedSettingsAreSaved_UsesQuantizedIndexedIdentity() {
    string sourcePath = WriteSourceTexture("indexed-method-default-cache-id.tga");
    AssetImportManager manager = CreateTgaManager();
    manager.CurrentPlatformId = "ds";

    TextureAssetImportSettings settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
    settings.Processor.Platforms["ds"] = new TextureAssetProcessorSettings {
        MaxResolution = 128,
        ColorFormat = TextureAssetColorFormat.Indexed8,
        AlphaPrecision = TextureAssetAlphaPrecision.A8,
        IndexingMethodId = string.Empty
    };
    manager.SaveTextureImportSettings(sourcePath, settings);
    Assert.True(manager.TryLoadTextureAsset(sourcePath, out _));
    string firstAssetId = manager.LoadOrCreateTextureImportSettings(sourcePath).Importer.AssetId;

    settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
    settings.Processor.Platforms["ds"].IndexingMethodId = TextureAssetIndexingMethod.QuantizedIndexed.ToString();
    manager.SaveTextureImportSettings(sourcePath, settings);
    Assert.True(manager.TryLoadTextureAsset(sourcePath, out _));
    string secondAssetId = manager.LoadOrCreateTextureImportSettings(sourcePath).Importer.AssetId;

    Assert.Equal(firstAssetId, secondAssetId);
}
```

```csharp
// engine/helengine.editor.tests/managers/project/EditorPlatformCookWorkItemFactoryTests.cs
public sealed class EditorPlatformCookWorkItemFactoryTests : IDisposable {
    readonly string ProjectRootPath;

    public EditorPlatformCookWorkItemFactoryTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-platform-cook-work-item-factory-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Images"));
        File.WriteAllBytes(Path.Combine(ProjectRootPath, "assets", "Images", "logo.png"), [0, 1, 2, 3]);
    }

    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    [Fact]
    public void CreateTextureWorkItem_WhenIndexedSettingsAreProvided_SerializesIndexingMethod() {
        PlatformCookWorkItem workItem = EditorPlatformCookWorkItemFactory.CreateTextureWorkItem(
            CreateTexturePlatformDefinition(),
            "ds",
            ProjectRootPath,
            "Images/logo.png",
            "COOKED/I/LOGO.HAS",
            CreateTextureImportSettings(TextureAssetColorFormat.Indexed8, TextureAssetIndexingMethod.QuantizedIndexed.ToString()),
            new AssetFileHasher());

        Assert.Contains("\"indexingMethod\":\"QuantizedIndexed\"", workItem.SerializedPlatformSettings);
    }

    PlatformDefinition CreateTexturePlatformDefinition() {
        return new PlatformDefinition(
            "ds",
            "Nintendo DS",
            Array.Empty<PlatformBuildProfileDefinition>(),
            Array.Empty<PlatformGraphicsProfileDefinition>(),
            Array.Empty<PlatformAssetRequirementDefinition>(),
            Array.Empty<PlatformMaterialSchemaDefinition>(),
            Array.Empty<PlatformComponentSupportRule>(),
            Array.Empty<PlatformCodegenProfileDefinition>(),
            Array.Empty<PlatformStorageProfileDefinition>(),
            Array.Empty<PlatformMediaProfileDefinition>(),
            assetCookCapabilities: [
                new PlatformAssetCookCapabilityDefinition(
                    "texture",
                    "runtime-texture",
                    PlatformAssetCookOwnershipKind.BuilderOwned,
                    "ds-texture",
                    textureFormatCapabilities: new PlatformTextureFormatCapabilityDefinition(
                        [TextureAssetColorFormat.Indexed8.ToString()],
                        [TextureAssetAlphaPrecision.A8],
                        [new PlatformTextureFormatCombinationDefinition(TextureAssetColorFormat.Indexed8.ToString(), TextureAssetAlphaPrecision.A8)]))
            ]);
    }

    TextureAssetImportSettings CreateTextureImportSettings(TextureAssetColorFormat colorFormat, string indexingMethodId) {
        TextureAssetImportSettings settings = new TextureAssetImportSettings();
        settings.Importer.ImporterId = "pfim";
        settings.Importer.AssetId = "images/logo";
        settings.Processor.Platforms["ds"] = new TextureAssetProcessorSettings {
            MaxResolution = 128,
            ColorFormat = colorFormat,
            AlphaPrecision = TextureAssetAlphaPrecision.A8,
            IndexingMethodId = indexingMethodId
        };
        return settings;
    }
}
```

- [ ] **Step 2: Run the targeted cache and work-item tests to verify they fail**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~TryLoadTextureAsset_WhenLegacyIndexedSettingsAreSaved_UsesQuantizedIndexedIdentity|FullyQualifiedName~CreateTextureWorkItem_WhenIndexedSettingsAreProvided_SerializesIndexingMethod" -v minimal`

Expected: FAIL because cache identity and builder-owned JSON currently ignore the indexing method.

- [ ] **Step 3: Include indexing-method state anywhere texture settings become identity or builder settings**

```csharp
string indexingMethodId = processorSettings.UsesIndexedColorFormat()
    ? processorSettings.ResolveIndexingMethod().ToString()
    : string.Empty;

string identity = string.Concat(
    "texture", "\n",
    sourceChecksum, "\n",
    settings.Importer.ImporterId ?? string.Empty, "\n",
    platformId, "\n",
    processorSettings.MaxResolution.ToString(System.Globalization.CultureInfo.InvariantCulture), "\n",
    processorSettings.ColorFormatId ?? string.Empty, "\n",
    ((int)processorSettings.AlphaPrecision).ToString(System.Globalization.CultureInfo.InvariantCulture), "\n",
    indexingMethodId);
```

```csharp
return new TextureAssetProcessorSettings {
    MaxResolution = maxResolution,
    ColorFormatId = colorFormatId,
    AlphaPrecision = alphaPrecision,
    IndexingMethodId = root.TryGetProperty("indexingMethod", out JsonElement indexingMethodElement)
        ? indexingMethodElement.GetString() ?? string.Empty
        : string.Empty
};
```

```csharp
string indexingMethodId = processorSettings.UsesIndexedColorFormat()
    ? processorSettings.ResolveIndexingMethod().ToString()
    : string.Empty;

return JsonSerializer.Serialize(new Dictionary<string, object> {
    ["maxResolution"] = processorSettings.MaxResolution,
    ["colorFormat"] = processorSettings.ColorFormatId,
    ["alphaPrecision"] = processorSettings.AlphaPrecision.ToString(),
    ["indexingMethod"] = indexingMethodId
});
```

- [ ] **Step 4: Run the cache and work-item tests to verify they pass**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~TryLoadTextureAsset_WhenLegacyIndexedSettingsAreSaved_UsesQuantizedIndexedIdentity|FullyQualifiedName~CreateTextureWorkItem_WhenIndexedSettingsAreProvided_SerializesIndexingMethod" -v minimal`

Expected: PASS.

- [ ] **Step 5: Commit the identity and work-item changes**

```bash
git add engine/helengine.editor/managers/asset/AssetImportManager.cs engine/helengine.editor/managers/project/EditorPlatformCookWorkItemFactory.cs engine/helengine.editor.tests/AssetImportManagerTests.cs engine/helengine.editor.tests/managers/project/EditorPlatformCookWorkItemFactoryTests.cs
git commit -m "feat: include texture indexing method in import identities"
```

### Task 4: Indexed-Only Import Settings UI

**Files:**
- Modify: `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`
- Modify: `engine/helengine.editor.tests/AssetImportSettingsViewTests.cs`

- [ ] **Step 1: Write the failing UI tests**

```csharp
[Fact]
public void Show_WhenTextureFormatIsIndexed_ShowsIndexingMethodControl() {
    AssetImportSettingsView view = new AssetImportSettingsView(CreateFont(), 1);
    AssetProcessorSettings settings = new AssetProcessorSettings();
    settings.Platforms["ds"] = new AssetPlatformProcessorSettings {
        Texture = new TextureAssetProcessorSettings {
            MaxResolution = 256,
            ColorFormat = TextureAssetColorFormat.Indexed8,
            AlphaPrecision = TextureAssetAlphaPrecision.A8,
            IndexingMethodId = TextureAssetIndexingMethod.QuantizedIndexed.ToString()
        }
    };

    view.Show(["pfim"], "pfim", settings, ["ds"], "ds", AssetEntryKind.Image);

    Assert.True(GetPrivateField<EditorEntity>(view, "TextureIndexingMethodLabelHost").Enabled);
    Assert.Equal(TextureAssetIndexingMethod.QuantizedIndexed.ToString(), view.CurrentTextureIndexingMethodId);
}

[Fact]
public void Show_WhenTextureFormatIsNotIndexed_HidesIndexingMethodControl() {
    AssetImportSettingsView view = new AssetImportSettingsView(CreateFont(), 1);
    AssetProcessorSettings settings = new AssetProcessorSettings();
    settings.Platforms["windows"] = new AssetPlatformProcessorSettings {
        Texture = new TextureAssetProcessorSettings {
            MaxResolution = 256,
            ColorFormat = TextureAssetColorFormat.Rgba32,
            AlphaPrecision = TextureAssetAlphaPrecision.A8
        }
    };

    view.Show(["pfim"], "pfim", settings, ["windows"], "windows", AssetEntryKind.Image);

    Assert.False(GetPrivateField<EditorEntity>(view, "TextureIndexingMethodLabelHost").Enabled);
}

[Fact]
public void HandleTextureColorFormatChanged_WhenIndexedFormatIsSelected_DefaultsQuantizedIndexed() {
    AssetImportSettingsView view = new AssetImportSettingsView(CreateFont(), 1);
    view.Show(["pfim"], "pfim", CreateSettings(false, false), ["ds"], "ds", AssetEntryKind.Image);

    InvokePrivate(view, "HandleTextureColorFormatChanged", 0, TextureAssetColorFormat.Indexed8.ToString());

    Assert.Equal(TextureAssetIndexingMethod.QuantizedIndexed.ToString(), view.CurrentTextureIndexingMethodId);
}
```

- [ ] **Step 2: Run the targeted UI tests to verify they fail**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~AssetImportSettingsViewTests" -v minimal`

Expected: FAIL because the view has no indexing-method controls or state yet.

- [ ] **Step 3: Add the indexed-only UI row and wire it into pending settings**

```csharp
const string TextureIndexingMethodLabel = "Indexing";

readonly List<string> TextureIndexingMethodValues;
readonly EditorEntity TextureIndexingMethodLabelHost;
readonly TextComponent TextureIndexingMethodLabelText;
readonly EditorEntity TextureIndexingMethodComboBoxHost;
readonly ComboBoxComponent TextureIndexingMethodComboBox;

public string CurrentTextureIndexingMethodId => GetPendingPlatformSettings(CurrentPlatformId).Texture.IndexingMethodId;
```

```csharp
void HandleTextureIndexingMethodChanged(int selectedIndex, string selectedValue) {
    if (IsUpdatingTextureControls) {
        return;
    } else if (string.IsNullOrWhiteSpace(selectedValue)) {
        throw new InvalidOperationException("Texture indexing method selection was not provided.");
    }

    AssetPlatformProcessorSettings platformSettings = GetPendingPlatformSettings(CurrentPlatformId);
    platformSettings.Texture.IndexingMethodId = selectedValue;
    UpdateStatusText();
    SyncTextureProcessorControlsFromPendingSettings();
}
```

```csharp
void SyncTextureProcessorControlsFromPendingSettings() {
    TextureAssetProcessorSettings textureSettings = GetPendingPlatformSettings(CurrentPlatformId).Texture;
    RepairTextureFormatSelection(textureSettings);
    if (textureSettings.UsesIndexedColorFormat() && string.IsNullOrWhiteSpace(textureSettings.IndexingMethodId)) {
        textureSettings.IndexingMethodId = TextureAssetIndexingMethod.QuantizedIndexed.ToString();
    }

    IsUpdatingTextureControls = true;
    TextureIndexingMethodComboBox.SetItems(TextureIndexingMethodValues, GetTextureIndexingMethodIndex(textureSettings.IndexingMethodId));
    TextureIndexingMethodLabelHost.Enabled = textureSettings.UsesIndexedColorFormat();
    TextureIndexingMethodComboBoxHost.Enabled = textureSettings.UsesIndexedColorFormat();
    IsUpdatingTextureControls = false;
}

int GetTextureIndexingMethodIndex(string indexingMethodId) {
    string resolvedValue = string.IsNullOrWhiteSpace(indexingMethodId)
        ? TextureAssetIndexingMethod.QuantizedIndexed.ToString()
        : indexingMethodId;
    for (int i = 0; i < TextureIndexingMethodValues.Count; i++) {
        if (string.Equals(TextureIndexingMethodValues[i], resolvedValue, StringComparison.Ordinal)) {
            return i;
        }
    }

    throw new InvalidOperationException($"Unsupported texture indexing method '{resolvedValue}'.");
}
```

```csharp
TextureAssetProcessorSettings CloneTextureProcessorSettings(TextureAssetProcessorSettings textureSettings) {
    TextureAssetProcessorSettings clone = new TextureAssetProcessorSettings();
    if (textureSettings == null) {
        return clone;
    }

    clone.MaxResolution = textureSettings.MaxResolution;
    clone.ColorFormatId = textureSettings.ColorFormatId;
    clone.AlphaPrecision = textureSettings.AlphaPrecision;
    clone.IndexingMethodId = textureSettings.IndexingMethodId;
    return clone;
}
```

- [ ] **Step 4: Run the UI tests to verify they pass**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~AssetImportSettingsViewTests" -v minimal`

Expected: PASS.

- [ ] **Step 5: Commit the UI changes**

```bash
git add engine/helengine.editor/components/ui/AssetImportSettingsView.cs engine/helengine.editor.tests/AssetImportSettingsViewTests.cs
git commit -m "feat: expose texture indexing methods in import settings"
```

### Task 5: Import Workflow Regression And Final Verification

**Files:**
- Modify: `engine/helengine.editor.tests/AssetImportManagerTests.cs`
- Modify: `engine/helengine.editor.tests/managers/asset/TextureAssetProcessorTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/TextureAssetImportSettingsBinarySerializerTests.cs`
- Modify: `engine/helengine.editor.tests/AssetImportSettingsViewTests.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorPlatformCookWorkItemFactoryTests.cs`

- [ ] **Step 1: Add the end-to-end import regression for saved indexed settings**

```csharp
[Fact]
public void TryLoadTextureAsset_WhenIndexed8QuantizedSettingsAreSaved_ImportsLargePaletteSource() {
    string sourcePath = WriteSourceTexture("quantized-indexed-import.tga");
    ContentManager contentManager = new ContentManager(AssetsRootPath);
    AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
    byte[] colors = new byte[17 * 17 * 4];
    for (int pixelIndex = 0; pixelIndex < 289; pixelIndex++) {
        int colorIndex = pixelIndex * 4;
        colors[colorIndex] = (byte)pixelIndex;
        colors[colorIndex + 1] = (byte)(255 - pixelIndex);
        colors[colorIndex + 2] = (byte)((pixelIndex * 37) % 255);
        colors[colorIndex + 3] = 255;
    }

    manager.RegisterTextureImporter(new TextureImporterRegistration("pfim", new ConfigurableTextureImporter(17, 17, colors), new[] { ".tga" }));
    manager.CurrentPlatformId = "ds";

    TextureAssetImportSettings settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
    settings.Importer.ImporterId = "pfim";
    settings.Processor.Platforms["ds"] = new TextureAssetProcessorSettings {
        MaxResolution = 0,
        ColorFormat = TextureAssetColorFormat.Indexed8,
        AlphaPrecision = TextureAssetAlphaPrecision.A8,
        IndexingMethodId = TextureAssetIndexingMethod.QuantizedIndexed.ToString()
    };
    manager.SaveTextureImportSettings(sourcePath, settings);

    bool loaded = manager.TryLoadTextureAsset(sourcePath, out TextureAsset asset);

    Assert.True(loaded);
    Assert.Equal(TextureAssetColorFormat.Indexed8, asset.ColorFormat);
    Assert.Equal(256 * 4, asset.PaletteColors.Length);
}
```

- [ ] **Step 2: Run the full targeted editor test suite for this feature**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~TextureAssetProcessorTests|FullyQualifiedName~TextureAssetImportSettingsBinarySerializerTests|FullyQualifiedName~AssetImportSettingsViewTests|FullyQualifiedName~AssetImportManagerTests|FullyQualifiedName~EditorPlatformCookWorkItemFactoryTests" -v minimal`

Expected: PASS.

- [ ] **Step 3: Run the smallest broader regression pass that covers shared serialization and import behavior**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~AssetImportManagerTests" -v minimal`

Expected: PASS.

- [ ] **Step 4: Commit the final workflow verification**

```bash
git add engine/helengine.editor.tests/AssetImportManagerTests.cs engine/helengine.editor.tests/managers/asset/TextureAssetProcessorTests.cs engine/helengine.editor.tests/serialization/TextureAssetImportSettingsBinarySerializerTests.cs engine/helengine.editor.tests/AssetImportSettingsViewTests.cs engine/helengine.editor.tests/managers/project/EditorPlatformCookWorkItemFactoryTests.cs
git commit -m "test: cover indexed texture quantization workflow"
```

- [ ] **Step 5: Record the final verification commands in the branch notes or PR description**

```text
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~TextureAssetProcessorTests|FullyQualifiedName~TextureAssetImportSettingsBinarySerializerTests|FullyQualifiedName~AssetImportSettingsViewTests|FullyQualifiedName~AssetImportManagerTests|FullyQualifiedName~EditorPlatformCookWorkItemFactoryTests" -v minimal
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~AssetImportManagerTests" -v minimal
```
