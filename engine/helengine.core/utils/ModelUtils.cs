namespace helengine {
    /// <summary>
    /// Helper methods for generating basic model assets procedurally.
    /// </summary>
    public class ModelUtils {
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
                new float3(-1, -1, -1), new float3(1, -1, -1), new float3(1, 1, -1), new float3(-1, 1, -1),
                // Front face
                new float3(-1, -1, 1), new float3(1, -1, 1), new float3(1, 1, 1), new float3(-1, 1, 1),
                // Right face
                new float3(1, -1, -1), new float3(1, -1, 1), new float3(1, 1, 1), new float3(1, 1, -1),
                // Left face
                new float3(-1, -1, -1), new float3(-1, -1, 1), new float3(-1, 1, 1), new float3(-1, 1, -1),
                // Top face
                new float3(-1, 1, -1), new float3(1, 1, -1), new float3(1, 1, 1), new float3(-1, 1, 1),
                // Bottom face
                new float3(-1, -1, -1), new float3(1, -1, -1), new float3(1, -1, 1), new float3(-1, -1, 1)
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
                0, 1, 2, 2, 3, 0,
                6, 5, 4, 4, 7, 6,
                8, 9, 10, 10, 11, 8,
                14, 13, 12, 12, 15, 14,
                16, 17, 18, 18, 19, 16,
                22, 21, 20, 20, 23, 22
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
        /// Generates a simple quad/plane mesh at the given position and scale.
        /// </summary>
        /// <param name="position">Center position.</param>
        /// <param name="scale">Scale to apply.</param>
        /// <returns>Generated model asset.</returns>
        public static ModelAsset GeneratePlaneMesh(float3 position, float3 scale) {
            ModelAsset modelData = new ModelAsset();
            modelData.Id = new Guid().ToString();

            float3[] positions = [
                // Bottom face
                new float3(-1, -1, -1), new float3(1, -1, -1), new float3(1, -1, 1), new float3(-1, -1, 1)
            ];

            float3[] normals = [
                // Bottom face (-Y)
                new float3(0, -1, 0), new float3(0, -1, 0), new float3(0, -1, 0), new float3(0, -1, 0)
            ];

            float2[] texCoords = [
                new float2(0, 0), new float2(1, 0), new float2(1, 1), new float2(0, 1)
            ];

            ushort[] indices = [
                0, 1, 2, 2, 3, 0,
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
