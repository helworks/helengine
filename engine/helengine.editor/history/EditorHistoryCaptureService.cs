namespace helengine.editor {
    /// <summary>
    /// Captures detached history snapshots for editor-owned scene entities and scene settings.
    /// </summary>
    public class EditorHistoryCaptureService {
        /// <summary>
        /// Scene-save service reused to serialize live entities using the normal scene persistence pipeline.
        /// </summary>
        readonly SceneSaveService SceneSaveService;

        /// <summary>
        /// Initializes one history capture service.
        /// </summary>
        /// <param name="sceneSaveService">Scene-save service reused for entity serialization.</param>
        public EditorHistoryCaptureService(SceneSaveService sceneSaveService) {
            SceneSaveService = sceneSaveService ?? throw new ArgumentNullException(nameof(sceneSaveService));
        }

        /// <summary>
        /// Captures one live editor entity into one detached serialized history snapshot.
        /// </summary>
        /// <param name="entity">Live editor entity that should be serialized.</param>
        /// <returns>Detached serialized entity snapshot.</returns>
        public SerializedEditorEntityState CaptureEntity(EditorEntity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            return SceneSaveService.CaptureEntityState(entity);
        }

        /// <summary>
        /// Creates one detached copy of the supplied scene settings payload for history storage.
        /// </summary>
        /// <param name="sceneSettings">Scene settings that should be copied.</param>
        /// <returns>Detached scene settings copy.</returns>
        public SceneSettingsAsset CloneSceneSettings(SceneSettingsAsset sceneSettings) {
            if (sceneSettings == null) {
                throw new ArgumentNullException(nameof(sceneSettings));
            }

            return SceneSaveService.CloneSceneSettingsAsset(sceneSettings);
        }
    }
}
