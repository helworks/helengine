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
            AngularVelocity = rigidBody.AngularVelocity;
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
            AngularVelocity = rigidBody.AngularVelocity;
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
            AngularVelocity = rigidBody.AngularVelocity;
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
        /// Gets or sets the current solver angular velocity in radians per second around each world axis.
        /// </summary>
        public float3 AngularVelocity { get; set; }

        /// <summary>
        /// Gets or sets the current inverse inertia approximation around each world axis.
        /// </summary>
        public float3 InverseInertia { get; set; }

        /// <summary>
        /// Gets or sets the current broadphase half extents used by overlap tests.
        /// </summary>
        public float3 HalfExtents { get; set; }

        /// <summary>
        /// Gets or sets the current world-axis envelope that encloses the oriented collider shape.
        /// </summary>
        public float3 AxisAlignedHalfExtents { get; set; }

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
        /// Gets or sets whether this body participated in a solid contact resolution during the current physics step.
        /// </summary>
        public bool ContactWasResolvedThisStep { get; set; }

        /// <summary>
        /// Gets or sets the strongest upward-facing contact normal observed for this body during the current physics step.
        /// </summary>
        public float MaximumContactNormalY { get; set; }

        /// <summary>
        /// Gets or sets whether this body is supported by a box contact whose footprint does not contain this body's center.
        /// </summary>
        public bool HasUnstableSupportContactThisStep { get; set; }

        /// <summary>
        /// Gets or sets whether this body is supported by a box contact whose footprint contains this body's center.
        /// </summary>
        public bool HasStableSupportContactThisStep { get; set; }

        /// <summary>
        /// Gets or sets the largest horizontal distance from the body center to a normal contact point during the current step.
        /// </summary>
        public float MaximumNormalContactLeverArmXZ { get; set; }

        /// <summary>
        /// Rebuilds the solver state from the current authored entity and component values.
        /// </summary>
        public void SynchronizeFromEntity() {
            Position = Entity.LocalPosition;
            Orientation = Entity.LocalOrientation;
            SynchronizeShapeFromEntity();
            if (RigidBody.BodyKind != BodyKind3D.Dynamic) {
                Velocity = RigidBody.LinearVelocity;
                AngularVelocity = RigidBody.AngularVelocity;
            }
        }

        /// <summary>
        /// Pushes the current solver state back into the authored entity and rigid body.
        /// </summary>
        public void SynchronizeToEntity() {
            Entity.LocalPosition = Position;
            Entity.LocalOrientation = Orientation;
            RigidBody.LinearVelocity = Velocity;
            RigidBody.AngularVelocity = AngularVelocity;
        }

        /// <summary>
        /// Applies a rotational impulse around this body's center of mass.
        /// </summary>
        /// <param name="worldImpulse">World-space impulse applied at the contact point.</param>
        /// <param name="worldPoint">World-space point where the impulse is applied.</param>
        public void ApplyAngularImpulseAtPoint(float3 worldImpulse, float3 worldPoint) {
            if (RigidBody.BodyKind != BodyKind3D.Dynamic) {
                return;
            }
            if (InverseInertia == float3.Zero) {
                return;
            }

            float3 radius = worldPoint - Position;
            float3 angularImpulse = float3.Cross(radius, worldImpulse);
            AngularVelocity = AngularVelocity + ResolveAngularVelocityDelta(angularImpulse);
        }

        /// <summary>
        /// Converts a world-space angular impulse into the angular velocity delta produced by this body's oriented inverse inertia.
        /// </summary>
        /// <param name="angularImpulse">World-space angular impulse around the body's center of mass.</param>
        /// <returns>World-space angular velocity delta created by the impulse.</returns>
        public float3 ResolveAngularVelocityDelta(float3 angularImpulse) {
            if (RigidBody.BodyKind != BodyKind3D.Dynamic) {
                return float3.Zero;
            }
            if (InverseInertia == float3.Zero) {
                return float3.Zero;
            }

            float4 inverseOrientation = float4.Inverse(Orientation);
            float3 localAngularImpulse = float4.RotateVector(angularImpulse, inverseOrientation);
            float3 localAngularVelocityDelta = new float3(
                localAngularImpulse.X * InverseInertia.X,
                localAngularImpulse.Y * InverseInertia.Y,
                localAngularImpulse.Z * InverseInertia.Z);
            return float4.RotateVector(localAngularVelocityDelta, Orientation);
        }

        /// <summary>
        /// Rebuilds derived bounds that depend on the current solver orientation.
        /// </summary>
        public void RefreshDerivedShapeState() {
            if (ColliderShapeKind == ColliderShapeKind3D.Box) {
                AxisAlignedHalfExtents = CreateBoxAxisAlignedHalfExtents(HalfExtents, Orientation);
                return;
            }

            AxisAlignedHalfExtents = HalfExtents;
        }

        /// <summary>
        /// Finds the furthest world-space point on this body in the supplied direction.
        /// </summary>
        /// <param name="direction">World-space direction used to choose the support point.</param>
        /// <returns>World-space support point on the collider approximation.</returns>
        public float3 GetSupportPoint(float3 direction) {
            if (ColliderShapeKind == ColliderShapeKind3D.Box) {
                return GetBoxSupportPoint(direction);
            }

            return Position + new float3(
                ResolveSignedExtent(direction.X, HalfExtents.X),
                ResolveSignedExtent(direction.Y, HalfExtents.Y),
                ResolveSignedExtent(direction.Z, HalfExtents.Z));
        }

        /// <summary>
        /// Rebuilds the bound shape representation from the current entity scale and authored collider values.
        /// </summary>
        void SynchronizeShapeFromEntity() {
            if (ColliderShapeKind == ColliderShapeKind3D.Box) {
                HalfExtents = CreateBoxHalfExtents(BoxCollider.Size, Entity.LocalScale);
                SphereRadius = 0f;
                CapsuleSegmentHalfLength = 0f;
                InverseInertia = CreateBoxInverseInertia(HalfExtents, RigidBody.Mass, RigidBody.BodyKind);
                RefreshDerivedShapeState();
                return;
            }
            if (ColliderShapeKind == ColliderShapeKind3D.Sphere) {
                SphereRadius = CreateScaledSphereRadius(SphereCollider.Radius, Entity.LocalScale);
                HalfExtents = new float3(SphereRadius, SphereRadius, SphereRadius);
                CapsuleSegmentHalfLength = 0f;
                InverseInertia = float3.Zero;
                RefreshDerivedShapeState();
                return;
            }

            SphereRadius = CreateScaledSphereRadius(CapsuleCollider.Radius, Entity.LocalScale);
            CapsuleSegmentHalfLength = CreateCapsuleSegmentHalfLength(CapsuleCollider.Height, CapsuleCollider.Radius, Entity.LocalScale);
            HalfExtents = new float3(SphereRadius, CapsuleSegmentHalfLength + SphereRadius, SphereRadius);
            InverseInertia = float3.Zero;
            RefreshDerivedShapeState();
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
        /// Builds the world-axis envelope required to conservatively test an oriented box in axis-aligned broadphase paths.
        /// </summary>
        /// <param name="halfExtents">Local box half extents.</param>
        /// <param name="orientation">Current world orientation.</param>
        /// <returns>Axis-aligned half extents enclosing the oriented box.</returns>
        static float3 CreateBoxAxisAlignedHalfExtents(float3 halfExtents, float4 orientation) {
            float3 axisX = float4.RotateVector(new float3(1f, 0f, 0f), orientation);
            float3 axisY = float4.RotateVector(new float3(0f, 1f, 0f), orientation);
            float3 axisZ = float4.RotateVector(new float3(0f, 0f, 1f), orientation);
            return new float3(
                (Math.Abs(axisX.X) * halfExtents.X) + (Math.Abs(axisY.X) * halfExtents.Y) + (Math.Abs(axisZ.X) * halfExtents.Z),
                (Math.Abs(axisX.Y) * halfExtents.X) + (Math.Abs(axisY.Y) * halfExtents.Y) + (Math.Abs(axisZ.Y) * halfExtents.Z),
                (Math.Abs(axisX.Z) * halfExtents.X) + (Math.Abs(axisY.Z) * halfExtents.Y) + (Math.Abs(axisZ.Z) * halfExtents.Z));
        }

        /// <summary>
        /// Finds the furthest world-space point on this oriented box in the supplied direction.
        /// </summary>
        /// <param name="direction">World-space direction used to choose the support corner.</param>
        /// <returns>World-space support point on the oriented box.</returns>
        float3 GetBoxSupportPoint(float3 direction) {
            float3 axisX = float4.RotateVector(new float3(1f, 0f, 0f), Orientation);
            float3 axisY = float4.RotateVector(new float3(0f, 1f, 0f), Orientation);
            float3 axisZ = float4.RotateVector(new float3(0f, 0f, 1f), Orientation);
            return Position +
                (axisX * ResolveSignedExtent(float3.Dot(axisX, direction), HalfExtents.X)) +
                (axisY * ResolveSignedExtent(float3.Dot(axisY, direction), HalfExtents.Y)) +
                (axisZ * ResolveSignedExtent(float3.Dot(axisZ, direction), HalfExtents.Z));
        }

        /// <summary>
        /// Chooses a signed extent for one support direction component.
        /// </summary>
        /// <param name="direction">Direction component or projected direction value.</param>
        /// <param name="extent">Positive half extent along the corresponding axis.</param>
        /// <returns>Positive extent when the direction is non-negative; otherwise negative extent.</returns>
        static float ResolveSignedExtent(float direction, float extent) {
            if (Math.Abs(direction) <= 0.000001f) {
                return 0f;
            }
            if (direction >= 0f) {
                return extent;
            }

            return -extent;
        }

        /// <summary>
        /// Builds one diagonal inverse inertia approximation for an axis-aligned box body.
        /// </summary>
        /// <param name="halfExtents">Current half extents of the box.</param>
        /// <param name="mass">Dynamic body mass.</param>
        /// <param name="bodyKind">Rigid body simulation kind.</param>
        /// <returns>Inverse inertia around the world X, Y, and Z axes.</returns>
        static float3 CreateBoxInverseInertia(float3 halfExtents, double mass, BodyKind3D bodyKind) {
            if (bodyKind != BodyKind3D.Dynamic) {
                return float3.Zero;
            }

            double width = Math.Abs(halfExtents.X) * 2d;
            double height = Math.Abs(halfExtents.Y) * 2d;
            double depth = Math.Abs(halfExtents.Z) * 2d;
            double inertiaX = mass * ((height * height) + (depth * depth)) / 12d;
            double inertiaY = mass * ((width * width) + (depth * depth)) / 12d;
            double inertiaZ = mass * ((width * width) + (height * height)) / 12d;
            return new float3(
                ResolveInverseInertiaComponent(inertiaX),
                ResolveInverseInertiaComponent(inertiaY),
                ResolveInverseInertiaComponent(inertiaZ));
        }

        /// <summary>
        /// Converts one inertia component into a finite inverse inertia value.
        /// </summary>
        /// <param name="inertia">Inertia component to invert.</param>
        /// <returns>Inverse inertia, or zero when the component cannot rotate safely.</returns>
        static float ResolveInverseInertiaComponent(double inertia) {
            if (double.IsNaN(inertia) || double.IsInfinity(inertia) || inertia <= 0d) {
                return 0f;
            }

            return (float)(1d / inertia);
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
