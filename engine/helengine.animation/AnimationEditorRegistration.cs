using helengine.editor;
using helengine.files;

namespace helengine.animation {
    /// <summary>
    /// Plugs the animation plugin into a host: the <c>.hanimclip</c> payload serializer, the packaged clip cooker and the glTF
    /// animation scene contributor. Hosts add it to the importer registrations they pass to the editor, the authoring session
    /// factory and the build packager; the editor never references this assembly itself.
    /// </summary>
    public sealed class AnimationEditorRegistration : IAssetImporterRegistration {
        /// <summary>
        /// Registers the serializer process-wide (repeating it is harmless) and the cooker and contributor with the registry.
        /// </summary>
        /// <param name="registry">Registry of the host being configured.</param>
        public void Register(AssetImporterRegistry registry) {
            if (registry == null) {
                throw new ArgumentNullException(nameof(registry));
            }

            EditorAssetPayloadSerializerRegistry.Register(new HierarchyAnimationClipPayloadSerializer());
            registry.RegisterPackagedAssetCooker(new HierarchyAnimationClipCooker());
            registry.RegisterSceneContributor(new GltfAnimationSceneContributor());
        }
    }
}
