namespace helengine {
    /// <summary>Identifies the monotonic lifecycle of one asynchronous encoded-image request.</summary>
    public enum ImageDecodeStatus : byte {
        /// <summary>No result has been published to the initiating thread.</summary>
        Pending = 0,
        /// <summary>A complete bounded RGBA image is available.</summary>
        Succeeded = 1,
        /// <summary>The input could not be decoded within its declared limits.</summary>
        Failed = 2,
        /// <summary>The caller canceled the request; later worker completion cannot revive it.</summary>
        Canceled = 3
    }
}
