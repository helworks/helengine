using System.Security.Cryptography;
using System.Text.Json;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;
using helengine.editor.tests.testing;

namespace helengine.editor.tests.managers.project {
    /// <summary>Verifies actual render variants, strict geometry contracts and CPU integrity through scene cooking.</summary>
    public sealed class EditorGpuModelReductionTests {
        /// <summary>Visible fixed test workspace independent of the production authored project.</summary>
        const string FixtureRoot = "C:/dev/helworks/builds/helengine-ps1/menu-20261007/scene-modules/gpu-model-reduction/fixtures";
        /// <summary>Existing platform builder used only to resolve the real PS1 asset cook contract.</summary>
        const string BuilderPath = "C:/dev/helworks/builds/helengine-ps1/menu-20261007/scene-modules/buildgraph/builder-artifacts/bin/helengine.ps1.builder/debug/helengine.ps1.builder.dll";

        /// <summary>Checks exact attribute retention, bounds, nonempty materials, deterministic output and impossible-target atomicity.</summary>
        [Fact]
        public void ReducerPreservesIndexedSurfaceContractsAndFailsAtomically() {
            ModelAsset source = GpuReductionGridImporter.CreateGrid();
            byte[] original = AssetSerializer.SerializeToBytes(source);
            ModelAsset reduced = (ModelAsset)AssetSerializer.DeserializeFromBytes(original);
            ModelAsset replay = (ModelAsset)AssetSerializer.DeserializeFromBytes(original);
            ModelGpuReductionSettings settings = new ModelGpuReductionSettings { Enabled = true };
            ModelGpuReductionProcessor.Apply(reduced, settings);
            ModelGpuReductionProcessor.Apply(replay, settings);
            Assert.InRange(reduced.Positions.Length, 3, 256);
            Assert.InRange(reduced.Indices16.Length, 3, 768);
            Assert.Equal(AssetSerializer.SerializeToBytes(reduced), AssetSerializer.SerializeToBytes(replay));
            Assert.Equal(original, AssetSerializer.SerializeToBytes(source));
            Assert.Equal(0f, reduced.Positions.Min(position => position.X));
            Assert.Equal(1f, reduced.Positions.Max(position => position.X));
            Assert.Equal(0f, reduced.Positions.Min(position => position.Y));
            Assert.Equal(1f, reduced.Positions.Max(position => position.Y));
            Assert.Equal("Surface", Assert.Single(reduced.Submeshes).MaterialSlotName);
            for (int index = 0; index < reduced.Positions.Length; index++) {
                int originalIndex = Array.IndexOf(source.Positions, reduced.Positions[index]);
                Assert.True(originalIndex >= 0);
                Assert.Equal(source.Normals[originalIndex], reduced.Normals[index]);
                Assert.Equal(source.TexCoords[originalIndex], reduced.TexCoords[index]);
            }
            ModelAsset cube = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);
            byte[] cubeBytes = AssetSerializer.SerializeToBytes(cube);
            Assert.Throws<InvalidOperationException>(() => ModelGpuReductionProcessor.Apply(cube, new ModelGpuReductionSettings { Enabled = true, MaximumVertices = 3, MaximumIndices = 3 }));
            Assert.Equal(cubeBytes, AssetSerializer.SerializeToBytes(cube));
        }

