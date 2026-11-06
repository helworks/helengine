namespace helengine {
    /// <summary>
    /// Platform-owned channel to the program hosting the engine (for example the web page around a WebGPU canvas).
    /// Only platforms that actually have such a host attach one; see <see cref="Core.SetHostMessageBackend"/>.
    /// </summary>
    public interface IHostMessageBackend {
        /// <summary>
        /// Delivers one message from the running game to the host.
        /// </summary>
        /// <param name="channel">Non-empty name that the host uses to route the message.</param>
        /// <param name="payload">Message body, usually JSON or a plain value; may be empty but never null.</param>
        void Send(string channel, string payload);
    }
}
