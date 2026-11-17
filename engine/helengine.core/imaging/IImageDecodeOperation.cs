namespace helengine {
    /// <summary>Caller-owned decode request; all methods and result access run on the initiating thread.</summary>
    public interface IImageDecodeOperation : IDisposable {
        /// <summary>Gets the last polled state. Cancellation invalidates even a previously published success.</summary>
        ImageDecodeStatus Status { get; }
        /// <summary>Gets diagnostic detail for failed decoding; no partial image is exposed.</summary>
        string ErrorMessage { get; }
        /// <summary>Gets borrowed top-down, tightly packed, straight-alpha sRGB Rgba32/A8 pixels after success, or null otherwise. Valid until cancel/dispose; upload before releasing this request.</summary>
        [NativeBorrowedReturn]
        TextureAsset Image { get; }
        /// <summary>Publishes completed worker data without waiting or decoding on the UI thread.</summary>
        void Advance();
        /// <summary>Cancels pending/completed work and releases published pixels; background completion cannot republish them.</summary>
        void Cancel();
    }
}
