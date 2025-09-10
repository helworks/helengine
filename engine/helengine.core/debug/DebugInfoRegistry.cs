namespace helengine {
    public interface IDebugInfoProvider {
        string Category { get; }
        void AppendInfo(List<(string Key, string Value)> items);
    }

    public static class DebugInfoRegistry {
        static readonly List<IDebugInfoProvider> providers = new List<IDebugInfoProvider>();
        static readonly object sync = new object();

        public static void Register(IDebugInfoProvider provider) {
            if (provider == null) return;
            lock (sync) providers.Add(provider);
        }

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

