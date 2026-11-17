namespace helengine {
    /// <summary>Host-owned asynchronous, read-only access to user documents; implementations never perform filesystem work while polling.</summary>
    public interface IUserFileBackend {
        /// <summary>Starts a bounded listing relative to Documents; the empty path selects Documents itself.</summary>
        [NativeOwnedReturn]
        IUserFileOperation BeginList(string relativePath, int maximumEntries);
        /// <summary>Starts a bounded binary read relative to Documents; oversize input fails without truncated results.</summary>
        [NativeOwnedReturn]
        IUserFileOperation BeginRead(string relativePath, int maximumBytes);
    }
}
