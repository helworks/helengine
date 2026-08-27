namespace helengine.editor {
    /// <summary>
    /// Provides the current public authoring operations used by maintained generated scene tools.
    /// </summary>
    public sealed class GeneratedSceneComponentAuthoringService {
        /// <summary>
        /// Reflection schema used by current automatic component authoring.
        /// </summary>
        readonly ScriptComponentReflectionSchemaBuilder SchemaBuilder;

        /// <summary>
        /// Initializes one current generated-scene component authoring service.
        /// </summary>
        public GeneratedSceneComponentAuthoringService() {
            SchemaBuilder = new ScriptComponentReflectionSchemaBuilder();
        }

        /// <summary>
        /// Creates the current persisted payload for one camera component.
        /// </summary>
        /// <param name="camera">Camera component to author.</param>
        /// <returns>Current component payload.</returns>
        public byte[] CreateCameraPayload(CameraComponent camera) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            return CreateAutomaticComponentPayload(camera);
        }

        /// <summary>
        /// Creates the current persisted payload for one automatic component.
        /// </summary>
        /// <param name="component">Automatic component to author.</param>
        /// <returns>Current component payload.</returns>
        public byte[] CreateLightPayload(Component component) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            return CreateAutomaticComponentPayload(component);
        }

        /// <summary>
        /// Creates the current tagged payload for one mesh's model and material references.
        /// </summary>
        /// <param name="model">Model reference assigned to the mesh.</param>
        /// <param name="material">Material reference assigned to the mesh.</param>
        /// <returns>Current mesh component payload.</returns>
        public byte[] CreateMeshPayload(SceneAssetReference model, SceneAssetReference material) {
            if (model == null) {
                throw new ArgumentNullException(nameof(model));
            } else if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("Model", field => SceneComponentBinaryFieldEncoding.WriteOptionalReference(field, model));
            writer.WriteField("Materials", field => SceneComponentBinaryFieldEncoding.WriteOptionalReferenceArray(field, new[] { material }));
            writer.WriteField("RenderOrder3D", field => field.WriteByte(0));
            return writer.BuildPayload();
        }

        /// <summary>
        /// Creates the empty current tagged payload used by generated script marker components.
        /// </summary>
        /// <returns>Empty current tagged component payload.</returns>
        public byte[] CreateEmptyScriptPayload() {
            return new EditorTaggedSceneComponentFieldWriter().BuildPayload();
        }

        /// <summary>
        /// Creates one current automatic component payload through the editor's registered persistence descriptor.
        /// </summary>
        /// <param name="component">Component to author.</param>
        /// <returns>Current component payload.</returns>
        byte[] CreateAutomaticComponentPayload(Component component) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            return new AutomaticScriptComponentPersistenceDescriptor(SchemaBuilder)
                .SerializeComponent(component, 0, null)
                .Payload;
        }
    }
}
