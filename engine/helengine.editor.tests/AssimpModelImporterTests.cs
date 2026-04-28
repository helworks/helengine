using helengine.editor.assimp;
using Assimp;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies Assimp-backed model importer conversion into the engine model asset format.
    /// </summary>
    public class AssimpModelImporterTests : IDisposable {
        /// <summary>
        /// Temporary directory used for source model fixtures.
        /// </summary>
        readonly string FixtureRootPath;

        /// <summary>
        /// Initializes a temporary fixture directory for each test.
        /// </summary>
        public AssimpModelImporterTests() {
            FixtureRootPath = Path.Combine(Path.GetTempPath(), "helengine-assimp-import-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(FixtureRootPath);
        }

        /// <summary>
        /// Deletes the temporary fixture directory after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(FixtureRootPath)) {
                Directory.Delete(FixtureRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a simple OBJ triangle is converted into model positions, normals, UVs, and indices.
        /// </summary>
        [Fact]
        public void ImportModel_WhenObjContainsOneTriangle_ReturnsModelAsset() {
            string sourcePath = WriteObjFixture("triangle.obj");
            HelengineAssimpImporter importer = new HelengineAssimpImporter();

            using FileStream stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            ModelAsset asset = importer.ImportModel(stream);

            Assert.NotNull(asset);
            Assert.Equal(3, asset.Positions.Length);
            Assert.Equal(3, asset.Normals.Length);
            Assert.Equal(3, asset.TexCoords.Length);
            Assert.Equal(new ushort[] { 0, 1, 2 }, asset.Indices16);
            Assert.Equal(new float3(0f, 0f, 0f), asset.Positions[0]);
            Assert.Equal(new float3(0f, 0f, 1f), asset.Normals[0]);
            Assert.Equal(new float2(0f, 0f), asset.TexCoords[0]);
        }

        /// <summary>
        /// Ensures large converted scenes switch to 32-bit indices instead of failing at the 16-bit limit.
        /// </summary>
        [Fact]
        public void Convert_WhenSceneExceeds16BitVertexLimit_Uses32BitIndices() {
            Scene scene = CreateLargeTriangleScene(65538);
            AssimpSceneModelAssetConverter converter = new AssimpSceneModelAssetConverter();
            ModelAsset asset = converter.Convert(scene);

            Assert.NotNull(asset);
            Assert.Equal(65538, asset.Positions.Length);
            Assert.Null(asset.Indices16);
            Assert.NotNull(asset.Indices32);
            Assert.Equal(65538, asset.Indices32.Length);
            Assert.Equal(0u, asset.Indices32[0]);
            Assert.Equal(65537u, asset.Indices32[65537]);
        }

        /// <summary>
        /// Writes a minimal OBJ fixture with positions, texture coordinates, normals, and one face.
        /// </summary>
        /// <param name="fileName">Fixture file name.</param>
        /// <returns>Absolute path to the fixture file.</returns>
        string WriteObjFixture(string fileName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("File name must be provided.", nameof(fileName));
            }

            string sourcePath = Path.Combine(FixtureRootPath, fileName);
            File.WriteAllText(
                sourcePath,
                string.Join(
                    Environment.NewLine,
                    "v 0 0 0",
                    "v 1 0 0",
                    "v 0 1 0",
                    "vt 0 0",
                    "vt 1 0",
                    "vt 0 1",
                    "vn 0 0 1",
                    "f 1/1/1 2/2/1 3/3/1"));
            return sourcePath;
        }

        /// <summary>
        /// Creates a large managed Assimp scene whose flattened vertex count exceeds the 16-bit index range.
        /// </summary>
        /// <param name="vertexCount">Vertex count to emit.</param>
        /// <returns>Managed scene containing one large triangle mesh.</returns>
        Scene CreateLargeTriangleScene(int vertexCount) {
            if (vertexCount <= ushort.MaxValue + 1) {
                throw new ArgumentOutOfRangeException(nameof(vertexCount), "Vertex count must exceed the 16-bit index limit.");
            } else if (vertexCount % 3 != 0) {
                throw new ArgumentOutOfRangeException(nameof(vertexCount), "Vertex count must be divisible by three.");
            }

            Mesh mesh = new Mesh("large-mesh", PrimitiveType.Triangle);
            mesh.TextureCoordinateChannels[0] = new List<System.Numerics.Vector3>(vertexCount);
            mesh.UVComponentCount[0] = 2;

            for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++) {
                mesh.Vertices.Add(new System.Numerics.Vector3(vertexIndex, 0f, 0f));
                mesh.Normals.Add(new System.Numerics.Vector3(0f, 0f, 1f));
                mesh.TextureCoordinateChannels[0].Add(new System.Numerics.Vector3(0f, 0f, 0f));
            }

            for (int vertexIndex = 1; vertexIndex <= vertexCount; vertexIndex += 3) {
                mesh.Faces.Add(new Face(new[] { vertexIndex - 1, vertexIndex, vertexIndex + 1 }));
            }

            Scene scene = new Scene();
            scene.Meshes.Add(mesh);
            return scene;
        }
    }
}
