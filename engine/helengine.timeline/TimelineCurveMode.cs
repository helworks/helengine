namespace helengine.timeline {
    /// <summary>
    /// How the cooker writes easing curves for a platform profile.
    /// </summary>
    public enum TimelineCurveMode {
        /// <summary>
        /// Keep catalog curves as curve codes; the runtime evaluates them (segments cut mid-curve are still split into
        /// straight pieces).
        /// </summary>
        Native = 0,
        /// <summary>
        /// Split every curve into straight segments within the tolerance so the runtime only interpolates linearly
        /// (curve code 0). Meant for targets where evaluating curves per frame is too costly, such as PlayStation 1.
        /// </summary>
        Linearized = 1
    }
}
