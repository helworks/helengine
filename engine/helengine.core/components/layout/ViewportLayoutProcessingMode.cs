namespace helengine {
    /// <summary>Chooses when a viewport resolves its authored UI layout.</summary>
    public enum ViewportLayoutProcessingMode : byte {
        /// <summary>Retains typed original values and recomposes after presentation changes.</summary>
        Dynamic = 0,
        /// <summary>Resolves loaded values once without retaining their originals.</summary>
        OnLoad = 1,
        /// <summary>Uses final values prepared by the scene cooker.</summary>
        Cook = 2
    }
}
