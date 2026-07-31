namespace helengine {
    /// <summary>
    /// Provides allocation-free transforms, bounds, and inertia calculations for centered box shapes.
    /// </summary>
    public static class HelPhysicsBoxGeometry3D {
        /// <summary>
        /// Computes conservative axis-aligned world bounds for an oriented box and expands each face by a non-negative margin.
        /// </summary>
        /// <param name="box">Centered box whose world bounds are required.</param>
        /// <param name="position">World-space center of the box.</param>
        /// <param name="orientation">World-space rotation of the box's local axes.</param>
        /// <param name="margin">Non-negative distance by which to expand every world-space face.</param>
        /// <returns>Inclusive conservative world bounds for the oriented box.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="margin"/> is negative.</exception>
        public static HelPhysicsAabb3D ComputeWorldAabb(
            HelPhysicsBoxShape3D box,
            PhysicsVector3 position,
            PhysicsQuaternion orientation,
            PhysicsScalar margin) {
            if (margin < PhysicsScalar.Zero) {
                throw new ArgumentOutOfRangeException(nameof(margin), "Box AABB margins must be non-negative.");
            }

            PhysicsMatrix3x3 rotation = PhysicsMatrix3x3.CreateFromQuaternion(orientation);
            PhysicsVector3 halfExtents = box.HalfExtents;
            PhysicsVector3 worldExtents = new PhysicsVector3(
                (PhysicsScalar.Abs(rotation.Row0.X) * halfExtents.X) + (PhysicsScalar.Abs(rotation.Row0.Y) * halfExtents.Y) + (PhysicsScalar.Abs(rotation.Row0.Z) * halfExtents.Z) + margin,
                (PhysicsScalar.Abs(rotation.Row1.X) * halfExtents.X) + (PhysicsScalar.Abs(rotation.Row1.Y) * halfExtents.Y) + (PhysicsScalar.Abs(rotation.Row1.Z) * halfExtents.Z) + margin,
                (PhysicsScalar.Abs(rotation.Row2.X) * halfExtents.X) + (PhysicsScalar.Abs(rotation.Row2.Y) * halfExtents.Y) + (PhysicsScalar.Abs(rotation.Row2.Z) * halfExtents.Z) + margin);

            return new HelPhysicsAabb3D(position - worldExtents, position + worldExtents);
        }

        /// <summary>
        /// Computes the reciprocal local inertia tensor for a box of the supplied mass and body kind.
        /// </summary>
        /// <param name="box">Centered box whose dimensions determine its rotational inertia.</param>
        /// <param name="bodyKind">Body behavior that determines whether rotational response is simulated.</param>
        /// <param name="mass">Strictly positive dynamic body mass.</param>
        /// <returns>A diagonal local inverse inertia tensor, or a zero matrix for static and kinematic bodies.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a dynamic body has zero or negative mass.</exception>
        public static PhysicsMatrix3x3 ComputeLocalInverseInertia(
            HelPhysicsBoxShape3D box,
            BodyKind3D bodyKind,
            PhysicsScalar mass) {
            if (bodyKind != BodyKind3D.Dynamic) {
                return PhysicsMatrix3x3.CreateDiagonal(PhysicsVector3.Zero);
            }

            if (mass <= PhysicsScalar.Zero) {
                throw new ArgumentOutOfRangeException(nameof(mass), "Dynamic box mass must be strictly positive.");
            }

            double width = (double)box.HalfExtents.X.ToFloat() * 2d;
            double height = (double)box.HalfExtents.Y.ToFloat() * 2d;
            double depth = (double)box.HalfExtents.Z.ToFloat() * 2d;
            double massValue = mass.ToFloat();
            double inertiaX = massValue * ((height * height) + (depth * depth)) / 12d;
            double inertiaY = massValue * ((width * width) + (depth * depth)) / 12d;
            double inertiaZ = massValue * ((width * width) + (height * height)) / 12d;

            return PhysicsMatrix3x3.CreateDiagonal(new PhysicsVector3(
                PhysicsScalar.FromFloat((float)(1d / inertiaX)),
                PhysicsScalar.FromFloat((float)(1d / inertiaY)),
                PhysicsScalar.FromFloat((float)(1d / inertiaZ))));
        }

        /// <summary>
        /// Gets one normalized world-space direction corresponding to a local box axis.
        /// </summary>
        /// <param name="orientation">World-space rotation of the box's local axes.</param>
        /// <param name="axisIndex">Local axis index: zero for X, one for Y, or two for Z.</param>
        /// <returns>The selected local axis transformed into world space.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="axisIndex"/> does not identify a local box axis.</exception>
        public static PhysicsVector3 GetWorldAxis(PhysicsQuaternion orientation, int axisIndex) {
            if (axisIndex == 0) {
                return orientation.Rotate(PhysicsVector3.UnitX);
            } else if (axisIndex == 1) {
                return orientation.Rotate(PhysicsVector3.UnitY);
            } else if (axisIndex == 2) {
                return orientation.Rotate(PhysicsVector3.UnitZ);
            }

            throw new ArgumentOutOfRangeException(nameof(axisIndex), "Box axes are indexed from zero through two.");
        }

        /// <summary>
        /// Gets one world-space corner of an oriented box using the conventional three-bit local vertex index.
        /// </summary>
        /// <param name="box">Centered box whose corner is required.</param>
        /// <param name="position">World-space center of the box.</param>
        /// <param name="orientation">World-space rotation of the box's local axes.</param>
        /// <param name="vertexIndex">Corner index from zero through seven, with bits selecting positive X, Y, and Z signs.</param>
        /// <returns>The selected transformed box corner in world space.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="vertexIndex"/> does not identify a box corner.</exception>
        public static PhysicsVector3 GetWorldVertex(
            HelPhysicsBoxShape3D box,
            PhysicsVector3 position,
            PhysicsQuaternion orientation,
            int vertexIndex) {
            if (vertexIndex < 0 || vertexIndex > 7) {
                throw new ArgumentOutOfRangeException(nameof(vertexIndex), "Box vertices are indexed from zero through seven.");
            }

            PhysicsVector3 halfExtents = box.HalfExtents;
            PhysicsScalar x = (vertexIndex & 1) == 0 ? -halfExtents.X : halfExtents.X;
            PhysicsScalar y = (vertexIndex & 2) == 0 ? -halfExtents.Y : halfExtents.Y;
            PhysicsScalar z = (vertexIndex & 4) == 0 ? -halfExtents.Z : halfExtents.Z;

            return position + orientation.Rotate(new PhysicsVector3(x, y, z));
        }
    }
}
