using System.Drawing;
using System.Drawing.Text;

namespace helengine.editor.windows.gdiimporter.tests {
    /// <summary>Loads an independently copied source face for pixel references. GDI can rasterize an installed path differently from an identical private source-file copy, so both reference and importer consume private files.</summary>
    internal sealed class GdiFontFileFixture : IDisposable {
        /// <summary>Owns the private family until all caller-created fonts have been disposed.</summary>
        readonly PrivateFontCollection Collection = new();
        /// <summary>Owns the temporary source file used by the reference rasterizer.</summary>
        readonly string SourcePath;

        /// <summary>Copies the actual source bytes outside the installed-font cache and loads them directly through GDI.</summary>
        /// <param name="sourcePath">Original face whose source bytes are tested by the importer.</param>
        public GdiFontFileFixture(string sourcePath) {
            string directory = @"C:\temp\helengine\gdi-font-tests";
            Directory.CreateDirectory(directory);
            SourcePath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".ttf");
            File.WriteAllBytes(SourcePath, File.ReadAllBytes(sourcePath));
            Collection.AddFontFile(SourcePath);
        }

        /// <summary>Creates an independently configured reference font; callers dispose it before disposing this fixture.</summary>
        /// <param name="pixels">Reference raster size in pixels.</param>
        /// <param name="style">Authored source style selected independently of the production parser.</param>
        /// <returns>The caller-owned GDI reference font.</returns>
        public Font CreateFont(float pixels, FontStyle style) => new(Collection.Families[0], pixels, style, GraphicsUnit.Pixel);

        /// <summary>Releases the reference family before removing its copied face file.</summary>
        public void Dispose() { Collection.Dispose(); File.Delete(SourcePath); }
    }
}