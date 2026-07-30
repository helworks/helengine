namespace helengine {
    /// <summary>
    /// Allocates process-local positive world ownership tokens monotonically and permanently latches exhaustion before wraparound.
    /// </summary>
    sealed class HelPhysicsWorldIdAllocator3D {
        /// <summary>
        /// Serializes infrequent world construction so exhaustion and the final positive assignment publish atomically.
        /// </summary>
        readonly object SynchronizationRoot;

        /// <summary>
        /// Stores the greatest positive ownership identifier already returned by this allocator.
        /// </summary>
        int LastAssignedId;

        /// <summary>
        /// Stores whether the positive integer range has been consumed permanently.
        /// </summary>
        bool Exhausted;

        /// <summary>
        /// Initializes a fresh process allocator before its first positive ownership token.
        /// </summary>
        internal HelPhysicsWorldIdAllocator3D()
            : this(0) {
        }

        /// <summary>
        /// Initializes a monotonic allocator after an explicitly restored non-negative sequence position.
        /// </summary>
        /// <param name="lastAssignedId">Greatest non-negative identifier considered already assigned.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the restored sequence position is negative.</exception>
        internal HelPhysicsWorldIdAllocator3D(int lastAssignedId) {
            if (lastAssignedId < 0) {
                throw new ArgumentOutOfRangeException(nameof(lastAssignedId), "World ownership sequences cannot begin below zero.");
            }

            SynchronizationRoot = new object();
            LastAssignedId = lastAssignedId;
        }

        /// <summary>
        /// Gets whether every positive token has been consumed and all future allocations must fail.
        /// </summary>
        internal bool IsExhausted {
            get {
                lock (SynchronizationRoot) {
                    return Exhausted;
                }
            }
        }

        /// <summary>
        /// Returns the next positive token exactly once or latches permanent exhaustion before integer wraparound.
        /// </summary>
        /// <returns>A process-local positive ownership token never previously returned by this allocator.</returns>
        /// <exception cref="InvalidOperationException">Thrown permanently after the final positive token has been assigned.</exception>
        internal uint Allocate() {
            lock (SynchronizationRoot) {
                if (Exhausted) {
                    throw new InvalidOperationException("The HelPhysics world ownership token range is exhausted.");
                }

                if (LastAssignedId == int.MaxValue) {
                    Exhausted = true;
                    throw new InvalidOperationException("The HelPhysics world ownership token range is exhausted.");
                }

                LastAssignedId++;
                return (uint)LastAssignedId;
            }
        }
    }
}
