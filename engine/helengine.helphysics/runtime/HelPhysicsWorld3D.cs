namespace helengine {
    /// <summary>
    /// Owns one deterministic fixed-capacity scalar box world and executes its simulation in a fixed twelve-phase order.
    /// </summary>
    public sealed class HelPhysicsWorld3D : IPhysicsRuntime, IPhysicsRuntimeProfilerMetricsProvider {
        /// <summary>
        /// Stores the process-local monotonic ownership allocator that permanently rejects token wraparound.
        /// </summary>
        static readonly HelPhysicsWorldIdAllocator3D WorldIdAllocator = new HelPhysicsWorldIdAllocator3D();

        /// <summary>
        /// Stores the conservative collision skin added to every world-space box bound.
        /// </summary>
        static readonly PhysicsScalar BroadphaseCollisionSkin = PhysicsScalar.FromFloat(0.005f);

        /// <summary>
        /// Stores the fraction of one-step linear travel used for speculative broadphase expansion.
        /// </summary>
        static readonly PhysicsScalar BroadphaseVelocityExpansionFactor = PhysicsScalar.FromFloat(0.1f);

        /// <summary>
        /// Stores all fixed hot and cold body slots, including reservations waiting for activation.
        /// </summary>
        readonly HelPhysicsBodyPool3D Bodies;

        /// <summary>
        /// Stores the separately generated box allocation owned by every reserved body.
        /// </summary>
        readonly HelPhysicsShapePool3D Shapes;

        /// <summary>
        /// Stores persistent sweep endpoints and fixed broadphase proxy metadata.
        /// </summary>
        readonly HelPhysicsSweepAndPrune3D Broadphase;

        /// <summary>
        /// Stores contact geometry, lifetimes, and warm-start impulses across fixed steps.
        /// </summary>
        readonly HelPhysicsManifoldCache3D ManifoldCache;

        /// <summary>
        /// Stores deterministic current and prior dynamic-island publications.
        /// </summary>
        readonly HelPhysicsIslandBuilder3D IslandBuilder;

        /// <summary>
        /// Stores aggressive whole-island sleep state and current-step wake diagnostics.
        /// </summary>
        readonly HelPhysicsIslandSleeper3D IslandSleeper;

        /// <summary>
        /// Integrates gravity, external force, torque, and damping into awake dynamic velocity.
        /// </summary>
        readonly HelPhysicsBodyIntegrator3D BodyIntegrator;

        /// <summary>
        /// Stores fixed contact constraints and executes warm starting, velocity solving, and penetration correction.
        /// </summary>
        readonly HelPhysicsContactSolver3D ContactSolver;

        /// <summary>
        /// Integrates solved awake dynamic velocities into world-space poses.
        /// </summary>
        readonly HelPhysicsPoseIntegrator3D PoseIntegrator;

        /// <summary>
        /// Stores reusable face-clipping buffers for box manifold construction.
        /// </summary>
        readonly HelPhysicsBoxCollisionScratch3D CollisionScratch;

        /// <summary>
        /// Stores commands accepted between steps in exact public insertion order.
        /// </summary>
        readonly HelPhysicsDeferredCommand3D[] DeferredCommands;

        /// <summary>
        /// Marks reservations that have passed phase-one activation and may participate in simulation.
        /// </summary>
        readonly bool[] BodyIsActive;

        /// <summary>
        /// Marks active body slots whose broadphase metadata or velocity-dependent bounds require publication before comparison can skip them.
        /// </summary>
        readonly bool[] ProxyIsDirty;

        /// <summary>
        /// Marks active body slots that currently own a published broadphase proxy and comparable pose snapshot.
        /// </summary>
        readonly bool[] ProxyIsRegistered;

        /// <summary>
        /// Stores each registered proxy's last exact body position for allocation-free moved-state detection.
        /// </summary>
        readonly PhysicsVector3[] ProxyPositions;

        /// <summary>
        /// Stores each registered proxy's last exact body orientation for allocation-free moved-state detection.
        /// </summary>
        readonly PhysicsQuaternion[] ProxyOrientations;

        /// <summary>
        /// Stores each registered proxy's last awake-or-moving activity flag for wake and sleep transition detection.
        /// </summary>
        readonly bool[] ProxyActivityStates;

        /// <summary>
        /// Stores each registered proxy's last scalar linear-velocity expansion so velocity-only bound changes cannot remain stale.
        /// </summary>
        readonly PhysicsScalar[] ProxyVelocityExpansions;

        /// <summary>
        /// Marks body identities that already own one accepted deferred removal command.
        /// </summary>
        readonly bool[] BodyRemovalQueued;

        /// <summary>
        /// Stores each pending reservation's authored body mode until its activation command executes.
        /// </summary>
        readonly BodyKind3D[] PendingBodyKinds;

        /// <summary>
        /// Stores each pending dynamic reservation's authored initial awake state until activation.
        /// </summary>
        readonly bool[] PendingInitialAwakeStates;

        /// <summary>
        /// Stores the final broadphase candidates for the step currently being assembled.
        /// </summary>
        readonly HelPhysicsCandidatePair3D[] CandidatePairs;

        /// <summary>
        /// Stores current narrow-phase pair keys in deterministic order and parallel to active manifolds.
        /// </summary>
        readonly HelPhysicsPairKey3D[] ActivePairs;

        /// <summary>
        /// Stores current narrow-phase manifolds in deterministic order and parallel to active pairs.
        /// </summary>
        readonly HelPhysicsContactManifold3D[] ActiveManifolds;

        /// <summary>
        /// Stores active and retained sleeping pair keys used only to preserve complete island connectivity.
        /// </summary>
        readonly HelPhysicsPairKey3D[] IslandPairs;

        /// <summary>
        /// Stores active and retained sleeping manifolds parallel to the island pair array.
        /// </summary>
        readonly HelPhysicsContactManifold3D[] IslandManifolds;

        /// <summary>
        /// Stores the single reusable outer-profiler sample returned without allocation.
        /// </summary>
        readonly HelPhysicsRuntimeProfilerMetrics3D RuntimeProfilerMetrics;

        /// <summary>
        /// Stores the nonzero ownership token embedded into every public body handle from this world.
        /// </summary>
        readonly uint WorldId;

        /// <summary>
        /// Stores how many leading deferred command slots await phase-one execution.
        /// </summary>
        int DeferredCommandCount;

        /// <summary>
        /// Stores the number of body reservations currently published into active simulation storage.
        /// </summary>
        int ActiveBodyCount;

        /// <summary>
        /// Stores how many leading current candidate slots were emitted by the final phase-three build.
        /// </summary>
        int CandidatePairCount;

        /// <summary>
        /// Stores how many leading active pair and manifold entries belong to current narrow phase.
        /// </summary>
        int ActiveManifoldCount;

        /// <summary>
        /// Stores the total number of inline contacts across current active manifolds.
        /// </summary>
        int ActiveContactPointCount;

        /// <summary>
        /// Stores how many leading pair and manifold entries define current island connectivity.
        /// </summary>
        int IslandManifoldCount;

        /// <summary>
        /// Stores the positive cache lifecycle identifier assigned to the current successful or attempted step.
        /// </summary>
        int StepId;

        /// <summary>
        /// Initializes every fixed pool, transient array, subsystem, ownership token, and reusable profiler sample.
        /// </summary>
        /// <param name="settings">Complete immutable allocation and solve profile for this world.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is <see langword="null"/>.</exception>
        public HelPhysicsWorld3D(HelPhysicsWorldSettings3D settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            Settings = settings;
            WorldId = WorldIdAllocator.Allocate();
            Bodies = new HelPhysicsBodyPool3D(settings.BodyCapacity);
            Shapes = new HelPhysicsShapePool3D(settings.ShapeCapacity);
            Broadphase = new HelPhysicsSweepAndPrune3D(settings.BodyCapacity, settings.CandidatePairCapacity);
            ManifoldCache = new HelPhysicsManifoldCache3D(settings.ManifoldCapacity);
            IslandBuilder = new HelPhysicsIslandBuilder3D(settings.BodyCapacity, settings.IslandCapacity);
            IslandSleeper = new HelPhysicsIslandSleeper3D(settings.BodyCapacity);
            BodyIntegrator = new HelPhysicsBodyIntegrator3D();
            ContactSolver = new HelPhysicsContactSolver3D(settings.ContactPointCapacity);
            PoseIntegrator = new HelPhysicsPoseIntegrator3D();
            CollisionScratch = new HelPhysicsBoxCollisionScratch3D();
            DeferredCommands = new HelPhysicsDeferredCommand3D[settings.DeferredCommandCapacity];
            BodyIsActive = new bool[settings.BodyCapacity];
            ProxyIsDirty = new bool[settings.BodyCapacity];
            ProxyIsRegistered = new bool[settings.BodyCapacity];
            ProxyPositions = new PhysicsVector3[settings.BodyCapacity];
            ProxyOrientations = new PhysicsQuaternion[settings.BodyCapacity];
            ProxyActivityStates = new bool[settings.BodyCapacity];
            ProxyVelocityExpansions = new PhysicsScalar[settings.BodyCapacity];
            BodyRemovalQueued = new bool[settings.BodyCapacity];
            PendingBodyKinds = new BodyKind3D[settings.BodyCapacity];
            PendingInitialAwakeStates = new bool[settings.BodyCapacity];
            CandidatePairs = new HelPhysicsCandidatePair3D[settings.CandidatePairCapacity];
            ActivePairs = new HelPhysicsPairKey3D[settings.ManifoldCapacity];
            ActiveManifolds = new HelPhysicsContactManifold3D[settings.ManifoldCapacity];
            IslandPairs = new HelPhysicsPairKey3D[settings.ManifoldCapacity];
            IslandManifolds = new HelPhysicsContactManifold3D[settings.ManifoldCapacity];
            RuntimeProfilerMetrics = new HelPhysicsRuntimeProfilerMetrics3D();
            LastStepMetrics = default;
        }

        /// <summary>
        /// Gets the validated fixed allocation and solve profile owned by this world.
        /// </summary>
        public HelPhysicsWorldSettings3D Settings { get; }

        /// <summary>
        /// Gets an immutable copy of every counter published by the most recently completed fixed step.
        /// </summary>
        public HelPhysicsStepMetrics3D LastStepMetrics { get; private set; }

        /// <summary>
        /// Gets whether an exception after fixed-step mutation permanently disabled further simulation work for this world.
        /// </summary>
        public bool IsFaulted { get; private set; }

        /// <summary>
        /// Gets the number of persistent manifolds currently retained for active or sleeping contacts.
        /// </summary>
        internal int CachedManifoldCount => ManifoldCache.Count;

        /// <summary>
        /// Gets the exact number of broadphase proxy publications performed before narrow phase in the latest attempted step.
        /// </summary>
        internal int PhaseTwoProxyUpdateCount { get; private set; }

        /// <summary>
        /// Gets the exact number of broadphase proxy publications performed after pose correction in the latest attempted step.
        /// </summary>
        internal int PhaseElevenProxyUpdateCount { get; private set; }

        /// <summary>
        /// Reserves one shape and body immediately, returning a stable pending handle whose activation executes first at the next valid step.
        /// </summary>
        /// <param name="description">Complete explicit box body description to reserve.</param>
        /// <returns>A generation-safe world-owned handle whose snapshot is pending until phase one.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="description"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the world is permanently faulted.</exception>
        /// <exception cref="HelPhysicsCapacityExceededException">Thrown before reservation when command, body, or shape storage is full.</exception>
        public HelPhysicsBodyHandle3D CreateBody(HelPhysicsBodyDescription3D description) {
            ThrowIfFaulted();
            if (description == null) {
                throw new ArgumentNullException(nameof(description));
            }

            EnsureDeferredCommandCapacity();
            if (Bodies.ActiveCount == Bodies.Capacity) {
                throw new HelPhysicsCapacityExceededException("body", Bodies.Capacity);
            }

            if (Shapes.ActiveCount == Shapes.Capacity) {
                throw new HelPhysicsCapacityExceededException("shape", Shapes.Capacity);
            }

            HelPhysicsShapeHandle3D shapeHandle = Shapes.Allocate(description.Shape);
            HelPhysicsBodyState3D state = new HelPhysicsBodyState3D {
                Position = description.Position,
                Orientation = description.Orientation,
                LinearVelocity = description.LinearVelocity,
                AngularVelocity = description.AngularVelocity,
                AccumulatedForce = PhysicsVector3.Zero,
                AccumulatedTorque = PhysicsVector3.Zero,
                InverseMass = description.InverseMass,
                LocalInverseInertia = description.LocalInverseInertia,
                GravityScale = description.GravityScale,
                LinearDamping = description.LinearDamping,
                AngularDamping = description.AngularDamping,
                LowMotionStepCount = 0,
                IsAwake = false
            };
            HelPhysicsBodyColdState3D coldState = new HelPhysicsBodyColdState3D(
                shapeHandle,
                BodyKind3D.Static,
                description.Material,
                description.CollisionLayer,
                description.CollisionMask,
                description.EntityBindingId,
                description.LinearSleepThresholdSquared,
                description.AngularSleepThresholdSquared,
                description.SleepTicks);
            HelPhysicsBodyHandle3D internalHandle = Bodies.Allocate(state, coldState);
            PendingBodyKinds[internalHandle.Index] = description.BodyKind;
            PendingInitialAwakeStates[internalHandle.Index] = description.IsAwake;
            AppendDeferredCommand(new HelPhysicsDeferredCommand3D(
                HelPhysicsDeferredCommandKind3D.ActivateBody,
                internalHandle,
                PhysicsVector3.Zero));

            return CreatePublicHandle(internalHandle);
        }

        /// <summary>
        /// Defers generation-safe removal until the next valid fixed step while leaving the current snapshot active or pending beforehand.
        /// </summary>
        /// <param name="handle">Current world-owned body identity to remove.</param>
        /// <exception cref="InvalidOperationException">Thrown for foreign, stale, released, duplicate-removal, or generation-exhausted handles.</exception>
        /// <exception cref="HelPhysicsCapacityExceededException">Thrown when the command buffer has no free slot.</exception>
        public void RemoveBody(HelPhysicsBodyHandle3D handle) {
            ThrowIfFaulted();
            HelPhysicsBodyHandle3D internalHandle = GetRequiredInternalHandle(handle);
            if (BodyRemovalQueued[internalHandle.Index]) {
                throw new InvalidOperationException("The body already has a deferred removal command.");
            }

            HelPhysicsShapeHandle3D shapeHandle = Bodies.GetRequiredColdState(internalHandle).ShapeHandle;
            if (internalHandle.Generation == ushort.MaxValue || shapeHandle.Generation == ushort.MaxValue) {
                throw new InvalidOperationException("The body or shape handle generation is exhausted and cannot be released safely.");
            }

            EnsureDeferredCommandCapacity();
            AppendDeferredCommand(new HelPhysicsDeferredCommand3D(
                HelPhysicsDeferredCommandKind3D.RemoveBody,
                internalHandle,
                PhysicsVector3.Zero));
            BodyRemovalQueued[internalHandle.Index] = true;
        }

        /// <summary>
        /// Defers one world-space force for deterministic next-step wake propagation and same-step velocity integration.
        /// </summary>
        /// <param name="handle">Current world-owned dynamic body identity receiving the force.</param>
        /// <param name="force">Finite world-space force accumulated during phase one.</param>
        /// <exception cref="InvalidOperationException">Thrown when the handle is invalid, non-dynamic, or already queued for removal.</exception>
        /// <exception cref="HelPhysicsCapacityExceededException">Thrown when the command buffer has no free slot.</exception>
        public void ApplyForce(HelPhysicsBodyHandle3D handle, PhysicsVector3 force) {
            ThrowIfFaulted();
            HelPhysicsBodyHandle3D internalHandle = GetRequiredDynamicInputHandle(handle);
            EnsureDeferredCommandCapacity();
            ValidateDeferredLinearInput(
                internalHandle,
                HelPhysicsDeferredCommandKind3D.ApplyForce,
                force);
            AppendDeferredCommand(new HelPhysicsDeferredCommand3D(
                HelPhysicsDeferredCommandKind3D.ApplyForce,
                internalHandle,
                force));
        }

        /// <summary>
        /// Defers one world-space linear impulse for deterministic next-step wake propagation and immediate phase-one velocity change.
        /// </summary>
        /// <param name="handle">Current world-owned dynamic body identity receiving the impulse.</param>
        /// <param name="impulse">Finite world-space linear impulse applied during phase one.</param>
        /// <exception cref="InvalidOperationException">Thrown when the handle is invalid, non-dynamic, or already queued for removal.</exception>
        /// <exception cref="HelPhysicsCapacityExceededException">Thrown when the command buffer has no free slot.</exception>
        public void ApplyImpulse(HelPhysicsBodyHandle3D handle, PhysicsVector3 impulse) {
            ThrowIfFaulted();
            HelPhysicsBodyHandle3D internalHandle = GetRequiredDynamicInputHandle(handle);
            EnsureDeferredCommandCapacity();
            ValidateDeferredLinearInput(
                internalHandle,
                HelPhysicsDeferredCommandKind3D.ApplyImpulse,
                impulse);
            AppendDeferredCommand(new HelPhysicsDeferredCommand3D(
                HelPhysicsDeferredCommandKind3D.ApplyImpulse,
                internalHandle,
                impulse));
        }

        /// <summary>
        /// Defers one atomic replacement of a kinematic body's world pose and authored velocity until the next fixed-step boundary.
        /// </summary>
        /// <param name="handle">Current world-owned kinematic body identity.</param>
        /// <param name="position">Finite world-space center-of-mass position.</param>
        /// <param name="orientation">Finite normalized world-space orientation.</param>
        /// <param name="linearVelocity">Finite world-space linear velocity.</param>
        /// <param name="angularVelocity">Finite world-space angular velocity.</param>
        /// <exception cref="InvalidOperationException">Thrown when the handle is foreign, stale, removed, or does not identify a kinematic body.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the orientation is not normalized or aggregate velocity arithmetic is not finite.</exception>
        /// <exception cref="HelPhysicsCapacityExceededException">Thrown before mutation when the deferred command buffer is full.</exception>
        public void SetKinematicState(
            HelPhysicsBodyHandle3D handle,
            PhysicsVector3 position,
            PhysicsQuaternion orientation,
            PhysicsVector3 linearVelocity,
            PhysicsVector3 angularVelocity) {
            ThrowIfFaulted();
            HelPhysicsBodyHandle3D internalHandle = GetRequiredKinematicInputHandle(handle);
            ValidateNormalizedOrientation(orientation);
            linearVelocity.LengthSquared();
            angularVelocity.LengthSquared();
            EnsureDeferredCommandCapacity();
            AppendDeferredCommand(new HelPhysicsDeferredCommand3D(
                internalHandle,
                position,
                orientation,
                linearVelocity,
                angularVelocity));
        }

        /// <summary>
        /// Copies the complete observable simulation and pending/active lifecycle state for one current world-owned handle.
        /// </summary>
        /// <param name="handle">Current world-owned body identity to inspect.</param>
        /// <returns>An immutable value copy that cannot mutate world storage.</returns>
        /// <exception cref="InvalidOperationException">Thrown when ownership, generation, occupancy, or index is invalid.</exception>
        public HelPhysicsBodySnapshot3D GetBodySnapshot(HelPhysicsBodyHandle3D handle) {
            HelPhysicsBodyHandle3D internalHandle = GetRequiredInternalHandle(handle);
            ref HelPhysicsBodyState3D state = ref Bodies.GetRequiredState(internalHandle);
            bool isActive = BodyIsActive[internalHandle.Index];
            BodyKind3D bodyKind;
            if (isActive) {
                bodyKind = Bodies.GetRequiredColdState(internalHandle).BodyKind;
            } else {
                bodyKind = PendingBodyKinds[internalHandle.Index];
            }

            return new HelPhysicsBodySnapshot3D(
                bodyKind,
                state.Position,
                state.Orientation,
                state.LinearVelocity,
                state.AngularVelocity,
                state.LowMotionStepCount,
                state.IsAwake,
                isActive);
        }

        /// <summary>
        /// Advances exactly one configured fixed step through command, broadphase, collision, island, integration, solve, pose, sleep, and publication phases.
        /// </summary>
        /// <param name="stepSeconds">Public double duration that must exactly equal <see cref="HelPhysicsWorldSettings3D.FixedStepSeconds"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown before mutation when the duration is non-positive, non-finite, or not the configured fixed step.</exception>
        /// <exception cref="InvalidOperationException">Thrown before mutation when a prior post-mutation failure permanently faulted the world.</exception>
        public void Step(double stepSeconds) {
            ThrowIfFaulted();
            ValidateStepSeconds(stepSeconds);
            if (StepId == int.MaxValue) {
                throw new InvalidOperationException("The manifold lifecycle step identifier is exhausted.");
            }

            PhysicsScalar scalarStepSeconds = PhysicsScalar.FromFloat((float)stepSeconds);
            try {
                StepId++;
                PhaseTwoProxyUpdateCount = 0;
                PhaseElevenProxyUpdateCount = 0;
                IslandSleeper.BeginStep();
                ApplyDeferredCommands();
                PhaseTwoProxyUpdateCount = UpdateBroadphaseProxies(scalarStepSeconds);
                BuildCandidatesAndRouteNewContactWakes(scalarStepSeconds);
                BuildActiveManifolds();
                BuildIslandsAndRouteKinematicWakes();
                PhysicsVector3 gravity = Settings.Gravity;
                BodyIntegrator.IntegrateVelocity(scalarStepSeconds, in gravity, Bodies);
                PrepareWarmStartAndSolve(scalarStepSeconds);
                CorrectPenetration();
                PoseIntegrator.IntegratePose(scalarStepSeconds, Bodies);
                PhaseElevenProxyUpdateCount = UpdateBroadphaseProxies(scalarStepSeconds);
                IslandSleeper.EvaluateSleep(Bodies, IslandBuilder);
                RetainSleepingContactsAndPublishMetrics();
            } catch {
                IsFaulted = true;
                throw;
            }
        }

        /// <summary>
        /// Returns the single reusable profiler sample most recently synchronized from completed step metrics.
        /// </summary>
        /// <param name="metrics">Receives a world-owned object whose values update after later completed steps.</param>
        /// <returns><see langword="true"/> because this world always owns body, contact, and manifold totals.</returns>
        public bool TryGetRuntimeProfilerMetrics(out RuntimePhysicsProfilerMetrics metrics) {
            metrics = RuntimeProfilerMetrics;
            return true;
        }

        /// <summary>
        /// Retrieves one retained manifold after validating both public body identities and world ownership.
        /// </summary>
        /// <param name="firstHandle">Current first body identity.</param>
        /// <param name="secondHandle">Current distinct second body identity.</param>
        /// <param name="manifold">Receives the retained manifold when present.</param>
        /// <returns><see langword="true"/> when the current body pair owns a retained cache entry.</returns>
        internal bool TryGetCachedManifold(
            HelPhysicsBodyHandle3D firstHandle,
            HelPhysicsBodyHandle3D secondHandle,
            out HelPhysicsContactManifold3D manifold) {
            HelPhysicsBodyHandle3D firstInternalHandle = GetRequiredInternalHandle(firstHandle);
            HelPhysicsBodyHandle3D secondInternalHandle = GetRequiredInternalHandle(secondHandle);
            HelPhysicsPairKey3D pair = new HelPhysicsPairKey3D(
                firstInternalHandle.Index,
                secondInternalHandle.Index);
            return ManifoldCache.TryGet(pair, out manifold);
        }

        /// <summary>
        /// Rejects simulation mutations after an earlier step failed beyond its no-mutation validation boundary.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when this world can no longer guarantee coherent mutable simulation state.</exception>
        void ThrowIfFaulted() {
            if (IsFaulted) {
                throw new InvalidOperationException("The HelPhysics world is faulted and cannot accept further simulation work.");
            }
        }

        /// <summary>
        /// Validates the public duration completely before step identifiers, wake state, commands, or simulation storage can mutate.
        /// </summary>
        /// <param name="stepSeconds">Public duration to validate.</param>
        static void ValidateFinitePositiveStep(double stepSeconds) {
            if (double.IsNaN(stepSeconds) || double.IsInfinity(stepSeconds) || stepSeconds <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), "Physics steps must be positive and finite.");
            }
        }

        /// <summary>
        /// Validates both the public scalar domain and exact configured fixed-step equality.
        /// </summary>
        /// <param name="stepSeconds">Public duration supplied to this world.</param>
        void ValidateStepSeconds(double stepSeconds) {
            ValidateFinitePositiveStep(stepSeconds);
            if (stepSeconds != Settings.FixedStepSeconds) {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), "Physics steps must exactly equal the configured fixed step.");
            }
        }

        /// <summary>
        /// Creates a public identity by attaching this world's ownership token to one pool-internal handle.
        /// </summary>
        /// <param name="internalHandle">Current body-pool identity.</param>
        /// <returns>The corresponding public world-owned identity.</returns>
        HelPhysicsBodyHandle3D CreatePublicHandle(HelPhysicsBodyHandle3D internalHandle) {
            return new HelPhysicsBodyHandle3D(internalHandle.Index, internalHandle.Generation, WorldId);
        }

        /// <summary>
        /// Validates ownership and delegates index, generation, and occupancy validation to the fixed body pool.
        /// </summary>
        /// <param name="handle">Public identity to validate.</param>
        /// <returns>The corresponding pool-internal identity after complete validation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when any ownership or pool identity component is invalid.</exception>
        HelPhysicsBodyHandle3D GetRequiredInternalHandle(HelPhysicsBodyHandle3D handle) {
            if (handle.WorldId != WorldId) {
                throw new InvalidOperationException("The body handle belongs to a different HelPhysics world.");
            }

            HelPhysicsBodyHandle3D internalHandle = new HelPhysicsBodyHandle3D(handle.Index, handle.Generation);
            Bodies.GetRequiredState(internalHandle);
            return internalHandle;
        }

        /// <summary>
        /// Validates one public force or impulse target including pending authored mode and queued-removal state.
        /// </summary>
        /// <param name="handle">Public target identity.</param>
        /// <returns>The current pool-internal dynamic identity.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the target is non-dynamic or already waiting for removal.</exception>
        HelPhysicsBodyHandle3D GetRequiredDynamicInputHandle(HelPhysicsBodyHandle3D handle) {
            HelPhysicsBodyHandle3D internalHandle = GetRequiredInternalHandle(handle);
            if (BodyRemovalQueued[internalHandle.Index]) {
                throw new InvalidOperationException("A body waiting for removal cannot accept another deferred input.");
            }

            BodyKind3D bodyKind;
            if (BodyIsActive[internalHandle.Index]) {
                bodyKind = Bodies.GetRequiredColdState(internalHandle).BodyKind;
            } else {
                bodyKind = PendingBodyKinds[internalHandle.Index];
            }

            if (bodyKind != BodyKind3D.Dynamic) {
                throw new InvalidOperationException("Explicit force and impulse targets must be dynamic bodies.");
            }

            return internalHandle;
        }

        /// <summary>
        /// Validates one public kinematic state target including pending authored mode and queued-removal state.
        /// </summary>
        /// <param name="handle">Public target identity.</param>
        /// <returns>The current pool-internal kinematic identity.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the target is non-kinematic or already waiting for removal.</exception>
        HelPhysicsBodyHandle3D GetRequiredKinematicInputHandle(HelPhysicsBodyHandle3D handle) {
            HelPhysicsBodyHandle3D internalHandle = GetRequiredInternalHandle(handle);
            if (BodyRemovalQueued[internalHandle.Index]) {
                throw new InvalidOperationException("A body waiting for removal cannot accept another deferred input.");
            }

            BodyKind3D bodyKind;
            if (BodyIsActive[internalHandle.Index]) {
                bodyKind = Bodies.GetRequiredColdState(internalHandle).BodyKind;
            } else {
                bodyKind = PendingBodyKinds[internalHandle.Index];
            }

            if (bodyKind != BodyKind3D.Kinematic) {
                throw new InvalidOperationException("Kinematic state targets must be kinematic bodies.");
            }

            return internalHandle;
        }

        /// <summary>
        /// Validates that one authored world orientation is unit length before a command can enter the deferred buffer.
        /// </summary>
        /// <param name="orientation">Quaternion to validate without normalizing silently.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the quaternion differs materially from unit length.</exception>
        static void ValidateNormalizedOrientation(PhysicsQuaternion orientation) {
            double lengthSquared =
                ((double)orientation.X.ToFloat() * orientation.X.ToFloat()) +
                ((double)orientation.Y.ToFloat() * orientation.Y.ToFloat()) +
                ((double)orientation.Z.ToFloat() * orientation.Z.ToFloat()) +
                ((double)orientation.W.ToFloat() * orientation.W.ToFloat());
            if (Math.Abs(lengthSquared - 1d) > 0.0001d) {
                throw new ArgumentOutOfRangeException(nameof(orientation), "Kinematic body orientations must be normalized before mutation.");
            }
        }

        /// <summary>
        /// Dry-runs every accepted and prospective linear input for one body through phase-one impulse, phase-six force, damping, and pose arithmetic.
        /// </summary>
        /// <param name="handle">Pool-internal dynamic body identity targeted by the prospective command.</param>
        /// <param name="prospectiveKind">Force or impulse command kind being validated before append.</param>
        /// <param name="prospectiveVector">World-space input carried by the prospective command.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown before queue mutation when aggregate scalar arithmetic is not finite.</exception>
        /// <exception cref="ArgumentException">Thrown when the prospective command kind is not a linear input.</exception>
        void ValidateDeferredLinearInput(
            HelPhysicsBodyHandle3D handle,
            HelPhysicsDeferredCommandKind3D prospectiveKind,
            PhysicsVector3 prospectiveVector) {
            ref HelPhysicsBodyState3D state = ref Bodies.GetRequiredState(handle);
            PhysicsVector3 predictedForce = state.AccumulatedForce;
            PhysicsVector3 predictedVelocity = state.LinearVelocity;
            for (int commandIndex = 0; commandIndex < DeferredCommandCount; commandIndex++) {
                HelPhysicsDeferredCommand3D command = DeferredCommands[commandIndex];
                if (command.BodyHandle.Index != handle.Index ||
                    command.BodyHandle.Generation != handle.Generation) {
                    continue;
                }

                if (command.Kind == HelPhysicsDeferredCommandKind3D.ApplyForce) {
                    predictedForce += command.Vector;
                } else if (command.Kind == HelPhysicsDeferredCommandKind3D.ApplyImpulse) {
                    predictedVelocity += command.Vector * state.InverseMass;
                }
            }

            if (prospectiveKind == HelPhysicsDeferredCommandKind3D.ApplyForce) {
                predictedForce += prospectiveVector;
            } else if (prospectiveKind == HelPhysicsDeferredCommandKind3D.ApplyImpulse) {
                predictedVelocity += prospectiveVector * state.InverseMass;
            } else {
                throw new ArgumentException("Deferred linear input validation requires a force or impulse command.", nameof(prospectiveKind));
            }

            PhysicsScalar stepSeconds = PhysicsScalar.FromFloat((float)Settings.FixedStepSeconds);
            PhysicsVector3 linearAcceleration =
                (Settings.Gravity * state.GravityScale) +
                (predictedForce * state.InverseMass);
            predictedVelocity += linearAcceleration * stepSeconds;
            PhysicsScalar dampingScale =
                PhysicsScalar.One / (PhysicsScalar.One + (state.LinearDamping * stepSeconds));
            predictedVelocity *= dampingScale;
            predictedVelocity.LengthSquared();
            _ = state.Position + (predictedVelocity * stepSeconds);
        }

        /// <summary>
        /// Throws the exact deferred-command diagnostic before any public operation mutates reservation or queue state.
        /// </summary>
        void EnsureDeferredCommandCapacity() {
            if (DeferredCommandCount == DeferredCommands.Length) {
                throw new HelPhysicsCapacityExceededException("deferred command", DeferredCommands.Length);
            }
        }

        /// <summary>
        /// Appends one already validated command to the next deterministic insertion-order slot.
        /// </summary>
        /// <param name="command">Complete command value to append.</param>
        void AppendDeferredCommand(HelPhysicsDeferredCommand3D command) {
            DeferredCommands[DeferredCommandCount++] = command;
        }

        /// <summary>
        /// Executes all accepted mutations in insertion order and clears their fixed slots only after each successful application.
        /// </summary>
        void ApplyDeferredCommands() {
            for (int commandIndex = 0; commandIndex < DeferredCommandCount; commandIndex++) {
                HelPhysicsDeferredCommand3D command = DeferredCommands[commandIndex];
                if (command.Kind == HelPhysicsDeferredCommandKind3D.ActivateBody) {
                    ActivateBody(command.BodyHandle);
                } else if (command.Kind == HelPhysicsDeferredCommandKind3D.RemoveBody) {
                    RemoveBodyImmediately(command.BodyHandle);
                } else if (command.Kind == HelPhysicsDeferredCommandKind3D.ApplyForce) {
                    ApplyForceImmediately(command.BodyHandle, command.Vector);
                } else if (command.Kind == HelPhysicsDeferredCommandKind3D.ApplyImpulse) {
                    ApplyImpulseImmediately(command.BodyHandle, command.Vector);
                } else if (command.Kind == HelPhysicsDeferredCommandKind3D.SetKinematicState) {
                    SetKinematicStateImmediately(
                        command.BodyHandle,
                        command.Position,
                        command.Orientation,
                        command.LinearVelocity,
                        command.AngularVelocity);
                } else {
                    throw new InvalidOperationException("The deferred command buffer contains an unsupported mutation kind.");
                }

                DeferredCommands[commandIndex] = default;
            }

            DeferredCommandCount = 0;
        }

        /// <summary>
        /// Publishes one pending reservation by restoring its authored body mode and initial dynamic awake state.
        /// </summary>
        /// <param name="handle">Pool-internal pending reservation identity.</param>
        void ActivateBody(HelPhysicsBodyHandle3D handle) {
            Bodies.GetRequiredState(handle);
            if (BodyIsActive[handle.Index]) {
                throw new InvalidOperationException("A deferred activation command cannot publish an already active body.");
            }

            ref HelPhysicsBodyColdState3D coldState = ref Bodies.GetRequiredColdState(handle);
            ref HelPhysicsBodyState3D state = ref Bodies.GetRequiredState(handle);
            coldState.BodyKind = PendingBodyKinds[handle.Index];
            state.IsAwake = PendingInitialAwakeStates[handle.Index];
            BodyIsActive[handle.Index] = true;
            ProxyIsDirty[handle.Index] = true;
            ActiveBodyCount++;
        }

        /// <summary>
        /// Removes one current reservation from broadphase and cache before releasing its shape and body generations.
        /// </summary>
        /// <param name="handle">Pool-internal identity accepted by the public removal API.</param>
        void RemoveBodyImmediately(HelPhysicsBodyHandle3D handle) {
            ref HelPhysicsBodyColdState3D coldState = ref Bodies.GetRequiredColdState(handle);
            HelPhysicsShapeHandle3D shapeHandle = coldState.ShapeHandle;
            if (BodyIsActive[handle.Index]) {
                Broadphase.RemoveProxy(handle.Index);
                ActiveBodyCount--;
            }

            ManifoldCache.RemoveBody(handle.Index);
            BodyIsActive[handle.Index] = false;
            ProxyIsDirty[handle.Index] = false;
            ProxyIsRegistered[handle.Index] = false;
            ProxyPositions[handle.Index] = default;
            ProxyOrientations[handle.Index] = default;
            ProxyActivityStates[handle.Index] = false;
            ProxyVelocityExpansions[handle.Index] = default;
            BodyRemovalQueued[handle.Index] = false;
            PendingBodyKinds[handle.Index] = default;
            PendingInitialAwakeStates[handle.Index] = false;
            Shapes.Release(shapeHandle);
            Bodies.Release(handle);
        }

        /// <summary>
        /// Routes an explicit-force wake through prior generation-safe islands before accumulating force on the current body.
        /// </summary>
        /// <param name="handle">Pool-internal dynamic identity.</param>
        /// <param name="force">World-space force to accumulate.</param>
        void ApplyForceImmediately(HelPhysicsBodyHandle3D handle, PhysicsVector3 force) {
            Bodies.GetRequiredState(handle);
            IslandSleeper.WakeForExplicitForce(handle.Index, Bodies, IslandBuilder);
            Bodies.GetRequiredState(handle).AccumulatedForce += force;
        }

        /// <summary>
        /// Routes an explicit-impulse wake through prior generation-safe islands before changing current linear velocity.
        /// </summary>
        /// <param name="handle">Pool-internal dynamic identity.</param>
        /// <param name="impulse">World-space linear impulse to apply.</param>
        void ApplyImpulseImmediately(HelPhysicsBodyHandle3D handle, PhysicsVector3 impulse) {
            ref HelPhysicsBodyState3D state = ref Bodies.GetRequiredState(handle);
            IslandSleeper.WakeForExplicitImpulse(handle.Index, Bodies, IslandBuilder);
            state.LinearVelocity += impulse * state.InverseMass;
            ProxyIsDirty[handle.Index] = true;
        }

        /// <summary>
        /// Atomically publishes one already validated kinematic pose and velocity replacement to hot body state.
        /// </summary>
        /// <param name="handle">Pool-internal kinematic identity validated when the command was accepted.</param>
        /// <param name="position">World-space center-of-mass position.</param>
        /// <param name="orientation">Normalized world-space orientation.</param>
        /// <param name="linearVelocity">World-space linear velocity.</param>
        /// <param name="angularVelocity">World-space angular velocity.</param>
        void SetKinematicStateImmediately(
            HelPhysicsBodyHandle3D handle,
            PhysicsVector3 position,
            PhysicsQuaternion orientation,
            PhysicsVector3 linearVelocity,
            PhysicsVector3 angularVelocity) {
            ref HelPhysicsBodyState3D state = ref Bodies.GetRequiredState(handle);
            state.Position = position;
            state.Orientation = orientation;
            state.LinearVelocity = linearVelocity;
            state.AngularVelocity = angularVelocity;
            ProxyIsDirty[handle.Index] = true;
        }

        /// <summary>
        /// Publishes only dirty, activity-changed, or pose-changed proxies while preserving persistent endpoint storage.
        /// </summary>
        /// <param name="stepSeconds">Current fixed scalar step used for velocity-dependent expansion.</param>
        /// <returns>The exact number of calls made to fixed broadphase proxy publication.</returns>
        int UpdateBroadphaseProxies(PhysicsScalar stepSeconds) {
            int updateCount = 0;
            for (int bodyIndex = 0; bodyIndex < Bodies.Capacity; bodyIndex++) {
                if (!Bodies.IsOccupied(bodyIndex) || !BodyIsActive[bodyIndex]) {
                    continue;
                }

                ref HelPhysicsBodyState3D state = ref Bodies.GetRequiredStateByIndex(bodyIndex);
                ref HelPhysicsBodyColdState3D coldState = ref Bodies.GetRequiredColdStateByIndex(bodyIndex);
                bool isActive = IsBroadphaseProxyActive(in state, coldState.BodyKind);
                PhysicsScalar velocityExpansion =
                    state.LinearVelocity.Length() * stepSeconds * BroadphaseVelocityExpansionFactor;
                if (ProxyIsRegistered[bodyIndex] &&
                    !ProxyIsDirty[bodyIndex] &&
                    ProxyActivityStates[bodyIndex] == isActive &&
                    ProxyVelocityExpansions[bodyIndex] == velocityExpansion &&
                    !HasProxyPoseChanged(bodyIndex, in state)) {
                    continue;
                }

                ref HelPhysicsBoxShape3D shape = ref Shapes.GetRequiredBox(coldState.ShapeHandle);
                PhysicsScalar margin = BroadphaseCollisionSkin + velocityExpansion;
                HelPhysicsAabb3D aabb = HelPhysicsBoxGeometry3D.ComputeWorldAabb(
                    shape,
                    state.Position,
                    state.Orientation,
                    margin);
                Broadphase.UpdateProxy(
                    bodyIndex,
                    coldState.BodyKind,
                    isActive,
                    coldState.CollisionLayer,
                    coldState.CollisionMask,
                    aabb);
                ProxyIsDirty[bodyIndex] = false;
                ProxyIsRegistered[bodyIndex] = true;
                ProxyPositions[bodyIndex] = state.Position;
                ProxyOrientations[bodyIndex] = state.Orientation;
                ProxyActivityStates[bodyIndex] = isActive;
                ProxyVelocityExpansions[bodyIndex] = velocityExpansion;
                updateCount++;
            }

            return updateCount;
        }

        /// <summary>
        /// Computes whether one active body should participate as a moving broadphase endpoint owner.
        /// </summary>
        /// <param name="state">Current hot state supplying awake and velocity values.</param>
        /// <param name="bodyKind">Current simulation mode interpreting activity.</param>
        /// <returns><see langword="true"/> for awake dynamics or moving kinematics; otherwise <see langword="false"/>.</returns>
        static bool IsBroadphaseProxyActive(in HelPhysicsBodyState3D state, BodyKind3D bodyKind) {
            if (bodyKind == BodyKind3D.Dynamic) {
                return state.IsAwake;
            } else if (bodyKind == BodyKind3D.Kinematic) {
                return state.LinearVelocity.LengthSquared() != PhysicsScalar.Zero ||
                    state.AngularVelocity.LengthSquared() != PhysicsScalar.Zero;
            }

            return false;
        }

        /// <summary>
        /// Compares one body's current pose against the exact pose stored by its most recent proxy publication.
        /// </summary>
        /// <param name="bodyIndex">Registered fixed body slot whose snapshot is inspected.</param>
        /// <param name="state">Current body state supplying position and orientation.</param>
        /// <returns><see langword="true"/> when any scalar pose component changed.</returns>
        bool HasProxyPoseChanged(int bodyIndex, in HelPhysicsBodyState3D state) {
            PhysicsVector3 position = ProxyPositions[bodyIndex];
            PhysicsQuaternion orientation = ProxyOrientations[bodyIndex];
            return position.X != state.Position.X ||
                position.Y != state.Position.Y ||
                position.Z != state.Position.Z ||
                orientation.X != state.Orientation.X ||
                orientation.Y != state.Orientation.Y ||
                orientation.Z != state.Orientation.Z ||
                orientation.W != state.Orientation.W;
        }

        /// <summary>
        /// Rebuilds candidates after actual sleeping-island transitions and routes only meaningful generation-safe new contacts before narrow phase.
        /// </summary>
        /// <param name="stepSeconds">Current fixed scalar step used when a wake requires proxy activity refresh.</param>
        void BuildCandidatesAndRouteNewContactWakes(PhysicsScalar stepSeconds) {
            int rebuildCount = 0;
            while (true) {
                CandidatePairCount = Broadphase.BuildCandidatePairs(CandidatePairs);
                bool wokeSleepingParticipant = false;
                for (int candidateIndex = 0; candidateIndex < CandidatePairCount; candidateIndex++) {
                    HelPhysicsCandidatePair3D candidate = CandidatePairs[candidateIndex];
                    if (!IsMeaningfulNewCandidate(candidate) || IsMovingKinematicCandidate(candidate)) {
                        continue;
                    }

                    bool containedSleepingDynamic = ContainsSleepingDynamic(candidate);
                    IslandSleeper.WakeForNewCandidateContact(candidate, Bodies, IslandBuilder);
                    wokeSleepingParticipant = wokeSleepingParticipant || containedSleepingDynamic;
                }

                if (!wokeSleepingParticipant) {
                    return;
                }

                rebuildCount++;
                if (rebuildCount > Bodies.Capacity) {
                    throw new InvalidOperationException("Candidate wake rebuilding did not converge within fixed body capacity.");
                }

                PhaseTwoProxyUpdateCount += UpdateBroadphaseProxies(stepSeconds);
            }
        }

        /// <summary>
        /// Determines whether a candidate lacks a retained manifold and therefore remains speculative regardless of prior broadphase publication.
        /// </summary>
        /// <param name="candidate">Current canonical broadphase candidate.</param>
        /// <returns><see langword="true"/> when the candidate represents newly published contact potential.</returns>
        bool IsMeaningfulNewCandidate(HelPhysicsCandidatePair3D candidate) {
            HelPhysicsPairKey3D pair = new HelPhysicsPairKey3D(
                candidate.FirstBodyIndex,
                candidate.SecondBodyIndex);
            return !ManifoldCache.TryGet(pair, out _);
        }

        /// <summary>
        /// Determines whether either candidate participant is a dynamic body currently outside the awake set.
        /// </summary>
        /// <param name="candidate">Current canonical broadphase candidate.</param>
        /// <returns><see langword="true"/> when at least one dynamic participant is asleep before wake routing.</returns>
        bool ContainsSleepingDynamic(HelPhysicsCandidatePair3D candidate) {
            return IsSleepingDynamic(candidate.FirstBodyIndex) || IsSleepingDynamic(candidate.SecondBodyIndex);
        }

        /// <summary>
        /// Determines whether one occupied active slot contains a sleeping dynamic body.
        /// </summary>
        /// <param name="bodyIndex">Fixed body slot to inspect.</param>
        /// <returns><see langword="true"/> only for an active dynamic whose awake flag is false.</returns>
        bool IsSleepingDynamic(int bodyIndex) {
            return BodyIsActive[bodyIndex] &&
                Bodies.GetRequiredColdStateByIndex(bodyIndex).BodyKind == BodyKind3D.Dynamic &&
                !Bodies.GetRequiredStateByIndex(bodyIndex).IsAwake;
        }

        /// <summary>
        /// Detects a dynamic-kinematic candidate whose kinematic participant has nonzero authored motion.
        /// </summary>
        /// <param name="candidate">Current canonical broadphase candidate.</param>
        /// <returns><see langword="true"/> when dedicated post-manifold wake routing must replace generic candidate routing.</returns>
        bool IsMovingKinematicCandidate(HelPhysicsCandidatePair3D candidate) {
            BodyKind3D firstKind = Bodies.GetRequiredColdStateByIndex(candidate.FirstBodyIndex).BodyKind;
            BodyKind3D secondKind = Bodies.GetRequiredColdStateByIndex(candidate.SecondBodyIndex).BodyKind;
            int kinematicBodyIndex;
            if (firstKind == BodyKind3D.Dynamic && secondKind == BodyKind3D.Kinematic) {
                kinematicBodyIndex = candidate.SecondBodyIndex;
            } else if (firstKind == BodyKind3D.Kinematic && secondKind == BodyKind3D.Dynamic) {
                kinematicBodyIndex = candidate.FirstBodyIndex;
            } else {
                return false;
            }

            ref HelPhysicsBodyState3D kinematicState = ref Bodies.GetRequiredStateByIndex(kinematicBodyIndex);
            return kinematicState.LinearVelocity.LengthSquared() != PhysicsScalar.Zero ||
                kinematicState.AngularVelocity.LengthSquared() != PhysicsScalar.Zero;
        }

        /// <summary>
        /// Builds all current manifolds into transient storage, preflights every capacity, then warms and publishes the complete batch to cache.
        /// </summary>
        void BuildActiveManifolds() {
            ActiveManifoldCount = 0;
            ActiveContactPointCount = 0;
            for (int candidateIndex = 0; candidateIndex < CandidatePairCount; candidateIndex++) {
                HelPhysicsCandidatePair3D candidate = CandidatePairs[candidateIndex];
                ref HelPhysicsBodyState3D bodyA = ref Bodies.GetRequiredStateByIndex(candidate.FirstBodyIndex);
                ref HelPhysicsBodyState3D bodyB = ref Bodies.GetRequiredStateByIndex(candidate.SecondBodyIndex);
                ref HelPhysicsBodyColdState3D coldStateA = ref Bodies.GetRequiredColdStateByIndex(candidate.FirstBodyIndex);
                ref HelPhysicsBodyColdState3D coldStateB = ref Bodies.GetRequiredColdStateByIndex(candidate.SecondBodyIndex);
                ref HelPhysicsBoxShape3D shapeA = ref Shapes.GetRequiredBox(coldStateA.ShapeHandle);
                ref HelPhysicsBoxShape3D shapeB = ref Shapes.GetRequiredBox(coldStateB.ShapeHandle);
                HelPhysicsContactManifold3D manifold = default;
                if (!HelPhysicsBoxBoxCollision3D.TryBuildManifold(
                    in shapeA,
                    in bodyA,
                    in shapeB,
                    in bodyB,
                    CollisionScratch,
                    ref manifold)) {
                    continue;
                }

                if (ActiveManifoldCount == ActiveManifolds.Length) {
                    throw new HelPhysicsCapacityExceededException("manifold", ActiveManifolds.Length);
                }

                if (manifold.ContactCount > Settings.ContactPointCapacity - ActiveContactPointCount) {
                    throw new HelPhysicsCapacityExceededException("contact point", Settings.ContactPointCapacity);
                }

                ActivePairs[ActiveManifoldCount] = new HelPhysicsPairKey3D(
                    candidate.FirstBodyIndex,
                    candidate.SecondBodyIndex);
                ActiveManifolds[ActiveManifoldCount] = manifold;
                ActiveManifoldCount++;
                ActiveContactPointCount += manifold.ContactCount;
            }

            ReclaimDefinitivelyStaleManifolds();
            int newCacheEntryCount = 0;
            for (int manifoldIndex = 0; manifoldIndex < ActiveManifoldCount; manifoldIndex++) {
                if (!ManifoldCache.TryGet(ActivePairs[manifoldIndex], out _)) {
                    newCacheEntryCount++;
                }
            }

            if (newCacheEntryCount > ManifoldCache.Capacity - ManifoldCache.Count) {
                throw new HelPhysicsCapacityExceededException("manifold", ManifoldCache.Capacity);
            }

            SortParallelManifolds(ActivePairs, ActiveManifolds, ActiveManifoldCount);
            for (int manifoldIndex = 0; manifoldIndex < ActiveManifoldCount; manifoldIndex++) {
                HelPhysicsPairKey3D pair = ActivePairs[manifoldIndex];
                bool anchorsWereStable = ManifoldCache.Update(
                    pair,
                    ref ActiveManifolds[manifoldIndex],
                    StepId);
                HelPhysicsCandidatePair3D candidate = new HelPhysicsCandidatePair3D(
                    pair.FirstBodyIndex,
                    pair.SecondBodyIndex);
                if (!anchorsWereStable && !IsMovingKinematicCandidate(candidate)) {
                    IslandSleeper.WakeForNewCandidateContact(candidate, Bodies, IslandBuilder);
                }
            }

            RouteSpeculativeCandidateSleepSuppression();
        }

        /// <summary>
        /// Suppresses quiet credit for current candidates lacking both a current manifold and a retained sleeping-stable manifold.
        /// </summary>
        void RouteSpeculativeCandidateSleepSuppression() {
            for (int candidateIndex = 0; candidateIndex < CandidatePairCount; candidateIndex++) {
                HelPhysicsCandidatePair3D candidate = CandidatePairs[candidateIndex];
                HelPhysicsPairKey3D pair = new HelPhysicsPairKey3D(
                    candidate.FirstBodyIndex,
                    candidate.SecondBodyIndex);
                if (ContainsPair(ActivePairs, ActiveManifoldCount, pair)) {
                    continue;
                }

                bool hasRetainedSleepingManifold =
                    ManifoldCache.TryGet(pair, out _) &&
                    ShouldRetainSleepingPair(pair);
                if (!hasRetainedSleepingManifold) {
                    IslandSleeper.WakeForNewCandidateContact(candidate, Bodies, IslandBuilder);
                }
            }
        }

        /// <summary>
        /// Reclaims prior cache entries before insertion only when they are absent from current manifolds and cannot qualify for sleeping retention.
        /// </summary>
        void ReclaimDefinitivelyStaleManifolds() {
            for (int cacheEntryIndex = 0; cacheEntryIndex < ManifoldCache.Capacity; cacheEntryIndex++) {
                if (!ManifoldCache.TryGetEntry(
                    cacheEntryIndex,
                    out HelPhysicsPairKey3D pair,
                    out _) ||
                    ContainsPair(ActivePairs, ActiveManifoldCount, pair) ||
                    ShouldRetainSleepingPair(pair)) {
                    continue;
                }

                ManifoldCache.RemoveEntryAt(cacheEntryIndex);
            }
        }

        /// <summary>
        /// Builds current dynamic islands from active contacts plus retained sleeping connectivity, then routes proven moving-kinematic contacts.
        /// </summary>
        void BuildIslandsAndRouteKinematicWakes() {
            IslandManifoldCount = 0;
            for (int manifoldIndex = 0; manifoldIndex < ActiveManifoldCount; manifoldIndex++) {
                IslandPairs[IslandManifoldCount] = ActivePairs[manifoldIndex];
                IslandManifolds[IslandManifoldCount] = ActiveManifolds[manifoldIndex];
                IslandManifoldCount++;
            }

            for (int cacheEntryIndex = 0; cacheEntryIndex < ManifoldCache.Capacity; cacheEntryIndex++) {
                if (!ManifoldCache.TryGetEntry(
                    cacheEntryIndex,
                    out HelPhysicsPairKey3D pair,
                    out HelPhysicsContactManifold3D manifold) ||
                    ContainsPair(ActivePairs, ActiveManifoldCount, pair) ||
                    !ShouldRetainSleepingPair(pair)) {
                    continue;
                }

                if (IslandManifoldCount == IslandManifolds.Length) {
                    throw new HelPhysicsCapacityExceededException("manifold", IslandManifolds.Length);
                }

                IslandPairs[IslandManifoldCount] = pair;
                IslandManifolds[IslandManifoldCount] = manifold;
                IslandManifoldCount++;
            }

            SortParallelManifolds(IslandPairs, IslandManifolds, IslandManifoldCount);
            IslandBuilder.Build(Bodies, IslandPairs, IslandManifolds, IslandManifoldCount);
            for (int manifoldIndex = 0; manifoldIndex < ActiveManifoldCount; manifoldIndex++) {
                IslandSleeper.WakeForMovingKinematicContact(
                    ActivePairs[manifoldIndex],
                    in ActiveManifolds[manifoldIndex],
                    Bodies,
                    IslandBuilder);
            }
        }

        /// <summary>
        /// Prepares current contacts, warm-starts them, executes configured velocity work, writes impulses back, and persists solved state in the same step.
        /// </summary>
        /// <param name="stepSeconds">Current positive fixed scalar step.</param>
        void PrepareWarmStartAndSolve(PhysicsScalar stepSeconds) {
            ContactSolver.Prepare(
                stepSeconds,
                Bodies,
                ActivePairs,
                ActiveManifolds,
                ActiveManifoldCount);
            ContactSolver.WarmStart(Bodies);
            if (ActiveContactPointCount > 0) {
                for (int iterationIndex = 0; iterationIndex < Settings.VelocityIterationCount; iterationIndex++) {
                    ContactSolver.SolveVelocityIteration(Bodies);
                }
            }

            ContactSolver.WriteBack(ActiveManifolds);
            for (int manifoldIndex = 0; manifoldIndex < ActiveManifoldCount; manifoldIndex++) {
                ManifoldCache.StoreSolved(
                    ActivePairs[manifoldIndex],
                    ref ActiveManifolds[manifoldIndex],
                    StepId);
            }
        }

        /// <summary>
        /// Executes the configured number of pose-only penetration-correction passes for current contacts.
        /// </summary>
        void CorrectPenetration() {
            if (ActiveContactPointCount == 0) {
                return;
            }

            for (int passIndex = 0; passIndex < Settings.PenetrationCorrectionPassCount; passIndex++) {
                ContactSolver.CorrectPenetration(Bodies);
            }
        }

        /// <summary>
        /// Touches valid sleeping contacts, expires genuinely untouched cache entries, and updates immutable and profiler metrics.
        /// </summary>
        void RetainSleepingContactsAndPublishMetrics() {
            for (int cacheEntryIndex = 0; cacheEntryIndex < ManifoldCache.Capacity; cacheEntryIndex++) {
                if (ManifoldCache.TryGetEntry(
                    cacheEntryIndex,
                    out HelPhysicsPairKey3D pair,
                    out _) &&
                    ShouldRetainSleepingPair(pair)) {
                    ManifoldCache.Touch(pair, StepId);
                }
            }

            ManifoldCache.RemoveUntouched(StepId);
            int awakeBodyCount = CountAwakeBodies();
            int sleepingIslandCount = CountSleepingIslands();
            int solverIterationCount = ActiveContactPointCount > 0
                ? Settings.VelocityIterationCount
                : 0;
            LastStepMetrics = new HelPhysicsStepMetrics3D(
                ActiveBodyCount,
                awakeBodyCount,
                CandidatePairCount,
                ActiveManifoldCount,
                ActiveContactPointCount,
                IslandBuilder.IslandCount,
                sleepingIslandCount,
                solverIterationCount,
                IslandSleeper.GetWakeCount(HelPhysicsWakeReason3D.ExplicitForce),
                IslandSleeper.GetWakeCount(HelPhysicsWakeReason3D.ExplicitImpulse),
                IslandSleeper.GetWakeCount(HelPhysicsWakeReason3D.NewCandidateContact),
                IslandSleeper.GetWakeCount(HelPhysicsWakeReason3D.MovingKinematicContact));
            RuntimeProfilerMetrics.Publish(
                LastStepMetrics.BodyCount,
                LastStepMetrics.ContactPointCount,
                LastStepMetrics.ManifoldCount);
        }

        /// <summary>
        /// Determines whether a retained pair still connects active sleeping dynamics against unchanged static or sleeping dynamic participants.
        /// </summary>
        /// <param name="pair">Retained canonical pair to validate against current world storage.</param>
        /// <returns><see langword="true"/> when the pair can safely persist without narrow-phase or solver work.</returns>
        bool ShouldRetainSleepingPair(HelPhysicsPairKey3D pair) {
            if (pair.FirstBodyIndex < 0 ||
                pair.SecondBodyIndex >= Bodies.Capacity ||
                !Bodies.IsOccupied(pair.FirstBodyIndex) ||
                !Bodies.IsOccupied(pair.SecondBodyIndex) ||
                !BodyIsActive[pair.FirstBodyIndex] ||
                !BodyIsActive[pair.SecondBodyIndex]) {
                return false;
            }

            BodyKind3D firstKind = Bodies.GetRequiredColdStateByIndex(pair.FirstBodyIndex).BodyKind;
            BodyKind3D secondKind = Bodies.GetRequiredColdStateByIndex(pair.SecondBodyIndex).BodyKind;
            bool firstSleepingDynamic = firstKind == BodyKind3D.Dynamic &&
                !Bodies.GetRequiredStateByIndex(pair.FirstBodyIndex).IsAwake;
            bool secondSleepingDynamic = secondKind == BodyKind3D.Dynamic &&
                !Bodies.GetRequiredStateByIndex(pair.SecondBodyIndex).IsAwake;
            if (firstSleepingDynamic && secondSleepingDynamic) {
                return true;
            } else if (firstSleepingDynamic && secondKind == BodyKind3D.Static) {
                return true;
            } else if (firstKind == BodyKind3D.Static && secondSleepingDynamic) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Counts active awake dynamics after current whole-island sleep evaluation.
        /// </summary>
        /// <returns>The number of active dynamic body slots whose awake flag remains true.</returns>
        int CountAwakeBodies() {
            int awakeBodyCount = 0;
            for (int bodyIndex = 0; bodyIndex < Bodies.Capacity; bodyIndex++) {
                if (Bodies.IsOccupied(bodyIndex) &&
                    BodyIsActive[bodyIndex] &&
                    Bodies.GetRequiredColdStateByIndex(bodyIndex).BodyKind == BodyKind3D.Dynamic &&
                    Bodies.GetRequiredStateByIndex(bodyIndex).IsAwake) {
                    awakeBodyCount++;
                }
            }

            return awakeBodyCount;
        }

        /// <summary>
        /// Counts current island publications whose complete dynamic membership is asleep.
        /// </summary>
        /// <returns>The number of wholly sleeping current dynamic islands.</returns>
        int CountSleepingIslands() {
            int sleepingIslandCount = 0;
            for (int islandIndex = 0; islandIndex < IslandBuilder.IslandCount; islandIndex++) {
                HelPhysicsIsland3D island = IslandBuilder.GetIsland(islandIndex);
                bool isSleeping = true;
                for (int memberOffset = 0; memberOffset < island.BodyCount; memberOffset++) {
                    int bodyIndex = IslandBuilder.GetBodyIndex(island.BodyStartIndex + memberOffset);
                    if (Bodies.GetRequiredStateByIndex(bodyIndex).IsAwake) {
                        isSleeping = false;
                        break;
                    }
                }

                if (isSleeping) {
                    sleepingIslandCount++;
                }
            }

            return sleepingIslandCount;
        }

        /// <summary>
        /// Determines whether a canonical pair appears in a leading fixed-array prefix.
        /// </summary>
        /// <param name="pairs">Fixed pair array to inspect.</param>
        /// <param name="pairCount">Number of leading entries belonging to the current publication.</param>
        /// <param name="pair">Canonical pair to locate.</param>
        /// <returns><see langword="true"/> when an equal pair exists in the active prefix.</returns>
        static bool ContainsPair(HelPhysicsPairKey3D[] pairs, int pairCount, HelPhysicsPairKey3D pair) {
            for (int pairIndex = 0; pairIndex < pairCount; pairIndex++) {
                if (pairs[pairIndex] == pair) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Insertion-sorts a pair/manifold prefix by canonical body indices while preserving exact parallel alignment.
        /// </summary>
        /// <param name="pairs">Pair keys to sort.</param>
        /// <param name="manifolds">Manifolds moved with their owning keys.</param>
        /// <param name="manifoldCount">Number of leading parallel entries to sort.</param>
        static void SortParallelManifolds(
            HelPhysicsPairKey3D[] pairs,
            HelPhysicsContactManifold3D[] manifolds,
            int manifoldCount) {
            for (int manifoldIndex = 1; manifoldIndex < manifoldCount; manifoldIndex++) {
                HelPhysicsPairKey3D pair = pairs[manifoldIndex];
                HelPhysicsContactManifold3D manifold = manifolds[manifoldIndex];
                int insertionIndex = manifoldIndex - 1;
                while (insertionIndex >= 0 && IsPairBefore(pair, pairs[insertionIndex])) {
                    pairs[insertionIndex + 1] = pairs[insertionIndex];
                    manifolds[insertionIndex + 1] = manifolds[insertionIndex];
                    insertionIndex--;
                }

                pairs[insertionIndex + 1] = pair;
                manifolds[insertionIndex + 1] = manifold;
            }
        }

        /// <summary>
        /// Compares canonical pairs lexicographically by first and then second fixed body index.
        /// </summary>
        /// <param name="first">Candidate earlier pair.</param>
        /// <param name="second">Candidate later pair.</param>
        /// <returns><see langword="true"/> when <paramref name="first"/> belongs before <paramref name="second"/>.</returns>
        static bool IsPairBefore(HelPhysicsPairKey3D first, HelPhysicsPairKey3D second) {
            if (first.FirstBodyIndex != second.FirstBodyIndex) {
                return first.FirstBodyIndex < second.FirstBodyIndex;
            }

            return first.SecondBodyIndex < second.SecondBodyIndex;
        }
    }
}
