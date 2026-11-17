namespace helengine {
    /// <summary>Immutable metadata borrowed from a completed user document listing.</summary>
    public sealed class UserFileEntry {
        /// <summary>Creates metadata without interpreting any application-specific document format.</summary>
        public UserFileEntry(string relativePath, string name, UserFileEntryKind kind, long modificationTimeUnixMilliseconds, long sizeBytes) {
            RelativePath = relativePath;
            Name = name;
            Kind = kind;
            ModificationTimeUnixMilliseconds = modificationTimeUnixMilliseconds;
            SizeBytes = sizeBytes;
        }
        /// <summary>Gets the UTF-8 round-trippable path relative to Documents.</summary>
        public string RelativePath { get; }
        /// <summary>Gets the entry's final path component.</summary>
        public string Name { get; }
        /// <summary>Gets the filesystem type observed without following a link.</summary>
        public UserFileEntryKind Kind { get; }
        /// <summary>Gets modification time as integer UTC milliseconds since the Unix epoch.</summary>
        public long ModificationTimeUnixMilliseconds { get; }
        /// <summary>Gets the native filesystem size in bytes.</summary>
        public long SizeBytes { get; }
    }
}
