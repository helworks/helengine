#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_BOX_BOX_CONTACT
namespace helengine {
    /// <summary>
    /// Resolves dynamic or static axis-aligned box contact information.
    /// </summary>
    public static class BoxBoxContactResolver3D {
        /// <summary>
        /// Minimum up-axis alignment required before the cheap axis-aligned face manifold can represent a box-box contact patch.
        /// </summary>
        const float AxisAlignedManifoldUpThreshold = 0.95f;

        /// <summary>
        /// Distance within which separated boxes still produce a speculative contact constraint.
        /// </summary>
        const float SpeculativeContactMargin = 0.05f;

        /// <summary>
        /// Minimum squared length required before a cross-axis candidate is stable enough for SAT projection.
        /// </summary>
        const float MinimumSatAxisLengthSquared = 0.000001f;

        /// <summary>
        /// SAT candidate kind used when the first box supplies the reference face.
        /// </summary>
        const int SatAxisKindFirstFace = 0;

        /// <summary>
        /// SAT candidate kind used when the second box supplies the reference face.
        /// </summary>
        const int SatAxisKindSecondFace = 1;

        /// <summary>
        /// SAT candidate kind used when an edge-edge axis is the best separation axis.
        /// </summary>
        const int SatAxisKindEdgeEdge = 2;

        /// <summary>
        /// Finds the minimum-penetration axis for one overlapping box pair.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <param name="penetration">Positive overlap distance on the selected axis.</param>
        /// <param name="axisIndex">Zero for X, one for Y, two for Z.</param>
        /// <returns>True when the boxes overlap.</returns>
        public static bool TryResolveContact(BodyState3D first, BodyState3D second, out float penetration, out int axisIndex) {
            return TryResolveContact(first, second, SpeculativeContactMargin, out penetration, out axisIndex);
        }

        /// <summary>
        /// Finds the minimum-penetration axis for one box pair, allowing a caller-provided speculative distance.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <param name="speculativeContactMargin">Allowed nonpenetrating distance that can still create a contact constraint.</param>
        /// <param name="penetration">Signed overlap distance on the selected axis.</param>
        /// <param name="axisIndex">Zero for X, one for Y, two for Z.</param>
        /// <returns>True when the boxes overlap or are close enough for speculative contact.</returns>
        public static bool TryResolveContact(BodyState3D first, BodyState3D second, float speculativeContactMargin, out float penetration, out int axisIndex) {
            if (first == null) {
                throw new ArgumentNullException(nameof(first));
            }
            if (second == null) {
                throw new ArgumentNullException(nameof(second));
            }
            if (float.IsNaN(speculativeContactMargin) || float.IsInfinity(speculativeContactMargin) || speculativeContactMargin < 0f) {
                throw new ArgumentOutOfRangeException(nameof(speculativeContactMargin), "Speculative contact margin must be finite and nonnegative.");
            }

            float xPenetration = PrimitiveContactMath3D.CalculateAxisPenetration(first.Position.X, first.AxisAlignedHalfExtents.X, second.Position.X, second.AxisAlignedHalfExtents.X);
            float yPenetration = PrimitiveContactMath3D.CalculateAxisPenetration(first.Position.Y, first.AxisAlignedHalfExtents.Y, second.Position.Y, second.AxisAlignedHalfExtents.Y);
            float zPenetration = PrimitiveContactMath3D.CalculateAxisPenetration(first.Position.Z, first.AxisAlignedHalfExtents.Z, second.Position.Z, second.AxisAlignedHalfExtents.Z);
            if (xPenetration < -speculativeContactMargin || yPenetration < -speculativeContactMargin || zPenetration < -speculativeContactMargin) {
                penetration = 0f;
                axisIndex = -1;
                return false;
            }
            if (ShouldPreferVerticalSupportAxis(first, second, xPenetration, yPenetration, zPenetration)) {
                penetration = yPenetration;
                axisIndex = 1;
                return true;
            }

            if (xPenetration <= yPenetration && xPenetration <= zPenetration) {
                penetration = xPenetration;
                axisIndex = 0;
                return true;
            }
            if (yPenetration <= zPenetration) {
                penetration = yPenetration;
                axisIndex = 1;
                return true;
            }

            penetration = zPenetration;
            axisIndex = 2;
            return true;
        }

        /// <summary>
        /// Determines whether an upright overlapping pair should keep vertical support instead of switching to a side axis at the edge of the footprint.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <param name="xPenetration">Signed X overlap.</param>
        /// <param name="yPenetration">Signed Y overlap.</param>
        /// <param name="zPenetration">Signed Z overlap.</param>
        /// <returns>True when the Y axis should represent a stacked support contact.</returns>
        static bool ShouldPreferVerticalSupportAxis(BodyState3D first, BodyState3D second, float xPenetration, float yPenetration, float zPenetration) {
            if (!CanUseAxisAlignedFaceManifold(first, second)) {
                return false;
            }
            if (xPenetration <= 0f || yPenetration <= 0f || zPenetration <= 0f) {
                return false;
            }

            float verticalCenterDistance = Math.Abs(first.Position.Y - second.Position.Y);
            float supportThreshold = Math.Min(first.AxisAlignedHalfExtents.Y, second.AxisAlignedHalfExtents.Y) * 0.5f;
            return verticalCenterDistance > supportThreshold;
        }

        /// <summary>
        /// Builds a four-contact face manifold for one overlapping axis-aligned box pair.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <param name="manifold">Contact manifold describing the overlap patch when the boxes overlap.</param>
        /// <returns>True when the boxes overlap and a contact manifold was produced.</returns>
        public static bool TryResolveManifold(BodyState3D first, BodyState3D second, out BoxBoxContactManifold3D manifold) {
            return TryResolveManifold(first, second, SpeculativeContactMargin, out manifold);
        }

