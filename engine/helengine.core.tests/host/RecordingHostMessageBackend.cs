namespace helengine.core.tests.host {
    /// <summary>
    /// Host channel double that records every message the game sends, in order.
    /// </summary>
    public sealed class RecordingHostMessageBackend : IHostMessageBackend {
        /// <summary>
        /// Gets the channels of the sent messages, in send order.
        /// </summary>
        public List<string> Channels { get; } = new List<string>();

        /// <summary>
        /// Gets the payloads of the sent messages, aligned with <see cref="Channels"/>.
        /// </summary>
        public List<string> Payloads { get; } = new List<string>();

        /// <summary>
        /// Records one outgoing message.
        /// </summary>
        /// <param name="channel">Routing name of the message.</param>
        /// <param name="payload">Message body.</param>
        public void Send(string channel, string payload) {
            Channels.Add(channel);
            Payloads.Add(payload);
        }
    }
}
