using helengine.editor.assimp;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that a glTF imported in scene mode produces a blueprint, one model per mesh and the materials inside a generated folder.
    /// </summary>
    public class SceneModeImportTests : IDisposable {
        /// <summary>
        /// Assets-relative path of the generated folder of the hinged box fixture.
        /// </summary>
        const string GeneratedFolder = "hinged_box.gltf.generated";

        /// <summary>
        /// Temporary project root used for each test.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Temporary assets root holding the copied fixture.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Temporary cache root.
        /// </summary>
        readonly string CacheRootPath;

        /// <summary>
        /// Initializes isolated project directories for each test case.
        /// </summary>
        public SceneModeImportTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-import-tests", Guid.NewGuid().ToString("N"));
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            CacheRootPath = Path.Combine(ProjectRootPath, "cache");
            Directory.CreateDirectory(AssetsRootPath);
            Directory.CreateDirectory(CacheRootPath);
            new EditorProjectPlatformsService(ProjectRootPath).Save(new EditorProjectPlatformsDocument { SupportedPlatforms = ["windows"] });
        }

        /// <summary>
        /// Deletes the temporary project after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures scene mode writes the blueprint, one model for the shared mesh and one material, all with file-system references.
        /// </summary>
        [Fact]
        public void SceneMode_writesBlueprintAndOneModelPerMesh() {
            string sourcePath = CopyFixture();
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);

            manager.ImportModel(sourcePath);

            string folder = Path.Combine(AssetsRootPath, GeneratedFolder);
            Assert.True(File.Exists(Path.Combine(folder, "mesh", "Tri.hasset")));
            Assert.True(File.Exists(Path.Combine(folder, "materials", "Paint.hasset")));
            Assert.Single(Directory.GetFiles(Path.Combine(folder, "mesh")));
            BlueprintAsset blueprint = ReadBlueprint();
            BlueprintValidationService.ValidateAsset(blueprint);
            Assert.Equal(GeneratedFolder + "/hinged_box.hblueprint", blueprint.Id);
            Assert.Equal(2, blueprint.AssetReferences.Length);
            for (int index = 0; index < blueprint.AssetReferences.Length; index++) {
                Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, blueprint.AssetReferences[index].SourceKind);
                Assert.StartsWith(GeneratedFolder + "/", blueprint.AssetReferences[index].RelativePath);
            }

            ModelAsset model = (ModelAsset)ReadAsset(Path.Combine(folder, "mesh", "Tri.hasset"));
            Assert.Equal(manager.LoadOrCreateModelImportSettings(sourcePath).Importer.ProcessedAssetId + "#mesh/Tri", model.Id);
            Assert.Equal(1f, model.Positions[1].X, 4);
            Assert.Equal(0f, model.Positions[1].Y, 4);
        }

        /// <summary>
        /// Ensures blueprint entities mirror the node tree with local transforms and a mesh component per node mesh.
        /// </summary>
        [Fact]
        public void Blueprint_entitiesCarryLocalTransforms() {
            string sourcePath = CopyFixture();
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);

            manager.ImportModel(sourcePath);

            SceneEntityAsset root = ReadBlueprint().RootEntity;
            Assert.Equal(2, root.Children.Length);
            SceneEntityAsset baseEntity = root.Children[0];
            Assert.Equal("Base", baseEntity.Name);
            Assert.Equal(1f, baseEntity.LocalPosition.X, 4);
            Assert.Equal(2f, baseEntity.LocalPosition.Y, 4);
            Assert.Equal(3f, baseEntity.LocalPosition.Z, 4);
            SceneEntityAsset lid = baseEntity.Children[0];
            Assert.Equal("Lid", lid.Name);
            Assert.Equal(2f, lid.LocalScale.X, 4);
            Assert.Equal(0.3826834f, lid.LocalOrientation.Y, 4);
            Assert.Equal(0.9238795f, lid.LocalOrientation.W, 4);
            SceneEntityAsset flap = lid.Children[0];
            Assert.Equal(5f, flap.LocalPosition.Z, 4);
            Assert.Equal("Flap_2", root.Children[1].Name);
            SceneComponentAssetRecord mesh = Assert.Single(lid.Components);
            Assert.Equal("helengine.MeshComponent", mesh.ComponentTypeId);

            HashSet<uint> ids = new HashSet<uint>();
            CollectIds(root, ids);
            Assert.Equal(5, ids.Count);
            Assert.DoesNotContain(0u, ids);
        }

        /// <summary>
        /// Ensures a reimport after the source changed keeps the identity of every generated file.
        /// </summary>
        [Fact]
        public void Reimport_keepsGeneratedAssetIds() {
            string sourcePath = CopyFixture();
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);
            manager.ImportModel(sourcePath);
            string firstProcessedId = manager.LoadOrCreateModelImportSettings(sourcePath).Importer.ProcessedAssetId;
            string[] relativePaths = { "mesh/Tri.hasset", "materials/Paint.hasset", "hinged_box.hblueprint" };
            string[] before = ReadAuthoringIds(relativePaths);

            File.AppendAllText(sourcePath, "\n");
            manager.ImportModel(sourcePath);

            Assert.NotEqual(firstProcessedId, manager.LoadOrCreateModelImportSettings(sourcePath).Importer.ProcessedAssetId);
            Assert.Equal(before, ReadAuthoringIds(relativePaths));
            foreach (string id in before) {
                Assert.Equal(32, id.Length);
            }
        }

        /// <summary>
        /// Ensures files of an earlier import that the new import did not produce are deleted and nothing outside the folder is touched.
        /// </summary>
        [Fact]
        public void RemovedMesh_deletesStaleGeneratedFile() {
            string sourcePath = CopyFixture();
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);
            manager.ImportModel(sourcePath);
            string folder = Path.Combine(AssetsRootPath, GeneratedFolder);
            string stale = Path.Combine(folder, "mesh", "Removed.hasset");
            File.Copy(Path.Combine(folder, "mesh", "Tri.hasset"), stale);
            string staleMaterial = Path.Combine(folder, "materials", "Old.hasset");
            File.WriteAllText(staleMaterial, "old");
            string lookalike = Path.Combine(folder, "materials", "Paint.hasset.notes.txt");
            File.WriteAllText(lookalike, "not an override");
            string outside = Path.Combine(AssetsRootPath, "keep.txt");
            File.WriteAllText(outside, "keep");

            File.AppendAllText(sourcePath, "\n");
            manager.ImportModel(sourcePath);

            Assert.False(File.Exists(stale));
            Assert.False(File.Exists(staleMaterial));
            Assert.False(File.Exists(lookalike));
            Assert.True(File.Exists(Path.Combine(folder, "mesh", "Tri.hasset")));
            Assert.True(File.Exists(outside));
        }

        /// <summary>
        /// Ensures one glTF mesh with two primitives and two materials becomes two mesh components with different material references.
        /// </summary>
        [Fact]
        public void NodeWithTwoPrimitives_getsTwoMeshComponentsWithDifferentMaterials() {
            string sourcePath = CopyFixture("two_primitives.gltf");
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);

            manager.ImportModel(sourcePath);

            string folder = Path.Combine(AssetsRootPath, "two_primitives.gltf.generated");
            BlueprintAsset blueprint = (BlueprintAsset)ReadAsset(Path.Combine(folder, "two_primitives.hblueprint"));
            SceneEntityAsset node = blueprint.RootEntity.Children[0];
            Assert.Equal("Two", node.Name);
            Assert.Equal(2, node.Components.Length);
            Assert.Equal(0, node.Components[0].ComponentIndex);
            Assert.Equal(1, node.Components[1].ComponentIndex);
            Assert.All(node.Components, component => Assert.Equal("helengine.MeshComponent", component.ComponentTypeId));
            Assert.Equal(2, Directory.GetFiles(Path.Combine(folder, "materials")).Count(f => !f.Contains(".hasset.")));
            Assert.Equal(2, Directory.GetFiles(Path.Combine(folder, "mesh")).Length);
            string[] materialPaths = blueprint.AssetReferences.Where(reference => reference.RelativePath.Contains("/materials/")).Select(reference => reference.RelativePath).ToArray();
            Assert.Equal(2, materialPaths.Length);
            Assert.NotEqual(materialPaths[0], materialPaths[1]);
            string first = System.Text.Encoding.UTF8.GetString(node.Components[0].Payload);
            string second = System.Text.Encoding.UTF8.GetString(node.Components[1].Payload);
            Assert.NotEqual(first, second);
            Assert.Single(materialPaths, path => first.Contains(path));
            Assert.Single(materialPaths, path => second.Contains(path));
            Assert.NotEqual(materialPaths.Single(path => first.Contains(path)), materialPaths.Single(path => second.Contains(path)));
        }

        /// <summary>
        /// Ensures a contributor cannot take the blueprint path and consumed indices are range checked.
        /// </summary>
        [Fact]
        public void Output_rejectsReservedPathAndOutOfRangeIndices() {
            ImportedSceneNode root = new ImportedSceneNode("Root", string.Empty, float3.Zero, float4.Identity, float3.One, Array.Empty<int>(), Array.Empty<ImportedSceneNode>());
            ImportedSceneData scene = new ImportedSceneData(root, Array.Empty<ImportedSceneMesh>(), Array.Empty<ImportedSceneMaterial>(), Array.Empty<ImportedSceneTexture>(), Array.Empty<ImportedSceneAnimation>(), string.Empty);
            ImportedSceneOutput output = new ImportedSceneOutput(scene, "a.gltf.generated", "id", "a.hblueprint", (path, material) => new byte[0], extension => true, (file, kind) => null);

            Assert.Throws<ArgumentException>(() => output.AddGeneratedFile("a.hblueprint", new ModelAsset()));
            Assert.Throws<ArgumentOutOfRangeException>(() => output.MarkAnimationConsumed(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => output.MarkMeshConsumed(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => output.MarkMaterialConsumed(0));
            Assert.Throws<ArgumentException>(() => output.AddGeneratedSourceFile("x.hmeta", new byte[1]));
            Assert.Throws<ArgumentException>(() => output.AddGeneratedSourceFile("x.hasset", new byte[1]));
            Assert.Throws<ArgumentException>(() => output.AddGeneratedSourceFile("x.hblueprint", new byte[1]));
        }

        /// <summary>
        /// Ensures an existing generated file whose identity cannot be read fails the import and names the file.
        /// </summary>
        [Fact]
        public void UnreadableExistingIdentity_failsNamingTheFile() {
            string sourcePath = CopyFixture();
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);
            manager.ImportModel(sourcePath);
            string model = Path.Combine(AssetsRootPath, GeneratedFolder, "mesh", "Tri.hasset");
            File.WriteAllText(model, "corrupt");
            File.AppendAllText(sourcePath, " ");

            Exception exception = Assert.ThrowsAny<Exception>(() => manager.ImportModel(sourcePath));

            Assert.Contains("Tri.hasset", exception.Message);
        }

        /// <summary>
        /// Ensures single-model imports keep today's identity, cache bytes and sibling materials, and write no generated folder.
        /// </summary>
        [Fact]
        public void SingleModelMode_isUnchanged() {
            string sourcePath = CopyFixture();
            AssetImportManager manager = CreateManager();

            ModelAsset imported = manager.ImportModel(sourcePath);

            ModelAssetImportSettings settings = manager.LoadOrCreateModelImportSettings(sourcePath);
            string expectedId = ComputeSingleModelId(settings.Importer.SourceChecksum);
            Assert.Equal(expectedId, settings.Importer.ProcessedAssetId);
            Assert.Equal(expectedId, imported.Id);
            Assert.False(Directory.Exists(Path.Combine(AssetsRootPath, GeneratedFolder)));
            Assert.True(File.Exists(Path.Combine(AssetsRootPath, "Paint.hasset")), string.Join(", ", Directory.GetFiles(AssetsRootPath, "*", SearchOption.AllDirectories)));

            ImportedModelAssetSet reference;
            using (FileStream stream = File.OpenRead(sourcePath)) {
                reference = new HelengineAssimpImporter().ImportModel(stream);
            }
            reference.ModelAsset.Id = expectedId;
            reference.ModelAsset.RuntimeAssetId = RuntimeAssetIdGenerator.Generate(expectedId);
            Assert.Equal(AssetSerializer.SerializeToBytes(reference.ModelAsset), File.ReadAllBytes(Path.Combine(CacheRootPath, expectedId)));
        }

        /// <summary>
        /// Ensures animations nobody consumed are reported.
        /// </summary>
        [Fact]
        public void UnconsumedAnimations_areReported() {
            string sourcePath = CopyFixture();
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);

            manager.ImportModel(sourcePath);

            Assert.Equal(new[] { "1 animations not imported: no animation plugin in the project" }, manager.LastSceneImportUnconsumed);
        }

        /// <summary>
        /// Ensures a plugin contributor runs after the core contributor, can see its entities and adds its own generated file.
        /// </summary>
        [Fact]
        public void PluginContributor_runsAfterCoreAndAddsFiles() {
            string sourcePath = CopyFixture();
            AssetImportManager manager = CreateManager();
            RecordingContributor plugin = new RecordingContributor();
            manager.ImporterRegistry.RegisterSceneContributor(plugin);
            SetMode(manager, sourcePath, ModelImportMode.Scene);

            manager.ImportModel(sourcePath);

            Assert.Equal(new[] { CoreImportedSceneContributor.CoreContributorId, RecordingContributor.PluginId }, ContributorIds(manager));
            Assert.True(plugin.SawBaseEntity);
            Assert.True(File.Exists(Path.Combine(AssetsRootPath, GeneratedFolder, "extra", "Extra.hasset")));
            Assert.Equal(new[] { "1 animations not imported: no animation plugin in the project" }, manager.LastSceneImportUnconsumed);
        }

        /// <summary>
        /// Ensures switching a file between the modes changes the cache identity so the up-to-date check re-imports it.
        /// </summary>
        [Fact]
        public void SwitchingMode_reimportsThroughTheCacheCheck() {
            string sourcePath = CopyFixture();
            AssetImportManager manager = CreateManager();
            manager.ImportModel(sourcePath);
            Assert.Empty(manager.ImportModelsMissingCache());

            SetMode(manager, sourcePath, ModelImportMode.Scene);

            Assert.Single(manager.ImportModelsMissingCache());
            Assert.True(File.Exists(Path.Combine(AssetsRootPath, GeneratedFolder, "hinged_box.hblueprint")));
            Assert.Empty(manager.ImportModelsMissingCache());

            // The single-model cache entry of the first import is still current, so switching back needs no new import.
            SetMode(manager, sourcePath, ModelImportMode.SingleModel);

            Assert.Empty(manager.ImportModelsMissingCache());
        }

        /// <summary>
        /// Ensures the registry runs the core contributor first, rejects duplicates and refuses registration once frozen.
        /// </summary>
        [Fact]
        public void Registry_ordersCoreFirstAndValidatesRegistrations() {
            AssetImportManager manager = CreateManager();
            AssetImporterRegistry registry = manager.ImporterRegistry;
            registry.RegisterSceneContributor(new RecordingContributor());

            Assert.Throws<InvalidOperationException>(() => registry.RegisterSceneContributor(new RecordingContributor()));
            Assert.Equal(new[] { CoreImportedSceneContributor.CoreContributorId, RecordingContributor.PluginId }, ContributorIds(manager));

            registry.Freeze();
            Assert.Throws<InvalidOperationException>(() => registry.RegisterSceneContributor(new OtherContributor()));
        }

        /// <summary>
        /// Ensures a PNG embedded in a glb is extracted into the generated folder, imported as a texture source and bound to the material.
        /// </summary>
        [Fact]
        public void EmbeddedTexture_isExtractedImportedAndBound() {
            string sourcePath = CopyFixture("textured_quad.glb");
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);

            manager.ImportModel(sourcePath);

            string folder = Path.Combine(AssetsRootPath, "textured_quad.glb.generated");
            string texturePath = Path.Combine(folder, "textures", "quad_art.png");
            Assert.Equal(File.ReadAllBytes(FixturePath("textured_quad.png")), File.ReadAllBytes(texturePath));
            Assert.True(File.Exists(texturePath + ".hmeta"));
            Assert.True(File.Exists(texturePath + AssetImportManager.SettingsExtension));
            Assert.True(manager.TryLoadOrCreateTextureImportSettings(texturePath, out TextureAssetImportSettings settings));
            Assert.False(string.IsNullOrWhiteSpace(settings.Importer.ProcessedAssetId));

            string textureId = ReadMaterialTextureId(Path.Combine(folder, "materials", "Printed.hasset"));
            Assert.Equal("textured_quad.glb.generated/textures/quad_art.png", textureId);
            Assert.True(manager.TryResolveImportedTextureSourcePath(textureId, out string resolved));
            Assert.Equal(Path.GetFullPath(texturePath), Path.GetFullPath(resolved));
        }

        /// <summary>
        /// Ensures an external PNG next to a .gltf is copied into the generated folder and bound to the material.
        /// </summary>
        [Fact]
        public void ExternalTexture_isCopiedAndBound() {
            string sourcePath = CopyFixture("textured_quad.gltf");
            File.Copy(FixturePath("textured_quad.png"), Path.Combine(AssetsRootPath, "textured_quad.png"));
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);

            manager.ImportModel(sourcePath);

            string folder = Path.Combine(AssetsRootPath, "textured_quad.gltf.generated");
            string texturePath = Path.Combine(folder, "textures", "textured_quad.png");
            Assert.Equal(File.ReadAllBytes(FixturePath("textured_quad.png")), File.ReadAllBytes(texturePath));
            Assert.True(File.Exists(Path.Combine(AssetsRootPath, "textured_quad.png")));
            Assert.Equal(
                "textured_quad.gltf.generated/textures/textured_quad.png",
                ReadMaterialTextureId(Path.Combine(folder, "materials", "Printed.hasset")));
        }

        /// <summary>
        /// Ensures a missing external texture file fails the import and names the material and the file.
        /// </summary>
        [Fact]
        public void MissingExternalTexture_failsNamingMaterial() {
            string sourcePath = CopyFixture("textured_quad.gltf");
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);

            FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => manager.ImportModel(sourcePath));

            Assert.Contains("Printed", exception.Message);
            Assert.Contains("textured_quad.png", exception.Message);
        }

        /// <summary>
        /// Ensures a reimport keeps the identity of the texture file and its settings and does not sweep them away.
        /// </summary>
        [Fact]
        public void Reimport_keepsTextureIds() {
            string sourcePath = CopyFixture("textured_quad.glb");
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);
            manager.ImportModel(sourcePath);
            string folder = Path.Combine(AssetsRootPath, "textured_quad.glb.generated");
            string texturePath = Path.Combine(folder, "textures", "quad_art.png");
            AssetIdentityMetadataService identity = new AssetIdentityMetadataService(ProjectRootPath);
            string idBefore = identity.Load(texturePath).AssetId;
            string settingsBefore = manager.LoadOrCreateTextureImportSettings(texturePath).Importer.ProcessedAssetId;
            Assert.Equal(32, idBefore.Length);

            // A trailing byte after the last chunk changes the source checksum without invalidating the glb.
            File.AppendAllText(sourcePath, "\0");
            manager.ImportModel(sourcePath);

            Assert.True(File.Exists(texturePath));
            Assert.True(File.Exists(texturePath + ".hmeta"));
            Assert.True(File.Exists(texturePath + AssetImportManager.SettingsExtension));
            Assert.Equal(idBefore, identity.Load(texturePath).AssetId);
            Assert.Equal(settingsBefore, manager.LoadOrCreateTextureImportSettings(texturePath).Importer.ProcessedAssetId);
            Assert.Single(Directory.GetFiles(Path.Combine(folder, "textures"), "*.png"));
        }

        /// <summary>
        /// Ensures an external texture that leaves the project assets (through "../") fails the import naming the material and the path.
        /// </summary>
        [Fact]
        public void ExternalTexture_escapingAssetsRoot_isRejected() {
            File.Copy(FixturePath("textured_quad.png"), Path.Combine(ProjectRootPath, "outside.png"));
            string sourcePath = WriteVariant("textured_quad.gltf", root => root["images"][0]["uri"] = "../outside.png");
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => manager.ImportModel(sourcePath));

            Assert.Contains("Printed", exception.Message);
            Assert.Contains("../outside.png", exception.Message);
        }

        /// <summary>
        /// Ensures an absolute external texture path fails the import naming the material and the path.
        /// </summary>
        [Fact]
        public void ExternalTexture_withAbsolutePath_isRejected() {
            string absolute = Path.Combine(ProjectRootPath, "outside.png").Replace('\\', '/');
            File.Copy(FixturePath("textured_quad.png"), Path.Combine(ProjectRootPath, "outside.png"));
            string sourcePath = WriteVariant("textured_quad.gltf", root => root["images"][0]["uri"] = absolute);
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);

            Exception exception = Assert.ThrowsAny<Exception>(() => manager.ImportModel(sourcePath));

            Assert.Contains("Printed", exception.Message);
            Assert.Contains("outside.png", exception.Message);
        }

        /// <summary>
        /// Ensures a "../" texture that stays inside the assets root is accepted.
        /// </summary>
        [Fact]
        public void ExternalTexture_inSiblingFolderInsideAssets_isAccepted() {
            Directory.CreateDirectory(Path.Combine(AssetsRootPath, "models"));
            Directory.CreateDirectory(Path.Combine(AssetsRootPath, "textures"));
            File.Copy(FixturePath("textured_quad.png"), Path.Combine(AssetsRootPath, "textures", "shared.png"));
            string text = File.ReadAllText(FixturePath("textured_quad.gltf")).Replace("textured_quad.png", "../textures/shared.png");
            string sourcePath = Path.Combine(AssetsRootPath, "models", "textured_quad.gltf");
            File.WriteAllText(sourcePath, text);
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);

            manager.ImportModel(sourcePath);

            Assert.True(File.Exists(Path.Combine(AssetsRootPath, "models", "textured_quad.gltf.generated", "textures", "shared.png")));
        }

        /// <summary>
        /// Ensures an unsupported image in the normal map slot, which nothing consumes yet, does not break the import.
        /// </summary>
        [Fact]
        public void UnsupportedNormalMap_doesNotBreakImport() {
            string sourcePath = WriteVariant("textured_quad.gltf", root => {
                root["images"].AsArray().Add(new System.Text.Json.Nodes.JsonObject { ["uri"] = "normal.webp" });
                root["textures"].AsArray().Add(new System.Text.Json.Nodes.JsonObject { ["source"] = 1 });
                root["materials"][0]["normalTexture"] = new System.Text.Json.Nodes.JsonObject { ["index"] = 1 };
            });
            File.Copy(FixturePath("textured_quad.png"), Path.Combine(AssetsRootPath, "textured_quad.png"));
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);

            manager.ImportModel(sourcePath);

            string textures = Path.Combine(AssetsRootPath, "textured_quad.gltf.generated", "textures");
            Assert.Single(Directory.GetFiles(textures, "*.png"));
            Assert.Empty(Directory.GetFiles(textures, "*.webp"));
        }

        /// <summary>
        /// Ensures a texture whose extension has no registered texture importer fails the import naming the extension and the material.
        /// </summary>
        [Fact]
        public void TextureWithoutRegisteredImporter_failsAtImport() {
            string sourcePath = CopyFixture("textured_quad.glb");
            AssetImportManager manager = CreateManager(false);
            SetMode(manager, sourcePath, ModelImportMode.Scene);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => manager.ImportModel(sourcePath));

            Assert.Contains("Printed", exception.Message);
            Assert.Contains(".png", exception.Message);
        }

        /// <summary>
        /// Ensures a texture the source no longer uses is deleted, with its sidecars, on reimport.
        /// </summary>
        [Fact]
        public void RemovedTexture_isSweptOnReimport() {
            string sourcePath = CopyFixture("textured_quad.gltf");
            File.Copy(FixturePath("textured_quad.png"), Path.Combine(AssetsRootPath, "textured_quad.png"));
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);
            manager.ImportModel(sourcePath);
            string texturePath = Path.Combine(AssetsRootPath, "textured_quad.gltf.generated", "textures", "textured_quad.png");
            Assert.True(File.Exists(texturePath));

            File.Copy(FixturePath("hinged_box.gltf"), sourcePath, true);
            manager.ImportModel(sourcePath);

            Assert.False(File.Exists(texturePath));
            Assert.False(File.Exists(texturePath + ".hmeta"));
            Assert.False(File.Exists(texturePath + AssetImportManager.SettingsExtension));
        }

        /// <summary>
        /// Copies a scene fixture into the assets folder after editing its JSON.
        /// </summary>
        /// <param name="fileName">Fixture file name.</param>
        /// <param name="edit">Edit applied to the parsed document.</param>
        /// <returns>Absolute path of the written source.</returns>
        string WriteVariant(string fileName, Action<System.Text.Json.Nodes.JsonNode> edit) {
            System.Text.Json.Nodes.JsonNode root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(FixturePath(fileName)));
            edit(root);
            string destination = Path.Combine(AssetsRootPath, fileName);
            File.WriteAllText(destination, root.ToJsonString());
            return destination;
        }

        /// <summary>
        /// Reads the base color texture id the material file stores for the windows platform.
        /// </summary>
        /// <param name="materialPath">Absolute path of the generated material.</param>
        /// <returns>The "texture-id" field value.</returns>
        string ReadMaterialTextureId(string materialPath) {
            MaterialAssetSettingsService service = new MaterialAssetSettingsService(ProjectRootPath);
            Assert.True(service.TryLoadPlatformSettings(materialPath, "windows", out MaterialAssetProcessorSettings settings));
            return settings.FieldValues["texture-id"];
        }

        /// <summary>
        /// Resolves the path of a scene fixture in the test output.
        /// </summary>
        /// <param name="fileName">Fixture file name.</param>
        /// <returns>Absolute fixture path.</returns>
        static string FixturePath(string fileName) {
            return Path.Combine(AppContext.BaseDirectory, "fixtures", "scene", fileName);
        }

        /// <summary>
        /// Lists the contributor ids in execution order.
        /// </summary>
        /// <param name="manager">Manager whose registry is inspected.</param>
        /// <returns>Contributor ids.</returns>
        static string[] ContributorIds(AssetImportManager manager) {
            IReadOnlyList<IImportedSceneContributor> contributors = manager.ImporterRegistry.GetSceneContributors();
            string[] ids = new string[contributors.Count];
            for (int index = 0; index < ids.Length; index++) {
                ids[index] = contributors[index].ContributorId;
            }

            return ids;
        }

        /// <summary>
        /// Collects every entity id in the subtree.
        /// </summary>
        /// <param name="entity">Subtree root.</param>
        /// <param name="ids">Receives the ids.</param>
        static void CollectIds(SceneEntityAsset entity, HashSet<uint> ids) {
            ids.Add(entity.Id);
            for (int index = 0; index < entity.Children.Length; index++) {
                CollectIds(entity.Children[index], ids);
            }
        }

        /// <summary>
        /// Computes the single-model processed id exactly as it was before scene mode existed.
        /// </summary>
        /// <param name="sourceChecksum">Checksum of the source file.</param>
        /// <returns>Processed asset id.</returns>
        static string ComputeSingleModelId(string sourceChecksum) {
            string identity = string.Concat("model", "\n", sourceChecksum, "\n", "assimp", "\n", string.Empty, "\n", "0", "\n", "0", "\n",
                1d.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// Deserializes one native asset file.
        /// </summary>
        /// <param name="path">Absolute file path.</param>
        /// <returns>Deserialized asset.</returns>
        static Asset ReadAsset(string path) {
            using FileStream stream = File.OpenRead(path);
            return AssetSerializer.Deserialize(stream);
        }

        /// <summary>
        /// Reads the authoring ids of generated files.
        /// </summary>
        /// <param name="relativePaths">Paths relative to the generated folder.</param>
        /// <returns>Authoring ids in the same order.</returns>
        string[] ReadAuthoringIds(string[] relativePaths) {
            AssetIdentityMetadataService identity = new AssetIdentityMetadataService(ProjectRootPath);
            string[] ids = new string[relativePaths.Length];
            for (int index = 0; index < ids.Length; index++) {
                ids[index] = identity.Load(Path.Combine(AssetsRootPath, GeneratedFolder, relativePaths[index])).AssetId;
            }

            return ids;
        }

        /// <summary>
        /// Reads the generated blueprint.
        /// </summary>
        /// <returns>Deserialized blueprint.</returns>
        BlueprintAsset ReadBlueprint() {
            return (BlueprintAsset)ReadAsset(Path.Combine(AssetsRootPath, GeneratedFolder, "hinged_box.hblueprint"));
        }

        /// <summary>
        /// Persists the import mode of a source file.
        /// </summary>
        /// <param name="manager">Manager owning the sidecar.</param>
        /// <param name="sourcePath">Absolute source path.</param>
        /// <param name="mode">Mode to store.</param>
        static void SetMode(AssetImportManager manager, string sourcePath, ModelImportMode mode) {
            ModelAssetImportSettings settings = manager.LoadOrCreateModelImportSettings(sourcePath);
            settings.Mode = mode;
            manager.SaveModelImportSettings(sourcePath, settings);
        }

        /// <summary>
        /// Copies the hinged box fixture into the temporary assets folder.
        /// </summary>
        /// <returns>Absolute path to the copied source.</returns>
        string CopyFixture() {
            return CopyFixture("hinged_box.gltf");
        }

        /// <summary>
        /// Copies a scene fixture into the temporary assets folder.
        /// </summary>
        /// <param name="fileName">Fixture file name.</param>
        /// <returns>Absolute path to the copied source.</returns>
        string CopyFixture(string fileName) {
            string destination = Path.Combine(AssetsRootPath, fileName);
            File.Copy(Path.Combine(AppContext.BaseDirectory, "fixtures", "scene", fileName), destination);
            return destination;
        }

        /// <summary>
        /// Creates a manager with the Assimp importer registered for glTF files.
        /// </summary>
        /// <returns>Configured manager.</returns>
        AssetImportManager CreateManager() {
            return CreateManager(true);
        }

        /// <summary>
        /// Creates a manager with the Assimp importer registered for glTF files and optionally the texture importer.
        /// </summary>
        /// <param name="registerTextureImporter">True to register a PNG/JPEG texture importer.</param>
        /// <returns>Configured manager.</returns>
        AssetImportManager CreateManager(bool registerTextureImporter) {
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(AssetsRootPath));
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterModelImporter(new ModelImporterRegistration("assimp", new HelengineAssimpImporter(), new[] { ".gltf", ".glb" }));
            if (registerTextureImporter) {
                manager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new helengine.editor.tests.testing.TestTextureImporter(), new[] { ".png", ".jpg" }));
            }
            return manager;
        }

        /// <summary>
        /// Plugin contributor that records what it saw and adds one extra generated model.
        /// </summary>
        sealed class RecordingContributor : IImportedSceneContributor {
            /// <summary>
            /// Identifier of the recording contributor.
            /// </summary>
            public const string PluginId = "test.recording";

            /// <summary>
            /// Gets a value indicating whether the core entities existed when this contributor ran.
            /// </summary>
            public bool SawBaseEntity { get; private set; }

            /// <summary>
            /// Gets the contributor identifier.
            /// </summary>
            public string ContributorId => PluginId;

            /// <summary>
            /// Records the core entities and adds an extra file.
            /// </summary>
            /// <param name="scene">Scene data.</param>
            /// <param name="output">Output to extend.</param>
            public void Contribute(ImportedSceneData scene, ImportedSceneOutput output) {
                SawBaseEntity = output.GetNodeEntity("Base").Name == "Base";
                output.AddGeneratedFile("extra/Extra.hasset", new ModelAsset {
                    Id = "extra",
                    Positions = new float3[3],
                    Normals = new float3[3],
                    TexCoords = new float2[3],
                    Indices16 = new ushort[] { 0, 1, 2 },
                    Submeshes = new[] { new ModelSubmeshAsset { IndexStart = 0, IndexCount = 3, MaterialSlotName = "Default" } }
                });
            }
        }

        /// <summary>
        /// Second plugin contributor used to test registration rules.
        /// </summary>
        sealed class OtherContributor : IImportedSceneContributor {
            /// <summary>
            /// Gets the contributor identifier.
            /// </summary>
            public string ContributorId => "test.other";

            /// <summary>
            /// Does nothing.
            /// </summary>
            /// <param name="scene">Scene data.</param>
            /// <param name="output">Output.</param>
            public void Contribute(ImportedSceneData scene, ImportedSceneOutput output) {
            }
        }
    }
}
