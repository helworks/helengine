namespace helengine {
    /// <summary>
    /// Selects how one audio asset should be consumed at runtime.
    /// </summary>
    public enum AudioPlaybackMode {
        /// <summary>
        /// Loads the encoded payload into memory for low-latency playback.
        /// </summary>
        Predecoded = 0,

        /// <summary>
        /// Streams the encoded payload in chunks during playback.
        /// </summary>
        Streamed = 1
    }
}