        /// <summary>
        /// Builds a four-contact face manifold for one box pair, allowing a caller-provided speculative distance.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <param name="speculativeContactMargin">Allowed nonpenetrating distance that can still create a contact constraint.</param>
        /// <param name="manifold">Contact manifold describing the overlap patch when the boxes overlap or are close enough for speculative contact.</param>
        /// <returns>True when a contact manifold was produced.</returns>
        public static bool TryResolveManifold(BodyState3D first, BodyState3D second, float speculativeContactMargin, out BoxBoxContactManifold3D manifold) {
            if (first == null) {
                throw new ArgumentNullException(nameof(first));
            }
            if (second == null) {
                throw new ArgumentNullException(nameof(second));
            }
            if (!CanUseAxisAlignedFaceManifold(first, second)) {
                manifold = new BoxBoxContactManifold3D();
                return false;
            }

            if (!TryResolveContact(first, second, speculativeContactMargin, out float penetration, out int axisIndex)) {
                manifold = new BoxBoxContactManifold3D();
                return false;
            }

            float axisDirection = PrimitiveContactMath3D.GetAxisDirection(first, second, axisIndex);
            float3 normal = CreateAxisNormal(axisIndex, axisDirection);
            float planeValue = ResolveContactPlaneValue(first, normal, axisIndex);
            int firstTangentAxis = ResolveFirstTangentAxis(axisIndex);
            int secondTangentAxis = ResolveSecondTangentAxis(axisIndex);
            float firstTangentMinimum = ResolveOverlapMinimum(first, second, firstTangentAxis);
            float firstTangentMaximum = ResolveOverlapMaximum(first, second, firstTangentAxis);
            float secondTangentMinimum = ResolveOverlapMinimum(first, second, secondTangentAxis);
            float secondTangentMaximum = ResolveOverlapMaximum(first, second, secondTangentAxis);

            manifold = new BoxBoxContactManifold3D {
                Normal = normal,
                Penetration = penetration,
                Penetration0 = penetration,
                Penetration1 = penetration,
                Penetration2 = penetration,
                Penetration3 = penetration,
                Contact0 = CreateContactPoint(axisIndex, planeValue, firstTangentAxis, firstTangentMinimum, secondTangentAxis, secondTangentMinimum),
                Contact1 = CreateContactPoint(axisIndex, planeValue, firstTangentAxis, firstTangentMaximum, secondTangentAxis, secondTangentMinimum),
                Contact2 = CreateContactPoint(axisIndex, planeValue, firstTangentAxis, firstTangentMinimum, secondTangentAxis, secondTangentMaximum),
                Contact3 = CreateContactPoint(axisIndex, planeValue, firstTangentAxis, firstTangentMaximum, secondTangentAxis, secondTangentMaximum),
                FeatureId0 = 0,
                FeatureId1 = 1,
                FeatureId2 = 2,
                FeatureId3 = 3,
                ContactCount = 4
            };
            return true;
        }

