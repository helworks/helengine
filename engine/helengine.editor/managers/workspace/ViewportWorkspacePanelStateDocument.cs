namespace helengine.editor {
    /// <summary>
    /// Represents the persisted per-instance state for one workspace-managed viewport panel.
    /// </summary>
    public sealed class ViewportWorkspacePanelStateDocument {
        /// <summary>
        /// Camera entity world position X component.
        /// </summary>
        public float CameraPositionX { get; set; }
        /// <summary>
        /// Camera entity world position Y component.
        /// </summary>
        public float CameraPositionY { get; set; }
        /// <summary>
        /// Camera entity world position Z component.
        /// </summary>
        public float CameraPositionZ { get; set; }
        /// <summary>
        /// Camera entity world orientation quaternion X component.
        /// </summary>
        public float CameraOrientationX { get; set; }
        /// <summary>
        /// Camera entity world orientation quaternion Y component.
        /// </summary>
        public float CameraOrientationY { get; set; }
        /// <summary>
        /// Camera entity world orientation quaternion Z component.
        /// </summary>
        public float CameraOrientationZ { get; set; }
        /// <summary>
        /// Camera entity world orientation quaternion W component.
        /// </summary>
        public float CameraOrientationW { get; set; }
        /// <summary>
        /// Viewport-local active tool mode.
        /// </summary>
        public EditorViewportToolMode ToolMode { get; set; }
        /// <summary>
        /// Viewport-local near clip plane distance.
        /// </summary>
        public float NearPlaneDistance { get; set; }
        /// <summary>
        /// Viewport-local far clip plane distance.
        /// </summary>
        public float FarPlaneDistance { get; set; }
        /// <summary>
        /// Viewport-local camera speed mode.
        /// </summary>
        public byte CameraSpeedMode { get; set; }
        /// <summary>
        /// Viewport-local manual camera speed override value.
        /// </summary>
        public double ManualCameraSpeedOverride { get; set; }
        /// <summary>
        /// Viewport-local simulated canvas width.
        /// </summary>
        public int CanvasWidth { get; set; }
        /// <summary>
        /// Viewport-local simulated canvas height.
        /// </summary>
        public int CanvasHeight { get; set; }
        /// <summary>
        /// Viewport-local simulated canvas density in pixels per world unit.
        /// </summary>
        public int PixelsPerWorldUnit { get; set; }
        /// <summary>
        /// True when the viewport grid was visible when the state was captured.
        /// </summary>
        public bool IsGridVisible { get; set; }
        /// <summary>
        /// True when the viewport settings overlay was open when the state was captured.
        /// </summary>
        public bool IsSettingsOverlayOpen { get; set; }
        /// <summary>
        /// Translation snap value used by the first viewport snap slot.
        /// </summary>
        public double TranslateSnap1 { get; set; }
        /// <summary>
        /// Translation snap value used by the second viewport snap slot.
        /// </summary>
        public double TranslateSnap2 { get; set; }
        /// <summary>
        /// Rotation snap value used by the first viewport snap slot.
        /// </summary>
        public double RotateSnap1 { get; set; }
        /// <summary>
        /// Rotation snap value used by the second viewport snap slot.
        /// </summary>
        public double RotateSnap2 { get; set; }
        /// <summary>
        /// Scale snap value used by the first viewport snap slot.
        /// </summary>
        public double ScaleSnap1 { get; set; }
        /// <summary>
        /// Scale snap value used by the second viewport snap slot.
        /// </summary>
        public double ScaleSnap2 { get; set; }
    }
}
