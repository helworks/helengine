using helengine.editor;

namespace helengine.animation.tests {
    /// <summary>
    /// Packager registration that registers the clip payload serializer and a <see cref="RecordingClipCooker"/> in place of
    /// <see cref="AnimationEditorRegistration"/>, whose own cooker cannot be observed from outside the packager.
    /// </summary>
    sealed class RecordingClipCookerRegistration : IAssetImporterRegistration {
        /// <summary>
        /// Cooker registered for clips.
        /// </summary>
        readonly RecordingClipCooker Cooker;

        /// <summary>
        /// Creates the registration.
        /// </summary>
        /// <param name="cooker">Cooker to register for clips.</param>
        public RecordingClipCookerRegistration(RecordingClipCooker cooker) {
            Cooker = cooker ?? throw new ArgumentNullException(nameof(cooker));
        }

        /// <summary>
        /// Registers the payload serializer and the recording cooker.
        /// </summary>
        /// <param name="registry">Packager registry.</param>
        public void Register(AssetImporterRegistry registry) {
            global::helengine.files.EditorAssetPayloadSerializerRegistry.Register(new HierarchyAnimationClipPayloadSerializer());
            registry.RegisterPackagedAssetCooker(Cooker);
        }
    }
}
