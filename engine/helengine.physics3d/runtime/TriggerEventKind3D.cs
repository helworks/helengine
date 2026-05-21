namespace helengine {
    /// <summary>
    /// Identifies the trigger overlap lifecycle transition emitted during one fixed physics step.
    /// </summary>
    public enum TriggerEventKind3D : byte {
        /// <summary>
        /// The overlap was first detected during the current step.
        /// </summary>
        Enter = 0,

        /// <summary>
        /// The overlap was already active and remained active during the current step.
        /// </summary>
        Stay = 1,

        /// <summary>
        /// The overlap was active during the previous step and ended during the current step.
        /// </summary>
        Exit = 2
    }
}
