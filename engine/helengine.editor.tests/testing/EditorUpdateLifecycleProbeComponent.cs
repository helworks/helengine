namespace helengine.editor.tests.testing {
    /// <summary>
    /// Records update-component lifecycle calls so editor execution policy can be verified without depending on gameplay code.
    /// </summary>
    class EditorUpdateLifecycleProbeComponent : UpdateComponent {
        /// <summary>
        /// Gets the number of times the attach lifecycle has executed.
        /// </summary>
        public int ComponentAddedCallCount { get; private set; }

        /// <summary>
        /// Gets the number of times the detach lifecycle has executed.
        /// </summary>
        public int ComponentRemovedCallCount { get; private set; }
        /// <summary>
        /// Gets the number of times the hierarchy-initialized lifecycle has executed.
        /// </summary>
        public int ComponentInitializedCallCount { get; private set; }

        /// <summary>
        /// Gets the number of times the per-frame update callback has executed.
        /// </summary>
        public int UpdateCallCount { get; private set; }

        /// <summary>
        /// Gets the number of times parent enabled-state changes have been forwarded to the component.
        /// </summary>
        public int ParentEnabledChangeCallCount { get; private set; }

        /// <summary>
        /// Records one attach lifecycle execution.
        /// </summary>
        /// <param name="entity">Entity receiving the component.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            ComponentAddedCallCount++;
        }

        /// <summary>
        /// Records one detach lifecycle execution.
        /// </summary>
        /// <param name="entity">Entity losing the component.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            ComponentRemovedCallCount++;
        }

        /// <summary>
        /// Records one hierarchy-initialized lifecycle execution.
        /// </summary>
        /// <param name="entity">Entity whose hierarchy finished initialization.</param>
        public override void ComponentInitialized(Entity entity) {
            base.ComponentInitialized(entity);
            ComponentInitializedCallCount++;
        }

        /// <summary>
        /// Records one enabled-state transition routed from the parent entity.
        /// </summary>
        /// <param name="newEnabled">True when the parent hierarchy became enabled; otherwise false.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);
            ParentEnabledChangeCallCount++;
        }

        /// <summary>
        /// Records one per-frame update callback.
        /// </summary>
        public override void Update() {
            UpdateCallCount++;
        }
    }
}
