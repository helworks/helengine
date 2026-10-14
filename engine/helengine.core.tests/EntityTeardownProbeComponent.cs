using helengine;

namespace helengine.core.tests {
    /// <summary>Records detach callbacks and final disposal separately so collection removal cannot substitute for lifecycle teardown.</summary>
    public sealed class EntityTeardownProbeComponent : Component {
        /// <summary>Gets the number of completed attach callbacks.</summary>
        public int AddedCount { get; private set; }
        /// <summary>Gets the number of completed detach callbacks.</summary>
        public int RemovedCount { get; private set; }
        /// <summary>Gets the number of final resource disposal calls.</summary>
        public int DisposeCount { get; private set; }
        /// <summary>Gets whether every detach callback observed a live component still associated with its owner.</summary>
        public bool RemovedWhileAttached { get; private set; } = true;
        /// <summary>Records attachment without adding unrelated rendering or input behavior.</summary>
        /// <param name="entity">Entity that has already attached this component.</param>
        public override void ComponentAdded(Entity entity) {
            AddedCount++;
        }
        /// <summary>Verifies the component remains usable until its detach callback finishes.</summary>
        /// <param name="entity">Owner that is releasing the attachment.</param>
        public override void ComponentRemoved(Entity entity) {
            ThrowIfDisposed();
            RemovedWhileAttached = RemovedWhileAttached && ReferenceEquals(Parent, entity);
            RemovedCount++;
        }
        /// <summary>Counts final cleanup and marks the component disposed using the real base implementation.</summary>
        public override void Dispose() {
            DisposeCount++;
            base.Dispose();
        }
    }
}
