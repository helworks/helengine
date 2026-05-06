using helengine.editor;
using helengine.directx11;
using helengine.vulkan;

namespace helengine.editor.app {
    /// <summary>
    /// Creates the default asset-importer registrations used by the editor host.
    /// </summary>
    internal static class EditorHostImporterFactory {
        /// <summary>
        /// Builds the default importer registrations used by both GUI and CLI editor startup.
        /// </summary>
        /// <returns>Default importer registrations.</returns>
        public static IReadOnlyList<IAssetImporterRegistration> CreateDefault() {
            string[] textExtensions = new[] { ".txt" };
            string[] modelExtensions = new[] { ".fbx", ".obj", ".gltf", ".glb", ".dae", ".3ds" };
            string[] fontExtensions = new[] { ".ttf", ".otf" };
            List<IAssetImporterRegistration> registrations = new List<IAssetImporterRegistration>(EditorHostTextureImporterFactory.CreateDefault());
            registrations.AddRange(new IAssetImporterRegistration[] {
                new TextImporterRegistration("text", new TextImporter(), textExtensions),
                new FontImporterRegistration("gdi-font", new GdiFontImporter(), fontExtensions),
                new ModelImporterRegistration(
                    "assimp",
                    new LazyModelImporter(new AssemblyModelImporterFactory("helengine.editor.assimp", "helengine.editor.assimp.HelengineAssimpImporter")),
                    modelExtensions)
            });

            return registrations;
        }
    }
}
