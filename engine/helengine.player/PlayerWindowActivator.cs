namespace helengine.player {
    /// <summary>
    /// Brings the freshly shown player window to the foreground, so the game receives keyboard input without the user
    /// clicking it first. The editor grants the player the foreground right when it launches it; when the editor was not
    /// the foreground app at that moment (the user switched apps while Play was cooking), Windows refuses the activation,
    /// the window stays behind and flashes in the taskbar, and one explanatory line is written to the player's standard
    /// output, which the editor shows in its console. The activation is never forced.
    /// </summary>
    public sealed class PlayerWindowActivator {
        /// <summary>
        /// Line written to standard output when Windows refused to activate the player window.
        /// </summary>
        public const string ActivationRefusedMessage = "window activation refused by Windows: the editor was not the foreground app when Play launched, so the player window stays behind and flashes in the taskbar";

        /// <summary>
        /// Performs the native foreground request.
        /// </summary>
        readonly IPlayerForegroundWindow ForegroundWindow;

        /// <summary>
        /// Standard output stream the refusal is reported on.
        /// </summary>
        readonly TextWriter Output;

        /// <summary>
        /// Initializes the activator.
        /// </summary>
        /// <param name="foregroundWindow">Performs the native foreground request.</param>
        /// <param name="output">Standard output stream the refusal is reported on.</param>
        /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
        public PlayerWindowActivator(IPlayerForegroundWindow foregroundWindow, TextWriter output) {
            if (foregroundWindow == null) {
                throw new ArgumentNullException(nameof(foregroundWindow));
            } else if (output == null) {
                throw new ArgumentNullException(nameof(output));
            }

            ForegroundWindow = foregroundWindow;
            Output = output;
        }

        /// <summary>
        /// Asks for the window to become the foreground window and writes <see cref="ActivationRefusedMessage"/> when
        /// Windows refused.
        /// </summary>
        /// <param name="windowHandle">Native handle of the shown player window.</param>
        /// <returns>True when the window is now the foreground window.</returns>
        public bool Activate(IntPtr windowHandle) {
            if (ForegroundWindow.TrySetForeground(windowHandle)) {
                return true;
            }

            Output.WriteLine(ActivationRefusedMessage);
            return false;
        }
    }
}
