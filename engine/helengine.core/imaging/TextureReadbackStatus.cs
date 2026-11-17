namespace helengine {
    /// <summary>Describes publication of one bounded asynchronous GPU texture-region readback.</summary>
    public enum TextureReadbackStatus : byte {
        /// <summary>The GPU copy has not yet been published by Advance.</summary>
        Pending = 0,
        /// <summary>A complete RGBA8 region is available until cancellation or disposal.</summary>
        Succeeded = 1,
        /// <summary>GPU copying or result publication failed; no partial pixels are available.</summary>
        Failed = 2,
        /// <summary>Publication was canceled permanently, including any earlier success.</summary>
        Canceled = 3
    }
}
