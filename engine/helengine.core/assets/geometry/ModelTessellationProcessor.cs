namespace helengine {
    /// <summary>
    /// Subdivides imported indexed model geometry into conforming smaller triangles before platform cooking.
    /// </summary>
    public static class ModelTessellationProcessor {
        /// <summary>
        /// Maximum number of output triangles accepted from a single imported model.
        /// </summary>
        const int MaximumTriangleCount = 1000000;

        /// <summary>
        /// Tessellates an indexed model until every triangle edge is at most the supplied length.
        /// </summary>
        /// <param name="asset">Imported model asset to transform.</param>
        /// <param name="maximumEdgeLength">Largest permitted output edge length.</param>
        public static void Apply(ModelAsset asset, double maximumEdgeLength) {
            Apply(asset, maximumEdgeLength, float3.One);
        }

        /// <summary>
        /// Creates an independent raw model copy suitable for per-component load-time geometry preparation.
        /// </summary>
        /// <param name="asset">Shared source model whose streams and metadata are copied.</param>
        /// <returns>A model asset with independently owned geometry and submesh arrays.</returns>
        public static ModelAsset Clone(ModelAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            ModelSubmeshAsset[] submeshes = null;
            if (asset.Submeshes != null) {
                submeshes = new ModelSubmeshAsset[asset.Submeshes.Length];
                for (int submeshIndex = 0; submeshIndex < asset.Submeshes.Length; submeshIndex++) {
                    ModelSubmeshAsset sourceSubmesh = asset.Submeshes[submeshIndex];
                    if (sourceSubmesh == null) {
                        throw new InvalidOperationException($"Model clone source submesh {submeshIndex} is null.");
                    }

                    submeshes[submeshIndex] = new ModelSubmeshAsset {
                        MaterialSlotName = sourceSubmesh.MaterialSlotName,
                        IndexStart = sourceSubmesh.IndexStart,
                        IndexCount = sourceSubmesh.IndexCount
                    };
                }
            }

            return new ModelAsset {
                Id = asset.Id,
                RuntimeAssetId = 0u,
                Positions = CopyPositions(asset.Positions),
                Normals = CopyPositions(asset.Normals),
                TexCoords = CopyTexCoords(asset.TexCoords),
                BoundsMin = asset.BoundsMin,
                BoundsMax = asset.BoundsMax,
                Indices16 = CopyIndices16(asset.Indices16),
                Indices32 = CopyIndices32(asset.Indices32),
                Submeshes = submeshes
            };
        }

        /// <summary>
        /// Copies a three-component vector stream when it exists.
        /// </summary>
        /// <param name="source">Optional source vector stream.</param>
        /// <returns>Independent copied vectors, or null.</returns>
        static float3[] CopyPositions(float3[] source) {
            if (source == null) {
                return null;
            }

            float3[] result = new float3[source.Length];
            for (int index = 0; index < source.Length; index++) {
                result[index] = source[index];
            }

            return result;
        }

        /// <summary>
        /// Copies a two-component vector stream when it exists.
        /// </summary>
        /// <param name="source">Optional source vector stream.</param>
        /// <returns>Independent copied vectors, or null.</returns>
        static float2[] CopyTexCoords(float2[] source) {
            if (source == null) {
                return null;
            }

            float2[] result = new float2[source.Length];
            for (int index = 0; index < source.Length; index++) {
                result[index] = source[index];
            }

            return result;
        }

        /// <summary>
        /// Copies a 16-bit index stream when it exists.
        /// </summary>
        /// <param name="source">Optional source index stream.</param>
        /// <returns>Independent copied indices, or null.</returns>
        static ushort[] CopyIndices16(ushort[] source) {
            if (source == null) {
                return null;
            }

            ushort[] result = new ushort[source.Length];
            for (int index = 0; index < source.Length; index++) {
                result[index] = source[index];
            }

            return result;
        }

        /// <summary>
        /// Copies a 32-bit index stream when it exists.
        /// </summary>
        /// <param name="source">Optional source index stream.</param>
        /// <returns>Independent copied indices, or null.</returns>
        static uint[] CopyIndices32(uint[] source) {
            if (source == null) {
                return null;
            }

            uint[] result = new uint[source.Length];
            for (int index = 0; index < source.Length; index++) {
                result[index] = source[index];
            }

            return result;
        }

        /// <summary>
        /// Copies source positions into a mutable list without unsupported collection-copy constructors.
        /// </summary>
        /// <param name="source">Source position stream.</param>
        /// <returns>Independent mutable position list.</returns>
        static List<float3> CreatePositionList(float3[] source) {
            List<float3> result = new List<float3>(source.Length);
            for (int index = 0; index < source.Length; index++) {
                result.Add(source[index]);
            }

            return result;
        }

        /// <summary>
        /// Copies source texture coordinates into a mutable list without unsupported collection-copy constructors.
        /// </summary>
        /// <param name="source">Source texture-coordinate stream.</param>
        /// <returns>Independent mutable texture-coordinate list.</returns>
        static List<float2> CreateTexCoordList(float2[] source) {
            List<float2> result = new List<float2>(source.Length);
            for (int index = 0; index < source.Length; index++) {
                result.Add(source[index]);
            }

            return result;
        }

        /// <summary>
        /// Bakes one static render scale into model positions and corrects its normals for fixed-function lighting.
        /// </summary>
        /// <param name="asset">Imported model asset whose geometry receives the scale.</param>
        /// <param name="scale">Finite nonzero scale to bake into the asset.</param>
        public static void ApplyBakeScale(ModelAsset asset, float3 scale) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            ValidateMeasurementScale(scale);
            if (asset.Positions == null || asset.Normals == null || asset.Positions.Length != asset.Normals.Length) {
                throw new InvalidOperationException("Model scale baking requires equally sized position and normal streams.");
            }

            for (int vertexIndex = 0; vertexIndex < asset.Positions.Length; vertexIndex++) {
                float3 position = asset.Positions[vertexIndex];
                float3 normal = asset.Normals[vertexIndex];
                asset.Positions[vertexIndex] = new float3(position.X * scale.X, position.Y * scale.Y, position.Z * scale.Z);
                double normalX = normal.X / scale.X;
                double normalY = normal.Y / scale.Y;
                double normalZ = normal.Z / scale.Z;
                double lengthSquared = (normalX * normalX) + (normalY * normalY) + (normalZ * normalZ);
                if (lengthSquared == 0d) {
                    asset.Normals[vertexIndex] = new float3(0f, 0f, 0f);
                    continue;
                }

                double inverseLength = 1d / Math.Sqrt(lengthSquared);
                asset.Normals[vertexIndex] = new float3(
                    (float)(normalX * inverseLength),
                    (float)(normalY * inverseLength),
                    (float)(normalZ * inverseLength));
            }
        }

        /// <summary>
        /// Tessellates an indexed model using a static world scale only for edge-length measurement while preserving local-space output geometry.
        /// </summary>
        /// <param name="asset">Imported model asset to transform.</param>
        /// <param name="maximumEdgeLength">Largest permitted output edge length measured after applying <paramref name="measurementScale"/>.</param>
        /// <param name="measurementScale">Static accumulated world scale used to measure local model edges.</param>
        public static void Apply(ModelAsset asset, double maximumEdgeLength, float3 measurementScale) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            ValidateMaximumEdgeLength(maximumEdgeLength);
            ValidateMeasurementScale(measurementScale);
            ModelAssetIndexData indexData = ModelAssetIndexData.Resolve(asset);
            ValidateSourceAsset(asset, indexData);
            if (indexData.IndexCount == 0) {
                return;
            }

            List<float3> positions = CreatePositionList(asset.Positions);
            List<float3> normals = CreatePositionList(asset.Normals);
            List<float2> texCoords = CreateTexCoordList(asset.TexCoords);
            List<ModelTessellationTriangle> triangles = CreateTriangles(asset, indexData);
            Dictionary<ModelTessellationAttributeEdgeKey, int> midpointIndices = new Dictionary<ModelTessellationAttributeEdgeKey, int>();
            ModelTessellationGeometricEdgeKey oversizedEdge = FindOversizedEdge(triangles, positions, maximumEdgeLength, measurementScale);
            while (oversizedEdge != null) {
                SplitGeometricEdge(triangles, positions, normals, texCoords, midpointIndices, oversizedEdge);
                if (triangles.Count > MaximumTriangleCount) {
                    throw new InvalidOperationException($"Model tessellation exceeded the maximum output triangle count of {MaximumTriangleCount}.");
                }

                oversizedEdge = FindOversizedEdge(triangles, positions, maximumEdgeLength, measurementScale);
            }

            ApplyOutput(asset, positions, normals, texCoords, triangles);
        }

        /// <summary>
        /// Validates the requested tessellation threshold.
        /// </summary>
        /// <param name="maximumEdgeLength">Largest permitted output edge length.</param>
        static void ValidateMaximumEdgeLength(double maximumEdgeLength) {
            if (double.IsNaN(maximumEdgeLength) || double.IsInfinity(maximumEdgeLength) || maximumEdgeLength <= 0d) {
                throw new InvalidOperationException("Model tessellation maximum edge length must be finite and greater than zero.");
            }
        }

        /// <summary>
        /// Validates the static world scale used exclusively for local-edge length measurement.
        /// </summary>
        /// <param name="measurementScale">Static accumulated world scale used to measure local model edges.</param>
        static void ValidateMeasurementScale(float3 measurementScale) {
            if (!float.IsFinite(measurementScale.X) || measurementScale.X == 0f) {
                throw new ArgumentException("Tessellation measurement scale X must be finite and non-zero.", nameof(measurementScale));
            } else if (!float.IsFinite(measurementScale.Y) || measurementScale.Y == 0f) {
                throw new ArgumentException("Tessellation measurement scale Y must be finite and non-zero.", nameof(measurementScale));
            } else if (!float.IsFinite(measurementScale.Z) || measurementScale.Z == 0f) {
                throw new ArgumentException("Tessellation measurement scale Z must be finite and non-zero.", nameof(measurementScale));
            }
        }

        /// <summary>
        /// Validates source vertex streams, index data, and authored submesh ranges before any asset mutation occurs.
        /// </summary>
        /// <param name="asset">Model asset to validate.</param>
        /// <param name="indexData">Resolved active index buffer.</param>
        static void ValidateSourceAsset(ModelAsset asset, ModelAssetIndexData indexData) {
            if (asset.Positions == null || asset.Normals == null || asset.TexCoords == null) {
                throw new InvalidOperationException("Model tessellation requires position, normal, and texture-coordinate streams.");
            } else if (asset.Positions.Length != asset.Normals.Length || asset.Positions.Length != asset.TexCoords.Length) {
                throw new InvalidOperationException("Model tessellation requires equally sized position, normal, and texture-coordinate streams.");
            } else if (indexData.IndexCount % 3 != 0) {
                throw new InvalidOperationException("Model tessellation requires an index count divisible by three.");
            }

            for (int vertexIndex = 0; vertexIndex < asset.Positions.Length; vertexIndex++) {
                ValidateFiniteVector(asset.Positions[vertexIndex], "position", vertexIndex);
                ValidateFiniteVector(asset.Normals[vertexIndex], "normal", vertexIndex);
                ValidateFiniteVector(asset.TexCoords[vertexIndex], "texture coordinate", vertexIndex);
            }

            for (int index = 0; index < indexData.IndexCount; index++) {
                uint vertexIndex = GetIndex(indexData, index);
                if (vertexIndex >= asset.Positions.Length) {
                    throw new InvalidOperationException($"Model tessellation index {index} references unavailable vertex {vertexIndex}.");
                }
            }

            ValidateSubmeshes(asset.Submeshes, indexData.IndexCount);
        }

        /// <summary>
        /// Validates one three-component input vector contains finite values.
        /// </summary>
        /// <param name="value">Vector to validate.</param>
        /// <param name="streamName">Name of the source stream.</param>
        /// <param name="vertexIndex">Vertex index owning the vector.</param>
        static void ValidateFiniteVector(float3 value, string streamName, int vertexIndex) {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z)) {
                throw new InvalidOperationException($"Model tessellation {streamName} at vertex {vertexIndex} must be finite.");
            }
        }

        /// <summary>
        /// Validates one two-component input vector contains finite values.
        /// </summary>
        /// <param name="value">Vector to validate.</param>
        /// <param name="streamName">Name of the source stream.</param>
        /// <param name="vertexIndex">Vertex index owning the vector.</param>
        static void ValidateFiniteVector(float2 value, string streamName, int vertexIndex) {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y)) {
                throw new InvalidOperationException($"Model tessellation {streamName} at vertex {vertexIndex} must be finite.");
            }
        }

        /// <summary>
        /// Validates authored submesh ranges are complete, non-overlapping triangle ranges.
        /// </summary>
        /// <param name="submeshes">Authored submesh metadata.</param>
        /// <param name="indexCount">Active source index count.</param>
        static void ValidateSubmeshes(ModelSubmeshAsset[] submeshes, int indexCount) {
            if (submeshes == null || submeshes.Length == 0) {
                return;
            }

            bool[] coveredIndices = new bool[indexCount];
            for (int submeshIndex = 0; submeshIndex < submeshes.Length; submeshIndex++) {
                ModelSubmeshAsset submesh = submeshes[submeshIndex];
                if (submesh == null) {
                    throw new InvalidOperationException($"Model tessellation submesh {submeshIndex} is null.");
                } else if (submesh.IndexStart < 0 || submesh.IndexCount < 0 || submesh.IndexStart % 3 != 0 || submesh.IndexCount % 3 != 0 || submesh.IndexStart > indexCount - submesh.IndexCount) {
                    throw new InvalidOperationException($"Model tessellation submesh {submeshIndex} has an invalid triangle index range.");
                }

                for (int index = submesh.IndexStart; index < submesh.IndexStart + submesh.IndexCount; index++) {
                    if (coveredIndices[index]) {
                        throw new InvalidOperationException($"Model tessellation submesh {submeshIndex} overlaps another submesh range.");
                    }

                    coveredIndices[index] = true;
                }
            }

            for (int index = 0; index < coveredIndices.Length; index++) {
                if (!coveredIndices[index]) {
                    throw new InvalidOperationException("Model tessellation submesh ranges must cover every active index.");
                }
            }
        }

        /// <summary>
        /// Converts the source index buffer into mutable triangles while retaining each triangle's submesh identity.
        /// </summary>
        /// <param name="asset">Source model asset.</param>
        /// <param name="indexData">Resolved source indices.</param>
        /// <returns>Mutable triangles in source order.</returns>
        static List<ModelTessellationTriangle> CreateTriangles(ModelAsset asset, ModelAssetIndexData indexData) {
            List<ModelTessellationTriangle> triangles = new List<ModelTessellationTriangle>(indexData.IndexCount / 3);
            for (int index = 0; index < indexData.IndexCount; index += 3) {
                triangles.Add(new ModelTessellationTriangle(
                    checked((int)GetIndex(indexData, index)),
                    checked((int)GetIndex(indexData, index + 1)),
                    checked((int)GetIndex(indexData, index + 2)),
                    GetSubmeshIndex(asset.Submeshes, index)));
            }

            return triangles;
        }

        /// <summary>
        /// Resolves the submesh that contains one source triangle index offset.
        /// </summary>
        /// <param name="submeshes">Authored submesh metadata.</param>
        /// <param name="indexStart">Triangle start offset in the index buffer.</param>
        /// <returns>Submesh index, or negative one without submesh metadata.</returns>
        static int GetSubmeshIndex(ModelSubmeshAsset[] submeshes, int indexStart) {
            if (submeshes == null || submeshes.Length == 0) {
                return -1;
            }

            for (int submeshIndex = 0; submeshIndex < submeshes.Length; submeshIndex++) {
                ModelSubmeshAsset submesh = submeshes[submeshIndex];
                if (indexStart >= submesh.IndexStart && indexStart < submesh.IndexStart + submesh.IndexCount) {
                    return submeshIndex;
                }
            }

            throw new InvalidOperationException($"Model tessellation could not resolve a submesh for index {indexStart}.");
        }

        /// <summary>
        /// Finds one geometric edge that remains longer than the requested maximum.
        /// </summary>
        /// <param name="triangles">Current mutable triangles.</param>
        /// <param name="positions">Current mutable vertex positions.</param>
        /// <param name="maximumEdgeLength">Largest permitted output edge length.</param>
        /// <param name="measurementScale">Static scale used only to measure output edge lengths.</param>
        /// <returns>One oversized geometric edge, or null when every edge fits.</returns>
        static ModelTessellationGeometricEdgeKey FindOversizedEdge(List<ModelTessellationTriangle> triangles, List<float3> positions, double maximumEdgeLength, float3 measurementScale) {
            double maximumEdgeLengthSquared = maximumEdgeLength * maximumEdgeLength;
            double longestOversizedEdgeLengthSquared = maximumEdgeLengthSquared;
            ModelTessellationGeometricEdgeKey longestOversizedEdge = null;
            for (int triangleIndex = 0; triangleIndex < triangles.Count; triangleIndex++) {
                ModelTessellationTriangle triangle = triangles[triangleIndex];
                double firstEdgeLengthSquared = GetDistanceSquared(positions[triangle.FirstIndex], positions[triangle.SecondIndex], measurementScale);
                if (firstEdgeLengthSquared > longestOversizedEdgeLengthSquared) {
                    longestOversizedEdgeLengthSquared = firstEdgeLengthSquared;
                    longestOversizedEdge = new ModelTessellationGeometricEdgeKey(positions[triangle.FirstIndex], positions[triangle.SecondIndex]);
                }

                double secondEdgeLengthSquared = GetDistanceSquared(positions[triangle.SecondIndex], positions[triangle.ThirdIndex], measurementScale);
                if (secondEdgeLengthSquared > longestOversizedEdgeLengthSquared) {
                    longestOversizedEdgeLengthSquared = secondEdgeLengthSquared;
                    longestOversizedEdge = new ModelTessellationGeometricEdgeKey(positions[triangle.SecondIndex], positions[triangle.ThirdIndex]);
                }

                double thirdEdgeLengthSquared = GetDistanceSquared(positions[triangle.ThirdIndex], positions[triangle.FirstIndex], measurementScale);
                if (thirdEdgeLengthSquared > longestOversizedEdgeLengthSquared) {
                    longestOversizedEdgeLengthSquared = thirdEdgeLengthSquared;
                    longestOversizedEdge = new ModelTessellationGeometricEdgeKey(positions[triangle.ThirdIndex], positions[triangle.FirstIndex]);
                }
            }

            return longestOversizedEdge;
        }

        /// <summary>
        /// Splits every current triangle occurrence of one geometric edge so shared edges remain conforming.
        /// </summary>
        /// <param name="triangles">Current mutable triangles.</param>
        /// <param name="positions">Current mutable positions.</param>
        /// <param name="normals">Current mutable normals.</param>
        /// <param name="texCoords">Current mutable texture coordinates.</param>
        /// <param name="midpointIndices">Cached attribute midpoint vertices.</param>
        /// <param name="geometricEdge">Position-space edge to split.</param>
        static void SplitGeometricEdge(List<ModelTessellationTriangle> triangles, List<float3> positions, List<float3> normals, List<float2> texCoords, Dictionary<ModelTessellationAttributeEdgeKey, int> midpointIndices, ModelTessellationGeometricEdgeKey geometricEdge) {
            int originalTriangleCount = triangles.Count;
            for (int triangleIndex = 0; triangleIndex < originalTriangleCount; triangleIndex++) {
                ModelTessellationTriangle triangle = triangles[triangleIndex];
                if (MatchesEdge(positions, triangle.FirstIndex, triangle.SecondIndex, geometricEdge)) {
                    SplitTriangle(triangles, triangleIndex, 0, positions, normals, texCoords, midpointIndices);
                } else if (MatchesEdge(positions, triangle.SecondIndex, triangle.ThirdIndex, geometricEdge)) {
                    SplitTriangle(triangles, triangleIndex, 1, positions, normals, texCoords, midpointIndices);
                } else if (MatchesEdge(positions, triangle.ThirdIndex, triangle.FirstIndex, geometricEdge)) {
                    SplitTriangle(triangles, triangleIndex, 2, positions, normals, texCoords, midpointIndices);
                }
            }
        }

        /// <summary>
        /// Tests whether one raw vertex edge represents the requested position-space edge.
        /// </summary>
        /// <param name="positions">Current mutable positions.</param>
        /// <param name="firstIndex">First raw vertex index.</param>
        /// <param name="secondIndex">Second raw vertex index.</param>
        /// <param name="geometricEdge">Position-space edge to compare.</param>
        /// <returns>True when the positions identify the supplied edge.</returns>
        static bool MatchesEdge(List<float3> positions, int firstIndex, int secondIndex, ModelTessellationGeometricEdgeKey geometricEdge) {
            return new ModelTessellationGeometricEdgeKey(positions[firstIndex], positions[secondIndex]).Equals(geometricEdge);
        }

        /// <summary>
        /// Replaces one triangle with two winding-preserving children split along one cyclic edge.
        /// </summary>
        /// <param name="triangles">Current mutable triangles.</param>
        /// <param name="triangleIndex">Index of the parent triangle.</param>
        /// <param name="edgeIndex">Cyclic edge index: first-second, second-third, or third-first.</param>
        /// <param name="positions">Current mutable positions.</param>
        /// <param name="normals">Current mutable normals.</param>
        /// <param name="texCoords">Current mutable texture coordinates.</param>
        /// <param name="midpointIndices">Cached attribute midpoint vertices.</param>
        static void SplitTriangle(List<ModelTessellationTriangle> triangles, int triangleIndex, int edgeIndex, List<float3> positions, List<float3> normals, List<float2> texCoords, Dictionary<ModelTessellationAttributeEdgeKey, int> midpointIndices) {
            ModelTessellationTriangle parent = triangles[triangleIndex];
            int firstIndex;
            int secondIndex;
            int oppositeIndex;
            if (edgeIndex == 0) {
                firstIndex = parent.FirstIndex;
                secondIndex = parent.SecondIndex;
                oppositeIndex = parent.ThirdIndex;
            } else if (edgeIndex == 1) {
                firstIndex = parent.SecondIndex;
                secondIndex = parent.ThirdIndex;
                oppositeIndex = parent.FirstIndex;
            } else {
                firstIndex = parent.ThirdIndex;
                secondIndex = parent.FirstIndex;
                oppositeIndex = parent.SecondIndex;
            }

            int midpointIndex = GetOrCreateMidpointIndex(firstIndex, secondIndex, positions, normals, texCoords, midpointIndices);
            triangles[triangleIndex] = new ModelTessellationTriangle(firstIndex, midpointIndex, oppositeIndex, parent.SubmeshIndex);
            triangles.Add(new ModelTessellationTriangle(midpointIndex, secondIndex, oppositeIndex, parent.SubmeshIndex));
        }

        /// <summary>
        /// Gets or creates the attribute-aware midpoint vertex for one source edge.
        /// </summary>
        /// <param name="firstIndex">First raw vertex index.</param>
        /// <param name="secondIndex">Second raw vertex index.</param>
        /// <param name="positions">Current mutable positions.</param>
        /// <param name="normals">Current mutable normals.</param>
        /// <param name="texCoords">Current mutable texture coordinates.</param>
        /// <param name="midpointIndices">Cached attribute midpoint vertices.</param>
        /// <returns>Raw vertex index for the edge midpoint.</returns>
        static int GetOrCreateMidpointIndex(int firstIndex, int secondIndex, List<float3> positions, List<float3> normals, List<float2> texCoords, Dictionary<ModelTessellationAttributeEdgeKey, int> midpointIndices) {
            ModelTessellationAttributeEdgeKey edgeKey = new ModelTessellationAttributeEdgeKey(firstIndex, secondIndex);
            if (midpointIndices.TryGetValue(edgeKey, out int midpointIndex)) {
                return midpointIndex;
            }

            midpointIndex = positions.Count;
            positions.Add(InterpolatePosition(positions[firstIndex], positions[secondIndex]));
            normals.Add(InterpolateNormal(normals[firstIndex], normals[secondIndex]));
            texCoords.Add(InterpolateTexCoord(texCoords[firstIndex], texCoords[secondIndex]));
            midpointIndices.Add(edgeKey, midpointIndex);
            return midpointIndex;
        }

        /// <summary>
        /// Interpolates two positions at the exact edge midpoint using double arithmetic.
        /// </summary>
        /// <param name="first">First endpoint.</param>
        /// <param name="second">Second endpoint.</param>
        /// <returns>Midpoint position.</returns>
        static float3 InterpolatePosition(float3 first, float3 second) {
            return new float3(
                (float)(((double)first.X + second.X) * 0.5d),
                (float)(((double)first.Y + second.Y) * 0.5d),
                (float)(((double)first.Z + second.Z) * 0.5d));
        }

        /// <summary>
        /// Interpolates and normalizes two normals at the edge midpoint.
        /// </summary>
        /// <param name="first">First endpoint normal.</param>
        /// <param name="second">Second endpoint normal.</param>
        /// <returns>Normalized midpoint normal, or zero when both inputs cancel.</returns>
        static float3 InterpolateNormal(float3 first, float3 second) {
            double x = ((double)first.X + second.X) * 0.5d;
            double y = ((double)first.Y + second.Y) * 0.5d;
            double z = ((double)first.Z + second.Z) * 0.5d;
            double lengthSquared = (x * x) + (y * y) + (z * z);
            if (lengthSquared == 0d) {
                return new float3(0f, 0f, 0f);
            }

            double inverseLength = 1d / Math.Sqrt(lengthSquared);
            return new float3((float)(x * inverseLength), (float)(y * inverseLength), (float)(z * inverseLength));
        }

        /// <summary>
        /// Interpolates two texture coordinates at the edge midpoint using double arithmetic.
        /// </summary>
        /// <param name="first">First endpoint coordinate.</param>
        /// <param name="second">Second endpoint coordinate.</param>
        /// <returns>Midpoint texture coordinate.</returns>
        static float2 InterpolateTexCoord(float2 first, float2 second) {
            return new float2(
                (float)(((double)first.X + second.X) * 0.5d),
                (float)(((double)first.Y + second.Y) * 0.5d));
        }

        /// <summary>
        /// Computes one squared position-space distance using double arithmetic.
        /// </summary>
        /// <param name="first">First endpoint.</param>
        /// <param name="second">Second endpoint.</param>
        /// <param name="measurementScale">Static scale used only to measure the edge length.</param>
        /// <returns>Squared Euclidean distance.</returns>
        static double GetDistanceSquared(float3 first, float3 second, float3 measurementScale) {
            double deltaX = ((double)second.X - first.X) * measurementScale.X;
            double deltaY = ((double)second.Y - first.Y) * measurementScale.Y;
            double deltaZ = ((double)second.Z - first.Z) * measurementScale.Z;
            return (deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ);
        }

        /// <summary>
        /// Writes completed temporary geometry into the asset only after tessellation succeeds.
        /// </summary>
        /// <param name="asset">Destination model asset.</param>
        /// <param name="positions">Completed output positions.</param>
        /// <param name="normals">Completed output normals.</param>
        /// <param name="texCoords">Completed output texture coordinates.</param>
        /// <param name="triangles">Completed output triangles.</param>
        static void ApplyOutput(ModelAsset asset, List<float3> positions, List<float3> normals, List<float2> texCoords, List<ModelTessellationTriangle> triangles) {
            List<uint> outputIndices = new List<uint>(triangles.Count * 3);
            ModelSubmeshAsset[] outputSubmeshes = CreateOutputSubmeshes(asset.Submeshes, triangles, outputIndices);
            if (outputSubmeshes == null) {
                for (int triangleIndex = 0; triangleIndex < triangles.Count; triangleIndex++) {
                    AddTriangleIndices(outputIndices, triangles[triangleIndex]);
                }
            }

            asset.Positions = positions.ToArray();
            asset.Normals = normals.ToArray();
            asset.TexCoords = texCoords.ToArray();
            asset.Submeshes = outputSubmeshes;
            if (positions.Count <= ushort.MaxValue) {
                ushort[] indices16 = new ushort[outputIndices.Count];
                for (int index = 0; index < outputIndices.Count; index++) {
                    indices16[index] = (ushort)outputIndices[index];
                }

                asset.Indices16 = indices16;
                asset.Indices32 = null;
            } else {
                asset.Indices16 = null;
                asset.Indices32 = outputIndices.ToArray();
            }
        }

        /// <summary>
        /// Rebuilds submesh index ranges and appends their triangles in authored submesh order.
        /// </summary>
        /// <param name="sourceSubmeshes">Source submesh metadata.</param>
        /// <param name="triangles">Completed output triangles.</param>
        /// <param name="outputIndices">Destination index buffer.</param>
        /// <returns>Rebuilt submeshes, or null when the source had no submesh metadata.</returns>
        static ModelSubmeshAsset[] CreateOutputSubmeshes(ModelSubmeshAsset[] sourceSubmeshes, List<ModelTessellationTriangle> triangles, List<uint> outputIndices) {
            if (sourceSubmeshes == null || sourceSubmeshes.Length == 0) {
                return null;
            }

            ModelSubmeshAsset[] outputSubmeshes = new ModelSubmeshAsset[sourceSubmeshes.Length];
            for (int submeshIndex = 0; submeshIndex < sourceSubmeshes.Length; submeshIndex++) {
                ModelSubmeshAsset sourceSubmesh = sourceSubmeshes[submeshIndex];
                int indexStart = outputIndices.Count;
                for (int triangleIndex = 0; triangleIndex < triangles.Count; triangleIndex++) {
                    ModelTessellationTriangle triangle = triangles[triangleIndex];
                    if (triangle.SubmeshIndex == submeshIndex) {
                        AddTriangleIndices(outputIndices, triangle);
                    }
                }

                outputSubmeshes[submeshIndex] = new ModelSubmeshAsset {
                    MaterialSlotName = sourceSubmesh.MaterialSlotName,
                    IndexStart = indexStart,
                    IndexCount = outputIndices.Count - indexStart
                };
            }

            return outputSubmeshes;
        }

        /// <summary>
        /// Appends one triangle's winding-preserving indices to an output buffer.
        /// </summary>
        /// <param name="outputIndices">Destination index buffer.</param>
        /// <param name="triangle">Triangle to append.</param>
        static void AddTriangleIndices(List<uint> outputIndices, ModelTessellationTriangle triangle) {
            outputIndices.Add((uint)triangle.FirstIndex);
            outputIndices.Add((uint)triangle.SecondIndex);
            outputIndices.Add((uint)triangle.ThirdIndex);
        }

        /// <summary>
        /// Resolves one active source index as an unsigned integer.
        /// </summary>
        /// <param name="indexData">Resolved active index buffer.</param>
        /// <param name="index">Offset to read.</param>
        /// <returns>Resolved source vertex index.</returns>
        static uint GetIndex(ModelAssetIndexData indexData, int index) {
            return indexData.Uses32BitIndices ? indexData.Indices32[index] : indexData.Indices16[index];
        }
    }
}
