namespace helengine.editor.tests.testing {
    /// <summary>
    /// Records gameplay callbacks without inheriting the update-component implementation.
    /// </summary>
    class EditorPlainLifecycleProbeComponent : Component, IUpdateable {
        /// <summary>Counts all gameplay lifecycle callbacks received by this instance.</summary>
        public int CallbackCount { get; private set; }

        /// <summary>Counts explicit update dispatch independently from lifecycle dispatch.</summary>
        public int UpdateCount { get; private set; }

        /// <inheritdoc />
        public byte UpdateOrder { get; set; }

        /// <inheritdoc />
        public void Update() { UpdateCount++; }

        /// <inheritdoc />
        public override void ComponentAdded(Entity entity) { CallbackCount++; }

        /// <inheritdoc />
        public override void ComponentInitialized(Entity entity) { CallbackCount++; }

        /// <inheritdoc />
        public override void ComponentRemoved(Entity entity) { CallbackCount++; }

        /// <inheritdoc />
        public override void ParentEnabledChange(bool newEnabled) { CallbackCount++; }

        /// <inheritdoc />
        public override void ParentStaticChange(bool newEnabled) { CallbackCount++; }
    }
}
