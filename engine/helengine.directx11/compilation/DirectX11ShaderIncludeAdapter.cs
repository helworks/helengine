using SharpDX;
using SharpDX.D3DCompiler;

namespace helengine.directx11 {
    /// <summary>
    /// Bridges engine include resolution with the D3DCompiler include interface.
    /// </summary>
    public class DirectX11ShaderIncludeAdapter : CallbackBase, Include {
        /// <summary>
        /// Stores the include resolver used to load shader includes.
        /// </summary>
        readonly IShaderIncludeResolver resolver;

        /// <summary>
        /// Stores the root shader file path used for the initial include.
        /// </summary>
        readonly string rootPath;

        /// <summary>
        /// Initializes a new include adapter.
        /// </summary>
        /// <param name="resolver">Include resolver used by the engine.</param>
        /// <param name="rootPath">Root shader source path.</param>
        public DirectX11ShaderIncludeAdapter(IShaderIncludeResolver resolver, string rootPath) {
            if (resolver == null) {
                throw new ArgumentNullException(nameof(resolver));
            }

            if (string.IsNullOrWhiteSpace(rootPath)) {
                throw new ArgumentException("Root path must be provided.", nameof(rootPath));
            }

            this.resolver = resolver;
            this.rootPath = rootPath;
        }

        /// <summary>
        /// Resolves an include file requested by the D3DCompiler.
        /// </summary>
        /// <param name="type">Include type requested by the compiler.</param>
        /// <param name="fileName">Include file name.</param>
        /// <param name="parentStream">Parent include stream when nested.</param>
        /// <returns>Stream containing include contents.</returns>
        public Stream Open(IncludeType type, string fileName, Stream parentStream) {
            string requestingFile = ResolveRequestingPath(parentStream);
            ShaderIncludeResult include = resolver.Resolve(requestingFile, fileName);
            return new DirectX11IncludeStream(include.Path, include.Source);
        }

        /// <summary>
        /// Closes an include stream opened by the compiler.
        /// </summary>
        /// <param name="stream">Stream to close.</param>
        public void Close(Stream stream) {
            if (stream != null) {
                stream.Dispose();
            }
        }

        /// <summary>
        /// Determines the requesting file path for include resolution.
        /// </summary>
        /// <param name="parentStream">Parent include stream when nested.</param>
        /// <returns>Resolved requesting file path.</returns>
        string ResolveRequestingPath(Stream parentStream) {
            if (parentStream is DirectX11IncludeStream includeStream) {
                return includeStream.SourcePath;
            }

            return rootPath;
        }
    }
}