        /// <summary>
        /// Builds a one-point oriented-box contact manifold using SAT axes when an axis-aligned face patch cannot represent the pair.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <param name="speculativeContactMargin">Allowed nonpenetrating distance that can still create a contact constraint.</param>
        /// <param name="manifold">Contact manifold describing the closest oriented-box feature pair.</param>
        /// <returns>True when an oriented contact was produced.</returns>
        public static bool TryResolveOrientedManifold(BodyState3D first, BodyState3D second, float speculativeContactMargin, out BoxBoxContactManifold3D manifold) {
            if (first == null) {
                throw new ArgumentNullException(nameof(first));
            }
            if (second == null) {
                throw new ArgumentNullException(nameof(second));
            }
            if (float.IsNaN(speculativeContactMargin) || float.IsInfinity(speculativeContactMargin) || speculativeContactMargin < 0f) {
                throw new ArgumentOutOfRangeException(nameof(speculativeContactMargin), "Speculative contact margin must be finite and nonnegative.");
            }

            float3 firstAxisX = ResolveBoxAxis(first, 0);
            float3 firstAxisY = ResolveBoxAxis(first, 1);
            float3 firstAxisZ = ResolveBoxAxis(first, 2);
            float3 secondAxisX = ResolveBoxAxis(second, 0);
            float3 secondAxisY = ResolveBoxAxis(second, 1);
            float3 secondAxisZ = ResolveBoxAxis(second, 2);
            float3 centerOffset = first.Position - second.Position;
            float smallestPenetration = float.MaxValue;
            float3 bestNormal = float3.Zero;
            int bestAxisKind = SatAxisKindFirstFace;
            int bestAxisIndex = 0;

            if (!TryEvaluateSatAxis(first, second, centerOffset, firstAxisX, speculativeContactMargin, SatAxisKindFirstFace, 0, ref smallestPenetration, ref bestNormal, ref bestAxisKind, ref bestAxisIndex)) {
                manifold = new BoxBoxContactManifold3D();
                return false;
            }
            if (!TryEvaluateSatAxis(first, second, centerOffset, firstAxisY, speculativeContactMargin, SatAxisKindFirstFace, 1, ref smallestPenetration, ref bestNormal, ref bestAxisKind, ref bestAxisIndex)) {
                manifold = new BoxBoxContactManifold3D();
                return false;
            }
            if (!TryEvaluateSatAxis(first, second, centerOffset, firstAxisZ, speculativeContactMargin, SatAxisKindFirstFace, 2, ref smallestPenetration, ref bestNormal, ref bestAxisKind, ref bestAxisIndex)) {
                manifold = new BoxBoxContactManifold3D();
                return false;
            }
            if (!TryEvaluateSatAxis(first, second, centerOffset, secondAxisX, speculativeContactMargin, SatAxisKindSecondFace, 0, ref smallestPenetration, ref bestNormal, ref bestAxisKind, ref bestAxisIndex)) {
                manifold = new BoxBoxContactManifold3D();
                return false;
            }
            if (!TryEvaluateSatAxis(first, second, centerOffset, secondAxisY, speculativeContactMargin, SatAxisKindSecondFace, 1, ref smallestPenetration, ref bestNormal, ref bestAxisKind, ref bestAxisIndex)) {
                manifold = new BoxBoxContactManifold3D();
                return false;
            }
            if (!TryEvaluateSatAxis(first, second, centerOffset, secondAxisZ, speculativeContactMargin, SatAxisKindSecondFace, 2, ref smallestPenetration, ref bestNormal, ref bestAxisKind, ref bestAxisIndex)) {
                manifold = new BoxBoxContactManifold3D();
                return false;
            }
            if (!TryEvaluateSatCrossAxis(first, second, centerOffset, firstAxisX, secondAxisX, speculativeContactMargin, ref smallestPenetration, ref bestNormal, ref bestAxisKind, ref bestAxisIndex)) {
                manifold = new BoxBoxContactManifold3D();
                return false;
            }
            if (!TryEvaluateSatCrossAxis(first, second, centerOffset, firstAxisX, secondAxisY, speculativeContactMargin, ref smallestPenetration, ref bestNormal, ref bestAxisKind, ref bestAxisIndex)) {
                manifold = new BoxBoxContactManifold3D();
                return false;
            }
            if (!TryEvaluateSatCrossAxis(first, second, centerOffset, firstAxisX, secondAxisZ, speculativeContactMargin, ref smallestPenetration, ref bestNormal, ref bestAxisKind, ref bestAxisIndex)) {
                manifold = new BoxBoxContactManifold3D();
                return false;
            }
            if (!TryEvaluateSatCrossAxis(first, second, centerOffset, firstAxisY, secondAxisX, speculativeContactMargin, ref smallestPenetration, ref bestNormal, ref bestAxisKind, ref bestAxisIndex)) {
                manifold = new BoxBoxContactManifold3D();
                return false;
            }
            if (!TryEvaluateSatCrossAxis(first, second, centerOffset, firstAxisY, secondAxisY, speculativeContactMargin, ref smallestPenetration, ref bestNormal, ref bestAxisKind, ref bestAxisIndex)) {
                manifold = new BoxBoxContactManifold3D();
                return false;
            }
            if (!TryEvaluateSatCrossAxis(first, second, centerOffset, firstAxisY, secondAxisZ, speculativeContactMargin, ref smallestPenetration, ref bestNormal, ref bestAxisKind, ref bestAxisIndex)) {
                manifold = new BoxBoxContactManifold3D();
                return false;
            }
            if (!TryEvaluateSatCrossAxis(first, second, centerOffset, firstAxisZ, secondAxisX, speculativeContactMargin, ref smallestPenetration, ref bestNormal, ref bestAxisKind, ref bestAxisIndex)) {
                manifold = new BoxBoxContactManifold3D();
                return false;
            }
            if (!TryEvaluateSatCrossAxis(first, second, centerOffset, firstAxisZ, secondAxisY, speculativeContactMargin, ref smallestPenetration, ref bestNormal, ref bestAxisKind, ref bestAxisIndex)) {
                manifold = new BoxBoxContactManifold3D();
                return false;
            }
            if (!TryEvaluateSatCrossAxis(first, second, centerOffset, firstAxisZ, secondAxisZ, speculativeContactMargin, ref smallestPenetration, ref bestNormal, ref bestAxisKind, ref bestAxisIndex)) {
                manifold = new BoxBoxContactManifold3D();
                return false;
            }

            if (bestAxisKind == SatAxisKindFirstFace && TryBuildClippedFaceManifold(first, second, bestNormal, smallestPenetration, speculativeContactMargin, true, bestAxisIndex, out manifold)) {
                return true;
            }
            if (bestAxisKind == SatAxisKindSecondFace && TryBuildClippedFaceManifold(first, second, bestNormal, smallestPenetration, speculativeContactMargin, false, bestAxisIndex, out manifold)) {
                return true;
            }

            float3 firstSupport = first.GetSupportPoint(bestNormal * -1f);
            float3 secondSupport = second.GetSupportPoint(bestNormal);
            manifold = new BoxBoxContactManifold3D {
                Normal = bestNormal,
                Penetration = smallestPenetration,
                Penetration0 = smallestPenetration,
                Contact0 = (firstSupport + secondSupport) * 0.5f,
                FeatureId0 = 200,
                ContactCount = 1
            };
            return true;
        }

        /// <summary>
        /// Evaluates one normalized SAT axis and updates the closest contact normal.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <param name="centerOffset">Vector from the second center to the first center.</param>
        /// <param name="axis">Unit axis to project onto.</param>
        /// <param name="speculativeContactMargin">Allowed nonpenetrating distance.</param>
        /// <param name="axisKind">Kind of SAT candidate being evaluated.</param>
        /// <param name="axisIndex">Local axis index for face candidates.</param>
        /// <param name="smallestPenetration">Smallest signed overlap found so far.</param>
        /// <param name="bestNormal">Best normal found so far, pointing from the second body toward the first body.</param>
        /// <param name="bestAxisKind">Kind associated with the best normal.</param>
        /// <param name="bestAxisIndex">Local axis index associated with the best normal.</param>
        /// <returns>False when this axis separates the boxes beyond the speculative margin.</returns>
        static bool TryEvaluateSatAxis(
            BodyState3D first,
            BodyState3D second,
            float3 centerOffset,
            float3 axis,
            float speculativeContactMargin,
            int axisKind,
            int axisIndex,
            ref float smallestPenetration,
            ref float3 bestNormal,
            ref int bestAxisKind,
            ref int bestAxisIndex) {
            float firstRadius = ResolveProjectionRadius(first, axis);
            float secondRadius = ResolveProjectionRadius(second, axis);
            float centerDistance = float3.Dot(centerOffset, axis);
            float penetration = firstRadius + secondRadius - Math.Abs(centerDistance);
            if (penetration < -speculativeContactMargin) {
                return false;
            }
            if (penetration < smallestPenetration) {
                smallestPenetration = penetration;
                bestNormal = centerDistance >= 0f ? axis : axis * -1f;
                bestAxisKind = axisKind;
                bestAxisIndex = axisIndex;
            }

            return true;
        }

