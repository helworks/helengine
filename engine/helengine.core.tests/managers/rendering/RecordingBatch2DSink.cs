using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>
    /// Records synchronous camera updates and copies every submitted batch for behavioral assertions.
    /// </summary>
    public sealed class RecordingBatch2DSink : Batch2DSink {
        /// <summary>
        /// Stores the runs observed at submission time in their original order.
        /// </summary>
        public List<Batch2DRun> Runs { get; } = new List<Batch2DRun>();

        /// <summary>
        /// Stores independent copies of each vertex chunk before Submit returns.
        /// </summary>
        public List<Batch2DVertex[]> Vertices { get; } = new List<Batch2DVertex[]>();

        /// <summary>
        /// Stores independent copies of each index chunk before Submit returns.
        /// </summary>
        public List<ushort[]> Indices { get; } = new List<ushort[]>();

        /// <summary>
        /// Stores projection values supplied to SetCamera in call order.
        /// </summary>
        public List<float4x4> Cameras { get; } = new List<float4x4>();

        /// <summary>Stores pixel viewport values supplied to SetCamera in call order.</summary>
        public List<float4> Viewports { get; } = new List<float4>();

        /// <summary>Records camera and submission operations in their actual call order.</summary>
        public List<string> Events { get; } = new List<string>();

        /// <summary>
        /// Gets the latest projection supplied to SetCamera.
        /// </summary>
        public float4x4 CurrentCamera { get; private set; }

        /// <summary>Gets the latest viewport supplied to SetCamera.</summary>
        public float4 CurrentViewport { get; private set; }

        /// <summary>Gets or sets an exception thrown before a submitted chunk is copied.</summary>
        public Exception SubmitFailure { get; set; }

        /// <summary>Gets or sets an exception thrown while installing camera state.</summary>
        public Exception CameraFailure { get; set; }

        /// <summary>Counts every attempted chunk submission, including a configured failure.</summary>
        public int SubmitCalls { get; private set; }

        /// <summary>Counts every attempted camera update, including a configured failure.</summary>
        public int CameraCalls { get; private set; }

        /// <summary>
        /// Copies current buffer values before returning to prove the sink's synchronous borrow contract.
        /// </summary>
        /// <param name="buffer">The temporary batch data borrowed for this call.</param>
        /// <param name="run">The run key that applies to the copied data.</param>
        public override void Submit(Batch2DBuffer buffer, Batch2DRun run) {
            SubmitCalls++;
            Events.Add("Submit:" + run.ScissorX);
            if (SubmitFailure != null) {
                throw SubmitFailure;
            }

            Batch2DVertex[] vertices = new Batch2DVertex[buffer.VertexCount];
            Array.Copy(buffer.Vertices, vertices, buffer.VertexCount);
            ushort[] indices = new ushort[buffer.IndexCount];
            Array.Copy(buffer.Indices, indices, buffer.IndexCount);
            Runs.Add(run);
            Vertices.Add(vertices);
            Indices.Add(indices);
        }

        /// <summary>
        /// Records the camera state forwarded by the writer.
        /// </summary>
        /// <param name="projection">The projection matrix for subsequent batches.</param>
        /// <param name="pixelViewport">The pixel viewport for subsequent batches.</param>
        public override void SetCamera(float4x4 projection, float4 pixelViewport) {
            CameraCalls++;
            Events.Add("SetCamera:" + projection.M11);
            if (CameraFailure != null) {
                throw CameraFailure;
            }

            CurrentCamera = projection;
            CurrentViewport = pixelViewport;
            Cameras.Add(projection);
            Viewports.Add(pixelViewport);
        }
    }
}
