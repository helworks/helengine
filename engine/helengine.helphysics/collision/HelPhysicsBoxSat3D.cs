namespace helengine {
    /// <summary>
    /// Performs allocation-free scalar separating-axis queries between two arbitrarily oriented boxes.
    /// </summary>
    static class HelPhysicsBoxSat3D {
        /// <summary>
        /// Stores the exclusive squared-length boundary below which nearly parallel edge cross axes are degenerate.
        /// </summary>
        static readonly PhysicsScalar CrossAxisLengthSquaredThreshold = PhysicsScalar.FromFloat(1e-8f);

        /// <summary>
        /// Tests all three A faces, three B faces, and nine ordered edge cross axes for overlap and returns the shallowest axis.
        /// </summary>
        /// <param name="ShapeA">First centered box shape.</param>
        /// <param name="BodyA">World-space pose of the first box.</param>
        /// <param name="ShapeB">Second centered box shape.</param>
        /// <param name="BodyB">World-space pose of the second box.</param>
        /// <param name="Result">Receives the A-to-B minimum-penetration result when every non-degenerate SAT axis overlaps.</param>
        /// <returns>True when the boxes overlap or exactly touch on every tested axis; otherwise false.</returns>
        public static bool TryFindMinimumPenetration(
            in HelPhysicsBoxShape3D ShapeA,
            in HelPhysicsBodyState3D BodyA,
            in HelPhysicsBoxShape3D ShapeB,
            in HelPhysicsBodyState3D BodyB,
            out HelPhysicsBoxSatResult3D Result) {
            PhysicsVector3 axisA0 = HelPhysicsBoxGeometry3D.GetWorldAxis(BodyA.Orientation, 0);
            PhysicsVector3 axisA1 = HelPhysicsBoxGeometry3D.GetWorldAxis(BodyA.Orientation, 1);
            PhysicsVector3 axisA2 = HelPhysicsBoxGeometry3D.GetWorldAxis(BodyA.Orientation, 2);
            PhysicsVector3 axisB0 = HelPhysicsBoxGeometry3D.GetWorldAxis(BodyB.Orientation, 0);
            PhysicsVector3 axisB1 = HelPhysicsBoxGeometry3D.GetWorldAxis(BodyB.Orientation, 1);
            PhysicsVector3 axisB2 = HelPhysicsBoxGeometry3D.GetWorldAxis(BodyB.Orientation, 2);
            PhysicsVector3 centerOffset = BodyB.Position - BodyA.Position;
            PhysicsVector3 minimumNormal = PhysicsVector3.Zero;
            PhysicsScalar minimumPenetration = PhysicsScalar.Zero;
            HelPhysicsBoxSatAxisKind3D minimumAxisKind = HelPhysicsBoxSatAxisKind3D.FaceA;
            int minimumAxisAIndex = -1;
            int minimumAxisBIndex = -1;
            bool hasMinimum = false;

            for (int axisIndex = 0; axisIndex < 3; axisIndex++) {
                PhysicsVector3 axis = GetAxis(axisA0, axisA1, axisA2, axisIndex);
                if (!TryEvaluateAxis(
                    axis,
                    centerOffset,
                    ShapeA.HalfExtents,
                    axisA0,
                    axisA1,
                    axisA2,
                    ShapeB.HalfExtents,
                    axisB0,
                    axisB1,
                    axisB2,
                    HelPhysicsBoxSatAxisKind3D.FaceA,
                    axisIndex,
                    -1,
                    ref hasMinimum,
                    ref minimumNormal,
                    ref minimumPenetration,
                    ref minimumAxisKind,
                    ref minimumAxisAIndex,
                    ref minimumAxisBIndex)) {
                    Result = default;
                    return false;
                }
            }

            for (int axisIndex = 0; axisIndex < 3; axisIndex++) {
                PhysicsVector3 axis = GetAxis(axisB0, axisB1, axisB2, axisIndex);
                if (!TryEvaluateAxis(
                    axis,
                    centerOffset,
                    ShapeA.HalfExtents,
                    axisA0,
                    axisA1,
                    axisA2,
                    ShapeB.HalfExtents,
                    axisB0,
                    axisB1,
                    axisB2,
                    HelPhysicsBoxSatAxisKind3D.FaceB,
                    -1,
                    axisIndex,
                    ref hasMinimum,
                    ref minimumNormal,
                    ref minimumPenetration,
                    ref minimumAxisKind,
                    ref minimumAxisAIndex,
                    ref minimumAxisBIndex)) {
                    Result = default;
                    return false;
                }
            }

            for (int axisAIndex = 0; axisAIndex < 3; axisAIndex++) {
                PhysicsVector3 axisA = GetAxis(axisA0, axisA1, axisA2, axisAIndex);
                for (int axisBIndex = 0; axisBIndex < 3; axisBIndex++) {
                    PhysicsVector3 axisB = GetAxis(axisB0, axisB1, axisB2, axisBIndex);
                    if (!TryEvaluateCrossAxis(
                        axisA,
                        axisB,
                        axisAIndex,
                        axisBIndex,
                        centerOffset,
                        ShapeA.HalfExtents,
                        axisA0,
                        axisA1,
                        axisA2,
                        ShapeB.HalfExtents,
                        axisB0,
                        axisB1,
                        axisB2,
                        ref hasMinimum,
                        ref minimumNormal,
                        ref minimumPenetration,
                        ref minimumAxisKind,
                        ref minimumAxisAIndex,
                        ref minimumAxisBIndex)) {
                        Result = default;
                        return false;
                    }
                }
            }

            Result = new HelPhysicsBoxSatResult3D(
                minimumNormal,
                minimumAxisKind,
                minimumAxisAIndex,
                minimumAxisBIndex,
                minimumPenetration);
            return true;
        }

        /// <summary>
        /// Evaluates one normalized SAT axis and updates the current winner only for a strictly smaller penetration.
        /// </summary>
        /// <param name="axis">Normalized world-space candidate axis.</param>
        /// <param name="centerOffset">World-space displacement from box A's center to box B's center.</param>
        /// <param name="halfExtentsA">Local half extents of box A.</param>
        /// <param name="axisA0">World-space local X axis of box A.</param>
        /// <param name="axisA1">World-space local Y axis of box A.</param>
        /// <param name="axisA2">World-space local Z axis of box A.</param>
        /// <param name="halfExtentsB">Local half extents of box B.</param>
        /// <param name="axisB0">World-space local X axis of box B.</param>
        /// <param name="axisB1">World-space local Y axis of box B.</param>
        /// <param name="axisB2">World-space local Z axis of box B.</param>
        /// <param name="axisKind">Family of the candidate axis.</param>
        /// <param name="axisAIndex">Candidate A-axis index, or negative one for a B-face candidate.</param>
        /// <param name="axisBIndex">Candidate B-axis index, or negative one for an A-face candidate.</param>
        /// <param name="hasMinimum">Indicates whether an earlier candidate initialized the winning values.</param>
        /// <param name="minimumNormal">Current A-to-B winning normal, updated for a strictly shallower candidate.</param>
        /// <param name="minimumPenetration">Current winning penetration, updated for a strictly shallower candidate.</param>
        /// <param name="minimumAxisKind">Current winning axis family, updated for a strictly shallower candidate.</param>
        /// <param name="minimumAxisAIndex">Current winning A-axis index, updated for a strictly shallower candidate.</param>
        /// <param name="minimumAxisBIndex">Current winning B-axis index, updated for a strictly shallower candidate.</param>
        /// <returns>False when the candidate separates the boxes; otherwise true.</returns>
        static bool TryEvaluateAxis(
            PhysicsVector3 axis,
            PhysicsVector3 centerOffset,
            PhysicsVector3 halfExtentsA,
            PhysicsVector3 axisA0,
            PhysicsVector3 axisA1,
            PhysicsVector3 axisA2,
            PhysicsVector3 halfExtentsB,
            PhysicsVector3 axisB0,
            PhysicsVector3 axisB1,
            PhysicsVector3 axisB2,
            HelPhysicsBoxSatAxisKind3D axisKind,
            int axisAIndex,
            int axisBIndex,
            ref bool hasMinimum,
            ref PhysicsVector3 minimumNormal,
            ref PhysicsScalar minimumPenetration,
            ref HelPhysicsBoxSatAxisKind3D minimumAxisKind,
            ref int minimumAxisAIndex,
            ref int minimumAxisBIndex) {
            PhysicsScalar projectedRadiusA = ComputeProjectedRadius(halfExtentsA, axisA0, axisA1, axisA2, axis);
            PhysicsScalar projectedRadiusB = ComputeProjectedRadius(halfExtentsB, axisB0, axisB1, axisB2, axis);
            PhysicsScalar centerDistance = PhysicsScalar.Abs(PhysicsVector3.Dot(centerOffset, axis));
            PhysicsScalar penetration = projectedRadiusA + projectedRadiusB - centerDistance;

            if (penetration < PhysicsScalar.Zero) {
                return false;
            }

            if (!hasMinimum || penetration < minimumPenetration) {
                minimumNormal = PhysicsVector3.Dot(centerOffset, axis) < PhysicsScalar.Zero ? -axis : axis;
                minimumPenetration = penetration;
                minimumAxisKind = axisKind;
                minimumAxisAIndex = axisAIndex;
                minimumAxisBIndex = axisBIndex;
                hasMinimum = true;
            }

            return true;
        }

        /// <summary>
        /// Rejects one degenerate edge pair or normalizes and evaluates its cross-product SAT axis.
        /// </summary>
        /// <param name="axisA">Normalized world-space edge direction from box A.</param>
        /// <param name="axisB">Normalized world-space edge direction from box B.</param>
        /// <param name="axisAIndex">Local A edge-direction index.</param>
        /// <param name="axisBIndex">Local B edge-direction index.</param>
        /// <param name="centerOffset">World-space displacement from box A's center to box B's center.</param>
        /// <param name="halfExtentsA">Local half extents of box A.</param>
        /// <param name="axisA0">World-space local X axis of box A.</param>
        /// <param name="axisA1">World-space local Y axis of box A.</param>
        /// <param name="axisA2">World-space local Z axis of box A.</param>
        /// <param name="halfExtentsB">Local half extents of box B.</param>
        /// <param name="axisB0">World-space local X axis of box B.</param>
        /// <param name="axisB1">World-space local Y axis of box B.</param>
        /// <param name="axisB2">World-space local Z axis of box B.</param>
        /// <param name="hasMinimum">Indicates whether an earlier candidate initialized the winning values.</param>
        /// <param name="minimumNormal">Current A-to-B winning normal, updated for a strictly shallower candidate.</param>
        /// <param name="minimumPenetration">Current winning penetration, updated for a strictly shallower candidate.</param>
        /// <param name="minimumAxisKind">Current winning axis family, updated for a strictly shallower candidate.</param>
        /// <param name="minimumAxisAIndex">Current winning A-axis index, updated for a strictly shallower candidate.</param>
        /// <param name="minimumAxisBIndex">Current winning B-axis index, updated for a strictly shallower candidate.</param>
        /// <returns>False when the accepted cross axis separates the boxes; otherwise true.</returns>
        static bool TryEvaluateCrossAxis(
            PhysicsVector3 axisA,
            PhysicsVector3 axisB,
            int axisAIndex,
            int axisBIndex,
            PhysicsVector3 centerOffset,
            PhysicsVector3 halfExtentsA,
            PhysicsVector3 axisA0,
            PhysicsVector3 axisA1,
            PhysicsVector3 axisA2,
            PhysicsVector3 halfExtentsB,
            PhysicsVector3 axisB0,
            PhysicsVector3 axisB1,
            PhysicsVector3 axisB2,
            ref bool hasMinimum,
            ref PhysicsVector3 minimumNormal,
            ref PhysicsScalar minimumPenetration,
            ref HelPhysicsBoxSatAxisKind3D minimumAxisKind,
            ref int minimumAxisAIndex,
            ref int minimumAxisBIndex) {
            PhysicsVector3 crossAxis = PhysicsVector3.Cross(axisA, axisB);
            PhysicsScalar lengthSquared = crossAxis.LengthSquared();
            if (lengthSquared < CrossAxisLengthSquaredThreshold) {
                return true;
            }

            PhysicsVector3 normalizedAxis = crossAxis * PhysicsScalar.ReciprocalSqrt(lengthSquared);
            return TryEvaluateAxis(
                normalizedAxis,
                centerOffset,
                halfExtentsA,
                axisA0,
                axisA1,
                axisA2,
                halfExtentsB,
                axisB0,
                axisB1,
                axisB2,
                HelPhysicsBoxSatAxisKind3D.EdgePair,
                axisAIndex,
                axisBIndex,
                ref hasMinimum,
                ref minimumNormal,
                ref minimumPenetration,
                ref minimumAxisKind,
                ref minimumAxisAIndex,
                ref minimumAxisBIndex);
        }

        /// <summary>
        /// Computes one box's scalar projection radius on a normalized world-space axis.
        /// </summary>
        /// <param name="halfExtents">Box half extents paired with the supplied local axes.</param>
        /// <param name="axis0">World-space local X axis of the box.</param>
        /// <param name="axis1">World-space local Y axis of the box.</param>
        /// <param name="axis2">World-space local Z axis of the box.</param>
        /// <param name="projectionAxis">Normalized world-space axis onto which the box is projected.</param>
        /// <returns>The non-negative distance from the projected center to either projected extreme.</returns>
        static PhysicsScalar ComputeProjectedRadius(
            PhysicsVector3 halfExtents,
            PhysicsVector3 axis0,
            PhysicsVector3 axis1,
            PhysicsVector3 axis2,
            PhysicsVector3 projectionAxis) {
            return (halfExtents.X * PhysicsScalar.Abs(PhysicsVector3.Dot(axis0, projectionAxis)))
                + (halfExtents.Y * PhysicsScalar.Abs(PhysicsVector3.Dot(axis1, projectionAxis)))
                + (halfExtents.Z * PhysicsScalar.Abs(PhysicsVector3.Dot(axis2, projectionAxis)));
        }

        /// <summary>
        /// Selects one of three already-computed orthonormal box axes without allocating indexed storage.
        /// </summary>
        /// <param name="axis0">Axis at index zero.</param>
        /// <param name="axis1">Axis at index one.</param>
        /// <param name="axis2">Axis at index two.</param>
        /// <param name="axisIndex">Requested axis index from zero through two.</param>
        /// <returns>The axis matching <paramref name="axisIndex"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="axisIndex"/> is outside the three box axes.</exception>
        static PhysicsVector3 GetAxis(PhysicsVector3 axis0, PhysicsVector3 axis1, PhysicsVector3 axis2, int axisIndex) {
            if (axisIndex == 0) {
                return axis0;
            } else if (axisIndex == 1) {
                return axis1;
            } else if (axisIndex == 2) {
                return axis2;
            }

            throw new ArgumentOutOfRangeException(nameof(axisIndex), "Box SAT axes are indexed from zero through two.");
        }
    }
}
