using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies topology-preserving tessellation of imported model geometry.
    /// </summary>
    public sealed class ModelTessellationProcessorTests {
        /// <summary>
        /// Ensures one oversized triangle is subdivided until each generated edge fits the configured maximum.
        /// </summary>
        [Fact]
        public void Apply_WhenTriangleContainsOversizedEdges_SubdividesAndInterpolatesVertexAttributes() {
            ModelAsset asset = CreateSingleTriangleAsset();

            ModelTessellationProcessor.Apply(asset, 1.1d);

            Assert.True(GetMaximumEdgeLength(asset) <= 1.1d);
            Assert.Contains(asset.Positions, position => position.X == 1f && position.Y == 1f && position.Z == 0f);
            int midpointIndex = Array.FindIndex(asset.Positions, position => position.X == 1f && position.Y == 1f && position.Z == 0f);
            Assert.Equal(new float2(0.5f, 0.5f), asset.TexCoords[midpointIndex]);
            Assert.Equal(new float3(0f, 0f, 1f), asset.Normals[midpointIndex]);
            Assert.NotNull(asset.Indices16);
            Assert.Null(asset.Indices32);
        }

        /// <summary>
        /// Ensures the model asset processor applies tessellation only when the platform setting enables it.
        /// </summary>
        [Fact]
        public void ModelAssetProcessor_Apply_WhenTessellationIsEnabled_SubdividesBeforeOtherModelProcessing() {
            ModelAsset asset = CreateSingleTriangleAsset();
            ModelAssetProcessorSettings settings = new ModelAssetProcessorSettings {
                Tessellate = true,
                TessellationMaxEdgeLength = 1.1d
            };

            ModelAssetProcessor processor = new ModelAssetProcessor();
            processor.Apply(asset, settings);

            Assert.True(GetMaximumEdgeLength(asset) <= 1.1d);
        }

        /// <summary>
        /// Ensures non-uniform final entity scale controls subdivision decisions while emitted positions remain in model-local space.
        /// </summary>
        [Fact]
        public void Apply_WhenWorldScaleStretchesOneAxis_SubdividesWithoutScalingOutputPositions() {
            ModelAsset unscaledAsset = CreateUnitTriangleAsset();
            ModelAsset scaledAsset = CreateUnitTriangleAsset();

            ModelTessellationProcessor.Apply(unscaledAsset, 1.5d, float3.One);
            ModelTessellationProcessor.Apply(scaledAsset, 1.5d, new float3(4f, 1f, 1f));

            Assert.Equal(3, unscaledAsset.Positions.Length);
            Assert.True(scaledAsset.Positions.Length > 3);
            Assert.All(scaledAsset.Positions, position => Assert.InRange(position.X, 0f, 1f));
            Assert.All(scaledAsset.Positions, position => Assert.InRange(position.Y, 0f, 1f));
        }

        /// <summary>
        /// Ensures static render-scale baking transforms positions and inverse-scale-corrects normals.
        /// </summary>
        [Fact]
        public void ApplyBakeScale_WhenScaleIsNonUniform_TransformsPositionsAndNormals() {
            ModelAsset asset = CreateUnitTriangleAsset();
            asset.Positions[1] = new float3(1f, 2f, 3f);
            asset.Normals[1] = new float3(1f, 1f, 0f);

            ModelTessellationProcessor.ApplyBakeScale(asset, new float3(2f, 4f, 1f));

            Assert.Equal(new float3(2f, 8f, 3f), asset.Positions[1]);
            Assert.InRange(asset.Normals[1].X, 0.8944f, 0.8945f);
            Assert.InRange(asset.Normals[1].Y, 0.4472f, 0.4473f);
            Assert.Equal(0f, asset.Normals[1].Z);
        }

        /// <summary>
        /// Ensures load-time preparation can mutate a private model copy without changing the shared source geometry.
        /// </summary>
        [Fact]
        public void Clone_WhenSourceModelIsMutatedAfterward_RemainsIndependent() {
            ModelAsset source = CreateUnitTriangleAsset();

            ModelAsset clone = ModelTessellationProcessor.Clone(source);
            clone.Positions[1] = new float3(9f, 0f, 0f);

            Assert.Equal(new float3(1f, 0f, 0f), source.Positions[1]);
            Assert.Equal(new float3(9f, 0f, 0f), clone.Positions[1]);
        }

        /// <summary>
        /// Creates one large indexed triangle with complete vertex attributes.
        /// </summary>
        /// <returns>Representative model asset for tessellation tests.</returns>
        static ModelAsset CreateSingleTriangleAsset() {
            return new ModelAsset {
                Positions = new[] {
                    new float3(0f, 0f, 0f),
                    new float3(2f, 0f, 0f),
                    new float3(0f, 2f, 0f)
                },
                Normals = new[] {
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f)
                },
                TexCoords = new[] {
                    new float2(0f, 0f),
                    new float2(1f, 0f),
                    new float2(0f, 1f)
                },
                Indices16 = new ushort[] { 0, 1, 2 },
                Submeshes = new[] {
                    new ModelSubmeshAsset {
                        MaterialSlotName = "default",
                        IndexStart = 0,
                        IndexCount = 3
                    }
                }
            };
        }

        /// <summary>
        /// Creates one unit right triangle whose local edges fit the scale-aware test threshold.
        /// </summary>
        /// <returns>Indexed model asset with complete vertex attributes.</returns>
        static ModelAsset CreateUnitTriangleAsset() {
            return new ModelAsset {
                Positions = new[] {
                    new float3(0f, 0f, 0f),
                    new float3(1f, 0f, 0f),
                    new float3(0f, 1f, 0f)
                },
                Normals = new[] {
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f)
                },
                TexCoords = new[] {
                    new float2(0f, 0f),
                    new float2(1f, 0f),
                    new float2(0f, 1f)
                },
                Indices16 = new ushort[] { 0, 1, 2 },
                Submeshes = new[] {
                    new ModelSubmeshAsset {
                        MaterialSlotName = "default",
                        IndexStart = 0,
                        IndexCount = 3
                    }
                }
            };
        }

        /// <summary>
        /// Calculates the longest edge in the resolved indexed model geometry.
        /// </summary>
        /// <param name="asset">Tessellated asset to inspect.</param>
        /// <returns>Largest triangle edge length.</returns>
        static double GetMaximumEdgeLength(ModelAsset asset) {
            ModelAssetIndexData indexData = ModelAssetIndexData.Resolve(asset);
            double maximumEdgeLength = 0d;
            for (int index = 0; index < indexData.IndexCount; index += 3) {
                uint firstIndex = GetIndex(indexData, index);
                uint secondIndex = GetIndex(indexData, index + 1);
                uint thirdIndex = GetIndex(indexData, index + 2);
                maximumEdgeLength = Math.Max(maximumEdgeLength, GetEdgeLength(asset.Positions[firstIndex], asset.Positions[secondIndex]));
                maximumEdgeLength = Math.Max(maximumEdgeLength, GetEdgeLength(asset.Positions[secondIndex], asset.Positions[thirdIndex]));
                maximumEdgeLength = Math.Max(maximumEdgeLength, GetEdgeLength(asset.Positions[thirdIndex], asset.Positions[firstIndex]));
            }

            return maximumEdgeLength;
        }

        /// <summary>
        /// Resolves one active model index as an unsigned 32-bit value.
        /// </summary>
        /// <param name="indexData">Resolved model index data.</param>
        /// <param name="index">Index-buffer offset to read.</param>
        /// <returns>Resolved vertex index.</returns>
        static uint GetIndex(ModelAssetIndexData indexData, int index) {
            return indexData.Uses32BitIndices ? indexData.Indices32[index] : indexData.Indices16[index];
        }

        /// <summary>
        /// Calculates the Euclidean length between two vertex positions.
        /// </summary>
        /// <param name="first">First endpoint.</param>
        /// <param name="second">Second endpoint.</param>
        /// <returns>Distance between the supplied points.</returns>
        static double GetEdgeLength(float3 first, float3 second) {
            double deltaX = second.X - first.X;
            double deltaY = second.Y - first.Y;
            double deltaZ = second.Z - first.Z;
            return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
        }
    }
}
