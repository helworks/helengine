namespace helengine {
    /// <summary>
    /// Exchanges named text messages between game scripts and the program hosting the engine. Messages from the host are
    /// queued as they arrive and raised through <see cref="MessageReceived"/> once per frame, before entities update, so
    /// scripts always observe them at the same point of the frame.
    /// </summary>
    public sealed class HostMessageManager {
        readonly IHostMessageBackend Backend;
        List<string> PendingChannels;
        List<string> PendingPayloads;
        List<string> DispatchingChannels;
        List<string> DispatchingPayloads;

        /// <summary>
        /// Initializes the manager over the platform channel to the host.
        /// </summary>
        /// <param name="backend">Platform-owned host channel used by <see cref="Send"/>.</param>
        public HostMessageManager(IHostMessageBackend backend) {
            Backend = backend ?? throw new ArgumentNullException(nameof(backend));
            PendingChannels = new List<string>();
            PendingPayloads = new List<string>();
            DispatchingChannels = new List<string>();
            DispatchingPayloads = new List<string>();
        }

        /// <summary>
        /// Raised during <see cref="Dispatch"/> for each message the host sent, in arrival order, with the channel
        /// followed by the payload.
        /// </summary>
        public event Action<string, string> MessageReceived;

        /// <summary>
        /// Gets the number of host messages waiting for the next <see cref="Dispatch"/>.
        /// </summary>
        public int PendingCount => PendingChannels.Count;

        /// <summary>
        /// Queues one message from the host. Called by the platform whenever the host posts; it is never raised
        /// immediately, so the host can post at any time without re-entering game code.
        /// </summary>
        /// <param name="channel">Non-empty routing name chosen by the host.</param>
        /// <param name="payload">Message body; may be empty but never null.</param>
        public void Receive(string channel, string payload) {
            ValidateMessage(channel, payload);
            PendingChannels.Add(channel);
            PendingPayloads.Add(payload);
        }

        /// <summary>
        /// Raises <see cref="MessageReceived"/> for every message queued before this call. Messages that arrive while
        /// handlers run stay queued for the next frame.
        /// </summary>
        public void Dispatch() {
            if (PendingChannels.Count == 0) {
                return;
            }

            List<string> channels = PendingChannels;
            List<string> payloads = PendingPayloads;
            PendingChannels = DispatchingChannels;
            PendingPayloads = DispatchingPayloads;
            DispatchingChannels = channels;
            DispatchingPayloads = payloads;
            for (int index = 0; index < channels.Count; index++) {
                MessageReceived?.Invoke(channels[index], payloads[index]);
            }

            channels.Clear();
            payloads.Clear();
        }

        /// <summary>
        /// Sends one message from the game to the host.
        /// </summary>
        /// <param name="channel">Non-empty routing name the host listens for.</param>
        /// <param name="payload">Message body; may be empty but never null.</param>
        public void Send(string channel, string payload) {
            ValidateMessage(channel, payload);
            Backend.Send(channel, payload);
        }

        /// <summary>
        /// Rejects messages without a routing name or with a null body.
        /// </summary>
        /// <param name="channel">Routing name to validate.</param>
        /// <param name="payload">Message body to validate.</param>
        static void ValidateMessage(string channel, string payload) {
            if (string.IsNullOrEmpty(channel)) {
                throw new ArgumentException("Host message channel is required.", nameof(channel));
            } else if (payload == null) {
                throw new ArgumentNullException(nameof(payload));
            }
        }
    }
}