        /// <summary>
        /// Evaluates one SAT cross product axis when the two source axes are not parallel.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <param name="centerOffset">Vector from the second center to the first center.</param>
        /// <param name="firstAxis">First box axis used to build the cross axis.</param>
        /// <param name="secondAxis">Second box axis used to build the cross axis.</param>
        /// <param name="speculativeContactMargin">Allowed nonpenetrating distance.</param>
        /// <param name="smallestPenetration">Smallest signed overlap found so far.</param>
        /// <param name="bestNormal">Best normal found so far, pointing from the second body toward the first body.</param>
        /// <param name="bestAxisKind">Kind associated with the best normal.</param>
        /// <param name="bestAxisIndex">Local axis index associated with the best normal.</param>
        /// <returns>False when this axis separates the boxes beyond the speculative margin.</returns>
        static bool TryEvaluateSatCrossAxis(
            BodyState3D first,
            BodyState3D second,
            float3 centerOffset,
            float3 firstAxis,
            float3 secondAxis,
            float speculativeContactMargin,
            ref float smallestPenetration,
            ref float3 bestNormal,
            ref int bestAxisKind,
            ref int bestAxisIndex) {
            float3 axis = float3.Cross(firstAxis, secondAxis);
            float axisLengthSquared = float3.Dot(axis, axis);
            if (axisLengthSquared <= MinimumSatAxisLengthSquared) {
                return true;
            }

            axis = axis / (float)Math.Sqrt(axisLengthSquared);
            return TryEvaluateSatAxis(first, second, centerOffset, axis, speculativeContactMargin, SatAxisKindEdgeEdge, -1, ref smallestPenetration, ref bestNormal, ref bestAxisKind, ref bestAxisIndex);
        }

        /// <summary>
        /// Builds a clipped four-point face manifold for an oriented box pair.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <param name="normal">Contact normal pointing from the second box toward the first box.</param>
        /// <param name="penetration">Signed SAT penetration depth.</param>
        /// <param name="referenceIsFirst">True when the first box owns the reference face.</param>
        /// <param name="referenceAxisIndex">Local face axis used by the reference box.</param>
        /// <param name="manifold">Resolved contact manifold.</param>
        /// <returns>True when at least one clipped face contact was produced.</returns>
        static bool TryBuildClippedFaceManifold(
            BodyState3D first,
            BodyState3D second,
            float3 normal,
            float penetration,
            float speculativeContactMargin,
            bool referenceIsFirst,
            int referenceAxisIndex,
            out BoxBoxContactManifold3D manifold) {
            BodyState3D reference = referenceIsFirst ? first : second;
            BodyState3D incident = referenceIsFirst ? second : first;
            float3 referencePlaneDirection = referenceIsFirst ? normal * -1f : normal;
            float3 incidentFaceDirection = referenceIsFirst ? normal : normal * -1f;
            int firstTangentIndex = ResolveFirstOrientedTangentAxis(referenceAxisIndex);
            int secondTangentIndex = ResolveSecondOrientedTangentAxis(referenceAxisIndex);
            float3 referenceTangentX = ResolveBoxAxis(reference, firstTangentIndex);
            float3 referenceTangentY = ResolveBoxAxis(reference, secondTangentIndex);
            float referenceHalfSpanX = GetComponent(reference.HalfExtents, firstTangentIndex);
            float referenceHalfSpanY = GetComponent(reference.HalfExtents, secondTangentIndex);
            float3 planePoint = reference.GetSupportPoint(referencePlaneDirection);
            float3[] incidentVertices = CreateIncidentFaceVertices(incident, incidentFaceDirection);
            float[] inputX = new float[8];
            float[] inputY = new float[8];
            float[] inputDepth = new float[8];
            float[] clipX = new float[8];
            float[] clipY = new float[8];
            float[] clipDepth = new float[8];
            float[] outputX = new float[8];
            float[] outputY = new float[8];
            float[] outputDepth = new float[8];
            int inputCount = ProjectIncidentFaceToReferencePlane(incidentVertices, planePoint, normal, referenceIsFirst, referenceTangentX, referenceTangentY, inputX, inputY, inputDepth);
            int clipCount = ClipPolygonAgainstMaximum(inputX, inputY, inputDepth, inputCount, 0, referenceHalfSpanX, clipX, clipY, clipDepth);
            inputCount = ClipPolygonAgainstMinimum(clipX, clipY, clipDepth, clipCount, 0, -referenceHalfSpanX, outputX, outputY, outputDepth);
            clipCount = ClipPolygonAgainstMaximum(outputX, outputY, outputDepth, inputCount, 1, referenceHalfSpanY, clipX, clipY, clipDepth);
            inputCount = ClipPolygonAgainstMinimum(clipX, clipY, clipDepth, clipCount, 1, -referenceHalfSpanY, outputX, outputY, outputDepth);
            if (inputCount <= 0) {
                manifold = new BoxBoxContactManifold3D();
                return false;
            }

            manifold = CreateClippedManifold(normal, penetration, planePoint, referenceTangentX, referenceTangentY, outputX, outputY, outputDepth, inputCount);
            return true;
        }

        /// <summary>
        /// Creates the manifold from clipped two-dimensional face coordinates.
        /// </summary>
        /// <param name="normal">Contact normal pointing from the second box toward the first box.</param>
        /// <param name="penetration">Signed SAT penetration depth.</param>
        /// <param name="planePoint">Reference face point.</param>
        /// <param name="tangentX">First reference face tangent.</param>
        /// <param name="tangentY">Second reference face tangent.</param>
        /// <param name="pointsX">Clipped X coordinates.</param>
        /// <param name="pointsY">Clipped Y coordinates.</param>
        /// <param name="pointDepths">Clipped contact depths.</param>
        /// <param name="pointCount">Number of clipped coordinates.</param>
        /// <returns>Contact manifold containing up to four clipped points.</returns>
        static BoxBoxContactManifold3D CreateClippedManifold(
            float3 normal,
            float penetration,
            float3 planePoint,
            float3 tangentX,
            float3 tangentY,
            float[] pointsX,
            float[] pointsY,
            float[] pointDepths,
            int pointCount) {
            int contactCount = Math.Min(4, pointCount);
            BoxBoxContactManifold3D manifold = new BoxBoxContactManifold3D {
                Normal = normal,
                Penetration = penetration,
                ContactCount = contactCount
            };

            for (int contactIndex = 0; contactIndex < contactCount; contactIndex++) {
                StoreContactPoint(ref manifold, contactIndex, planePoint + (tangentX * pointsX[contactIndex]) + (tangentY * pointsY[contactIndex]));
                StoreContactFeatureId(ref manifold, contactIndex, 300 + contactIndex);
                StoreContactPenetration(ref manifold, contactIndex, pointDepths[contactIndex]);
            }

            return manifold;
        }

