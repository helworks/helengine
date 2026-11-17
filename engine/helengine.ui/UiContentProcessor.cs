using helengine.ui.format;
namespace helengine.ui {
    /// <summary>Reads standalone UI assets through ContentManager's existing stream boundary.</summary>
    public sealed class UiContentProcessor : IContentProcessor<UiDocument> {
        /// <summary>Reports the portable model produced by this processor.</summary>
        public Type OutputType { get { return typeof(UiDocument); } }
        /// <summary>Reads partial/non-seekable streams with a hard limit; leaves the source stream with its caller.</summary>
        [NativeOwnedReturn] public UiDocument Read([NativeNoEscape] Stream stream) {
            if (stream == null) { throw new ArgumentNullException(nameof(stream)); }
            byte[] buffer = new byte[4096];
            try { return ReadBuffered(stream, buffer); }
            finally { NativeOwnership.Release(ref buffer); }
        }
        /// <summary>Consumes the borrowed buffer synchronously; only the returned document escapes this call.</summary>
        [NativeOwnedReturn] static UiDocument ReadBuffered([NativeNoEscape] Stream stream, [NativeNoEscape] byte[] buffer) {
                using (MemoryStream collected = new MemoryStream()) {
                    int count;
                    while ((count = stream.Read(buffer, 0, buffer.Length)) != 0) {
                        if (collected.Length + count > UiFormatLimits.Bytes) { throw new FormatException("UI stream exceeds byte limit."); }
                        collected.Write(buffer, 0, count);
                    }
                    byte[] bytes = collected.ToArray();
                    try { return UiBinaryCodec.Read(bytes); } finally { NativeOwnership.Release(ref bytes); }
                }
        }
        /// <summary>Transfers the same parsed document through the untyped content interface.</summary>
        [NativeOwnedReturn] object IContentProcessor.ReadObject([NativeNoEscape] Stream stream) { return Read(stream); }
    }
}
