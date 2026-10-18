namespace helengine.editor {
    /// <summary>Retains a detached participant and its reference metadata until official ordinal payload emission.</summary>
    internal sealed class ViewportPreparedCookComponent {
        /// <summary>Pairs detached values with their existing persistence metadata.</summary>
        public ViewportPreparedCookComponent(Component component, EntitySaveComponent saveComponent) {
            Component = component;
            SaveComponent = saveComponent;
        }

        /// <summary>Effective component instance modified by the cook pass.</summary>
        public Component Component { get; }
        /// <summary>Saved asset/entity references decoded with that same component instance.</summary>
        public EntitySaveComponent SaveComponent { get; }
    }
}
