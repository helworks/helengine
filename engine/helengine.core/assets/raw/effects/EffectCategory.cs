namespace helengine {
    /// <summary>
    /// Says where an effect can be used: on one layer's image, or between two scenes as a transition.
    /// </summary>
    public enum EffectCategory {
        /// <summary>
        /// Processes one layer; the first input is the layer image and other inputs are bound media.
        /// </summary>
        Layer = 0,

        /// <summary>
        /// Blends two scenes over the transition window; the inputs are the outgoing scene then the incoming scene,
        /// and the normalized time is the transition progress.
        /// </summary>
        Transition = 1
    }
}
