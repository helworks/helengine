namespace helengine {
    /// <summary>
    /// Receives optional high-resolution CPU timing samples from Core for native-host profiling.
    /// </summary>
    public interface IRuntimeCpuProfileSink {
        /// <summary>
        /// Reads the host monotonic clock in microseconds.
        /// </summary>
        /// <returns>The current monotonic timestamp.</returns>
        ulong ReadMicroseconds();

        /// <summary>
        /// Records one fixed Core phase duration.
        /// </summary>
        /// <param name="stage">Stable phase identifier.</param>
        /// <param name="elapsedMicroseconds">Elapsed phase duration.</param>
        void RecordStage(RuntimeCpuProfileStage stage, ulong elapsedMicroseconds);

        /// <summary>
        /// Records one updateable execution duration and its stable diagnostic identity.
        /// </summary>
        /// <param name="item">Updateable instance that was executed.</param>
        /// <param name="index">Ordered update-list index.</param>
        /// <param name="typeHash">Stable updateable type-name hash.</param>
        /// <param name="ownerSceneEntityId">Owning scene entity id, when available.</param>
        /// <param name="elapsedMicroseconds">Elapsed update duration.</param>
        void RecordUpdateable(IUpdateable item, int index, uint typeHash, uint ownerSceneEntityId, ulong elapsedMicroseconds);

        /// <summary>
        /// Registers a readable type-name legend entry outside the timed update loop.
        /// </summary>
        /// <param name="item">Updateable instance whose concrete native identity may be used by the host.</param>
        /// <param name="typeHash">Stable updateable type-name hash.</param>
        /// <param name="typeName">Readable updateable type name.</param>
        void RegisterType(IUpdateable item, uint typeHash, string typeName);

        /// <summary>
        /// Reports that the host clock moved backwards or otherwise failed monotonicity.
        /// </summary>
        void ClockFault();
    }
}