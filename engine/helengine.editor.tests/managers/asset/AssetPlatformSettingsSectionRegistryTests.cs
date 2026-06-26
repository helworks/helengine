using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies generic platform settings section registration and typed retrieval.
    /// </summary>
    public sealed class AssetPlatformSettingsSectionRegistryTests {
        /// <summary>
        /// Ensures the built-in font section creates a default payload with the historical effective pixel size.
        /// </summary>
        [Fact]
        public void GetOrCreateSection_WhenFontSectionIsMissing_CreatesDefaultFontSettings() {
            AssetPlatformProcessorSettings platformSettings = new AssetPlatformProcessorSettings();

            FontAssetProcessorSettings fontSettings = AssetPlatformSettingsSectionRegistry.Shared.GetOrCreateSection<FontAssetProcessorSettings>(platformSettings, "font");

            Assert.Equal(32, fontSettings.PixelSize);
        }

        /// <summary>
        /// Ensures requesting a section through the wrong payload type fails explicitly.
        /// </summary>
        [Fact]
        public void GetOrCreateSection_WhenRequestedWithWrongType_ThrowsInvalidOperationException() {
            AssetPlatformProcessorSettings platformSettings = new AssetPlatformProcessorSettings();
            AssetPlatformSettingsSectionRegistry.Shared.GetOrCreateSection<FontAssetProcessorSettings>(platformSettings, "font");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => AssetPlatformSettingsSectionRegistry.Shared.GetOrCreateSection<TextureAssetProcessorSettings>(platformSettings, "font"));

            Assert.Contains("font", exception.Message, StringComparison.Ordinal);
        }
    }
}