        /// <summary>
        /// Stores one contact point into a manifold by index.
        /// </summary>
        /// <param name="manifold">Manifold being written.</param>
        /// <param name="contactIndex">Contact index to write.</param>
        /// <param name="contactPoint">World-space contact point.</param>
        static void StoreContactPoint(ref BoxBoxContactManifold3D manifold, int contactIndex, float3 contactPoint) {
            if (contactIndex == 0) {
                manifold.Contact0 = contactPoint;
                return;
            }
            if (contactIndex == 1) {
                manifold.Contact1 = contactPoint;
                return;
            }
            if (contactIndex == 2) {
                manifold.Contact2 = contactPoint;
                return;
            }
            if (contactIndex == 3) {
                manifold.Contact3 = contactPoint;
                return;
            }

            throw new ArgumentOutOfRangeException(nameof(contactIndex), "Contact index must be between zero and three.");
        }

        /// <summary>
        /// Stores one contact feature id into a manifold by index.
        /// </summary>
        /// <param name="manifold">Manifold being written.</param>
        /// <param name="contactIndex">Contact index to write.</param>
        /// <param name="featureId">Stable feature id for the contact.</param>
        static void StoreContactFeatureId(ref BoxBoxContactManifold3D manifold, int contactIndex, int featureId) {
            if (contactIndex == 0) {
                manifold.FeatureId0 = featureId;
                return;
            }
            if (contactIndex == 1) {
                manifold.FeatureId1 = featureId;
                return;
            }
            if (contactIndex == 2) {
                manifold.FeatureId2 = featureId;
                return;
            }
            if (contactIndex == 3) {
                manifold.FeatureId3 = featureId;
                return;
            }

            throw new ArgumentOutOfRangeException(nameof(contactIndex), "Contact index must be between zero and three.");
        }

        /// <summary>
        /// Stores one signed contact penetration into a manifold by index.
        /// </summary>
        /// <param name="manifold">Manifold being written.</param>
        /// <param name="contactIndex">Contact index to write.</param>
        /// <param name="penetration">Signed contact penetration depth.</param>
        static void StoreContactPenetration(ref BoxBoxContactManifold3D manifold, int contactIndex, float penetration) {
            if (contactIndex == 0) {
                manifold.Penetration0 = penetration;
                return;
            }
            if (contactIndex == 1) {
                manifold.Penetration1 = penetration;
                return;
            }
            if (contactIndex == 2) {
                manifold.Penetration2 = penetration;
                return;
            }
            if (contactIndex == 3) {
                manifold.Penetration3 = penetration;
                return;
            }

            throw new ArgumentOutOfRangeException(nameof(contactIndex), "Contact index must be between zero and three.");
        }

        /// <summary>
        /// Projects incident face vertices onto the reference contact plane.
        /// </summary>
        /// <param name="incidentVertices">Incident face vertices.</param>
        /// <param name="planePoint">Reference face point.</param>
        /// <param name="normal">Contact normal.</param>
        /// <param name="referenceIsFirst">True when the first box owns the reference face.</param>
        /// <param name="tangentX">First reference face tangent.</param>
        /// <param name="tangentY">Second reference face tangent.</param>
        /// <param name="pointsX">Output X coordinates.</param>
        /// <param name="pointsY">Output Y coordinates.</param>
        /// <param name="pointDepths">Output signed contact depths.</param>
        /// <returns>Number of projected vertices.</returns>
        static int ProjectIncidentFaceToReferencePlane(
            float3[] incidentVertices,
            float3 planePoint,
            float3 normal,
            bool referenceIsFirst,
            float3 tangentX,
            float3 tangentY,
            float[] pointsX,
            float[] pointsY,
            float[] pointDepths) {
            for (int index = 0; index < incidentVertices.Length; index++) {
                float3 vertex = incidentVertices[index];
                float distance = float3.Dot(vertex - planePoint, normal);
                float3 projected = vertex - (normal * distance);
                float3 offset = projected - planePoint;
                pointsX[index] = float3.Dot(offset, tangentX);
                pointsY[index] = float3.Dot(offset, tangentY);
                pointDepths[index] = referenceIsFirst ? distance : -distance;
            }

            return incidentVertices.Length;
        }

        /// <summary>
        /// Creates the four vertices on the incident box face most directly facing the reference face.
        /// </summary>
        /// <param name="bodyState">Incident box body.</param>
        /// <param name="faceDirection">World-space direction selecting the incident face.</param>
        /// <returns>Four incident face vertices.</returns>
        static float3[] CreateIncidentFaceVertices(BodyState3D bodyState, float3 faceDirection) {
            int faceAxisIndex = ResolveMostAlignedAxisIndex(bodyState, faceDirection);
            int firstTangentIndex = ResolveFirstOrientedTangentAxis(faceAxisIndex);
            int secondTangentIndex = ResolveSecondOrientedTangentAxis(faceAxisIndex);
            float3 faceAxis = ResolveBoxAxis(bodyState, faceAxisIndex);
            float faceSign = float3.Dot(faceDirection, faceAxis) >= 0f ? 1f : -1f;
            float3 faceCenter = bodyState.Position + (faceAxis * (GetComponent(bodyState.HalfExtents, faceAxisIndex) * faceSign));
            float3 tangentX = ResolveBoxAxis(bodyState, firstTangentIndex) * GetComponent(bodyState.HalfExtents, firstTangentIndex);
            float3 tangentY = ResolveBoxAxis(bodyState, secondTangentIndex) * GetComponent(bodyState.HalfExtents, secondTangentIndex);
            return new[] {
                faceCenter - tangentX - tangentY,
                faceCenter + tangentX - tangentY,
                faceCenter + tangentX + tangentY,
                faceCenter - tangentX + tangentY
            };
        }

