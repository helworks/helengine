namespace helengine {
    /// <summary>
    /// Stores one optional guarded transition between two registered finite state machine states.
    /// </summary>
    public sealed class FiniteStateTransition<TState> where TState : struct {
        /// <summary>
        /// Gets or sets the state being exited by this transition.
        /// </summary>
        public TState FromState { get; set; }

        /// <summary>
        /// Gets or sets the state being entered by this transition.
        /// </summary>
        public TState ToState { get; set; }

        /// <summary>
        /// Gets or sets the optional transition guard that decides whether the transition may proceed.
        /// </summary>
        public Func<bool> CanTransition { get; set; }
    }
}
