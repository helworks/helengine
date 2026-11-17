namespace helengine {
    /// <summary>Exposes an optional write capability without making read-only hosts advertise unsupported writes. Host/session wiring supplies this separately.</summary>
    public sealed class UserFileWriteService {
        /// <summary>Borrows the optional host write backend for the service lifetime.</summary>
        readonly IUserFileWriteBackend Backend;
        /// <summary>Creates a service around an optional host-owned transactional write backend.</summary>
        public UserFileWriteService(IUserFileWriteBackend backend) { Backend = backend; }
        /// <summary>Gets whether the host explicitly supplied transactional writing.</summary>
        public bool IsAvailable => Backend != null;
        /// <summary>Begins a bounded caller-owned immutable publication or explicitly rejects unsupported writing.</summary>
        [NativeOwnedReturn]
        public IUserFileWriteOperation BeginPublish(string relativePath, [NativeNoEscape] byte[] payload, string sha256, long expectedLength, int maximumBytes) {
            if (Backend == null) { throw new NotSupportedException("User document writing is unavailable on this platform."); }
            return Backend.BeginPublish(relativePath, payload, sha256, expectedLength, maximumBytes);
        }
        /// <summary>Begins conditional descriptor replacement against the exact original content revision and all referenced asset identities.</summary>
        [NativeOwnedReturn]
        public IUserFileWriteOperation BeginReplace(string relativePath, [NativeNoEscape] byte[] descriptor, string expectedRevision, [NativeNoEscape] UserFilePrecondition[] preconditions, int maximumBytes) {
            if (Backend == null) { throw new NotSupportedException("User document writing is unavailable on this platform."); }
            return Backend.BeginReplace(relativePath, descriptor, expectedRevision, preconditions, maximumBytes);
        }
    }
}
