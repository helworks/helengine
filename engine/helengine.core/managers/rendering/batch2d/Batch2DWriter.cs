namespace helengine {
    /// <summary>
    /// Collects copied quad values into bounded chunks and submits adjacent equal run keys in order.
    /// </summary>
    public sealed class Batch2DWriter : IDisposable {
        /// <summary>Sets the smallest supported quad capacity.</summary>
        public const int MinimumQuadCapacity = 1;
        /// <summary>Sets the largest capacity whose vertex indices fit in an unsigned short.</summary>
        public const int MaximumQuadCapacity = 16384;

        /// <summary>Holds the sink borrowed for synchronous submissions.</summary>
        Batch2DSink Sink;
        /// <summary>Owns the fixed vertex and index arrays reused by this writer.</summary>
        [NativeOwnedMember]
        Batch2DBuffer Buffer;
        /// <summary>Stores the maximum quads in one reusable chunk.</summary>
        int QuadCapacity;
        /// <summary>Stores the complete quads currently held in the buffer.</summary>
        int QuadCount;
        /// <summary>Indicates whether camera state has been installed on the sink.</summary>
        bool HasCamera;
        /// <summary>Indicates whether the current chunk has a run key.</summary>
        bool HasRun;
        /// <summary>Stores the run key shared by buffered quads.</summary>
        Batch2DRun CurrentRun;
        /// <summary>Indicates that CPU storage has been released.</summary>
        bool IsDisposed;
        /// <summary>Prevents retries after a sink failure or invalidated borrowed texture.</summary>
        bool IsFaulted;

        /// <summary>Creates a bounded writer that borrows its sink for the writer's lifetime.</summary>
        /// <param name="sink">The sink that consumes submitted chunks synchronously.</param>
        /// <param name="quadCapacity">The number of quads stored in each reusable chunk.</param>
        public Batch2DWriter([NativeRetainsBorrow] Batch2DSink sink, int quadCapacity) {
            if (sink == null) {
                throw new ArgumentNullException(nameof(sink));
            }
            if (quadCapacity < MinimumQuadCapacity || quadCapacity > MaximumQuadCapacity) {
                throw new ArgumentOutOfRangeException(nameof(quadCapacity),
                    $"Quad capacity must be between {MinimumQuadCapacity} and {MaximumQuadCapacity}.");
            }

            Sink = sink;
            QuadCapacity = quadCapacity;
            Buffer = new Batch2DBuffer(quadCapacity);
        }

        /// <summary>Sets finite projection and viewport state, flushing geometry before a change.</summary>
        /// <param name="projection">The finite projection matrix for subsequent quads.</param>
        /// <param name="pixelViewport">The finite viewport ordered x, y, width, and height.</param>
        public void ConfigureCamera(float4x4 projection, float4 pixelViewport) {
            ThrowIfUnavailable();
            ValidateCamera(projection, pixelViewport);
            Flush();

            try {
                Sink.SetCamera(projection, pixelViewport);
                HasCamera = true;
            }
            catch {
                IsFaulted = true;
                throw;
            }
        }

        /// <summary>Validates and copies one quad while preserving original run order.</summary>
        /// <param name="run">The shader, borrowed texture, and scissor state for this quad.</param>
        /// <param name="a">The first vertex copied into the chunk.</param>
        /// <param name="b">The second vertex copied into the chunk.</param>
        /// <param name="c">The third vertex copied into the chunk.</param>
        /// <param name="d">The fourth vertex copied into the chunk.</param>
        public void AppendQuad(Batch2DRun run, Batch2DVertex a, Batch2DVertex b, Batch2DVertex c, Batch2DVertex d) {
            ThrowIfUnavailable();
            if (!HasCamera) {
                throw new InvalidOperationException("ConfigureCamera must be called before appending a quad.");
            }

            ValidateRun(run);
            ValidateVertex(a, nameof(a));
            ValidateVertex(b, nameof(b));
            ValidateVertex(c, nameof(c));
            ValidateVertex(d, nameof(d));
            if (run.ScissorWidth == 0 || run.ScissorHeight == 0) {
                return;
            }

            if (HasRun && (!SameRun(CurrentRun, run) || QuadCount == QuadCapacity)) {
                Flush();
            }

            int vertexOffset = Buffer.VertexCount;
            int indexOffset = Buffer.IndexCount;
            Buffer.Vertices[vertexOffset] = a;
            Buffer.Vertices[vertexOffset + 1] = b;
            Buffer.Vertices[vertexOffset + 2] = c;
            Buffer.Vertices[vertexOffset + 3] = d;
            Buffer.Indices[indexOffset] = checked((ushort)vertexOffset);
            Buffer.Indices[indexOffset + 1] = checked((ushort)(vertexOffset + 1));
            Buffer.Indices[indexOffset + 2] = checked((ushort)(vertexOffset + 2));
            Buffer.Indices[indexOffset + 3] = checked((ushort)vertexOffset);
            Buffer.Indices[indexOffset + 4] = checked((ushort)(vertexOffset + 2));
            Buffer.Indices[indexOffset + 5] = checked((ushort)(vertexOffset + 3));
            Buffer.VertexCount += 4;
            Buffer.IndexCount += 6;
            QuadCount++;
            CurrentRun = run;
            HasRun = true;
        }

        /// <summary>Submits pending geometry and resets counts only after successful consumption.</summary>
        public void Flush() {
            ThrowIfUnavailable();
            if (QuadCount == 0) {
                return;
            }

            try {
                ValidateRun(CurrentRun);
                Sink.Submit(Buffer, CurrentRun);
                Buffer.VertexCount = 0;
                Buffer.IndexCount = 0;
                QuadCount = 0;
                HasRun = false;
            }
            catch {
                IsFaulted = true;
                throw;
            }
        }

        /// <summary>Releases owned CPU arrays without submitting or disposing borrowed objects.</summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            Buffer.Dispose();
            NativeOwnership.Release(ref Buffer);
            Sink = null;
            IsDisposed = true;
        }

        /// <summary>Rejects calls after disposal or after a failed submission.</summary>
        void ThrowIfUnavailable() {
            if (IsDisposed) {
                throw new ObjectDisposedException(nameof(Batch2DWriter));
            }
            if (IsFaulted) {
                throw new InvalidOperationException("The batch writer is faulted and must be disposed.");
            }
        }

        /// <summary>Checks that all camera values are finite and viewport dimensions are positive.</summary>
        static void ValidateCamera(float4x4 projection, float4 pixelViewport) {
            if (!IsFinite(projection.M11) || !IsFinite(projection.M12) || !IsFinite(projection.M13) || !IsFinite(projection.M14) ||
                !IsFinite(projection.M21) || !IsFinite(projection.M22) || !IsFinite(projection.M23) || !IsFinite(projection.M24) ||
                !IsFinite(projection.M31) || !IsFinite(projection.M32) || !IsFinite(projection.M33) || !IsFinite(projection.M34) ||
                !IsFinite(projection.M41) || !IsFinite(projection.M42) || !IsFinite(projection.M43) || !IsFinite(projection.M44)) {
                throw new ArgumentException("Projection components must be finite.", nameof(projection));
            }
            if (!IsFinite(pixelViewport.X) || !IsFinite(pixelViewport.Y) ||
                !IsFinite(pixelViewport.Z) || !IsFinite(pixelViewport.W) ||
                pixelViewport.Z <= 0f || pixelViewport.W <= 0f) {
                throw new ArgumentException("Viewport origin must be finite and dimensions must be finite and positive.", nameof(pixelViewport));
            }
        }

        /// <summary>Checks the required borrowed texture is live before storage or submission.</summary>
        static void ValidateRun(Batch2DRun run) {
            if (run.Variant == Batch2DVariant.Textured) {
                if (run.Texture == null) {
                    throw new ArgumentException("Textured batch runs require a runtime texture.", nameof(run));
                }
                if (run.Texture.IsDisposed) {
                    throw new ObjectDisposedException(nameof(run.Texture), "A disposed texture cannot be used by a batch run.");
                }
            }
            else if (run.Variant != Batch2DVariant.RoundedShape) {
                throw new ArgumentException("The batch shader variant is not supported.", nameof(run));
            }
        }

        /// <summary>Rejects nonfinite values in any of the six packed vertex vectors.</summary>
        static void ValidateVertex(Batch2DVertex vertex, string parameterName) {
            if (!IsFinite(vertex.Position.X) || !IsFinite(vertex.Position.Y) || !IsFinite(vertex.Position.Z) || !IsFinite(vertex.Position.W) ||
                !IsFinite(vertex.TexLocal.X) || !IsFinite(vertex.TexLocal.Y) || !IsFinite(vertex.TexLocal.Z) || !IsFinite(vertex.TexLocal.W) ||
                !IsFinite(vertex.Color.X) || !IsFinite(vertex.Color.Y) || !IsFinite(vertex.Color.Z) || !IsFinite(vertex.Color.W) ||
                !IsFinite(vertex.Shape.X) || !IsFinite(vertex.Shape.Y) || !IsFinite(vertex.Shape.Z) || !IsFinite(vertex.Shape.W) ||
                !IsFinite(vertex.Corners.X) || !IsFinite(vertex.Corners.Y) || !IsFinite(vertex.Corners.Z) || !IsFinite(vertex.Corners.W) ||
                !IsFinite(vertex.BorderColor.X) || !IsFinite(vertex.BorderColor.Y) || !IsFinite(vertex.BorderColor.Z) || !IsFinite(vertex.BorderColor.W)) {
                throw new ArgumentException("Every batch vertex component must be finite.", parameterName);
            }
        }

        /// <summary>Determines whether one component is neither NaN nor infinite.</summary>
        static bool IsFinite(float value) {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>Compares run keys using reference identity for their borrowed texture.</summary>
        static bool SameRun(Batch2DRun left, Batch2DRun right) {
            return left.Variant == right.Variant && ReferenceEquals(left.Texture, right.Texture) &&
                left.ScissorX == right.ScissorX && left.ScissorY == right.ScissorY &&
                left.ScissorWidth == right.ScissorWidth && left.ScissorHeight == right.ScissorHeight;
        }
    }
}
