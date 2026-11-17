namespace helengine {
    /// <summary>Optional host-owned asynchronous transactional writes, independent from the read-only backend contract. Inputs are copied before Begin returns.</summary>
    public interface IUserFileWriteBackend {
        /// <summary>Publishes at most maximumBytes immutable bytes. Validates exact expectedLength and lowercase SHA256; an identical existing file succeeds and different bytes never overwrite.</summary>
        [NativeOwnedReturn]
        IUserFileWriteOperation BeginPublish(string relativePath, [NativeNoEscape] byte[] payload, string sha256, long expectedLength, int maximumBytes);
        /// <summary>Atomically replaces an existing descriptor only when SHA256 of its current complete bytes equals expectedRevision and every referenced file still matches. The platform never parses descriptor contents.</summary>
        [NativeOwnedReturn]
        IUserFileWriteOperation BeginReplace(string relativePath, [NativeNoEscape] byte[] descriptor, string expectedRevision, [NativeNoEscape] UserFilePrecondition[] preconditions, int maximumBytes);
    }
}
