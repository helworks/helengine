namespace helengine {
    /// <summary>
    /// Stores the dense runtime state tracked for one simulated 3D body.
    /// </summary>
    public sealed class BodyState3D {
        /// <summary>
        /// Initializes a new runtime body state for one entity-backed rigid body that uses a box collider.
        /// </summary>
        /// <param name="entity">Entity whose transform is synchronized by the body state.</param>
        /// <param name="rigidBody">Authored rigid body component that owns the body settings.</param>
        /// <param name="boxCollider">Authored box collider component that defines the body bounds.</param>
        /// <param name="kinematicMotionComponent">Optional kinematic motion path component.</param>
        public BodyState3D(Entity entity, RigidBody3DComponent rigidBody, BoxCollider3DComponent boxCollider, KinematicMotion3DComponent kinematicMotionComponent) {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            RigidBody = rigidBody ?? throw new ArgumentNullException(nameof(rigidBody));
            Collider = boxCollider ?? throw new ArgumentNullException(nameof(boxCollider));
            BoxCollider = boxCollider ?? throw new ArgumentNullException(nameof(boxCollider));
            KinematicMotionComponent = kinematicMotionComponent;
            ColliderShapeKind = ColliderShapeKind3D.Box;
            Position = entity.LocalPosition;
            Orientation = entity.LocalOrientation;
            Velocity = rigidBody.LinearVelocity;
            SynchronizeShapeFromEntity();
        }

        /// <summary>
        /// Initializes a new runtime body state for one entity-backed rigid body that uses a sphere collider.
        /// </summary>
        /// <param name="entity">Entity whose transform is synchronized by the body state.</param>
        /// <param name="rigidBody">Authored rigid body component that owns the body settings.</param>
        /// <param name="sphereCollider">Authored sphere collider component that defines the body bounds.</param>
        /// <param name="kinematicMotionComponent">Optional kinematic motion path component.</param>
        public BodyState3D(Entity entity, RigidBody3DComponent rigidBody, SphereCollider3DComponent sphereCollider, KinematicMotion3DComponent kinematicMotionComponent) {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            RigidBody = rigidBody ?? throw new ArgumentNullException(nameof(rigidBody));
            Collider = sphereCollider ?? throw new ArgumentNullException(nameof(sphereCollider));
            SphereCollider = sphereCollider ?? throw new ArgumentNullException(nameof(sphereCollider));
            KinematicMotionComponent = kinematicMotionComponent;
            ColliderShapeKind = ColliderShapeKind3D.Sphere;
            Position = entity.LocalPosition;
            Orientation = entity.LocalOrientation;
            Velocity = rigidBody.LinearVelocity;
            SynchronizeShapeFromEntity();
        }

        /// <summary>
        /// Initializes a new runtime body state for one entity-backed rigid body that uses a vertically aligned capsule collider.
        /// </summary>
        /// <param name="entity">Entity whose transform is synchronized by the body state.</param>
        /// <param name="rigidBody">Authored rigid body component that owns the body settings.</param>
        /// <param name="capsuleCollider">Authored capsule collider component that defines the body bounds.</param>
        /// <param name="kinematicMotionComponent">Optional kinematic motion path component.</param>
        public BodyState3D(Entity entity, RigidBody3DComponent rigidBody, CapsuleCollider3DComponent capsuleCollider, KinematicMotion3DComponent kinematicMotionComponent) {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            RigidBody = rigidBody ?? throw new ArgumentNullException(nameof(rigidBody));
            Collider = capsuleCollider ?? throw new ArgumentNullException(nameof(capsuleCollider));
            CapsuleCollider = capsuleCollider ?? throw new ArgumentNullException(nameof(capsuleCollider));
            KinematicMotionComponent = kinematicMotionComponent;
            ColliderShapeKind = ColliderShapeKind3D.Capsule;
            Position = entity.LocalPosition;
            Orientation = entity.LocalOrientation;
            Velocity = rigidBody.LinearVelocity;
            SynchronizeShapeFromEntity();
        }

        /// <summary>
        /// Gets the entity synchronized by the runtime body state.
        /// </summary>
        public Entity Entity { get; }

        /// <summary>
        /// Gets the authored rigid body component consumed by the runtime body state.
        /// </summary>
        public RigidBody3DComponent RigidBody { get; }

        /// <summary>
        /// Gets the authored collider component consumed by the runtime body state.
        /// </summary>
        public Collider3DComponent Collider { get; }

        /// <summary>
        /// Gets the authored box collider consumed by the runtime body state, when the bound shape is a box.
        /// </summary>
        public BoxCollider3DComponent BoxCollider { get; }

        /// <summary>
        /// Gets the authored sphere collider consumed by the runtime body state, when the bound shape is a sphere.
        /// </summary>
        public SphereCollider3DComponent SphereCollider { get; }

        /// <summary>
        /// Gets the authored capsule collider consumed by the runtime body state, when the bound shape is a capsule.
        /// </summary>
        public CapsuleCollider3DComponent CapsuleCollider { get; }

        /// <summary>
        /// Gets the authored kinematic motion component consumed by the runtime body state, when present.
        /// </summary>
        public KinematicMotion3DComponent KinematicMotionComponent { get; }

        /// <summary>
        /// Gets the collider shape kind bound to this runtime body state.
        /// </summary>
        public ColliderShapeKind3D ColliderShapeKind { get; }

        /// <summary>
        /// Gets or sets the current solver position.
        /// </summary>
        public float3 Position { get; set; }

        /// <summary>
        /// Gets or sets the current solver orientation.
        /// </summary>
        public float4 Orientation { get; set; }

        /// <summary>
        /// Gets or sets the current solver velocity.
        /// </summary>
        public float3 Velocity { get; set; }

        /// <summary>
        /// Gets or sets the current broadphase half extents used by overlap tests.
        /// </summary>
        public float3 HalfExtents { get; set; }

        /// <summary>
        /// Gets or sets the current scaled sphere radius when the bound shape is a sphere or capsule.
        /// </summary>
        public float SphereRadius { get; set; }

        /// <summary>
        /// Gets or sets half of the current vertical capsule line segment length when the bound shape is a capsule.
        /// </summary>
        public float CapsuleSegmentHalfLength { get; set; }

        /// <summary>
        /// Gets or sets the accumulated motion time used by runtime-driven kinematic paths.
        /// </summary>
        public double KinematicMotionElapsedSeconds { get; set; }

        /// <summary>
        /// Rebuilds the solver state from the current authored entity and component values.
        /// </summary>
        public void SynchronizeFromEntity() {
            Position = Entity.LocalPosition;
            Orientation = Entity.LocalOrientation;
            SynchronizeShapeFromEntity();
            if (RigidBody.BodyKind != BodyKind3D.Dynamic) {
                Velocity = RigidBody.LinearVelocity;
            }
        }

        /// <summary>
        /// Pushes the current solver state back into the authored entity and rigid body.
        /// </summary>
        public void SynchronizeToEntity() {
            Entity.LocalPosition = Position;
            Entity.LocalOrientation = Orientation;
            RigidBody.LinearVelocity = Velocity;
        }

        /// <summary>
        /// Rebuilds the bound shape representation from the current entity scale and authored collider values.
        /// </summary>
        void SynchronizeShapeFromEntity() {
            if (ColliderShapeKind == ColliderShapeKind3D.Box) {
                HalfExtents = CreateBoxHalfExtents(BoxCollider.Size, Entity.LocalScale);
                SphereRadius = 0f;
                CapsuleSegmentHalfLength = 0f;
                return;
            }
            if (ColliderShapeKind == ColliderShapeKind3D.Sphere) {
                SphereRadius = CreateScaledSphereRadius(SphereCollider.Radius, Entity.LocalScale);
                HalfExtents = new float3(SphereRadius, SphereRadius, SphereRadius);
                CapsuleSegmentHalfLength = 0f;
                return;
            }

            SphereRadius = CreateScaledSphereRadius(CapsuleCollider.Radius, Entity.LocalScale);
            CapsuleSegmentHalfLength = CreateCapsuleSegmentHalfLength(CapsuleCollider.Height, CapsuleCollider.Radius, Entity.LocalScale);
            HalfExtents = new float3(SphereRadius, CapsuleSegmentHalfLength + SphereRadius, SphereRadius);
        }

        /// <summary>
        /// Builds half extents from one authored box size and entity scale.
        /// </summary>
        /// <param name="size">Full authored box size.</param>
        /// <param name="scale">Current entity scale.</param>
        /// <returns>Axis-aligned half extents used by the solver.</returns>
        static float3 CreateBoxHalfExtents(float3 size, float3 scale) {
            return new float3(
                Math.Abs(size.X * scale.X) * 0.5f,
                Math.Abs(size.Y * scale.Y) * 0.5f,
                Math.Abs(size.Z * scale.Z) * 0.5f);
        }

        /// <summary>
        /// Builds one conservative scaled sphere radius from the authored radius and entity scale.
        /// </summary>
        /// <param name="radius">Authored sphere radius.</param>
        /// <param name="scale">Current entity scale.</param>
        /// <returns>Scaled sphere radius used by the solver.</returns>
        static float CreateScaledSphereRadius(float radius, float3 scale) {
            float maximumScale = Math.Max(Math.Abs(scale.X), Math.Max(Math.Abs(scale.Y), Math.Abs(scale.Z)));
            return radius * maximumScale;
        }

        /// <summary>
        /// Builds half of the scaled capsule line-segment length from the authored full height and radius.
        /// </summary>
        /// <param name="height">Authored full capsule height.</param>
        /// <param name="radius">Authored capsule radius.</param>
        /// <param name="scale">Current entity scale.</param>
        /// <returns>Scaled half segment length used by the solver.</returns>
        static float CreateCapsuleSegmentHalfLength(float height, float radius, float3 scale) {
            float verticalScale = Math.Abs(scale.Y);
            float scaledHeight = height * verticalScale;
            float scaledRadius = CreateScaledSphereRadius(radius, scale);
            float cylinderHeight = Math.Max(0f, scaledHeight - (scaledRadius * 2f));
            return cylinderHeight * 0.5f;
        }
    }
}
