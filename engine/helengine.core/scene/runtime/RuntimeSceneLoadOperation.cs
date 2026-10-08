namespace helengine {
    /// <summary>
    /// Incrementally materializes one packaged scene so the runtime can publish real progress between frame boundaries.
    /// </summary>
    public sealed class RuntimeSceneLoadOperation : IDisposable {
        /// <summary>
        /// Service that resolves scene entities and owns asset-tracking lifecycle operations.
        /// </summary>
        readonly RuntimeSceneLoadService SceneLoadService;

        /// <summary>
        /// Serialized root entities awaiting materialization.
        /// </summary>
        readonly SceneEntityAsset[] RootEntityAssets;

        /// <summary>
        /// Runtime roots materialized by completed advances.
        /// </summary>
        readonly List<Entity> RootEntities;

        /// <summary>
        /// References collected only while this scene is materialized.
        /// </summary>
        [NativeOwnedMember]
        RuntimeSceneReferenceFixups ReferenceFixups;

        /// <summary>
        /// Index of the next serialized root entity to materialize.
        /// </summary>
        int NextRootEntityIndex;

        /// <summary>Remembers finalization after the result wrapper transfers to its caller.</summary>
        bool IsCompletedValue;

        /// <summary>Prevents using an operation after its temporary ownership has been released.</summary>
        bool IsDisposedValue;

        /// <summary>
        /// Owns the temporary result wrapper until consumption or disposal; its roots and assets transfer separately.
        /// </summary>
        [NativeOwnedMember]
        RuntimeSceneLoadResult ResultValue;

        /// <summary>
        /// Initializes an incremental operation for the supplied packaged scene.
        /// </summary>
        /// <param name="sceneLoadService">Service that materializes scene entities.</param>
        /// <param name="sceneAsset">Packaged scene payload to materialize.</param>
        internal RuntimeSceneLoadOperation(RuntimeSceneLoadService sceneLoadService, SceneAsset sceneAsset) {
            SceneLoadService = sceneLoadService ?? throw new ArgumentNullException(nameof(sceneLoadService));
            if (sceneAsset == null) {
                throw new ArgumentNullException(nameof(sceneAsset));
            }

            RootEntityAssets = sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>();
            RootEntities = new List<Entity>(RootEntityAssets.Length);
            ReferenceFixups = new RuntimeSceneReferenceFixups();
            SceneLoadService.BeginTrackedLoad();
        }

        /// <summary>
        /// Gets the normalized fraction of serialized root entities that have been materialized.
        /// </summary>
        public float Progress => RootEntityAssets.Length == 0
            ? (IsCompleted ? 1f : 0f)
            : (float)NextRootEntityIndex / RootEntityAssets.Length;

        /// <summary>
        /// Gets whether all roots and owned assets have been finalized.
        /// </summary>
        public bool IsCompleted => IsCompletedValue;

        /// <summary>
        /// Borrows the completed payload while it remains in this operation. The borrow becomes invalid after TakeResult or Dispose.
        /// </summary>
        [NativeBorrowedReturn]
        public RuntimeSceneLoadResult Result {
            get {
                if (ResultValue == null) {
                    throw new InvalidOperationException("The runtime scene load operation has no unconsumed completed result.");
                }

                return ResultValue;
            }
        }

        /// <summary>
        /// Transfers the same roots and asset set into a caller-owned result wrapper and releases this operation's wrapper.
        /// Does not copy entities or assets. The result can be consumed once, and previous Result borrows become invalid.
        /// </summary>
        /// <returns>Owned wrapper that retains the finalized scene's original root container and asset set.</returns>
        [NativeOwnedReturn]
        public RuntimeSceneLoadResult TakeResult() {
            if (ResultValue == null) {
                throw new InvalidOperationException("The runtime scene load operation has no unconsumed completed result.");
            }

            RuntimeSceneLoadResult result = new RuntimeSceneLoadResult(ResultValue.RootEntities, ResultValue.OwnedAssets);
            NativeOwnership.Release(ref ResultValue);
            return result;
        }

        /// <summary>
        /// Materializes at most one root entity and finalizes the scene when the final root has been processed.
        /// </summary>
        public void Advance() {
            if (IsDisposedValue) {
                throw new InvalidOperationException("Disposed runtime scene load operations cannot advance.");
            }
            if (IsCompleted) {
                return;
            }

            if (NextRootEntityIndex < RootEntityAssets.Length) {
                RootEntities.Add(SceneLoadService.LoadRootEntity(RootEntityAssets[NextRootEntityIndex], NextRootEntityIndex, ReferenceFixups));
                NextRootEntityIndex++;
            }

            if (NextRootEntityIndex == RootEntityAssets.Length) {
                RuntimeMeshPreparationService meshPreparationService = new RuntimeMeshPreparationService();
                for (int index = 0; index < RootEntities.Count; index++) {
                    meshPreparationService.Prepare(RootEntities[index], SceneLoadService.TrackPreparedModel);
                }
                ReferenceFixups.Bind(RootEntities);
                for (int index = 0; index < RootEntities.Count; index++) {
                    RootEntities[index].InitializeHierarchy();
                }

                NativeOwnership.Release(ref ResultValue);
                ResultValue = new RuntimeSceneLoadResult(RootEntities, SceneLoadService.CompleteTrackedLoad());
                IsCompletedValue = true;
            }
        }

        /// <summary>
        /// Releases temporary fixups and any unconsumed result wrapper without destroying the borrowed roots or asset set.
        /// Repeated disposal is safe and invalidates all Result borrows.
        /// </summary>
        public void Dispose() {
            NativeOwnership.Release(ref ResultValue);
            NativeOwnership.DisposeAndRelease(ref ReferenceFixups);
            IsDisposedValue = true;
        }
    }
}
