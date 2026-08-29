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
        /// <param name="renderManager2D">Session-owned renderer used by imported font atlases, or null for headless imports.</param>
        /// <returns>Default importer registrations.</returns>
        public static IReadOnlyList<IAssetImporterRegistration> CreateDefault(RenderManager2D renderManager2D) {
            string[] textExtensions = new[] { ".txt" };
            string[] modelExtensions = new[] { ".fbx", ".obj", ".gltf", ".glb", ".dae", ".3ds", ".x" };
            string[] fontExtensions = new[] { ".ttf", ".otf" };
            List<IAssetImporterRegistration> registrations = new List<IAssetImporterRegistration>(EditorHostTextureImporterFactory.CreateDefault());
            registrations.AddRange(EditorHostAudioImporterFactory.CreateDefault());
            registrations.AddRange(new IAssetImporterRegistration[] {
                new TextImporterRegistration("text", new TextImporter(), textExtensions),
                new FontImporterRegistration("gdi-font", new GdiFontImporter(renderManager2D), fontExtensions),
                new ModelImporterRegistration(
                    "assimp",
                    new LazyModelImporter(new AssemblyModelImporterFactory("helengine.editor.assimp", "helengine.editor.assimp.HelengineAssimpImporter")),
                    modelExtensions)
            });

            return registrations;
        }

        /// <summary>
        /// Creates the project authoring-session factory configured with this host's importer registrations.
        /// </summary>
        /// <returns>Host-configured project authoring-session factory.</returns>
        public static IEditorProjectAuthoringSessionFactory CreateAuthoringSessionFactory() {
            return new EditorProjectAssetAuthoringServiceFactory(CreateDefault(null));
        }
    }
}
