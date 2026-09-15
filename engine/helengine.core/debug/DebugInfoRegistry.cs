namespace helengine {
    /// <summary>
    /// Provides debug information entries to be displayed by overlays.
    /// </summary>
    public interface IDebugInfoProvider {
        /// <summary>
        /// Gets the category label for the provider.
        /// </summary>
        string Category { get; }

        /// <summary>
        /// Appends this provider's debug info entries to the supplied collection.
        /// </summary>
        /// <param name="items">Collection to append entries to.</param>
        void AppendInfo([NativeNoEscape] List<DebugInfoEntry> items);
    }

    /// <summary>
    /// Registry for aggregating debug info providers.
    /// </summary>
    public static class DebugInfoRegistry {
        static readonly List<IDebugInfoProvider> Providers = new List<IDebugInfoProvider>();
        static readonly object Sync = new object();

        /// <summary>
        /// Registers a debug info provider.
        /// </summary>
        /// <param name="provider">Provider to register.</param>
        public static void Register(IDebugInfoProvider provider) {
            if (provider == null) {
                return;
            }

            lock (Sync) {
                Providers.Add(provider);
            }
        }

        /// <summary>
        /// Captures a snapshot of all provider info entries.
        /// </summary>
        /// <returns>List of debug info entries collected from every registered provider.</returns>
        public static List<DebugInfoEntry> Snapshot() {
            List<DebugInfoEntry> result = new List<DebugInfoEntry>();
            lock (Sync) {
                for (int i = 0; i < Providers.Count; i++) {
                    Providers[i].AppendInfo(result);
                }
            }

            return result;
        }
    }
}
