namespace helengine.editor.tests.managers.project {
    /// <summary>Supplies a real dense indexed surface for render/CPU shared-reference cooking tests.</summary>
    public sealed class GpuReductionGridImporter : IModelImporter {
        /// <summary>Creates independent importer geometry for each actual model import request.</summary>
        public ImportedModelAssetSet ImportModel(Stream stream) {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return new ImportedModelAssetSet(CreateGrid(), Array.Empty<ImportedModelMaterialAsset>());
        }
        /// <summary>Creates a planar UV chart with 441 vertices and 2400 triangle indices.</summary>
        public static ModelAsset CreateGrid() {
            const int segments = 20;
            float3[] positions = new float3[(segments + 1) * (segments + 1)], normals = new float3[positions.Length];
            float2[] uv = new float2[positions.Length];
            List<ushort> indices = new List<ushort>();
            for (int y = 0; y <= segments; y++) {
                for (int x = 0; x <= segments; x++) {
                    int vertex = y * (segments + 1) + x;
                    positions[vertex] = new float3((float)x / segments, (float)y / segments, 0f);
                    normals[vertex] = new float3(0f, 0f, 1f);
                    uv[vertex] = new float2((float)x / segments, (float)y / segments);
                    if (x == segments || y == segments) continue;
                    ushort a = (ushort)vertex, b = (ushort)(vertex + 1), c = (ushort)(vertex + segments + 1), d = (ushort)(c + 1);
                    indices.AddRange(new[] { a, b, d, a, d, c });
                }
            }
            return new ModelAsset { Positions = positions, Normals = normals, TexCoords = uv, Indices16 = indices.ToArray(), Submeshes = new[] { new ModelSubmeshAsset { MaterialSlotName = "Surface", IndexStart = 0, IndexCount = indices.Count } } };
        }
    }
}
