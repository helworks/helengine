namespace helengine.directx11 {
    /// <summary>
    /// Wraps include source data with a resolved file path for D3DCompiler include handling.
    /// </summary>
    public class DirectX11IncludeStream : MemoryStream {
        /// <summary>
        /// Initializes a new include stream from source text.
        /// </summary>
        /// <param name="sourcePath">Resolved include file path.</param>
        /// <param name="source">Include source text.</param>
        public DirectX11IncludeStream(string sourcePath, string source) : base(System.Text.Encoding.UTF8.GetBytes(source)) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Include source path must be provided.", nameof(sourcePath));
            }

            if (string.IsNullOrWhiteSpace(source)) {
                throw new ArgumentException("Include source must be provided.", nameof(source));
            }

            SourcePath = sourcePath;
        }

        /// <summary>
        /// Gets the resolved include file path for this stream.
        /// </summary>
        public string SourcePath { get; }
    }
}