        /// <summary>
        /// Finds the local box axis most aligned with a world-space direction.
        /// </summary>
        /// <param name="bodyState">Box body to inspect.</param>
        /// <param name="direction">World-space face selection direction.</param>
        /// <returns>Local axis index with the largest absolute dot product.</returns>
        static int ResolveMostAlignedAxisIndex(BodyState3D bodyState, float3 direction) {
            float x = Math.Abs(float3.Dot(direction, ResolveBoxAxis(bodyState, 0)));
            float y = Math.Abs(float3.Dot(direction, ResolveBoxAxis(bodyState, 1)));
            float z = Math.Abs(float3.Dot(direction, ResolveBoxAxis(bodyState, 2)));
            if (x >= y && x >= z) {
                return 0;
            }
            if (y >= z) {
                return 1;
            }

            return 2;
        }

        /// <summary>
        /// Clips a polygon against an upper bound on one projected axis.
        /// </summary>
        /// <param name="inputX">Input X coordinates.</param>
        /// <param name="inputY">Input Y coordinates.</param>
        /// <param name="inputCount">Input vertex count.</param>
        /// <param name="axisIndex">Zero for X, one for Y.</param>
        /// <param name="maximum">Maximum retained coordinate.</param>
        /// <param name="outputX">Output X coordinates.</param>
        /// <param name="outputY">Output Y coordinates.</param>
        /// <returns>Output vertex count.</returns>
        static int ClipPolygonAgainstMaximum(float[] inputX, float[] inputY, float[] inputDepth, int inputCount, int axisIndex, float maximum, float[] outputX, float[] outputY, float[] outputDepth) {
            return ClipPolygonAgainstPlane(inputX, inputY, inputDepth, inputCount, axisIndex, maximum, true, outputX, outputY, outputDepth);
        }

        /// <summary>
        /// Clips a polygon against a lower bound on one projected axis.
        /// </summary>
        /// <param name="inputX">Input X coordinates.</param>
        /// <param name="inputY">Input Y coordinates.</param>
        /// <param name="inputCount">Input vertex count.</param>
        /// <param name="axisIndex">Zero for X, one for Y.</param>
        /// <param name="minimum">Minimum retained coordinate.</param>
        /// <param name="outputX">Output X coordinates.</param>
        /// <param name="outputY">Output Y coordinates.</param>
        /// <returns>Output vertex count.</returns>
        static int ClipPolygonAgainstMinimum(float[] inputX, float[] inputY, float[] inputDepth, int inputCount, int axisIndex, float minimum, float[] outputX, float[] outputY, float[] outputDepth) {
            return ClipPolygonAgainstPlane(inputX, inputY, inputDepth, inputCount, axisIndex, minimum, false, outputX, outputY, outputDepth);
        }

        /// <summary>
        /// Clips a projected polygon against one axis-aligned half space.
        /// </summary>
        /// <param name="inputX">Input X coordinates.</param>
        /// <param name="inputY">Input Y coordinates.</param>
        /// <param name="inputDepth">Input signed depths.</param>
        /// <param name="inputCount">Input vertex count.</param>
        /// <param name="axisIndex">Zero for X, one for Y.</param>
        /// <param name="limit">Half-space coordinate limit.</param>
        /// <param name="keepBelow">True to keep values less than the limit; false to keep values greater.</param>
        /// <param name="outputX">Output X coordinates.</param>
        /// <param name="outputY">Output Y coordinates.</param>
        /// <param name="outputDepth">Output signed depths.</param>
        /// <returns>Output vertex count.</returns>
        static int ClipPolygonAgainstPlane(float[] inputX, float[] inputY, float[] inputDepth, int inputCount, int axisIndex, float limit, bool keepBelow, float[] outputX, float[] outputY, float[] outputDepth) {
            if (inputCount <= 0) {
                return 0;
            }

            int outputCount = 0;
            float previousX = inputX[inputCount - 1];
            float previousY = inputY[inputCount - 1];
            float previousDepth = inputDepth[inputCount - 1];
            bool previousInside = IsInsideClipPlane(previousX, previousY, axisIndex, limit, keepBelow);
            for (int index = 0; index < inputCount; index++) {
                float currentX = inputX[index];
                float currentY = inputY[index];
                float currentDepth = inputDepth[index];
                bool currentInside = IsInsideClipPlane(currentX, currentY, axisIndex, limit, keepBelow);
                if (currentInside != previousInside) {
                    outputCount = StoreClipIntersection(previousX, previousY, previousDepth, currentX, currentY, currentDepth, axisIndex, limit, outputX, outputY, outputDepth, outputCount);
                }
                if (currentInside) {
                    outputX[outputCount] = currentX;
                    outputY[outputCount] = currentY;
                    outputDepth[outputCount] = currentDepth;
                    outputCount++;
                }

                previousX = currentX;
                previousY = currentY;
                previousDepth = currentDepth;
                previousInside = currentInside;
            }

            return outputCount;
        }

        /// <summary>
        /// Returns whether one projected point is inside a clipping half-space.
        /// </summary>
        /// <param name="x">Point X coordinate.</param>
        /// <param name="y">Point Y coordinate.</param>
        /// <param name="axisIndex">Zero for X, one for Y.</param>
        /// <param name="limit">Half-space coordinate limit.</param>
        /// <param name="keepBelow">True to keep values less than the limit; false to keep values greater.</param>
        /// <returns>True when the point is retained.</returns>
        static bool IsInsideClipPlane(float x, float y, int axisIndex, float limit, bool keepBelow) {
            float value = axisIndex == 0 ? x : y;
            return keepBelow ? value <= limit : value >= limit;
        }

