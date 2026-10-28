namespace helengine.directx11 {
    /// <summary>Retains a runtime texture identity while leaving its live DirectX view to draw-time resolution.</summary>
    sealed class DirectX11PreparedTexture {
        /// <summary>Original layout binding used for backend slot remapping.</summary>
        public MaterialLayoutBinding Binding { get; }
        /// <summary>Runtime owner whose current view must be read on every use.</summary>
        public RuntimeTexture Texture { get; }
        /// <summary>Captures an inherited texture without caching a replaceable native resource view.</summary>
        public DirectX11PreparedTexture(MaterialLayoutBinding binding, RuntimeTexture texture) { Binding = binding; Texture = texture; }
    }
}
