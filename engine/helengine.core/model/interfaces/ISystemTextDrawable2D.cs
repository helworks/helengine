namespace helengine {
    /// <summary>Opt-in system typography carried by existing text components without changing authored bitmap-font drawables.</summary>
    public interface ISystemTextDrawable2D {
        /// <summary>Gets the immutable system style, or null to retain bitmap-font rendering.</summary>
        SystemTextStyle SystemTextStyle { get; }
    }
}