        /// <summary>
        /// Stores the intersection point where a polygon edge crosses one clipping plane.
        /// </summary>
        /// <param name="startX">Start point X coordinate.</param>
        /// <param name="startY">Start point Y coordinate.</param>
        /// <param name="startDepth">Start point signed depth.</param>
        /// <param name="endX">End point X coordinate.</param>
        /// <param name="endY">End point Y coordinate.</param>
        /// <param name="endDepth">End point signed depth.</param>
        /// <param name="axisIndex">Zero for X, one for Y.</param>
        /// <param name="limit">Clipping plane coordinate.</param>
        /// <param name="outputX">Output X coordinates.</param>
        /// <param name="outputY">Output Y coordinates.</param>
        /// <param name="outputDepth">Output signed depths.</param>
        /// <param name="outputCount">Current output vertex count.</param>
        /// <returns>Updated output vertex count.</returns>
        static int StoreClipIntersection(float startX, float startY, float startDepth, float endX, float endY, float endDepth, int axisIndex, float limit, float[] outputX, float[] outputY, float[] outputDepth, int outputCount) {
            float startValue = axisIndex == 0 ? startX : startY;
            float endValue = axisIndex == 0 ? endX : endY;
            float denominator = endValue - startValue;
            float amount = Math.Abs(denominator) <= 0.000001f ? 0f : (limit - startValue) / denominator;
            outputX[outputCount] = startX + ((endX - startX) * amount);
            outputY[outputCount] = startY + ((endY - startY) * amount);
            outputDepth[outputCount] = startDepth + ((endDepth - startDepth) * amount);
            return outputCount + 1;
        }

        /// <summary>
        /// Resolves the first local tangent axis for an oriented box face.
        /// </summary>
        /// <param name="axisIndex">Face axis index.</param>
        /// <returns>First tangent axis index.</returns>
        static int ResolveFirstOrientedTangentAxis(int axisIndex) {
            if (axisIndex == 0) {
                return 1;
            }
            if (axisIndex == 1) {
                return 0;
            }
            if (axisIndex == 2) {
                return 0;
            }

            throw new ArgumentOutOfRangeException(nameof(axisIndex), "Axis index must be between zero and two.");
        }

        /// <summary>
        /// Resolves the second local tangent axis for an oriented box face.
        /// </summary>
        /// <param name="axisIndex">Face axis index.</param>
        /// <returns>Second tangent axis index.</returns>
        static int ResolveSecondOrientedTangentAxis(int axisIndex) {
            if (axisIndex == 0) {
                return 2;
            }
            if (axisIndex == 1) {
                return 2;
            }
            if (axisIndex == 2) {
                return 1;
            }

            throw new ArgumentOutOfRangeException(nameof(axisIndex), "Axis index must be between zero and two.");
        }

        /// <summary>
        /// Resolves one world-space local axis for an oriented box.
        /// </summary>
        /// <param name="bodyState">Box body state to inspect.</param>
        /// <param name="axisIndex">Zero for local X, one for local Y, two for local Z.</param>
        /// <returns>World-space unit axis.</returns>
        static float3 ResolveBoxAxis(BodyState3D bodyState, int axisIndex) {
            if (axisIndex == 0) {
                return float4.RotateVector(new float3(1f, 0f, 0f), bodyState.Orientation);
            }
            if (axisIndex == 1) {
                return float4.RotateVector(new float3(0f, 1f, 0f), bodyState.Orientation);
            }
            if (axisIndex == 2) {
                return float4.RotateVector(new float3(0f, 0f, 1f), bodyState.Orientation);
            }

            throw new ArgumentOutOfRangeException(nameof(axisIndex), "Axis index must be between zero and two.");
        }

        /// <summary>
        /// Projects an oriented box radius onto one world-space axis.
        /// </summary>
        /// <param name="bodyState">Box body state to project.</param>
        /// <param name="axis">Unit axis receiving the projection.</param>
        /// <returns>Positive projection radius.</returns>
        static float ResolveProjectionRadius(BodyState3D bodyState, float3 axis) {
            float3 boxAxisX = ResolveBoxAxis(bodyState, 0);
            float3 boxAxisY = ResolveBoxAxis(bodyState, 1);
            float3 boxAxisZ = ResolveBoxAxis(bodyState, 2);
            return (Math.Abs(float3.Dot(axis, boxAxisX)) * bodyState.HalfExtents.X) +
                (Math.Abs(float3.Dot(axis, boxAxisY)) * bodyState.HalfExtents.Y) +
                (Math.Abs(float3.Dot(axis, boxAxisZ)) * bodyState.HalfExtents.Z);
        }

        /// <summary>
        /// Returns whether the two boxes are upright enough for the axis-aligned overlap patch to behave like a face manifold.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <returns>True when both box up axes are close to world up; otherwise false.</returns>
        static bool CanUseAxisAlignedFaceManifold(BodyState3D first, BodyState3D second) {
            return IsUprightBox(first) && IsUprightBox(second);
        }

        /// <summary>
        /// Returns whether one box is close enough to upright for axis-aligned face contact generation.
        /// </summary>
        /// <param name="bodyState">Box body state to inspect.</param>
        /// <returns>True when the local up axis is closely aligned with world up.</returns>
        static bool IsUprightBox(BodyState3D bodyState) {
            float3 up = float4.RotateVector(new float3(0f, 1f, 0f), bodyState.Orientation);
            return up.Y >= AxisAlignedManifoldUpThreshold;
        }

        /// <summary>
        /// Builds one axis-aligned normal from a solver axis index and sign.
        /// </summary>
        /// <param name="axisIndex">Zero for X, one for Y, two for Z.</param>
        /// <param name="axisDirection">Signed direction along the selected axis.</param>
        /// <returns>Axis-aligned unit normal.</returns>
        static float3 CreateAxisNormal(int axisIndex, float axisDirection) {
            if (axisIndex == 0) {
                return new float3(axisDirection, 0f, 0f);
            }
            if (axisIndex == 1) {
                return new float3(0f, axisDirection, 0f);
            }
            if (axisIndex == 2) {
                return new float3(0f, 0f, axisDirection);
            }

            throw new ArgumentOutOfRangeException(nameof(axisIndex), "Axis index must be between zero and two.");
        }

        /// <summary>
        /// Resolves the contact plane coordinate on the first box face touched by the second box.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="normal">Contact normal pointing away from the second box.</param>
        /// <param name="axisIndex">Normal axis index.</param>
        /// <returns>World-space coordinate for the manifold plane.</returns>
        static float ResolveContactPlaneValue(BodyState3D first, float3 normal, int axisIndex) {
            float center = GetComponent(first.Position, axisIndex);
            float extent = GetComponent(first.AxisAlignedHalfExtents, axisIndex);
            float normalComponent = GetComponent(normal, axisIndex);
            if (normalComponent >= 0f) {
                return center - extent;
            }

            return center + extent;
        }

