using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;

namespace helengine {
    /// <summary>
    /// Hosts the real BEPU simulation used by supported Helengine 3D rigid-body scenes.
    /// </summary>
    public sealed class BepuPhysicsWorld3D : ISceneBindablePhysicsRuntime, IPhysicsBodySynchronizationRuntime3D, IPhysicsTriggerEventRuntime3D {
        /// <summary>
        /// Default contact spring settings used for the initial BEPU integration pass.
        /// </summary>
        static readonly SpringSettings DefaultContactSpringSettings = new SpringSettings(30f, 1f);

        /// <summary>
        /// Stores the default velocity-iteration count used by the standard runtime solve schedule.
        /// </summary>
        const int DefaultSolveVelocityIterationCount = 4;

        /// <summary>
        /// Stores the default substep count used by the standard runtime solve schedule.
        /// </summary>
        const int DefaultSolveSubstepCount = 1;

        /// <summary>
        /// Stores the minimum block size used by the BEPU memory pool for the reduced Helengine runtime slice.
        /// </summary>
        const int DefaultBufferPoolBlockSize = 16384;

        /// <summary>
        /// Stores the expected number of pooled BEPU memory resources retained by the reduced Helengine runtime slice.
        /// </summary>
        const int DefaultBufferPoolResourceCount = 8;

        /// <summary>
        /// Shared memory pool used by the active BEPU simulation.
        /// </summary>
        readonly BufferPool BufferPoolValue;

        /// <summary>
        /// Runtime registry containing every bound rigid body supported by the BEPU adapter.
        /// </summary>
        readonly BepuBodyRegistry3D BodyRegistryValue;

        /// <summary>
        /// Trigger overlap events emitted during the most recent fixed step.
        /// </summary>
        readonly List<TriggerEvent3D> TriggerEventsValue;

        /// <summary>
        /// Trigger overlap pairs that remained active after the previous fixed step completed.
        /// </summary>
        readonly List<TriggerPairKey3D> ActiveTriggerPairsValue;

        /// <summary>
        /// Trigger overlap pairs detected during the current fixed step.
        /// </summary>
        readonly List<TriggerPairKey3D> CurrentTriggerPairsValue;

        /// <summary>
        /// Stores the configured BEPU velocity-iteration count used whenever the world recreates its simulation.
        /// </summary>
        readonly int SolveVelocityIterationCountValue;

        /// <summary>
        /// Stores the configured BEPU substep count used whenever the world recreates its simulation.
        /// </summary>
        readonly int SolveSubstepCountValue;

        /// <summary>
        /// Active BEPU simulation owned by the world.
        /// </summary>
        Simulation SimulationValue;

        /// <summary>
        /// Authored collidable properties aligned to active BEPU collidable handles.
        /// </summary>
        CollidableProperty<BepuCollidableProperties3D> CollidablePropertiesValue;

        /// <summary>
        /// Authored gravity accelerations aligned to active BEPU body handles.
        /// </summary>
        CollidableProperty<float> GravityAccelerationsValue;

        /// <summary>
        /// Initializes one BEPU-backed physics world.
        /// </summary>
        public BepuPhysicsWorld3D() {
            SolveVelocityIterationCountValue = DefaultSolveVelocityIterationCount;
            SolveSubstepCountValue = DefaultSolveSubstepCount;
            BufferPoolValue = new BufferPool(DefaultBufferPoolBlockSize, DefaultBufferPoolResourceCount);
            BodyRegistryValue = new BepuBodyRegistry3D();
            TriggerEventsValue = new List<TriggerEvent3D>();
            ActiveTriggerPairsValue = new List<TriggerPairKey3D>();
            CurrentTriggerPairsValue = new List<TriggerPairKey3D>();
            ResetSimulation();
        }

        /// <summary>
        /// Initializes one BEPU-backed physics world with an explicit solve schedule.
        /// </summary>
        /// <param name="solveVelocityIterationCount">Velocity-iteration count applied to each simulation step.</param>
        /// <param name="solveSubstepCount">Substep count applied to each simulation step.</param>
        public BepuPhysicsWorld3D(int solveVelocityIterationCount, int solveSubstepCount) {
            if (solveVelocityIterationCount <= 0) {
                throw new ArgumentOutOfRangeException(nameof(solveVelocityIterationCount), "Velocity iteration count must be greater than zero.");
            } else if (solveSubstepCount <= 0) {
                throw new ArgumentOutOfRangeException(nameof(solveSubstepCount), "Substep count must be greater than zero.");
            }

            SolveVelocityIterationCountValue = solveVelocityIterationCount;
            SolveSubstepCountValue = solveSubstepCount;
            BufferPoolValue = new BufferPool(DefaultBufferPoolBlockSize, DefaultBufferPoolResourceCount);
            BodyRegistryValue = new BepuBodyRegistry3D();
            TriggerEventsValue = new List<TriggerEvent3D>();
            ActiveTriggerPairsValue = new List<TriggerPairKey3D>();
            CurrentTriggerPairsValue = new List<TriggerPairKey3D>();
            ResetSimulation();
        }

        /// <summary>
        /// Creates one default BEPU-backed physics world.
        /// </summary>
        /// <returns>Constructed physics world instance.</returns>
        public static BepuPhysicsWorld3D CreateDefault() {
            return new BepuPhysicsWorld3D();
        }

