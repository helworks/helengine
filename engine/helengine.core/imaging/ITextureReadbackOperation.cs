namespace helengine {
    /// <summary>Caller-owned bounded GPU copy; consume on its initiating thread and dispose after using borrowed pixels.</summary>
    public interface ITextureReadbackOperation : IDisposable {
        /// <summary>Gets the last polled state; Advance never waits for the GPU.</summary>
        TextureReadbackStatus Status { get; }
        /// <summary>Gets diagnostic details after failure; no partial image is published.</summary>
        string ErrorMessage { get; }
        /// <summary>Gets the requested region width in texture pixels.</summary>
        int Width { get; }
        /// <summary>Gets the requested region height in texture pixels.</summary>
        int Height { get; }
        /// <summary>Gets whether returned RGB already includes alpha, preserving the source texture's storage semantics.</summary>
        bool IsPremultipliedAlpha { get; }
        /// <summary>Gets borrowed top-down tightly packed RGBA8 bytes after success, otherwise null. Valid until cancel/dispose; values preserve source encoded channels without color conversion.</summary>
        [NativeBorrowedReturn]
        byte[] Pixels { get; }
        /// <summary>Publishes completed native data with a bounded CPU copy, without waiting for GPU execution.</summary>
        void Advance();
        /// <summary>Invalidates pixels immediately; already submitted GPU copying retires independently without blocking.</summary>
        void Cancel();
    }
}
