using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>Records coarse builder samples against a deterministic clock and detects accidental ownership or unrelated profiling callbacks.</summary>
    public sealed class CommandBuilderProfileTestSink : IRuntimeCpuProfileSink, IDisposable {
        /// <summary>Number of clock reads performed by the builder.</summary>
        public int ClockReads { get; private set; }
        /// <summary>Number of backwards-clock notifications received.</summary>
        public int ClockFaults { get; private set; }
        /// <summary>Number of disposal calls, which must remain zero when only the borrowing builder is disposed.</summary>
        public int DisposeCalls { get; private set; }
        /// <summary>Whether the second clock read should simulate one backwards timestamp.</summary>
        public bool BackwardsSecondRead { get; set; }
        /// <summary>Ordered samples identify the exact completed nonoverlapping segments.</summary>
        public List<RuntimeCpuProfileStage> Stages { get; } = new List<RuntimeCpuProfileStage>();
        /// <summary>Durations corresponding to the ordered stage samples.</summary>
        public List<ulong> Durations { get; } = new List<ulong>();

        /// <summary>Advances ten microseconds per read, with an optional backwards second read for validation.</summary>
        /// <returns>The deterministic timestamp.</returns>
        public ulong ReadMicroseconds() {
            ClockReads++;
            return BackwardsSecondRead && ClockReads == 2 ? 0UL : (ulong)ClockReads * 10UL;
        }
        /// <summary>Stores one completed phase without taking ownership of any render object.</summary>
        /// <param name="stage">Stable builder phase identifier.</param>
        /// <param name="elapsedMicroseconds">Duration measured around that phase.</param>
        public void RecordStage(RuntimeCpuProfileStage stage, ulong elapsedMicroseconds) {
            Stages.Add(stage);
            Durations.Add(elapsedMicroseconds);
        }
        /// <summary>Rejects an updateable callback because this sink is attached only to the command builder.</summary>
        /// <param name="item">Unexpected updateable.</param>
        /// <param name="index">Unexpected update index.</param>
        /// <param name="typeHash">Unexpected type hash.</param>
        /// <param name="ownerSceneEntityId">Unexpected owner.</param>
        /// <param name="elapsedMicroseconds">Unexpected duration.</param>
        public void RecordUpdateable(IUpdateable item, int index, uint typeHash, uint ownerSceneEntityId, ulong elapsedMicroseconds) {
            throw new InvalidOperationException("The command builder must not profile updateables.");
        }
        /// <summary>Rejects type registration because the builder never registers or owns scene components.</summary>
        /// <param name="item">Unexpected updateable.</param>
        /// <param name="typeHash">Unexpected type identity.</param>
        /// <param name="typeName">Unexpected type name.</param>
        public void RegisterType(IUpdateable item, uint typeHash, string typeName) {
            throw new InvalidOperationException("The command builder must not register updateable types.");
        }
        /// <summary>Records a backwards-clock notification without hiding it in a valid sample.</summary>
        public void ClockFault() { ClockFaults++; }
        /// <summary>Clears captured samples for the next build while preserving ownership diagnostics.</summary>
        public void ClearSamples() { ClockReads = 0; ClockFaults = 0; Stages.Clear(); Durations.Clear(); }
        /// <summary>Records explicit owner disposal to prove the builder did not dispose the borrowed sink.</summary>
        public void Dispose() { DisposeCalls++; }
    }
}
