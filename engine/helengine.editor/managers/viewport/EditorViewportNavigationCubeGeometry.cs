namespace helengine.editor {
    /// <summary>
    /// Projects a unit navigation cube and resolves only its camera-visible face, edge, and corner regions.
    /// </summary>
    public sealed class EditorViewportNavigationCubeGeometry {
        /// <summary>
        /// Fraction of the square logical bounds occupied by the cube's longest projected dimension.
        /// </summary>
        public const double ProjectedCubeFillFraction = 0.76;
        /// <summary>Distance from the private camera to the unit cube center.</summary>
        public const float CameraDistance = 3f;
        /// <summary>Projection shared by rendering, labels, and pointer classification.</summary>
        public CameraProjectionMode ProjectionMode { get; set; } = CameraProjectionMode.Orthographic;

        /// <summary>Returns the centered projection span required to fit the rotated cube.</summary>
        /// <param name="orientation">Camera orientation around the cube.</param>
        /// <returns>Vertical span in world units or perspective tangent units.</returns>
        public float GetProjectionSpan(float4 orientation) {
            float4 inverse = float4.Inverse(NormalizeOrientation(orientation));
            double extent = 0;
            foreach (float3 vertex in UnitCubeVertices) {
                float3 point = float4.RotateVector(vertex, inverse);
                double divisor = ProjectionMode == CameraProjectionMode.Perspective ? CameraDistance - point.Z : 1.0;
                extent = Math.Max(extent, Math.Max(Math.Abs(point.X), Math.Abs(point.Y)) / divisor);
            }
            return (float)(2.0 * extent / ProjectedCubeFillFraction);
        }

        /// <summary>Maps camera-space depth to the quantity linear across a projected surface.</summary>
        /// <param name="depth">Camera-space cube depth.</param>
        /// <returns>Reciprocal distance for perspective, otherwise unchanged depth.</returns>
        double EncodeDepth(float depth) => ProjectionMode == CameraProjectionMode.Perspective ? 1.0 / (CameraDistance - depth) : depth;

        /// <summary>Converts an interpolated surface value back to camera-space depth.</summary>
        /// <param name="value">Interpolated depth quantity.</param>
        /// <returns>Camera-space cube depth.</returns>
        float DecodeDepth(double value) => (float)(ProjectionMode == CameraProjectionMode.Perspective ? CameraDistance - 1.0 / value : value);

        /// <summary>Interpolates an edge depth at a screen-space fraction.</summary>
        /// <param name="start">First endpoint depth.</param>
        /// <param name="end">Second endpoint depth.</param>
        /// <param name="amount">Screen-space interpolation fraction.</param>
        /// <returns>Perspective-correct camera-space depth.</returns>
        float InterpolateDepth(float start, float end, float amount) => DecodeDepth(EncodeDepth(start) * (1.0 - amount) + EncodeDepth(end) * amount);

        /// <summary>
        /// Radius of the corner target in logical cube-size units.
        /// </summary>
        public const double CornerHitRadiusFraction = 0.07;
        /// <summary>
        /// Half-width of an edge target in logical cube-size units.
        /// </summary>
        public const double EdgeHitRadiusFraction = 0.045;
        /// <summary>
        /// Minimum camera-space normal component considered front-facing.
        /// </summary>
        const double VisibleFaceThreshold = 0.000001;
        /// <summary>
        /// Depth tolerance used to reject projected vertices and edges hidden behind a front surface.
        /// </summary>
        const double SurfaceDepthTolerance = 0.0001;
        /// <summary>
        /// Smallest face area accepted for camera-space depth interpolation.
        /// </summary>
        const double MinimumProjectedFaceArea = 0.000001;
        /// <summary>
        /// Signed unit-cube corners in stable left/right, bottom/top, rear/front order.
        /// </summary>
        static readonly float3[] UnitCubeVertices = {
            new float3(-0.5f, -0.5f, -0.5f),
            new float3(0.5f, -0.5f, -0.5f),
            new float3(-0.5f, 0.5f, -0.5f),
            new float3(0.5f, 0.5f, -0.5f),
            new float3(-0.5f, -0.5f, 0.5f),
            new float3(0.5f, -0.5f, 0.5f),
            new float3(-0.5f, 0.5f, 0.5f),
            new float3(0.5f, 0.5f, 0.5f)
        };
        /// <summary>
        /// Four ordered cube-corner indices for each signed axis face.
        /// </summary>
        static readonly int[][] FaceVertexIndices = {
            new[] { 0, 2, 6, 4 },
            new[] { 1, 5, 7, 3 },
            new[] { 0, 1, 5, 4 },
            new[] { 2, 6, 7, 3 },
            new[] { 0, 1, 3, 2 },
            new[] { 4, 6, 7, 5 }
        };
        /// <summary>
        /// Signed face targets in the same order as the face-corner index table.
        /// </summary>
        static readonly EditorViewportNavigationTarget[] FaceTargets = {
            new EditorViewportNavigationTarget(-1, 0, 0),
            new EditorViewportNavigationTarget(1, 0, 0),
            new EditorViewportNavigationTarget(0, -1, 0),
            new EditorViewportNavigationTarget(0, 1, 0),
            new EditorViewportNavigationTarget(0, 0, -1),
            new EditorViewportNavigationTarget(0, 0, 1)
        };
        /// <summary>
        /// The twelve unordered cube-corner pairs that form physical cube edges.
        /// </summary>
        static readonly int2[] CubeEdgeVertexIndices = {
            new int2(0, 1), new int2(2, 3), new int2(4, 5), new int2(6, 7),
            new int2(0, 2), new int2(1, 3), new int2(4, 6), new int2(5, 7),
            new int2(0, 4), new int2(1, 5), new int2(2, 6), new int2(3, 7)
        };
        /// <summary>
        /// Signed vectors for the positive and negative world-axis face normals.
        /// </summary>
        static readonly float3[] FaceNormals = {
            new float3(-1f, 0f, 0f), new float3(1f, 0f, 0f),
            new float3(0f, -1f, 0f), new float3(0f, 1f, 0f),
            new float3(0f, 0f, -1f), new float3(0f, 0f, 1f)
        };

        /// <summary>
        /// Returns screen-space cube corners used by the overlay renderer and pointer hit testing.
        /// </summary>
        /// <param name="cameraOrientation">World orientation of the viewport camera.</param>
        /// <param name="size">Square logical width and height of the cube bounds.</param>
        /// <returns>Eight projected cube vertices in front/back and left/right/top/bottom order.</returns>
        public IReadOnlyList<float2> GetProjectedVertices(float4 cameraOrientation, float size) {
            ProjectedCube projectedCube = ProjectCube(cameraOrientation, size);
            float2[] vertices = new float2[projectedCube.Vertices.Length];
            for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++) {
                vertices[vertexIndex] = projectedCube.Vertices[vertexIndex].Position;
            }

            return vertices;
        }

        /// <summary>
        /// Returns the four projected corners of one face in perimeter order for rendering and labels.
        /// </summary>
        /// <param name="target">One signed axis face target.</param>
        /// <param name="cameraOrientation">World orientation of the viewport camera.</param>
        /// <param name="size">Square logical width and height of the cube bounds.</param>
        /// <returns>Four screen-space corners for the requested face.</returns>
        public IReadOnlyList<float2> GetFaceVertices(EditorViewportNavigationTarget target, float4 cameraOrientation, float size) {
            int faceIndex = GetFaceIndex(target);
            ProjectedCube projectedCube = ProjectCube(cameraOrientation, size);
            float2[] faceVertices = new float2[4];
            int[] vertexIndices = FaceVertexIndices[faceIndex];
            for (int faceVertexIndex = 0; faceVertexIndex < faceVertices.Length; faceVertexIndex++) {
                faceVertices[faceVertexIndex] = projectedCube.Vertices[vertexIndices[faceVertexIndex]].Position;
            }

            return faceVertices;
        }

        /// <summary>
        /// Returns the signed axis faces whose outward normals face toward the camera.
        /// </summary>
        /// <param name="cameraOrientation">World orientation of the viewport camera.</param>
        /// <returns>Visible face targets in stable signed-axis order.</returns>
        public IReadOnlyList<EditorViewportNavigationTarget> GetVisibleFaceTargets(float4 cameraOrientation) {
            float4 normalizedOrientation = NormalizeOrientation(cameraOrientation);
            float4 inverseOrientation = float4.Inverse(normalizedOrientation);
            List<EditorViewportNavigationTarget> visibleTargets = new List<EditorViewportNavigationTarget>(3);
            for (int faceIndex = 0; faceIndex < FaceTargets.Length; faceIndex++) {
                float3 cameraSpaceNormal = float4.RotateVector(FaceNormals[faceIndex], inverseOrientation);
                if (cameraSpaceNormal.Z > (ProjectionMode == CameraProjectionMode.Perspective ? 0.5 / CameraDistance : 0.0) + VisibleFaceThreshold) {
                    visibleTargets.Add(FaceTargets[faceIndex]);
                }
            }

            return visibleTargets;
        }

        /// <summary>
        /// Returns the twelve edge index pairs that connect the shared projected cube vertices.
        /// </summary>
        /// <returns>Vertex index pairs for all twelve cube edges.</returns>
        public IReadOnlyList<int2> GetCubeEdgeVertexIndices() {
            return CubeEdgeVertexIndices;
        }

        /// <summary>
        /// Attempts to resolve the pointer to a visible cube face, edge, or corner.
        /// </summary>
        /// <param name="localPointer">Pointer position in cube-local logical pixels.</param>
        /// <param name="cameraOrientation">World orientation of the viewport camera.</param>
        /// <param name="size">Square logical width and height of the cube bounds.</param>
        /// <param name="hit">Resolved visible cube region when the method returns true.</param>
        /// <returns>True when the pointer lies within a visible target region.</returns>
        /// <summary>
        /// Returns the signed front-face depth used to sort visible faces back to front.
        /// </summary>
        /// <param name="target">Signed axis face.</param>
        /// <param name="cameraOrientation">World orientation of the viewport camera.</param>
        /// <returns>Face-center camera-space depth, where larger values are closer to the camera.</returns>
        public float GetFaceDepth(EditorViewportNavigationTarget target, float4 cameraOrientation) {
            int faceIndex = GetFaceIndex(target);
            float3 cameraSpaceNormal = float4.RotateVector(FaceNormals[faceIndex], float4.Inverse(NormalizeOrientation(cameraOrientation)));
            return cameraSpaceNormal.Z * 0.5f;
        }

        /// <summary>
        /// Returns cube-edge index pairs whose physical midpoint belongs to a visible surface.
        /// </summary>
        /// <param name="cameraOrientation">World orientation of the viewport camera.</param>
        /// <param name="size">Square logical width and height of the cube bounds.</param>
        /// <returns>Visible physical edge endpoint pairs indexing <see cref="GetProjectedVertices"/>.</returns>
        public IReadOnlyList<int2> GetVisibleEdgeVertexIndices(float4 cameraOrientation, float size) {
            ProjectedCube projectedCube = ProjectCube(cameraOrientation, size);
            List<int2> visibleEdges = new List<int2>(9);
            for (int edgeIndex = 0; edgeIndex < CubeEdgeVertexIndices.Length; edgeIndex++) {
                int2 edge = CubeEdgeVertexIndices[edgeIndex];
                ProjectedCubeVertex start = projectedCube.Vertices[edge.X];
                ProjectedCubeVertex end = projectedCube.Vertices[edge.Y];
                float3 midpoint = float3.Lerp(UnitCubeVertices[edge.X], UnitCubeVertices[edge.Y], 0.5f);
                EditorViewportNavigationTarget target = GetEdgeTarget(midpoint, edge);
                float2 screenMidpoint = new float2(
                    (start.Position.X + end.Position.X) * 0.5f,
                    (start.Position.Y + end.Position.Y) * 0.5f);
                float depth = InterpolateDepth(start.Depth, end.Depth, 0.5f);
                if (HasVisibleAdjacentFace(target, projectedCube.CameraOrientation) &&
                    IsSurfacePointVisible(screenMidpoint, depth, projectedCube)) {
                    visibleEdges.Add(edge);
                }
            }

            return visibleEdges;
        }

        /// <summary>
        /// Returns projected cube corners that lie on the nearest visible surface.
        /// </summary>
        /// <param name="cameraOrientation">World orientation of the viewport camera.</param>
        /// <param name="size">Square logical width and height of the cube bounds.</param>
        /// <returns>Visible physical cube-corner indices.</returns>
        public IReadOnlyList<int> GetVisibleVertexIndices(float4 cameraOrientation, float size) {
            ProjectedCube projectedCube = ProjectCube(cameraOrientation, size);
            List<int> visibleVertices = new List<int>(7);
            for (int vertexIndex = 0; vertexIndex < projectedCube.Vertices.Length; vertexIndex++) {
                ProjectedCubeVertex vertex = projectedCube.Vertices[vertexIndex];
                if (IsSurfacePointVisible(vertex.Position, vertex.Depth, projectedCube)) {
                    visibleVertices.Add(vertexIndex);
                }
            }

            return visibleVertices;
        }
        public bool TryHit(float2 localPointer, float4 cameraOrientation, float size, out EditorViewportNavigationCubeHit hit) {
            ProjectedCube projectedCube = ProjectCube(cameraOrientation, size);
            hit = null;
            if (!float.IsFinite(localPointer.X) || !float.IsFinite(localPointer.Y) ||
                localPointer.X < 0f || localPointer.Y < 0f || localPointer.X >= size || localPointer.Y >= size) {
                return false;
            }

            float cornerRadius = (float)(size * CornerHitRadiusFraction);
            double bestCornerDistanceSquared = cornerRadius * cornerRadius;
            float bestCornerDepth = float.NegativeInfinity;
            EditorViewportNavigationTarget bestCornerTarget = null;
            for (int vertexIndex = 0; vertexIndex < projectedCube.Vertices.Length; vertexIndex++) {
                ProjectedCubeVertex vertex = projectedCube.Vertices[vertexIndex];
                double distanceSquared = GetDistanceSquared(localPointer, vertex.Position);
                if (distanceSquared > bestCornerDistanceSquared || !IsSurfacePointVisible(vertex.Position, vertex.Depth, projectedCube)) {
                    continue;
                }

                EditorViewportNavigationTarget target = GetCornerTarget(UnitCubeVertices[vertexIndex]);
                bool isCloserCorner = bestCornerTarget == null || distanceSquared < (bestCornerDistanceSquared - 0.0001);
                bool isDepthTie = Math.Abs(distanceSquared - bestCornerDistanceSquared) <= 0.0001;
                if (isCloserCorner || (isDepthTie && vertex.Depth > bestCornerDepth)) {
                    bestCornerDistanceSquared = distanceSquared;
                    bestCornerDepth = vertex.Depth;
                    bestCornerTarget = target;
                }
            }
            if (bestCornerTarget != null) {
                hit = new EditorViewportNavigationCubeHit(bestCornerTarget, bestCornerDepth);
                return true;
            }

            float edgeRadius = (float)(size * EdgeHitRadiusFraction);
            double bestEdgeDistanceSquared = edgeRadius * edgeRadius;
            float bestEdgeDepth = float.NegativeInfinity;
            EditorViewportNavigationTarget bestEdgeTarget = null;
            for (int edgeIndex = 0; edgeIndex < CubeEdgeVertexIndices.Length; edgeIndex++) {
                int2 edgeIndices = CubeEdgeVertexIndices[edgeIndex];
                ProjectedCubeVertex start = projectedCube.Vertices[edgeIndices.X];
                ProjectedCubeVertex end = projectedCube.Vertices[edgeIndices.Y];
                float amountAlongEdge;
                double distanceSquared = GetDistanceToSegmentSquared(localPointer, start.Position, end.Position, out amountAlongEdge);
                if (distanceSquared > bestEdgeDistanceSquared) {
                    continue;
                }

                float3 midpoint = float3.Lerp(UnitCubeVertices[edgeIndices.X], UnitCubeVertices[edgeIndices.Y], amountAlongEdge);
                EditorViewportNavigationTarget target = GetEdgeTarget(midpoint, edgeIndices);
                if (!HasVisibleAdjacentFace(target, projectedCube.CameraOrientation) ||
                    !IsSurfacePointVisible(new float2(
                        start.Position.X + ((end.Position.X - start.Position.X) * amountAlongEdge),
                        start.Position.Y + ((end.Position.Y - start.Position.Y) * amountAlongEdge)),
                        InterpolateDepth(start.Depth, end.Depth, amountAlongEdge),
                        projectedCube)) {
                    continue;
                }

                float depth = InterpolateDepth(start.Depth, end.Depth, amountAlongEdge);
                bool isCloserEdge = bestEdgeTarget == null || distanceSquared < (bestEdgeDistanceSquared - 0.0001);
                bool isDepthTie = Math.Abs(distanceSquared - bestEdgeDistanceSquared) <= 0.0001;
                if (isCloserEdge || (isDepthTie && depth > bestEdgeDepth)) {
                    bestEdgeDistanceSquared = distanceSquared;
                    bestEdgeDepth = depth;
                    bestEdgeTarget = target;
                }
            }
            if (bestEdgeTarget != null) {
                hit = new EditorViewportNavigationCubeHit(bestEdgeTarget, bestEdgeDepth);
                return true;
            }

            return TryHitVisibleFace(localPointer, projectedCube, out hit);
        }

        /// <summary>
        /// Projects all cube corners through the inverse camera orientation into one centered square UI bounds.
        /// </summary>
        /// <param name="cameraOrientation">World orientation of the viewport camera.</param>
        /// <param name="size">Square logical width and height of the cube bounds.</param>
        /// <returns>Camera-space corner positions, depths, and their common screen-space projection.</returns>
        ProjectedCube ProjectCube(float4 cameraOrientation, float size) {
            if (!float.IsFinite(size) || size <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(size), "Cube size must be finite and positive.");
            }

            float4 inverseOrientation = float4.Inverse(NormalizeOrientation(cameraOrientation));
            float3[] cameraPositions = new float3[UnitCubeVertices.Length];
            for (int vertexIndex = 0; vertexIndex < UnitCubeVertices.Length; vertexIndex++) {
                cameraPositions[vertexIndex] = float4.RotateVector(UnitCubeVertices[vertexIndex], inverseOrientation);
            }
            double scale = size / GetProjectionSpan(cameraOrientation);
            ProjectedCubeVertex[] vertices = new ProjectedCubeVertex[UnitCubeVertices.Length];
            for (int vertexIndex = 0; vertexIndex < cameraPositions.Length; vertexIndex++) {
                float3 cameraPosition = cameraPositions[vertexIndex];
                double divisor = ProjectionMode == CameraProjectionMode.Perspective ? CameraDistance - cameraPosition.Z : 1.0;
                vertices[vertexIndex] = new ProjectedCubeVertex(
                    new float2(
                        (float)((size * 0.5) + (cameraPosition.X * scale / divisor)),
                        (float)((size * 0.5) - (cameraPosition.Y * scale / divisor))),
                    cameraPosition.Z);
            }

            return new ProjectedCube(vertices, cameraPositions, NormalizeOrientation(cameraOrientation));
        }

        /// <summary>
        /// Resolves the visible face containing the pointer using the front-most overlapping surface depth.
        /// </summary>
        /// <param name="localPointer">Pointer position in cube-local pixels.</param>
        /// <param name="projectedCube">Projected cube used by the hit test.</param>
        /// <param name="hit">Resolved visible face when one contains the pointer.</param>
        /// <returns>True when a visible face polygon contains the pointer.</returns>
        bool TryHitVisibleFace(float2 localPointer, ProjectedCube projectedCube, out EditorViewportNavigationCubeHit hit) {
            hit = null;
            float closestSurfaceDepth = float.NegativeInfinity;
            EditorViewportNavigationTarget closestFaceTarget = null;
            for (int faceIndex = 0; faceIndex < FaceTargets.Length; faceIndex++) {
                EditorViewportNavigationTarget target = FaceTargets[faceIndex];
                if (!IsFaceVisible(target, projectedCube.CameraOrientation)) {
                    continue;
                }

                IReadOnlyList<float2> faceVertices = GetFaceVertices(target, projectedCube);
                if (!ContainsPoint(faceVertices, localPointer)) {
                    continue;
                }

                float faceDepth;
                if (TryGetFaceDepth(target, localPointer, projectedCube, out faceDepth) && faceDepth > closestSurfaceDepth) {
                    closestSurfaceDepth = faceDepth;
                    closestFaceTarget = target;
                }
            }

            if (closestFaceTarget == null) {
                return false;
            }

            hit = new EditorViewportNavigationCubeHit(closestFaceTarget, closestSurfaceDepth);
            return true;
        }

        /// <summary>
        /// Determines whether a projected point lies on the nearest visible surface of the cube.
        /// </summary>
        /// <param name="screenPoint">Projected screen position.</param>
        /// <param name="candidateDepth">Depth of the vertex or edge being considered.</param>
        /// <param name="projectedCube">Projected cube used by the hit test.</param>
        /// <returns>True when no visible face occludes the candidate.</returns>
        bool IsSurfacePointVisible(float2 screenPoint, float candidateDepth, ProjectedCube projectedCube) {
            float nearestSurfaceDepth = float.NegativeInfinity;
            for (int faceIndex = 0; faceIndex < FaceTargets.Length; faceIndex++) {
                EditorViewportNavigationTarget target = FaceTargets[faceIndex];
                if (!IsFaceVisible(target, projectedCube.CameraOrientation)) {
                    continue;
                }

                IReadOnlyList<float2> faceVertices = GetFaceVertices(target, projectedCube);
                if (!ContainsPoint(faceVertices, screenPoint)) {
                    continue;
                }

                float faceDepth;
                if (TryGetFaceDepth(target, screenPoint, projectedCube, out faceDepth)) {
                    nearestSurfaceDepth = Math.Max(nearestSurfaceDepth, faceDepth);
                }
            }

            return nearestSurfaceDepth == float.NegativeInfinity || candidateDepth + SurfaceDepthTolerance >= nearestSurfaceDepth;
        }

        /// <summary>
        /// Interpolates the camera-space depth of a non-degenerate projected face plane.
        /// </summary>
        /// <param name="target">Face whose plane is evaluated.</param>
        /// <param name="screenPoint">Screen position at which to evaluate depth.</param>
        /// <param name="projectedCube">Projected cube containing camera-space face corners.</param>
        /// <param name="depth">Interpolated camera-space depth when the method returns true.</param>
        /// <returns>True when the projected face has a usable two-dimensional area.</returns>
        bool TryGetFaceDepth(EditorViewportNavigationTarget target, float2 screenPoint, ProjectedCube projectedCube, out float depth) {
            int[] vertexIndices = FaceVertexIndices[GetFaceIndex(target)];
            ProjectedCubeVertex first = projectedCube.Vertices[vertexIndices[0]];
            ProjectedCubeVertex second = projectedCube.Vertices[vertexIndices[1]];
            ProjectedCubeVertex third = projectedCube.Vertices[vertexIndices[2]];
            double determinant =
                ((second.Position.X - first.Position.X) * (third.Position.Y - first.Position.Y)) -
                ((third.Position.X - first.Position.X) * (second.Position.Y - first.Position.Y));
            if (Math.Abs(determinant) <= MinimumProjectedFaceArea) {
                depth = 0f;
                return false;
            }

            double depthX =
                (((EncodeDepth(second.Depth) - EncodeDepth(first.Depth)) * (third.Position.Y - first.Position.Y)) -
                 ((EncodeDepth(third.Depth) - EncodeDepth(first.Depth)) * (second.Position.Y - first.Position.Y))) / determinant;
            double depthY =
                (((second.Position.X - first.Position.X) * (EncodeDepth(third.Depth) - EncodeDepth(first.Depth))) -
                 ((third.Position.X - first.Position.X) * (EncodeDepth(second.Depth) - EncodeDepth(first.Depth)))) / determinant;
            double depthOffset = EncodeDepth(first.Depth) - (depthX * first.Position.X) - (depthY * first.Position.Y);
            depth = DecodeDepth((depthX * screenPoint.X) + (depthY * screenPoint.Y) + depthOffset);
            return float.IsFinite(depth);
        }

        /// <summary>
        /// Returns the four projected face corners from a projection already computed by the current hit operation.
        /// </summary>
        /// <param name="target">One signed axis face target.</param>
        /// <param name="projectedCube">Projected cube used by the hit operation.</param>
        /// <returns>Four screen-space face corners.</returns>
        IReadOnlyList<float2> GetFaceVertices(EditorViewportNavigationTarget target, ProjectedCube projectedCube) {
            int[] vertexIndices = FaceVertexIndices[GetFaceIndex(target)];
            float2[] faceVertices = new float2[vertexIndices.Length];
            for (int faceVertexIndex = 0; faceVertexIndex < vertexIndices.Length; faceVertexIndex++) {
                faceVertices[faceVertexIndex] = projectedCube.Vertices[vertexIndices[faceVertexIndex]].Position;
            }

            return faceVertices;
        }

        /// <summary>
        /// Determines whether one signed axis face points toward the current camera.
        /// </summary>
        /// <param name="target">One signed axis face target.</param>
        /// <param name="cameraOrientation">World orientation of the viewport camera.</param>
        /// <returns>True when the transformed outward normal has positive camera-space depth.</returns>
        bool IsFaceVisible(EditorViewportNavigationTarget target, float4 cameraOrientation) {
            int faceIndex = GetFaceIndex(target);
            float3 cameraSpaceNormal = float4.RotateVector(FaceNormals[faceIndex], float4.Inverse(cameraOrientation));
            return cameraSpaceNormal.Z > (ProjectionMode == CameraProjectionMode.Perspective ? 0.5 / CameraDistance : 0.0) + VisibleFaceThreshold;
        }

        /// <summary>
        /// Determines whether an edge's shared projected location belongs to at least one visible adjacent face.
        /// </summary>
        /// <param name="target">Signed pair of fixed axis directions for the edge.</param>
        /// <param name="cameraOrientation">World orientation of the viewport camera.</param>
        /// <returns>True when at least one adjacent outward face is front-facing.</returns>
        bool HasVisibleAdjacentFace(EditorViewportNavigationTarget target, float4 cameraOrientation) {
            if (target.X != 0 && IsFaceVisible(new EditorViewportNavigationTarget(target.X, 0, 0), cameraOrientation)) {
                return true;
            }
            if (target.Y != 0 && IsFaceVisible(new EditorViewportNavigationTarget(0, target.Y, 0), cameraOrientation)) {
                return true;
            }
            return target.Z != 0 && IsFaceVisible(new EditorViewportNavigationTarget(0, 0, target.Z), cameraOrientation);
        }

        /// <summary>
        /// Resolves the face table index for one signed axis target.
        /// </summary>
        /// <param name="target">Face target to resolve.</param>
        /// <returns>Index into the face target and corner tables.</returns>
        int GetFaceIndex(EditorViewportNavigationTarget target) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }
            if (!target.IsFace) {
                throw new ArgumentException("A signed axis face target is required.", nameof(target));
            }

            for (int faceIndex = 0; faceIndex < FaceTargets.Length; faceIndex++) {
                if (FaceTargets[faceIndex].Equals(target)) {
                    return faceIndex;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(target), "Face target does not match a signed cube axis.");
        }

        /// <summary>
        /// Converts a projected cube edge midpoint and endpoint pair into the signed pair of fixed cube axes.
        /// </summary>
        /// <param name="midpoint">Cube-space midpoint of the edge.</param>
        /// <param name="edgeIndices">Projected cube-corner indices for the edge.</param>
        /// <returns>Target whose two non-zero axes identify the edge.</returns>
        static EditorViewportNavigationTarget GetEdgeTarget(float3 midpoint, int2 edgeIndices) {
            float3 firstVertex = UnitCubeVertices[edgeIndices.X];
            float3 secondVertex = UnitCubeVertices[edgeIndices.Y];
            int x = firstVertex.X == secondVertex.X ? Math.Sign(midpoint.X) : 0;
            int y = firstVertex.Y == secondVertex.Y ? Math.Sign(midpoint.Y) : 0;
            int z = firstVertex.Z == secondVertex.Z ? Math.Sign(midpoint.Z) : 0;
            return new EditorViewportNavigationTarget(x, y, z);
        }

        /// <summary>
        /// Converts one signed cube-space corner into its three-axis target.
        /// </summary>
        /// <param name="vertex">Signed unit-cube corner position.</param>
        /// <returns>Target that identifies the cube corner.</returns>
        static EditorViewportNavigationTarget GetCornerTarget(float3 vertex) {
            return new EditorViewportNavigationTarget(Math.Sign(vertex.X), Math.Sign(vertex.Y), Math.Sign(vertex.Z));
        }

        /// <summary>
        /// Normalizes and validates a camera orientation before rotating cube geometry.
        /// </summary>
        /// <param name="orientation">Viewport camera orientation.</param>
        /// <returns>Finite normalized quaternion.</returns>
        static float4 NormalizeOrientation(float4 orientation) {
            if (!float.IsFinite(orientation.X) || !float.IsFinite(orientation.Y) ||
                !float.IsFinite(orientation.Z) || !float.IsFinite(orientation.W)) {
                throw new ArgumentOutOfRangeException(nameof(orientation), "Camera orientation components must be finite.");
            }

            double lengthSquared =
                (orientation.X * orientation.X) +
                (orientation.Y * orientation.Y) +
                (orientation.Z * orientation.Z) +
                (orientation.W * orientation.W);
            if (lengthSquared <= 0.000000000001) {
                throw new ArgumentOutOfRangeException(nameof(orientation), "Camera orientation must have non-zero length.");
            }

            orientation.Normalize();
            return orientation;
        }

        /// <summary>
        /// Determines whether a point lies inside or on a four-corner projected polygon.
        /// </summary>
        /// <param name="vertices">Ordered screen-space polygon corners.</param>
        /// <param name="point">Screen-space point to check.</param>
        /// <returns>True when the point lies inside the polygon.</returns>
        static bool ContainsPoint(IReadOnlyList<float2> vertices, float2 point) {
            bool isInside = false;
            for (int currentIndex = 0, previousIndex = vertices.Count - 1; currentIndex < vertices.Count; previousIndex = currentIndex++) {
                float2 current = vertices[currentIndex];
                float2 previous = vertices[previousIndex];
                float boundaryAmount;
                if (GetDistanceToSegmentSquared(point, previous, current, out boundaryAmount) <= 0.0001) {
                    return true;
                }

                bool crossesHorizontalRay = (current.Y > point.Y) != (previous.Y > point.Y);
                if (crossesHorizontalRay && point.X <
                    (((previous.X - current.X) * (point.Y - current.Y) / (previous.Y - current.Y)) + current.X)) {
                    isInside = !isInside;
                }
            }

            return isInside;
        }

        /// <summary>
        /// Computes squared distance from a point to a projected corner.
        /// </summary>
        /// <param name="first">First screen-space point.</param>
        /// <param name="second">Second screen-space point.</param>
        /// <returns>Squared Euclidean distance.</returns>
        static double GetDistanceSquared(float2 first, float2 second) {
            double x = first.X - second.X;
            double y = first.Y - second.Y;
            return (x * x) + (y * y);
        }

        /// <summary>
        /// Computes squared distance and closest interpolation parameter from a point to a projected edge segment.
        /// </summary>
        /// <param name="point">Screen-space point.</param>
        /// <param name="start">Projected segment start.</param>
        /// <param name="end">Projected segment end.</param>
        /// <param name="amountAlongSegment">Clamped segment parameter at the closest point.</param>
        /// <returns>Squared distance from point to closest segment point.</returns>
        static double GetDistanceToSegmentSquared(float2 point, float2 start, float2 end, out float amountAlongSegment) {
            double deltaX = end.X - start.X;
            double deltaY = end.Y - start.Y;
            double segmentLengthSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (segmentLengthSquared <= 0.000000000001) {
                amountAlongSegment = 0f;
                return GetDistanceSquared(point, start);
            }

            double amount = Math.Clamp((((point.X - start.X) * deltaX) + ((point.Y - start.Y) * deltaY)) / segmentLengthSquared, 0.0, 1.0);
            amountAlongSegment = (float)amount;
            float2 closestPoint = new float2((float)(start.X + (deltaX * amount)), (float)(start.Y + (deltaY * amount)));
            return GetDistanceSquared(point, closestPoint);
        }

        /// <summary>
        /// Computes the camera-space depth of each projected cube corner.
        /// </summary>
        sealed class ProjectedCube {
            /// <summary>
            /// Initializes a projection snapshot used consistently throughout one render or hit operation.
            /// </summary>
            /// <param name="vertices">Screen-space and depth records for all eight cube corners.</param>
            /// <param name="cameraPositions">Camera-space position of each cube corner.</param>
            /// <param name="cameraOrientation">Normalized camera orientation used to build the snapshot.</param>
            public ProjectedCube(ProjectedCubeVertex[] vertices, float3[] cameraPositions, float4 cameraOrientation) {
                Vertices = vertices;
                CameraPositions = cameraPositions;
                CameraOrientation = cameraOrientation;
            }

            /// <summary>
            /// Gets screen-space and camera-depth values for all cube corners.
            /// </summary>
            public ProjectedCubeVertex[] Vertices { get; }
            /// <summary>
            /// Gets camera-space positions used for cube-face projection.
            /// </summary>
            public float3[] CameraPositions { get; }
            /// <summary>
            /// Gets the camera orientation associated with this projection.
            /// </summary>
            public float4 CameraOrientation { get; }
        }

        /// <summary>
        /// Stores one cube corner's shared screen-space projection and camera-space depth.
        /// </summary>
        sealed class ProjectedCubeVertex {
            /// <summary>
            /// Initializes one projected cube corner.
            /// </summary>
            /// <param name="position">Screen-space location in cube-local logical pixels.</param>
            /// <param name="depth">Camera-space depth used to resolve overlapping surfaces.</param>
            public ProjectedCubeVertex(float2 position, float depth) {
                Position = position;
                Depth = depth;
            }

            /// <summary>
            /// Gets the corner location shared by rendering and hit testing.
            /// </summary>
            public float2 Position { get; }
            /// <summary>
            /// Gets the camera-space depth of the corner.
            /// </summary>
            public float Depth { get; }
        }
    }
}
