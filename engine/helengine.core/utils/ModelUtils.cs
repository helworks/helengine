namespace helengine {
    /// <summary>
    /// Helper methods for generating basic model assets procedurally.
    /// </summary>
    public class ModelUtils {
        /// <summary>
        /// Default longitudinal segment count used by the generated sphere primitive.
        /// </summary>
        const int SphereLongitudeSegmentCount = 24;

        /// <summary>
        /// Default latitudinal segment count used by the generated sphere primitive.
        /// </summary>
        const int SphereLatitudeSegmentCount = 12;

        /// <summary>
        /// Generates a cube mesh centered at the given position with the specified scale.
        /// </summary>
        /// <param name="position">Cube center position.</param>
        /// <param name="scale">Cube scale.</param>
        /// <returns>Generated model asset.</returns>
        public static ModelAsset GenerateCubeMesh(float3 position, float3 scale) {
            ModelAsset modelData = new ModelAsset();
            modelData.Id = new Guid().ToString();

            float3[] positions = [
                // Back face
                new float3(-0.5f, -0.5f, -0.5f), new float3(0.5f, -0.5f, -0.5f), new float3(0.5f, 0.5f, -0.5f), new float3(-0.5f, 0.5f, -0.5f),
                // Front face
                new float3(-0.5f, -0.5f, 0.5f), new float3(0.5f, -0.5f, 0.5f), new float3(0.5f, 0.5f, 0.5f), new float3(-0.5f, 0.5f, 0.5f),
                // Right face
                new float3(0.5f, -0.5f, -0.5f), new float3(0.5f, -0.5f, 0.5f), new float3(0.5f, 0.5f, 0.5f), new float3(0.5f, 0.5f, -0.5f),
                // Left face
                new float3(-0.5f, -0.5f, -0.5f), new float3(-0.5f, -0.5f, 0.5f), new float3(-0.5f, 0.5f, 0.5f), new float3(-0.5f, 0.5f, -0.5f),
                // Top face
                new float3(-0.5f, 0.5f, -0.5f), new float3(0.5f, 0.5f, -0.5f), new float3(0.5f, 0.5f, 0.5f), new float3(-0.5f, 0.5f, 0.5f),
                // Bottom face
                new float3(-0.5f, -0.5f, -0.5f), new float3(0.5f, -0.5f, -0.5f), new float3(0.5f, -0.5f, 0.5f), new float3(-0.5f, -0.5f, 0.5f)
            ];


            float3[] normals = [
                // Back face (-Z)
                new float3(0, 0, -1), new float3(0, 0, -1), new float3(0, 0, -1), new float3(0, 0, -1),
                // Front face (+Z)
                new float3(0, 0, 1), new float3(0, 0, 1), new float3(0, 0, 1), new float3(0, 0, 1),
                // Right face (+X)
                new float3(1, 0, 0), new float3(1, 0, 0), new float3(1, 0, 0), new float3(1, 0, 0),
                // Left face (-X)
                new float3(-1, 0, 0), new float3(-1, 0, 0), new float3(-1, 0, 0), new float3(-1, 0, 0),
                // Top face (+Y)
                new float3(0, 1, 0), new float3(0, 1, 0), new float3(0, 1, 0), new float3(0, 1, 0),
                // Bottom face (-Y)
                new float3(0, -1, 0), new float3(0, -1, 0), new float3(0, -1, 0), new float3(0, -1, 0)
            ];

            float2[] texCoords = [
                new float2(0, 0), new float2(1, 0), new float2(1, 1), new float2(0, 1),
                new float2(0, 0), new float2(1, 0), new float2(1, 1), new float2(0, 1),
                new float2(0, 0), new float2(1, 0), new float2(1, 1), new float2(0, 1),
                new float2(0, 0), new float2(1, 0), new float2(1, 1), new float2(0, 1),
                new float2(0, 0), new float2(1, 0), new float2(1, 1), new float2(0, 1),
                new float2(0, 0), new float2(1, 0), new float2(1, 1), new float2(0, 1)
            ];

            ushort[] indices = [
                0, 2, 1, 2, 0, 3,
                6, 4, 5, 4, 6, 7,
                8, 10, 9, 10, 8, 11,
                14, 12, 13, 12, 14, 15,
                16, 18, 17, 18, 16, 19,
                22, 20, 21, 20, 22, 23
            ];

            for (int i = 0; i < 24; i++) {
                positions[i] = positions[i] * scale + position;
            }

            modelData.Positions = positions;
            modelData.TexCoords = texCoords;
            modelData.Normals = normals;
            modelData.Indices16 = indices;

            return modelData;
        }

        /// <summary>
        /// Generates a UV sphere mesh centered at the given position with the specified scale.
        /// </summary>
        /// <param name="position">Sphere center position.</param>
        /// <param name="scale">Sphere scale.</param>
        /// <returns>Generated model asset.</returns>
        public static ModelAsset GenerateSphereMesh(float3 position, float3 scale) {
            ModelAsset modelData = new ModelAsset();
            modelData.Id = new Guid().ToString();

            int vertexCount = (SphereLatitudeSegmentCount + 1) * (SphereLongitudeSegmentCount + 1);
            int indexCount = SphereLatitudeSegmentCount * SphereLongitudeSegmentCount * 6;
            float3[] positions = new float3[vertexCount];
            float3[] normals = new float3[vertexCount];
            float2[] texCoords = new float2[vertexCount];
            ushort[] indices = new ushort[indexCount];

            int vertexIndex = 0;
            for (int latitudeIndex = 0; latitudeIndex <= SphereLatitudeSegmentCount; latitudeIndex++) {
                double latitudePercent = (double)latitudeIndex / SphereLatitudeSegmentCount;
                double latitudeAngle = latitudePercent * Math.PI;
                float y = (float)Math.Cos(latitudeAngle);
                float horizontalRadius = (float)Math.Sin(latitudeAngle);

                for (int longitudeIndex = 0; longitudeIndex <= SphereLongitudeSegmentCount; longitudeIndex++) {
                    double longitudePercent = (double)longitudeIndex / SphereLongitudeSegmentCount;
                    double longitudeAngle = longitudePercent * Math.PI * 2d;
                    float x = (float)(Math.Cos(longitudeAngle) * horizontalRadius);
                    float z = (float)(Math.Sin(longitudeAngle) * horizontalRadius);
                    float3 normal = new float3(x, y, z);
                    positions[vertexIndex] = normal * 0.5f * scale + position;
                    normals[vertexIndex] = normal;
                    texCoords[vertexIndex] = new float2((float)longitudePercent, 1f - (float)latitudePercent);
                    vertexIndex++;
                }
            }

            int indexWriteOffset = 0;
            int stride = SphereLongitudeSegmentCount + 1;
            for (int latitudeIndex = 0; latitudeIndex < SphereLatitudeSegmentCount; latitudeIndex++) {
                for (int longitudeIndex = 0; longitudeIndex < SphereLongitudeSegmentCount; longitudeIndex++) {
                    int currentVertex = latitudeIndex * stride + longitudeIndex;
                    int nextRowVertex = currentVertex + stride;
                    ushort currentVertexIndex = (ushort)currentVertex;
                    ushort nextRowVertexIndex = (ushort)nextRowVertex;
                    ushort currentVertexNextIndex = (ushort)(currentVertex + 1);
                    ushort nextRowVertexNextIndex = (ushort)(nextRowVertex + 1);

                    indices[indexWriteOffset++] = currentVertexIndex;
                    indices[indexWriteOffset++] = nextRowVertexNextIndex;
                    indices[indexWriteOffset++] = currentVertexNextIndex;
                    indices[indexWriteOffset++] = currentVertexIndex;
                    indices[indexWriteOffset++] = nextRowVertexIndex;
                    indices[indexWriteOffset++] = nextRowVertexNextIndex;
                }
            }

            modelData.Positions = positions;
            modelData.TexCoords = texCoords;
            modelData.Normals = normals;
            modelData.Indices16 = indices;

            return modelData;
        }

        /// <summary>
        /// Generates a simple quad/plane mesh at the given position and scale.
        /// </summary>
        /// <param name="position">Center position.</param>
        /// <param name="scale">Scale to apply.</param>
        /// <returns>Generated model asset.</returns>
        public static ModelAsset GeneratePlaneMesh(float3 position, float3 scale) {
            ModelAsset modelData = new ModelAsset();
            modelData.Id = new Guid().ToString();

            float3[] positions = [
                // Top face aligned to the XZ plane
                new float3(-1, 0, -1), new float3(1, 0, -1), new float3(1, 0, 1), new float3(-1, 0, 1)
            ];

            float3[] normals = [
                // Top face (+Y)
                new float3(0, 1, 0), new float3(0, 1, 0), new float3(0, 1, 0), new float3(0, 1, 0)
            ];

            float2[] texCoords = [
                new float2(0, 0), new float2(1, 0), new float2(1, 1), new float2(0, 1)
            ];

            ushort[] indices = [
                0, 2, 1, 2, 0, 3,
            ];

            for (int i = 0; i < positions.Length; i++) {
                positions[i] = positions[i] * scale + position;
            }

            modelData.Positions = positions;
            modelData.TexCoords = texCoords;
            modelData.Normals = normals;
            modelData.Indices16 = indices;

            return modelData;
        }
    }
}
