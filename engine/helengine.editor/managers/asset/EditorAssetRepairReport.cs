namespace helengine.editor {
    /// <summary>
    /// Collects automatic asset repair diagnostics for one authoring session.
    /// </summary>
    public sealed class EditorAssetRepairReport {
        readonly object SyncRoot = new object();
        readonly List<EditorAssetRepairRecord> MutableRecords = new List<EditorAssetRepairRecord>();

        /// <summary>
        /// Creates an empty report for a new authoring session.
        /// </summary>
        public EditorAssetRepairReport() {
        }

        /// <summary>
        /// Gets an immutable snapshot of all records in append order.
        /// </summary>
        public IReadOnlyList<EditorAssetRepairRecord> Records {
            get {
                lock (SyncRoot) {
                    return Array.AsReadOnly(MutableRecords.ToArray());
                }
            }
        }

        /// <summary>
        /// Gets an immutable snapshot of all records.
        /// </summary>
        public IReadOnlyList<EditorAssetRepairRecord> Snapshot => Records;

        /// <summary>Gets the number of recorded mutation events.</summary>
        public int Count {
            get {
                lock (SyncRoot) {
                    return MutableRecords.Count;
                }
            }
        }

        /// <summary>
        /// Appends one immutable repair record for one mutation event.
        /// </summary>
        public void Append(EditorAssetRepairRecord record) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            lock (SyncRoot) {
                MutableRecords.Add(record);
            }
        }

        /// <summary>Appends one immutable repair record.</summary>
        public void Add(EditorAssetRepairRecord record) => Append(record);

        /// <summary>
        /// Returns the current human-readable summary.
        /// </summary>
        /// <returns>An empty summary until a repair is recorded by the repair service.</returns>
        public string CreateSummary() {
            EditorAssetRepairRecord[] records = Records.ToArray();
            if (records.Length == 0) {
                return string.Empty;
            }

            Dictionary<EditorAssetRepairKind, int> counts = new Dictionary<EditorAssetRepairKind, int>();
            for (int index = 0; index < records.Length; index++) {
                counts.TryGetValue(records[index].Kind, out int count);
                counts[records[index].Kind] = count + 1;
            }

            string details = string.Join(", ", counts
                .OrderBy(pair => pair.Key)
                .Select(pair => $"{pair.Key}={pair.Value}"));
            return $"Asset repairs: {records.Length} ({details})";
        }
    }
}
