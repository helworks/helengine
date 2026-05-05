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
        /// Layer mask used by the editor-only viewport grid.
        /// </summary>
        public const ushort SceneGrid = 0b0001000000000000;

        /// <summary>
        /// Layer mask used by in-scene editor gizmos.
        /// </summary>
        public const ushort SceneGizmo = 0b0010000000000000;

        /// <summary>
        /// Layer mask used by editor-only camera visual children.
        /// </summary>
        public const ushort SceneCameraVisuals = 0b0000100000000000;

        /// <summary>
        /// Layer mask used by Scene Hierarchy row visuals rendered inside the hierarchy viewport.
        /// </summary>
        public const ushort SceneHierarchyContent = 0b0000010000000000;

        /// <summary>
        /// Layer mask used by the world-space 2D canvas preview plane.
        /// </summary>
        public const ushort SceneCanvasPlane = 0b0000001000000000;
    }
}
