namespace helengine {
    /// <summary>Builds the shared hollow rotation-ring mesh and its axis transforms for runtime and editor gizmos.</summary>
    public static class RotationGizmoGeometry {
        /// <summary>Maps the local Y-normal ring to axis zero (X), one (Y), or two (Z).</summary>
        /// <param name="axis">Axis index in the gizmo's local orientation.</param>
        /// <returns>Unit quaternion used for both drawing and picking that axis ring.</returns>
        public static float4 GetAxisOrientation(int axis) {
            if (axis == 1) {
                return float4.Identity;
            }
            if (axis != 0 && axis != 2) {
                throw new ArgumentOutOfRangeException(nameof(axis), "A rotation axis must be X, Y or Z.");
            }
            float3 rotationAxis = axis == 0 ? new float3(0, 0, 1) : new float3(1, 0, 0);
            RealEvaluation angle = axis == 0 ? -Math.PI * 0.5 : Math.PI * 0.5;
            float4.CreateFromAxisAngle(ref rotationAxis, angle, out float4 orientation);
            return orientation;
        }

        /// <summary>
        /// Builds a hollow tube ring centered at the origin around the local Y axis.
        /// </summary>
        /// <param name="innerRadius">Inner radius of the ring hole.</param>
        /// <param name="outerRadius">Outer radius of the ring body.</param>
        /// <param name="height">Axial thickness of the ring along local Y.</param>
        /// <param name="segments">Segment count around the circular ring.</param>
        /// <returns>Generated hollow tube-ring model asset.</returns>
        [NativeOwnedReturn]
        public static ModelAsset CreateTubeRing(float innerRadius, float outerRadius, float height, int segments) {
            ValidateTubeRingArguments(innerRadius, outerRadius, height, segments);

            List<float3> positions = new List<float3>(segments * 8);
            List<float3> normals = new List<float3>(segments * 8);
            List<float2> texCoords = new List<float2>(segments * 8);
            List<int> indices = new List<int>(segments * 48);
            float halfHeight = height * 0.5f;

            int outerSideStart = positions.Count;
            for (int segmentIndex = 0; segmentIndex < segments; segmentIndex++) {
                RealEvaluation fraction = (RealEvaluation)segmentIndex / segments;
                RealEvaluation angle = fraction * Math.PI * 2.0;
                float x = (float)(RealMath.Cos(angle) * outerRadius);
                float z = (float)(RealMath.Sin(angle) * outerRadius);
                float3 normal = NormalizeXZ(x, z);

                positions.Add(new float3(x, -halfHeight, z));
                positions.Add(new float3(x, halfHeight, z));
                normals.Add(normal);
                normals.Add(normal);
                texCoords.Add(new float2((float)fraction, 0f));
                texCoords.Add(new float2((float)fraction, 1f));
            }

            for (int segmentIndex = 0; segmentIndex < segments; segmentIndex++) {
                int currentBottom = outerSideStart + (segmentIndex * 2);
                int currentTop = currentBottom + 1;
                int nextBottom = outerSideStart + (((segmentIndex + 1) % segments) * 2);
                int nextTop = nextBottom + 1;
                AddDoubleSidedTriangle(indices, currentBottom, currentTop, nextTop);
                AddDoubleSidedTriangle(indices, currentBottom, nextTop, nextBottom);
            }

            int innerSideStart = positions.Count;
            for (int segmentIndex = 0; segmentIndex < segments; segmentIndex++) {
                RealEvaluation fraction = (RealEvaluation)segmentIndex / segments;
                RealEvaluation angle = fraction * Math.PI * 2.0;
                float x = (float)(RealMath.Cos(angle) * innerRadius);
                float z = (float)(RealMath.Sin(angle) * innerRadius);
                float3 normal = NormalizeXZ(-x, -z);

                positions.Add(new float3(x, -halfHeight, z));
                positions.Add(new float3(x, halfHeight, z));
                normals.Add(normal);
                normals.Add(normal);
                texCoords.Add(new float2((float)fraction, 0f));
                texCoords.Add(new float2((float)fraction, 1f));
            }

            for (int segmentIndex = 0; segmentIndex < segments; segmentIndex++) {
                int currentBottom = innerSideStart + (segmentIndex * 2);
                int currentTop = currentBottom + 1;
                int nextBottom = innerSideStart + (((segmentIndex + 1) % segments) * 2);
                int nextTop = nextBottom + 1;
                AddDoubleSidedTriangle(indices, currentBottom, nextBottom, nextTop);
                AddDoubleSidedTriangle(indices, currentBottom, nextTop, currentTop);
            }

            int topCapStart = positions.Count;
            for (int segmentIndex = 0; segmentIndex < segments; segmentIndex++) {
                RealEvaluation fraction = (RealEvaluation)segmentIndex / segments;
                RealEvaluation angle = fraction * Math.PI * 2.0;
                float outerX = (float)(RealMath.Cos(angle) * outerRadius);
                float outerZ = (float)(RealMath.Sin(angle) * outerRadius);
                float innerX = (float)(RealMath.Cos(angle) * innerRadius);
                float innerZ = (float)(RealMath.Sin(angle) * innerRadius);

                positions.Add(new float3(outerX, halfHeight, outerZ));
                positions.Add(new float3(innerX, halfHeight, innerZ));
                normals.Add(new float3(0f, 1f, 0f));
                normals.Add(new float3(0f, 1f, 0f));
                texCoords.Add(new float2((float)fraction, 0f));
                texCoords.Add(new float2((float)fraction, 1f));
            }

            for (int segmentIndex = 0; segmentIndex < segments; segmentIndex++) {
                int currentOuter = topCapStart + (segmentIndex * 2);
                int currentInner = currentOuter + 1;
                int nextOuter = topCapStart + (((segmentIndex + 1) % segments) * 2);
                int nextInner = nextOuter + 1;
                AddDoubleSidedTriangle(indices, currentOuter, currentInner, nextInner);
                AddDoubleSidedTriangle(indices, currentOuter, nextInner, nextOuter);
            }

            int bottomCapStart = positions.Count;
            for (int segmentIndex = 0; segmentIndex < segments; segmentIndex++) {
                RealEvaluation fraction = (RealEvaluation)segmentIndex / segments;
                RealEvaluation angle = fraction * Math.PI * 2.0;
                float outerX = (float)(RealMath.Cos(angle) * outerRadius);
                float outerZ = (float)(RealMath.Sin(angle) * outerRadius);
                float innerX = (float)(RealMath.Cos(angle) * innerRadius);
                float innerZ = (float)(RealMath.Sin(angle) * innerRadius);

                positions.Add(new float3(outerX, -halfHeight, outerZ));
                positions.Add(new float3(innerX, -halfHeight, innerZ));
                normals.Add(new float3(0f, -1f, 0f));
                normals.Add(new float3(0f, -1f, 0f));
                texCoords.Add(new float2((float)fraction, 0f));
                texCoords.Add(new float2((float)fraction, 1f));
            }

            for (int segmentIndex = 0; segmentIndex < segments; segmentIndex++) {
                int currentOuter = bottomCapStart + (segmentIndex * 2);
                int currentInner = currentOuter + 1;
                int nextOuter = bottomCapStart + (((segmentIndex + 1) % segments) * 2);
                int nextInner = nextOuter + 1;
                AddDoubleSidedTriangle(indices, currentOuter, nextOuter, nextInner);
                AddDoubleSidedTriangle(indices, currentOuter, nextInner, currentInner);
            }

            return CreateModelAsset(positions, normals, texCoords, indices);
        }

