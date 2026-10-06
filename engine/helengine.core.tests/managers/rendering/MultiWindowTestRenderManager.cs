namespace helengine.core.tests {
    /// <summary>
    /// Exercises portable window bookkeeping without creating graphics resources.
    /// </summary>
    public sealed class MultiWindowTestRenderManager : RenderManager3D {
        /// <summary>
        /// Rejects asset upload because this renderer exercises only window bookkeeping.
        /// </summary>
        /// <param name="data">Model asset that cannot be uploaded by this renderer.</param>
        /// <returns>No model is returned; this implementation always throws.</returns>
        public override RuntimeModel BuildModelFromRaw(ModelAsset data) {
            throw new NotSupportedException("This test renderer does not upload models.");
        }
    }
}
