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

        /// <summary>Owns materialization cursors, whose entity and payload references remain borrowed.</summary>
        [NativeOwnedMember]
        List<RuntimeSceneEntityLoadCursor> EntityCursors;
        /// <summary>Owns indices into cursors for depth-first materialization without recursion.</summary>
        [NativeOwnedMember]
        List<int> ActiveCursors;
        /// <summary>Owns initialization cursors; callbacks can change children before those children are visited.</summary>
        [NativeOwnedMember]
        List<RuntimeSceneEntityLoadCursor> InitializationCursors;
        /// <summary>Owns only a container borrowing current live entities for layout and preparation callbacks.</summary>
        [NativeOwnedMember]
        List<Entity> LiveEntities;
        /// <summary>Owns the mesh preparation service shared across individual component steps.</summary>
        [NativeOwnedMember]
        RuntimeMeshPreparationService MeshPreparation;
        /// <summary>Identifies materialize, mesh, bind, validate, initialize, layout and prepare phases before publication.</summary>
        RuntimeSceneLoadPhase Phase;
        /// <summary>Identifies the entity processed by the current preparation phase.</summary>
        int EntityIndex;
        /// <summary>Identifies the component processed by the current preparation phase.</summary>
        int ComponentIndex;
        /// <summary>Identifies the next root whose live hierarchy must initialize.</summary>
        int InitializationRootIndex;

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
            EntityCursors = new List<RuntimeSceneEntityLoadCursor>();
            ActiveCursors = new List<int>();
            InitializationCursors = new List<RuntimeSceneEntityLoadCursor>();
            LiveEntities = new List<Entity>();
            MeshPreparation = new RuntimeMeshPreparationService();
            SceneLoadService.BeginTrackedLoad();
        }

        /// <summary>
        /// Gets staged materialization and readiness progress, remaining below one until prepared roots are published.
        /// </summary>
        public float Progress => IsCompleted ? 1f : Phase == RuntimeSceneLoadPhase.Materialize
            ? (RootEntityAssets.Length == 0 ? 0f : 0.6f * NextRootEntityIndex / RootEntityAssets.Length)
            : 0.6f + (0.05f * ((int)Phase - 1));

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
        /// Advances one resumable entity, component or readiness step; an empty scene completes immediately.
        /// Synchronous loaders loop until completion. Frame-driven transitions use AdvanceForBudget for multiple timed steps.
        /// </summary>
        public void Advance() {
            if (IsDisposedValue) {
                throw new InvalidOperationException("Disposed runtime scene load operations cannot advance.");
            }
            if (IsCompleted) {
                return;
            }

            if (RootEntityAssets.Length == 0 && Phase == RuntimeSceneLoadPhase.Materialize) {
                Phase = RuntimeSceneLoadPhase.Publish;
            }
            AdvanceStep();
        }

        /// <summary>
        /// Advances multiple small steps until the monotonic time budget is consumed, always allowing one step to make progress.
        /// Component deserialization, mesh preparation, lifecycle/layout callbacks and final root activation cannot be interrupted and may exceed it.
        /// </summary>
        /// <param name="budgetMilliseconds">Finite positive milliseconds available for this frame.</param>
        /// <param name="clock">Borrowed monotonic clock; injectable for deterministic scheduling tests.</param>
        public void AdvanceForBudget(double budgetMilliseconds, IRuntimeSceneLoadingClock clock) {
            if (IsDisposedValue) {
                throw new InvalidOperationException("Disposed runtime scene load operations cannot advance.");
            }
            if (double.IsNaN(budgetMilliseconds) || double.IsInfinity(budgetMilliseconds) || budgetMilliseconds <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(budgetMilliseconds), "Loading budget must be finite and greater than zero.");
            }
            if (clock == null) {
                throw new ArgumentNullException(nameof(clock));
            }
            double start = clock.ElapsedMilliseconds;
            if (double.IsNaN(start) || double.IsInfinity(start)) {
                throw new InvalidOperationException("Scene loading clock must return finite milliseconds.");
            }
            double previous = start;
            while (!IsCompleted) {
                AdvanceStep();
                double now = clock.ElapsedMilliseconds;
                if (double.IsNaN(now) || double.IsInfinity(now) || now < previous) {
                    throw new InvalidOperationException("Scene loading clock must be monotonic.");
                }
                previous = now;
                if (now - start >= budgetMilliseconds) {
                    break;
                }
            }
        }

        /// <summary>Performs one entity, component or readiness step without exposing the unfinished root hierarchy.</summary>
        void AdvanceStep() {
            if (Phase == RuntimeSceneLoadPhase.Materialize) {
                MaterializeStep();
            } else if (Phase == RuntimeSceneLoadPhase.PrepareMeshes) {
                PrepareMeshStep();
            } else if (Phase == RuntimeSceneLoadPhase.BindReferences) {
                if (EntityIndex < EntityCursors.Count) {
                    ReferenceFixups.BindSingleEntity(EntityCursors[EntityIndex].Entity);
                    EntityIndex++;
                } else {
                    MoveToPhase(RuntimeSceneLoadPhase.ValidateReferences);
                }
            } else if (Phase == RuntimeSceneLoadPhase.ValidateReferences) {
                if (EntityIndex < ReferenceFixups.RequestCount) {
                    ReferenceFixups.ValidateRequest(EntityIndex);
                    EntityIndex++;
                } else {
                    ReferenceFixups.Clear();
                    MoveToPhase(RuntimeSceneLoadPhase.Initialize);
                }
            } else if (Phase == RuntimeSceneLoadPhase.Initialize) {
                InitializeStep();
            } else if (Phase == RuntimeSceneLoadPhase.ResolveLayout || Phase == RuntimeSceneLoadPhase.PreparePresentation) {
                PreparePresentationStep();
            } else {
                NativeOwnership.Release(ref ResultValue);
                ResultValue = new RuntimeSceneLoadResult(RootEntities, SceneLoadService.CompleteTrackedLoad());
                // The published scene's owner disposes its roots explicitly before deleting this container.
                // Preserve owning construction, then transfer element cleanup at the publication boundary.
                NativeOwnership.DetachOwned(RootEntities);
                for (int index = 0; index < RootEntities.Count; index++) {
                    RootEntities[index].RuntimeSuppressed = false;
                }
                IsCompletedValue = true;
            }
        }

        /// <summary>Resets per-phase cursors without changing live authored entity state.</summary>
        /// <param name="phase">Next readiness phase to execute.</param>
        void MoveToPhase(RuntimeSceneLoadPhase phase) {
            Phase = phase;
            EntityIndex = 0;
            ComponentIndex = 0;
        }

        /// <summary>Creates one shell, deserializes one component, or descends one child before yielding.</summary>
        void MaterializeStep() {
            if (ActiveCursors.Count == 0) {
                if (NextRootEntityIndex == RootEntityAssets.Length) {
                    MoveToPhase(RuntimeSceneLoadPhase.PrepareMeshes);
                    return;
                }
                SceneEntityAsset asset = RootEntityAssets[NextRootEntityIndex];
                Entity root = SceneLoadService.CreateEntityShell(asset, true);
                EntityCursors.Add(new RuntimeSceneEntityLoadCursor(asset, root, NextRootEntityIndex, 0));
                RootEntities.Add(root);
                ActiveCursors.Add(EntityCursors.Count - 1);
                NextRootEntityIndex++;
                return;
            }
            RuntimeSceneEntityLoadCursor cursor = EntityCursors[ActiveCursors[ActiveCursors.Count - 1]];
            SceneComponentAssetRecord[] components = cursor.Asset.Components;
            if (components != null && cursor.NextComponent < components.Length) {
                SceneLoadService.LoadEntityComponent(cursor, components[cursor.NextComponent], ReferenceFixups);
                cursor.NextComponent++;
                return;
            }
            SceneEntityAsset[] children = cursor.Asset.Children;
            if (children != null && cursor.NextChild < children.Length) {
                SceneEntityAsset childAsset = children[cursor.NextChild];
                Entity child = SceneLoadService.CreateEntityShell(childAsset, false);
                EntityCursors.Add(new RuntimeSceneEntityLoadCursor(childAsset, child, cursor.RootIndex, cursor.Depth + 1));
                cursor.Entity.AddChild(child);
                cursor.NextChild++;
                ActiveCursors.Add(EntityCursors.Count - 1);
                return;
            }
            ActiveCursors.RemoveAt(ActiveCursors.Count - 1);
        }

        /// <summary>Prepares one mesh component; a single tessellation or upload remains an indivisible operation.</summary>
        void PrepareMeshStep() {
            if (EntityIndex == EntityCursors.Count) {
                MoveToPhase(RuntimeSceneLoadPhase.BindReferences);
                return;
            }
            Entity entity = EntityCursors[EntityIndex].Entity;
            if (ComponentIndex < entity.Components.Count) {
                if (entity.Components[ComponentIndex] is MeshComponent mesh) {
                    MeshPreparation.PrepareMeshComponent(mesh, SceneLoadService.TrackPreparedModel);
                }
                ComponentIndex++;
            } else {
                EntityIndex++;
                ComponentIndex = 0;
            }
        }

        /// <summary>Initializes one component and follows the current child list only after its parent's callbacks finish.</summary>
        void InitializeStep() {
            if (ActiveCursors.Count == 0) {
                if (InitializationRootIndex == RootEntities.Count) {
                    MoveToPhase(RuntimeSceneLoadPhase.ResolveLayout);
                    return;
                }
                PushInitializationCursor(RootEntities[InitializationRootIndex]);
                InitializationRootIndex++;
                return;
            }
            RuntimeSceneEntityLoadCursor cursor = InitializationCursors[ActiveCursors[ActiveCursors.Count - 1]];
            if (cursor.Entity.Components != null && cursor.NextComponent < cursor.ComponentLimit
                && cursor.NextComponent < cursor.Entity.Components.Count) {
                cursor.Entity.InitializeSceneComponent(cursor.NextComponent);
                cursor.NextComponent++;
            } else if (cursor.Entity.Children != null && cursor.NextChild < cursor.Entity.Children.Count) {
                Entity child = cursor.Entity.Children[cursor.NextChild];
                cursor.NextChild++;
                PushInitializationCursor(child);
            } else {
                ActiveCursors.RemoveAt(ActiveCursors.Count - 1);
            }
        }

        /// <summary>Captures one live entity for readiness and prevents duplicate initialization of children created by callbacks.</summary>
        /// <param name="entity">Borrowed live hierarchy node.</param>
        void PushInitializationCursor([NativeRetainsBorrow] Entity entity) {
            InitializationCursors.Add(new RuntimeSceneEntityLoadCursor(null, entity, 0, 0) {
                ComponentLimit = entity.IsInitialized || entity.Components == null ? 0 : entity.Components.Count
            });
            entity.BeginSceneInitialization();
            ActiveCursors.Add(InitializationCursors.Count - 1);
            LiveEntities.Add(entity);
        }

        /// <summary>Resolves core layout, then runs only explicit readiness callbacks; gameplay and input updates never run here.</summary>
        void PreparePresentationStep() {
            if (EntityIndex == LiveEntities.Count) {
                MoveToPhase(Phase == RuntimeSceneLoadPhase.ResolveLayout
                    ? RuntimeSceneLoadPhase.PreparePresentation : RuntimeSceneLoadPhase.Publish);
                return;
            }
            Entity entity = LiveEntities[EntityIndex];
            if (entity.Components != null && ComponentIndex < entity.Components.Count) {
                Component component = entity.Components[ComponentIndex];
                if (Phase == RuntimeSceneLoadPhase.ResolveLayout) {
                    if (component is ViewportComponent viewport) {
                        viewport.PrepareSceneLayout();
                    } else if (component is ReferenceCanvasFitComponent canvas) {
                        canvas.PrepareSceneLayout();
                    } else if (component is LayoutComponent layout) {
                        layout.RefreshAnchoring();
                    }
                } else if (ComponentExecutionPolicy.ShouldRunComponentLifecycle(component, entity)) {
                    component.ScenePrepared(entity);
                }
                ComponentIndex++;
            } else {
                EntityIndex++;
                ComponentIndex = 0;
            }
        }

        /// <summary>
        /// Releases temporary fixups and any unconsumed result wrapper without destroying the borrowed roots or asset set.
        /// Repeated disposal is safe and invalidates all Result borrows.
        /// </summary>
        public void Dispose() {
            NativeOwnership.Release(ref ResultValue);
            NativeOwnership.DisposeAndRelease(ref ReferenceFixups);
            NativeOwnership.Release(ref EntityCursors);
            NativeOwnership.Release(ref ActiveCursors);
            NativeOwnership.Release(ref InitializationCursors);
            if (LiveEntities != null) {
                NativeOwnership.DetachOwned(LiveEntities);
            }
            NativeOwnership.Release(ref LiveEntities);
            NativeOwnership.Release(ref MeshPreparation);
            IsDisposedValue = true;
        }
    }
}
