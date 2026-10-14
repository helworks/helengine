namespace helengine.baseplatform.Player {
    /// <summary>
    /// How large the Play window is relative to the platform render size: a fixed integer multiple, or the largest
    /// integer multiple that fits the chosen monitor's work area.
    /// </summary>
    public enum PlayerWindowScale {
        /// <summary>The window client equals the render size.</summary>
        One,

        /// <summary>The window client is twice the render size.</summary>
        Two,

        /// <summary>The window client is three times the render size.</summary>
        Three,

        /// <summary>The window client is four times the render size.</summary>
        Four,

        /// <summary>The largest integer multiple (at least one) whose window fits the monitor work area.</summary>
        Fit
    }
}
