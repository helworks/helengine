namespace helengine.directx11 {
    /// <summary>Owns one shared immutable constant payload and its backend resource.</summary>
    sealed class DirectX11ConstantContent<T> where T : class, IDisposable {
        /// <summary>Private resolved copy used as the exact pool key.</summary>
        public byte[] Data { get; }
        /// <summary>Resource initialized once from the retained payload.</summary>
        public T Resource { get; }
        /// <summary>Number of prepared bindings retaining this resource.</summary>
        public int References { get; set; }
        /// <summary>Records ownership after resource creation succeeds.</summary>
        public DirectX11ConstantContent(byte[] data, T resource) { Data = data; Resource = resource; }
    }
}