        /// <summary>Verifies the explicitly generated sphere profile fits hardware while the Core source primitive remains unchanged.</summary>
        [Fact]
        public void GpuSphereProfilePreservesCorePrimitiveAndUvSeam() {
            ModelAsset original = ModelUtils.GenerateSphereMesh(float3.Zero, float3.One);
            byte[] before = AssetSerializer.SerializeToBytes(original);
            ModelAsset gpu = ModelGpuSphereProcessor.Create(new ModelGpuReductionProfile { Reduction = new ModelGpuReductionSettings { Enabled = true } });
            Assert.Equal(91, gpu.Positions.Length);
            Assert.Equal(360, gpu.Indices16.Length);
            Assert.Equal(-0.5f, gpu.Positions.Min(position => position.X));
            Assert.Equal(0.5f, gpu.Positions.Max(position => position.X));
            Assert.Equal(-0.5f, gpu.Positions.Min(position => position.Y));
            Assert.Equal(0.5f, gpu.Positions.Max(position => position.Y));
            for (int ring = 0; ring <= 6; ring++) {
                Assert.Equal(0f, gpu.TexCoords[ring * 13].X);
                Assert.Equal(1f, gpu.TexCoords[ring * 13 + 12].X);
            }
            Assert.Equal(before, AssetSerializer.SerializeToBytes(original));
            Assert.Throws<InvalidOperationException>(() => ModelGpuSphereProcessor.Create(new ModelGpuReductionProfile { SphereRings = 7 }));
        }

        /// <summary>Reduces adjacent charts together without welding their distinct normals, UVs or material ranges.</summary>
        [Fact]
        public void PairedCollapsePreservesHardNormalUvAndMaterialSeams() {
            ModelAsset first = GpuReductionGridImporter.CreateGrid();
            ModelAsset second = GpuReductionGridImporter.CreateGrid();
            for (int vertex = 0; vertex < second.Positions.Length; vertex++) {
                float3 position = second.Positions[vertex];
                second.Positions[vertex] = new float3(0f, position.Y, position.X);
                second.Normals[vertex] = new float3(-1f, 0f, 0f);
                second.TexCoords[vertex] = new float2(second.TexCoords[vertex].X + 2f, second.TexCoords[vertex].Y);
            }
            int offset = first.Positions.Length;
            ModelAsset combined = new ModelAsset {
                Positions = first.Positions.Concat(second.Positions).ToArray(), Normals = first.Normals.Concat(second.Normals).ToArray(),
                TexCoords = first.TexCoords.Concat(second.TexCoords).ToArray(),
                Indices16 = first.Indices16.Concat(second.Indices16.Select(index => checked((ushort)(index + offset)))).ToArray(),
                Submeshes = [new ModelSubmeshAsset { MaterialSlotName = "Front", IndexStart = 0, IndexCount = first.Indices16.Length },
                    new ModelSubmeshAsset { MaterialSlotName = "Side", IndexStart = first.Indices16.Length, IndexCount = second.Indices16.Length }]
            };
            ModelGpuReductionProcessor.Apply(combined, new ModelGpuReductionSettings { Enabled = true });
            Assert.InRange(combined.Positions.Length, 3, 256);
            Assert.InRange(combined.Indices16.Length, 3, 768);
            Assert.Equal(new[] { "Front", "Side" }, combined.Submeshes.Select(submesh => submesh.MaterialSlotName));
            Assert.All(combined.Submeshes, submesh => Assert.True(submesh.IndexCount > 0));
            foreach (ModelSubmeshAsset submesh in combined.Submeshes) {
                foreach (ushort vertex in combined.Indices16.Skip(submesh.IndexStart).Take(submesh.IndexCount)) {
                    Assert.Equal(submesh.MaterialSlotName == "Front" ? new float3(0f, 0f, 1f) : new float3(-1f, 0f, 0f), combined.Normals[vertex]);
                    Assert.True(submesh.MaterialSlotName == "Front" ? combined.TexCoords[vertex].X <= 1f : combined.TexCoords[vertex].X >= 2f);
                }
            }
        }

