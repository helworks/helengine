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
            string outside = Path.Combine(AssetsRootPath, "keep.txt");
            File.WriteAllText(outside, "keep");

            File.AppendAllText(sourcePath, "\n");
            manager.ImportModel(sourcePath);

            Assert.False(File.Exists(stale));
            Assert.False(File.Exists(staleMaterial));
            Assert.True(File.Exists(Path.Combine(folder, "mesh", "Tri.hasset")));
            Assert.True(File.Exists(outside));
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
            string destination = Path.Combine(AssetsRootPath, "hinged_box.gltf");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "fixtures", "scene", "hinged_box.gltf"), destination);
            return destination;
        }

        /// <summary>
        /// Creates a manager with the Assimp importer registered for glTF files.
        /// </summary>
        /// <returns>Configured manager.</returns>
        AssetImportManager CreateManager() {
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(AssetsRootPath));
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterModelImporter(new ModelImporterRegistration("assimp", new HelengineAssimpImporter(), new[] { ".gltf" }));
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