        /// <summary>
        /// Creates one BEPU-backed physics world with an explicit solve schedule.
        /// </summary>
        /// <param name="solveVelocityIterationCount">Velocity-iteration count applied to each simulation step.</param>
        /// <param name="solveSubstepCount">Substep count applied to each simulation step.</param>
        /// <returns>Constructed physics world instance.</returns>
        public static BepuPhysicsWorld3D CreateWithSolveSchedule(int solveVelocityIterationCount, int solveSubstepCount) {
            return new BepuPhysicsWorld3D(solveVelocityIterationCount, solveSubstepCount);
        }

        /// <summary>
        /// Gets the number of rigid bodies currently registered in the bound scene.
        /// </summary>
        public int RegisteredBodyCount => BodyRegistryValue.Handles.Count;

        /// <summary>
        /// Gets the trigger overlap events emitted during the most recent fixed step.
        /// </summary>
        public IReadOnlyList<TriggerEvent3D> TriggerEvents => TriggerEventsValue;

        /// <summary>
        /// Gets the configured BEPU velocity-iteration count used by the active world.
        /// </summary>
        public int SolveVelocityIterationCount => SolveVelocityIterationCountValue;

        /// <summary>
        /// Gets the configured BEPU substep count used by the active world.
        /// </summary>
        public int SolveSubstepCount => SolveSubstepCountValue;

        /// <summary>
        /// Synchronizes one already-bound kinematic body from the current authored entity transform and rigid-body velocity values.
        /// </summary>
        /// <param name="entity">Bound entity whose kinematic body should be updated.</param>
        public void SynchronizeKinematicBody(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            } else if (SimulationValue == null) {
                throw new InvalidOperationException("A BEPU simulation must exist before kinematic bodies can be synchronized.");
            }

            BepuBodyHandle3D handle = FindRequiredKinematicBodyHandle(entity);
            BodyReference bodyReference = SimulationValue.Bodies[handle.BodyHandle];
            bodyReference.Pose = BepuEntitySynchronization3D.CreatePose(entity);
            bodyReference.Velocity = BepuEntitySynchronization3D.CreateVelocity(handle.RigidBody);
            bodyReference.UpdateBounds();
            bodyReference.Awake = true;
        }

        /// <summary>
        /// Synchronizes one already-bound dynamic body from the current authored entity transform and rigid-body velocity values.
        /// </summary>
        /// <param name="entity">Bound entity whose dynamic body should be updated.</param>
        public void SynchronizeDynamicBody(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            } else if (SimulationValue == null) {
                throw new InvalidOperationException("A BEPU simulation must exist before dynamic bodies can be synchronized.");
            }

