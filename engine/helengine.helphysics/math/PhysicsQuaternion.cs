namespace helengine {
    /// <summary>
    /// Represents a quaternion rotation expressed with dedicated physics scalar components.
    /// </summary>
    public readonly struct PhysicsQuaternion {
        /// <summary>
        /// Stores the imaginary quaternion component along the X axis.
        /// </summary>
        public readonly PhysicsScalar X;

        /// <summary>
        /// Stores the imaginary quaternion component along the Y axis.
        /// </summary>
        public readonly PhysicsScalar Y;

        /// <summary>
        /// Stores the imaginary quaternion component along the Z axis.
        /// </summary>
        public readonly PhysicsScalar Z;

        /// <summary>
        /// Stores the real quaternion component.
        /// </summary>
        public readonly PhysicsScalar W;

        /// <summary>
        /// Initializes a quaternion from its imaginary vector and real scalar components.
        /// </summary>
        /// <param name="x">Imaginary component along the X axis.</param>
        /// <param name="y">Imaginary component along the Y axis.</param>
        /// <param name="z">Imaginary component along the Z axis.</param>
        /// <param name="w">Real component.</param>
        public PhysicsQuaternion(PhysicsScalar x, PhysicsScalar y, PhysicsScalar z, PhysicsScalar w) {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        /// <summary>
        /// Gets the quaternion that leaves every vector unchanged.
        /// </summary>
        public static PhysicsQuaternion Identity => new PhysicsQuaternion(PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.One);

        /// <summary>
        /// Creates a unit quaternion from a rotation axis and angle measured in radians.
        /// </summary>
        /// <param name="axis">Non-zero axis around which to rotate.</param>
        /// <param name="angle">Rotation angle in radians.</param>
        /// <returns>A normalized quaternion representing the requested rotation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="axis"/> has zero length.</exception>
        public static PhysicsQuaternion CreateFromAxisAngle(PhysicsVector3 axis, PhysicsScalar angle) {
            PhysicsVector3 normalizedAxis = axis.Normalized();
            PhysicsScalar halfAngle = angle * PhysicsScalar.FromFloat(0.5f);
            PhysicsScalar sine = PhysicsMath.Sin(halfAngle);

            return new PhysicsQuaternion(
                normalizedAxis.X * sine,
                normalizedAxis.Y * sine,
                normalizedAxis.Z * sine,
                PhysicsMath.Cos(halfAngle));
        }

        /// <summary>
        /// Creates a unit quaternion with the same rotation as this quaternion.
        /// </summary>
        /// <returns>A normalized quaternion.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when this quaternion has zero length.</exception>
        public PhysicsQuaternion Normalized() {
            PhysicsScalar lengthSquared = (X * X) + (Y * Y) + (Z * Z) + (W * W);
            PhysicsScalar inverseLength = PhysicsScalar.ReciprocalSqrt(lengthSquared);

            return new PhysicsQuaternion(X * inverseLength, Y * inverseLength, Z * inverseLength, W * inverseLength);
        }

        /// <summary>
        /// Returns the quaternion inverse for a normalized quaternion.
        /// </summary>
        /// <returns>The conjugate quaternion.</returns>
        public PhysicsQuaternion Conjugated() {
            return new PhysicsQuaternion(-X, -Y, -Z, W);
        }

        /// <summary>
        /// Rotates a vector by this quaternion.
        /// </summary>
        /// <param name="vector">Vector to rotate.</param>
        /// <returns>The rotated vector.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when this quaternion has zero length.</exception>
        public PhysicsVector3 Rotate(PhysicsVector3 vector) {
            PhysicsQuaternion normalized = Normalized();
            PhysicsVector3 imaginary = new PhysicsVector3(normalized.X, normalized.Y, normalized.Z);
            PhysicsVector3 doubledCross = PhysicsVector3.Cross(imaginary, vector) * PhysicsScalar.FromFloat(2f);

            return vector + (doubledCross * normalized.W) + PhysicsVector3.Cross(imaginary, doubledCross);
        }

        /// <summary>
        /// Composes two quaternion rotations.
        /// </summary>
        /// <param name="left">Rotation applied second.</param>
        /// <param name="right">Rotation applied first.</param>
        /// <returns>The composed quaternion rotation.</returns>
        public static PhysicsQuaternion operator *(PhysicsQuaternion left, PhysicsQuaternion right) {
            return new PhysicsQuaternion(
                (left.W * right.X) + (left.X * right.W) + (left.Y * right.Z) - (left.Z * right.Y),
                (left.W * right.Y) - (left.X * right.Z) + (left.Y * right.W) + (left.Z * right.X),
                (left.W * right.Z) + (left.X * right.Y) - (left.Y * right.X) + (left.Z * right.W),
                (left.W * right.W) - (left.X * right.X) - (left.Y * right.Y) - (left.Z * right.Z));
        }
    }
}
