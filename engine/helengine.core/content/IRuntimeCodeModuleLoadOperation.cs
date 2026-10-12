namespace helengine {
    /// <summary>Owns cooperative native code input, validation and initialization until one completed module handle is transferred.</summary>
    public interface IRuntimeCodeModuleLoadOperation : IDisposable {
        /// <summary>Gets whether verified code and its initialization are ready for registration.</summary>
        bool IsCompleted { get; }
        /// <summary>Performs one bounded loading step or polls an outstanding asynchronous read without blocking for I/O.</summary>
        void Advance();
        /// <summary>Transfers the ready module exactly once; disposal of the operation must not release the transferred code.</summary>
        /// <returns>Owned resident adapter for the loaded native module.</returns>
        [NativeOwnedReturn]
        IRuntimeCodeModule TakeModule();
    }
}
