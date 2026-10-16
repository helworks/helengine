using helengine.editor;

namespace helengine.editor.windows.tests.content.font;

/// <summary>
/// Verifies the GDI-backed font importer can produce runtime font assets from raw source font streams.
/// </summary>
public sealed class GdiFontImporterTests {
    /// <summary>
    /// Ensures the importer can read one raw TrueType font stream and emit a runtime font asset with a generated atlas texture.
    /// </summary>
    [Fact]
    public void ImportFont_WhenUsingVendorTrueTypeSource_ProducesRuntimeFontAsset() {
        string repositoryRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string sourceFontPath = Path.Combine(repositoryRootPath, "vendor", "bepuphysics2", "Demos", "Content", "Carlito-Regular.ttf");

        using FileStream stream = new FileStream(sourceFontPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        FontAsset fontAsset = new GdiFontImporter().ImportFont(stream, new FontAssetProcessorSettings {
            PixelSize = 32
        });

        Assert.NotNull(fontAsset);
        Assert.NotNull(fontAsset.SourceTextureAsset);
        Assert.NotEmpty(fontAsset.Characters);
        Assert.True(fontAsset.AtlasWidth > 0);
        Assert.True(fontAsset.AtlasHeight > 0);
    }

    /// <summary>
    /// Ensures the importer retains the source font bytes long enough for GDI to rasterize the requested family.
    /// </summary>
    [Fact]
    public void ImportFont_WhenUsingVendorTrueTypeSource_PreservesSourceFamilyName() {
        string repositoryRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string sourceFontPath = Path.Combine(repositoryRootPath, "vendor", "bepuphysics2", "Demos", "Content", "Carlito-Regular.ttf");

        using FileStream stream = new FileStream(sourceFontPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        FontAsset fontAsset = new GdiFontImporter().ImportFont(stream, new FontAssetProcessorSettings {
            PixelSize = 32
        });

        Assert.Equal("Carlito", fontAsset.FontInfo.Name);
    }

    /// <summary>
    /// Ensures importing the same source font with two different pixel sizes changes the emitted font metrics.
    /// </summary>
    [Fact]
    public void ImportFont_WhenPixelSizeChanges_UsesRequestedPlatformSize() {
        string repositoryRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string sourceFontPath = Path.Combine(repositoryRootPath, "vendor", "bepuphysics2", "Demos", "Content", "Carlito-Regular.ttf");
        GdiFontImporter importer = new GdiFontImporter();

        using FileStream smallStream = new FileStream(sourceFontPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        FontAsset smallFont = importer.ImportFont(smallStream, new FontAssetProcessorSettings {
            PixelSize = 12
        });

        using FileStream largeStream = new FileStream(sourceFontPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        FontAsset largeFont = importer.ImportFont(largeStream, new FontAssetProcessorSettings {
            PixelSize = 32
        });

        Assert.NotEqual(smallFont.LineHeight, largeFont.LineHeight);
        Assert.True(smallFont.AtlasHeight < largeFont.AtlasHeight);
    }
    /// <summary>
    /// Locally reproduces the DemoDisc PSP font-import path without becoming a portable repository test.
    /// </summary>
    [Fact]
    public void LocalDiagnostic_BuildFontAssetForPsp_UsesDemoDiscFredokaFamily() {
        string projectRootPath = @"C:\dev\helprojs\demodisc";
        string sourceFontPath = Path.Combine(projectRootPath, "assets", "fonts", "Fredoka.ttf");
        ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(projectRootPath, "assets")));
        AssetImportManager assetImportManager = new AssetImportManager(projectRootPath, contentManager);
        assetImportManager.RegisterFontImporter(new FontImporterRegistration("gdi-font", new GdiFontImporter(), new[] { ".ttf" }));

        FontAsset fontAsset = assetImportManager.BuildFontAssetForPlatform(sourceFontPath, "psp");

        Assert.Equal("Fredoka SemiBold", fontAsset.FontInfo.Name);
    }

    /// <summary>
    /// Locally verifies that serializing the DemoDisc PSP font cache preserves the source family.
    /// </summary>
    [Fact]
    public void LocalDiagnostic_ResolvePspFontVariant_PreservesDemoDiscFredokaFamily() {
        string projectRootPath = @"C:\dev\helprojs\demodisc";
        string sourceFontPath = Path.Combine(projectRootPath, "assets", "fonts", "Fredoka.ttf");
        ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(projectRootPath, "assets")));
        AssetImportManager assetImportManager = new AssetImportManager(projectRootPath, contentManager);
        assetImportManager.RegisterFontImporter(new FontImporterRegistration("gdi-font", new GdiFontImporter(), new[] { ".ttf" }));
        EditorPlatformFontVariantCacheService service = new EditorPlatformFontVariantCacheService(assetImportManager);

        EditorPlatformFontVariantCacheResult result = service.ResolveVariant(sourceFontPath, "psp");
        using FileStream stream = File.OpenRead(result.CachedFontAssetPath);
        FontAsset fontAsset = helengine.files.FontAssetBinarySerializer.Deserialize(stream);

        Assert.Equal("Fredoka SemiBold", fontAsset.FontInfo.Name);
    }
    /// <summary>
    /// Locally reproduces headless editor initialization before serializing the DemoDisc PSP font cache.
    /// </summary>
    [Fact]
    public void LocalDiagnostic_ResolvePspFontVariant_AfterHeadlessDefaultFont_PreservesDemoDiscFredokaFamily() {
        using Font defaultFont = new Font("Consolas", 12, FontStyle.Regular, GraphicsUnit.Pixel);
        _ = GDIFontProcessor.ImportFont(defaultFont);

        string projectRootPath = @"C:\dev\helprojs\demodisc";
        string sourceFontPath = Path.Combine(projectRootPath, "assets", "fonts", "Fredoka.ttf");
        ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(projectRootPath, "assets")));
        AssetImportManager assetImportManager = new AssetImportManager(projectRootPath, contentManager);
        assetImportManager.RegisterFontImporter(new FontImporterRegistration("gdi-font", new GdiFontImporter(), new[] { ".ttf" }));
        EditorPlatformFontVariantCacheService service = new EditorPlatformFontVariantCacheService(assetImportManager);

        EditorPlatformFontVariantCacheResult result = service.ResolveVariant(sourceFontPath, "psp");
        using FileStream stream = File.OpenRead(result.CachedFontAssetPath);
        FontAsset fontAsset = helengine.files.FontAssetBinarySerializer.Deserialize(stream);

        Assert.Equal("Fredoka SemiBold", fontAsset.FontInfo.Name);
    }
    /// <summary>
    /// Locally reproduces the STA apartment used by the headless editor host before creating the PSP font cache.
    /// </summary>
    [Fact]
    public void LocalDiagnostic_ResolvePspFontVariant_OnStaThread_PreservesDemoDiscFredokaFamily() {
        Exception failure = null;
        string familyName = null;
        Thread thread = new Thread(() => {
            try {
                string projectRootPath = @"C:\dev\helprojs\demodisc";
                string sourceFontPath = Path.Combine(projectRootPath, "assets", "fonts", "Fredoka.ttf");
                ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(projectRootPath, "assets")));
                AssetImportManager assetImportManager = new AssetImportManager(projectRootPath, contentManager);
                assetImportManager.RegisterFontImporter(new FontImporterRegistration("gdi-font", new GdiFontImporter(), new[] { ".ttf" }));
                EditorPlatformFontVariantCacheService service = new EditorPlatformFontVariantCacheService(assetImportManager);

                EditorPlatformFontVariantCacheResult result = service.ResolveVariant(sourceFontPath, "psp");
                using FileStream stream = File.OpenRead(result.CachedFontAssetPath);
                familyName = helengine.files.FontAssetBinarySerializer.Deserialize(stream).FontInfo.Name;
            } catch (Exception exception) {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
        Assert.Equal("Fredoka SemiBold", familyName);
    }
    /// <summary>
    /// Locally generates a fresh isolated PSP cache from the DemoDisc font and its real import sidecar.
    /// </summary>
    [Fact]
    public void LocalDiagnostic_GenerateFreshPspFontVariant_PreservesDemoDiscFredokaFamily() {
        string sourceProjectRootPath = @"C:\dev\helprojs\demodisc";
        string projectRootPath = Path.Combine(@"C:\dev\helworks\b\font-cache-diagnostics", Guid.NewGuid().ToString("N"));
        string assetsRootPath = Path.Combine(projectRootPath, "assets");
        string fontsRootPath = Path.Combine(assetsRootPath, "fonts");
        Directory.CreateDirectory(fontsRootPath);
        string sourceFontPath = Path.Combine(sourceProjectRootPath, "assets", "fonts", "Fredoka.ttf");
        string copiedFontPath = Path.Combine(fontsRootPath, "Fredoka.ttf");
        File.Copy(sourceFontPath, copiedFontPath);
        File.Copy(sourceFontPath + ".hasset", copiedFontPath + ".hasset");

        ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(assetsRootPath));
        AssetImportManager assetImportManager = new AssetImportManager(projectRootPath, contentManager) {
            CurrentPlatformId = "psp",
            CurrentEnvironmentId = "release"
        };
        assetImportManager.RegisterFontImporter(new FontImporterRegistration("gdi-font", new GdiFontImporter(), new[] { ".ttf" }));
        EditorPlatformFontVariantCacheService service = new EditorPlatformFontVariantCacheService(assetImportManager);

        EditorPlatformFontVariantCacheResult result = service.ResolveVariant(copiedFontPath, "psp");
        Assert.False(result.IsCacheHit);
        using FileStream stream = File.OpenRead(result.CachedFontAssetPath);
        FontAsset fontAsset = helengine.files.FontAssetBinarySerializer.Deserialize(stream);
        Assert.Equal("Fredoka SemiBold", fontAsset.FontInfo.Name);
    }
}
