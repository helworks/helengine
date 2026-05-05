namespace helengine.editor {
    /// <summary>
    /// Persists the marker component that identifies the selected-description text target inside one baked menu panel.
    /// </summary>
    public class DemoMenuSelectedDescriptionComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Gets the concrete runtime component type handled by the descriptor.
        /// </summary>
        public Type ComponentType => typeof(DemoMenuSelectedDescriptionComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => DemoMenuSelectedDescriptionComponent.SerializedComponentTypeId;

        /// <summary>
        /// Serializes one marker component into a scene record.
        /// </summary>
        public SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState) {
            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = new EditorTaggedSceneComponentFieldWriter().BuildPayload()
            };
        }

        /// <summary>
        /// Deserializes one marker component from a scene record.
        /// </summary>
        public Component DeserializeComponent(SceneComponentAssetRecord record, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver) {
            return new DemoMenuSelectedDescriptionComponent();
        }
    }
}
