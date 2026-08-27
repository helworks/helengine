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

        /// <summary>
        /// Ensures rewriting an existing native asset with a fresh semantic object reuses its embedded identity and bytes.
        /// </summary>
        [Fact]
        public void WriteAsset_TwiceWithEquivalentFreshModels_PreservesEmbeddedIdentityAndBytes() {
            string projectRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-asset-idempotence-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(projectRootPath, "assets"));
            string fullPath = Path.Combine(projectRootPath, "assets", "models", "TestModel.hasset");

            try {
                GeneratedAssetWriteService writeService = new GeneratedAssetWriteService();
                writeService.WriteAsset(projectRootPath, "models/TestModel.hasset", CreateModel());
                byte[] firstBytes = File.ReadAllBytes(fullPath);
                string firstAssetId = ReadModel(fullPath).AuthoringAssetId;

                writeService.WriteAsset(projectRootPath, "models/TestModel.hasset", CreateModel());
                byte[] secondBytes = File.ReadAllBytes(fullPath);
                ModelAsset secondModel = ReadModel(fullPath);

                Assert.Equal(firstAssetId, secondModel.AuthoringAssetId);
                Assert.Equal(firstBytes, secondBytes);
            } finally {
                if (Directory.Exists(projectRootPath)) {
                    Directory.Delete(projectRootPath, true);
                }
            }
        }

        /// <summary>
        /// Creates one deterministic model semantic payload.
        /// </summary>
        static ModelAsset CreateModel() {
            return new ModelAsset {
                Id = "Models/TestModel",
                Positions = Array.Empty<float3>(),
                Normals = Array.Empty<float3>(),
                TexCoords = Array.Empty<float2>(),
                Indices16 = Array.Empty<ushort>(),
                Indices32 = Array.Empty<uint>(),
                Submeshes = Array.Empty<ModelSubmeshAsset>()
            };
        }

        /// <summary>
        /// Loads one generated model payload from disk.
        /// </summary>
        static ModelAsset ReadModel(string path) {
            using FileStream stream = File.OpenRead(path);
            return Assert.IsType<ModelAsset>(AssetSerializer.Deserialize(stream));
        }
    }
}
