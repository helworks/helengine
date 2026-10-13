namespace helengine.editor {
    /// <summary>
    /// Describes one player process launch: which executable to start and the exact command-line arguments, in order.
    /// </summary>
    public sealed class EditorPlayLaunchRequest {
        /// <summary>
        /// Initializes one player launch request.
        /// </summary>
        /// <param name="playerExecutablePath">Absolute path of the desktop player executable.</param>
        /// <param name="arguments">Command-line arguments passed to the player, one entry per argument, in order.</param>
        /// <exception cref="ArgumentException">Thrown when the executable path is blank.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the argument list is null.</exception>
        public EditorPlayLaunchRequest(string playerExecutablePath, IReadOnlyList<string> arguments) {
            if (string.IsNullOrWhiteSpace(playerExecutablePath)) {
                throw new ArgumentException("A player launch requires the player executable path.", nameof(playerExecutablePath));
            }

            PlayerExecutablePath = playerExecutablePath;
            Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        }

        /// <summary>
        /// Gets the absolute path of the desktop player executable to start.
        /// </summary>
        public string PlayerExecutablePath { get; }

        /// <summary>
        /// Gets the command-line arguments passed to the player, one entry per argument, in order.
        /// </summary>
        public IReadOnlyList<string> Arguments { get; }
    }
}
