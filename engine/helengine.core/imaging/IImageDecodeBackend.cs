namespace helengine {
    /// <summary>Host-owned asynchronous decoder for PNG/JPEG bytes, independent from files or application document formats.</summary>
    public interface IImageDecodeBackend {
        /// <summary>Copies encoded bytes before returning a caller-owned request. Off-thread decoding produces an orientation-correct thumbnail no larger than either dimension/pixel bound, without upscaling.</summary>
        /// <remarks>Called on the host UI thread. The backend enforces the same limits as ImageDecodeService and bounds running/queued work. It must never retain the borrowed input array.</remarks>
        [NativeOwnedReturn]
        IImageDecodeOperation BeginDecode([NativeNoEscape] byte[] encoded, int maximumDimension, int maximumPixels);
    }
}
