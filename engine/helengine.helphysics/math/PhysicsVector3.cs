namespace helengine {
    /// <summary>
    /// Represents a three-component vector whose components use the dedicated physics scalar contract.
    /// </summary>
    public readonly struct PhysicsVector3 {
        /// <summary>
        /// Stores the vector component along the X axis.
        /// </summary>
        public readonly PhysicsScalar X;

        /// <summary>
        /// Stores the vector component along the Y axis.
        /// </summary>
        public readonly PhysicsScalar Y;

        /// <summary>
        /// Stores the vector component along the Z axis.
        /// </summary>
        public readonly PhysicsScalar Z;

        /// <summary>
        /// Initializes a vector from dedicated physics scalar components.
        /// </summary>
        /// <param name="x">Component along the X axis.</param>
        /// <param name="y">Component along the Y axis.</param>
        /// <param name="z">Component along the Z axis.</param>
        public PhysicsVector3(PhysicsScalar x, PhysicsScalar y, PhysicsScalar z) {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Initializes a vector from explicit engine-boundary float components.
        /// </summary>
        /// <param name="x">Finite component along the X axis.</param>
        /// <param name="y">Finite component along the Y axis.</param>
        /// <param name="z">Finite component along the Z axis.</param>
        public PhysicsVector3(float x, float y, float z) {
            X = PhysicsScalar.FromFloat(x);
            Y = PhysicsScalar.FromFloat(y);
            Z = PhysicsScalar.FromFloat(z);
        }

        /// <summary>
        /// Gets a vector with all components set to zero.
        /// </summary>
        public static PhysicsVector3 Zero => new PhysicsVector3(PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.Zero);

        /// <summary>
        /// Gets a vector with all components set to one.
        /// </summary>
        public static PhysicsVector3 One => new PhysicsVector3(PhysicsScalar.One, PhysicsScalar.One, PhysicsScalar.One);

        /// <summary>
        /// Gets the unit vector along the positive X axis.
        /// </summary>
        public static PhysicsVector3 UnitX => new PhysicsVector3(PhysicsScalar.One, PhysicsScalar.Zero, PhysicsScalar.Zero);

        /// <summary>
        /// Gets the unit vector along the positive Y axis.
        /// </summary>
        public static PhysicsVector3 UnitY => new PhysicsVector3(PhysicsScalar.Zero, PhysicsScalar.One, PhysicsScalar.Zero);

        /// <summary>
        /// Gets the unit vector along the positive Z axis.
        /// </summary>
        public static PhysicsVector3 UnitZ => new PhysicsVector3(PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.One);

        /// <summary>
        /// Computes the dot product of two vectors.
        /// </summary>
        /// <param name="left">First vector operand.</param>
        /// <param name="right">Second vector operand.</param>
        /// <returns>The scalar dot product of both vectors.</returns>
        public static PhysicsScalar Dot(PhysicsVector3 left, PhysicsVector3 right) {
            return (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);
        }

        /// <summary>
        /// Computes the right-handed cross product of two vectors.
        /// </summary>
        /// <param name="left">First vector operand.</param>
        /// <param name="right">Second vector operand.</param>
        /// <returns>A vector perpendicular to both operands using right-handed orientation.</returns>
        public static PhysicsVector3 Cross(PhysicsVector3 left, PhysicsVector3 right) {
            return new PhysicsVector3(
                (left.Y * right.Z) - (left.Z * right.Y),
                (left.Z * right.X) - (left.X * right.Z),
                (left.X * right.Y) - (left.Y * right.X));
        }

        /// <summary>
        /// Computes the squared Euclidean length of this vector.
        /// </summary>
        /// <returns>The non-negative squared length.</returns>
        public PhysicsScalar LengthSquared() {
            return Dot(this, this);
        }

        /// <summary>
        /// Computes the Euclidean length of this vector.
        /// </summary>
        /// <returns>The non-negative vector length.</returns>
        public PhysicsScalar Length() {
            return PhysicsScalar.Sqrt(LengthSquared());
        }

        /// <summary>
        /// Creates a unit-length vector with the same direction as this vector.
        /// </summary>
        /// <returns>A normalized vector.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when this vector has zero length.</exception>
        public PhysicsVector3 Normalized() {
            return this * PhysicsScalar.ReciprocalSqrt(LengthSquared());
        }

        /// <summary>
        /// Adds corresponding components of two vectors.
        /// </summary>
        /// <param name="left">Left vector operand.</param>
        /// <param name="right">Right vector operand.</param>
        /// <returns>The component-wise sum.</returns>
        public static PhysicsVector3 operator +(PhysicsVector3 left, PhysicsVector3 right) {
            return new PhysicsVector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        /// <summary>
        /// Subtracts corresponding components of two vectors.
        /// </summary>
        /// <param name="left">Vector from which to subtract.</param>
        /// <param name="right">Vector to subtract.</param>
        /// <returns>The component-wise difference.</returns>
        public static PhysicsVector3 operator -(PhysicsVector3 left, PhysicsVector3 right) {
            return new PhysicsVector3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        /// <summary>
        /// Negates every component of a vector.
        /// </summary>
        /// <param name="value">Vector to negate.</param>
        /// <returns>The additive inverse of <paramref name="value"/>.</returns>
        public static PhysicsVector3 operator -(PhysicsVector3 value) {
            return new PhysicsVector3(-value.X, -value.Y, -value.Z);
        }

        /// <summary>
        /// Multiplies corresponding components of two vectors.
        /// </summary>
        /// <param name="left">Left vector operand.</param>
        /// <param name="right">Right vector operand.</param>
        /// <returns>The component-wise product.</returns>
        public static PhysicsVector3 operator *(PhysicsVector3 left, PhysicsVector3 right) {
            return new PhysicsVector3(left.X * right.X, left.Y * right.Y, left.Z * right.Z);
        }

        /// <summary>
        /// Multiplies every vector component by a scalar.
        /// </summary>
        /// <param name="vector">Vector to scale.</param>
        /// <param name="scalar">Scalar multiplier.</param>
        /// <returns>The scaled vector.</returns>
        public static PhysicsVector3 operator *(PhysicsVector3 vector, PhysicsScalar scalar) {
            return new PhysicsVector3(vector.X * scalar, vector.Y * scalar, vector.Z * scalar);
        }

        /// <summary>
        /// Multiplies every vector component by a scalar.
        /// </summary>
        /// <param name="scalar">Scalar multiplier.</param>
        /// <param name="vector">Vector to scale.</param>
        /// <returns>The scaled vector.</returns>
        public static PhysicsVector3 operator *(PhysicsScalar scalar, PhysicsVector3 vector) {
            return vector * scalar;
        }

        /// <summary>
        /// Divides corresponding components of two vectors.
        /// </summary>
        /// <param name="left">Dividend vector.</param>
        /// <param name="right">Divisor vector with no zero components.</param>
        /// <returns>The component-wise quotient.</returns>
        public static PhysicsVector3 operator /(PhysicsVector3 left, PhysicsVector3 right) {
            return new PhysicsVector3(left.X / right.X, left.Y / right.Y, left.Z / right.Z);
        }

        /// <summary>
        /// Divides every vector component by a non-zero scalar.
        /// </summary>
        /// <param name="vector">Vector to divide.</param>
        /// <param name="scalar">Non-zero scalar divisor.</param>
        /// <returns>The scaled-down vector.</returns>
        public static PhysicsVector3 operator /(PhysicsVector3 vector, PhysicsScalar scalar) {
            return new PhysicsVector3(vector.X / scalar, vector.Y / scalar, vector.Z / scalar);
        }
    }
}
