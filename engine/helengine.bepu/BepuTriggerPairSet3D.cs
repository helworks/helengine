namespace helengine {
    /// <summary>
    /// Stores a deduplicated set of trigger overlap pairs for one fixed simulation step.
    /// BEPU narrow-phase callbacks run on the simulation's worker threads, so every mutation is serialized while
    /// the ordered pair list is kept alongside the hash lookup to preserve a deterministic single-threaded replay.
    /// </summary>
    public sealed class BepuTriggerPairSet3D {
        /// <summary>
        /// Monitor guarding both backing collections against concurrent narrow-phase workers.
        /// </summary>
        readonly object SyncRootValue = new object();

        /// <summary>
        /// Tracked pairs in the order they were first recorded.
        /// </summary>
        readonly List<TriggerPairKey3D> PairsValue = new List<TriggerPairKey3D>();

        /// <summary>
        /// Hash lookup mirroring <see cref="PairsValue"/> so membership tests stay constant time.
        /// </summary>
        readonly HashSet<TriggerPairKey3D> PairLookupValue = new HashSet<TriggerPairKey3D>();

        /// <summary>
        /// Gets the tracked pairs in recording order. Only read once the owning simulation step has completed.
        /// </summary>
        public IReadOnlyList<TriggerPairKey3D> Pairs => PairsValue;

        /// <summary>
        /// Removes every tracked pair.
        /// </summary>
        public void Clear() {
            lock (SyncRootValue) {
                PairsValue.Clear();
                PairLookupValue.Clear();
            }
        }

        /// <summary>
        /// Tracks one trigger overlap pair, ignoring pairs that are already tracked.
        /// </summary>
        /// <param name="pairKey">Trigger overlap pair to track.</param>
        public void Add(TriggerPairKey3D pairKey) {
            lock (SyncRootValue) {
                if (PairLookupValue.Contains(pairKey)) {
                    return;
                }

                PairLookupValue.Add(pairKey);
                PairsValue.Add(pairKey);
            }
        }

        /// <summary>
        /// Determines whether one trigger overlap pair is currently tracked.
        /// </summary>
        /// <param name="pairKey">Trigger overlap pair to look up.</param>
        /// <returns>True when the pair is tracked.</returns>
        public bool Contains(TriggerPairKey3D pairKey) {
            lock (SyncRootValue) {
                return PairLookupValue.Contains(pairKey);
            }
        }

        /// <summary>
        /// Replaces every tracked pair with the pairs tracked by another set, preserving their recording order.
        /// </summary>
        /// <param name="source">Set whose pairs should be copied.</param>
        public void ReplaceWith(BepuTriggerPairSet3D source) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }

            IReadOnlyList<TriggerPairKey3D> sourcePairs = source.Pairs;
            lock (SyncRootValue) {
                PairsValue.Clear();
                PairLookupValue.Clear();
                for (int index = 0; index < sourcePairs.Count; index++) {
                    TriggerPairKey3D pairKey = sourcePairs[index];
                    if (PairLookupValue.Contains(pairKey)) {
                        continue;
                    }

                    PairLookupValue.Add(pairKey);
                    PairsValue.Add(pairKey);
                }
            }
        }
    }
}
