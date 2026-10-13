namespace helengine.player {
    /// <summary>
    /// Forwards engine <see cref="Logger"/> entries written inside the player to its console streams, so the editor,
    /// which reads the player's standard output and standard error, shows script and engine logs in its console.
    /// Informational entries go to the output writer unchanged, warnings go to the output writer behind
    /// <see cref="WarningPrefix"/>, and errors go to the error writer.
    /// </summary>
    public sealed class PlayerConsoleLogForwarder : IDisposable {
        /// <summary>
        /// Prefix written before every forwarded warning so it stays distinguishable from informational lines.
        /// </summary>
        public const string WarningPrefix = "warning: ";

        /// <summary>
        /// Writer that receives informational and warning entries; the player passes its standard output.
        /// </summary>
        readonly TextWriter Output;

        /// <summary>
        /// Writer that receives error entries; the player passes its standard error.
        /// </summary>
        readonly TextWriter Error;

        /// <summary>
        /// True while this forwarder is subscribed to <see cref="Logger.MessageLogged"/>.
        /// </summary>
        bool IsSubscribed;

        /// <summary>
        /// Creates a forwarder and subscribes it to the engine logger until it is disposed.
        /// </summary>
        /// <param name="output">Writer for informational and warning entries.</param>
        /// <param name="error">Writer for error entries.</param>
        /// <exception cref="ArgumentNullException">Thrown when either writer is null.</exception>
        public PlayerConsoleLogForwarder(TextWriter output, TextWriter error) {
            Output = output ?? throw new ArgumentNullException(nameof(output));
            Error = error ?? throw new ArgumentNullException(nameof(error));
            Logger.MessageLogged += HandleMessageLogged;
            IsSubscribed = true;
        }

        /// <summary>
        /// Unsubscribes from the engine logger; later log entries are no longer forwarded.
        /// </summary>
        public void Dispose() {
            if (!IsSubscribed) {
                return;
            }

            Logger.MessageLogged -= HandleMessageLogged;
            IsSubscribed = false;
        }

        /// <summary>
        /// Writes one log entry to the writer that matches its severity. <see cref="Logger.MessageLogged"/> is raised
        /// for every severity, so this single handler covers informational, warning and error entries exactly once.
        /// </summary>
        /// <param name="entry">Entry the engine logged.</param>
        /// <exception cref="InvalidOperationException">Thrown for a severity this forwarder does not know.</exception>
        void HandleMessageLogged(LogEntry entry) {
            switch (entry.Level) {
                case LogLevel.Info:
                    Output.WriteLine(entry.Message);
                    break;
                case LogLevel.Warning:
                    Output.WriteLine(WarningPrefix + entry.Message);
                    break;
                case LogLevel.Error:
                    Error.WriteLine(entry.Message);
                    break;
                default:
                    throw new InvalidOperationException($"Log level '{entry.Level}' is not supported by the player console forwarder.");
            }
        }
    }
}
