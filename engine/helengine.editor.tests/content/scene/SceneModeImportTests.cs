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
            Assert.Equal(new AssetIdentityMetadataService(ProjectRootPath).Load(sourcePath).AssetId + "#mesh/Tri", model.Id);
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
        /// Ensures two byte-identical sources get disjoint generated ids, because the ids derive from each source's own authoring id.
        /// </summary>
        [Fact]
        public void IdenticalSources_getDisjointGeneratedIds() {
            string firstSource = CopyFixture();
            string secondSource = Path.Combine(AssetsRootPath, "hinged_copy.gltf");
            File.Copy(firstSource, secondSource);
            AssetImportManager manager = CreateManager();
            SetMode(manager, firstSource, ModelImportMode.Scene);
            SetMode(manager, secondSource, ModelImportMode.Scene);

            manager.ImportModel(firstSource);
            manager.ImportModel(secondSource);

            string[] firstPaths = { "mesh/Tri.hasset", "materials/Paint.hasset", "hinged_box.hblueprint" };
            string[] secondPaths = { "mesh/Tri.hasset", "materials/Paint.hasset", "hinged_copy.hblueprint" };
            string[] first = ReadAuthoringIds("hinged_box.gltf.generated", firstPaths);
            string[] second = ReadAuthoringIds("hinged_copy.gltf.generated", secondPaths);
            Assert.Equal(6, first.Concat(second).Distinct().Count());

            // Colliding ids would only look disjoint because the identity index repairs duplicates with random ids; regenerating both
            // folders in the other order proves the ids are the deterministic ones.
            Directory.Delete(Path.Combine(AssetsRootPath, "hinged_box.gltf.generated"), true);
            Directory.Delete(Path.Combine(AssetsRootPath, "hinged_copy.gltf.generated"), true);
            manager.ImportModel(secondSource);
            manager.ImportModel(firstSource);
            Assert.Equal(first, ReadAuthoringIds("hinged_box.gltf.generated", firstPaths));
            Assert.Equal(second, ReadAuthoringIds("hinged_copy.gltf.generated", secondPaths));
            ModelAsset firstModel = (ModelAsset)ReadAsset(Path.Combine(AssetsRootPath, "hinged_box.gltf.generated", "mesh", "Tri.hasset"));
            ModelAsset secondModel = (ModelAsset)ReadAsset(Path.Combine(AssetsRootPath, "hinged_copy.gltf.generated", "mesh", "Tri.hasset"));
            Assert.NotEqual(firstModel.Id, secondModel.Id);
            Assert.NotEqual(firstModel.RuntimeAssetId, secondModel.RuntimeAssetId);
        }

        /// <summary>
        /// Ensures a generated folder that is deleted and regenerated after the source and its processor settings changed gets the same ids
        /// as before, so scenes that reference the blueprint keep resolving.
        /// </summary>
        [Fact]
        public void RegeneratedFolder_keepsIdsAcrossSourceEditSettingsAndPlatform() {
            new EditorProjectPlatformsService(ProjectRootPath).Save(new EditorProjectPlatformsDocument { SupportedPlatforms = ["windows", "webgpu"] });
            string sourcePath = CopyFixture();
            AssetImportManager manager = CreateManager();
            manager.CurrentPlatformId = "windows";
            SetMode(manager, sourcePath, ModelImportMode.Scene);
            manager.ImportModel(sourcePath);
            string firstProcessedId = manager.LoadOrCreateModelImportSettings(sourcePath).Importer.ProcessedAssetId;
            string[] relativePaths = { "mesh/Tri.hasset", "materials/Paint.hasset", "hinged_box.hblueprint" };
            string[] before = ReadAuthoringIds(relativePaths);
            string modelIdBefore = ((ModelAsset)ReadAsset(Path.Combine(AssetsRootPath, GeneratedFolder, "mesh", "Tri.hasset"))).Id;

            File.AppendAllText(sourcePath, "\n");
            AssetImportManager otherPlatform = CreateManager();
            otherPlatform.CurrentPlatformId = "webgpu";
            ModelAssetImportSettings settings = otherPlatform.LoadOrCreateModelImportSettings(sourcePath);
            settings.Processor.Platforms["webgpu"] = new ModelAssetProcessorSettings { FlipWinding = true };
            otherPlatform.SaveModelImportSettings(sourcePath, settings);
            Directory.Delete(Path.Combine(AssetsRootPath, GeneratedFolder), true);
            otherPlatform.ImportModel(sourcePath);

            Assert.NotEqual(firstProcessedId, otherPlatform.LoadOrCreateModelImportSettings(sourcePath).Importer.ProcessedAssetId);
            Assert.Equal(before, ReadAuthoringIds(relativePaths));
            Assert.Equal(modelIdBefore, ((ModelAsset)ReadAsset(Path.Combine(AssetsRootPath, GeneratedFolder, "mesh", "Tri.hasset"))).Id);
        }

        /// <summary>
        /// Ensures a build-time import (generated folders read-only) for another platform with other processor settings still writes its
        /// cache model but leaves the authored generated folder byte for byte as the editor wrote it.
        /// </summary>
        [Fact]
        public void ReadOnlyImport_leavesGeneratedFolderByteIdentical() {
            new EditorProjectPlatformsService(ProjectRootPath).Save(new EditorProjectPlatformsDocument { SupportedPlatforms = ["windows", "webgpu"] });
            string sourcePath = CopyFixture();
            AssetImportManager editor = CreateManager();
            editor.CurrentPlatformId = "windows";
            SetMode(editor, sourcePath, ModelImportMode.Scene);
            ModelAssetImportSettings settings = editor.LoadOrCreateModelImportSettings(sourcePath);
            settings.Processor.Platforms["webgpu"] = new ModelAssetProcessorSettings { FlipWinding = true };
            editor.SaveModelImportSettings(sourcePath, settings);
            editor.ImportModel(sourcePath);
            Dictionary<string, byte[]> before = SnapshotFolder(Path.Combine(AssetsRootPath, GeneratedFolder));

            AssetImportManager build = CreateManager();
            build.CurrentPlatformId = "webgpu";
            build.GeneratedSceneFoldersAreReadOnly = true;
            build.ImportModel(sourcePath);

            string buildProcessedId = build.LoadOrCreateModelImportSettings(sourcePath).Importer.ProcessedAssetId;
            Assert.True(File.Exists(build.GetModelAssetPath(buildProcessedId)));
            AssertSnapshotEqual(before, SnapshotFolder(Path.Combine(AssetsRootPath, GeneratedFolder)));
        }

        /// <summary>
        /// Ensures a build-time import of a scene-mode source whose generated folder is missing fails, tells the user to reimport in the
        /// editor and writes nothing.
        /// </summary>
        [Fact]
        public void ReadOnlyImport_failsWhenGeneratedFolderIsMissing() {
            string sourcePath = CopyFixture();
            AssetImportManager editor = CreateManager();
            SetMode(editor, sourcePath, ModelImportMode.Scene);
            editor.ImportModel(sourcePath);
            Directory.Delete(Path.Combine(AssetsRootPath, GeneratedFolder), true);

            AssetImportManager build = CreateManager();
            build.GeneratedSceneFoldersAreReadOnly = true;
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => build.ImportModel(sourcePath));

            Assert.Contains("hinged_box.gltf", exception.Message);
            Assert.Contains("reimport", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(Directory.Exists(Path.Combine(AssetsRootPath, GeneratedFolder)));
        }

        /// <summary>
        /// Ensures a build-time import fails when the source changed after its generated folder was written, and leaves the folder as it was.
        /// </summary>
        [Fact]
        public void ReadOnlyImport_failsWhenSourceChangedAfterGeneration() {
            string sourcePath = CopyFixture();
            AssetImportManager editor = CreateManager();
            SetMode(editor, sourcePath, ModelImportMode.Scene);
            editor.ImportModel(sourcePath);
            Dictionary<string, byte[]> before = SnapshotFolder(Path.Combine(AssetsRootPath, GeneratedFolder));
            File.AppendAllText(sourcePath, "\n");

            AssetImportManager build = CreateManager();
            build.GeneratedSceneFoldersAreReadOnly = true;
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => build.ImportModel(sourcePath));

            Assert.Contains("source changed", exception.Message);
            Assert.Contains("reimport", exception.Message, StringComparison.OrdinalIgnoreCase);
            AssertSnapshotEqual(before, SnapshotFolder(Path.Combine(AssetsRootPath, GeneratedFolder)));
        }

        /// <summary>
        /// Ensures the GPU reduction policy of a generated mesh is read from the owning source's sidecar, because the generated folder has
        /// no model sidecar of its own.
        /// </summary>
        [Fact]
        public void GeneratedMesh_readsGpuReductionPolicyOfItsSource() {
            string sourcePath = CopyFixture();
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);
            ModelAssetImportSettings settings = manager.LoadOrCreateModelImportSettings(sourcePath);
            settings.Processor.Platforms["windows"] = new ModelAssetProcessorSettings {
                GpuReduction = new ModelGpuReductionSettings { Enabled = true, MaximumVertices = 120, MaximumIndices = 360 }
            };
            manager.SaveModelImportSettings(sourcePath, settings);
            manager.ImportModel(sourcePath);

            ModelGpuReductionSettings policy = ModelGpuReductionAssetSettingsReader.Read(
                Path.Combine(AssetsRootPath, GeneratedFolder, "mesh", "Tri.hasset"), "windows", string.Empty);

            Assert.NotNull(policy);
            Assert.True(policy.Enabled);
            Assert.Equal(120, policy.MaximumVertices);
            Assert.Equal(360, policy.MaximumIndices);
            Assert.Null(ModelGpuReductionAssetSettingsReader.Read(Path.Combine(AssetsRootPath, GeneratedFolder, "mesh", "Tri.hasset"), "ps2", string.Empty));
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
        /// Ensures two contributors cannot add paths that differ only by case, because they name the same file on Windows.
        /// </summary>
        [Fact]
        public void Output_rejectsPathsThatCollideByCase() {
            ImportedSceneOutput output = CreateEmptyOutput();
            output.AddGeneratedFile("Anim/x.hasset", new ModelAsset());
            output.AddGeneratedSourceFile("Textures/t.png", new byte[1]);

            Assert.Throws<InvalidOperationException>(() => output.AddGeneratedFile("anim/X.hasset", new ModelAsset()));
            Assert.Throws<InvalidOperationException>(() => output.AddGeneratedSourceFile("textures/T.PNG", new byte[1]));
            Assert.Equal(2, output.GeneratedFiles.Count);
        }

        /// <summary>
        /// Ensures generated paths with an NTFS stream separator, "." or ".." segments or empty segments are rejected.
        /// </summary>
        [Theory]
        [InlineData("a:b.hasset")]
        [InlineData("x.png:stream")]
        [InlineData("./a.hasset")]
        [InlineData("a/./b.hasset")]
        [InlineData("a/../b.hasset")]
        [InlineData("..")]
        [InlineData("a//b.hasset")]
        [InlineData("a/")]
        [InlineData("a/ /b.hasset")]
        public void Output_rejectsUnsafePathSegments(string path) {
            ImportedSceneOutput output = CreateEmptyOutput();

            Assert.Throws<ArgumentException>(() => output.AddGeneratedFile(path, new ModelAsset()));
            Assert.Throws<ArgumentException>(() => output.AddGeneratedSourceFile(path, new byte[1]));
            Assert.Empty(output.GeneratedFiles);
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
        /// Ensures the up-to-date scan regenerates a deleted generated folder even though the cache model of the source is still current.
        /// </summary>
        [Fact]
        public void MissingGeneratedFolder_isRegeneratedByTheUpToDateScan() {
            string sourcePath = CopyFixture();
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);
            Assert.Single(manager.ImportModelsMissingCache());
            string[] relativePaths = { "mesh/Tri.hasset", "materials/Paint.hasset", "hinged_box.hblueprint" };
            string[] before = ReadAuthoringIds(relativePaths);
            Directory.Delete(Path.Combine(AssetsRootPath, GeneratedFolder), true);

            Assert.Single(manager.ImportModelsMissingCache());

            Assert.Equal(before, ReadAuthoringIds(relativePaths));
            Assert.Empty(manager.ImportModelsMissingCache());
        }

        /// <summary>
        /// Ensures the up-to-date scan regenerates the folder when a scene contributor is added to or removed from the project.
        /// </summary>
        [Fact]
        public void ChangedContributorSet_regeneratesTheFolder() {
            string sourcePath = CopyFixture();
            AssetImportManager coreOnly = CreateManager();
            SetMode(coreOnly, sourcePath, ModelImportMode.Scene);
            Assert.Single(coreOnly.ImportModelsMissingCache());
            string extra = Path.Combine(AssetsRootPath, GeneratedFolder, "extra", "Extra.hasset");
            Assert.False(File.Exists(extra));

            AssetImportManager withPlugin = CreateManager();
            withPlugin.ImporterRegistry.RegisterSceneContributor(new RecordingContributor());
            Assert.Single(withPlugin.ImportModelsMissingCache());
            Assert.True(File.Exists(extra));
            Assert.Empty(withPlugin.ImportModelsMissingCache());

            Assert.Single(CreateManager().ImportModelsMissingCache());
            Assert.False(File.Exists(extra));
        }

        /// <summary>
        /// Ensures the up-to-date scan regenerates the folder when a contributor's output version changes while the source and the
        /// contributor set stay the same.
        /// </summary>
        [Fact]
        public void ChangedContributorOutputVersion_regeneratesTheFolder() {
            string sourcePath = CopyFixture();
            AssetImportManager first = CreateManager();
            first.ImporterRegistry.RegisterSceneContributor(new RecordingContributor());
            SetMode(first, sourcePath, ModelImportMode.Scene);
            Assert.Single(first.ImportModelsMissingCache());

            AssetImportManager unchanged = CreateManager();
            unchanged.ImporterRegistry.RegisterSceneContributor(new RecordingContributor());
            Assert.Empty(unchanged.ImportModelsMissingCache());

            AssetImportManager bumped = CreateManager();
            bumped.ImporterRegistry.RegisterSceneContributor(new RecordingContributor { OutputVersion = 2 });
            Assert.Single(bumped.ImportModelsMissingCache());
            Assert.Empty(bumped.ImportModelsMissingCache());
            ModelAssetImportSettings settings = bumped.LoadOrCreateModelImportSettings(sourcePath);
            Assert.True(SidecarDataEntryRegistry.Shared.TryRead(settings.Importer.SidecarData, GeneratedSceneImportStamp.SidecarDataKey, out GeneratedSceneImportStamp stamp));
            Assert.Equal(new[] { CoreImportedSceneContributor.CoreContributorId, RecordingContributor.PluginId }, stamp.ContributorIds);
            Assert.Equal(new[] { new CoreImportedSceneContributor().OutputVersion, 2 }, stamp.ContributorVersions);
        }

        /// <summary>
        /// Ensures a build fails with the reimport message, and does not regenerate, when a contributor's output version changed.
        /// </summary>
        [Fact]
        public void ChangedContributorOutputVersion_failsTheBuildAsStale() {
            string sourcePath = CopyFixture();
            AssetImportManager editor = CreateManager();
            editor.ImporterRegistry.RegisterSceneContributor(new RecordingContributor());
            SetMode(editor, sourcePath, ModelImportMode.Scene);
            editor.ImportModel(sourcePath);
            byte[] sidecarBefore = File.ReadAllBytes(sourcePath + AssetImportManager.SettingsExtension);

            AssetImportManager build = CreateManager();
            build.ImporterRegistry.RegisterSceneContributor(new RecordingContributor { OutputVersion = 2 });
            build.GeneratedSceneFoldersAreReadOnly = true;
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => build.ImportModel(sourcePath));

            Assert.Contains("output changed", exception.Message);
            Assert.Contains("reimport", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(sidecarBefore, File.ReadAllBytes(sourcePath + AssetImportManager.SettingsExtension));
        }

        /// <summary>
        /// Ensures a stamp in the older ids-only layout (payload version 1) is still read but always counts as stale, so the editor
        /// regenerates and a build fails.
        /// </summary>
        [Fact]
        public void IdsOnlyStampLayout_isReadAsStale() {
            string sourcePath = CopyFixture();
            AssetImportManager editor = CreateManager();
            SetMode(editor, sourcePath, ModelImportMode.Scene);
            editor.ImportModel(sourcePath);
            ModelAssetImportSettings settings = editor.LoadOrCreateModelImportSettings(sourcePath);
            using MemoryStream stream = new MemoryStream();
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian)) {
                writer.WriteByte(GeneratedSceneImportStampEntryDefinition.IdsOnlyVersion);
                writer.WriteString("assimp");
                writer.WriteString(settings.Importer.SourceChecksum);
                writer.WriteInt32(1);
                writer.WriteString(CoreImportedSceneContributor.CoreContributorId);
            }
            settings.Importer.SidecarData.SetRaw(GeneratedSceneImportStamp.SidecarDataKey, stream.ToArray());
            editor.SaveModelImportSettings(sourcePath, settings);

            Assert.True(SidecarDataEntryRegistry.Shared.TryRead(editor.LoadOrCreateModelImportSettings(sourcePath).Importer.SidecarData, GeneratedSceneImportStamp.SidecarDataKey, out GeneratedSceneImportStamp old));
            Assert.Null(old.ContributorVersions);
            AssetImportManager build = CreateManager();
            build.GeneratedSceneFoldersAreReadOnly = true;
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => build.ImportModel(sourcePath));
            Assert.Contains("older editor", exception.Message);

            Assert.Single(CreateManager().ImportModelsMissingCache());
            Assert.Empty(CreateManager().ImportModelsMissingCache());
        }

        /// <summary>
        /// Ensures a scene import records its stamp (importer, source checksum, contributors in order) as the
        /// <c>helengine.scene.import</c> entry of the source's sidecar, and writes no stamp file into the generated folder.
        /// </summary>
        [Fact]
        public void SceneImport_recordsStampInTheSourceSidecar() {
            string sourcePath = CopyFixture();
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);
            manager.ImportModel(sourcePath);

            ModelAssetImportSettings settings = manager.LoadOrCreateModelImportSettings(sourcePath);
            Assert.True(SidecarDataEntryRegistry.Shared.TryRead(settings.Importer.SidecarData, GeneratedSceneImportStamp.SidecarDataKey, out GeneratedSceneImportStamp stamp));
            Assert.Equal("helengine.scene.import", GeneratedSceneImportStamp.SidecarDataKey);
            Assert.Equal("assimp", stamp.ImporterId);
            Assert.Equal(settings.Importer.SourceChecksum, stamp.SourceChecksum);
            Assert.Equal(ContributorIds(manager), stamp.ContributorIds);
            Assert.Equal(new[] { new CoreImportedSceneContributor().OutputVersion }, stamp.ContributorVersions);
            Assert.Empty(Directory.GetFiles(Path.Combine(AssetsRootPath, GeneratedFolder), "*.himport", SearchOption.AllDirectories));
        }

        /// <summary>
        /// Ensures the stamp entry survives the sidecar rewrite a build-time import performs, so the next build still finds the folder current.
        /// </summary>
        [Fact]
        public void ReadOnlyImport_keepsTheStampInTheSidecar() {
            string sourcePath = CopyFixture();
            AssetImportManager editor = CreateManager();
            SetMode(editor, sourcePath, ModelImportMode.Scene);
            editor.ImportModel(sourcePath);
            string settingsPath = sourcePath + AssetImportManager.SettingsExtension;
            byte[] sidecarBefore = File.ReadAllBytes(settingsPath);

            AssetImportManager build = CreateManager();
            build.GeneratedSceneFoldersAreReadOnly = true;
            build.ImportModel(sourcePath);
            build.SaveModelImportSettings(sourcePath, build.LoadOrCreateModelImportSettings(sourcePath));
            CreateManager().EnsureGeneratedScenesCurrent();

            Assert.Equal(sidecarBefore, File.ReadAllBytes(settingsPath));
            AssetImportManager nextBuild = CreateManager();
            nextBuild.GeneratedSceneFoldersAreReadOnly = true;
            nextBuild.ImportModel(sourcePath);
        }

        /// <summary>
        /// Ensures a sidecar without the stamp entry (written before the stamp moved into it) makes a build fail as stale and the editor's
        /// up-to-date scan regenerate the folder, which also deletes a leftover <c>scene-import.himport</c> file and records the stamp.
        /// </summary>
        [Fact]
        public void SidecarWithoutStamp_isStaleAndRegeneratesWithoutTheOldStampFile() {
            string sourcePath = CopyFixture();
            AssetImportManager editor = CreateManager();
            SetMode(editor, sourcePath, ModelImportMode.Scene);
            Assert.Single(editor.ImportModelsMissingCache());
            ModelAssetImportSettings settings = editor.LoadOrCreateModelImportSettings(sourcePath);
            Assert.True(settings.Importer.SidecarData.Remove(GeneratedSceneImportStamp.SidecarDataKey));
            editor.SaveModelImportSettings(sourcePath, settings);
            string leftoverStampPath = Path.Combine(AssetsRootPath, GeneratedFolder, "scene-import.himport");
            File.WriteAllText(leftoverStampPath, "{}");

            AssetImportManager build = CreateManager();
            build.GeneratedSceneFoldersAreReadOnly = true;
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => build.ImportModel(sourcePath));
            Assert.Contains("stamp", exception.Message);
            Assert.Contains("reimport", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Throws<InvalidOperationException>(() => CreateManager().EnsureGeneratedScenesCurrent());

            Assert.Single(CreateManager().ImportModelsMissingCache());

            Assert.False(File.Exists(leftoverStampPath));
            ModelAssetImportSettings regenerated = editor.LoadOrCreateModelImportSettings(sourcePath);
            Assert.True(regenerated.Importer.SidecarData.ContainsKey(GeneratedSceneImportStamp.SidecarDataKey));
            CreateManager().EnsureGeneratedScenesCurrent();
        }

        /// <summary>
        /// Ensures a stamp entry its owner cannot read (here an unknown payload version) counts as stale instead of failing the scan.
        /// </summary>
        [Fact]
        public void UnreadableStampEntry_isReportedAsStale() {
            string sourcePath = CopyFixture();
            AssetImportManager editor = CreateManager();
            SetMode(editor, sourcePath, ModelImportMode.Scene);
            editor.ImportModel(sourcePath);
            ModelAssetImportSettings settings = editor.LoadOrCreateModelImportSettings(sourcePath);
            settings.Importer.SidecarData.TryGetRaw(GeneratedSceneImportStamp.SidecarDataKey, out byte[] payload);
            payload[0] = 200;
            settings.Importer.SidecarData.SetRaw(GeneratedSceneImportStamp.SidecarDataKey, payload);
            editor.SaveModelImportSettings(sourcePath, settings);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => CreateManager().EnsureGeneratedScenesCurrent());

            Assert.Contains("cannot be read", exception.Message);
            Assert.Contains("200", exception.Message);
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
        /// Ensures a material with a glTF base colour factor yields a generated material whose base-colour buffer holds that colour,
        /// sRGB-encoded because the engine's buffer is gamma-space (linear 0.5, 0.25, 0.75 becomes 188, 137, 225 in 8-bit sRGB).
        /// </summary>
        [Fact]
        public void BaseColorFactor_isWrittenToTheMaterialBuffer() {
            string sourcePath = CopyFixture();
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);

            manager.ImportModel(sourcePath);

            float4 color = ReadMaterialBaseColor(Path.Combine(AssetsRootPath, GeneratedFolder, "materials", "Paint.hasset"));
            Assert.Equal(188f / 255f, color.X.ToSingle(), 0.001f);
            Assert.Equal(137f / 255f, color.Y.ToSingle(), 0.001f);
            Assert.Equal(225f / 255f, color.Z.ToSingle(), 0.001f);
            Assert.Equal(1f, color.W.ToSingle(), 0.001f);
        }

        /// <summary>
        /// Ensures a material without a base colour factor yields a white base-colour buffer.
        /// </summary>
        [Fact]
        public void MissingBaseColorFactor_isWhite() {
            string sourcePath = CopyFixture("textured_quad.gltf");
            File.Copy(FixturePath("textured_quad.png"), Path.Combine(AssetsRootPath, "textured_quad.png"));
            AssetImportManager manager = CreateManager();
            SetMode(manager, sourcePath, ModelImportMode.Scene);

            manager.ImportModel(sourcePath);

            float4 color = ReadMaterialBaseColor(Path.Combine(AssetsRootPath, "textured_quad.gltf.generated", "materials", "Printed.hasset"));
            Assert.Equal(1f, color.X.ToSingle(), 0.001f);
            Assert.Equal(1f, color.Y.ToSingle(), 0.001f);
            Assert.Equal(1f, color.Z.ToSingle(), 0.001f);
            Assert.Equal(1f, color.W.ToSingle(), 0.001f);
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
        /// Reads the base-colour constant buffer of a generated material as the windows platform resolves it.
        /// </summary>
        /// <param name="materialPath">Absolute path of the generated material.</param>
        /// <returns>The colour stored in the buffer.</returns>
        float4 ReadMaterialBaseColor(string materialPath) {
            MaterialAssetSettingsService service = new MaterialAssetSettingsService(ProjectRootPath);
            ShaderMaterialAsset material = service.LoadMaterialAsset(materialPath, "windows");
            MaterialConstantBufferAsset buffer = Assert.Single(material.ConstantBuffers, candidate => candidate.Name == StandardMaterialBaseColorDefaults.BaseColorBufferName);
            return StandardMaterialBaseColorDefaults.ReadConstantBufferData(buffer.Data);
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
            return ReadAuthoringIds(GeneratedFolder, relativePaths);
        }

        /// <summary>
        /// Reads the authoring ids of files in one generated folder.
        /// </summary>
        /// <param name="generatedFolder">Assets-relative generated folder.</param>
        /// <param name="relativePaths">Paths relative to the generated folder.</param>
        /// <returns>Authoring ids in the same order.</returns>
        string[] ReadAuthoringIds(string generatedFolder, string[] relativePaths) {
            AssetIdentityMetadataService identity = new AssetIdentityMetadataService(ProjectRootPath);
            string[] ids = new string[relativePaths.Length];
            for (int index = 0; index < ids.Length; index++) {
                ids[index] = identity.Load(Path.Combine(AssetsRootPath, generatedFolder, relativePaths[index])).AssetId;
            }

            return ids;
        }

        /// <summary>
        /// Creates an output for an empty scene whose publisher writes nothing.
        /// </summary>
        /// <returns>Output to add files to.</returns>
        static ImportedSceneOutput CreateEmptyOutput() {
            ImportedSceneNode root = new ImportedSceneNode("Root", string.Empty, float3.Zero, float4.Identity, float3.One, Array.Empty<int>(), Array.Empty<ImportedSceneNode>());
            ImportedSceneData scene = new ImportedSceneData(root, Array.Empty<ImportedSceneMesh>(), Array.Empty<ImportedSceneMaterial>(), Array.Empty<ImportedSceneTexture>(), Array.Empty<ImportedSceneAnimation>(), string.Empty);
            return new ImportedSceneOutput(scene, "a.gltf.generated", "id", "a.hblueprint", (path, material) => new byte[0], extension => true, (file, kind) => null);
        }

        /// <summary>
        /// Reads every file of a folder tree.
        /// </summary>
        /// <param name="folderPath">Absolute folder path.</param>
        /// <returns>File bytes keyed by the path relative to the folder.</returns>
        static Dictionary<string, byte[]> SnapshotFolder(string folderPath) {
            Dictionary<string, byte[]> files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories)) {
                files.Add(Path.GetRelativePath(folderPath, path), File.ReadAllBytes(path));
            }

            return files;
        }

        /// <summary>
        /// Asserts two folder snapshots hold the same files with the same bytes.
        /// </summary>
        /// <param name="expected">Snapshot taken first.</param>
        /// <param name="actual">Snapshot taken later.</param>
        static void AssertSnapshotEqual(Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual) {
            Assert.Equal(expected.Keys.OrderBy(key => key, StringComparer.Ordinal), actual.Keys.OrderBy(key => key, StringComparer.Ordinal));
            foreach (KeyValuePair<string, byte[]> entry in expected) {
                Assert.True(entry.Value.SequenceEqual(actual[entry.Key]), "Generated file changed: " + entry.Key);
            }
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
            /// Gets or sets the output version, which tests change to simulate a contributor whose output changed.
            /// </summary>
            public int OutputVersion { get; set; } = 1;

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
            /// Gets the output version.
            /// </summary>
            public int OutputVersion => 1;

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
