namespace helengine.editor {
    /// <summary>Generates an explicitly selected low-resolution GPU sphere while preserving the Core source primitive.</summary>
    public static class ModelGpuSphereProcessor {
        /// <summary>Builds a separate unit-diameter UV sphere with authored winding and independent seam vertices.</summary>
        public static ModelAsset Create(ModelGpuReductionProfile profile) {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            profile.Validate();
            int rings = profile.SphereRings, sectors = profile.SphereSectors;
            int vertexCount = checked((rings + 1) * (sectors + 1));
            int indexCount = checked((rings - 1) * sectors * 6);
            if (vertexCount > profile.Reduction.MaximumVertices || indexCount > profile.Reduction.MaximumIndices) throw new InvalidOperationException($"GPU sphere profile exceeds its caps: {vertexCount} vertices/{indexCount} indices.");
            float3[] positions = new float3[vertexCount], normals = new float3[vertexCount];
            float2[] uvs = new float2[vertexCount];
            List<ushort> indices = new List<ushort>(indexCount);
            for (int ring = 0; ring <= rings; ring++) {
                double latitude = (double)ring / rings * Math.PI;
                double radius = ring == 0 || ring == rings ? 0d : Math.Sin(latitude);
                for (int sector = 0; sector <= sectors; sector++) {
                    double longitude = sector == sectors ? 0d : (double)sector / sectors * Math.PI * 2d;
                    int vertex = ring * (sectors + 1) + sector;
                    float3 normal = new float3((float)(Math.Cos(longitude) * radius), (float)Math.Cos(latitude), (float)(Math.Sin(longitude) * radius));
                    normals[vertex] = normal;
                    positions[vertex] = normal * 0.5f;
                    uvs[vertex] = new float2((float)sector / sectors, 1f - (float)ring / rings);
                    if (ring == rings || sector == sectors) continue;
                    ushort current = checked((ushort)vertex), next = checked((ushort)(vertex + sectors + 1));
                    if (ring != 0) { indices.Add(current); indices.Add((ushort)(current + 1)); indices.Add((ushort)(next + 1)); }
                    if (ring != rings - 1) { indices.Add(current); indices.Add((ushort)(next + 1)); indices.Add(next); }
                }
            }
            return new ModelAsset { Id = ModelUtils.GeneratedSphereModelId, Positions = positions, Normals = normals, TexCoords = uvs, Indices16 = indices.ToArray(), BoundsMin = new float3(-0.5f), BoundsMax = new float3(0.5f) };
        }
    }
}
