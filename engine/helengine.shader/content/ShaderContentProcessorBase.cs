namespace helengine {
    /// <summary>
    /// Provides a generic shader-owned content-processor base so generated native output preserves the concrete runtime type token for shader payload registration.
    /// </summary>
    /// <typeparam name="T">Shader-owned content type produced by the processor.</typeparam>
    public abstract class ShaderContentProcessorBase<T> : IContentProcessor<T> {
        /// <summary>
        /// Gets the output type produced by this processor.
        /// </summary>
        public Type OutputType => typeof(T);

        /// <summary>
        /// Reads one shader-owned payload from the supplied stream.
        /// </summary>
        /// <param name="stream">Content stream positioned at the serialized payload.</param>
        /// <returns>Deserialized shader-owned payload.</returns>
        public abstract T Read(Stream stream);

        /// <summary>
        /// Reads one shader-owned payload from the supplied stream and returns it through the non-generic processor contract.
        /// </summary>
        /// <param name="stream">Content stream positioned at the serialized payload.</param>
        /// <returns>Deserialized shader-owned payload boxed as an object.</returns>
        object IContentProcessor.ReadObject(Stream stream) {
            return Read(stream);
        }
    }
}
