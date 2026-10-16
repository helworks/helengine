namespace helengine.editor.tests.testing {
    /// <summary>Represents a game script derived from an editor-visible rendering component.</summary>
    class EditorUnmarkedSpriteProbeComponent : SpriteComponent {
        /// <summary>Counts attach callbacks, which must remain suppressed without an explicit opt-in.</summary>
        public int AddedCount { get; private set; }

        /// <inheritdoc />
        public override void ComponentAdded(Entity entity) { AddedCount++; }
    }
}
