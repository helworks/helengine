namespace helengine;

/// <summary>
/// Describes how a control produces data for the input system.
/// </summary>
public enum InputControlKind {
    /// <summary>
    /// A digital button that is either active or inactive.
    /// </summary>
    Button,
    /// <summary>
    /// A signed analog axis.
    /// </summary>
    Axis,
    /// <summary>
    /// Pointer X or Y delta used for relative motion.
    /// </summary>
    PointerDelta,
    /// <summary>
    /// Pointer scroll wheel delta.
    /// </summary>
    ScrollWheel
}
