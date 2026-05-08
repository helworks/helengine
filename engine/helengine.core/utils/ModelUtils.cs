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

        /// <summary>
        /// Generates a UV sphere mesh centered at the given position with the specified per-axis scale.
        /// </summary>
        /// <param name="position">Sphere center position.</param>
        /// <param name="scale">Per-axis scale applied to the unit sphere.</param>
        /// <returns>Generated model asset.</returns>
        public static ModelAsset GenerateSphereMesh(float3 position, float3 scale) {
            const int LatitudeSegments = 12;
            const int LongitudeSegments = 24;

            ModelAsset modelData = new ModelAsset();
            modelData.Id = new Guid().ToString();

            List<float3> positions = new List<float3>((LatitudeSegments + 1) * (LongitudeSegments + 1));
            List<float3> normals = new List<float3>((LatitudeSegments + 1) * (LongitudeSegments + 1));
            List<float2> texCoords = new List<float2>((LatitudeSegments + 1) * (LongitudeSegments + 1));
            List<ushort> indices = new List<ushort>(LatitudeSegments * LongitudeSegments * 6);

            for (int latitudeIndex = 0; latitudeIndex <= LatitudeSegments; latitudeIndex++) {
                double latitudeProgress = (double)latitudeIndex / LatitudeSegments;
                double theta = latitudeProgress * Math.PI;
                double sinTheta = Math.Sin(theta);
                double cosTheta = Math.Cos(theta);

                for (int longitudeIndex = 0; longitudeIndex <= LongitudeSegments; longitudeIndex++) {
                    double longitudeProgress = (double)longitudeIndex / LongitudeSegments;
                    double phi = longitudeProgress * Math.PI * 2d;
                    double sinPhi = Math.Sin(phi);
                    double cosPhi = Math.Cos(phi);

                    float3 normal = new float3(
                        (float)(sinTheta * cosPhi),
                        (float)cosTheta,
                        (float)(sinTheta * sinPhi));
                    normals.Add(normal);
                    positions.Add(new float3(
                        position.X + (normal.X * scale.X),
                        position.Y + (normal.Y * scale.Y),
                        position.Z + (normal.Z * scale.Z)));
                    texCoords.Add(new float2((float)longitudeProgress, 1f - (float)latitudeProgress));
                }
            }

            int vertexStride = LongitudeSegments + 1;
            for (int latitudeIndex = 0; latitudeIndex < LatitudeSegments; latitudeIndex++) {
                for (int longitudeIndex = 0; longitudeIndex < LongitudeSegments; longitudeIndex++) {
                    int topLeft = (latitudeIndex * vertexStride) + longitudeIndex;
                    int bottomLeft = topLeft + vertexStride;
                    int topRight = topLeft + 1;
                    int bottomRight = bottomLeft + 1;

                    indices.Add((ushort)topLeft);
                    indices.Add((ushort)bottomLeft);
                    indices.Add((ushort)topRight);
                    indices.Add((ushort)topRight);
                    indices.Add((ushort)bottomLeft);
                    indices.Add((ushort)bottomRight);
                }
            }

            modelData.Positions = positions.ToArray();
            modelData.Normals = normals.ToArray();
            modelData.TexCoords = texCoords.ToArray();
            modelData.Indices16 = indices.ToArray();
            return modelData;
        }
    }
}
