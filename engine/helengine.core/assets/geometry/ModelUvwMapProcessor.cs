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
        /// Stable axis identifier selecting the world X component.
        /// </summary>
        public const string AxisX = "X";

        /// <summary>
        /// Stable axis identifier selecting the world Y component.
        /// </summary>
        public const string AxisY = "Y";

        /// <summary>
        /// Stable axis identifier selecting the world Z component.
        /// </summary>
        public const string AxisZ = "Z";

        /// <summary>
        /// Maps world-space vertex positions to texture coordinates using one chosen world axis per UV component.
        /// </summary>
        /// <param name="asset">Model whose texture coordinates should be rewritten in place.</param>
        /// <param name="axisU">World axis identifier mapped to the U component.</param>
        /// <param name="axisV">World axis identifier mapped to the V component.</param>
        /// <param name="scaleU">Texture repeats per world unit along U.</param>
        /// <param name="scaleV">Texture repeats per world unit along V.</param>
        /// <param name="offsetU">Offset added to the U component after scaling.</param>
        /// <param name="offsetV">Offset added to the V component after scaling.</param>
        /// <param name="worldPosition">World position of the owning entity.</param>
        /// <param name="worldOrientation">World orientation of the owning entity.</param>
        public static void ApplyWorldMap(
            ModelAsset asset,
            string axisU,
            string axisV,
            double scaleU,
            double scaleV,
            double offsetU,
            double offsetV,
            float3 worldPosition,
            float4 worldOrientation) {
            ValidateAsset(asset);
            ValidateFinite(scaleU, nameof(scaleU));
            ValidateFinite(scaleV, nameof(scaleV));
            ValidateFinite(offsetU, nameof(offsetU));
            ValidateFinite(offsetV, nameof(offsetV));
            if (!IsSupportedAxis(axisU)) {
                throw new ArgumentException($"World map axis '{axisU}' is not supported.", nameof(axisU));
            }
            if (!IsSupportedAxis(axisV)) {
                throw new ArgumentException($"World map axis '{axisV}' is not supported.", nameof(axisV));
            }

            float2[] texCoords = new float2[asset.Positions.Length];
            for (int vertexIndex = 0; vertexIndex < asset.Positions.Length; vertexIndex++) {
                float3 worldVertex = worldPosition + float4.RotateVector(asset.Positions[vertexIndex], worldOrientation);
                texCoords[vertexIndex] = new float2(
                    SelectAxisComponent(worldVertex, axisU) * (float)scaleU + (float)offsetU,
                    SelectAxisComponent(worldVertex, axisV) * (float)scaleV + (float)offsetV);
            }

            NativeOwnership.Release(ref asset.TexCoords);
            asset.TexCoords = texCoords;
        }

        /// <summary>
        /// Projects local-space vertex positions per dominant triangle axis and stores them as texture coordinates,
        /// splitting shared vertices whose triangles project onto different box faces.
        /// </summary>
        /// <param name="asset">Model whose texture coordinates should be rewritten in place.</param>
        /// <param name="boxWidth">Mapping box size along the X axis in world units; one texture repeat spans this distance.</param>
        /// <param name="boxHeight">Mapping box size along the Y axis in world units; one texture repeat spans this distance.</param>
        /// <param name="boxLength">Mapping box size along the Z axis in world units; one texture repeat spans this distance.</param>
        /// <param name="tileU">Tiling multiplier applied on top of the box width.</param>
        /// <param name="tileV">Tiling multiplier applied on top of the box height.</param>
        /// <param name="tileW">Tiling multiplier applied on top of the box length.</param>
        /// <param name="offsetU">Offset added to the U component after mapping.</param>
        /// <param name="offsetV">Offset added to the V component after mapping.</param>
        public static void ApplyBoxMap(
            ModelAsset asset,
            double boxWidth,
            double boxHeight,
            double boxLength,
            double tileU,
            double tileV,
            double tileW,
            double offsetU,
            double offsetV) {
            ValidateAsset(asset);
            ValidateBoxDimension(boxWidth, nameof(boxWidth));
            ValidateBoxDimension(boxHeight, nameof(boxHeight));
            ValidateBoxDimension(boxLength, nameof(boxLength));
            ValidateFinite(tileU, nameof(tileU));
            ValidateFinite(tileV, nameof(tileV));
            ValidateFinite(tileW, nameof(tileW));
            ValidateFinite(offsetU, nameof(offsetU));
            ValidateFinite(offsetV, nameof(offsetV));
            if (asset.Indices16 == null || asset.Indices16.Length == 0) {
                throw new InvalidOperationException("Box mapping requires 16-bit model indices.");
            }
            if (asset.Normals == null || asset.Normals.Length != asset.Positions.Length) {
                throw new InvalidOperationException("Box mapping requires an equally sized normal stream.");
            }

            float3 axisScale = new float3(
                (float)(tileU / boxWidth),
                (float)(tileV / boxHeight),
                (float)(tileW / boxLength));
            float2 uvOffset = new float2((float)offsetU, (float)offsetV);
            List<float3> positions = new List<float3>(asset.Positions);
            List<float3> normals = new List<float3>(asset.Normals);
            List<float2> texCoords = new List<float2>(new float2[asset.Positions.Length]);
            int[] assignedAxisByVertex = new int[asset.Positions.Length];
            Dictionary<int, int> splitVertices = new Dictionary<int, int>();
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
                    texCoords[resolvedIndex] = ProjectAxisScaled(positions[resolvedIndex], axis, axisScale) + uvOffset;
                    if (resolvedIndex > ushort.MaxValue) {
                        throw new InvalidOperationException("Box mapping vertex splitting exceeded 16-bit index capacity.");
                    }

                    indices[index + corner] = (ushort)resolvedIndex;
                }
            }

            float3[] outputPositions = positions.ToArray();
            float3[] outputNormals = normals.ToArray();
            float2[] outputTexCoords = texCoords.ToArray();
            NativeOwnership.Release(ref asset.Positions);
            NativeOwnership.Release(ref asset.Normals);
            NativeOwnership.Release(ref asset.TexCoords);
            asset.Positions = outputPositions;
            asset.Normals = outputNormals;
            asset.TexCoords = outputTexCoords;
        }

        /// <summary>
        /// Returns whether one world axis identifier is supported by world mapping.
        /// </summary>
        /// <param name="axis">Axis identifier to validate.</param>
        /// <returns>True for the supported X, Y, and Z identifiers.</returns>
        public static bool IsSupportedAxis(string axis) {
            return string.Equals(axis, AxisX, StringComparison.Ordinal)
                || string.Equals(axis, AxisY, StringComparison.Ordinal)
                || string.Equals(axis, AxisZ, StringComparison.Ordinal);
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
            Dictionary<int, int> splitVertices) {
            if (vertexIndex < assignedAxisByVertex.Length) {
                if (assignedAxisByVertex[vertexIndex] == 0) {
                    assignedAxisByVertex[vertexIndex] = axis + 1;
                    return vertexIndex;
                }
                if (assignedAxisByVertex[vertexIndex] == axis + 1) {
                    return vertexIndex;
                }
            }

            int splitKey = vertexIndex * 3 + axis;
            if (splitVertices.TryGetValue(splitKey, out int existingSplitIndex)) {
                return existingSplitIndex;
            }

            positions.Add(positions[vertexIndex]);
            normals.Add(normals[vertexIndex]);
            texCoords.Add(new float2(0f, 0f));
            int splitIndex = positions.Count - 1;
            splitVertices[splitKey] = splitIndex;
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
        /// Projects one local position onto the plane perpendicular to the supplied dominant axis,
        /// multiplying each projected component by its own axis tiling scale.
        /// </summary>
        /// <param name="position">Local position to project.</param>
        /// <param name="axis">Dominant axis index.</param>
        /// <param name="axisScale">Per-axis texture repeats per local unit.</param>
        /// <returns>Two projected texture coordinates.</returns>
        static float2 ProjectAxisScaled(float3 position, int axis, float3 axisScale) {
            if (axis == 0) {
                return new float2(position.Z * axisScale.Z, position.Y * axisScale.Y);
            }
            if (axis == 1) {
                return new float2(position.X * axisScale.X, position.Z * axisScale.Z);
            }

            return new float2(position.X * axisScale.X, position.Y * axisScale.Y);
        }

        /// <summary>
        /// Selects one world position component by its axis identifier.
        /// </summary>
        /// <param name="worldVertex">World-space vertex position.</param>
        /// <param name="axis">Axis identifier to select.</param>
        /// <returns>Selected world component.</returns>
        static float SelectAxisComponent(float3 worldVertex, string axis) {
            if (string.Equals(axis, AxisX, StringComparison.Ordinal)) {
                return worldVertex.X;
            }
            if (string.Equals(axis, AxisY, StringComparison.Ordinal)) {
                return worldVertex.Y;
            }

            return worldVertex.Z;
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
        /// Validates one finite scale or offset value.
        /// </summary>
        /// <param name="value">Scale or offset value to validate.</param>
        /// <param name="parameterName">Name of the validated parameter.</param>
        static void ValidateFinite(double value, string parameterName) {
            if (double.IsNaN(value) || double.IsInfinity(value)) {
                throw new ArgumentOutOfRangeException(parameterName, "UVW map scales and offsets must be finite.");
            }
        }

        /// <summary>
        /// Validates one finite non-zero mapping box dimension.
        /// </summary>
        /// <param name="value">Box dimension in world units.</param>
        /// <param name="parameterName">Name of the validated parameter.</param>
        static void ValidateBoxDimension(double value, string parameterName) {
            if (double.IsNaN(value) || double.IsInfinity(value) || value == 0d) {
                throw new ArgumentOutOfRangeException(parameterName, "UVW box dimensions must be finite and non-zero.");
            }
        }
    }
}
