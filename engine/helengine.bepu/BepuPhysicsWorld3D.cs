using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using BepuUtilities.Memory;

namespace helengine {
    /// <summary>
    /// Hosts the real BEPU simulation used by supported Helengine 3D rigid-body scenes.
    /// </summary>
    public sealed class BepuPhysicsWorld3D : ISceneBindablePhysicsRuntime {
        /// <summary>
        /// Default contact spring settings used for the initial BEPU integration pass.
        /// </summary>
        static readonly SpringSettings DefaultContactSpringSettings = new SpringSettings(30f, 1f);

        /// <summary>
        /// Shared memory pool used by the active BEPU simulation.
        /// </summary>
        readonly BufferPool BufferPoolValue;

        /// <summary>
        /// Runtime registry containing every bound rigid body supported by the BEPU adapter.
        /// </summary>
        readonly BepuBodyRegistry3D BodyRegistryValue;

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
            BufferPoolValue = new BufferPool();
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
        /// Gets the number of rigid bodies currently registered in the bound scene.
        /// </summary>
        public int RegisteredBodyCount => BodyRegistryValue.Handles.Count;

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

            SimulationValue.Timestep((float)stepSeconds);
            SynchronizeBodiesBackToEntities();
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
                new SolveDescription(4, 1));
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
            if (boxCollider == null && sphereCollider == null) {
                return;
            }
            if (boxCollider != null && sphereCollider != null) {
                throw new NotSupportedException("Entities with both box and sphere colliders are not supported by helengine.bepu.");
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
