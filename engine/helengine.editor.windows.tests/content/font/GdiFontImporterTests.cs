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
        FontAsset fontAsset = new GdiFontImporter().ImportFont(stream);

        Assert.NotNull(fontAsset);
        Assert.NotNull(fontAsset.SourceTextureAsset);
        Assert.NotEmpty(fontAsset.Characters);
        Assert.True(fontAsset.AtlasWidth > 0);
        Assert.True(fontAsset.AtlasHeight > 0);
    }
}
