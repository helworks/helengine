namespace helengine.editor {
    /// <summary>
    /// Represents the persisted per-instance state for one workspace-managed viewport panel.
    /// </summary>
    public sealed class ViewportWorkspacePanelStateDocument {
        /// <summary>
        /// Camera entity world position X component.
        /// </summary>
        public double CameraPositionX { get; set; }
        /// <summary>
        /// Camera entity world position Y component.
        /// </summary>
        public double CameraPositionY { get; set; }
        /// <summary>
        /// Camera entity world position Z component.
        /// </summary>
        public double CameraPositionZ { get; set; }
        /// <summary>
        /// Camera entity world orientation quaternion X component.
        /// </summary>
        public double CameraOrientationX { get; set; }
        /// <summary>
        /// Camera entity world orientation quaternion Y component.
        /// </summary>
        public double CameraOrientationY { get; set; }
        /// <summary>
        /// Camera entity world orientation quaternion Z component.
        /// </summary>
        public double CameraOrientationZ { get; set; }
        /// <summary>
        /// Camera entity world orientation quaternion W component.
        /// </summary>
        public double CameraOrientationW { get; set; }
        /// <summary>
        /// Viewport-local active tool mode.
        /// </summary>
        public EditorViewportToolMode ToolMode { get; set; }
        /// <summary>
        /// Viewport-local near clip plane distance.
        /// </summary>
        public double NearPlaneDistance { get; set; }
        /// <summary>
        /// Viewport-local far clip plane distance.
        /// </summary>
        public double FarPlaneDistance { get; set; }
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
        /// Projection mode used by this viewport's editor camera. Missing fields in older workspace documents resolve to perspective.
        /// </summary>
        public CameraProjectionMode ProjectionMode { get; set; } = CameraProjectionMode.Perspective;
        /// <summary>
        /// Preserved orthographic vertical span used by this viewport camera.
        /// </summary>
        public double OrthographicVerticalSpan { get; set; }
        /// <summary>
        /// Indicates whether the saved orbit pivot is valid, including when it is the world origin.
        /// </summary>
        public bool HasOrbitPivot { get; set; }
        /// <summary>
        /// X coordinate of the viewport camera orbit pivot.
        /// </summary>
        public double OrbitPivotX { get; set; }
        /// <summary>
        /// Y coordinate of the viewport camera orbit pivot.
        /// </summary>
        public double OrbitPivotY { get; set; }
        /// <summary>
        /// Z coordinate of the viewport camera orbit pivot.
        /// </summary>
        public double OrbitPivotZ { get; set; }
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
