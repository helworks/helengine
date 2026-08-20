namespace helengine {
    /// <summary>
    /// Rewrites model texture coordinates from box or world-plane projections for cook-time UVW mapping.
    /// </summary>
    public static class ModelUvwMapProcessor {
        /// <summary>
        /// Stable mode identifier for local-space box projection.
        /// </summary>
        public const string BoxMode = "Box";

        /// <summary>
        /// Stable mode identifier for world-plane projection.
        /// </summary>
        public const string WorldMode = "World";

        /// <summary>
        /// Stable plane identifier projecting world X to U and world Y to V.
        /// </summary>
        public const string PlaneXY = "XY";

        /// <summary>
        /// Stable plane identifier projecting world X to U and world Z to V.
        /// </summary>
        public const string PlaneXZ = "XZ";

        /// <summary>
        /// Stable plane identifier projecting world Z to U and world Y to V.
        /// </summary>
        public const string PlaneZY = "ZY";

        /// <summary>
        /// Projects world-space vertex positions onto one axis plane and stores them as texture coordinates.
        /// </summary>
        /// <param name="asset">Model whose texture coordinates should be rewritten in place.</param>
        /// <param name="plane">Axis plane identifier selecting the two projected world axes.</param>
        /// <param name="scale">World units covered by one repeat of the texture.</param>
        /// <param name="worldPosition">World position of the owning entity.</param>
        /// <param name="worldOrientation">World orientation of the owning entity.</param>
        /// <param name="worldScale">World scale of the owning entity.</param>
        public static void ApplyWorldMap(
            ModelAsset asset,
            string plane,
            double scale,
            float3 worldPosition,
            float4 worldOrientation,
            float3 worldScale) {
            ValidateAsset(asset);
            ValidateScale(scale);
            if (!IsSupportedPlane(plane)) {
                throw new ArgumentException($"World map plane '{plane}' is not supported.", nameof(plane));
            }

            float inverseScale = (float)(1d / scale);
            float2[] texCoords = new float2[asset.Positions.Length];
            for (int vertexIndex = 0; vertexIndex < asset.Positions.Length; vertexIndex++) {
                float3 localPosition = asset.Positions[vertexIndex];
                float3 scaledPosition = new float3(
                    localPosition.X * worldScale.X,
                    localPosition.Y * worldScale.Y,
                    localPosition.Z * worldScale.Z);
                float3 worldVertex = worldPosition + float4.RotateVector(scaledPosition, worldOrientation);
                float2 projected = ProjectPlane(worldVertex, plane);
                texCoords[vertexIndex] = new float2(projected.X * inverseScale, projected.Y * inverseScale);
            }

            asset.TexCoords = texCoords;
        }

        /// <summary>
        /// Projects local-space vertex positions per dominant triangle axis and stores them as texture coordinates,
        /// splitting shared vertices whose triangles project onto different box faces.
        /// </summary>
        /// <param name="asset">Model whose texture coordinates should be rewritten in place.</param>
        /// <param name="scale">Local units covered by one repeat of the texture.</param>
        public static void ApplyBoxMap(ModelAsset asset, double scale) {
            ValidateAsset(asset);
            ValidateScale(scale);
            if (asset.Indices16 == null || asset.Indices16.Length == 0) {
                throw new InvalidOperationException("Box mapping requires 16-bit model indices.");
            }

            float inverseScale = (float)(1d / scale);
            List<float3> positions = new List<float3>(asset.Positions);
            List<float3> normals = asset.Normals != null ? new List<float3>(asset.Normals) : null;
            List<float2> texCoords = new List<float2>(new float2[asset.Positions.Length]);
            int[] assignedAxisByVertex = new int[asset.Positions.Length];
            Dictionary<(int VertexIndex, int Axis), int> splitVertices = new Dictionary<(int, int), int>();
            ushort[] indices = asset.Indices16;

            for (int index = 0; index + 2 < indices.Length; index += 3) {
                float3 positionA = positions[indices[index]];
                float3 positionB = positions[indices[index + 1]];
                float3 positionC = positions[indices[index + 2]];
                int axis = ResolveDominantAxis(float3.Cross(positionB - positionA, positionC - positionA));

                for (int corner = 0; corner < 3; corner++) {
                    int vertexIndex = indices[index + corner];
                    int resolvedIndex = ResolveVertexForAxis(
                        vertexIndex,
                        axis,
                        positions,
                        normals,
                        texCoords,
                        assignedAxisByVertex,
                        splitVertices);
                    float2 projected = ProjectAxis(positions[resolvedIndex], axis);
                    texCoords[resolvedIndex] = new float2(projected.X * inverseScale, projected.Y * inverseScale);
                    if (resolvedIndex > ushort.MaxValue) {
                        throw new InvalidOperationException("Box mapping vertex splitting exceeded 16-bit index capacity.");
                    }

                    indices[index + corner] = (ushort)resolvedIndex;
                }
            }

            asset.Positions = positions.ToArray();
            if (normals != null) {
                asset.Normals = normals.ToArray();
            }

            asset.TexCoords = texCoords.ToArray();
        }

        /// <summary>
        /// Returns whether one plane identifier is supported by world mapping.
        /// </summary>
        /// <param name="plane">Plane identifier to validate.</param>
        /// <returns>True for the supported XY, XZ, and ZY identifiers.</returns>
        public static bool IsSupportedPlane(string plane) {
            return string.Equals(plane, PlaneXY, StringComparison.Ordinal)
                || string.Equals(plane, PlaneXZ, StringComparison.Ordinal)
                || string.Equals(plane, PlaneZY, StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns whether one mode identifier is supported by UVW mapping.
        /// </summary>
        /// <param name="mode">Mode identifier to validate.</param>
        /// <returns>True for the supported Box and World identifiers.</returns>
        public static bool IsSupportedMode(string mode) {
            return string.Equals(mode, BoxMode, StringComparison.Ordinal)
                || string.Equals(mode, WorldMode, StringComparison.Ordinal);
        }

        /// <summary>
        /// Resolves one vertex index for a triangle projected on the supplied axis, splitting shared vertices whose
        /// previously assigned axis differs.
        /// </summary>
        static int ResolveVertexForAxis(
            int vertexIndex,
            int axis,
            List<float3> positions,
            List<float3> normals,
            List<float2> texCoords,
            int[] assignedAxisByVertex,
            Dictionary<(int VertexIndex, int Axis), int> splitVertices) {
            if (vertexIndex < assignedAxisByVertex.Length) {
                if (assignedAxisByVertex[vertexIndex] == 0) {
                    assignedAxisByVertex[vertexIndex] = axis + 1;
                    return vertexIndex;
                }
                if (assignedAxisByVertex[vertexIndex] == axis + 1) {
                    return vertexIndex;
                }
            }

            if (splitVertices.TryGetValue((vertexIndex, axis), out int existingSplitIndex)) {
                return existingSplitIndex;
            }

            positions.Add(positions[vertexIndex]);
            normals?.Add(normals[vertexIndex]);
            texCoords.Add(default);
            int splitIndex = positions.Count - 1;
            splitVertices[(vertexIndex, axis)] = splitIndex;
            return splitIndex;
        }

        /// <summary>
        /// Resolves the dominant axis of one geometric triangle normal.
        /// </summary>
        /// <param name="normal">Unnormalized geometric triangle normal.</param>
        /// <returns>0 for X-dominant, 1 for Y-dominant, and 2 for Z-dominant triangles.</returns>
        static int ResolveDominantAxis(float3 normal) {
            float absoluteX = Math.Abs(normal.X);
            float absoluteY = Math.Abs(normal.Y);
            float absoluteZ = Math.Abs(normal.Z);
            if (absoluteX >= absoluteY && absoluteX >= absoluteZ) {
                return 0;
            }

            return absoluteY >= absoluteZ ? 1 : 2;
        }

        /// <summary>
        /// Projects one position onto the plane perpendicular to the supplied dominant axis.
        /// </summary>
        /// <param name="position">Position to project.</param>
        /// <param name="axis">Dominant axis index.</param>
        /// <returns>Two projected components.</returns>
        static float2 ProjectAxis(float3 position, int axis) {
            if (axis == 0) {
                return new float2(position.Z, position.Y);
            }
            if (axis == 1) {
                return new float2(position.X, position.Z);
            }

            return new float2(position.X, position.Y);
        }

        /// <summary>
        /// Projects one world position onto the requested axis plane.
        /// </summary>
        /// <param name="worldVertex">World-space vertex position.</param>
        /// <param name="plane">Axis plane identifier.</param>
        /// <returns>Two projected components.</returns>
        static float2 ProjectPlane(float3 worldVertex, string plane) {
            if (string.Equals(plane, PlaneXY, StringComparison.Ordinal)) {
                return new float2(worldVertex.X, worldVertex.Y);
            }
            if (string.Equals(plane, PlaneXZ, StringComparison.Ordinal)) {
                return new float2(worldVertex.X, worldVertex.Z);
            }

            return new float2(worldVertex.Z, worldVertex.Y);
        }

        /// <summary>
        /// Validates the model asset carries the position stream required for projection.
        /// </summary>
        /// <param name="asset">Model asset to validate.</param>
        static void ValidateAsset(ModelAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }
            if (asset.Positions == null || asset.Positions.Length == 0) {
                throw new InvalidOperationException("UVW mapping requires model positions.");
            }
        }

        /// <summary>
        /// Validates one finite positive projection scale.
        /// </summary>
        /// <param name="scale">World or local units covered by one texture repeat.</param>
        static void ValidateScale(double scale) {
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(scale), "UVW map scales must be finite and positive.");
            }
        }
    }
}
