namespace helengine.editor.tests {
    /// <summary>
    /// Verifies UVW map projections rewrite model texture coordinates deterministically.
    /// </summary>
    public sealed class ModelUvwMapProcessorTests {
        /// <summary>
        /// Ensures world mapping multiplies the chosen world axes by the per-component tiling scales and adds the offsets,
        /// without applying the entity scale.
        /// </summary>
        [Fact]
        public void ApplyWorldMap_WithChosenAxes_MultipliesWorldComponentsByScalesAndAddsOffsets() {
            ModelAsset model = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);

            ModelUvwMapProcessor.ApplyWorldMap(model, ModelUvwMapProcessor.AxisX, ModelUvwMapProcessor.AxisZ, 2d, 4d, 0.25d, -1.5d, new float3(10f, 0f, 4f), float4.Identity);

            for (int index = 0; index < model.Positions.Length; index++) {
                float3 world = new float3(10f, 0f, 4f) + model.Positions[index];
                Assert.Equal(world.X * 2f + 0.25f, model.TexCoords[index].X, 3);
                Assert.Equal(world.Z * 4f - 1.5f, model.TexCoords[index].Y, 3);
            }
        }

        /// <summary>
        /// Ensures world mapping can select the same or any world axis for either UV component.
        /// </summary>
        [Fact]
        public void ApplyWorldMap_WithYAxisForBothComponents_MapsWorldYToBoth() {
            ModelAsset model = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);

            ModelUvwMapProcessor.ApplyWorldMap(model, ModelUvwMapProcessor.AxisY, ModelUvwMapProcessor.AxisY, 1d, 2d, 0d, 0d, new float3(0f, 3f, 0f), float4.Identity);

            for (int index = 0; index < model.Positions.Length; index++) {
                float worldY = 3f + model.Positions[index].Y;
                Assert.Equal(worldY, model.TexCoords[index].X, 3);
                Assert.Equal(worldY * 2f, model.TexCoords[index].Y, 3);
            }
        }

        /// <summary>
        /// Ensures world mapping honors the entity orientation when composing world positions.
        /// </summary>
        [Fact]
        public void ApplyWorldMap_WithRotation_UsesRotatedWorldPositions() {
            ModelAsset model = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);
            float3 yAxis = new float3(0f, 1f, 0f);
            float4.CreateFromAxisAngle(ref yAxis, (float)(Math.PI / 2d), out float4 quarterTurn);

            ModelUvwMapProcessor.ApplyWorldMap(model, ModelUvwMapProcessor.AxisX, ModelUvwMapProcessor.AxisZ, 1d, 1d, 0d, 0d, float3.Zero, quarterTurn);

            for (int index = 0; index < model.Positions.Length; index++) {
                float3 world = float4.RotateVector(model.Positions[index], quarterTurn);
                Assert.Equal(world.X, model.TexCoords[index].X, 3);
                Assert.Equal(world.Z, model.TexCoords[index].Y, 3);
            }
        }

        /// <summary>
        /// Ensures box projection assigns per-face texture coordinates and splits shared vertices between differently facing triangles.
        /// </summary>
        [Fact]
        public void ApplyBoxMap_WithCube_SplitsVerticesAndProjectsPerDominantAxis() {
            ModelAsset model = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);
            int originalVertexCount = model.Positions.Length;
            int originalIndexCount = model.Indices16.Length;

            ModelUvwMapProcessor.ApplyBoxMap(model, 1d, 1d, 1d, 1d, 1d, 1d, 0d, 0d);

            Assert.Equal(originalIndexCount, model.Indices16.Length);
            Assert.True(model.Positions.Length >= originalVertexCount);
            Assert.Equal(model.Positions.Length, model.TexCoords.Length);
            Assert.Equal(model.Positions.Length, model.Normals.Length);

            for (int index = 0; index + 2 < model.Indices16.Length; index += 3) {
                float3 a = model.Positions[model.Indices16[index]];
                float3 b = model.Positions[model.Indices16[index + 1]];
                float3 c = model.Positions[model.Indices16[index + 2]];
                float3 normal = float3.Cross(b - a, c - a);
                float2 uvA = model.TexCoords[model.Indices16[index]];
                float2 uvB = model.TexCoords[model.Indices16[index + 1]];
                float2 uvC = model.TexCoords[model.Indices16[index + 2]];
                float2 span = new float2(
                    Math.Max(Math.Max(uvA.X, uvB.X), uvC.X) - Math.Min(Math.Min(uvA.X, uvB.X), uvC.X),
                    Math.Max(Math.Max(uvA.Y, uvB.Y), uvC.Y) - Math.Min(Math.Min(uvA.Y, uvB.Y), uvC.Y));
                Assert.True(normal.LengthSquared() > 0.000001f);
                Assert.True(span.X > 0.0001f || span.Y > 0.0001f, $"Triangle at index {index} projected to a degenerate UV span.");
            }
        }

        /// <summary>
        /// Ensures box mapping spans one repeat per box dimension, multiplied by the tiling and shifted by the UV offset.
        /// </summary>
        [Fact]
        public void ApplyBoxMap_WithBoxDimensionsTilesAndOffsets_MapsRepeatsPerDimension() {
            ModelAsset model = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);

            ModelUvwMapProcessor.ApplyBoxMap(model, 0.5d, 1d, 0.5d, 2d, 1d, 2d, 0.25d, 0d);

            float maxU = float.MinValue;
            float minU = float.MaxValue;
            for (int index = 0; index < model.TexCoords.Length; index++) {
                maxU = Math.Max(maxU, model.TexCoords[index].X);
                minU = Math.Min(minU, model.TexCoords[index].X);
            }

            // A unit cube half-extent of 0.5 over a 0.5-unit box tiled 2x spans +/-2 repeats, shifted by the 0.25 U offset.
            Assert.Equal(2.25f, maxU, 3);
            Assert.Equal(-1.75f, minU, 3);
        }

        /// <summary>
        /// Ensures non-finite values and unknown axes are rejected before any mutation.
        /// </summary>
        [Fact]
        public void Apply_WithInvalidArguments_Throws() {
            ModelAsset model = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);

            Assert.Throws<ArgumentOutOfRangeException>(() => ModelUvwMapProcessor.ApplyBoxMap(model, 0d, 1d, 1d, 1d, 1d, 1d, 0d, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => ModelUvwMapProcessor.ApplyBoxMap(model, 1d, 1d, 1d, double.NaN, 1d, 1d, 0d, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => ModelUvwMapProcessor.ApplyWorldMap(model, ModelUvwMapProcessor.AxisX, ModelUvwMapProcessor.AxisZ, 1d, 1d, double.PositiveInfinity, 0d, float3.Zero, float4.Identity));
            Assert.Throws<ArgumentException>(() => ModelUvwMapProcessor.ApplyWorldMap(model, "W", ModelUvwMapProcessor.AxisZ, 1d, 1d, 0d, 0d, float3.Zero, float4.Identity));
        }
    }
}
