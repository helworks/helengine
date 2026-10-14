using System.Drawing.Text;

namespace helengine.editor {
    /// <summary>
    /// Imports source font files through the existing GDI-backed font rasterization path.
    /// </summary>
    public sealed class GdiFontImporter : IFontImporter {
        /// <summary>
        /// Renderer owned by the importing editor session, when the importer is
        /// used by an interactive host. Headless callers pass null and retain a
        /// managed atlas texture without consulting ambient core state.
        /// </summary>
        readonly RenderManager2D RenderManager2D;

        /// <summary>
        /// Initializes a GDI font importer whose renderer belongs to the calling thread. This is the contract with the
        /// interactive host: it must construct the importer on the editor frame thread, the thread every frame and every
        /// interactive import runs on. If the host ever drives frames from another thread, it must use the overload that
        /// names the renderer thread, or interactive imports would build headless atlases the renderer cannot draw.
        /// </summary>
        /// <param name="renderManager2D">Session-owned renderer, or null for headless imports.</param>
        public GdiFontImporter(RenderManager2D renderManager2D)
            : this(renderManager2D, Environment.CurrentManagedThreadId) {
        }

        /// <summary>
        /// Initializes a GDI font importer with its renderer and the managed id of the only thread that may use it.
        /// </summary>
        /// <param name="renderManager2D">Session-owned renderer, or null for headless imports.</param>
        /// <param name="rendererThreadId">Managed id of the thread that owns the renderer.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the thread id is not positive.</exception>
        public GdiFontImporter(RenderManager2D renderManager2D, int rendererThreadId) {
            if (rendererThreadId <= 0) {
                throw new ArgumentOutOfRangeException(nameof(rendererThreadId), rendererThreadId, "The renderer thread id must be a managed thread id.");
            }

            RenderManager2D = renderManager2D;
            RendererThreadId = rendererThreadId;
        }

        /// <summary>
        /// Gets the managed id of the thread whose imports build atlases on the renderer; imports on any other thread
        /// build the headless managed atlas.
        /// </summary>
        public int RendererThreadId { get; }

        /// <summary>
        /// Imports one source font stream into a runtime-ready font asset. The renderer is single-threaded, so only an
        /// import on the renderer's own thread builds the atlas texture on it. An import on any other thread, such as
        /// the editor's background Play cook, builds the headless managed atlas a command-line build uses; cooking
        /// packages the atlas pixels from <see cref="FontAsset.SourceTextureAsset"/>, which both paths fill identically.
        /// </summary>
        /// <param name="stream">Stream containing source font bytes.</param>
        /// <param name="settings">Platform font settings supplied by the caller.</param>
        /// <returns>Imported font asset.</returns>
        public FontAsset ImportFont(Stream stream, FontAssetProcessorSettings settings) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (settings.PixelSize < 1) {
                throw new InvalidOperationException("Font pixel size must be greater than zero.");
            }

            using MemoryStream buffer = new MemoryStream();
            stream.CopyTo(buffer);
            byte[] bytes = buffer.ToArray();
            if (bytes.Length == 0) {
                throw new InvalidOperationException("Font source stream must contain data.");
            }
            System.Drawing.FontStyle sourceStyle = GdiFontSourceStyle.Read(bytes);

            string temporaryFontFilePath = string.Empty;
            try {
                using PrivateFontCollection fontCollection = LoadFontCollection(bytes, ref temporaryFontFilePath);
                if (!fontCollection.Families[0].IsStyleAvailable(sourceStyle)) {
                    throw new InvalidOperationException("The source font family does not support its authored face style.");
                }
                using System.Drawing.Font font = new System.Drawing.Font(
                    fontCollection.Families[0],
                    settings.PixelSize,
                    sourceStyle,
                    System.Drawing.GraphicsUnit.Pixel);
                RenderManager2D atlasRenderer = Environment.CurrentManagedThreadId == RendererThreadId ? RenderManager2D : null;
                FontAsset asset = GDIFontProcessor.ImportFont(font, atlasRenderer);
                FontHorizontalMetricsProcessor.Apply(asset, settings);
                return asset;
            } finally {
                if (!string.IsNullOrWhiteSpace(temporaryFontFilePath) && File.Exists(temporaryFontFilePath)) {
                    File.Delete(temporaryFontFilePath);
                }
            }
        }

        /// <summary>
        /// Loads one private font collection from raw source bytes through a temporary font file so GDI resolves the authored family deterministically.
        /// </summary>
        /// <param name="bytes">Source font bytes copied from the importer stream.</param>
        /// <param name="temporaryFontFilePath">Receives the temporary font-file path used by the private font collection.</param>
        /// <returns>Private font collection that exposes at least one installable font family.</returns>
        static PrivateFontCollection LoadFontCollection(byte[] bytes, ref string temporaryFontFilePath) {
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            } else if (bytes.Length == 0) {
                throw new InvalidOperationException("Font source stream must contain data.");
            }

            temporaryFontFilePath = CreateTemporaryFontFile(bytes);
            PrivateFontCollection fontCollection = new PrivateFontCollection();
            fontCollection.AddFontFile(temporaryFontFilePath);
            if (fontCollection.Families.Length == 0) {
                fontCollection.Dispose();
                throw new InvalidOperationException("Source font did not produce any installable font families.");
            }

            return fontCollection;
        }

        /// <summary>
        /// Writes one temporary source font file that GDI can open through the file-backed import path.
        /// </summary>
        /// <param name="bytes">Source font bytes copied from the importer stream.</param>
        /// <returns>Absolute temporary file path.</returns>
        static string CreateTemporaryFontFile(byte[] bytes) {
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            } else if (bytes.Length == 0) {
                throw new InvalidOperationException("Font source stream must contain data.");
            }

            string temporaryDirectoryPath = Path.Combine(Path.GetTempPath(), "helengine", "gdi-font-import");
            Directory.CreateDirectory(temporaryDirectoryPath);
            string temporaryFontFilePath = Path.Combine(temporaryDirectoryPath, Guid.NewGuid().ToString("N") + ".ttf");
            File.WriteAllBytes(temporaryFontFilePath, bytes);
            return temporaryFontFilePath;
        }
    }
}
