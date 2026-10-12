namespace helengine {
    /// <summary>Acquires a dependency-first code closure without waiting synchronously for platform IO.</summary>
    public sealed class SceneModuleLoadOperation : IDisposable {
        /// <summary>Borrowed manager receiving loaded handles and completed scene references.</summary>
        readonly SceneModuleManager Manager;
        /// <summary>Borrowed resident platform provider.</summary>
        readonly IRuntimeCodeModuleProvider Provider;
        /// <summary>Scene whose lifetime will retain the completed closure.</summary>
        readonly string SceneId;
        /// <summary>Owned manifest indices retained until acquisition disposal; committed scenes keep an independent closure copy.</summary>
        [NativeOwnedMember] int[] Indices;
        /// <summary>Owned pending platform operation, which retains partial allocations.</summary>
        [NativeOwnedMember] IRuntimeCodeModuleLoadOperation CurrentOperation;
        /// <summary>Number of acquired references, including shared modules.</summary>
        int Cursor;
        /// <summary>Total references in the deduplicated closure.</summary>
        readonly int Total;
        /// <summary>Records transfer of reference ownership to the loaded scene.</summary>
        bool Committed;
        /// <summary>Prevents double cancellation or use after disposal.</summary>
        bool IsDisposed;

        /// <summary>Creates an acquisition for one validated dependency closure.</summary>
        /// <param name="manager">Owning manager.</param>
        /// <param name="provider">Resident loader.</param>
        /// <param name="sceneId">Scene identity.</param>
        /// <param name="indices">Owned dependency-first indices.</param>
        internal SceneModuleLoadOperation(SceneModuleManager manager, IRuntimeCodeModuleProvider provider, string sceneId, [NativeTakesOwnership] int[] indices) {
            Manager = manager;
            Provider = provider;
            SceneId = sceneId;
            Indices = indices;
            Total = indices.Length;
        }

        /// <summary>Gets whether all required code is registered and can safely construct scene components.</summary>
        public bool IsCompleted => Cursor == Total;
        /// <summary>Gets completed references relative to the complete closure.</summary>
        public double Progress => Total == 0 ? 1d : (double)Cursor / Total;

        /// <summary>Advances one module load or shared reference; a pending IO request returns immediately.</summary>
        public void Advance() {
            if (IsDisposed || Committed) { throw new InvalidOperationException("A consumed module acquisition cannot advance."); }
            if (IsCompleted) { return; }
            RuntimeCodeModuleState state = Manager.GetState(Indices[Cursor]);
            if (state.Module == null) {
                if (CurrentOperation == null) {
                    NativeOwnership.Release(ref CurrentOperation);
                    CurrentOperation = Provider.BeginLoad(state.Entry);
                    if (CurrentOperation == null) { throw new InvalidOperationException("Module providers must return an owned load operation."); }
                }
                CurrentOperation.Advance();
                if (!CurrentOperation.IsCompleted) { return; }
                IRuntimeCodeModule module = CurrentOperation.TakeModule();
                Manager.InstallModule(Indices[Cursor], module);
                NativeOwnership.DisposeAndRelease(ref CurrentOperation);
            }
            state.ReferenceCount++;
            Cursor++;
        }

        /// <summary>Processes ready dependency steps within the existing scene budget, never polling pending IO in a busy loop.</summary>
        /// <param name="budgetMilliseconds">Finite positive cooperative frame budget.</param>
        /// <param name="clock">Borrowed monotonic scene loading clock.</param>
        public void AdvanceForBudget(double budgetMilliseconds, IRuntimeSceneLoadingClock clock) {
            if (clock == null) { throw new ArgumentNullException(nameof(clock)); }
            if (double.IsNaN(budgetMilliseconds) || double.IsInfinity(budgetMilliseconds) || budgetMilliseconds <= 0d) { throw new ArgumentOutOfRangeException(nameof(budgetMilliseconds)); }
            double start = clock.ElapsedMilliseconds;
            if (double.IsNaN(start) || double.IsInfinity(start)) { throw new InvalidOperationException("Scene loading clock must return finite milliseconds."); }
            double previousTime = start;
            do {
                int previous = Cursor;
                Advance();
                double now = clock.ElapsedMilliseconds;
                if (double.IsNaN(now) || double.IsInfinity(now) || now < previousTime) { throw new InvalidOperationException("Scene loading clock must be monotonic."); }
                previousTime = now;
                if (Cursor == previous) { return; }
                if (now - start >= budgetMilliseconds) { return; }
            } while (!IsCompleted);
        }

        /// <summary>Transfers the acquired references to the loaded-scene lifetime before deserialization can use their factories.</summary>
        public void CommitSceneLease() {
            if (IsDisposed || Committed || !IsCompleted) { throw new InvalidOperationException("Only one completed module acquisition can be committed."); }
            Manager.CommitSceneLease(SceneId, Indices);
            Committed = true;
        }

        /// <summary>Cancels partial loading and releases acquired references in reverse dependency order.</summary>
        public void Dispose() {
            NativeOwnership.DisposeAndRelease(ref CurrentOperation);
            if (!IsDisposed && !Committed) {
                for (int index = Cursor - 1; index >= 0; index--) { Manager.ReleaseReference(Indices[index]); }
            }
            NativeOwnership.Release(ref Indices);
            IsDisposed = true;
        }
    }
}
