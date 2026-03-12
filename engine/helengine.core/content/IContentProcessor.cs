namespace helengine {
    /// <summary>
    /// Describes a processor that reads content from a stream and produces a strongly typed value.
    /// </summary>
    public interface IContentProcessor {
        /// <summary>
        /// Gets the output type produced by this processor.
        /// </summary>
        Type OutputType { get; }

        /// <summary>
        /// Reads content from the supplied stream and returns the processed value as an object.
        /// </summary>
        /// <param name="stream">Stream containing the source data to parse.</param>
        /// <returns>Processed content value.</returns>
        object ReadObject(Stream stream);
    }

    /// <summary>
    /// Describes a processor that reads content from a stream and produces a specific type.
    /// </summary>
    /// <typeparam name="T">Type produced by the processor.</typeparam>
    public interface IContentProcessor<T> : IContentProcessor {
        /// <summary>
        /// Reads content from the supplied stream and returns the processed value.
        /// </summary>
        /// <param name="stream">Stream containing the source data to parse.</param>
        /// <returns>Processed content value.</returns>
        T Read(Stream stream);
    }
}
