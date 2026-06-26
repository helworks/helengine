using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies editor-side project-cache generation for platform-specific font variants.
/// </summary>
public sealed class EditorPlatformFontVariantCacheServiceTests : IDisposable {
    /// <summary>
    /// Temporary project root used for each isolated cache-variant test.
    /// </summary>
    readonly string ProjectRootPath;

    /// <summary>
    /// Temporary assets root that hosts authored font source files.
    /// </summary>
    readonly string AssetsRootPath;

    /// <summary>
    /// Temporary cache root used for generated platform font variants.
    /// </summary>
    readonly string CacheRootPath;

    /// <summary>
    /// Initializes one isolated project workspace for platform font cache tests.
    /// </summary>
    public EditorPlatformFontVariantCacheServiceTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-platform-font-variant-cache-tests", Guid.NewGuid().ToString("N"));
        AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
        CacheRootPath = Path.Combine(ProjectRootPath, "cache");
        Directory.CreateDirectory(AssetsRootPath);
        Directory.CreateDirectory(CacheRootPath);
    }

    /// <summary>
    /// Deletes the isolated project workspace after each test.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    /// <summary>
    /// Ensures the first platform-font request generates one cached font asset plus one cached atlas texture asset.
    /// </summary>
    [Fact]
    public void ResolveVariant_WhenVariantIsMissing_WritesCachedFontAndAtlasAssets() {
        string sourcePath = WriteSourceFont("Fonts/DemoTitle.ttf");
        AssetImportManager manager = CreateFontManager(new ConfigurableFontImporter(256, 128, new byte[256 * 128 * 4]));
        EditorPlatformFontVariantCacheService service = new EditorPlatformFontVariantCacheService(manager);

        EditorPlatformFontVariantCacheResult result = service.ResolveVariant(sourcePath, "gamecube");

        Assert.False(result.IsCacheHit);
        Assert.True(File.Exists(result.CachedFontAssetPath));
        Assert.True(File.Exists(result.CachedAtlasTextureAssetPath));

        using FileStream fontStream = File.OpenRead(result.CachedFontAssetPath);
        FontAsset cachedFontAsset = helengine.files.FontAssetBinarySerializer.Deserialize(fontStream);
        Assert.Null(cachedFontAsset.SourceTextureAsset);
        Assert.Equal(256, cachedFontAsset.AtlasWidth);
        Assert.Equal(128, cachedFontAsset.AtlasHeight);

        using FileStream atlasStream = File.OpenRead(result.CachedAtlasTextureAssetPath);
        TextureAsset cachedAtlasTexture = Assert.IsType<TextureAsset>(helengine.files.AssetSerializer.Deserialize(atlasStream));
        Assert.Equal((ushort)256, cachedAtlasTexture.Width);
        Assert.Equal((ushort)128, cachedAtlasTexture.Height);
        Assert.Equal(256 * 128 * 4, cachedAtlasTexture.Colors.Length);
    }

    /// <summary>
    /// Ensures repeated platform-font requests with unchanged inputs reuse the existing cached variant instead of regenerating it.
    /// </summary>
    [Fact]
    public void ResolveVariant_WhenInputsAreUnchanged_ReusesCachedVariant() {
        string sourcePath = WriteSourceFont("Fonts/DemoBody.ttf");
        AssetImportManager manager = CreateFontManager(new TestFontImporter());
        EditorPlatformFontVariantCacheService service = new EditorPlatformFontVariantCacheService(manager);

        EditorPlatformFontVariantCacheResult firstResult = service.ResolveVariant(sourcePath, "gamecube");
        EditorPlatformFontVariantCacheResult secondResult = service.ResolveVariant(sourcePath, "gamecube");

        Assert.False(firstResult.IsCacheHit);
        Assert.True(secondResult.IsCacheHit);
        Assert.Equal(firstResult.CachedFontAssetPath, secondResult.CachedFontAssetPath);
        Assert.Equal(firstResult.CachedAtlasTextureAssetPath, secondResult.CachedAtlasTextureAssetPath);
    }

    /// <summary>
    /// Ensures changing per-platform texture settings invalidates the cached font variant and regenerates the atlas outputs.
    /// </summary>
    [Fact]
    public void ResolveVariant_WhenPlatformTextureSettingsChange_RegeneratesCachedVariant() {
        string sourcePath = WriteSourceFont("Fonts/DemoSubtitle.ttf");
        AssetImportManager manager = CreateFontManager(new ConfigurableFontImporter(256, 128, new byte[256 * 128 * 4]));
        ConfigureFontTextureSettings(manager, sourcePath, "gamecube", 0, TextureAssetColorFormat.Rgba32);
        EditorPlatformFontVariantCacheService service = new EditorPlatformFontVariantCacheService(manager);

        EditorPlatformFontVariantCacheResult firstResult = service.ResolveVariant(sourcePath, "gamecube");
        ConfigureFontTextureSettings(manager, sourcePath, "gamecube", 128, TextureAssetColorFormat.Rgba32);

        EditorPlatformFontVariantCacheResult secondResult = service.ResolveVariant(sourcePath, "gamecube");

        Assert.False(firstResult.IsCacheHit);
        Assert.False(secondResult.IsCacheHit);
        Assert.NotEqual(firstResult.CachedFontAssetPath, secondResult.CachedFontAssetPath);
        Assert.NotEqual(firstResult.CachedAtlasTextureAssetPath, secondResult.CachedAtlasTextureAssetPath);

        using FileStream fontStream = File.OpenRead(secondResult.CachedFontAssetPath);
        FontAsset cachedFontAsset = helengine.files.FontAssetBinarySerializer.Deserialize(fontStream);
        Assert.Equal(128, cachedFontAsset.AtlasWidth);
        Assert.Equal(64, cachedFontAsset.AtlasHeight);

        using FileStream atlasStream = File.OpenRead(secondResult.CachedAtlasTextureAssetPath);
        TextureAsset cachedAtlasTexture = Assert.IsType<TextureAsset>(helengine.files.AssetSerializer.Deserialize(atlasStream));
        Assert.Equal((ushort)128, cachedAtlasTexture.Width);
        Assert.Equal((ushort)64, cachedAtlasTexture.Height);
    }

    /// <summary>
    /// Ensures changing per-platform font settings invalidates the cached font variant and regenerates the cached outputs.
    /// </summary>
    [Fact]
    public void ResolveVariant_WhenPlatformFontSettingsChange_RegeneratesCachedVariant() {
        string sourcePath = WriteSourceFont("Fonts/DemoCaption.ttf");
        AssetImportManager manager = CreateFontManager(new ConfigurableFontImporter(256, 128, new byte[256 * 128 * 4]));
        ConfigureFontSettings(manager, sourcePath, "gamecube", 32);
        EditorPlatformFontVariantCacheService service = new EditorPlatformFontVariantCacheService(manager);

        EditorPlatformFontVariantCacheResult firstResult = service.ResolveVariant(sourcePath, "gamecube");
        ConfigureFontSettings(manager, sourcePath, "gamecube", 12);

        EditorPlatformFontVariantCacheResult secondResult = service.ResolveVariant(sourcePath, "gamecube");

        Assert.False(firstResult.IsCacheHit);
        Assert.False(secondResult.IsCacheHit);
        Assert.NotEqual(firstResult.CachedFontAssetPath, secondResult.CachedFontAssetPath);
    }

    /// <summary>
    /// Creates one font-enabled asset import manager for the current temporary project.
    /// </summary>
    /// <param name="fontImporter">Deterministic font importer used by the test case.</param>
    /// <returns>Configured asset import manager instance.</returns>
    AssetImportManager CreateFontManager(IFontImporter fontImporter) {
        if (fontImporter == null) {
            throw new ArgumentNullException(nameof(fontImporter));
        }

        ContentManager contentManager = new ContentManager(AssetsRootPath);
        AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
        manager.RegisterFontImporter(new FontImporterRegistration("test-font", fontImporter, [".ttf"]));
        return manager;
    }

    /// <summary>
    /// Writes one minimal authored font source file beneath the temporary assets root.
    /// </summary>
    /// <param name="relativePath">Project-relative font source path to create.</param>
    /// <returns>Absolute source path to the authored font file.</returns>
    string WriteSourceFont(string relativePath) {
        if (string.IsNullOrWhiteSpace(relativePath)) {
            throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
        }

        string sourcePath = Path.Combine(AssetsRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath) ?? throw new InvalidOperationException("Source font directory path could not be resolved."));
        File.WriteAllBytes(sourcePath, [0x01, 0x02, 0x03, 0x04]);
        return sourcePath;
    }

    /// <summary>
    /// Writes one per-platform font texture-settings override into the authored import settings sidecar.
    /// </summary>
    /// <param name="manager">Asset import manager that owns the source font settings.</param>
    /// <param name="sourcePath">Absolute source font path.</param>
    /// <param name="platformId">Target platform identifier whose settings should be changed.</param>
    /// <param name="maxResolution">Maximum processed atlas resolution.</param>
    /// <param name="colorFormat">Shared generic texture format used for the processed atlas.</param>
    void ConfigureFontTextureSettings(AssetImportManager manager, string sourcePath, string platformId, int maxResolution, TextureAssetColorFormat colorFormat) {
        if (manager == null) {
            throw new ArgumentNullException(nameof(manager));
        } else if (string.IsNullOrWhiteSpace(sourcePath)) {
            throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
        } else if (string.IsNullOrWhiteSpace(platformId)) {
            throw new ArgumentException("Platform id must be provided.", nameof(platformId));
        }

        AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);
        settings.Processor.Platforms[platformId] = new AssetPlatformProcessorSettings {
            Texture = new TextureAssetProcessorSettings {
                MaxResolution = maxResolution,
                ColorFormat = colorFormat,
                AlphaPrecision = TextureAssetAlphaPrecision.A8
            },
            Model = new ModelAssetProcessorSettings(),
            Material = new MaterialAssetProcessorSettings()
        };
        manager.SaveImportSettings(sourcePath, settings);
    }

    /// <summary>
    /// Writes one per-platform font rasterization override into the authored import settings sidecar.
    /// </summary>
    /// <param name="manager">Asset import manager that owns the source font settings.</param>
    /// <param name="sourcePath">Absolute source font path.</param>
    /// <param name="platformId">Target platform identifier whose settings should be changed.</param>
    /// <param name="pixelSize">Requested font pixel size.</param>
    void ConfigureFontSettings(AssetImportManager manager, string sourcePath, string platformId, int pixelSize) {
        if (manager == null) {
            throw new ArgumentNullException(nameof(manager));
        } else if (string.IsNullOrWhiteSpace(sourcePath)) {
            throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
        } else if (string.IsNullOrWhiteSpace(platformId)) {
            throw new ArgumentException("Platform id must be provided.", nameof(platformId));
        } else if (pixelSize < 1) {
            throw new ArgumentOutOfRangeException(nameof(pixelSize));
        }

        AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);
        if (!settings.Processor.Platforms.TryGetValue(platformId, out AssetPlatformProcessorSettings platformSettings) || platformSettings == null) {
            platformSettings = new AssetPlatformProcessorSettings();
            settings.Processor.Platforms[platformId] = platformSettings;
        }

        AssetPlatformSettingsSectionRegistry.Shared.SetSection(platformSettings, "font", new FontAssetProcessorSettings {
            PixelSize = pixelSize
        });
        manager.SaveImportSettings(sourcePath, settings);
    }
}
