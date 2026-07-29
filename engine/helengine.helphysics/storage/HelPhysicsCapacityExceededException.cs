namespace helengine {
    /// <summary>
    /// Reports an attempt to allocate from a fixed-capacity physics pool that has no free slots remaining.
    /// </summary>
    public sealed class HelPhysicsCapacityExceededException : InvalidOperationException {
        /// <summary>
        /// Gets the concise name of the exhausted pool.
        /// </summary>
        public string PoolName { get; }

        /// <summary>
        /// Gets the fixed number of slots configured for the exhausted pool.
        /// </summary>
        public int Capacity { get; }

        /// <summary>
        /// Initializes an exception that identifies the exhausted pool and its configured slot count.
        /// </summary>
        /// <param name="poolName">Concise name of the pool that rejected allocation.</param>
        /// <param name="capacity">Configured number of slots in the exhausted pool.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="poolName"/> is empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is negative.</exception>
        public HelPhysicsCapacityExceededException(string poolName, int capacity)
            : base(CreateMessage(poolName, capacity)) {
            if (string.IsNullOrWhiteSpace(poolName)) {
                throw new ArgumentException("A capacity exception requires the exhausted pool name.", nameof(poolName));
            }

            if (capacity < 0) {
                throw new ArgumentOutOfRangeException(nameof(capacity), "A capacity exception cannot report a negative pool capacity.");
            }

            PoolName = poolName;
            Capacity = capacity;
        }

        /// <summary>
        /// Creates the diagnostic message shared by capacity-exhaustion exceptions.
        /// </summary>
        /// <param name="poolName">Concise name of the exhausted pool.</param>
        /// <param name="capacity">Configured number of slots in the exhausted pool.</param>
        /// <returns>A message that identifies the exhausted fixed-capacity pool.</returns>
        static string CreateMessage(string poolName, int capacity) {
            if (string.IsNullOrWhiteSpace(poolName)) {
                throw new ArgumentException("A capacity exception requires the exhausted pool name.", nameof(poolName));
            }

            if (capacity < 0) {
                throw new ArgumentOutOfRangeException(nameof(capacity), "A capacity exception cannot report a negative pool capacity.");
            }

            return $"The {poolName} pool capacity of {capacity} has been exceeded.";
        }
    }
}
