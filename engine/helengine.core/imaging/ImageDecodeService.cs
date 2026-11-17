namespace helengine {
    /// <summary>Validates bounded runtime image requests before passing them to an optional platform decoder.</summary>
    public sealed class ImageDecodeService {
        /// <summary>Hard upper bound for one copied encoded payload.</summary>
        public const int MaximumEncodedBytes = 16 * 1024 * 1024;
        /// <summary>Hard bound on either decoded image dimension.</summary>
        public const int MaximumDimension = 4096;
        /// <summary>Hard bound on decoded pixels, independent from the compressed input size.</summary>
        public const int MaximumPixels = 16 * 1024 * 1024;
        /// <summary>Borrows the host decoder for the Core lifetime.</summary>
        readonly IImageDecodeBackend Backend;

        /// <summary>Wraps an optional backend without choosing a hidden fallback codec.</summary>
        public ImageDecodeService(IImageDecodeBackend backend) { Backend = backend; }
        /// <summary>Gets whether this host supplies runtime image decoding.</summary>
        public bool IsAvailable => Backend != null;

        /// <summary>Begins a bounded decode; callers may release encoded input immediately after this method returns.</summary>
        [NativeOwnedReturn]
        public IImageDecodeOperation BeginDecode([NativeNoEscape] byte[] encoded, int maximumDimension, int maximumPixels) {
            if (Backend == null) { throw new NotSupportedException("Runtime image decoding is unavailable on this platform."); }
            ValidateRequest(encoded, maximumDimension, maximumPixels);
            return Backend.BeginDecode(encoded, maximumDimension, maximumPixels);
        }

        /// <summary>Applies the public allocation bounds consistently to native and managed adapters.</summary>
        public static void ValidateRequest([NativeNoEscape] byte[] encoded, int maximumDimension, int maximumPixels) {
            if (encoded == null) { throw new ArgumentNullException(nameof(encoded)); }
            if (encoded.Length == 0 || encoded.Length > MaximumEncodedBytes) { throw new ArgumentOutOfRangeException(nameof(encoded)); }
            if (maximumDimension < 1 || maximumDimension > MaximumDimension) { throw new ArgumentOutOfRangeException(nameof(maximumDimension)); }
            if (maximumPixels < 1 || maximumPixels > MaximumPixels) { throw new ArgumentOutOfRangeException(nameof(maximumPixels)); }
        }
    }
}
