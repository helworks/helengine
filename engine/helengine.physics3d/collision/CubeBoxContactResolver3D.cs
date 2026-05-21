namespace helengine {
    /// <summary>
    /// Resolves contacts between two oriented cube runtime bodies.
    /// </summary>
    public static class CubeBoxContactResolver3D {
        /// <summary>
        /// Minimum local-axis alignment required before rectangular face patches use world-axis overlap math.
        /// </summary>
        const float AxisAlignedBoxContactThreshold = 0.95f;

        /// <summary>
        /// Finds one contact manifold for overlapping cube bodies.
        /// </summary>
        /// <param name="first">First cube body state.</param>
        /// <param name="second">Second cube body state.</param>
        /// <param name="manifold">Resolved contact manifold when the cubes overlap.</param>
        /// <returns>True when no separating axis exists.</returns>
        public static bool TryResolveManifold(CubeBodyState3D first, CubeBodyState3D second, out CubeContactManifold3D manifold) {
            if (!TryResolveContactAxis(first, second, out float3 normal, out float penetration)) {
                manifold = null;
                return false;
            }

            int axisIndex = ResolveDominantAxisIndex(normal);
            if (TryResolveAxisAlignedFacePatch(first, second, normal, penetration, axisIndex, out manifold)) {
                return true;
            }

            float3 contactPosition = ResolveFallbackContactPosition(first, second, normal);
            manifold = new CubeContactManifold3D(normal, new CubeContactPoint3D(contactPosition, penetration));
            return true;
        }

        /// <summary>
        /// Finds the least-penetrating separating-axis-test axis for one cube pair.
        /// </summary>
        /// <param name="first">First cube body state.</param>
        /// <param name="second">Second cube body state.</param>
        /// <param name="normal">Unit normal pointing from the second body toward the first body.</param>
        /// <param name="penetration">Positive overlap distance on the selected axis.</param>
        /// <returns>True when all SAT axes overlap.</returns>
        static bool TryResolveContactAxis(CubeBodyState3D first, CubeBodyState3D second, out float3 normal, out float penetration) {
            if (first == null) {
                throw new ArgumentNullException(nameof(first));
            }
            if (second == null) {
                throw new ArgumentNullException(nameof(second));
            }

            float3 firstAxisX = first.ResolveBoxAxis(0);
            float3 firstAxisY = first.ResolveBoxAxis(1);
            float3 firstAxisZ = first.ResolveBoxAxis(2);
            float3 secondAxisX = second.ResolveBoxAxis(0);
            float3 secondAxisY = second.ResolveBoxAxis(1);
            float3 secondAxisZ = second.ResolveBoxAxis(2);
            float bestPenetration = float.MaxValue;
            float3 bestNormal = float3.Zero;

            if (!TryAccumulateFaceAxes(first, second, firstAxisX, firstAxisY, firstAxisZ, secondAxisX, secondAxisY, secondAxisZ, ref bestPenetration, ref bestNormal)) {
                normal = float3.Zero;
                penetration = 0f;
                return false;
            }
            if (!TryAccumulateCrossAxes(first, second, firstAxisX, firstAxisY, firstAxisZ, secondAxisX, secondAxisY, secondAxisZ, ref bestPenetration, ref bestNormal)) {
                normal = float3.Zero;
                penetration = 0f;
                return false;
            }

            normal = bestNormal;
            penetration = bestPenetration;
            return true;
        }

        /// <summary>
        /// Accumulates the six face-normal SAT axes for two cube bodies.
        /// </summary>
        /// <param name="first">First cube body state.</param>
        /// <param name="second">Second cube body state.</param>
        /// <param name="firstAxisX">First cube local X axis in world space.</param>
        /// <param name="firstAxisY">First cube local Y axis in world space.</param>
        /// <param name="firstAxisZ">First cube local Z axis in world space.</param>
        /// <param name="secondAxisX">Second cube local X axis in world space.</param>
        /// <param name="secondAxisY">Second cube local Y axis in world space.</param>
        /// <param name="secondAxisZ">Second cube local Z axis in world space.</param>
        /// <param name="bestPenetration">Best overlap depth found so far.</param>
        /// <param name="bestNormal">Best contact normal found so far.</param>
        /// <returns>False when a separating axis is found.</returns>
        static bool TryAccumulateFaceAxes(
            CubeBodyState3D first,
            CubeBodyState3D second,
            float3 firstAxisX,
            float3 firstAxisY,
            float3 firstAxisZ,
            float3 secondAxisX,
            float3 secondAxisY,
            float3 secondAxisZ,
            ref float bestPenetration,
            ref float3 bestNormal) {
            if (!TryAccumulateSeparationAxis(first, second, firstAxisX, firstAxisX, firstAxisY, firstAxisZ, secondAxisX, secondAxisY, secondAxisZ, ref bestPenetration, ref bestNormal)) {
                return false;
            }
            if (!TryAccumulateSeparationAxis(first, second, firstAxisY, firstAxisX, firstAxisY, firstAxisZ, secondAxisX, secondAxisY, secondAxisZ, ref bestPenetration, ref bestNormal)) {
                return false;
            }
            if (!TryAccumulateSeparationAxis(first, second, firstAxisZ, firstAxisX, firstAxisY, firstAxisZ, secondAxisX, secondAxisY, secondAxisZ, ref bestPenetration, ref bestNormal)) {
                return false;
            }
            if (!TryAccumulateSeparationAxis(first, second, secondAxisX, firstAxisX, firstAxisY, firstAxisZ, secondAxisX, secondAxisY, secondAxisZ, ref bestPenetration, ref bestNormal)) {
                return false;
            }
            if (!TryAccumulateSeparationAxis(first, second, secondAxisY, firstAxisX, firstAxisY, firstAxisZ, secondAxisX, secondAxisY, secondAxisZ, ref bestPenetration, ref bestNormal)) {
                return false;
            }

            return TryAccumulateSeparationAxis(first, second, secondAxisZ, firstAxisX, firstAxisY, firstAxisZ, secondAxisX, secondAxisY, secondAxisZ, ref bestPenetration, ref bestNormal);
        }

        /// <summary>
        /// Accumulates the nine cross-product SAT axes for two cube bodies.
        /// </summary>
        /// <param name="first">First cube body state.</param>
        /// <param name="second">Second cube body state.</param>
        /// <param name="firstAxisX">First cube local X axis in world space.</param>
        /// <param name="firstAxisY">First cube local Y axis in world space.</param>
        /// <param name="firstAxisZ">First cube local Z axis in world space.</param>
        /// <param name="secondAxisX">Second cube local X axis in world space.</param>
        /// <param name="secondAxisY">Second cube local Y axis in world space.</param>
        /// <param name="secondAxisZ">Second cube local Z axis in world space.</param>
        /// <param name="bestPenetration">Best overlap depth found so far.</param>
        /// <param name="bestNormal">Best contact normal found so far.</param>
        /// <returns>False when a separating axis is found.</returns>
        static bool TryAccumulateCrossAxes(
            CubeBodyState3D first,
            CubeBodyState3D second,
            float3 firstAxisX,
            float3 firstAxisY,
            float3 firstAxisZ,
            float3 secondAxisX,
            float3 secondAxisY,
            float3 secondAxisZ,
            ref float bestPenetration,
            ref float3 bestNormal) {
            if (!TryAccumulateSeparationAxis(first, second, float3.Cross(firstAxisX, secondAxisX), firstAxisX, firstAxisY, firstAxisZ, secondAxisX, secondAxisY, secondAxisZ, ref bestPenetration, ref bestNormal)) {
                return false;
            }
            if (!TryAccumulateSeparationAxis(first, second, float3.Cross(firstAxisX, secondAxisY), firstAxisX, firstAxisY, firstAxisZ, secondAxisX, secondAxisY, secondAxisZ, ref bestPenetration, ref bestNormal)) {
                return false;
            }
            if (!TryAccumulateSeparationAxis(first, second, float3.Cross(firstAxisX, secondAxisZ), firstAxisX, firstAxisY, firstAxisZ, secondAxisX, secondAxisY, secondAxisZ, ref bestPenetration, ref bestNormal)) {
                return false;
            }
            if (!TryAccumulateSeparationAxis(first, second, float3.Cross(firstAxisY, secondAxisX), firstAxisX, firstAxisY, firstAxisZ, secondAxisX, secondAxisY, secondAxisZ, ref bestPenetration, ref bestNormal)) {
                return false;
            }
            if (!TryAccumulateSeparationAxis(first, second, float3.Cross(firstAxisY, secondAxisY), firstAxisX, firstAxisY, firstAxisZ, secondAxisX, secondAxisY, secondAxisZ, ref bestPenetration, ref bestNormal)) {
                return false;
            }
            if (!TryAccumulateSeparationAxis(first, second, float3.Cross(firstAxisY, secondAxisZ), firstAxisX, firstAxisY, firstAxisZ, secondAxisX, secondAxisY, secondAxisZ, ref bestPenetration, ref bestNormal)) {
                return false;
            }
            if (!TryAccumulateSeparationAxis(first, second, float3.Cross(firstAxisZ, secondAxisX), firstAxisX, firstAxisY, firstAxisZ, secondAxisX, secondAxisY, secondAxisZ, ref bestPenetration, ref bestNormal)) {
                return false;
            }
            if (!TryAccumulateSeparationAxis(first, second, float3.Cross(firstAxisZ, secondAxisY), firstAxisX, firstAxisY, firstAxisZ, secondAxisX, secondAxisY, secondAxisZ, ref bestPenetration, ref bestNormal)) {
                return false;
            }

            return TryAccumulateSeparationAxis(first, second, float3.Cross(firstAxisZ, secondAxisZ), firstAxisX, firstAxisY, firstAxisZ, secondAxisX, secondAxisY, secondAxisZ, ref bestPenetration, ref bestNormal);
        }

        /// <summary>
        /// Tests one SAT axis and records it when it is the shallowest overlap so far.
        /// </summary>
        /// <param name="first">First cube body state.</param>
        /// <param name="second">Second cube body state.</param>
        /// <param name="axis">Candidate separation axis.</param>
        /// <param name="firstAxisX">First cube local X axis in world space.</param>
        /// <param name="firstAxisY">First cube local Y axis in world space.</param>
        /// <param name="firstAxisZ">First cube local Z axis in world space.</param>
        /// <param name="secondAxisX">Second cube local X axis in world space.</param>
        /// <param name="secondAxisY">Second cube local Y axis in world space.</param>
        /// <param name="secondAxisZ">Second cube local Z axis in world space.</param>
        /// <param name="bestPenetration">Best overlap depth found so far.</param>
        /// <param name="bestNormal">Best contact normal found so far.</param>
        /// <returns>False when this axis separates the cubes.</returns>
        static bool TryAccumulateSeparationAxis(
            CubeBodyState3D first,
            CubeBodyState3D second,
            float3 axis,
            float3 firstAxisX,
            float3 firstAxisY,
            float3 firstAxisZ,
            float3 secondAxisX,
            float3 secondAxisY,
            float3 secondAxisZ,
            ref float bestPenetration,
            ref float3 bestNormal) {
            double axisLengthSquared = float3.Dot(axis, axis);
            if (axisLengthSquared <= 0.000000001d) {
                return true;
            }

            float3 unitAxis = axis / (float)Math.Sqrt(axisLengthSquared);
            double firstRadius = ProjectBoxRadius(first, unitAxis, firstAxisX, firstAxisY, firstAxisZ);
            double secondRadius = ProjectBoxRadius(second, unitAxis, secondAxisX, secondAxisY, secondAxisZ);
            double centerDistance = Math.Abs(float3.Dot(first.Position - second.Position, unitAxis));
            double penetration = firstRadius + secondRadius - centerDistance;
            if (penetration <= 0d) {
                return false;
            }
            if (penetration >= bestPenetration) {
                return true;
            }

            if (float3.Dot(first.Position - second.Position, unitAxis) < 0f) {
                unitAxis = unitAxis * -1f;
            }

            bestPenetration = (float)penetration;
            bestNormal = unitAxis;
            return true;
        }

        /// <summary>
        /// Projects one cube radius onto a unit axis.
        /// </summary>
        /// <param name="bodyState">Cube body state being projected.</param>
        /// <param name="axis">Unit projection axis.</param>
        /// <param name="boxAxisX">Cube local X axis in world space.</param>
        /// <param name="boxAxisY">Cube local Y axis in world space.</param>
        /// <param name="boxAxisZ">Cube local Z axis in world space.</param>
        /// <returns>Positive projected radius.</returns>
        static double ProjectBoxRadius(CubeBodyState3D bodyState, float3 axis, float3 boxAxisX, float3 boxAxisY, float3 boxAxisZ) {
            return (Math.Abs(float3.Dot(axis, boxAxisX)) * bodyState.HalfExtents.X) +
                (Math.Abs(float3.Dot(axis, boxAxisY)) * bodyState.HalfExtents.Y) +
                (Math.Abs(float3.Dot(axis, boxAxisZ)) * bodyState.HalfExtents.Z);
        }

        /// <summary>
        /// Creates a four-point rectangular patch for axis-aligned face contacts.
        /// </summary>
        /// <param name="first">First overlapping cube body.</param>
        /// <param name="second">Second overlapping cube body.</param>
        /// <param name="normal">Unit normal pointing from the second body toward the first body.</param>
        /// <param name="penetration">Positive overlap on the selected axis.</param>
        /// <param name="normalAxisIndex">Dominant world axis of the manifold normal.</param>
        /// <param name="manifold">Resolved four-point manifold when a patch can be created.</param>
        /// <returns>True when a stable four-point patch was created.</returns>
        static bool TryResolveAxisAlignedFacePatch(CubeBodyState3D first, CubeBodyState3D second, float3 normal, float penetration, int normalAxisIndex, out CubeContactManifold3D manifold) {
            manifold = null;
            if (!CanUseAxisAlignedOverlapPatch(first, second)) {
                return false;
            }

            float normalComponent = Math.Abs(GetVectorComponent(normal, normalAxisIndex));
            if (normalComponent < AxisAlignedBoxContactThreshold) {
                return false;
            }

            int firstTangentAxisIndex = ResolveFirstTangentAxisIndex(normalAxisIndex);
            int secondTangentAxisIndex = ResolveSecondTangentAxisIndex(normalAxisIndex);
            float firstTangentMinimum = ResolveOverlapPatchMinimum(first, second, firstTangentAxisIndex);
            float firstTangentMaximum = ResolveOverlapPatchMaximum(first, second, firstTangentAxisIndex);
            float secondTangentMinimum = ResolveOverlapPatchMinimum(first, second, secondTangentAxisIndex);
            float secondTangentMaximum = ResolveOverlapPatchMaximum(first, second, secondTangentAxisIndex);
            float planeValue = ResolveOverlapPatchAxisValue(first, second, normal, normalAxisIndex, normalAxisIndex);

            CubeContactPoint3D point0 = new CubeContactPoint3D(CreatePatchPoint(normalAxisIndex, firstTangentAxisIndex, secondTangentAxisIndex, planeValue, firstTangentMinimum, secondTangentMinimum), penetration);
            CubeContactPoint3D point1 = new CubeContactPoint3D(CreatePatchPoint(normalAxisIndex, firstTangentAxisIndex, secondTangentAxisIndex, planeValue, firstTangentMinimum, secondTangentMaximum), penetration);
            CubeContactPoint3D point2 = new CubeContactPoint3D(CreatePatchPoint(normalAxisIndex, firstTangentAxisIndex, secondTangentAxisIndex, planeValue, firstTangentMaximum, secondTangentMinimum), penetration);
            CubeContactPoint3D point3 = new CubeContactPoint3D(CreatePatchPoint(normalAxisIndex, firstTangentAxisIndex, secondTangentAxisIndex, planeValue, firstTangentMaximum, secondTangentMaximum), penetration);
            manifold = new CubeContactManifold3D(normal, point0, point1, point2, point3);
            return true;
        }

        /// <summary>
        /// Resolves one fallback support-point contact position for non-axis-aligned contacts.
        /// </summary>
        /// <param name="first">First overlapping cube body.</param>
        /// <param name="second">Second overlapping cube body.</param>
        /// <param name="normal">Contact normal.</param>
        /// <returns>Midpoint between support points along the contact normal.</returns>
        static float3 ResolveFallbackContactPosition(CubeBodyState3D first, CubeBodyState3D second, float3 normal) {
            float3 firstPoint = first.GetSupportPoint(normal * -1f);
            float3 secondPoint = second.GetSupportPoint(normal);
            return (firstPoint + secondPoint) * 0.5f;
        }

        /// <summary>
        /// Creates one world-space contact patch point from a plane axis and two tangent axes.
        /// </summary>
        /// <param name="normalAxisIndex">Axis index that receives the contact plane coordinate.</param>
        /// <param name="firstTangentAxisIndex">First tangent axis index.</param>
        /// <param name="secondTangentAxisIndex">Second tangent axis index.</param>
        /// <param name="planeValue">Contact plane coordinate.</param>
        /// <param name="firstTangentValue">Coordinate on the first tangent axis.</param>
        /// <param name="secondTangentValue">Coordinate on the second tangent axis.</param>
        /// <returns>World-space contact patch point.</returns>
        static float3 CreatePatchPoint(int normalAxisIndex, int firstTangentAxisIndex, int secondTangentAxisIndex, float planeValue, float firstTangentValue, float secondTangentValue) {
            float x = ResolvePatchPointComponent(0, normalAxisIndex, firstTangentAxisIndex, secondTangentAxisIndex, planeValue, firstTangentValue, secondTangentValue);
            float y = ResolvePatchPointComponent(1, normalAxisIndex, firstTangentAxisIndex, secondTangentAxisIndex, planeValue, firstTangentValue, secondTangentValue);
            float z = ResolvePatchPointComponent(2, normalAxisIndex, firstTangentAxisIndex, secondTangentAxisIndex, planeValue, firstTangentValue, secondTangentValue);
            return new float3(x, y, z);
        }

        /// <summary>
        /// Resolves one coordinate for a contact patch point.
        /// </summary>
        /// <param name="componentIndex">Requested vector component index.</param>
        /// <param name="normalAxisIndex">Axis index that receives the contact plane coordinate.</param>
        /// <param name="firstTangentAxisIndex">First tangent axis index.</param>
        /// <param name="secondTangentAxisIndex">Second tangent axis index.</param>
        /// <param name="planeValue">Contact plane coordinate.</param>
        /// <param name="firstTangentValue">Coordinate on the first tangent axis.</param>
        /// <param name="secondTangentValue">Coordinate on the second tangent axis.</param>
        /// <returns>Selected coordinate value.</returns>
        static float ResolvePatchPointComponent(int componentIndex, int normalAxisIndex, int firstTangentAxisIndex, int secondTangentAxisIndex, float planeValue, float firstTangentValue, float secondTangentValue) {
            if (componentIndex == normalAxisIndex) {
                return planeValue;
            }
            if (componentIndex == firstTangentAxisIndex) {
                return firstTangentValue;
            }
            if (componentIndex == secondTangentAxisIndex) {
                return secondTangentValue;
            }

            throw new ArgumentOutOfRangeException(nameof(componentIndex), "Patch point component index must be 0, 1, or 2.");
        }

        /// <summary>
        /// Resolves one world-space component of the overlap patch center or plane.
        /// </summary>
        /// <param name="first">First overlapping cube body.</param>
        /// <param name="second">Second overlapping cube body.</param>
        /// <param name="normal">Unit normal pointing from the second body toward the first body.</param>
        /// <param name="normalAxisIndex">Dominant manifold normal axis.</param>
        /// <param name="componentIndex">Requested vector component index.</param>
        /// <returns>World-space component value for the overlap patch.</returns>
        static float ResolveOverlapPatchAxisValue(CubeBodyState3D first, CubeBodyState3D second, float3 normal, int normalAxisIndex, int componentIndex) {
            if (componentIndex == normalAxisIndex) {
                float direction = GetVectorComponent(normal, componentIndex);
                float center = GetVectorComponent(second.Position, componentIndex);
                float extent = GetVectorComponent(second.HalfExtents, componentIndex);
                return direction >= 0f
                    ? center + extent
                    : center - extent;
            }

            float overlapMinimum = ResolveOverlapPatchMinimum(first, second, componentIndex);
            float overlapMaximum = ResolveOverlapPatchMaximum(first, second, componentIndex);
            return (overlapMinimum + overlapMaximum) * 0.5f;
        }

        /// <summary>
        /// Resolves the minimum shared coordinate along one world axis.
        /// </summary>
        /// <param name="first">First overlapping cube body.</param>
        /// <param name="second">Second overlapping cube body.</param>
        /// <param name="componentIndex">Requested vector component index.</param>
        /// <returns>Minimum coordinate of the overlap interval.</returns>
        static float ResolveOverlapPatchMinimum(CubeBodyState3D first, CubeBodyState3D second, int componentIndex) {
            return Math.Max(
                GetVectorComponent(first.Position, componentIndex) - GetVectorComponent(first.HalfExtents, componentIndex),
                GetVectorComponent(second.Position, componentIndex) - GetVectorComponent(second.HalfExtents, componentIndex));
        }

        /// <summary>
        /// Resolves the maximum shared coordinate along one world axis.
        /// </summary>
        /// <param name="first">First overlapping cube body.</param>
        /// <param name="second">Second overlapping cube body.</param>
        /// <param name="componentIndex">Requested vector component index.</param>
        /// <returns>Maximum coordinate of the overlap interval.</returns>
        static float ResolveOverlapPatchMaximum(CubeBodyState3D first, CubeBodyState3D second, int componentIndex) {
            return Math.Min(
                GetVectorComponent(first.Position, componentIndex) + GetVectorComponent(first.HalfExtents, componentIndex),
                GetVectorComponent(second.Position, componentIndex) + GetVectorComponent(second.HalfExtents, componentIndex));
        }

        /// <summary>
        /// Resolves the first tangent axis for a world-axis-aligned contact normal.
        /// </summary>
        /// <param name="normalAxisIndex">Dominant normal axis.</param>
        /// <returns>First tangent axis index.</returns>
        static int ResolveFirstTangentAxisIndex(int normalAxisIndex) {
            if (normalAxisIndex == 0) {
                return 1;
            }
            if (normalAxisIndex == 1) {
                return 0;
            }
            if (normalAxisIndex == 2) {
                return 0;
            }

            throw new ArgumentOutOfRangeException(nameof(normalAxisIndex), "Normal axis index must be 0, 1, or 2.");
        }

        /// <summary>
        /// Resolves the second tangent axis for a world-axis-aligned contact normal.
        /// </summary>
        /// <param name="normalAxisIndex">Dominant normal axis.</param>
        /// <returns>Second tangent axis index.</returns>
        static int ResolveSecondTangentAxisIndex(int normalAxisIndex) {
            if (normalAxisIndex == 0) {
                return 2;
            }
            if (normalAxisIndex == 1) {
                return 2;
            }
            if (normalAxisIndex == 2) {
                return 1;
            }

            throw new ArgumentOutOfRangeException(nameof(normalAxisIndex), "Normal axis index must be 0, 1, or 2.");
        }

        /// <summary>
        /// Resolves one vector component by zero-based axis index.
        /// </summary>
        /// <param name="value">Vector whose component should be returned.</param>
        /// <param name="componentIndex">Zero for X, one for Y, two for Z.</param>
        /// <returns>Selected vector component.</returns>
        static float GetVectorComponent(float3 value, int componentIndex) {
            if (componentIndex == 0) {
                return value.X;
            }
            if (componentIndex == 1) {
                return value.Y;
            }
            if (componentIndex == 2) {
                return value.Z;
            }

            throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be 0, 1, or 2.");
        }

        /// <summary>
        /// Determines whether both cube bodies are upright enough for world-axis overlap patch math.
        /// </summary>
        /// <param name="first">First overlapping cube body.</param>
        /// <param name="second">Second overlapping cube body.</param>
        /// <returns>True when both bodies are nearly world-axis aligned.</returns>
        static bool CanUseAxisAlignedOverlapPatch(CubeBodyState3D first, CubeBodyState3D second) {
            return IsWorldAxisAlignedBox(first) && IsWorldAxisAlignedBox(second);
        }

        /// <summary>
        /// Determines whether one cube's local axes are close enough to world axes for patch math.
        /// </summary>
        /// <param name="bodyState">Cube body state whose orientation should be inspected.</param>
        /// <returns>True when every local axis is close to one cardinal world axis.</returns>
        static bool IsWorldAxisAlignedBox(CubeBodyState3D bodyState) {
            return IsWorldAxisAlignedVector(bodyState.ResolveBoxAxis(0)) &&
                IsWorldAxisAlignedVector(bodyState.ResolveBoxAxis(1)) &&
                IsWorldAxisAlignedVector(bodyState.ResolveBoxAxis(2));
        }

        /// <summary>
        /// Determines whether one unit vector is close to one of the six cardinal world axes.
        /// </summary>
        /// <param name="axis">Unit axis to inspect.</param>
        /// <returns>True when the axis can safely use world-axis overlap math.</returns>
        static bool IsWorldAxisAlignedVector(float3 axis) {
            float absoluteX = Math.Abs(axis.X);
            float absoluteY = Math.Abs(axis.Y);
            float absoluteZ = Math.Abs(axis.Z);
            return absoluteX >= AxisAlignedBoxContactThreshold ||
                absoluteY >= AxisAlignedBoxContactThreshold ||
                absoluteZ >= AxisAlignedBoxContactThreshold;
        }

        /// <summary>
        /// Resolves the dominant world axis of a contact normal.
        /// </summary>
        /// <param name="normal">Contact normal to inspect.</param>
        /// <returns>Zero for X, one for Y, or two for Z.</returns>
        static int ResolveDominantAxisIndex(float3 normal) {
            float absoluteX = Math.Abs(normal.X);
            float absoluteY = Math.Abs(normal.Y);
            float absoluteZ = Math.Abs(normal.Z);
            if (absoluteX >= absoluteY && absoluteX >= absoluteZ) {
                return 0;
            }
            if (absoluteY >= absoluteX && absoluteY >= absoluteZ) {
                return 1;
            }

            return 2;
        }
    }
}
