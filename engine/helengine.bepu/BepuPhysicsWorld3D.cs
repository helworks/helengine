using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using BepuUtilities.Memory;

namespace helengine {
    /// <summary>
    /// Hosts the real BEPU simulation used by supported Helengine 3D rigid-body scenes.
    /// </summary>
    public sealed class BepuPhysicsWorld3D : ISceneBindablePhysicsRuntime, IPhysicsBodySynchronizationRuntime3D {
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

            RuntimeExecutionPhaseProbe.SetCurrentPhaseId(RuntimeExecutionPhaseProbe.BeforeBepuTimestepPhaseId);
            SimulationValue.Timestep((float)stepSeconds);
            RuntimeExecutionPhaseProbe.SetCurrentPhaseId(RuntimeExecutionPhaseProbe.AfterBepuTimestepBeforeSyncPhaseId);
            SynchronizeBodiesBackToEntities();
            RuntimeExecutionPhaseProbe.SetCurrentPhaseId(RuntimeExecutionPhaseProbe.AfterBepuSyncPhaseId);
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
        }

        /// <summary>
        /// Creates one conservative initial-allocation profile sized for Helengine's reduced box-and-sphere BEPU runtime slice.
        /// </summary>
        /// <returns>Initial simulation allocation sizes for the reduced runtime slice.</returns>
        static SimulationAllocationSizes CreateDefaultSimulationAllocationSizes() {
            return new SimulationAllocationSizes(
                bodies: 64,
                statics: 64,
                islands: 32,
                shapesPerType: 16,
                constraints: 256,
                constraintsPerTypeBatch: 64,
                constraintCountPerBodyEstimate: 4);
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
                DynamicFriction = (float)collider.DynamicFriction,
                MaximumRecoveryVelocity = (float)(2d + (collider.Restitution * 8d)),
                SpringSettings = DefaultContactSpringSettings
            };
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
