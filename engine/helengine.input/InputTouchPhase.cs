namespace helengine {
    /// <summary>Describes one sample in a contact's ordered lifetime, including explicit cancellation.</summary>
    public enum InputTouchPhase {
        /// <summary>A new finger touched the surface and may capture a control.</summary>
        Began,
        /// <summary>An existing contact changed position or pressure.</summary>
        Moved,
        /// <summary>A contact remains held without a new sample this frame.</summary>
        Stationary,
        /// <summary>The finger lifted normally; controls may activate.</summary>
        Ended,
        /// <summary>The host interrupted the contact; controls must abandon it without activation.</summary>
        Canceled
    }
}
