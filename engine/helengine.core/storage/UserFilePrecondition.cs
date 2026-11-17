namespace helengine {
    /// <summary>Immutable caller-supplied identity for an existing relative asset, checked under the same coordination as descriptor replacement.</summary>
    public sealed class UserFilePrecondition {
        /// <summary>Creates a platform-independent file identity; the write backend validates path, lowercase SHA256 and exact bounded length.</summary>
        public UserFilePrecondition(string relativePath, string sha256, long length) { RelativePath = relativePath; Sha256 = sha256; Length = length; }
        /// <summary>Gets the canonical slash-separated path relative to the host document root.</summary>
        public string RelativePath { get; }
        /// <summary>Gets the lowercase 64-character SHA256 of the complete expected bytes.</summary>
        public string Sha256 { get; }
        /// <summary>Gets the exact expected byte length, bounded by the immutable asset limit.</summary>
        public long Length { get; }
    }
}
