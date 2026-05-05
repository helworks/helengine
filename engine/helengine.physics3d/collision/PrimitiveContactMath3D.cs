namespace helengine {
    /// <summary>
    /// Provides shared geometric helpers used by primitive contact resolvers.
    /// </summary>
    public static class PrimitiveContactMath3D {
        /// <summary>
        /// Determines whether two runtime body states overlap on all three world axes.
        /// </summary>
        /// <param name="first">First body state.</param>
        /// <param name="second">Second body state.</param>
        /// <returns>True when the bodies overlap on every axis.</returns>
        public static bool Overlaps(BodyState3D first, BodyState3D second) {
            if (first == null) {
                throw new ArgumentNullException(nameof(first));
            }
            if (second == null) {
                throw new ArgumentNullException(nameof(second));
            }

            return Overlaps(first.Position, first.HalfExtents, second.Position, second.HalfExtents);
        }

        /// <summary>
        /// Determines whether two axis-aligned boxes overlap on all three world axes.
        /// </summary>
        /// <param name="firstCenter">First box center position.</param>
        /// <param name="firstHalfExtents">First box half extents.</param>
        /// <param name="secondCenter">Second box center position.</param>
        /// <param name="secondHalfExtents">Second box half extents.</param>
        /// <returns>True when the boxes overlap on every axis.</returns>
        public static bool Overlaps(float3 firstCenter, float3 firstHalfExtents, float3 secondCenter, float3 secondHalfExtents) {
            return Math.Abs(firstCenter.X - secondCenter.X) < (firstHalfExtents.X + secondHalfExtents.X) &&
                Math.Abs(firstCenter.Y - secondCenter.Y) < (firstHalfExtents.Y + secondHalfExtents.Y) &&
                Math.Abs(firstCenter.Z - secondCenter.Z) < (firstHalfExtents.Z + secondHalfExtents.Z);
        }

        /// <summary>
        /// Calculates the overlap amount along one axis for two axis-aligned boxes.
        /// </summary>
        /// <param name="firstCenter">First box center.</param>
        /// <param name="firstHalfExtent">First box half extent.</param>
        /// <param name="secondCenter">Second box center.</param>
        /// <param name="secondHalfExtent">Second box half extent.</param>
        /// <returns>Signed penetration amount, or a non-positive value when no overlap exists.</returns>
        public static float CalculateAxisPenetration(float firstCenter, float firstHalfExtent, float secondCenter, float secondHalfExtent) {
            float centerDistance = Math.Abs(firstCenter - secondCenter);
            return (firstHalfExtent + secondHalfExtent) - centerDistance;
        }

        /// <summary>
        /// Gets the sign used to separate one body away from another on the selected axis.
        /// </summary>
        /// <param name="first">First body state.</param>
        /// <param name="second">Second body state.</param>
        /// <param name="axisIndex">Zero for X, one for Y, two for Z.</param>
        /// <returns>Positive or negative separation direction.</returns>
        public static float GetAxisDirection(BodyState3D first, BodyState3D second, int axisIndex) {
            if (first == null) {
                throw new ArgumentNullException(nameof(first));
            }
            if (second == null) {
                throw new ArgumentNullException(nameof(second));
            }

            return GetAxisDirection(first.Position, second.Position, axisIndex);
        }

        /// <summary>
        /// Gets the sign used to separate one axis-aligned box center away from another on the selected axis.
        /// </summary>
        /// <param name="firstCenter">First box center position.</param>
        /// <param name="secondCenter">Second box center position.</param>
        /// <param name="axisIndex">Zero for X, one for Y, two for Z.</param>
        /// <returns>Positive or negative separation direction.</returns>
        public static float GetAxisDirection(float3 firstCenter, float3 secondCenter, int axisIndex) {
            float difference = GetAxisValue(firstCenter, axisIndex) - GetAxisValue(secondCenter, axisIndex);
            if (difference >= 0f) {
                return 1f;
            }

            return -1f;
        }

        /// <summary>
        /// Reads one axis value from a vector.
        /// </summary>
        /// <param name="value">Vector being queried.</param>
        /// <param name="axisIndex">Zero for X, one for Y, two for Z.</param>
        /// <returns>Selected component value.</returns>
        public static float GetAxisValue(float3 value, int axisIndex) {
            if (axisIndex == 0) {
                return value.X;
            }
            if (axisIndex == 1) {
                return value.Y;
            }
            if (axisIndex == 2) {
                return value.Z;
            }

            throw new ArgumentOutOfRangeException(nameof(axisIndex), "Axis index must be between zero and two.");
        }

        /// <summary>
        /// Applies one scalar offset to the selected axis of a vector.
        /// </summary>
        /// <param name="value">Original vector value.</param>
        /// <param name="axisIndex">Zero for X, one for Y, two for Z.</param>
        /// <param name="offset">Scalar offset to apply.</param>
        /// <returns>Offset vector.</returns>
        public static float3 OffsetAxis(float3 value, int axisIndex, float offset) {
            if (axisIndex == 0) {
                return new float3(value.X + offset, value.Y, value.Z);
            }
            if (axisIndex == 1) {
                return new float3(value.X, value.Y + offset, value.Z);
            }
            if (axisIndex == 2) {
                return new float3(value.X, value.Y, value.Z + offset);
            }

            throw new ArgumentOutOfRangeException(nameof(axisIndex), "Axis index must be between zero and two.");
        }

        /// <summary>
        /// Clamps one scalar value to the supplied inclusive range.
        /// </summary>
        /// <param name="value">Value being clamped.</param>
        /// <param name="minimum">Inclusive minimum value.</param>
        /// <param name="maximum">Inclusive maximum value.</param>
        /// <returns>Clamped scalar value.</returns>
        public static float Clamp(float value, float minimum, float maximum) {
            if (value < minimum) {
                return minimum;
            }
            if (value > maximum) {
                return maximum;
            }

            return value;
        }

        /// <summary>
        /// Resolves the closest point on one vertical capsule segment to the supplied world-space Y coordinate.
        /// </summary>
        /// <param name="capsuleBody">Capsule body whose segment should be queried.</param>
        /// <param name="targetY">World-space Y coordinate to clamp to the capsule segment.</param>
        /// <returns>Closest point on the capsule segment.</returns>
        public static float3 GetClosestPointOnVerticalCapsuleSegment(BodyState3D capsuleBody, float targetY) {
            if (capsuleBody == null) {
                throw new ArgumentNullException(nameof(capsuleBody));
            }

            float clampedY = Clamp(
                targetY,
                capsuleBody.Position.Y - capsuleBody.CapsuleSegmentHalfLength,
                capsuleBody.Position.Y + capsuleBody.CapsuleSegmentHalfLength);
            return new float3(capsuleBody.Position.X, clampedY, capsuleBody.Position.Z);
        }
    }
}