        /// <summary>
        /// Resolves the first tangent axis for a contact normal axis.
        /// </summary>
        /// <param name="axisIndex">Normal axis index.</param>
        /// <returns>First tangent axis index.</returns>
        static int ResolveFirstTangentAxis(int axisIndex) {
            if (axisIndex == 0) {
                return 1;
            }
            if (axisIndex == 1) {
                return 0;
            }
            if (axisIndex == 2) {
                return 0;
            }

            throw new ArgumentOutOfRangeException(nameof(axisIndex), "Axis index must be between zero and two.");
        }

        /// <summary>
        /// Resolves the second tangent axis for a contact normal axis.
        /// </summary>
        /// <param name="axisIndex">Normal axis index.</param>
        /// <returns>Second tangent axis index.</returns>
        static int ResolveSecondTangentAxis(int axisIndex) {
            if (axisIndex == 0) {
                return 2;
            }
            if (axisIndex == 1) {
                return 2;
            }
            if (axisIndex == 2) {
                return 1;
            }

            throw new ArgumentOutOfRangeException(nameof(axisIndex), "Axis index must be between zero and two.");
        }

        /// <summary>
        /// Resolves the lower bound of the shared overlap interval on one tangent axis.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <param name="axisIndex">Tangent axis index.</param>
        /// <returns>Minimum world-space coordinate of the overlap interval.</returns>
        static float ResolveOverlapMinimum(BodyState3D first, BodyState3D second, int axisIndex) {
            float firstMinimum = GetComponent(first.Position, axisIndex) - GetComponent(first.AxisAlignedHalfExtents, axisIndex);
            float secondMinimum = GetComponent(second.Position, axisIndex) - GetComponent(second.AxisAlignedHalfExtents, axisIndex);
            return Math.Max(firstMinimum, secondMinimum);
        }

        /// <summary>
        /// Resolves the upper bound of the shared overlap interval on one tangent axis.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <param name="axisIndex">Tangent axis index.</param>
        /// <returns>Maximum world-space coordinate of the overlap interval.</returns>
        static float ResolveOverlapMaximum(BodyState3D first, BodyState3D second, int axisIndex) {
            float firstMaximum = GetComponent(first.Position, axisIndex) + GetComponent(first.AxisAlignedHalfExtents, axisIndex);
            float secondMaximum = GetComponent(second.Position, axisIndex) + GetComponent(second.AxisAlignedHalfExtents, axisIndex);
            return Math.Min(firstMaximum, secondMaximum);
        }

        /// <summary>
        /// Creates one world-space contact point from one normal coordinate and two tangent coordinates.
        /// </summary>
        /// <param name="normalAxisIndex">Axis index used by the contact normal.</param>
        /// <param name="normalAxisValue">World-space coordinate on the normal axis.</param>
        /// <param name="firstTangentAxisIndex">First tangent axis index.</param>
        /// <param name="firstTangentAxisValue">World-space coordinate on the first tangent axis.</param>
        /// <param name="secondTangentAxisIndex">Second tangent axis index.</param>
        /// <param name="secondTangentAxisValue">World-space coordinate on the second tangent axis.</param>
        /// <returns>World-space contact point.</returns>
        static float3 CreateContactPoint(
            int normalAxisIndex,
            float normalAxisValue,
            int firstTangentAxisIndex,
            float firstTangentAxisValue,
            int secondTangentAxisIndex,
            float secondTangentAxisValue) {
            float x = 0f;
            float y = 0f;
            float z = 0f;
            x = AssignComponent(x, 0, normalAxisIndex, normalAxisValue, firstTangentAxisIndex, firstTangentAxisValue, secondTangentAxisIndex, secondTangentAxisValue);
            y = AssignComponent(y, 1, normalAxisIndex, normalAxisValue, firstTangentAxisIndex, firstTangentAxisValue, secondTangentAxisIndex, secondTangentAxisValue);
            z = AssignComponent(z, 2, normalAxisIndex, normalAxisValue, firstTangentAxisIndex, firstTangentAxisValue, secondTangentAxisIndex, secondTangentAxisValue);
            return new float3(x, y, z);
        }

        /// <summary>
        /// Selects the coordinate assigned to one vector component from the normal or tangent axes.
        /// </summary>
        /// <param name="currentValue">Existing component value used when no axis matches.</param>
        /// <param name="componentIndex">Component index being assigned.</param>
        /// <param name="normalAxisIndex">Normal axis index.</param>
        /// <param name="normalAxisValue">Normal axis coordinate.</param>
        /// <param name="firstTangentAxisIndex">First tangent axis index.</param>
        /// <param name="firstTangentAxisValue">First tangent axis coordinate.</param>
        /// <param name="secondTangentAxisIndex">Second tangent axis index.</param>
        /// <param name="secondTangentAxisValue">Second tangent axis coordinate.</param>
        /// <returns>Coordinate assigned to the requested component.</returns>
        static float AssignComponent(
            float currentValue,
            int componentIndex,
            int normalAxisIndex,
            float normalAxisValue,
            int firstTangentAxisIndex,
            float firstTangentAxisValue,
            int secondTangentAxisIndex,
            float secondTangentAxisValue) {
            if (componentIndex == normalAxisIndex) {
                return normalAxisValue;
            }
            if (componentIndex == firstTangentAxisIndex) {
                return firstTangentAxisValue;
            }
            if (componentIndex == secondTangentAxisIndex) {
                return secondTangentAxisValue;
            }

            return currentValue;
        }

        /// <summary>
        /// Reads one vector component by numeric axis index.
        /// </summary>
        /// <param name="value">Vector to inspect.</param>
        /// <param name="axisIndex">Zero for X, one for Y, two for Z.</param>
        /// <returns>Selected vector component.</returns>
        static float GetComponent(float3 value, int axisIndex) {
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
    }
}
#endif
