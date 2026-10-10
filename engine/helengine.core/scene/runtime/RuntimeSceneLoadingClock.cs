namespace helengine {
    /// <summary>Measures scene-load work using the monotonic stopwatch supported by managed and generated runtimes.</summary>
    public sealed class RuntimeSceneLoadingClock : IRuntimeSceneLoadingClock, IDisposable {
        /// <summary>Owns the monotonic timer for this scene manager's lifetime.</summary>
        [NativeOwnedMember]
        System.Diagnostics.Stopwatch Timer = new System.Diagnostics.Stopwatch();
        /// <summary>Starts the explicitly owned timer once for monotonic observations across all frame budgets.</summary>
        public RuntimeSceneLoadingClock() { Timer.Start(); }
        /// <summary>Gets monotonic milliseconds without restarting the timer between frame budgets.</summary>
        public double ElapsedMilliseconds {
            get {
                if (Timer == null) { throw new InvalidOperationException("Disposed scene loading clocks cannot be observed."); }
                return Timer.Elapsed.TotalMilliseconds;
            }
        }
        /// <summary>Releases the owned stopwatch once and invalidates future observations; repeated disposal is safe.</summary>
        public void Dispose() { NativeOwnership.Release(ref Timer); }
    }
}
