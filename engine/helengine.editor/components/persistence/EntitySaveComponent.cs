namespace helengine {
    /// <summary>
    /// Hidden editor-only component that stores per-component scene persistence metadata for one entity.
    /// </summary>
    public class EntitySaveComponent : Component, IEditorHiddenComponent {
        /// <summary>
        /// Save-state containers keyed by the live component instance they describe.
        /// </summary>
        readonly Dictionary<Component, EntityComponentSaveState> SaveStatesByComponent;

        /// <summary>
        /// Stable id used to reference the owning entity from serialized scene data.
        /// </summary>
        public string EntityId { get; set; }

        /// <summary>
        /// Initializes a new empty entity save-component.
        /// </summary>
        public EntitySaveComponent() {
            SaveStatesByComponent = new Dictionary<Component, EntityComponentSaveState>();
        }

        /// <summary>
        /// Gets the existing save state for a component or creates a new one when needed.
        /// </summary>
        /// <param name="component">Component whose save state should be returned.</param>
        /// <returns>Mutable save-state container for the component.</returns>
        public EntityComponentSaveState GetOrCreateComponentState(Component component) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            if (!SaveStatesByComponent.TryGetValue(component, out EntityComponentSaveState saveState)) {
                saveState = new EntityComponentSaveState();
                SaveStatesByComponent.Add(component, saveState);
            }

            return saveState;
        }

        /// <summary>
        /// Attempts to read the save state stored for one component.
        /// </summary>
        /// <param name="component">Component whose save state should be resolved.</param>
        /// <param name="saveState">Save-state container when one exists.</param>
        /// <returns>True when save metadata exists for the supplied component.</returns>
        public bool TryGetComponentState(Component component, out EntityComponentSaveState saveState) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            return SaveStatesByComponent.TryGetValue(component, out saveState);
        }

        /// <summary>
        /// Stores one named asset reference for one component.
        /// </summary>
        /// <param name="component">Component that owns the reference.</param>
        /// <param name="referenceName">Stable reference slot name.</param>
        /// <param name="reference">Stable asset reference to persist.</param>
        public void SetAssetReference(Component component, string referenceName, SceneAssetReference reference) {
            EntityComponentSaveState saveState = GetOrCreateComponentState(component);
            saveState.SetAssetReference(referenceName, reference);
        }
    }
}
