namespace helengine.directx11 {
    /// <summary>Compares retained constant bytes by content so equal materials share one GPU resource.</summary>
    sealed class DirectX11ConstantContentComparer : IEqualityComparer<byte[]> {
        /// <summary>Resolves hash collisions through exact byte equality.</summary>
        public bool Equals(byte[] left, byte[] right) => left.AsSpan().SequenceEqual(right);
        /// <summary>Hashes every byte and length; hashes are never treated as proof of equality.</summary>
        public int GetHashCode(byte[] data) {
            unchecked {
                int hash = data.Length;
                for (int i = 0; i < data.Length; i++) {
                    hash = hash * 31 + data[i];
                }
                return hash;
            }
        }
    }
}
