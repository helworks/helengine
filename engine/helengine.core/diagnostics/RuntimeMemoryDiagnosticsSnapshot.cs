namespace helengine {
    /// <summary>
    /// Stores one portable runtime memory snapshot captured for diagnostics.
    /// </summary>
    public sealed class RuntimeMemoryDiagnosticsSnapshot : IDisposable {
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
        /// Gets or sets the current available physical memory size in bytes when known.
        /// </summary>
        public ulong AvailablePhysicalBytes { get; set; }

        /// <summary>
        /// Gets or sets the current process page-fault count when known.
        /// </summary>
        public ulong PageFaultCount { get; set; }

        /// <summary>
        /// Gets or sets the currently tracked loaded scene ids represented by this snapshot.
        /// </summary>
        public List<string> TrackedSceneIds { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets optional platform-specific detail metrics attached to this snapshot.
        /// </summary>
        public List<RuntimeDiagnosticsMetric> DetailMetrics { get; set; } = new List<RuntimeDiagnosticsMetric>();

        /// <summary>
        /// Releases the transient lists owned by this snapshot so native exports can delete the full object graph deterministically.
        /// </summary>
        public void Dispose() {
            if (DetailMetrics != null) {
                for (int index = 0; index < DetailMetrics.Count; index++) {
                    NativeOwnership.Delete(DetailMetrics[index]);
                }

                DetailMetrics.Clear();
                NativeOwnership.Delete(DetailMetrics);
                DetailMetrics = null;
            }

            if (TrackedSceneIds != null) {
                TrackedSceneIds.Clear();
                NativeOwnership.Delete(TrackedSceneIds);
                TrackedSceneIds = null;
            }
        }
    }
}
