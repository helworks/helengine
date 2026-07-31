namespace helengine {
    /// <summary>
    /// Provides scalar math functions required by physics calculations without exposing primitive numeric operations to solver code.
    /// </summary>
    public static class PhysicsMath {
        /// <summary>
        /// Gets the ratio of a circle's circumference to its diameter as a physics scalar.
        /// </summary>
        public static PhysicsScalar Pi => PhysicsScalar.FromFloat((float)Math.PI);

        /// <summary>
        /// Returns the absolute magnitude of a scalar.
        /// </summary>
        /// <param name="value">Scalar whose magnitude is required.</param>
        /// <returns>The non-negative magnitude of <paramref name="value"/>.</returns>
        public static PhysicsScalar Abs(PhysicsScalar value) {
            return PhysicsScalar.Abs(value);
        }

        /// <summary>
        /// Returns the lower of two scalar values.
        /// </summary>
        /// <param name="first">First scalar to compare.</param>
        /// <param name="second">Second scalar to compare.</param>
        /// <returns>The smaller input scalar.</returns>
        public static PhysicsScalar Min(PhysicsScalar first, PhysicsScalar second) {
            return PhysicsScalar.Min(first, second);
        }

        /// <summary>
        /// Returns the greater of two scalar values.
        /// </summary>
        /// <param name="first">First scalar to compare.</param>
        /// <param name="second">Second scalar to compare.</param>
        /// <returns>The greater input scalar.</returns>
        public static PhysicsScalar Max(PhysicsScalar first, PhysicsScalar second) {
            return PhysicsScalar.Max(first, second);
        }

        /// <summary>
        /// Restricts a scalar value to an inclusive range.
        /// </summary>
        /// <param name="value">Scalar to restrict.</param>
        /// <param name="minimum">Inclusive lower range boundary.</param>
        /// <param name="maximum">Inclusive upper range boundary.</param>
        /// <returns><paramref name="value"/> restricted to the supplied range.</returns>
        public static PhysicsScalar Clamp(PhysicsScalar value, PhysicsScalar minimum, PhysicsScalar maximum) {
            return PhysicsScalar.Clamp(value, minimum, maximum);
        }

        /// <summary>
        /// Computes the non-negative square root of a non-negative scalar.
        /// </summary>
        /// <param name="value">Non-negative scalar whose square root is required.</param>
        /// <returns>The square root of <paramref name="value"/>.</returns>
        public static PhysicsScalar Sqrt(PhysicsScalar value) {
            return PhysicsScalar.Sqrt(value);
        }

        /// <summary>
        /// Computes one divided by the square root of a positive scalar.
        /// </summary>
        /// <param name="value">Positive scalar whose reciprocal square root is required.</param>
        /// <returns>The reciprocal square root of <paramref name="value"/>.</returns>
        public static PhysicsScalar ReciprocalSqrt(PhysicsScalar value) {
            return PhysicsScalar.ReciprocalSqrt(value);
        }

        /// <summary>
        /// Computes the sine of an angle measured in radians.
        /// </summary>
        /// <param name="angle">Angle in radians.</param>
        /// <returns>The sine of <paramref name="angle"/>.</returns>
        public static PhysicsScalar Sin(PhysicsScalar angle) {
            return PhysicsScalar.FromFloat((float)Math.Sin((double)angle.ToFloat()));
        }

        /// <summary>
        /// Computes the cosine of an angle measured in radians.
        /// </summary>
        /// <param name="angle">Angle in radians.</param>
        /// <returns>The cosine of <paramref name="angle"/>.</returns>
        public static PhysicsScalar Cos(PhysicsScalar angle) {
            return PhysicsScalar.FromFloat((float)Math.Cos((double)angle.ToFloat()));
        }
    }
}
