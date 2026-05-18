namespace helengine {
    /// <summary>
    /// Stores one reusable set of scalar runtime memory counters for allocation-free steady-state diagnostics sampling.
    /// </summary>
    public sealed class RuntimeMemoryCounters {
        /// <summary>
        /// Gets or sets the current resident memory size in bytes.
        /// </summary>
        public ulong ResidentBytes { get; set; }

        /// <summary>
        /// Gets or sets the peak resident memory size in bytes.
        /// </summary>
        public ulong PeakResidentBytes { get; set; }

        /// <summary>
        /// Gets or sets the current committed memory size in bytes.
        /// </summary>
        public ulong CommittedBytes { get; set; }

        /// <summary>
        /// Gets or sets the peak committed memory size in bytes.
        /// </summary>
        public ulong PeakCommittedBytes { get; set; }

        /// <summary>
        /// Gets or sets the currently available physical memory size in bytes.
        /// </summary>
        public ulong AvailablePhysicalBytes { get; set; }

        /// <summary>
        /// Gets or sets the current process page-fault count.
        /// </summary>
        public ulong PageFaultCount { get; set; }

        /// <summary>
        /// Clears every stored counter so one reusable instance can be repopulated safely.
        /// </summary>
        public void Reset() {
            ResidentBytes = 0u;
            PeakResidentBytes = 0u;
            CommittedBytes = 0u;
            PeakCommittedBytes = 0u;
            AvailablePhysicalBytes = 0u;
            PageFaultCount = 0u;
        }

        /// <summary>
        /// Copies the scalar counters from one rich diagnostics snapshot into this reusable container.
        /// </summary>
        /// <param name="snapshot">Rich diagnostics snapshot that should supply the scalar memory counters.</param>
        public void CopyFromSnapshot(RuntimeMemoryDiagnosticsSnapshot snapshot) {
            if (snapshot == null) {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ResidentBytes = snapshot.ResidentBytes;
            PeakResidentBytes = snapshot.PeakResidentBytes;
            CommittedBytes = snapshot.CommittedBytes;
            PeakCommittedBytes = snapshot.PeakCommittedBytes;
            AvailablePhysicalBytes = snapshot.AvailablePhysicalBytes;
            PageFaultCount = snapshot.PageFaultCount;
        }
    }
}
