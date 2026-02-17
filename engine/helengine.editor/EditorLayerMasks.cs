namespace helengine.editor {
    /// <summary>
    /// Defines layer masks used by editor rendering systems.
    /// </summary>
    public static class EditorLayerMasks {
        /// <summary>
        /// Layer mask used by 2D editor UI and panels.
        /// </summary>
        public const ushort EditorUi = 0b1000000000000000;

        /// <summary>
        /// Layer mask used by scene objects authored by the user.
        /// </summary>
        public const ushort SceneObjects = 0b0100000000000000;

        /// <summary>
        /// Layer mask used by in-scene editor gizmos.
        /// </summary>
        public const ushort SceneGizmo = 0b0010000000000000;
    }
}
