using System.Drawing;
using helengine;
using helengine.editor;
using helengine.platforms;
using FontStyle = System.Drawing.FontStyle;
using GraphicsUnit = System.Drawing.GraphicsUnit;

namespace helengine.debugtools {
    /// <summary>
    /// Runs the city Windows build through a console executable so build failures are visible in terminal output.
    /// </summary>
    public static class Program {
        /// <summary>
        /// Stable city project path used for local Windows build validation.
        /// </summary>
        const string ProjectPath = @"C:\dev\helprojs\city\project.heproj";

        /// <summary>
        /// Stable output directory used for the Windows export.
        /// </summary>
        const string OutputDirectoryPath = @"C:\dev\helprojs\city\windows-build";

        /// <summary>
        /// Stable Windows platform identifier used by the installed builder metadata.
        /// </summary>
        const string WindowsPlatformId = "windows";

        /// <summary>
        /// Builds the city project for Windows and writes the build result to the console.
        /// </summary>
        /// <param name="args">Unused command-line arguments.</param>
        /// <returns>Zero when the build succeeds; otherwise one.</returns>
        public static int Main(string[] args) {
            try {
                FontAsset defaultFontAsset = GDIFontProcessor.ImportFont(new Font("Consolas", 12, FontStyle.Regular, GraphicsUnit.Pixel));
                EditorCliBuildRunner runner = new EditorCliBuildRunner(CreateDefaultImporters(), defaultFontAsset);
                EditorCliBuildOptions options = new EditorCliBuildOptions(ProjectPath, WindowsPlatformId, OutputDirectoryPath, false);
                EditorBuildExecutionResult result = runner.Run(options);
                if (result.Succeeded) {
                    Console.WriteLine(result.Message);
                    return 0;
                }

                Console.Error.WriteLine(result.Message);
                return 1;
            } catch (Exception exception) {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        /// <summary>
        /// Builds the default importer registrations required by the Windows editor build graph.
        /// </summary>
        /// <returns>Default asset importer registrations for textures, text, fonts, and models.</returns>
        static IReadOnlyList<IAssetImporterRegistration> CreateDefaultImporters() {
            string[] textExtensions = [".txt"];
            string[] modelExtensions = [".fbx", ".obj", ".gltf", ".glb", ".dae", ".3ds", ".x"];
            string[] fontExtensions = [".ttf", ".otf"];
            List<IAssetImporterRegistration> registrations = new List<IAssetImporterRegistration>(EditorHostTextureImporterFactory.CreateDefault());
            registrations.AddRange([
                new TextImporterRegistration("text", new TextImporter(), textExtensions),
                new FontImporterRegistration("gdi-font", new GdiFontImporter(), fontExtensions),
                new ModelImporterRegistration(
                    "assimp",
                    new LazyModelImporter(new AssemblyModelImporterFactory("helengine.editor.assimp", "helengine.editor.assimp.HelengineAssimpImporter")),
                    modelExtensions)
            ]);
            return registrations;
        }
    }
}
