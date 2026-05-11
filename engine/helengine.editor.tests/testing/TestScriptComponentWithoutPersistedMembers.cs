namespace helengine.editor.tests.testing {
    /// <summary>
    /// Scripted component used to verify packaging behavior for automatic components that expose no persisted public members.
    /// </summary>
    public sealed class TestScriptComponentWithoutPersistedMembers : Component {
        /// <summary>
        /// Tracks transient runtime state that should never participate in automatic persistence.
        /// </summary>
        int TransientCounter;

        /// <summary>
        /// Increments the transient runtime state to prove the component can carry non-persisted implementation details.
        /// </summary>
        public void Tick() {
            TransientCounter++;
        }
    }
}
