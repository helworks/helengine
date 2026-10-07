namespace helengine.editor {
    /// <summary>
    /// Publishes completed directory trees while tolerating brief Windows sharing locks on newly written outputs.
    /// </summary>
    public static class EditorDirectoryPublicationMove {
        /// <summary>Maximum number of move attempts, bounding lock recovery to five seconds.</summary>
        const int MaximumMoveAttempts = 26;

        /// <summary>Milliseconds between attempts while Windows reports a sharing or access lock.</summary>
        const int RetryDelayMilliseconds = 200;

        /// <summary>
        /// Moves one completed tree without replacing an existing destination or hiding a persistent publication failure.
        /// </summary>
        /// <param name="sourceDirectoryPath">Completed tree to publish.</param>
        /// <param name="destinationDirectoryPath">Unoccupied publication destination.</param>
        public static void Move(string sourceDirectoryPath, string destinationDirectoryPath) {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectoryPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectoryPath);
            for (int attempt = 0; ; attempt++) {
                try {
                    Directory.Move(sourceDirectoryPath, destinationDirectoryPath);
                    return;
                } catch (Exception exception) when (
                    attempt + 1 < MaximumMoveAttempts &&
                    CanRetryMove(exception, sourceDirectoryPath, destinationDirectoryPath)) {
                    Thread.Sleep(RetryDelayMilliseconds);
                }
            }
        }

        /// <summary>Recognizes Windows locks only while the source exists and the publication destination remains unoccupied.</summary>
        /// <param name="exception">Failure returned by the directory move.</param>
        /// <param name="sourceDirectoryPath">Source that must remain available for another attempt.</param>
        /// <param name="destinationDirectoryPath">Destination that must remain absent to preserve move semantics.</param>
        /// <returns>True for a Windows access, sharing, or lock error that can still be retried safely.</returns>
        static bool CanRetryMove(Exception exception, string sourceDirectoryPath, string destinationDirectoryPath) {
            if (!OperatingSystem.IsWindows() ||
                !(exception is IOException || exception is UnauthorizedAccessException) ||
                !Directory.Exists(sourceDirectoryPath) ||
                Directory.Exists(destinationDirectoryPath) || File.Exists(destinationDirectoryPath)) {
                return false;
            }

            return exception.HResult == unchecked((int)0x80070005) ||
                exception.HResult == unchecked((int)0x80070020) ||
                exception.HResult == unchecked((int)0x80070021);
        }
    }
}
