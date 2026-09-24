namespace helengine {
    /// <summary>
    /// Receives camera state and synchronous borrows of populated CPU batch chunks.
    /// </summary>
    public abstract class Batch2DSink {
        /// <summary>Sets projection and pixel viewport state for later submissions.</summary>
        /// <param name="projection">The projection matrix for following batches.</param>
        /// <param name="pixelViewport">The viewport origin and dimensions ordered x, y, width, height.</param>
        public abstract void SetCamera(float4x4 projection, float4 pixelViewport);

        /// <summary>Consumes the borrowed buffer and run before returning; neither may be retained.</summary>
        /// <param name="buffer">The writer-owned arrays borrowed only for this call.</param>
        /// <param name="run">The shader, borrowed texture, and scissor key for this chunk.</param>
        public abstract void Submit([NativeNoEscape] Batch2DBuffer buffer, Batch2DRun run);
    }
}
