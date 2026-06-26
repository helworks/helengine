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
}
