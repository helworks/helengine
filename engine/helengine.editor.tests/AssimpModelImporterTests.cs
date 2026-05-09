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
            ImportedModelAssetSet importedModel = importer.ImportModel(stream);
            ModelAsset asset = importedModel.ModelAsset;

            Assert.NotNull(asset);
            Assert.NotNull(importedModel.GeneratedMaterials);
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
        /// Ensures anonymous materials still produce submesh slot names that line up with generated material names.
        /// </summary>
        [Fact]
        public void Convert_WhenMaterialIndicesDifferFromMeshIndices_UsesMaterialIndicesForFallbackNames() {
            Scene scene = CreateAnonymousMaterialIndexScene();
            AssimpSceneModelAssetConverter converter = new AssimpSceneModelAssetConverter();

            ModelAsset asset = converter.Convert(scene);

            Assert.Equal(2, asset.Submeshes.Length);
            Assert.Equal("Material3", asset.Submeshes[0].MaterialSlotName);
            Assert.Equal("Material5", asset.Submeshes[1].MaterialSlotName);
        }

        /// <summary>
        /// Ensures OBJ sources that switch materials produce one submesh and one generated material asset per material.
        /// </summary>
        [Fact]
        public void ImportModel_WhenObjUsesTwoMaterials_ReturnsTwoSubmeshesAndTwoGeneratedMaterials() {
            string sourcePath = WriteObjFixtureWithMtl("sponza.obj");
            HelengineAssimpImporter importer = new HelengineAssimpImporter();

            using FileStream stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            ImportedModelAssetSet importedModel = importer.ImportModel(stream);

            Assert.Equal(2, importedModel.ModelAsset.Submeshes.Length);
            Assert.Equal("Fabric", importedModel.ModelAsset.Submeshes[0].MaterialSlotName);
            Assert.Equal("Wood", importedModel.ModelAsset.Submeshes[1].MaterialSlotName);
            Assert.Equal(2, importedModel.GeneratedMaterials.Length);
        }

        /// <summary>
        /// Ensures legacy X sources that switch materials produce one submesh and one generated material asset per material.
        /// </summary>
        [Fact]
        public void ImportModel_WhenXUsesTwoMaterials_ReturnsTwoSubmeshesAndTwoGeneratedMaterials() {
            string sourcePath = WriteXFixtureWithMtl("racer.x");
            HelengineAssimpImporter importer = new HelengineAssimpImporter();

            using FileStream stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            ImportedModelAssetSet importedModel = importer.ImportModel(stream);

            Assert.Equal(2, importedModel.ModelAsset.Submeshes.Length);
            Assert.Equal("Body", importedModel.ModelAsset.Submeshes[0].MaterialSlotName);
            Assert.Equal("Trim", importedModel.ModelAsset.Submeshes[1].MaterialSlotName);
            Assert.Equal(2, importedModel.GeneratedMaterials.Length);
        }

        /// <summary>
        /// Ensures duplicate X material names are normalized into unique preview slots instead of overwriting each other.
        /// </summary>
        [Fact]
        public void ImportModel_WhenXUsesDuplicateMaterialNames_ReturnsUniqueSubmeshAndMaterialNames() {
            string sourcePath = WriteXFixtureWithDuplicateMaterialNames("racer-duplicate.x");
            HelengineAssimpImporter importer = new HelengineAssimpImporter();

            using FileStream stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            ImportedModelAssetSet importedModel = importer.ImportModel(stream);

            Assert.Equal(2, importedModel.ModelAsset.Submeshes.Length);
            Assert.Equal("Body", importedModel.ModelAsset.Submeshes[0].MaterialSlotName);
            Assert.Equal("Body_1", importedModel.ModelAsset.Submeshes[1].MaterialSlotName);
            Assert.Equal(2, importedModel.GeneratedMaterials.Length);
            Assert.Equal("Body", importedModel.GeneratedMaterials[0].MaterialName);
            Assert.Equal("Body_1", importedModel.GeneratedMaterials[1].MaterialName);
        }

        /// <summary>
        /// Ensures OBJ material libraries forward `map_Kd` texture references into generated material assets.
        /// </summary>
        [Fact]
        public void ImportModel_WhenMtlDefinesMapKd_SetsGeneratedMaterialDiffuseTextureAssetId() {
            string sourcePath = WriteObjFixtureWithMtl("textured.obj");
            HelengineAssimpImporter importer = new HelengineAssimpImporter();

            using FileStream stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            ImportedModelAssetSet importedModel = importer.ImportModel(stream);
            ImportedModelMaterialAsset generatedMaterial = Assert.Single(
                importedModel.GeneratedMaterials,
                value => string.Equals(value.MaterialName, "Fabric", StringComparison.Ordinal));

            Assert.Equal("Textures/Fabric.png", generatedMaterial.MaterialAsset.DiffuseTextureAssetId);
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
        /// Writes an OBJ fixture and matching material library that switch between two materials.
        /// </summary>
        /// <param name="fileName">Fixture file name.</param>
        /// <returns>Absolute path to the OBJ fixture.</returns>
        string WriteObjFixtureWithMtl(string fileName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("File name must be provided.", nameof(fileName));
            }

            string materialLibraryPath = Path.Combine(FixtureRootPath, "sponza.mtl");
            string textureDirectoryPath = Path.Combine(FixtureRootPath, "Textures");
            Directory.CreateDirectory(textureDirectoryPath);
            File.WriteAllText(
                materialLibraryPath,
                string.Join(
                    Environment.NewLine,
                    "newmtl Fabric",
                    "map_Kd Textures/Fabric.png",
                    string.Empty,
                    "newmtl Wood"));

            string sourcePath = Path.Combine(FixtureRootPath, fileName);
            File.WriteAllText(
                sourcePath,
                string.Join(
                    Environment.NewLine,
                    "mtllib sponza.mtl",
                    "v 0 0 0",
                    "v 1 0 0",
                    "v 0 1 0",
                    "v 1 1 0",
                    "v 2 1 0",
                    "v 1 2 0",
                    "vt 0 0",
                    "vt 1 0",
                    "vt 0 1",
                    "vt 1 1",
                    "vt 2 1",
                    "vt 1 2",
                    "vn 0 0 1",
                    "usemtl Fabric",
                    "f 1/1/1 2/2/1 3/3/1",
                    "usemtl Wood",
                    "f 4/4/1 5/5/1 6/6/1"));
            return sourcePath;
        }

        /// <summary>
        /// Writes a minimal text-based X fixture with two materials assigned to two faces.
        /// </summary>
        /// <param name="fileName">Fixture file name.</param>
        /// <returns>Absolute path to the X fixture.</returns>
        string WriteXFixtureWithMtl(string fileName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("File name must be provided.", nameof(fileName));
            }

            string sourcePath = Path.Combine(FixtureRootPath, fileName);
            File.WriteAllText(
                sourcePath,
                string.Join(
                    Environment.NewLine,
                    "xof 0303txt 0032",
                    "Mesh RacerMesh {",
                    "  6;",
                    "  0.0; 0.0; 0.0;,",
                    "  1.0; 0.0; 0.0;,",
                    "  0.0; 1.0; 0.0;,",
                    "  1.0; 1.0; 0.0;,",
                    "  0.0; 2.0; 0.0;,",
                    "  1.0; 2.0; 0.0;;",
                    "  2;",
                    "  3; 0, 1, 2;,",
                    "  3; 3, 4, 5;;",
                    "  MeshMaterialList {",
                    "    2;",
                    "    2;",
                    "    0,",
                    "    1;;",
                    "    Material Body {",
                    "      1.0; 1.0; 1.0; 1.0;;",
                    "      0.0;",
                    "      0.0; 0.0; 0.0;;",
                    "      0.0; 0.0; 0.0;;",
                    "    }",
                    "    Material Trim {",
                    "      1.0; 1.0; 1.0; 1.0;;",
                    "      0.0;",
                    "      0.0; 0.0; 0.0;;",
                    "      0.0; 0.0; 0.0;;",
                    "    }",
                    "  }",
                    "}"));
            return sourcePath;
        }

        /// <summary>
        /// Writes a minimal X fixture whose materials intentionally share the same display name.
        /// </summary>
        /// <param name="fileName">Fixture file name.</param>
        /// <returns>Absolute path to the X fixture.</returns>
        string WriteXFixtureWithDuplicateMaterialNames(string fileName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("File name must be provided.", nameof(fileName));
            }

            string sourcePath = Path.Combine(FixtureRootPath, fileName);
            File.WriteAllText(
                sourcePath,
                string.Join(
                    Environment.NewLine,
                    "xof 0303txt 0032",
                    "Mesh RacerMesh {",
                    "  6;",
                    "  0.0; 0.0; 0.0;,",
                    "  1.0; 0.0; 0.0;,",
                    "  0.0; 1.0; 0.0;,",
                    "  1.0; 1.0; 0.0;,",
                    "  0.0; 2.0; 0.0;,",
                    "  1.0; 2.0; 0.0;;",
                    "  2;",
                    "  3; 0, 1, 2;,",
                    "  3; 3, 4, 5;;",
                    "  MeshMaterialList {",
                    "    2;",
                    "    2;",
                    "    0,",
                    "    1;;",
                    "    Material Body {",
                    "      1.0; 1.0; 1.0; 1.0;;",
                    "      0.0;",
                    "      0.0; 0.0; 0.0;;",
                    "      0.0; 0.0; 0.0;;",
                    "    }",
                    "    Material Body {",
                    "      1.0; 1.0; 1.0; 1.0;;",
                    "      0.0;",
                    "      0.0; 0.0; 0.0;;",
                    "      0.0; 0.0; 0.0;;",
                    "    }",
                    "  }",
                    "}"));
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

        /// <summary>
        /// Creates a scene whose meshes reference anonymous materials with material indices that do not match mesh order.
        /// </summary>
        /// <returns>Managed Assimp scene used to verify fallback material slot naming.</returns>
        Scene CreateAnonymousMaterialIndexScene() {
            Scene scene = new Scene();
            for (int materialIndex = 0; materialIndex < 6; materialIndex++) {
                scene.Materials.Add(new Material());
            }

            scene.Meshes.Add(CreateTriangleMesh("mesh-a", 3));
            scene.Meshes.Add(CreateTriangleMesh("mesh-b", 5));
            return scene;
        }

        /// <summary>
        /// Creates one triangle mesh with deterministic vertex data and the requested material index.
        /// </summary>
        /// <param name="meshName">Mesh name used when the material does not provide one.</param>
        /// <param name="materialIndex">Material index to assign to the mesh.</param>
        /// <returns>Triangle mesh with one face and three vertices.</returns>
        Mesh CreateTriangleMesh(string meshName, int materialIndex) {
            if (string.IsNullOrWhiteSpace(meshName)) {
                throw new ArgumentException("Mesh name must be provided.", nameof(meshName));
            } else if (materialIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(materialIndex), "Material index must be non-negative.");
            }

            Mesh mesh = new Mesh(meshName, PrimitiveType.Triangle);
            mesh.MaterialIndex = materialIndex;
            mesh.Vertices.Add(new System.Numerics.Vector3(0f, 0f, 0f));
            mesh.Vertices.Add(new System.Numerics.Vector3(1f, 0f, 0f));
            mesh.Vertices.Add(new System.Numerics.Vector3(0f, 1f, 0f));
            mesh.Normals.Add(new System.Numerics.Vector3(0f, 0f, 1f));
            mesh.Normals.Add(new System.Numerics.Vector3(0f, 0f, 1f));
            mesh.Normals.Add(new System.Numerics.Vector3(0f, 0f, 1f));
            mesh.Faces.Add(new Face(new[] { 0, 1, 2 }));
            return mesh;
        }
    }
}
