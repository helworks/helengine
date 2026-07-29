namespace helengine {
    /// <summary>
    /// Converts deterministic oriented-box SAT results into stable face-clipped or support-edge contact manifolds.
    /// </summary>
    static class HelPhysicsBoxBoxCollision3D {
        /// <summary>
        /// Stores the fixed maximum number of contacts retained by one box manifold.
        /// </summary>
        const int MaximumContactCount = 4;

        /// <summary>
        /// Stores the scalar used to average paired surface anchors into one solver contact position.
        /// </summary>
        static readonly PhysicsScalar OneHalf = PhysicsScalar.FromFloat(0.5f);

        /// <summary>
        /// Builds an allocation-free contact manifold from the minimum separating-axis result for two boxes.
        /// </summary>
        /// <param name="ShapeA">First centered box shape.</param>
        /// <param name="BodyA">World-space pose of the first box.</param>
        /// <param name="ShapeB">Second centered box shape.</param>
        /// <param name="BodyB">World-space pose of the second box.</param>
        /// <param name="Scratch">Reusable clipping storage allocated outside the query hot loop.</param>
        /// <param name="Manifold">Receives zero to four contacts and is cleared when the boxes are separated.</param>
        /// <returns>True when SAT and manifold generation find at least one touching or penetrating contact; otherwise false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the required clipping scratch object is null.</exception>
        public static bool TryBuildManifold(
            in HelPhysicsBoxShape3D ShapeA,
            in HelPhysicsBodyState3D BodyA,
            in HelPhysicsBoxShape3D ShapeB,
            in HelPhysicsBodyState3D BodyB,
            HelPhysicsBoxCollisionScratch3D Scratch,
            ref HelPhysicsContactManifold3D Manifold) {
            if (Scratch == null) {
                throw new ArgumentNullException(nameof(Scratch), "Box manifold generation requires reusable clipping scratch storage.");
            }

            Manifold.Reset();
            if (!HelPhysicsBoxSat3D.TryFindMinimumPenetration(
                in ShapeA,
                in BodyA,
                in ShapeB,
                in BodyB,
                out HelPhysicsBoxSatResult3D satResult)) {
                return false;
            }

            if (satResult.AxisKind == HelPhysicsBoxSatAxisKind3D.EdgePair) {
                BuildEdgeContact(in ShapeA, in BodyA, in ShapeB, in BodyB, in satResult, ref Manifold);
                return true;
            }

            return BuildFaceContacts(in ShapeA, in BodyA, in ShapeB, in BodyB, in satResult, Scratch, ref Manifold);
        }

        /// <summary>
        /// Clips the most anti-parallel incident face against the selected reference face and retains the deepest four contacts.
        /// </summary>
        /// <param name="shapeA">First centered box shape.</param>
        /// <param name="bodyA">World-space pose of the first box.</param>
        /// <param name="shapeB">Second centered box shape.</param>
        /// <param name="bodyB">World-space pose of the second box.</param>
        /// <param name="satResult">Face-axis SAT winner and A-to-B normal.</param>
        /// <param name="scratch">Reusable alternating clipping buffers.</param>
        /// <param name="manifold">Manifold receiving deterministically ordered contacts.</param>
        /// <returns>True when at least one clipped incident point is on or behind the reference plane.</returns>
        static bool BuildFaceContacts(
            in HelPhysicsBoxShape3D shapeA,
            in HelPhysicsBodyState3D bodyA,
            in HelPhysicsBoxShape3D shapeB,
            in HelPhysicsBodyState3D bodyB,
            in HelPhysicsBoxSatResult3D satResult,
            HelPhysicsBoxCollisionScratch3D scratch,
            ref HelPhysicsContactManifold3D manifold) {
            bool referenceIsA = satResult.AxisKind == HelPhysicsBoxSatAxisKind3D.FaceA;
            HelPhysicsBoxShape3D referenceShape = referenceIsA ? shapeA : shapeB;
            HelPhysicsBodyState3D referenceBody = referenceIsA ? bodyA : bodyB;
            HelPhysicsBoxShape3D incidentShape = referenceIsA ? shapeB : shapeA;
            HelPhysicsBodyState3D incidentBody = referenceIsA ? bodyB : bodyA;
            int referenceAxisIndex = referenceIsA ? satResult.AxisAIndex : satResult.AxisBIndex;
            PhysicsVector3 referenceNormal = referenceIsA ? satResult.Normal : -satResult.Normal;
            PhysicsVector3 referenceAxis = HelPhysicsBoxGeometry3D.GetWorldAxis(referenceBody.Orientation, referenceAxisIndex);
            bool referencePositiveFace = PhysicsVector3.Dot(referenceAxis, referenceNormal) >= PhysicsScalar.Zero;
            PhysicsScalar referenceExtent = GetExtent(referenceShape.HalfExtents, referenceAxisIndex);
            PhysicsVector3 referenceFaceCenter = referenceBody.Position + (referenceNormal * referenceExtent);
            int firstTangentAxisIndex = GetFirstFaceTangentAxisIndex(referenceAxisIndex);
            int secondTangentAxisIndex = GetSecondFaceTangentAxisIndex(referenceAxisIndex);
            PhysicsVector3 firstTangent = HelPhysicsBoxGeometry3D.GetWorldAxis(referenceBody.Orientation, firstTangentAxisIndex);
            PhysicsVector3 secondTangent = HelPhysicsBoxGeometry3D.GetWorldAxis(referenceBody.Orientation, secondTangentAxisIndex);
            PhysicsScalar firstTangentExtent = GetExtent(referenceShape.HalfExtents, firstTangentAxisIndex);
            PhysicsScalar secondTangentExtent = GetExtent(referenceShape.HalfExtents, secondTangentAxisIndex);

            FindIncidentFace(
                incidentBody.Orientation,
                referenceNormal,
                out int incidentAxisIndex,
                out bool incidentPositiveFace);

            HelPhysicsBoxClipVertex3D[] input = scratch.ClippingBuffer0;
            HelPhysicsBoxClipVertex3D[] output = scratch.ClippingBuffer1;
            FillIncidentFace(
                in incidentShape,
                in incidentBody,
                incidentAxisIndex,
                incidentPositiveFace,
                input);
            int clippedCount = 4;

            clippedCount = ClipPolygonAgainstPlane(
                input,
                clippedCount,
                output,
                referenceFaceCenter,
                firstTangent,
                firstTangentExtent,
                0);
            SwapClippingBuffers(ref input, ref output);
            clippedCount = ClipPolygonAgainstPlane(
                input,
                clippedCount,
                output,
                referenceFaceCenter,
                -firstTangent,
                firstTangentExtent,
                1);
            SwapClippingBuffers(ref input, ref output);
            clippedCount = ClipPolygonAgainstPlane(
                input,
                clippedCount,
                output,
                referenceFaceCenter,
                secondTangent,
                secondTangentExtent,
                2);
            SwapClippingBuffers(ref input, ref output);
            clippedCount = ClipPolygonAgainstPlane(
                input,
                clippedCount,
                output,
                referenceFaceCenter,
                -secondTangent,
                secondTangentExtent,
                3);
            SwapClippingBuffers(ref input, ref output);

            int referenceFaceIndex = GetFaceIndex(referenceAxisIndex, referencePositiveFace);
            int incidentFaceIndex = GetFaceIndex(incidentAxisIndex, incidentPositiveFace);
            for (int vertexIndex = 0; vertexIndex < clippedCount; vertexIndex++) {
                HelPhysicsBoxClipVertex3D clippedVertex = input[vertexIndex];
                PhysicsScalar signedDistance = PhysicsVector3.Dot(
                    referenceNormal,
                    clippedVertex.Position - referenceFaceCenter);
                if (signedDistance > PhysicsScalar.Zero) {
                    continue;
                }

                PhysicsScalar penetrationDepth = -signedDistance;
                PhysicsVector3 referenceAnchor = clippedVertex.Position - (referenceNormal * signedDistance);
                PhysicsVector3 position = (referenceAnchor + clippedVertex.Position) * OneHalf;
                PhysicsVector3 localAnchorA;
                PhysicsVector3 localAnchorB;
                if (referenceIsA) {
                    localAnchorA = TransformWorldAnchorToLocal(referenceAnchor, in bodyA);
                    localAnchorB = TransformWorldAnchorToLocal(clippedVertex.Position, in bodyB);
                } else {
                    localAnchorA = TransformWorldAnchorToLocal(clippedVertex.Position, in bodyA);
                    localAnchorB = TransformWorldAnchorToLocal(referenceAnchor, in bodyB);
                }

                HelPhysicsContactFeature3D feature = CreateFaceFeature(
                    referenceIsA,
                    referenceFaceIndex,
                    incidentFaceIndex,
                    clippedVertex.IncidentVertexMask,
                    clippedVertex.ClipPlaneMask);
                HelPhysicsContactPoint3D contact = new HelPhysicsContactPoint3D(
                    position,
                    satResult.Normal,
                    localAnchorA,
                    localAnchorB,
                    penetrationDepth,
                    feature);
                InsertDeepestContact(ref manifold, in contact);
            }

            return manifold.ContactCount > 0;
        }

        /// <summary>
        /// Creates the two SAT-winning support edges, finds their closest points, and stores their midpoint contact.
        /// </summary>
        /// <param name="shapeA">First centered box shape.</param>
        /// <param name="bodyA">World-space pose of the first box.</param>
        /// <param name="shapeB">Second centered box shape.</param>
        /// <param name="bodyB">World-space pose of the second box.</param>
        /// <param name="satResult">Edge-pair SAT winner, edge indices, A-to-B normal, and depth.</param>
        /// <param name="manifold">Manifold receiving the single edge contact.</param>
        static void BuildEdgeContact(
            in HelPhysicsBoxShape3D shapeA,
            in HelPhysicsBodyState3D bodyA,
            in HelPhysicsBoxShape3D shapeB,
            in HelPhysicsBodyState3D bodyB,
            in HelPhysicsBoxSatResult3D satResult,
            ref HelPhysicsContactManifold3D manifold) {
            BuildSupportEdge(
                in shapeA,
                in bodyA,
                satResult.AxisAIndex,
                satResult.Normal,
                out PhysicsVector3 edgeAStart,
                out PhysicsVector3 edgeAEnd,
                out byte supportMaskA);
            BuildSupportEdge(
                in shapeB,
                in bodyB,
                satResult.AxisBIndex,
                -satResult.Normal,
                out PhysicsVector3 edgeBStart,
                out PhysicsVector3 edgeBEnd,
                out byte supportMaskB);
            FindClosestSegmentPoints(
                edgeAStart,
                edgeAEnd,
                edgeBStart,
                edgeBEnd,
                out PhysicsVector3 anchorA,
                out PhysicsVector3 anchorB);

            PhysicsVector3 position = (anchorA + anchorB) * OneHalf;
            HelPhysicsContactFeature3D feature = CreateEdgeFeature(
                satResult.AxisAIndex,
                satResult.AxisBIndex,
                supportMaskA,
                supportMaskB);
            HelPhysicsContactPoint3D contact = new HelPhysicsContactPoint3D(
                position,
                satResult.Normal,
                TransformWorldAnchorToLocal(anchorA, in bodyA),
                TransformWorldAnchorToLocal(anchorB, in bodyB),
                satResult.PenetrationDepth,
                feature);
            manifold.SetContact(0, in contact);
            manifold.ContactCount = 1;
        }

        /// <summary>
        /// Selects the incident face whose outward normal has the smallest dot product with the reference normal.
        /// </summary>
        /// <param name="orientation">World-space orientation of the incident box.</param>
        /// <param name="referenceNormal">Outward world-space normal of the selected reference face.</param>
        /// <param name="incidentAxisIndex">Receives the local axis index of the most anti-parallel face.</param>
        /// <param name="incidentPositiveFace">Receives whether the positive side of that local axis is incident.</param>
        static void FindIncidentFace(
            PhysicsQuaternion orientation,
            PhysicsVector3 referenceNormal,
            out int incidentAxisIndex,
            out bool incidentPositiveFace) {
            PhysicsVector3 bestAxis = HelPhysicsBoxGeometry3D.GetWorldAxis(orientation, 0);
            PhysicsScalar bestDot = PhysicsVector3.Dot(bestAxis, referenceNormal);
            PhysicsScalar bestAbsoluteDot = PhysicsScalar.Abs(bestDot);
            incidentAxisIndex = 0;

            for (int axisIndex = 1; axisIndex < 3; axisIndex++) {
                PhysicsVector3 axis = HelPhysicsBoxGeometry3D.GetWorldAxis(orientation, axisIndex);
                PhysicsScalar axisDot = PhysicsVector3.Dot(axis, referenceNormal);
                PhysicsScalar absoluteAxisDot = PhysicsScalar.Abs(axisDot);
                if (absoluteAxisDot > bestAbsoluteDot) {
                    bestDot = axisDot;
                    bestAbsoluteDot = absoluteAxisDot;
                    incidentAxisIndex = axisIndex;
                }
            }

            incidentPositiveFace = bestDot <= PhysicsScalar.Zero;
        }

        /// <summary>
        /// Writes the four corners of one incident face in deterministic perimeter order with original vertex provenance.
        /// </summary>
        /// <param name="shape">Incident box shape.</param>
        /// <param name="body">World-space pose of the incident box.</param>
        /// <param name="faceAxisIndex">Local normal axis of the incident face.</param>
        /// <param name="positiveFace">Whether the incident face lies on the positive side of its local axis.</param>
        /// <param name="destination">Scratch buffer receiving exactly four face vertices.</param>
        static void FillIncidentFace(
            in HelPhysicsBoxShape3D shape,
            in HelPhysicsBodyState3D body,
            int faceAxisIndex,
            bool positiveFace,
            HelPhysicsBoxClipVertex3D[] destination) {
            for (int faceVertexIndex = 0; faceVertexIndex < 4; faceVertexIndex++) {
                int boxVertexIndex = GetFaceVertexIndex(faceAxisIndex, positiveFace, faceVertexIndex);
                destination[faceVertexIndex] = new HelPhysicsBoxClipVertex3D(
                    HelPhysicsBoxGeometry3D.GetWorldVertex(
                        shape,
                        body.Position,
                        body.Orientation,
                        boxVertexIndex),
                    (byte)(1 << boxVertexIndex),
                    0);
            }
        }

        /// <summary>
        /// Clips a convex polygon against one inward half-space while propagating deterministic endpoint and plane provenance.
        /// </summary>
        /// <param name="input">Scratch polygon produced by the preceding clipping stage.</param>
        /// <param name="inputCount">Number of valid leading vertices in <paramref name="input"/>.</param>
        /// <param name="output">Alternate scratch buffer receiving the clipped polygon.</param>
        /// <param name="faceCenter">Any point centered on the reference face and its side planes.</param>
        /// <param name="planeNormal">Outward side-plane normal; points at non-positive signed distance are retained.</param>
        /// <param name="planeExtent">Distance from the face center to this side plane.</param>
        /// <param name="planeIndex">Deterministic side-plane index from zero through three.</param>
        /// <returns>The number of valid leading vertices written to <paramref name="output"/>.</returns>
        static int ClipPolygonAgainstPlane(
            HelPhysicsBoxClipVertex3D[] input,
            int inputCount,
            HelPhysicsBoxClipVertex3D[] output,
            PhysicsVector3 faceCenter,
            PhysicsVector3 planeNormal,
            PhysicsScalar planeExtent,
            int planeIndex) {
            if (inputCount == 0) {
                return 0;
            }

            int outputCount = 0;
            HelPhysicsBoxClipVertex3D previous = input[inputCount - 1];
            PhysicsScalar previousDistance = PhysicsVector3.Dot(
                planeNormal,
                previous.Position - faceCenter) - planeExtent;

            for (int inputIndex = 0; inputIndex < inputCount; inputIndex++) {
                HelPhysicsBoxClipVertex3D current = input[inputIndex];
                PhysicsScalar currentDistance = PhysicsVector3.Dot(
                    planeNormal,
                    current.Position - faceCenter) - planeExtent;
                bool currentInside = currentDistance <= PhysicsScalar.Zero;

                if ((previousDistance < PhysicsScalar.Zero && currentDistance > PhysicsScalar.Zero)
                    || (previousDistance > PhysicsScalar.Zero && currentDistance < PhysicsScalar.Zero)) {
                    PhysicsScalar interpolation = previousDistance / (previousDistance - currentDistance);
                    PhysicsVector3 intersection = previous.Position
                        + ((current.Position - previous.Position) * interpolation);
                    byte incidentVertexMask = (byte)(previous.IncidentVertexMask | current.IncidentVertexMask);
                    byte clipPlaneMask = (byte)(previous.ClipPlaneMask | current.ClipPlaneMask | (1 << planeIndex));
                    output[outputCount] = new HelPhysicsBoxClipVertex3D(
                        intersection,
                        incidentVertexMask,
                        clipPlaneMask);
                    outputCount++;
                }

                if (currentInside) {
                    output[outputCount] = current;
                    outputCount++;
                }

                previous = current;
                previousDistance = currentDistance;
            }

            return outputCount;
        }

        /// <summary>
        /// Exchanges which preallocated clipping buffer is read and which is written by the next side plane.
        /// </summary>
        /// <param name="input">Current input buffer, replaced with the current output buffer.</param>
        /// <param name="output">Current output buffer, replaced with the current input buffer.</param>
        static void SwapClippingBuffers(
            ref HelPhysicsBoxClipVertex3D[] input,
            ref HelPhysicsBoxClipVertex3D[] output) {
            HelPhysicsBoxClipVertex3D[] previousInput = input;
            input = output;
            output = previousInput;
        }

        /// <summary>
        /// Constructs one finite support edge parallel to the selected local axis and extreme along a world-space direction.
        /// </summary>
        /// <param name="shape">Box shape supplying local half extents.</param>
        /// <param name="body">World-space pose of the box.</param>
        /// <param name="edgeAxisIndex">Local axis parallel to the support edge.</param>
        /// <param name="supportDirection">World-space direction selecting the two fixed edge signs.</param>
        /// <param name="edgeStart">Receives the negative endpoint along the selected edge axis.</param>
        /// <param name="edgeEnd">Receives the positive endpoint along the selected edge axis.</param>
        /// <param name="supportMask">Receives bits identifying positive fixed-axis signs for feature matching.</param>
        static void BuildSupportEdge(
            in HelPhysicsBoxShape3D shape,
            in HelPhysicsBodyState3D body,
            int edgeAxisIndex,
            PhysicsVector3 supportDirection,
            out PhysicsVector3 edgeStart,
            out PhysicsVector3 edgeEnd,
            out byte supportMask) {
            PhysicsVector3 edgeCenter = body.Position;
            supportMask = 0;
            for (int axisIndex = 0; axisIndex < 3; axisIndex++) {
                if (axisIndex == edgeAxisIndex) {
                    continue;
                }

                PhysicsVector3 axis = HelPhysicsBoxGeometry3D.GetWorldAxis(body.Orientation, axisIndex);
                PhysicsScalar extent = GetExtent(shape.HalfExtents, axisIndex);
                if (PhysicsVector3.Dot(axis, supportDirection) >= PhysicsScalar.Zero) {
                    edgeCenter += axis * extent;
                    supportMask = (byte)(supportMask | (1 << axisIndex));
                } else {
                    edgeCenter -= axis * extent;
                }
            }

            PhysicsVector3 edgeAxis = HelPhysicsBoxGeometry3D.GetWorldAxis(body.Orientation, edgeAxisIndex);
            PhysicsVector3 edgeOffset = edgeAxis * GetExtent(shape.HalfExtents, edgeAxisIndex);
            edgeStart = edgeCenter - edgeOffset;
            edgeEnd = edgeCenter + edgeOffset;
        }

        /// <summary>
        /// Finds the closest points on two finite non-degenerate segments with clamped scalar parameters.
        /// </summary>
        /// <param name="segmentAStart">First endpoint of segment A.</param>
        /// <param name="segmentAEnd">Second endpoint of segment A.</param>
        /// <param name="segmentBStart">First endpoint of segment B.</param>
        /// <param name="segmentBEnd">Second endpoint of segment B.</param>
        /// <param name="closestA">Receives the closest point on segment A.</param>
        /// <param name="closestB">Receives the closest point on segment B.</param>
        static void FindClosestSegmentPoints(
            PhysicsVector3 segmentAStart,
            PhysicsVector3 segmentAEnd,
            PhysicsVector3 segmentBStart,
            PhysicsVector3 segmentBEnd,
            out PhysicsVector3 closestA,
            out PhysicsVector3 closestB) {
            PhysicsVector3 directionA = segmentAEnd - segmentAStart;
            PhysicsVector3 directionB = segmentBEnd - segmentBStart;
            PhysicsVector3 offset = segmentAStart - segmentBStart;
            PhysicsScalar lengthSquaredA = PhysicsVector3.Dot(directionA, directionA);
            PhysicsScalar lengthSquaredB = PhysicsVector3.Dot(directionB, directionB);
            PhysicsScalar directionDot = PhysicsVector3.Dot(directionA, directionB);
            PhysicsScalar offsetDotA = PhysicsVector3.Dot(directionA, offset);
            PhysicsScalar offsetDotB = PhysicsVector3.Dot(directionB, offset);
            PhysicsScalar denominator = (lengthSquaredA * lengthSquaredB) - (directionDot * directionDot);
            PhysicsScalar parameterA = PhysicsScalar.Zero;
            if (denominator > PhysicsScalar.Zero) {
                parameterA = PhysicsScalar.Clamp(
                    ((directionDot * offsetDotB) - (offsetDotA * lengthSquaredB)) / denominator,
                    PhysicsScalar.Zero,
                    PhysicsScalar.One);
            }

            PhysicsScalar parameterB = (directionDot * parameterA) + offsetDotB;
            if (parameterB < PhysicsScalar.Zero) {
                parameterB = PhysicsScalar.Zero;
                parameterA = PhysicsScalar.Clamp(
                    -offsetDotA / lengthSquaredA,
                    PhysicsScalar.Zero,
                    PhysicsScalar.One);
            } else if (parameterB > lengthSquaredB) {
                parameterB = PhysicsScalar.One;
                parameterA = PhysicsScalar.Clamp(
                    (directionDot - offsetDotA) / lengthSquaredA,
                    PhysicsScalar.Zero,
                    PhysicsScalar.One);
            } else {
                parameterB /= lengthSquaredB;
            }

            closestA = segmentAStart + (directionA * parameterA);
            closestB = segmentBStart + (directionB * parameterB);
        }

        /// <summary>
        /// Inserts one face contact by descending penetration and deterministic feature/position tie order, truncating after four.
        /// </summary>
        /// <param name="manifold">Inline manifold whose leading contacts are already ordered.</param>
        /// <param name="contact">Candidate clipped contact to retain when it ranks among the deepest four.</param>
        static void InsertDeepestContact(
            ref HelPhysicsContactManifold3D manifold,
            in HelPhysicsContactPoint3D contact) {
            int insertionIndex = manifold.ContactCount;
            for (int contactIndex = 0; contactIndex < manifold.ContactCount; contactIndex++) {
                HelPhysicsContactPoint3D existing = manifold.GetContact(contactIndex);
                if (IsContactOrderedBefore(in contact, in existing)) {
                    insertionIndex = contactIndex;
                    break;
                }
            }

            if (insertionIndex >= MaximumContactCount) {
                return;
            }

            int lastDestinationIndex = manifold.ContactCount < MaximumContactCount
                ? manifold.ContactCount
                : MaximumContactCount - 1;
            for (int destinationIndex = lastDestinationIndex; destinationIndex > insertionIndex; destinationIndex--) {
                HelPhysicsContactPoint3D shiftedContact = manifold.GetContact(destinationIndex - 1);
                manifold.SetContact(destinationIndex, in shiftedContact);
            }

            manifold.SetContact(insertionIndex, in contact);
            if (manifold.ContactCount < MaximumContactCount) {
                manifold.ContactCount++;
            }
        }

        /// <summary>
        /// Compares two contacts by depth, feature provenance, and exact position components for deterministic retention.
        /// </summary>
        /// <param name="candidate">Candidate contact being inserted.</param>
        /// <param name="existing">Existing ordered contact at the comparison position.</param>
        /// <returns>True when the candidate must appear before the existing contact.</returns>
        static bool IsContactOrderedBefore(
            in HelPhysicsContactPoint3D candidate,
            in HelPhysicsContactPoint3D existing) {
            if (candidate.PenetrationDepth > existing.PenetrationDepth) {
                return true;
            } else if (candidate.PenetrationDepth < existing.PenetrationDepth) {
                return false;
            }

            if (candidate.Feature.Value < existing.Feature.Value) {
                return true;
            } else if (candidate.Feature.Value > existing.Feature.Value) {
                return false;
            }

            if (candidate.Position.X < existing.Position.X) {
                return true;
            } else if (candidate.Position.X > existing.Position.X) {
                return false;
            } else if (candidate.Position.Y < existing.Position.Y) {
                return true;
            } else if (candidate.Position.Y > existing.Position.Y) {
                return false;
            }

            return candidate.Position.Z < existing.Position.Z;
        }

        /// <summary>
        /// Packs reference ownership, face indices, original vertices, and clipping planes into one stable face feature.
        /// </summary>
        /// <param name="referenceIsA">Whether query body A supplied the reference face.</param>
        /// <param name="referenceFaceIndex">Reference local face index from zero through five.</param>
        /// <param name="incidentFaceIndex">Incident local face index from zero through five.</param>
        /// <param name="incidentVertexMask">Bits identifying original incident vertices contributing to the point.</param>
        /// <param name="clipPlaneMask">Bits identifying reference side planes contributing to the point.</param>
        /// <returns>A packed deterministic contact feature.</returns>
        static HelPhysicsContactFeature3D CreateFaceFeature(
            bool referenceIsA,
            int referenceFaceIndex,
            int incidentFaceIndex,
            byte incidentVertexMask,
            byte clipPlaneMask) {
            uint referenceOwner = referenceIsA ? 0u : 1u;
            uint value = 0x10000000u
                | (referenceOwner << 27)
                | ((uint)referenceFaceIndex << 24)
                | ((uint)incidentFaceIndex << 21)
                | ((uint)incidentVertexMask << 4)
                | clipPlaneMask;
            return new HelPhysicsContactFeature3D(value);
        }

        /// <summary>
        /// Packs both edge directions and fixed support signs into one stable edge-pair feature.
        /// </summary>
        /// <param name="axisAIndex">Local edge direction index on body A.</param>
        /// <param name="axisBIndex">Local edge direction index on body B.</param>
        /// <param name="supportMaskA">Positive fixed-axis signs selecting body A's support edge.</param>
        /// <param name="supportMaskB">Positive fixed-axis signs selecting body B's support edge.</param>
        /// <returns>A packed deterministic contact feature.</returns>
        static HelPhysicsContactFeature3D CreateEdgeFeature(
            int axisAIndex,
            int axisBIndex,
            byte supportMaskA,
            byte supportMaskB) {
            uint value = 0x20000000u
                | ((uint)axisAIndex << 24)
                | ((uint)axisBIndex << 22)
                | ((uint)supportMaskA << 19)
                | ((uint)supportMaskB << 16);
            return new HelPhysicsContactFeature3D(value);
        }

        /// <summary>
        /// Converts one world-space surface point into an anchor relative to a body's local center and orientation.
        /// </summary>
        /// <param name="worldAnchor">World-space point on the body's collision surface.</param>
        /// <param name="body">Body pose defining the local anchor frame.</param>
        /// <returns>The center-relative anchor rotated into body-local coordinates.</returns>
        static PhysicsVector3 TransformWorldAnchorToLocal(
            PhysicsVector3 worldAnchor,
            in HelPhysicsBodyState3D body) {
            return body.Orientation.Conjugated().Rotate(worldAnchor - body.Position);
        }

        /// <summary>
        /// Returns the half extent paired with one local box axis without indexed storage.
        /// </summary>
        /// <param name="halfExtents">Local X, Y, and Z box half extents.</param>
        /// <param name="axisIndex">Requested local axis index from zero through two.</param>
        /// <returns>The half extent on the selected axis.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="axisIndex"/> is outside the box axes.</exception>
        static PhysicsScalar GetExtent(PhysicsVector3 halfExtents, int axisIndex) {
            if (axisIndex == 0) {
                return halfExtents.X;
            } else if (axisIndex == 1) {
                return halfExtents.Y;
            } else if (axisIndex == 2) {
                return halfExtents.Z;
            }

            throw new ArgumentOutOfRangeException(nameof(axisIndex), "Box extents are indexed from zero through two.");
        }

        /// <summary>
        /// Returns the lower-index local axis tangent to a face with the supplied normal axis.
        /// </summary>
        /// <param name="faceAxisIndex">Local face-normal axis index.</param>
        /// <returns>The deterministic first tangent-axis index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="faceAxisIndex"/> is outside the box axes.</exception>
        static int GetFirstFaceTangentAxisIndex(int faceAxisIndex) {
            if (faceAxisIndex == 0) {
                return 1;
            } else if (faceAxisIndex == 1 || faceAxisIndex == 2) {
                return 0;
            }

            throw new ArgumentOutOfRangeException(nameof(faceAxisIndex), "Box face axes are indexed from zero through two.");
        }

        /// <summary>
        /// Returns the higher-index local axis tangent to a face with the supplied normal axis.
        /// </summary>
        /// <param name="faceAxisIndex">Local face-normal axis index.</param>
        /// <returns>The deterministic second tangent-axis index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="faceAxisIndex"/> is outside the box axes.</exception>
        static int GetSecondFaceTangentAxisIndex(int faceAxisIndex) {
            if (faceAxisIndex == 0 || faceAxisIndex == 1) {
                return 2;
            } else if (faceAxisIndex == 2) {
                return 1;
            }

            throw new ArgumentOutOfRangeException(nameof(faceAxisIndex), "Box face axes are indexed from zero through two.");
        }

        /// <summary>
        /// Encodes one signed local face as two consecutive identifiers per axis.
        /// </summary>
        /// <param name="axisIndex">Local face-normal axis index.</param>
        /// <param name="positiveFace">Whether the face lies on the positive side of the local axis.</param>
        /// <returns>A face identifier from zero through five.</returns>
        static int GetFaceIndex(int axisIndex, bool positiveFace) {
            return (axisIndex * 2) + (positiveFace ? 1 : 0);
        }

        /// <summary>
        /// Selects one conventional three-bit box vertex from a signed face in deterministic perimeter order.
        /// </summary>
        /// <param name="faceAxisIndex">Local face-normal axis index.</param>
        /// <param name="positiveFace">Whether the selected face has a positive local normal.</param>
        /// <param name="faceVertexIndex">Perimeter index from zero through three.</param>
        /// <returns>The conventional full-box vertex index from zero through seven.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either index is outside its valid range.</exception>
        static int GetFaceVertexIndex(int faceAxisIndex, bool positiveFace, int faceVertexIndex) {
            if (faceVertexIndex < 0 || faceVertexIndex > 3) {
                throw new ArgumentOutOfRangeException(nameof(faceVertexIndex), "Face vertices are indexed from zero through three.");
            }

            int fixedBit = positiveFace ? 1 << faceAxisIndex : 0;
            if (faceAxisIndex == 0) {
                if (faceVertexIndex == 0) {
                    return fixedBit;
                } else if (faceVertexIndex == 1) {
                    return fixedBit | 2;
                } else if (faceVertexIndex == 2) {
                    return fixedBit | 2 | 4;
                }

                return fixedBit | 4;
            } else if (faceAxisIndex == 1) {
                if (faceVertexIndex == 0) {
                    return fixedBit;
                } else if (faceVertexIndex == 1) {
                    return fixedBit | 1;
                } else if (faceVertexIndex == 2) {
                    return fixedBit | 1 | 4;
                }

                return fixedBit | 4;
            } else if (faceAxisIndex == 2) {
                if (faceVertexIndex == 0) {
                    return fixedBit;
                } else if (faceVertexIndex == 1) {
                    return fixedBit | 1;
                } else if (faceVertexIndex == 2) {
                    return fixedBit | 1 | 2;
                }

                return fixedBit | 2;
            }

            throw new ArgumentOutOfRangeException(nameof(faceAxisIndex), "Box face axes are indexed from zero through two.");
        }
    }
}
