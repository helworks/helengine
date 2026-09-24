namespace helengine {
    /// <summary>
    /// Owns reusable vertex and index arrays borrowed synchronously by a batch sink.
    /// </summary>
    public sealed class Batch2DBuffer : IDisposable {
        /// <summary>Owns the packed vertex storage for the current batch chunk.</summary>
        [NativeOwnedMember]
        public Batch2DVertex[] Vertices;

        /// <summary>Owns the triangle index storage for the current batch chunk.</summary>
        [NativeOwnedMember]
        public ushort[] Indices;

        /// <summary>Gets the number of initialized vertices in the current chunk.</summary>
        public int VertexCount { get; internal set; }

        /// <summary>Gets the number of initialized indices in the current chunk.</summary>
        public int IndexCount { get; internal set; }

        /// <summary>Allocates fixed storage sized for the writer's validated quad capacity.</summary>
        /// <param name="quadCapacity">The maximum number of quads in one chunk.</param>
        internal Batch2DBuffer(int quadCapacity) {
            Vertices = new Batch2DVertex[checked(quadCapacity * 4)];
            Indices = new ushort[checked(quadCapacity * 6)];
        }

        /// <summary>Releases the owned value arrays without deleting their elements.</summary>
        public void Dispose() {
            NativeOwnership.Release(ref Vertices);
            NativeOwnership.Release(ref Indices);
            VertexCount = 0;
            IndexCount = 0;
        }
    }
}
