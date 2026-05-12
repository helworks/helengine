namespace helengine.editor.tests.testing {
    /// <summary>
    /// Records the delta-time values that were visible from the current core during update execution.
    /// </summary>
    internal class TestDeltaTimeProbeComponent : UpdateComponent {
        /// <summary>
        /// Gets the most recent scaled delta time observed by the component.
        /// </summary>
        public float LastObservedDeltaTime { get; private set; }

        /// <summary>
        /// Gets the most recent unscaled delta time observed by the component.
        /// </summary>
        public float LastObservedUnscaledDeltaTime { get; private set; }

        /// <summary>
        /// Gets the number of update callbacks that observed a non-zero delta.
        /// </summary>
        public int ObservedUpdateCount { get; private set; }

        /// <summary>
        /// Records the current core delta values during the update callback.
        /// </summary>
        public override void Update() {
            base.Update();
            LastObservedDeltaTime = Core.Instance.DeltaTime;
            LastObservedUnscaledDeltaTime = Core.Instance.UnscaledDeltaTime;
            if (LastObservedDeltaTime > 0f) {
                ObservedUpdateCount++;
            }
        }
    }
}
