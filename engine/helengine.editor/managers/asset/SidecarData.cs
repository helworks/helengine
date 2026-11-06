namespace helengine.editor {
    /// <summary>
    /// Generic keyed data stored in a source import-settings sidecar (<c>&lt;source&gt;.hasset</c>). Each entry is a non-empty key and
    /// an opaque payload. New kinds of data are added as new keys, so the sidecar layout never needs another version for them. Entries
    /// are kept as raw bytes: an entry whose owner is not loaded in this editor (for example a plugin) is written back unchanged. Typed
    /// access goes through <see cref="SidecarDataEntryRegistry"/>, where owners register a reader and a writer for their key.
    /// </summary>
    public sealed class SidecarData {
        /// <summary>
        /// Payloads keyed by entry key, kept in ordinal key order so the sidecar bytes are deterministic.
        /// </summary>
        readonly SortedDictionary<string, byte[]> PayloadsByKey = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);

        /// <summary>
        /// Gets the number of entries.
        /// </summary>
        public int Count => PayloadsByKey.Count;

        /// <summary>
        /// Gets the entry keys in ordinal order, the order the sidecar writes them in.
        /// </summary>
        public IReadOnlyList<string> Keys => PayloadsByKey.Keys.ToArray();

        /// <summary>
        /// Returns whether an entry is stored under a key.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <returns>True when the entry exists.</returns>
        public bool ContainsKey(string key) {
            ValidateKey(key);
            return PayloadsByKey.ContainsKey(key);
        }

        /// <summary>
        /// Gets a copy of the raw payload stored under a key.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <param name="payload">Copy of the payload when the entry exists; null otherwise.</param>
        /// <returns>True when the entry exists.</returns>
        public bool TryGetRaw(string key, out byte[] payload) {
            ValidateKey(key);
            if (PayloadsByKey.TryGetValue(key, out byte[] stored)) {
                payload = (byte[])stored.Clone();
                return true;
            }

            payload = null;
            return false;
        }

        /// <summary>
        /// Stores a copy of a raw payload under a key, replacing any earlier entry.
        /// </summary>
        /// <param name="key">Non-empty entry key.</param>
        /// <param name="payload">Payload bytes; an empty array is allowed.</param>
        public void SetRaw(string key, byte[] payload) {
            ValidateKey(key);
            if (payload == null) {
                throw new ArgumentNullException(nameof(payload));
            }

            PayloadsByKey[key] = (byte[])payload.Clone();
        }

        /// <summary>
        /// Removes the entry stored under a key.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <returns>True when an entry was removed.</returns>
        public bool Remove(string key) {
            ValidateKey(key);
            return PayloadsByKey.Remove(key);
        }

        /// <summary>
        /// Rejects a blank entry key.
        /// </summary>
        /// <param name="key">Key to validate.</param>
        static void ValidateKey(string key) {
            if (string.IsNullOrWhiteSpace(key)) {
                throw new ArgumentException("Sidecar data keys must be non-empty.", nameof(key));
            }
        }
    }
}