        /// <summary>Exercises the five exact current Tilt GPU outliers without changing any staged payload.</summary>
        [Fact]
        public void ActualTiltVariantsMeetCapsWithoutChangingSourceBytes() {
            string root = "C:/dev/helworks/builds/helengine-ps1/menu-20261007/scene-modules";
            using JsonDocument audit = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "current-declared-memory-audit.json")));
            using JsonDocument declarations = JsonDocument.Parse(File.ReadAllBytes("C:/dev/helworks/builds/helengine-ps1/menu-20261007/style-final/staged-project/cache/build/v2/b2e9a8af1309e9acf12da508093ca468/b/ps1/Release/release/build-graph/builder/ps1-authored-declarations.json"));
            Dictionary<string, string> paths = declarations.RootElement.GetProperty("files").EnumerateArray().ToDictionary(entry => entry.GetProperty("relativePath").GetString(), entry => entry.GetProperty("sourcePath").GetString());
            int count = 0;
            foreach (JsonElement entry in audit.RootElement.GetProperty("models").EnumerateArray()) {
                string path = entry.GetProperty("path").GetString();
                if (!entry.GetProperty("overGpuCaps").GetBoolean() || !path.StartsWith("cooked/generated/models/tessellation/", StringComparison.Ordinal)) continue;
                byte[] source = File.ReadAllBytes(paths[path]);
                ModelAsset model = (ModelAsset)AssetSerializer.DeserializeFromBytes(source);
                string[] materials = model.Submeshes?.Select(submesh => submesh.MaterialSlotName).ToArray();
                ModelGpuReductionProcessor.Apply(model, new ModelGpuReductionSettings { Enabled = true });
                Assert.InRange(model.Positions.Length, 3, 256);
                Assert.InRange(model.Indices16.Length, 3, 768);
                Assert.Equal(materials, model.Submeshes?.Select(submesh => submesh.MaterialSlotName).ToArray());
                Assert.Equal(source, File.ReadAllBytes(paths[path]));
                count++;
            }
            Assert.Equal(5, count);
        }

        /// <summary>Keeps original M4A1 data when disabled and explicitly rejects a seam-safe target that cannot be reached.</summary>
        [Fact]
        public void ActualM4A1DisabledIsUnchangedAndEnabledImpossibleTargetRejects() {
            string declarationsPath = "C:/dev/helworks/builds/helengine-ps1/menu-20261007/style-final/staged-project/cache/build/v2/b2e9a8af1309e9acf12da508093ca468/b/ps1/Release/release/build-graph/builder/ps1-authored-declarations.json";
            using JsonDocument declarations = JsonDocument.Parse(File.ReadAllBytes(declarationsPath));
            JsonElement entry = declarations.RootElement.GetProperty("files").EnumerateArray().Single(item => item.GetProperty("relativePath").GetString() == "cooked/imported/models/games/zombislayer/weapons/m4a1.hasset");
            string path = entry.GetProperty("sourcePath").GetString();
            byte[] source = File.ReadAllBytes(path);
            ModelAsset model = (ModelAsset)AssetSerializer.DeserializeFromBytes(source);
            byte[] original = AssetSerializer.SerializeToBytes(model);
            ModelGpuReductionProcessor.Apply(model, new ModelGpuReductionSettings());
            Assert.Equal(original, AssetSerializer.SerializeToBytes(model));
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => ModelGpuReductionProcessor.Apply(model, new ModelGpuReductionSettings { Enabled = true }));
            Assert.Contains("cannot meet 256 vertices/768 indices", failure.Message);
            Assert.Equal(original, AssetSerializer.SerializeToBytes(model));
            Assert.Equal(source, File.ReadAllBytes(path));
        }

        /// <summary>Leaves a render cube at its existing packaged path when it already fits and no modifier changes geometry.</summary>
        [Fact]
        public void CubeWithinCapsDoesNotCreateRedundantGpuVariant() {
            string root = Path.Combine(FixtureRoot, "cube-within-caps");
            Directory.CreateDirectory(Path.Combine(root, "assets/Scenes"));
            SceneAssetReference cube = SceneAssetReferenceTestFactory.CreateEngineCubeModel();
            WriteScene(root, "Cube", cube, cube);
            WriteProfile(root, true);
            System.Reflection.Assembly assembly = System.Reflection.Assembly.LoadFrom(BuilderPath);
            PlatformDefinition platform = (PlatformDefinition)assembly.GetType("helengine.ps1.builder.PlayStation1PlatformDefinitionFactory", true).GetMethod("Create").Invoke(null, null);
            EditorPlatformAssetCookService service = new EditorPlatformAssetCookService(root, "1.0.0-engine", "gpu-cube", "1.0.0", [], PackagedFontAssetFactory.Create(), null, null, TestGeneratedAssetGraph.CreateShaderLibrary());
            PlatformBuildManifest manifest = service.Cook(platform, ["Cube"], Path.Combine(root, "output"), ["ps1"]);
            string[] dependencies = ReadDependencies(Assert.Single(manifest.Scenes));
            Assert.Contains("cooked/engine/models/cube.hasset", dependencies);
            Assert.DoesNotContain(dependencies, path => path.StartsWith("cooked/generated/models/", StringComparison.Ordinal));
        }

        /// <summary>Round-trips optional platform/environment settings and proves inherited clones are independent.</summary>
        [Fact]
        public void OptionalSectionRoundTripsEnvironmentOverrideWithoutChangingBaseModelSettings() {
            AssetImportSettings settings = new AssetImportSettings();
            AssetPlatformProcessorSettings platform = new AssetPlatformProcessorSettings();
            platform.Model.FlipWinding = true;
            platform.GpuModelReduction = new ModelGpuReductionSettings { Enabled = false, MaximumVertices = 192, MaximumIndices = 576 };
            platform.Environments.Add("retail", new AssetPlatformProcessorSettings { GpuModelReduction = new ModelGpuReductionSettings { Enabled = true, MaximumVertices = 128, MaximumIndices = 384 } });
            settings.Processor.Platforms.Add("ps1", platform);
            using MemoryStream stream = new MemoryStream();
            SectionedAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            stream.Position = 0;
            AssetImportSettings restored = SectionedAssetImportSettingsBinarySerializer.Deserialize(stream);
            ModelGpuReductionSettings effective = AssetProcessorSettingsScopeResolver.ResolveSection<ModelGpuReductionSettings>(restored.Processor, new EditorOverrideScope("ps1", "retail"), ModelGpuReductionSectionDefinition.SectionIdValue);
            Assert.True(effective.Enabled);
            Assert.Equal(128, effective.MaximumVertices);
            effective.MaximumVertices = 96;
            Assert.Equal(128, restored.Processor.Platforms["ps1"].Environments["retail"].GpuModelReduction.MaximumVertices);
            Assert.True(restored.Processor.Platforms["ps1"].Model.FlipWinding);
            Assert.False(new AssetPlatformProcessorSettings().GpuModelReduction.Enabled);
        }

        /// <summary>Round-trips the optional platform and environment GPU policy of a current model sidecar without
        /// changing its base settings.</summary>
        [Fact]
        public void CurrentModelSettingsRoundTripOptionalEnvironmentPolicy() {
            ModelAssetImportSettings settings = new ModelAssetImportSettings();
            settings.Importer.ImporterId = "gpu-grid";
            settings.Processor.Platforms["ps1"] = new ModelAssetProcessorSettings { FlipWinding = true, GpuReduction = new ModelGpuReductionSettings { Enabled = true } };
            settings.Processor.Platforms["n64"] = new ModelAssetProcessorSettings();
            settings.Processor.Environments["ps1"] = new Dictionary<string, ModelAssetProcessorSettings> {
                ["retail"] = new ModelAssetProcessorSettings { GpuReduction = new ModelGpuReductionSettings { Enabled = false, MaximumVertices = 96, MaximumIndices = 288 } }
            };
            using MemoryStream current = new MemoryStream();
            ModelAssetImportSettingsBinarySerializer.Serialize(current, settings);
            current.Position = 0;
            ModelAssetImportSettings restored = ModelAssetImportSettingsBinarySerializer.Deserialize(current);
            Assert.Equal("gpu-grid", restored.Importer.ImporterId);
            Assert.True(restored.Processor.Platforms["ps1"].FlipWinding);
            Assert.True(restored.Processor.Platforms["ps1"].GpuReduction.Enabled);
            Assert.Null(restored.Processor.Platforms["n64"].GpuReduction);
            Assert.False(restored.Processor.Environments["ps1"]["retail"].GpuReduction.Enabled);
            Assert.Equal(96, restored.Processor.Environments["ps1"]["retail"].GpuReduction.MaximumVertices);
        }

        /// <summary>Preserves new modifier controls in platform/environment metadata and rejects an invalid discrete profile.</summary>
        [Fact]
        public void ScopedMeshReduceControlsRoundTripAndValidateLimits() {
            EntityComponentSaveState state = new EntityComponentSaveState();
            MeshComponentModifierStackService service = new MeshComponentModifierStackService();
            service.SetStack(state, new EditorOverrideScope("ps1", "retail"), [new MeshComponentModifier(MeshComponentModifier.ReduceKind) { MaximumVertices = 192, MaximumIndices = 576, SphereRings = 4, SphereSectors = 8 }]);
            EntityComponentPlatformOverrideState scope = Assert.Single(state.EnumeratePlatformOverrides());
            Assert.True(scope.Scope.TryGetStepId(SceneOverrideScopeStepKind.BuildConfig, out string environment));
            Assert.Equal("retail", environment);
            Assert.Equal("192", scope.EnumerateMemberValues().Single(entry => entry.Key.EndsWith("MaximumVertices", StringComparison.Ordinal)).Value);
            Assert.Throws<InvalidOperationException>(() => service.SetStack(new EntityComponentSaveState(), "ps1", [new MeshComponentModifier(MeshComponentModifier.ReduceKind) { MaximumIndices = 10 }]));
        }

        /// <summary>Runs actual PS1 two-scene cooking with shared CPU/GPU models, deduplication and restoration into a fresh cache destination.</summary>
        [Fact]
        public void CookSeparatesGpuVariantsFromCpuCompanionsAndRestoresFreshCache() {
            string root = Path.Combine(FixtureRoot, "cpu-gpu-shared");
            Directory.CreateDirectory(Path.Combine(root, "assets/models"));
            Directory.CreateDirectory(Path.Combine(root, "assets/Scenes"));
            File.WriteAllText(Path.Combine(root, "assets/models/shared.obj"), "stable importer fixture");
            SetAssetGpuPolicy(root, null);
            SceneAssetReference generated = SceneAssetReferenceTestFactory.CreateEngineSphereModel();
            SceneAssetReference imported = SceneAssetReferenceTestFactory.CreateFileSystemModel("models/shared.obj");
            WriteScene(root, "First", generated, imported);
            WriteScene(root, "Second", generated, imported);
            System.Reflection.Assembly assembly = System.Reflection.Assembly.LoadFrom(BuilderPath);
            PlatformDefinition platform = (PlatformDefinition)assembly.GetType("helengine.ps1.builder.PlayStation1PlatformDefinitionFactory", true).GetMethod("Create").Invoke(null, null);
            EditorPlatformAssetCookService service = new EditorPlatformAssetCookService(root, "1.0.0-engine", "gpu-variants", "1.0.0",
                [new ModelImporterRegistration("gpu-grid", new GpuReductionGridImporter(), [".obj"])], PackagedFontAssetFactory.Create(), null, null, TestGeneratedAssetGraph.CreateShaderLibrary());
            WriteProfile(root, false);
            PlatformBuildManifest baseline = service.Cook(platform, ["First", "Second"], Path.Combine(root, "baseline"), ["ps1"]);
            string[] cpuPaths = ReadDependencies(baseline.Scenes[0]).Where(path => path.StartsWith("cooked/cpu-models/", StringComparison.Ordinal)).ToArray();
            Assert.Equal(2, cpuPaths.Length);
            Dictionary<string, byte[]> cpuBytes = cpuPaths.ToDictionary(path => path, path => File.ReadAllBytes(Path.Combine(root, "baseline", path)));
            string baseImportId = ResolveProcessedModelId(root);
            byte[] baseImportBytes = File.ReadAllBytes(Path.Combine(root, "cache", baseImportId));
            string sourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, "assets/models/shared.obj"))));
            WriteProfile(root, true);
            PlatformBuildManifest first = service.Cook(platform, ["First", "Second"], Path.Combine(root, "reduced"), ["ps1"]);
            PlatformBuildManifest replay = service.Cook(platform, ["First", "Second"], Path.Combine(root, "fresh-cache"), ["ps1"]);
            string[] variants = ReadDependencies(first.Scenes[0]).Where(path => path.StartsWith("cooked/generated/models/", StringComparison.Ordinal)).ToArray();
            Assert.Equal(2, variants.Length);
            foreach (PlatformBuildScene scene in first.Scenes) {
                Assert.All(cpuPaths.Concat(variants), path => Assert.Contains(path, ReadDependencies(scene)));
                Assert.DoesNotContain("cooked/engine/models/sphere.hasset", ReadDependencies(scene));
            }
            foreach (string path in cpuPaths) {
                Assert.Equal(cpuBytes[path], File.ReadAllBytes(Path.Combine(root, "reduced", path)));
                Assert.Equal(cpuBytes[path], File.ReadAllBytes(Path.Combine(root, "fresh-cache", path)));
            }
            foreach (string path in variants) {
                using FileStream modelStream = File.OpenRead(Path.Combine(root, "reduced", path));
                ModelAsset model = new AssetContentProcessor<ModelAsset>().Read(modelStream);
                Assert.InRange(model.Positions.Length, 3, 256);
                Assert.InRange(model.Indices16.Length, 3, 768);
                Assert.Equal(File.ReadAllBytes(Path.Combine(root, "reduced", path)), File.ReadAllBytes(Path.Combine(root, "fresh-cache", path)));
            }
            Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, "assets/models/shared.obj")))));
            Assert.Equal(first.Scenes.SelectMany(ReadDependencies).OrderBy(path => path), replay.Scenes.SelectMany(ReadDependencies).OrderBy(path => path));
            SetAssetGpuPolicy(root, new ModelGpuReductionSettings { Enabled = true, MaximumVertices = 96, MaximumIndices = 288 });
            PlatformBuildManifest changed = service.Cook(platform, ["First", "Second"], Path.Combine(root, "changed-gpu-policy"), ["ps1"]);
            Assert.Equal(baseImportId, ResolveProcessedModelId(root));
            Assert.Equal(baseImportBytes, File.ReadAllBytes(Path.Combine(root, "cache", baseImportId)));
            foreach (string path in cpuPaths) Assert.Equal(cpuBytes[path], File.ReadAllBytes(Path.Combine(root, "changed-gpu-policy", path)));
            string[] changedVariants = ReadDependencies(changed.Scenes[0]).Where(path => path.StartsWith("cooked/generated/models/", StringComparison.Ordinal)).ToArray();
            Assert.Single(variants.Intersect(changedVariants));
            Assert.Equal(2, changedVariants.Length);
        }

        /// <summary>
        /// Recomputes the processed id the ps1 cook gives the shared model from its source and sidecar; sidecars never
        /// persist processed ids.
        /// </summary>
        static string ResolveProcessedModelId(string root) {
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(root, "assets")));
            using AssetImportManager manager = new AssetImportManager(root, contentManager);
            manager.RegisterModelImporter(new ModelImporterRegistration("gpu-grid", new GpuReductionGridImporter(), [".obj"]));
            manager.CurrentPlatformId = "ps1";
            return manager.LoadOrCreateModelImportSettings(Path.Combine(root, "assets/models/shared.obj")).Importer.ProcessedAssetId;
        }

        /// <summary>Reads the authored importer sidecar of the shared fixture model.</summary>
        static ModelAssetImportSettings ReadModelSettings(string root) {
            using FileStream stream = File.OpenRead(Path.Combine(root, "assets/models/shared.obj.hasset"));
            return ModelAssetImportSettingsBinarySerializer.Deserialize(stream);
        }

        /// <summary>Changes only an optional GPU policy in the owned fixture sidecar.</summary>
        static void SetAssetGpuPolicy(string root, ModelGpuReductionSettings policy) {
            string path = Path.Combine(root, "assets/models/shared.obj.hasset");
            if (!File.Exists(path)) return;
            ModelAssetImportSettings settings = ReadModelSettings(root);
            if (!settings.Processor.Platforms.TryGetValue("ps1", out ModelAssetProcessorSettings platform)) {
                platform = new ModelAssetProcessorSettings();
                settings.Processor.Platforms.Add("ps1", platform);
            }
            platform.GpuReduction = policy;
            using FileStream stream = File.Create(path);
            ModelAssetImportSettingsBinarySerializer.Serialize(stream, settings);
        }

        /// <summary>Persists only builder-exposed optional settings in the isolated test project.</summary>
        static void WriteProfile(string root, bool enabled) {
            Directory.CreateDirectory(Path.Combine(root, "settings"));
            EditorProfileSettingsDocument document = new EditorProfileSettingsDocument();
            EditorPlatformProfileSettingsDocument platform = new EditorPlatformProfileSettingsDocument { PlatformId = "ps1" };
            platform.Build.SelectedOptionValues["gpu-model-reduction"] = enabled ? "true" : "false";
            document.Platforms.Add(platform);
            new EditorProfileSettingsService(root).Save(document);
        }

        /// <summary>Serializes two actual MeshComponents and CPU-readable references using the production schema.</summary>
        static void WriteScene(string root, string sceneId, SceneAssetReference generated, SceneAssetReference imported) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            List<SceneComponentAssetRecord> records = new List<SceneComponentAssetRecord>();
            foreach (SceneAssetReference reference in new[] { generated, imported }) {
                EntityComponentSaveState state = new EntityComponentSaveState();
                state.SetAssetReference(nameof(MeshComponent.Model), reference);
                records.Add(descriptor.SerializeComponent(new MeshComponent(), records.Count, state));
            }
            records.Add(descriptor.SerializeComponent(new SceneDependencyCpuModelComponent { GeneratedModel = generated, ImportedModel = imported }, records.Count, null));
            EntityComponentSaveState spawnerState = new EntityComponentSaveState();
            spawnerState.SetAssetReference(nameof(GpuReductionSpawnerComponent.GeneratedModel), generated);
            spawnerState.SetAssetReference(nameof(GpuReductionSpawnerComponent.ImportedModel), imported);
            records.Add(descriptor.SerializeComponent(new GpuReductionSpawnerComponent(), records.Count, spawnerState));
            string path = "Scenes/" + sceneId + ".helen";
            File.WriteAllBytes(Path.Combine(root, "assets", path), AssetSerializer.SerializeToBytes(new SceneAsset {
                Id = path, AuthoringAssetId = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path))).Substring(0, 32).ToLowerInvariant(), AssetReferences = [generated, imported],
                RootEntities = [new SceneEntityAsset { Id = 1, Name = "Shared render and CPU models", LocalScale = float3.One, LocalOrientation = float4.Identity, Components = records.ToArray(), Children = [] }]
            }));
        }

        /// <summary>Reads the exact per-scene dependency paths emitted by the actual cooker.</summary>
        static string[] ReadDependencies(PlatformBuildScene scene) { return JsonSerializer.Deserialize<string[]>(scene.ResolvedMetadata.Single(pair => pair.Key == "cooked-dependency-paths").Value); }
    }
}
