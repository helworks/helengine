using helengine.editor;

namespace helengine.editor.tests {
    public sealed class GeneratedAssetWriteServiceTests {
        [Fact]
        public void WriteAsset_WhenModelHasNoAuthoringIdentity_WritesCurrentNativePayloadWithEmbeddedIdentity() {
            string projectRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-asset-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(projectRootPath, "assets"));

            try {
                ModelAsset model = new ModelAsset {
                    Id = "Models/TestModel",
                    Positions = Array.Empty<float3>(),
                    Normals = Array.Empty<float3>(),
                    TexCoords = Array.Empty<float2>(),
                    Indices16 = Array.Empty<ushort>(),
                    Indices32 = Array.Empty<uint>(),
                    Submeshes = Array.Empty<ModelSubmeshAsset>()
                };

                new GeneratedAssetWriteService().WriteAsset(projectRootPath, "models/TestModel.hasset", model);

                string fullPath = Path.Combine(projectRootPath, "assets", "models", "TestModel.hasset");
                using FileStream stream = File.OpenRead(fullPath);
                EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
                stream.Position = 0;
                ModelAsset saved = Assert.IsType<ModelAsset>(AssetSerializer.Deserialize(stream));

                Assert.Equal(EditorAssetBinarySerializer.CurrentVersion, header.Version);
                Assert.False(string.IsNullOrWhiteSpace(saved.AuthoringAssetId));
                Assert.Empty(saved.FormerAuthoringAssetIds);
            } finally {
                if (Directory.Exists(projectRootPath)) {
                    Directory.Delete(projectRootPath, true);
                }
            }
        }
    }
}
