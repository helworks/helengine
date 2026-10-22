namespace helengine {
    /// <summary>Retains serialized content through preparation and loaded callbacks. Disposal ends this read scope or cancels a pending preload; outstanding streams retain their own source bytes.</summary>
    public interface IRuntimeSceneContentLoadOperation : IDisposable {
        /// <summary>Gets whether every declared asset can be opened without starting or waiting for I/O.</summary>
        bool IsCompleted { get; }
        /// <summary>Performs one bounded cache preparation step or polls pending I/O without treating incomplete input as end of stream.</summary>
        void Advance();
    }
}