        /// <summary>
        /// Validates hollow tube-ring generation arguments.
        /// </summary>
        /// <param name="innerRadius">Inner radius of the ring hole.</param>
        /// <param name="outerRadius">Outer radius of the ring body.</param>
        /// <param name="height">Axial thickness of the ring along local Y.</param>
        /// <param name="segments">Segment count around the circular ring.</param>
        static void ValidateTubeRingArguments(float innerRadius, float outerRadius, float height, int segments) {
            if (!float.IsFinite(innerRadius) || innerRadius <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(innerRadius), "Inner radius must be greater than zero.");
            }

            if (!float.IsFinite(outerRadius) || outerRadius <= innerRadius) {
                throw new ArgumentOutOfRangeException(nameof(outerRadius), "Outer radius must be greater than inner radius.");
            }

            if (!float.IsFinite(height) || height <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(height), "Ring height must be greater than zero.");
            }

            if (segments < 3 || segments > ushort.MaxValue / 8) {
                throw new ArgumentOutOfRangeException(nameof(segments), "At least three ring segments are required.");
            }
        }

        /// <summary>
        /// Creates a model asset from generated vertex streams and triangle indices.
        /// </summary>
        /// <param name="positions">Generated vertex positions.</param>
        /// <param name="normals">Generated vertex normals.</param>
        /// <param name="texCoords">Generated vertex texture coordinates.</param>
        /// <param name="indices">Generated triangle indices.</param>
        /// <returns>Generated model asset.</returns>
        static ModelAsset CreateModelAsset(
            List<float3> positions,
            List<float3> normals,
            List<float2> texCoords,
            List<int> indices) {
            if (positions == null) {
                throw new ArgumentNullException(nameof(positions));
            }

            if (normals == null) {
                throw new ArgumentNullException(nameof(normals));
            }

            if (texCoords == null) {
                throw new ArgumentNullException(nameof(texCoords));
            }

            if (indices == null) {
                throw new ArgumentNullException(nameof(indices));
            }

            if (positions.Count == 0) {
                throw new InvalidOperationException("Mesh generation produced no vertices.");
            }

            if (positions.Count != normals.Count) {
                throw new InvalidOperationException("Vertex position and normal counts must match.");
            }

            if (positions.Count != texCoords.Count) {
                throw new InvalidOperationException("Vertex position and UV counts must match.");
            }

            if (positions.Count > ushort.MaxValue) {
                throw new InvalidOperationException("Mesh uses more vertices than 16-bit indices support.");
            }

            ModelAsset modelAsset = new ModelAsset();
            modelAsset.Id = Guid.NewGuid().ToString("N");
            modelAsset.Positions = positions.ToArray();
            modelAsset.Normals = normals.ToArray();
            modelAsset.TexCoords = texCoords.ToArray();
            modelAsset.Indices16 = ConvertIndices(indices);
            return modelAsset;
        }

        /// <summary>
        /// Converts integer triangle indices to 16-bit indices with range validation.
        /// </summary>
        /// <param name="indices">Triangle indices to convert.</param>
        /// <returns>Converted 16-bit index buffer.</returns>
        static ushort[] ConvertIndices(List<int> indices) {
            if (indices == null) {
                throw new ArgumentNullException(nameof(indices));
            }

            ushort[] converted = new ushort[indices.Count];
            for (int i = 0; i < indices.Count; i++) {
                int index = indices[i];
                if (index < 0 || index > ushort.MaxValue) {
                    throw new InvalidOperationException("Mesh index exceeds 16-bit range.");
                }

                converted[i] = (ushort)index;
            }

            return converted;
        }

        /// <summary>
        /// Adds a triangle to the index list in both winding orders.
        /// </summary>
        /// <param name="indices">Index list to append to.</param>
        /// <param name="a">First vertex index.</param>
        /// <param name="b">Second vertex index.</param>
        /// <param name="c">Third vertex index.</param>
        static void AddDoubleSidedTriangle(List<int> indices, int a, int b, int c) {
            if (indices == null) {
                throw new ArgumentNullException(nameof(indices));
            }

            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
            indices.Add(a);
            indices.Add(c);
            indices.Add(b);
        }

        /// <summary>
        /// Normalizes an XZ vector into a unit vector on the XZ plane.
        /// </summary>
        /// <param name="x">X component.</param>
        /// <param name="z">Z component.</param>
        /// <returns>Normalized XZ vector.</returns>
        static float3 NormalizeXZ(float x, float z) {
            RealEvaluation length = RealMath.Sqrt((RealEvaluation)x * x + (RealEvaluation)z * z);
            if (length <= 0.0) {
                throw new InvalidOperationException("Cannot normalize a zero-length XZ vector.");
            }

            return new float3((float)(x / length), 0f, (float)(z / length));
        }
    }
}
