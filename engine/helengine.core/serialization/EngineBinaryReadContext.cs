namespace helengine {
    /// <summary>
    /// Stores the current binary-read context so failures can report the active asset file and read stage.
    /// </summary>
    public static class EngineBinaryReadContext {
        /// <summary>
        /// Tracks the asset path currently being deserialized by the active runtime read operation.
        /// </summary>
        static string CurrentAssetPathValue = string.Empty;
        /// <summary>
        /// Tracks the current binary-read stage within the active runtime read operation.
        /// </summary>
        static string CurrentReadStageValue = string.Empty;
        /// <summary>
        /// Tracks the last successfully completed binary-read checkpoint within the active runtime read operation.
        /// </summary>
        static string LastCheckpointValue = string.Empty;

        /// <summary>
        /// Gets or sets the current asset path associated with engine binary reads.
        /// </summary>
        public static string CurrentAssetPath {
            get { return CurrentAssetPathValue; }
            set {
                if (value == null) {
                    CurrentAssetPathValue = string.Empty;
                } else {
                    CurrentAssetPathValue = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the current stage associated with engine binary reads.
        /// </summary>
        public static string CurrentReadStage {
            get { return CurrentReadStageValue; }
            set {
                if (value == null) {
                    CurrentReadStageValue = string.Empty;
                } else {
                    CurrentReadStageValue = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the last successfully completed binary-read checkpoint.
        /// </summary>
        public static string LastCheckpoint {
            get { return LastCheckpointValue; }
            set {
                if (value == null) {
                    LastCheckpointValue = string.Empty;
                } else {
                    LastCheckpointValue = value;
                }
            }
        }
    }
}
