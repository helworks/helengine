namespace helengine.editor {
    /// <summary>
    /// Defines how one editor slider maps normalized track positions into authored values.
    /// </summary>
    public enum EditorSliderScaleMode {
        /// <summary>
        /// Maps the track position linearly between the authored minimum and maximum values.
        /// </summary>
        Linear,
        /// <summary>
        /// Maps the track position logarithmically so wide numeric ranges remain usable.
        /// </summary>
        Logarithmic
    }
}
