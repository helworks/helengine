using helengine;
using helengine.editor;

namespace helengine.editor.windows.gdiimporter.tests {
    /// <summary>
    /// Verifies the GDI font importer only builds atlas textures on the session renderer from the renderer's own thread:
    /// an import on another thread, such as the editor's background Play cook, produces the headless managed atlas with
    /// the same pixels instead of touching the single-threaded renderer.
    /// </summary>
    public sealed class GdiFontImporterRendererThreadTests {
        /// <summary>
        /// Ensures an import on the creating thread uses the renderer, an import on another thread does not, and both
        /// produce identical atlas pixels.
        /// </summary>
        [Fact]
        public void ImportOnAnotherThread_DoesNotUseTheRenderer_AndKeepsTheAtlasPixels() {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "trebuc.ttf");
            CountingRenderManager2D renderer = new CountingRenderManager2D();
            GdiFontImporter importer = new GdiFontImporter(renderer);
            FontAssetProcessorSettings settings = new FontAssetProcessorSettings { PixelSize = 34 };

            using FileStream ownerStream = File.OpenRead(path);
            using FontAsset ownerImport = importer.ImportFont(ownerStream, settings);
            Assert.Equal(1, renderer.BuildTextureFromRawCallCount);

            FontAsset backgroundImport = null;
            Exception backgroundFailure = null;
            Thread thread = new Thread(() => {
                try {
                    using FileStream backgroundStream = File.OpenRead(path);
                    backgroundImport = importer.ImportFont(backgroundStream, settings);
                } catch (Exception exception) {
                    backgroundFailure = exception;
                }
            });
            thread.Start();
            thread.Join();

            Assert.Null(backgroundFailure);
            using (backgroundImport) {
                Assert.NotNull(backgroundImport);
                Assert.Equal(1, renderer.BuildTextureFromRawCallCount);
                Assert.Equal(ownerImport.SourceTextureAsset.Colors, backgroundImport.SourceTextureAsset.Colors);
                Assert.Equal(ownerImport.AtlasWidth, backgroundImport.AtlasWidth);
                Assert.Equal(ownerImport.AtlasHeight, backgroundImport.AtlasHeight);
            }
        }

        /// <summary>
        /// Ensures a host can name the renderer thread explicitly: the importer reports it, and an import on any other
        /// thread, here the constructing one, does not use the renderer.
        /// </summary>
        [Fact]
        public void ExplicitRendererThread_IsTheOnlyThreadThatUsesTheRenderer() {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "trebuc.ttf");
            CountingRenderManager2D renderer = new CountingRenderManager2D();
            int otherThreadId = Environment.CurrentManagedThreadId + 1000;
            GdiFontImporter importer = new GdiFontImporter(renderer, otherThreadId);

            using FileStream stream = File.OpenRead(path);
            using FontAsset imported = importer.ImportFont(stream, new FontAssetProcessorSettings { PixelSize = 34 });

            Assert.Equal(otherThreadId, importer.RendererThreadId);
            Assert.Equal(0, renderer.BuildTextureFromRawCallCount);
            Assert.NotNull(imported.SourceTextureAsset);
        }
    }
}
