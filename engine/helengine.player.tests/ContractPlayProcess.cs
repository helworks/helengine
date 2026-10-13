using helengine.editor;

namespace helengine.player.tests {
    /// <summary>
    /// Stand-in player process for the editor-to-player argument contract test. It stays running, never raises
    /// events, and marks itself exited when killed.
    /// </summary>
    public sealed class ContractPlayProcess : IEditorPlayProcess {
        /// <summary>Never raised; the contract test does not simulate player output.</summary>
        public event Action<string> OutputLine {
            add { }
            remove { }
        }

        /// <summary>Never raised; the contract test does not simulate player errors.</summary>
        public event Action<string> ErrorLine {
            add { }
            remove { }
        }

        /// <summary>Never raised; the contract test does not simulate the player exiting.</summary>
        public event Action Exited {
            add { }
            remove { }
        }

        /// <summary>Gets a fixed stand-in process identifier.</summary>
        public int ProcessId => 4242;

        /// <summary>Gets whether <see cref="Kill"/> was called.</summary>
        public bool HasExited { get; private set; }

        /// <summary>Gets a zero exit code; the stand-in only exits through <see cref="Kill"/>.</summary>
        public int ExitCode => 0;

        /// <summary>
        /// Marks the stand-in process as exited.
        /// </summary>
        public void Kill() {
            HasExited = true;
        }
    }
}
