namespace helengine.directx11 {
    /// <summary>
    /// Describes one planned DirectX11 post-process pass.
    /// </summary>
    public sealed class DirectX11PostProcessPass {
        /// <summary>
        /// Initializes one planned post-process pass.
        /// </summary>
        /// <param name="name">Stable pass name used for execution and diagnostics.</param>
        public DirectX11PostProcessPass(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Post-process passes require a stable name.", nameof(name));
            }

            Name = name;
        }

        /// <summary>
        /// Gets the stable pass name.
        /// </summary>
        public string Name { get; }
    }
}