            BepuBodyHandle3D handle = FindRequiredDynamicBodyHandle(entity);
            BodyReference bodyReference = SimulationValue.Bodies[handle.BodyHandle];
            bodyReference.Pose = BepuEntitySynchronization3D.CreatePose(entity);
            bodyReference.Velocity = BepuEntitySynchronization3D.CreateVelocity(handle.RigidBody);
            bodyReference.UpdateBounds();
            bodyReference.Awake = true;
        }

        /// <summary>
        /// Synchronizes one already-bound dynamic body's authored velocity values into the live BEPU runtime while preserving the current runtime pose.
        /// </summary>
        /// <param name="entity">Bound entity whose dynamic body velocity should be updated.</param>
        public void SynchronizeDynamicBodyVelocity(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            } else if (SimulationValue == null) {
                throw new InvalidOperationException("A BEPU simulation must exist before dynamic-body velocities can be synchronized.");
            }

            BepuBodyHandle3D handle = FindRequiredDynamicBodyHandle(entity);
            BodyReference bodyReference = SimulationValue.Bodies[handle.BodyHandle];
            bodyReference.Velocity = BepuEntitySynchronization3D.CreateVelocity(handle.RigidBody);
            bodyReference.Awake = true;
        }

        /// <summary>
        /// Binds one scene hierarchy to the active BEPU simulation.
        /// </summary>
        /// <param name="rootEntities">Root entities that should be scanned for supported rigid bodies.</param>
        public void BindScene(IReadOnlyList<Entity> rootEntities) {
            if (rootEntities == null) {
                throw new ArgumentNullException(nameof(rootEntities));
            }

            BodyRegistryValue.Clear();
            TriggerEventsValue.Clear();
            ActiveTriggerPairsValue.Clear();
            CurrentTriggerPairsValue.Clear();
            ResetSimulation();
            for (int index = 0; index < rootEntities.Count; index++) {
                RegisterEntityHierarchy(rootEntities[index]);
            }

            BepuPhysicsWorld3DDiagnostics.Reset(BodyRegistryValue.Handles);
        }

        /// <summary>
        /// Advances the active BEPU simulation by one fixed simulation step.
        /// </summary>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        public void Step(double stepSeconds) {
            if (double.IsNaN(stepSeconds) || double.IsInfinity(stepSeconds) || stepSeconds <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), "Simulation step must be a finite value greater than zero.");
            }

            if (Core.Instance != null) {
                Core.Instance.ReportSceneTransitionStage("BeforeBepuTimestep");
            }
            RuntimeExecutionPhaseProbe.SetCurrentPhaseId(RuntimeExecutionPhaseProbe.BeforeBepuTimestepPhaseId);
            SimulationValue.Timestep((float)stepSeconds);
            if (Core.Instance != null) {
                Core.Instance.ReportSceneTransitionStage("AfterBepuTimestepBeforeSync");
            }
            RuntimeExecutionPhaseProbe.SetCurrentPhaseId(RuntimeExecutionPhaseProbe.AfterBepuTimestepBeforeSyncPhaseId);
            if (Core.Instance != null) {
                Core.Instance.ReportSceneTransitionStage("BeforeBepuSynchronizeBodies");
            }
            SynchronizeBodiesBackToEntities();
            if (Core.Instance != null) {
                Core.Instance.ReportSceneTransitionStage("AfterBepuSynchronizeBodiesBeforeTriggerCollection");
            }
            if (Core.Instance != null) {
                Core.Instance.ReportSceneTransitionStage("BeforeBepuCollectTriggerEvents");
            }
            CollectTriggerEvents();
            if (Core.Instance != null) {
                Core.Instance.ReportSceneTransitionStage("AfterBepuCollectTriggerEvents");
            }
            RuntimeExecutionPhaseProbe.SetCurrentPhaseId(RuntimeExecutionPhaseProbe.AfterBepuSyncPhaseId);
            if (Core.Instance != null) {
                Core.Instance.ReportSceneTransitionStage("AfterBepuSync");
            }
        }

        /// <summary>
        /// Recreates the BEPU simulation and the collidable-property stores that accompany it.
        /// </summary>
        void ResetSimulation() {
            if (SimulationValue != null) {
                SimulationValue.Dispose();
            }

            CollidablePropertiesValue = new CollidableProperty<BepuCollidableProperties3D>(BufferPoolValue);
            GravityAccelerationsValue = new CollidableProperty<float>(BufferPoolValue);
            HelengineBepuNarrowPhaseCallbacks narrowPhaseCallbacks = new HelengineBepuNarrowPhaseCallbacks(CollidablePropertiesValue);
            HelengineBepuPoseIntegratorCallbacks poseIntegratorCallbacks = new HelengineBepuPoseIntegratorCallbacks(GravityAccelerationsValue);
            SimulationValue = Simulation.Create(
                BufferPoolValue,
                narrowPhaseCallbacks,
                poseIntegratorCallbacks,
                new SolveDescription(SolveVelocityIterationCountValue, SolveSubstepCountValue),
                initialAllocationSizes: CreateDefaultSimulationAllocationSizes());
            WireSimulationStageDiagnostics();
        }

        /// <summary>
        /// Subscribes the active BEPU timestepper events into the shared core diagnostics stream so native crash repros can distinguish the major simulation stages inside one timestep.
        /// </summary>
        void WireSimulationStageDiagnostics() {
            if (SimulationValue == null) {
                throw new InvalidOperationException("Simulation must exist before timestep diagnostics can be wired.");
            }

            DefaultTimestepper defaultTimestepper = SimulationValue.Timestepper as DefaultTimestepper;
            if (defaultTimestepper == null) {
                return;
            }

            defaultTimestepper.Slept += OnSimulationSlept;
            defaultTimestepper.BeforeCollisionDetection += OnSimulationBeforeCollisionDetection;
            defaultTimestepper.CollisionsDetected += OnSimulationCollisionsDetected;
            defaultTimestepper.ConstraintsSolved += OnSimulationConstraintsSolved;
            SimulationValue.BeforeCollisionOverlapDispatch += OnSimulationBeforeCollisionOverlapDispatch;
            SimulationValue.AfterCollisionOverlapDispatch += OnSimulationAfterCollisionOverlapDispatch;
            SimulationValue.AfterCollisionFlush += OnSimulationAfterCollisionFlush;
        }

        /// <summary>
        /// Reports that the BEPU sleeper completed and the timestep is about to predict body bounding boxes.
        /// </summary>
        /// <param name="dt">Simulation timestep in seconds.</param>
        /// <param name="threadDispatcher">Optional dispatcher used by the active BEPU simulation.</param>
        void OnSimulationSlept(float dt, IThreadDispatcher threadDispatcher) {
            if (Core.Instance != null) {
                Core.Instance.ReportSceneTransitionStage("AfterBepuSleepBeforePredictBoundingBoxes");
            }
        }

        /// <summary>
        /// Reports that BEPU predicted bounding boxes and is about to run collision detection.
        /// </summary>
        /// <param name="dt">Simulation timestep in seconds.</param>
        /// <param name="threadDispatcher">Optional dispatcher used by the active BEPU simulation.</param>
        void OnSimulationBeforeCollisionDetection(float dt, IThreadDispatcher threadDispatcher) {
            if (Core.Instance != null) {
                Core.Instance.ReportSceneTransitionStage("AfterBepuPredictBoundingBoxesBeforeCollisionDetection");
            }
        }

        /// <summary>
        /// Reports that BEPU completed collision detection and is about to enter the solver phase.
        /// </summary>
        /// <param name="dt">Simulation timestep in seconds.</param>
        /// <param name="threadDispatcher">Optional dispatcher used by the active BEPU simulation.</param>
        void OnSimulationCollisionsDetected(float dt, IThreadDispatcher threadDispatcher) {
            if (Core.Instance != null) {
                Core.Instance.ReportSceneTransitionStage("AfterBepuCollisionDetectionBeforeSolve");
            }
        }

        /// <summary>
        /// Reports that BEPU completed constraint solving and is about to run post-solve data-structure optimization.
        /// </summary>
        /// <param name="dt">Simulation timestep in seconds.</param>
        /// <param name="threadDispatcher">Optional dispatcher used by the active BEPU simulation.</param>
        void OnSimulationConstraintsSolved(float dt, IThreadDispatcher threadDispatcher) {
            if (Core.Instance != null) {
                Core.Instance.ReportSceneTransitionStage("AfterBepuSolveBeforeOptimize");
            }
        }

        /// <summary>
        /// Reports that BEPU completed its broad phase update and is about to dispatch potentially colliding overlaps.
        /// </summary>
        /// <param name="dt">Simulation timestep in seconds.</param>
        /// <param name="threadDispatcher">Optional dispatcher used by the active BEPU simulation.</param>
        void OnSimulationBeforeCollisionOverlapDispatch(float dt, IThreadDispatcher threadDispatcher) {
            if (Core.Instance != null) {
                Core.Instance.ReportSceneTransitionStage("AfterBepuBroadPhaseUpdateBeforeOverlapDispatch");
            }
        }

        /// <summary>
        /// Reports that BEPU completed overlap dispatch and is about to flush the narrow phase.
        /// </summary>
        /// <param name="dt">Simulation timestep in seconds.</param>
        /// <param name="threadDispatcher">Optional dispatcher used by the active BEPU simulation.</param>
        void OnSimulationAfterCollisionOverlapDispatch(float dt, IThreadDispatcher threadDispatcher) {
            if (Core.Instance != null) {
                Core.Instance.ReportSceneTransitionStage("AfterBepuOverlapDispatchBeforeNarrowPhaseFlush");
            }
        }

        /// <summary>
        /// Reports that BEPU finished its narrow phase flush and collision detection is about to return.
        /// </summary>
        /// <param name="dt">Simulation timestep in seconds.</param>
        /// <param name="threadDispatcher">Optional dispatcher used by the active BEPU simulation.</param>
        void OnSimulationAfterCollisionFlush(float dt, IThreadDispatcher threadDispatcher) {
            if (Core.Instance != null) {
                Core.Instance.ReportSceneTransitionStage("AfterBepuNarrowPhaseFlush");
            }
        }

        /// <summary>
        /// Creates one conservative initial-allocation profile sized for Helengine's reduced box-and-sphere BEPU runtime slice.
        /// </summary>
        /// <returns>Initial simulation allocation sizes for the reduced runtime slice.</returns>
        static SimulationAllocationSizes CreateDefaultSimulationAllocationSizes() {
            return new SimulationAllocationSizes(
                bodies: 16,
                statics: 8,
                islands: 8,
                shapesPerType: 8,
                constraints: 64,
                constraintsPerTypeBatch: 16,
                constraintCountPerBodyEstimate: 2);
        }

        /// <summary>
        /// Recursively scans one entity hierarchy and registers supported rigid bodies.
        /// </summary>
        /// <param name="entity">Entity hierarchy root to inspect.</param>
        void RegisterEntityHierarchy(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            RegisterEntityIfSupported(entity);
            List<Entity> children = entity.Children;
            if (children == null) {
                return;
            }

            for (int childIndex = 0; childIndex < children.Count; childIndex++) {
                RegisterEntityHierarchy(children[childIndex]);
            }
        }

        /// <summary>
        /// Registers one supported entity into the active BEPU simulation.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        void RegisterEntityIfSupported(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            BepuPhysicsFeatureGuard3D.ValidateEntity(entity);
            RigidBody3DComponent rigidBody = ResolveRigidBody(entity);
            if (rigidBody == null) {
                return;
            }

            BoxCollider3DComponent boxCollider = ResolveBoxCollider(entity);
            SphereCollider3DComponent sphereCollider = ResolveSphereCollider(entity);
            StaticMeshCollider3DComponent staticMeshCollider = ResolveStaticMeshCollider(entity);
            if (boxCollider == null && sphereCollider == null && staticMeshCollider == null) {
                return;
            }
            int colliderCount = 0;
            colliderCount += boxCollider == null ? 0 : 1;
            colliderCount += sphereCollider == null ? 0 : 1;
            colliderCount += staticMeshCollider == null ? 0 : 1;
            if (colliderCount > 1) {
                throw new NotSupportedException("Entities with more than one supported collider are not supported by helengine.bepu.");
            }

            if (staticMeshCollider != null) {
                RegisterStaticMeshBody(entity, rigidBody, staticMeshCollider);
                return;
            }

            if (boxCollider != null) {
                RegisterBoxBody(entity, rigidBody, boxCollider);
                return;
            }

            RegisterSphereBody(entity, rigidBody, sphereCollider);
        }

        /// <summary>
        /// Registers one box-backed rigid body in the active BEPU simulation.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        /// <param name="rigidBody">Authored rigid body.</param>
        /// <param name="boxCollider">Authored box collider.</param>
        void RegisterBoxBody(Entity entity, RigidBody3DComponent rigidBody, BoxCollider3DComponent boxCollider) {
            Box boxShape = BepuShapeFactory3D.CreateBoxShape(boxCollider);
            TypedIndex shapeIndex = SimulationValue.Shapes.Add(boxShape);
            if (rigidBody.BodyKind == BodyKind3D.Static) {
                StaticHandle staticHandle = SimulationValue.Statics.Add(new StaticDescription(BepuEntitySynchronization3D.CreatePose(entity), shapeIndex));
                CollidablePropertiesValue.Allocate(staticHandle) = CreateCollidableProperties(boxCollider);
                BodyRegistryValue.Add(new BepuBodyHandle3D(entity, rigidBody, boxCollider, shapeIndex, staticHandle));
                return;
            }

            BodyHandle bodyHandle = RegisterDynamicOrKinematicBoxBody(entity, rigidBody, boxShape, shapeIndex);
            CollidablePropertiesValue.Allocate(bodyHandle) = CreateCollidableProperties(boxCollider);
            GravityAccelerationsValue.Allocate(bodyHandle) = ResolveGravityAcceleration(rigidBody);
            BodyRegistryValue.Add(new BepuBodyHandle3D(entity, rigidBody, boxCollider, shapeIndex, bodyHandle));
        }

        /// <summary>
        /// Registers one sphere-backed rigid body in the active BEPU simulation.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        /// <param name="rigidBody">Authored rigid body.</param>
        /// <param name="sphereCollider">Authored sphere collider.</param>
        void RegisterSphereBody(Entity entity, RigidBody3DComponent rigidBody, SphereCollider3DComponent sphereCollider) {
            Sphere sphereShape = BepuShapeFactory3D.CreateSphereShape(sphereCollider);
            TypedIndex shapeIndex = SimulationValue.Shapes.Add(sphereShape);
            if (rigidBody.BodyKind == BodyKind3D.Static) {
                StaticHandle staticHandle = SimulationValue.Statics.Add(new StaticDescription(BepuEntitySynchronization3D.CreatePose(entity), shapeIndex));
                CollidablePropertiesValue.Allocate(staticHandle) = CreateCollidableProperties(sphereCollider);
                BodyRegistryValue.Add(new BepuBodyHandle3D(entity, rigidBody, sphereCollider, shapeIndex, staticHandle));
                return;
            }

            BodyHandle bodyHandle = RegisterDynamicOrKinematicSphereBody(entity, rigidBody, sphereShape, shapeIndex);
            CollidablePropertiesValue.Allocate(bodyHandle) = CreateCollidableProperties(sphereCollider);
            GravityAccelerationsValue.Allocate(bodyHandle) = ResolveGravityAcceleration(rigidBody);
            BodyRegistryValue.Add(new BepuBodyHandle3D(entity, rigidBody, sphereCollider, shapeIndex, bodyHandle));
        }

        /// <summary>
        /// Registers one cooked static-mesh-backed rigid body in the active BEPU simulation.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        /// <param name="rigidBody">Authored rigid body.</param>
        /// <param name="staticMeshCollider">Authored static mesh collider.</param>
        void RegisterStaticMeshBody(Entity entity, RigidBody3DComponent rigidBody, StaticMeshCollider3DComponent staticMeshCollider) {
            if (rigidBody.BodyKind != BodyKind3D.Static) {
                throw new NotSupportedException("Static mesh colliders are supported only for static rigid bodies in helengine.bepu.");
            }

            Mesh meshShape = BepuShapeFactory3D.CreateStaticMeshShape(staticMeshCollider.CookedRuntimeData, BufferPoolValue);
            TypedIndex shapeIndex = SimulationValue.Shapes.Add(meshShape);
            StaticHandle staticHandle = SimulationValue.Statics.Add(new StaticDescription(BepuEntitySynchronization3D.CreatePose(entity), shapeIndex));
            CollidablePropertiesValue.Allocate(staticHandle) = CreateCollidableProperties(staticMeshCollider);
            BodyRegistryValue.Add(new BepuBodyHandle3D(entity, rigidBody, staticMeshCollider, shapeIndex, staticHandle));
        }

        /// <summary>
        /// Builds one bounded debug snapshot string for the authored four-way stack-box validation scene.
        /// </summary>
        /// <returns>Snapshot text when the currently bound scene matches the traced stack-box layout; otherwise an empty string.</returns>
        public string TryBuildStackBoxesDebugSnapshot() {
            return BepuPhysicsWorld3DDiagnostics.BuildSyncSnapshot(BodyRegistryValue.Handles, SimulationValue);
        }

        /// <summary>
        /// Returns one direct sentinel string so the native host can verify generated string-return plumbing independently of the diagnostics builder path.
        /// </summary>
        /// <returns>One constant sentinel line.</returns>
        public string TryBuildStackBoxesDebugSentinel() {
            return "[BepuWorldSentinel]\n";
        }

        /// <summary>
        /// Registers one dynamic or kinematic box body.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        /// <param name="rigidBody">Authored rigid body.</param>
        /// <param name="boxShape">BEPU box shape.</param>
        /// <param name="shapeIndex">Allocated BEPU shape index.</param>
        /// <returns>Registered BEPU body handle.</returns>
        BodyHandle RegisterDynamicOrKinematicBoxBody(Entity entity, RigidBody3DComponent rigidBody, Box boxShape, TypedIndex shapeIndex) {
            RigidPose pose = BepuEntitySynchronization3D.CreatePose(entity);
            BodyVelocity velocity = BepuEntitySynchronization3D.CreateVelocity(rigidBody);
            if (rigidBody.BodyKind == BodyKind3D.Kinematic) {
                BodyDescription bodyDescription = BodyDescription.CreateKinematic(pose, velocity, shapeIndex, BodyDescription.GetDefaultActivity(boxShape));
                return SimulationValue.Bodies.Add(bodyDescription);
            }

            BodyDescription dynamicDescription = BodyDescription.CreateDynamic(
                pose,
                velocity,
                boxShape.ComputeInertia((float)rigidBody.Mass),
                shapeIndex,
                BodyDescription.GetDefaultActivity(boxShape));
            return SimulationValue.Bodies.Add(dynamicDescription);
        }

        /// <summary>
        /// Registers one dynamic or kinematic sphere body.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        /// <param name="rigidBody">Authored rigid body.</param>
        /// <param name="sphereShape">BEPU sphere shape.</param>
        /// <param name="shapeIndex">Allocated BEPU shape index.</param>
        /// <returns>Registered BEPU body handle.</returns>
        BodyHandle RegisterDynamicOrKinematicSphereBody(Entity entity, RigidBody3DComponent rigidBody, Sphere sphereShape, TypedIndex shapeIndex) {
            RigidPose pose = BepuEntitySynchronization3D.CreatePose(entity);
            BodyVelocity velocity = BepuEntitySynchronization3D.CreateVelocity(rigidBody);
            if (rigidBody.BodyKind == BodyKind3D.Kinematic) {
                BodyDescription bodyDescription = BodyDescription.CreateKinematic(pose, velocity, shapeIndex, BodyDescription.GetDefaultActivity(sphereShape));
                return SimulationValue.Bodies.Add(bodyDescription);
            }

            BodyDescription dynamicDescription = BodyDescription.CreateDynamic(
                pose,
                velocity,
                sphereShape.ComputeInertia((float)rigidBody.Mass),
                shapeIndex,
                BodyDescription.GetDefaultActivity(sphereShape));
            return SimulationValue.Bodies.Add(dynamicDescription);
        }

        /// <summary>
        /// Copies resolved runtime transforms and velocities back into the authored entity graph.
        /// </summary>
        void SynchronizeBodiesBackToEntities() {
            IReadOnlyList<BepuBodyHandle3D> handles = BodyRegistryValue.Handles;
            for (int index = 0; index < handles.Count; index++) {
                BepuBodyHandle3D handle = handles[index];
                if (!handle.HasBodyHandle) {
                    continue;
                }

                BodyReference bodyReference = SimulationValue.Bodies[handle.BodyHandle];
                BepuEntitySynchronization3D.CopyBodyToEntity(bodyReference, handle.Entity, handle.RigidBody);
            }
        }

        /// <summary>
        /// Resolves one already-bound kinematic runtime handle for the supplied entity.
        /// </summary>
        /// <param name="entity">Bound entity whose kinematic body should be located.</param>
        /// <returns>Resolved kinematic runtime handle.</returns>
        BepuBodyHandle3D FindRequiredKinematicBodyHandle(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            IReadOnlyList<BepuBodyHandle3D> handles = BodyRegistryValue.Handles;
            for (int index = 0; index < handles.Count; index++) {
                BepuBodyHandle3D handle = handles[index];
                if (!ReferenceEquals(handle.Entity, entity)) {
                    continue;
                } else if (!handle.IsKinematic) {
                    throw new InvalidOperationException("Only kinematic BEPU bodies can be synchronized from authored transforms.");
                } else if (!handle.HasBodyHandle) {
                    throw new InvalidOperationException("Kinematic BEPU body synchronization requires one runtime body handle.");
                }

                return handle;
            }

            throw new InvalidOperationException("The supplied entity is not registered as one bound kinematic BEPU body.");
        }

        /// <summary>
        /// Resolves one already-bound dynamic runtime handle for the supplied entity.
        /// </summary>
        /// <param name="entity">Bound entity whose dynamic body should be located.</param>
        /// <returns>Resolved dynamic runtime handle.</returns>
        BepuBodyHandle3D FindRequiredDynamicBodyHandle(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            IReadOnlyList<BepuBodyHandle3D> handles = BodyRegistryValue.Handles;
            for (int index = 0; index < handles.Count; index++) {
                BepuBodyHandle3D handle = handles[index];
                if (!ReferenceEquals(handle.Entity, entity)) {
                    continue;
                } else if (!handle.IsDynamic) {
                    throw new InvalidOperationException("Only dynamic BEPU bodies can be synchronized through the dynamic-body transform path.");
                } else if (!handle.HasBodyHandle) {
                    throw new InvalidOperationException("Dynamic BEPU body synchronization requires one runtime body handle.");
                }

                return handle;
            }

            throw new InvalidOperationException("The supplied entity is not registered as one bound dynamic BEPU body.");
        }

        /// <summary>
        /// Resolves the authored rigid-body component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>Rigid body component when present; otherwise null.</returns>
        RigidBody3DComponent ResolveRigidBody(Entity entity) {
            List<Component> components = entity.Components;
            for (int index = 0; index < components.Count; index++) {
                if (components[index] is RigidBody3DComponent rigidBody) {
                    return rigidBody;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the authored box collider attached to one entity.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>Box collider component when present; otherwise null.</returns>
        BoxCollider3DComponent ResolveBoxCollider(Entity entity) {
            List<Component> components = entity.Components;
            for (int index = 0; index < components.Count; index++) {
                if (components[index] is BoxCollider3DComponent boxCollider) {
                    return boxCollider;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the authored sphere collider attached to one entity.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>Sphere collider component when present; otherwise null.</returns>
        SphereCollider3DComponent ResolveSphereCollider(Entity entity) {
            List<Component> components = entity.Components;
            for (int index = 0; index < components.Count; index++) {
                if (components[index] is SphereCollider3DComponent sphereCollider) {
                    return sphereCollider;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the authored static mesh collider attached to one entity.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>Static mesh collider component when present; otherwise null.</returns>
        StaticMeshCollider3DComponent ResolveStaticMeshCollider(Entity entity) {
            List<Component> components = entity.Components;
            for (int index = 0; index < components.Count; index++) {
                if (components[index] is StaticMeshCollider3DComponent staticMeshCollider) {
                    return staticMeshCollider;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates the BEPU collidable metadata used for filtering and material blending.
        /// </summary>
        /// <param name="collider">Authored collider to translate.</param>
        /// <returns>BEPU collidable properties matching the authored collider.</returns>
        BepuCollidableProperties3D CreateCollidableProperties(Collider3DComponent collider) {
            return new BepuCollidableProperties3D {
                CollisionLayer = collider.CollisionLayer,
                CollisionMask = collider.CollisionMask,
                IsTrigger = collider.IsTrigger,
                DynamicFriction = (float)collider.DynamicFriction,
                MaximumRecoveryVelocity = (float)(2d + (collider.Restitution * 8d)),
                SpringSettings = DefaultContactSpringSettings
            };
        }

        /// <summary>
        /// Collects primitive trigger overlaps for the current fixed step and emits enter, stay, and exit events.
        /// </summary>
        void CollectTriggerEvents() {
            TriggerEventsValue.Clear();
            CurrentTriggerPairsValue.Clear();

            IReadOnlyList<BepuBodyHandle3D> handles = BodyRegistryValue.Handles;
            for (int firstIndex = 0; firstIndex < handles.Count; firstIndex++) {
                BepuBodyHandle3D first = handles[firstIndex];
                for (int secondIndex = firstIndex + 1; secondIndex < handles.Count; secondIndex++) {
                    BepuBodyHandle3D second = handles[secondIndex];
                    if (!CanBodiesInteract(first, second)) {
                        continue;
                    }
                    if (!IsTriggerPair(first, second)) {
                        continue;
                    }
                    if (!PrimitiveTriggerBodiesOverlap(first, second)) {
                        continue;
                    }

                    TrackTriggerPair(first, second);
                }
            }

            FinalizeTriggerEvents();
        }

        /// <summary>
        /// Returns whether the supplied handle pair contains at least one trigger collider.
        /// </summary>
        static bool IsTriggerPair(BepuBodyHandle3D first, BepuBodyHandle3D second) {
            return GetRequiredCollider(first).IsTrigger || GetRequiredCollider(second).IsTrigger;
        }

        /// <summary>
        /// Returns whether the supplied handle pair passes the authored collision-layer filtering.
        /// </summary>
        static bool CanBodiesInteract(BepuBodyHandle3D first, BepuBodyHandle3D second) {
            Collider3DComponent firstCollider = GetRequiredCollider(first);
            Collider3DComponent secondCollider = GetRequiredCollider(second);
            return (firstCollider.CollisionMask & secondCollider.CollisionLayer) != 0 &&
                (secondCollider.CollisionMask & firstCollider.CollisionLayer) != 0;
        }

        /// <summary>
        /// Tracks one primitive trigger pair using whichever entity owns the trigger collider.
        /// </summary>
        void TrackTriggerPair(BepuBodyHandle3D first, BepuBodyHandle3D second) {
            Collider3DComponent firstCollider = GetRequiredCollider(first);
            Collider3DComponent secondCollider = GetRequiredCollider(second);
            if (firstCollider.IsTrigger) {
                AddCurrentTriggerPair(new TriggerPairKey3D(first.Entity, second.Entity));
                return;
            }
            if (secondCollider.IsTrigger) {
                AddCurrentTriggerPair(new TriggerPairKey3D(second.Entity, first.Entity));
                return;
            }

            throw new InvalidOperationException("Tracked trigger overlap pair does not contain a trigger collider.");
        }

        /// <summary>
        /// Returns whether the supplied primitive collider pair currently overlaps according to authored entity transforms.
        /// </summary>
        static bool PrimitiveTriggerBodiesOverlap(BepuBodyHandle3D first, BepuBodyHandle3D second) {
            if (first.SphereCollider != null && second.SphereCollider != null) {
                return SphereSphereOverlaps(first, second);
            }
            if (first.SphereCollider != null && second.BoxCollider != null) {
                return SphereBoxOverlaps(first, second);
            }
            if (first.BoxCollider != null && second.SphereCollider != null) {
                return SphereBoxOverlaps(second, first);
            }

            return false;
        }

        /// <summary>
        /// Returns whether the supplied sphere pair currently overlaps.
        /// </summary>
        static bool SphereSphereOverlaps(BepuBodyHandle3D first, BepuBodyHandle3D second) {
            float firstRadius = ResolveWorldSphereRadius(first);
            float secondRadius = ResolveWorldSphereRadius(second);
            float combinedRadius = firstRadius + secondRadius;
            return (first.Entity.Position - second.Entity.Position).LengthSquared() <= combinedRadius * combinedRadius;
        }

        /// <summary>
        /// Returns whether the supplied sphere overlaps the supplied box.
        /// </summary>
        static bool SphereBoxOverlaps(BepuBodyHandle3D sphereHandle, BepuBodyHandle3D boxHandle) {
            float3 boxCenter = boxHandle.Entity.Position;
            float4 inverseBoxOrientation = new float4(
                -boxHandle.Entity.Orientation.X,
                -boxHandle.Entity.Orientation.Y,
                -boxHandle.Entity.Orientation.Z,
                boxHandle.Entity.Orientation.W);
            float3 localSphereOffset = float4.RotateVector(sphereHandle.Entity.Position - boxCenter, inverseBoxOrientation);
            float3 halfExtents = ResolveWorldBoxHalfExtents(boxHandle);
            float clampedX = Math.Clamp(localSphereOffset.X, -halfExtents.X, halfExtents.X);
            float clampedY = Math.Clamp(localSphereOffset.Y, -halfExtents.Y, halfExtents.Y);
            float clampedZ = Math.Clamp(localSphereOffset.Z, -halfExtents.Z, halfExtents.Z);
            float3 closestPoint = new float3(clampedX, clampedY, clampedZ);
            float3 delta = localSphereOffset - closestPoint;
            float radius = ResolveWorldSphereRadius(sphereHandle);
            return delta.LengthSquared() <= radius * radius;
        }

        /// <summary>
        /// Resolves one collider from the supplied runtime handle.
        /// </summary>
        static Collider3DComponent GetRequiredCollider(BepuBodyHandle3D handle) {
            if (handle == null) {
                throw new ArgumentNullException(nameof(handle));
            }
            if (handle.BoxCollider != null) {
                return handle.BoxCollider;
            }
            if (handle.SphereCollider != null) {
                return handle.SphereCollider;
            }
            if (handle.StaticMeshCollider != null) {
                return handle.StaticMeshCollider;
            }

            throw new InvalidOperationException("BEPU trigger inspection requires one supported collider.");
        }

        /// <summary>
        /// Resolves one sphere collider's effective world-space radius.
        /// </summary>
        static float ResolveWorldSphereRadius(BepuBodyHandle3D handle) {
            float3 scale = handle.Entity.Scale;
            float scaleFactor = Math.Max(scale.X, Math.Max(scale.Y, scale.Z));
            return handle.SphereCollider.Radius * scaleFactor;
        }

        /// <summary>
        /// Resolves one box collider's effective world-space half extents.
        /// </summary>
        static float3 ResolveWorldBoxHalfExtents(BepuBodyHandle3D handle) {
            float3 scale = handle.Entity.Scale;
            float3 scaledSize = new float3(
                handle.BoxCollider.Size.X * scale.X,
                handle.BoxCollider.Size.Y * scale.Y,
                handle.BoxCollider.Size.Z * scale.Z);
            return scaledSize * 0.5f;
        }

        /// <summary>
        /// Adds one trigger pair to the current step list when it has not already been tracked.
        /// </summary>
        void AddCurrentTriggerPair(TriggerPairKey3D pairKey) {
            if (ContainsTriggerPair(CurrentTriggerPairsValue, pairKey)) {
                return;
            }

            CurrentTriggerPairsValue.Add(pairKey);
        }

        /// <summary>
        /// Finalizes trigger overlap lifecycle events by comparing the current step pair set against the previously active set.
        /// </summary>
        void FinalizeTriggerEvents() {
            for (int index = 0; index < CurrentTriggerPairsValue.Count; index++) {
                TriggerPairKey3D pairKey = CurrentTriggerPairsValue[index];
                if (ContainsTriggerPair(ActiveTriggerPairsValue, pairKey)) {
                    TriggerEventsValue.Add(new TriggerEvent3D(TriggerEventKind3D.Stay, pairKey.TriggerEntity, pairKey.OtherEntity));
                } else {
                    TriggerEventsValue.Add(new TriggerEvent3D(TriggerEventKind3D.Enter, pairKey.TriggerEntity, pairKey.OtherEntity));
                }
            }

            for (int index = 0; index < ActiveTriggerPairsValue.Count; index++) {
                TriggerPairKey3D pairKey = ActiveTriggerPairsValue[index];
                if (!ContainsTriggerPair(CurrentTriggerPairsValue, pairKey)) {
                    TriggerEventsValue.Add(new TriggerEvent3D(TriggerEventKind3D.Exit, pairKey.TriggerEntity, pairKey.OtherEntity));
                }
            }

            ActiveTriggerPairsValue.Clear();
            for (int index = 0; index < CurrentTriggerPairsValue.Count; index++) {
                ActiveTriggerPairsValue.Add(CurrentTriggerPairsValue[index]);
            }
        }

        /// <summary>
        /// Returns whether one tracked trigger-pair list already contains the supplied pair key.
        /// </summary>
        static bool ContainsTriggerPair(IReadOnlyList<TriggerPairKey3D> pairs, TriggerPairKey3D pairKey) {
            for (int index = 0; index < pairs.Count; index++) {
                if (pairs[index].Equals(pairKey)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves the authored gravity acceleration for one body.
        /// </summary>
        /// <param name="rigidBody">Authored rigid body.</param>
        /// <returns>World-space Y acceleration that should be applied during integration.</returns>
        float ResolveGravityAcceleration(RigidBody3DComponent rigidBody) {
            if (rigidBody.BodyKind != BodyKind3D.Dynamic || !rigidBody.UseGravity) {
                return 0f;
            }

            return (float)(-9.81d * rigidBody.GravityScale);
        }
    }
}
