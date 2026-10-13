namespace helengine.editor {
    /// <summary>
    /// Subscription between the Play session service and one launched player process. Each launch gets its own
    /// binding, so every event the service receives names the process that raised it, and the service can ignore
    /// late events from a player it has already replaced or stopped.
    /// </summary>
    sealed class EditorPlayProcessBinding {
        /// <summary>
        /// Session service that receives this process's events.
        /// </summary>
        readonly EditorPlaySessionService Service;

        /// <summary>
        /// Initializes one binding between the session service and a launched player process.
        /// </summary>
        /// <param name="service">Session service that receives the process's events.</param>
        /// <param name="process">Player process whose events are forwarded.</param>
        /// <exception cref="ArgumentNullException">Thrown when the service or the process is null.</exception>
        public EditorPlayProcessBinding(EditorPlaySessionService service, IEditorPlayProcess process) {
            Service = service ?? throw new ArgumentNullException(nameof(service));
            Process = process ?? throw new ArgumentNullException(nameof(process));
        }

        /// <summary>
        /// Gets the player process this binding forwards events from.
        /// </summary>
        public IEditorPlayProcess Process { get; }

        /// <summary>
        /// Subscribes to the process's output, error and exit events.
        /// </summary>
        public void Attach() {
            Process.OutputLine += HandleOutputLine;
            Process.ErrorLine += HandleErrorLine;
            Process.Exited += HandleExited;
        }

        /// <summary>
        /// Unsubscribes from the process's output, error and exit events.
        /// </summary>
        public void Detach() {
            Process.OutputLine -= HandleOutputLine;
            Process.ErrorLine -= HandleErrorLine;
            Process.Exited -= HandleExited;
        }

        /// <summary>
        /// Forwards one standard-output line to the service together with this binding's identity.
        /// </summary>
        /// <param name="line">Line the player wrote.</param>
        void HandleOutputLine(string line) {
            Service.HandlePlayerOutputLine(this, line);
        }

        /// <summary>
        /// Forwards one standard-error line to the service together with this binding's identity.
        /// </summary>
        /// <param name="line">Line the player wrote.</param>
        void HandleErrorLine(string line) {
            Service.HandlePlayerErrorLine(this, line);
        }

        /// <summary>
        /// Forwards the process exit to the service together with this binding's identity.
        /// </summary>
        void HandleExited() {
            Service.HandlePlayerExited(this);
        }
    }
}
