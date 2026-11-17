namespace helengine {
    /// <summary>Exposes the optional host document backend without inventing a root on unsupported platforms.</summary>
    public sealed class UserFileService {
        /// <summary>Borrows the host backend for the lifetime of Core.</summary>
        readonly IUserFileBackend Backend;
        /// <summary>Creates a service around an optional host-owned backend.</summary>
        public UserFileService(IUserFileBackend backend) { Backend = backend; }
        /// <summary>Gets whether this host supplied user document access.</summary>
        public bool IsAvailable => Backend != null;
        /// <summary>Begins a caller-owned bounded listing, or explicitly reports unsupported storage.</summary>
        [NativeOwnedReturn]
        public IUserFileOperation BeginList(string relativePath, int maximumEntries) {
            if (Backend == null) { throw new NotSupportedException("User document files are unavailable on this platform."); }
            return Backend.BeginList(relativePath, maximumEntries);
        }
        /// <summary>Begins a caller-owned bounded read, or explicitly reports unsupported storage.</summary>
        [NativeOwnedReturn]
        public IUserFileOperation BeginRead(string relativePath, int maximumBytes) {
            if (Backend == null) { throw new NotSupportedException("User document files are unavailable on this platform."); }
            return Backend.BeginRead(relativePath, maximumBytes);
        }
    }
}
