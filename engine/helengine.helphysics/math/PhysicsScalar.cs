namespace helengine {
    /// <summary>
    /// Represents one finite scalar value used exclusively by the physics simulation.
    /// </summary>
    public readonly struct PhysicsScalar : IEquatable<PhysicsScalar> {
        /// <summary>
        /// Stores the finite single-precision value represented by this scalar.
        /// </summary>
        readonly float Value;

        /// <summary>
        /// Initializes a scalar from one finite single-precision value.
        /// </summary>
        /// <param name="value">Finite value to store for physics calculations.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is not finite.</exception>
        public PhysicsScalar(float value) {
            if (float.IsNaN(value) || float.IsInfinity(value)) {
                throw new ArgumentOutOfRangeException(nameof(value), "Physics scalar values must be finite.");
            }

            Value = value;
        }

        /// <summary>
        /// Gets the additive identity for physics scalar arithmetic.
        /// </summary>
        public static PhysicsScalar Zero => new PhysicsScalar(0f);

        /// <summary>
        /// Gets the multiplicative identity for physics scalar arithmetic.
        /// </summary>
        public static PhysicsScalar One => new PhysicsScalar(1f);

        /// <summary>
        /// Creates a physics scalar from one finite engine-boundary float value.
        /// </summary>
        /// <param name="value">Finite float value to wrap.</param>
        /// <returns>A scalar containing <paramref name="value"/>.</returns>
        public static PhysicsScalar FromFloat(float value) {
            return new PhysicsScalar(value);
        }

        /// <summary>
        /// Returns the finite float value for an explicit engine-boundary conversion.
        /// </summary>
        /// <returns>The float value stored by this scalar.</returns>
        public float ToFloat() {
            return Value;
        }

        /// <summary>
        /// Returns the absolute magnitude of a scalar.
        /// </summary>
        /// <param name="value">Scalar whose magnitude is required.</param>
        /// <returns>The non-negative magnitude of <paramref name="value"/>.</returns>
        public static PhysicsScalar Abs(PhysicsScalar value) {
            return new PhysicsScalar((float)Math.Abs((double)value.Value));
        }

        /// <summary>
        /// Returns the lower of two scalar values.
        /// </summary>
        /// <param name="first">First scalar to compare.</param>
        /// <param name="second">Second scalar to compare.</param>
        /// <returns>The smaller input scalar.</returns>
        public static PhysicsScalar Min(PhysicsScalar first, PhysicsScalar second) {
            return new PhysicsScalar((float)Math.Min((double)first.Value, (double)second.Value));
        }

        /// <summary>
        /// Returns the greater of two scalar values.
        /// </summary>
        /// <param name="first">First scalar to compare.</param>
        /// <param name="second">Second scalar to compare.</param>
        /// <returns>The greater input scalar.</returns>
        public static PhysicsScalar Max(PhysicsScalar first, PhysicsScalar second) {
            return new PhysicsScalar((float)Math.Max((double)first.Value, (double)second.Value));
        }

        /// <summary>
        /// Restricts a scalar value to an inclusive range.
        /// </summary>
        /// <param name="value">Scalar to restrict.</param>
        /// <param name="minimum">Inclusive lower range boundary.</param>
        /// <param name="maximum">Inclusive upper range boundary.</param>
        /// <returns><paramref name="value"/> restricted to the supplied range.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="minimum"/> exceeds <paramref name="maximum"/>.</exception>
        public static PhysicsScalar Clamp(PhysicsScalar value, PhysicsScalar minimum, PhysicsScalar maximum) {
            if (minimum > maximum) {
                throw new ArgumentException("The minimum scalar value cannot exceed the maximum scalar value.", nameof(minimum));
            }

            return Min(Max(value, minimum), maximum);
        }

        /// <summary>
        /// Computes the non-negative square root of a non-negative scalar.
        /// </summary>
        /// <param name="value">Non-negative scalar whose square root is required.</param>
        /// <returns>The square root of <paramref name="value"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is negative.</exception>
        public static PhysicsScalar Sqrt(PhysicsScalar value) {
            if (value < Zero) {
                throw new ArgumentOutOfRangeException(nameof(value), "Physics square root requires a non-negative scalar.");
            }

            return new PhysicsScalar((float)Math.Sqrt((double)value.Value));
        }

        /// <summary>
        /// Computes one divided by the non-negative square root of a positive scalar.
        /// </summary>
        /// <param name="value">Positive scalar whose reciprocal square root is required.</param>
        /// <returns>The reciprocal square root of <paramref name="value"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is zero or negative.</exception>
        public static PhysicsScalar ReciprocalSqrt(PhysicsScalar value) {
            if (value <= Zero) {
                throw new ArgumentOutOfRangeException(nameof(value), "Physics reciprocal square root requires a positive scalar.");
            }

            return new PhysicsScalar((float)(1d / Math.Sqrt((double)value.Value)));
        }

        /// <summary>
        /// Determines whether another scalar stores the same finite value.
        /// </summary>
        /// <param name="other">Scalar to compare with this instance.</param>
        /// <returns>True when both scalars store the same value.</returns>
        public bool Equals(PhysicsScalar other) {
            return Value.Equals(other.Value);
        }

        /// <summary>
        /// Determines whether an object is an equal physics scalar.
        /// </summary>
        /// <param name="obj">Object to compare with this instance.</param>
        /// <returns>True when <paramref name="obj"/> is an equal physics scalar.</returns>
        public override bool Equals(object obj) {
            return obj is PhysicsScalar other && Equals(other);
        }

        /// <summary>
        /// Returns a hash code derived from the stored scalar value.
        /// </summary>
        /// <returns>A hash code for this scalar.</returns>
        public override int GetHashCode() {
            return Value.GetHashCode();
        }

        /// <summary>
        /// Adds two physics scalars.
        /// </summary>
        /// <param name="left">Left scalar operand.</param>
        /// <param name="right">Right scalar operand.</param>
        /// <returns>The finite sum of both operands.</returns>
        public static PhysicsScalar operator +(PhysicsScalar left, PhysicsScalar right) {
            return new PhysicsScalar(left.Value + right.Value);
        }

        /// <summary>
        /// Subtracts one physics scalar from another.
        /// </summary>
        /// <param name="left">Scalar from which to subtract.</param>
        /// <param name="right">Scalar to subtract.</param>
        /// <returns>The finite difference between both operands.</returns>
        public static PhysicsScalar operator -(PhysicsScalar left, PhysicsScalar right) {
            return new PhysicsScalar(left.Value - right.Value);
        }

        /// <summary>
        /// Negates a physics scalar.
        /// </summary>
        /// <param name="value">Scalar to negate.</param>
        /// <returns>The additive inverse of <paramref name="value"/>.</returns>
        public static PhysicsScalar operator -(PhysicsScalar value) {
            return new PhysicsScalar(-value.Value);
        }

        /// <summary>
        /// Multiplies two physics scalars.
        /// </summary>
        /// <param name="left">Left scalar operand.</param>
        /// <param name="right">Right scalar operand.</param>
        /// <returns>The finite product of both operands.</returns>
        public static PhysicsScalar operator *(PhysicsScalar left, PhysicsScalar right) {
            return new PhysicsScalar(left.Value * right.Value);
        }

        /// <summary>
        /// Divides one physics scalar by another.
        /// </summary>
        /// <param name="left">Dividend scalar.</param>
        /// <param name="right">Non-zero divisor scalar.</param>
        /// <returns>The finite quotient of both operands.</returns>
        /// <exception cref="DivideByZeroException">Thrown when <paramref name="right"/> is zero.</exception>
        public static PhysicsScalar operator /(PhysicsScalar left, PhysicsScalar right) {
            if (right == Zero) {
                throw new DivideByZeroException("Physics scalar division requires a non-zero divisor.");
            }

            return new PhysicsScalar(left.Value / right.Value);
        }

        /// <summary>
        /// Determines whether two physics scalars store the same value.
        /// </summary>
        /// <param name="left">First scalar to compare.</param>
        /// <param name="right">Second scalar to compare.</param>
        /// <returns>True when both scalars are equal.</returns>
        public static bool operator ==(PhysicsScalar left, PhysicsScalar right) {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two physics scalars store different values.
        /// </summary>
        /// <param name="left">First scalar to compare.</param>
        /// <param name="right">Second scalar to compare.</param>
        /// <returns>True when both scalars differ.</returns>
        public static bool operator !=(PhysicsScalar left, PhysicsScalar right) {
            return !left.Equals(right);
        }

        /// <summary>
        /// Determines whether one scalar is less than another.
        /// </summary>
        /// <param name="left">First scalar to compare.</param>
        /// <param name="right">Second scalar to compare.</param>
        /// <returns>True when <paramref name="left"/> is less than <paramref name="right"/>.</returns>
        public static bool operator <(PhysicsScalar left, PhysicsScalar right) {
            return left.Value < right.Value;
        }

        /// <summary>
        /// Determines whether one scalar is less than or equal to another.
        /// </summary>
        /// <param name="left">First scalar to compare.</param>
        /// <param name="right">Second scalar to compare.</param>
        /// <returns>True when <paramref name="left"/> does not exceed <paramref name="right"/>.</returns>
        public static bool operator <=(PhysicsScalar left, PhysicsScalar right) {
            return left.Value <= right.Value;
        }

        /// <summary>
        /// Determines whether one scalar is greater than another.
        /// </summary>
        /// <param name="left">First scalar to compare.</param>
        /// <param name="right">Second scalar to compare.</param>
        /// <returns>True when <paramref name="left"/> is greater than <paramref name="right"/>.</returns>
        public static bool operator >(PhysicsScalar left, PhysicsScalar right) {
            return left.Value > right.Value;
        }

        /// <summary>
        /// Determines whether one scalar is greater than or equal to another.
        /// </summary>
        /// <param name="left">First scalar to compare.</param>
        /// <param name="right">Second scalar to compare.</param>
        /// <returns>True when <paramref name="left"/> is not less than <paramref name="right"/>.</returns>
        public static bool operator >=(PhysicsScalar left, PhysicsScalar right) {
            return left.Value >= right.Value;
        }
    }
}
