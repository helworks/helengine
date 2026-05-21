namespace helengine {
    /// <summary>
    /// Stores the compact runtime state required to simulate one box-shaped 3D body.
    /// </summary>
    public sealed class CubeBodyState3D {
        /// <summary>
        /// Initializes one entity-backed cube body state.
        /// </summary>
        /// <param name="entity">Entity whose local transform is synchronized by the solver.</param>
        /// <param name="rigidBody">Rigid body component that defines mass, velocities, and body kind.</param>
        /// <param name="boxCollider">Box collider component that defines the cube dimensions.</param>
        /// <param name="kinematicMotionComponent">Optional kinematic motion path used by kinematic boxes.</param>
        public CubeBodyState3D(Entity entity, RigidBody3DComponent rigidBody, BoxCollider3DComponent boxCollider, KinematicMotion3DComponent kinematicMotionComponent) {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            RigidBody = rigidBody ?? throw new ArgumentNullException(nameof(rigidBody));
            Collider = boxCollider ?? throw new ArgumentNullException(nameof(boxCollider));
            KinematicMotionComponent = kinematicMotionComponent;
            Position = entity.LocalPosition;
            Orientation = entity.LocalOrientation;
            Velocity = rigidBody.LinearVelocity;
            AngularVelocity = rigidBody.AngularVelocity;
            RefreshShapeFromEntity();
        }

        /// <summary>
        /// Gets the entity synchronized by this cube body state.
        /// </summary>
        public Entity Entity { get; }

        /// <summary>
        /// Gets the authored rigid body consumed by the solver.
        /// </summary>
        public RigidBody3DComponent RigidBody { get; }

        /// <summary>
        /// Gets the authored box collider consumed by the solver.
        /// </summary>
        public BoxCollider3DComponent Collider { get; }

        /// <summary>
        /// Gets the optional authored kinematic motion path.
        /// </summary>
        public KinematicMotion3DComponent KinematicMotionComponent { get; }

        /// <summary>
        /// Gets or sets the current center of mass position in scene-local space.
        /// </summary>
        public float3 Position { get; set; }

        /// <summary>
        /// Gets or sets the current body orientation.
        /// </summary>
        public float4 Orientation { get; set; }

        /// <summary>
        /// Gets or sets the current linear velocity in world units per second.
        /// </summary>
        public float3 Velocity { get; set; }

        /// <summary>
        /// Gets or sets the current angular velocity in radians per second.
        /// </summary>
        public float3 AngularVelocity { get; set; }

        /// <summary>
        /// Gets or sets the current local-space box half extents after entity scale is applied.
        /// </summary>
        public float3 HalfExtents { get; set; }

        /// <summary>
        /// Gets or sets the current world-axis envelope that encloses the oriented box.
        /// </summary>
        public float3 AxisAlignedHalfExtents { get; set; }

        /// <summary>
        /// Gets or sets the diagonal inverse inertia approximation around world axes.
        /// </summary>
        public float3 InverseInertia { get; set; }

        /// <summary>
        /// Gets or sets elapsed runtime seconds for the optional kinematic motion path.
        /// </summary>
        public double KinematicMotionElapsedSeconds { get; set; }

        /// <summary>
        /// Copies authored transform data into runtime state before a simulation step.
        /// </summary>
        public void SynchronizeFromEntity() {
            Position = Entity.LocalPosition;
            Orientation = Entity.LocalOrientation;
            RefreshShapeFromEntity();
            if (RigidBody.BodyKind != BodyKind3D.Dynamic) {
                Velocity = RigidBody.LinearVelocity;
                AngularVelocity = RigidBody.AngularVelocity;
            }
        }

        /// <summary>
        /// Copies runtime state back into the authored entity and rigid body after a simulation step.
        /// </summary>
        public void SynchronizeToEntity() {
            Entity.LocalPosition = Position;
            Entity.LocalOrientation = Orientation;
            RigidBody.LinearVelocity = Velocity;
            RigidBody.AngularVelocity = AngularVelocity;
        }

        /// <summary>
        /// Rebuilds derived shape state after transform, scale, or collider changes.
        /// </summary>
        public void RefreshShapeFromEntity() {
            HalfExtents = new float3(
                Math.Abs(Collider.Size.X * Entity.LocalScale.X) * 0.5f,
                Math.Abs(Collider.Size.Y * Entity.LocalScale.Y) * 0.5f,
                Math.Abs(Collider.Size.Z * Entity.LocalScale.Z) * 0.5f);
            InverseInertia = CreateBoxInverseInertia(HalfExtents, RigidBody.Mass, RigidBody.BodyKind);
            RefreshDerivedShapeState();
        }

        /// <summary>
        /// Rebuilds derived bounds that depend on the current solver orientation.
        /// </summary>
        public void RefreshDerivedShapeState() {
            float3 axisX = ResolveBoxAxis(0);
            float3 axisY = ResolveBoxAxis(1);
            float3 axisZ = ResolveBoxAxis(2);
            AxisAlignedHalfExtents = new float3(
                (Math.Abs(axisX.X) * HalfExtents.X) + (Math.Abs(axisY.X) * HalfExtents.Y) + (Math.Abs(axisZ.X) * HalfExtents.Z),
                (Math.Abs(axisX.Y) * HalfExtents.X) + (Math.Abs(axisY.Y) * HalfExtents.Y) + (Math.Abs(axisZ.Y) * HalfExtents.Z),
                (Math.Abs(axisX.Z) * HalfExtents.X) + (Math.Abs(axisY.Z) * HalfExtents.Y) + (Math.Abs(axisZ.Z) * HalfExtents.Z));
        }

        /// <summary>
        /// Finds the furthest world-space point on this cube in the supplied direction.
        /// </summary>
        /// <param name="direction">World-space direction used to choose the support corner.</param>
        /// <returns>World-space support point on the oriented box.</returns>
        public float3 GetSupportPoint(float3 direction) {
            float3 axisX = ResolveBoxAxis(0);
            float3 axisY = ResolveBoxAxis(1);
            float3 axisZ = ResolveBoxAxis(2);
            return Position +
                (axisX * ResolveSignedExtent(float3.Dot(axisX, direction), HalfExtents.X)) +
                (axisY * ResolveSignedExtent(float3.Dot(axisY, direction), HalfExtents.Y)) +
                (axisZ * ResolveSignedExtent(float3.Dot(axisZ, direction), HalfExtents.Z));
        }

        /// <summary>
        /// Resolves one local box axis in world space.
        /// </summary>
        /// <param name="axisIndex">Zero for X, one for Y, two for Z.</param>
        /// <returns>Unit local axis in world space.</returns>
        public float3 ResolveBoxAxis(int axisIndex) {
            if (axisIndex == 0) {
                return float4.RotateVector(new float3(1f, 0f, 0f), Orientation);
            }
            if (axisIndex == 1) {
                return float4.RotateVector(new float3(0f, 1f, 0f), Orientation);
            }
            if (axisIndex == 2) {
                return float4.RotateVector(new float3(0f, 0f, 1f), Orientation);
            }

            throw new ArgumentOutOfRangeException(nameof(axisIndex), "Axis index must be 0, 1, or 2.");
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
    }
}
