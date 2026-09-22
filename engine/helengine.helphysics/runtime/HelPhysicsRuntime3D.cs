namespace helengine {
    /// <summary>Adapts a scene-bound HelPhysics world to the core scheduler and component synchronization contracts.</summary>
    public sealed class HelPhysicsRuntime3D : ISceneBindablePhysicsRuntime, IPhysicsBodySynchronizationRuntime3D, IPhysicsTriggerEventRuntime3D, IDisposable
#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER
        , IPhysicsRuntimeProfilerMetricsProvider
#endif
    {
        /// <summary>Core supplying the scheduler, when this adapter was created for a core.</summary>
        readonly Core OwnerCore;

        /// <summary>Fixed simulation interval shared with the host scheduler.</summary>
        readonly double FixedStepSeconds;

        /// <summary>Owns the current scene binding and its simulation world.</summary>
        [NativeOwnedMember]
        HelPhysicsSceneBinder3D BinderValue;

        /// <summary>Prevents use after the adapter releases its scene resources.</summary>
        bool IsDisposedValue;

        /// <summary>Creates an adapter using the owning core scheduler interval.</summary>
        public HelPhysicsRuntime3D(Core ownerCore) {
            OwnerCore = ownerCore ?? throw new ArgumentNullException("ownerCore");
            FixedStepSeconds = ownerCore.PhysicsScheduler.StepSeconds;
        }

        /// <summary>Creates a standalone adapter with a positive finite fixed interval.</summary>
        public HelPhysicsRuntime3D(double fixedStepSeconds) {
            if (double.IsNaN(fixedStepSeconds) || double.IsInfinity(fixedStepSeconds) || fixedStepSeconds <= 0.0) {
                throw new ArgumentOutOfRangeException("fixedStepSeconds", "The HelPhysics runtime fixed step must be positive and finite.");
            }
            FixedStepSeconds = fixedStepSeconds;
        }

        /// <summary>Gets the owning core, or null for a standalone adapter.</summary>
        public Core Owner => OwnerCore;

        /// <summary>Gets the fixed interval required by this runtime.</summary>
        public double ConfiguredFixedStepSeconds => FixedStepSeconds;

        /// <summary>Gets the active scene binder, or null when unbound.</summary>
        public HelPhysicsSceneBinder3D Binder => BinderValue;

        /// <summary>Gets the active simulation world, or null when unbound.</summary>
        public HelPhysicsWorld3D World => (BinderValue == null) ? null : BinderValue.World;

        /// <summary>Gets the number of bodies registered by the current scene.</summary>
        public int RegisteredBodyCount => (BinderValue != null) ? BinderValue.Bindings.Count : 0;

        /// <summary>Gets current trigger events, or an empty collection when unbound.</summary>
        public IReadOnlyList<TriggerEvent3D> TriggerEvents => BinderValue == null ? Array.Empty<TriggerEvent3D>() : BinderValue.TriggerEvents;

        /// <summary>Builds a replacement scene binding and releases the previous binding only after success.</summary>
        public void BindScene(IReadOnlyList<Entity> rootEntities) {
            ThrowIfDisposed();
            if (rootEntities == null) {
                throw new ArgumentNullException("rootEntities");
            }
            int bodyCount = HelPhysicsSceneRuntimeSizing3D.CountPotentialBodies(rootEntities);
            HelPhysicsWorldSettings3D settings = HelPhysicsSceneRuntimeSizing3D.CreateSettings(bodyCount, FixedStepSeconds);
            HelPhysicsSceneBinder3D previous = BinderValue;
            HelPhysicsSceneBinder3D replacement = new HelPhysicsSceneBinder3D(new HelPhysicsWorld3D(settings), previous);
            bool replacementBound = false;
            try {
                replacement.BindScene(rootEntities);
                replacementBound = true;
            } finally {
                if (!replacementBound) {
                    replacement.Dispose();
                }
            }
            NativeOwnership.DisposeAndRelease(ref BinderValue);
            BinderValue = replacement;
        }

        /// <summary>Advances the bound world by one scheduler-compatible fixed step.</summary>
        public void Step(double stepSeconds) {
            ThrowIfDisposed();
            if (stepSeconds != FixedStepSeconds) {
                throw new ArgumentOutOfRangeException("stepSeconds", "HelPhysics runtime steps must equal the host scheduler fixed step.");
            }
            if (BinderValue != null) {
                BinderValue.Step();
            }
        }

        /// <summary>Transfers an authored kinematic transform into its bound simulation body.</summary>
        public void SynchronizeKinematicBody(Entity entity) {
            ThrowIfDisposed();
            RequireBinder().Synchronizer.SynchronizeKinematicBody(entity);
        }

        /// <summary>Transfers an authored dynamic transform and body state into the simulation.</summary>
        public void SynchronizeDynamicBody(Entity entity) {
            ThrowIfDisposed();
            RequireBinder().Synchronizer.SynchronizeDynamicBody(entity);
        }

        /// <summary>Transfers authored dynamic velocity without replacing the body transform.</summary>
        public void SynchronizeDynamicBodyVelocity(Entity entity) {
            ThrowIfDisposed();
            RequireBinder().Synchronizer.SynchronizeDynamicBodyVelocity(entity);
        }

#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER
        /// <summary>Reports world counters, using zero active bodies when no scene is bound.</summary>
        public bool TryGetRuntimeProfilerMetrics(out RuntimePhysicsProfilerMetrics metrics) {
            if (BinderValue == null) {
                metrics = new RuntimePhysicsProfilerMetrics(0);
                return true;
            }
            return BinderValue.World.TryGetRuntimeProfilerMetrics(out metrics);
        }
#endif

        /// <summary>Releases the scene binder and prevents subsequent simulation or synchronization.</summary>
        public void Dispose() {
            if (!IsDisposedValue) {
                IsDisposedValue = true;
            }
            NativeOwnership.DisposeAndRelease(ref BinderValue);
        }

        /// <summary>Requires a bound scene before resolving component synchronization services.</summary>
        HelPhysicsSceneBinder3D RequireBinder() {
            if (BinderValue == null) {
                throw new InvalidOperationException("A HelPhysics scene must be bound before body synchronization is requested.");
            }
            return BinderValue;
        }

        /// <summary>Rejects operations after owned scene resources have been released.</summary>
        void ThrowIfDisposed() {
            if (IsDisposedValue) {
                throw new InvalidOperationException("HelPhysicsRuntime3D has been disposed.");
            }
        }
    }



}
