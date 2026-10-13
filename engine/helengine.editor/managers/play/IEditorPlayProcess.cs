namespace helengine.editor {
    /// <summary>
    /// Handle to one running player process: exposes its identity and exit status, lets the editor terminate it, and
    /// streams its console output.
    /// </summary>
    public interface IEditorPlayProcess {
        /// <summary>
        /// Raised for each line the player writes to standard output.
        /// </summary>
        event Action<string> OutputLine;

        /// <summary>
        /// Raised for each line the player writes to standard error.
        /// </summary>
        event Action<string> ErrorLine;

        /// <summary>
        /// Raised once when the player process exits on its own.
        /// </summary>
        event Action Exited;

        /// <summary>
        /// Gets the operating-system identifier of the player process.
        /// </summary>
        int ProcessId { get; }

        /// <summary>
        /// Gets whether the player process has exited.
        /// </summary>
        bool HasExited { get; }

        /// <summary>
        /// Gets the player's exit code; only valid once <see cref="HasExited"/> is true.
        /// </summary>
        int ExitCode { get; }

        /// <summary>
        /// Terminates the player process and returns only once it is gone, so files it held are released.
        /// </summary>
        void Kill();
    }
}
