namespace helengine {
    /// <summary>
    /// Stores the optional lifecycle callbacks associated with one registered finite state machine state.
    /// </summary>
    public sealed class FiniteStateDefinition<TState> where TState : struct {
        /// <summary>
        /// Gets or sets the callback invoked immediately after the machine enters the owning state.
        /// </summary>
        public Action<TState> OnEnter { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked immediately before the machine exits the owning state.
        /// </summary>
        public Action<TState> OnExit { get; set; }
    }
}
