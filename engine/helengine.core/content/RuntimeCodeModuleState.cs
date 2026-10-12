namespace helengine {
    /// <summary>Retains the native handle and scene reference count for one validated manifest entry.</summary>
    internal sealed class RuntimeCodeModuleState : IDisposable {
        /// <summary>Borrowed immutable declaration owned by the host-provided module manifest.</summary>
        public readonly RuntimeCodeModuleManifestEntry Entry;
        /// <summary>Owned resident adapter whose code remains loaded until callbacks and factories have been removed.</summary>
        [NativeOwnedMember]
        public IRuntimeCodeModule Module;
        /// <summary>Number of active scene or in-progress acquisition leases that require this module.</summary>
        public int ReferenceCount;
        /// <summary>Initializes unloaded state for a manifest declaration.</summary>
        /// <param name="entry">Validated module declaration retained by the manager's manifest.</param>
        public RuntimeCodeModuleState(RuntimeCodeModuleManifestEntry entry) { Entry = entry; }

        /// <summary>Releases an empty state only after the manager has removed callbacks, factories and its native module handle in that order.</summary>
        public void Dispose() {
            if (Module != null || ReferenceCount != 0) {
                throw new InvalidOperationException("Native module state cannot be disposed while a handle or acquisition remains active.");
            }
            NativeOwnership.Release(ref Module);
        }
    }
}
