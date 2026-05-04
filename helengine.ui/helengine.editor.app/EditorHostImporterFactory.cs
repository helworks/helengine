using helengine.editor;
using helengine.editor.assimp;
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
            string[] textureExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif" };
            string[] textExtensions = new[] { ".txt" };
            string[] modelExtensions = new[] { ".fbx", ".obj", ".gltf", ".glb", ".dae", ".3ds" };
            return new IAssetImporterRegistration[] {
                new TextureImporterRegistration("gdi", new GDITextureImporter(), textureExtensions),
                new TextImporterRegistration("text", new TextImporter(), textExtensions),
                new ModelImporterRegistration("assimp", new HelengineAssimpImporter(), modelExtensions)
            };
        }
    }
}
