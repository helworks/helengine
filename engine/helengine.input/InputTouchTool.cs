namespace helengine {
    /// <summary>Identifies a native contact source without guessing from pressure.</summary>
    public enum InputTouchTool {
        /// <summary>A finger or legacy backend contact without an explicit pen source.</summary>
        Finger,
        /// <summary>A native pen or Pencil tip contact.</summary>
        Pen,
        /// <summary>A native pen explicitly reported as an eraser tip.</summary>
        Eraser
    }
}
