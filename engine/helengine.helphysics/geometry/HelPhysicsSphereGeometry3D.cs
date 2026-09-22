namespace helengine {
    /// <summary>
    /// Provides allocation-free bounds and inertia calculations for sphere shapes.
    /// </summary>
    public static class HelPhysicsSphereGeometry3D {
        /// <summary>
        /// Computes the world-space AABB of a sphere and expands each face by a non-negative margin.
        /// </summary>
        /// <param name="sphere">Sphere whose bounds are required.</param>
        /// <param name="position">World-space center of the sphere.</param>
        /// <param name="margin">Non-negative broadphase margin.</param>
        /// <returns>Inclusive world-space sphere bounds.</returns>
        public static HelPhysicsAabb3D ComputeWorldAabb(
            HelPhysicsSphereShape3D sphere,
            PhysicsVector3 position,
            PhysicsScalar margin) {
            if (margin < PhysicsScalar.Zero) {
                throw new ArgumentOutOfRangeException(nameof(margin), "Sphere AABB margins must be non-negative.");
            }

            PhysicsScalar extent = sphere.Radius + margin;
            PhysicsVector3 extents = new PhysicsVector3(extent, extent, extent);
            return new HelPhysicsAabb3D(position - extents, position + extents);
        }

        /// <summary>
        /// Computes the reciprocal isotropic inertia tensor for a sphere.
        /// </summary>
        /// <param name="sphere">Sphere whose radius determines rotational inertia.</param>
        /// <param name="bodyKind">Body mode controlling whether rotational response is simulated.</param>
        /// <param name="mass">Strictly positive dynamic mass.</param>
        /// <returns>A diagonal inverse inertia tensor or zero for immovable bodies.</returns>
        public static PhysicsMatrix3x3 ComputeLocalInverseInertia(
            HelPhysicsSphereShape3D sphere,
            BodyKind3D bodyKind,
            PhysicsScalar mass) {
            if (bodyKind != BodyKind3D.Dynamic) {
                return PhysicsMatrix3x3.CreateDiagonal(PhysicsVector3.Zero);
            }

            if (mass <= PhysicsScalar.Zero) {
                throw new ArgumentOutOfRangeException(nameof(mass), "Dynamic sphere mass must be strictly positive.");
            }

            double radius = sphere.Radius.ToFloat();
            double inverseInertia = 2.5d / (mass.ToFloat() * radius * radius);
            PhysicsScalar value = PhysicsScalar.FromFloat((float)inverseInertia);
            return PhysicsMatrix3x3.CreateDiagonal(new PhysicsVector3(value, value, value));
        }
    }
}
