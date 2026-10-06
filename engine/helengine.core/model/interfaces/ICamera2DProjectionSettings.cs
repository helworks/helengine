namespace helengine {
    /// <summary>Allows an offscreen camera to project authored 2D coordinates independently of its render-target resolution.</summary>
    public interface ICamera2DProjectionSettings {
        /// <summary>Gets the positive logical canvas dimensions used by 2D projection and clipping.</summary>
        int2 LogicalViewportSize { get; }
    }
}
