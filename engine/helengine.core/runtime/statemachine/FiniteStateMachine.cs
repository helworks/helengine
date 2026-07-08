namespace helengine {
    /// <summary>
    /// Provides one reusable finite state machine for enum-backed runtime systems.
    /// </summary>
    public sealed class FiniteStateMachine<TState> where TState : struct {
        /// <summary>
        /// Stores the registered state definitions keyed by state value.
        /// </summary>
        readonly Dictionary<TState, FiniteStateDefinition<TState>> StateDefinitionsByState;

        /// <summary>
        /// Stores the registered guarded transitions keyed by source and target state.
        /// </summary>
        readonly Dictionary<FiniteStateTransitionKey<TState>, FiniteStateTransition<TState>> TransitionsByKey;

        /// <summary>
        /// Stores the currently active state after initialization succeeds.
        /// </summary>
        TState CurrentStateValue;

        /// <summary>
        /// Stores the previously active state from the last successful transition.
        /// </summary>
        TState PreviousStateValue;

        /// <summary>
        /// Initializes one empty finite state machine.
        /// </summary>
        public FiniteStateMachine() {
            StateDefinitionsByState = new Dictionary<TState, FiniteStateDefinition<TState>>();
            TransitionsByKey = new Dictionary<FiniteStateTransitionKey<TState>, FiniteStateTransition<TState>>();
        }

        /// <summary>
        /// Gets the currently active state.
        /// </summary>
        public TState CurrentState {
            get {
                EnsureInitialized();
                return CurrentStateValue;
            }
        }

        /// <summary>
        /// Gets the previously active state from the last successful transition.
        /// </summary>
        public TState PreviousState {
            get {
                EnsureInitialized();
                return PreviousStateValue;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the machine has entered one starting state yet.
        /// </summary>
        public bool HasCurrentState { get; private set; }

        /// <summary>
        /// Registers one state definition before initialization.
        /// </summary>
        /// <param name="state">State to register.</param>
        /// <param name="definition">Definition associated with the state.</param>
        public void RegisterState(TState state, FiniteStateDefinition<TState> definition) {
            ValidateStateType();
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            } else if (StateDefinitionsByState.ContainsKey(state)) {
                throw new InvalidOperationException("Finite state machine states may only be registered once.");
            }

            StateDefinitionsByState.Add(state, definition);
        }

        /// <summary>
        /// Registers one optional guarded transition between two states.
        /// </summary>
        /// <param name="fromState">Source state.</param>
        /// <param name="toState">Target state.</param>
        /// <param name="canTransition">Optional guard.</param>
        public void RegisterTransition(TState fromState, TState toState, Func<bool> canTransition = null) {
            EnsureStateRegisteredForTransition(fromState);
            EnsureStateRegisteredForTransition(toState);
            FiniteStateTransitionKey<TState> key = new FiniteStateTransitionKey<TState>(fromState, toState);
            TransitionsByKey[key] = new FiniteStateTransition<TState> {
                FromState = fromState,
                ToState = toState,
                CanTransition = canTransition
            };
        }

        /// <summary>
        /// Initializes the machine at one registered starting state.
        /// </summary>
        /// <param name="initialState">Registered starting state.</param>
        public void Initialize(TState initialState) {
            ValidateStateType();
            if (HasCurrentState) {
                throw new InvalidOperationException("Finite state machine may only be initialized once.");
            } else if (!StateDefinitionsByState.ContainsKey(initialState)) {
                throw new InvalidOperationException("Finite state machine states must be registered before they can be used.");
            }

            CurrentStateValue = initialState;
            PreviousStateValue = initialState;
            HasCurrentState = true;
            ResolveRequiredDefinition(initialState).OnEnter?.Invoke(initialState);
        }

        /// <summary>
        /// Determines whether the machine may move into the supplied target state.
        /// </summary>
        /// <param name="nextState">Requested target state.</param>
        /// <returns><c>true</c> when the state change may proceed; otherwise <c>false</c>.</returns>
        public bool CanChangeState(TState nextState) {
            EnsureInitialized();
            EnsureStateRegisteredForTransition(nextState);
            if (EqualityComparer<TState>.Default.Equals(CurrentStateValue, nextState)) {
                return false;
            }

            if (!TransitionsByKey.TryGetValue(
                new FiniteStateTransitionKey<TState>(CurrentStateValue, nextState),
                out FiniteStateTransition<TState> transition)) {
                return true;
            }

            return transition.CanTransition == null || transition.CanTransition();
        }

        /// <summary>
        /// Attempts one transition into the supplied target state.
        /// </summary>
        /// <param name="nextState">Requested target state.</param>
        /// <returns><c>true</c> when the transition ran; otherwise <c>false</c>.</returns>
        public bool TryChangeState(TState nextState) {
            if (!CanChangeState(nextState)) {
                return false;
            }

            TState previousState = CurrentStateValue;
            ResolveRequiredDefinition(previousState).OnExit?.Invoke(previousState);
            PreviousStateValue = previousState;
            CurrentStateValue = nextState;
            ResolveRequiredDefinition(nextState).OnEnter?.Invoke(nextState);
            return true;
        }

        /// <summary>
        /// Validates that the generic state type is one enum-backed value type.
        /// </summary>
        void ValidateStateType() {
            if (!typeof(TState).IsEnum) {
                throw new InvalidOperationException("Finite state machine state types must be enum value types.");
            }
        }

        /// <summary>
        /// Ensures one state registration exists for the supplied value before transition setup or evaluation proceeds.
        /// </summary>
        /// <param name="state">State to validate.</param>
        void EnsureStateRegisteredForTransition(TState state) {
            ValidateStateType();
            if (!StateDefinitionsByState.ContainsKey(state)) {
                throw new InvalidOperationException("Finite state machine transitions require both endpoint states to be registered first.");
            }
        }

        /// <summary>
        /// Ensures the machine has one active state before state queries or changes proceed.
        /// </summary>
        void EnsureInitialized() {
            if (!HasCurrentState) {
                throw new InvalidOperationException("Finite state machine must be initialized before it can be queried or advanced.");
            }
        }

        /// <summary>
        /// Resolves the required state definition for one registered state.
        /// </summary>
        /// <param name="state">State whose definition should be returned.</param>
        /// <returns>Registered definition.</returns>
        FiniteStateDefinition<TState> ResolveRequiredDefinition(TState state) {
            return StateDefinitionsByState[state];
        }
    }
}
