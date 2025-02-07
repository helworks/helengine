using helengine.editor;

namespace helengine {
    public class ModelUtils {
        public static EditorModelData GenerateCubeMesh(float3 position, float3 scale) {
            EditorModelData modelData = new EditorModelData();
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
                4, 5, 6, 6, 7, 4,
                8, 9, 10, 10, 11, 8,
                12, 13, 14, 14, 15, 12,
                16, 17, 18, 18, 19, 16,
                20, 21, 22, 22, 23, 20
            ];

            for (int i = 0; i < 24; i++) {
                positions[i] = positions[i] * scale + position;
            }

            modelData.Positions = positions;
            modelData.TexCoords = texCoords;
            modelData.Indices16 = indices;

            return modelData;
        }
    }
}
