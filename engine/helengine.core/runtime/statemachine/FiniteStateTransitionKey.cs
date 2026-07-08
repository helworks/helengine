namespace helengine {
    /// <summary>
    /// Provides one dictionary-safe value key for a transition edge between two finite state machine states.
    /// </summary>
    public readonly struct FiniteStateTransitionKey<TState> : IEquatable<FiniteStateTransitionKey<TState>> where TState : struct {
        /// <summary>
        /// Gets the source state for the keyed transition.
        /// </summary>
        public TState FromState { get; }

        /// <summary>
        /// Gets the target state for the keyed transition.
        /// </summary>
        public TState ToState { get; }

        /// <summary>
        /// Initializes one transition key for the supplied source and target states.
        /// </summary>
        /// <param name="fromState">Source state.</param>
        /// <param name="toState">Target state.</param>
        public FiniteStateTransitionKey(TState fromState, TState toState) {
            FromState = fromState;
            ToState = toState;
        }

        /// <summary>
        /// Determines whether this key matches another transition key.
        /// </summary>
        /// <param name="other">Other key to compare.</param>
        /// <returns><c>true</c> when both endpoints match; otherwise <c>false</c>.</returns>
        public bool Equals(FiniteStateTransitionKey<TState> other) {
            return EqualityComparer<TState>.Default.Equals(FromState, other.FromState)
                && EqualityComparer<TState>.Default.Equals(ToState, other.ToState);
        }

        /// <summary>
        /// Determines whether this key matches another object instance.
        /// </summary>
        /// <param name="obj">Object to compare.</param>
        /// <returns><c>true</c> when the object is one equal key; otherwise <c>false</c>.</returns>
        public override bool Equals(object obj) {
            return obj is FiniteStateTransitionKey<TState> other && Equals(other);
        }

        /// <summary>
        /// Builds the stable hash code used by transition dictionaries.
        /// </summary>
        /// <returns>Combined endpoint hash code.</returns>
        public override int GetHashCode() {
            return HashCode.Combine(FromState, ToState);
        }
    }
}
