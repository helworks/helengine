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
        /// Appends debug key/value pairs to the supplied collection.
        /// </summary>
        /// <param name="items">Collection to append to.</param>
        void AppendInfo(List<(string Key, string Value)> items);
    }

    /// <summary>
    /// Registry for aggregating debug info providers.
    /// </summary>
    public static class DebugInfoRegistry {
        static readonly List<IDebugInfoProvider> providers = new List<IDebugInfoProvider>();
        static readonly object sync = new object();

        /// <summary>
        /// Registers a debug info provider.
        /// </summary>
        /// <param name="provider">Provider to register.</param>
        public static void Register(IDebugInfoProvider provider) {
            if (provider == null) return;
            lock (sync) providers.Add(provider);
        }

        /// <summary>
        /// Captures a snapshot of all provider info entries.
        /// </summary>
        /// <returns>List of category/key/value tuples.</returns>
        public static List<(string Category, string Key, string Value)> Snapshot() {
            var result = new List<(string, string, string)>();
            lock (sync) {
                for (int i = 0; i < providers.Count; i++) {
                    var p = providers[i];
                    var items = new List<(string Key, string Value)>();
                    try { p.AppendInfo(items); } catch { }
                    for (int j = 0; j < items.Count; j++) {
                        var it = items[j];
                        result.Add((p.Category, it.Key, it.Value));
                    }
                }
            }
            return result;
        }
    }
}
