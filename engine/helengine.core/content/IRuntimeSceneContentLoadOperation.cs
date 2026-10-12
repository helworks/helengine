namespace helengine {
    /// <summary>Retains prefetched content until scene unload, including later component reads and dynamic spawns; disposal also cancels incomplete preloads.</summary>
    public interface IRuntimeSceneContentLoadOperation : IDisposable {
        /// <summary>Gets whether every declared asset can be opened without starting or waiting for I/O.</summary>
        bool IsCompleted { get; }
        /// <summary>Performs one bounded cache preparation step or polls pending I/O without treating incomplete input as end of stream.</summary>
        void Advance();
    }
}
