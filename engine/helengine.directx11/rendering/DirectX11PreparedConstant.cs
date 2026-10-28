namespace helengine.directx11 {
    /// <summary>Pairs a native slot with pooled constant contents, or null for an explicit unbind.</summary>
    sealed class DirectX11PreparedConstant<T> where T : class, IDisposable {
        /// <summary>Constant-buffer register written for both shader stages.</summary>
        public int Slot { get; }
        /// <summary>Shared content retained by this binding, or null for missing material data.</summary>
        public DirectX11ConstantContent<T> Content { get; }
        /// <summary>Captures the slot and the already-acquired pooled content.</summary>
        public DirectX11PreparedConstant(int slot, DirectX11ConstantContent<T> content) { Slot = slot; Content = content; }
    }
}
