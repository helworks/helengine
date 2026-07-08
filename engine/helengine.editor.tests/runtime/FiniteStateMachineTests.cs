using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the reusable finite state machine runtime behavior.
    /// </summary>
    public sealed class FiniteStateMachineTests {
        /// <summary>
        /// Declares one representative enum-backed state set for runtime FSM tests.
        /// </summary>
        enum TestState {
            Waiting,
            Playing,
            Failed
        }

        /// <summary>
        /// Ensures initialization enters the starting state and exposes it through the current-state surface.
        /// </summary>
        [Fact]
        public void Initialize_WhenStartingStateIsRegistered_SetsCurrentStateAndRunsEnterHook() {
            List<string> events = new List<string>();
            FiniteStateMachine<TestState> machine = new FiniteStateMachine<TestState>();
            machine.RegisterState(TestState.Waiting, new FiniteStateDefinition<TestState> {
                OnEnter = state => events.Add($"enter:{state}")
            });

            machine.Initialize(TestState.Waiting);

            Assert.True(machine.HasCurrentState);
            Assert.Equal(TestState.Waiting, machine.CurrentState);
            Assert.Equal(new[] { "enter:Waiting" }, events);
        }

        /// <summary>
        /// Ensures one successful transition runs exit before enter and updates previous-state tracking.
        /// </summary>
        [Fact]
        public void TryChangeState_WhenGuardAllows_RunsExitThenEnterAndUpdatesPreviousState() {
            List<string> events = new List<string>();
            FiniteStateMachine<TestState> machine = new FiniteStateMachine<TestState>();
            machine.RegisterState(TestState.Waiting, new FiniteStateDefinition<TestState> {
                OnExit = state => events.Add($"exit:{state}")
            });
            machine.RegisterState(TestState.Playing, new FiniteStateDefinition<TestState> {
                OnEnter = state => events.Add($"enter:{state}")
            });
            machine.RegisterTransition(TestState.Waiting, TestState.Playing, () => true);
            machine.Initialize(TestState.Waiting);
            events.Clear();

            bool changed = machine.TryChangeState(TestState.Playing);

            Assert.True(changed);
            Assert.Equal(TestState.Waiting, machine.PreviousState);
            Assert.Equal(TestState.Playing, machine.CurrentState);
            Assert.Equal(new[] { "exit:Waiting", "enter:Playing" }, events);
        }

        /// <summary>
        /// Ensures a rejected guard leaves the active state untouched and skips lifecycle callbacks.
        /// </summary>
        [Fact]
        public void TryChangeState_WhenGuardRejects_LeavesCurrentStateAndSkipsHooks() {
            List<string> events = new List<string>();
            FiniteStateMachine<TestState> machine = new FiniteStateMachine<TestState>();
            machine.RegisterState(TestState.Waiting, new FiniteStateDefinition<TestState> {
                OnExit = state => events.Add($"exit:{state}")
            });
            machine.RegisterState(TestState.Failed, new FiniteStateDefinition<TestState> {
                OnEnter = state => events.Add($"enter:{state}")
            });
            machine.RegisterTransition(TestState.Waiting, TestState.Failed, () => false);
            machine.Initialize(TestState.Waiting);
            events.Clear();

            bool changed = machine.TryChangeState(TestState.Failed);

            Assert.False(changed);
            Assert.Equal(TestState.Waiting, machine.CurrentState);
            Assert.Empty(events);
        }

        /// <summary>
        /// Ensures registering one transition against an unregistered state fails fast.
        /// </summary>
        [Fact]
        public void RegisterTransition_WhenEndpointStateIsUnregistered_ThrowsInvalidOperationException() {
            FiniteStateMachine<TestState> machine = new FiniteStateMachine<TestState>();
            machine.RegisterState(TestState.Waiting, new FiniteStateDefinition<TestState>());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => machine.RegisterTransition(TestState.Waiting, TestState.Playing, () => true));

            Assert.Equal("Finite state machine transitions require both endpoint states to be registered first.", exception.Message);
        }

        /// <summary>
        /// Ensures non-enum generic state types are rejected during setup in the managed runtime path.
        /// </summary>
        [Fact]
        public void RegisterState_WhenStateTypeIsNotEnum_ThrowsInvalidOperationException() {
            FiniteStateMachine<int> machine = new FiniteStateMachine<int>();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => machine.RegisterState(1, new FiniteStateDefinition<int>()));

            Assert.Equal("Finite state machine state types must be enum value types.", exception.Message);
        }
    }
}
