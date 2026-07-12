namespace helengine {
    /// <summary>
    /// Stores shared runtime state for one logical audio bus.
    /// </summary>
    public sealed class AudioBus {
        /// <summary>
        /// Initializes one audio bus with its stable identifier.
        /// </summary>
        /// <param name="busId">Stable bus identifier.</param>
        public AudioBus(string busId) {
            if (string.IsNullOrWhiteSpace(busId)) {
                throw new ArgumentException("Bus id must be provided.", nameof(busId));
            }

            BusId = busId;
            Gain = 1f;
        }

        /// <summary>
        /// Gets the stable bus identifier.
        /// </summary>
        public string BusId { get; }

        /// <summary>
        /// Gets or sets the current linear bus gain.
        /// </summary>
        public float Gain { get; set; }

        /// <summary>
        /// Gets or sets whether the bus is currently paused.
        /// </summary>
        public bool Paused { get; set; }
    }
}
