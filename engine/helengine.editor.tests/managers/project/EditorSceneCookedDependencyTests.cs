using helengine.baseplatform.Manifest;
using helengine.baseplatform.Definitions;
using helengine.editor.tests.testing;

namespace helengine.editor.tests.managers.project {
    /// <summary>Exercises actual scene cooking with shared dependencies rather than reconstructing manifest selections in a test double.</summary>
    public sealed class EditorSceneCookedDependencyTests {
        /// <summary>Visible fixed fixture root, kept outside temporary folders and production authored projects.</summary>
        const string FixtureRoot = "C:/dev/helworks/builds/helengine-ps1/menu-20261007/scene-modules/scene-dependency-tests/fixtures";

        /// <summary>Actual PS1 builder compiled for the current scene-module graph, used without starting its native build.</summary>
        const string Ps1BuilderPath = "C:/dev/helworks/builds/helengine-ps1/menu-20261007/scene-modules/buildgraph/builder-artifacts/bin/helengine.ps1.builder/debug/helengine.ps1.builder.dll";

        /// <summary>Actual default-font payload and generated atlas retained from the failed full-scene cook.</summary>
        const string DefaultFontInputRoot = "C:/dev/helworks/builds/helengine-ps1/menu-20261007/scene-modules/scene-dependency-tests/default-font-input";

        /// <summary>Requires each scene to retain both direct CPU companion dependencies after shared output deduplication and restoration into a fresh cache destination.</summary>
        [Fact]
        public void SharedDirectCpuModelReferencesRemainInEachSceneClosureAfterFreshCacheRestore() {
            string projectRoot = PrepareFixture("ps1-shared-cpu-models");
            string buildRoot = Path.Combine(projectRoot, "output");
            SceneAssetReference generated = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateEngineCubeModel();
            SceneAssetReference imported = Reference("models/shared.obj");
            WriteSource(projectRoot, imported.RelativePath);
            byte[] sourceBytes = File.ReadAllBytes(Path.Combine(projectRoot, "assets", imported.RelativePath));
            AutomaticScriptComponentPersistenceDescriptor descriptor = new(new ScriptComponentReflectionSchemaBuilder());
            foreach (string scenePath in new[] { "Scenes/First.helen", "Scenes/Second.helen" }) {
                SceneComponentAssetRecord record = descriptor.SerializeComponent(new SceneDependencyCpuModelComponent {
                    GeneratedModel = generated, ImportedModel = imported
                }, 0, null);
                string path = Path.Combine(projectRoot, "assets", scenePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, AssetSerializer.SerializeToBytes(new SceneAsset {
                    Id = scenePath, AuthoringAssetId = Identity(scenePath), AssetReferences = [generated, imported],
                    RootEntities = [new SceneEntityAsset {
                        Id = 1, Name = "CPU model owner", LocalScale = float3.One, LocalOrientation = float4.Identity,
                        Components = [record], Children = []
                    }]
                }));
            }
            System.Reflection.Assembly assembly = System.Reflection.Assembly.LoadFrom(Ps1BuilderPath);
            Type factory = assembly.GetType("helengine.ps1.builder.PlayStation1PlatformDefinitionFactory", true);
            PlatformDefinition platform = (PlatformDefinition)factory.GetMethod("Create").Invoke(null, null);
            EditorPlatformAssetCookService service = new(projectRoot, "1.0.0-engine", "dependencies", "1.0.0",
                [new ModelImporterRegistration("test-model", new TestModelImporter(), [".obj"])],
                PackagedFontAssetFactory.Create(), null, null, TestGeneratedAssetGraph.CreateShaderLibrary());
            PlatformBuildManifest first = service.Cook(platform, ["First", "Second"], buildRoot, ["ps1"]);
            string stabilizedRoot = Path.Combine(projectRoot, "output-stabilized");
            PlatformBuildManifest stabilized = service.Cook(platform, ["First", "Second"], stabilizedRoot, ["ps1"]);
            string replayRoot = Path.Combine(projectRoot, "output-cache-hit");
            PlatformBuildManifest replay = service.Cook(platform, ["First", "Second"], replayRoot, ["ps1"]);
            string[] sharedModelPaths = ReadDependencies(first.Scenes[0]).Where(path => path.StartsWith("cooked/cpu-models/", StringComparison.Ordinal)).ToArray();
            Assert.Equal(2, sharedModelPaths.Length);
            Assert.Contains("cooked/cpu-models/engine/cube.hasset", sharedModelPaths);
            string importedPath = Assert.Single(sharedModelPaths, path => path.StartsWith("cooked/cpu-models/filesystem/", StringComparison.Ordinal));
            Assert.Equal(sourceBytes, File.ReadAllBytes(Path.Combine(projectRoot, "assets", imported.RelativePath)));
            for (int index = 0; index < first.Scenes.Length; index++) {
                string[] dependencies = ReadDependencies(first.Scenes[index]);
                Assert.Equal(sharedModelPaths.Append(first.Scenes[index].SourceIdentity).OrderBy(path => path), dependencies.OrderBy(path => path));
                Assert.Equal(dependencies, ReadDependencies(stabilized.Scenes[index]));
                Assert.Equal(dependencies, ReadDependencies(replay.Scenes[index]));
                AssertScenePayloadContract(replay.Scenes[index], dependencies, replayRoot);
                using FileStream stream = File.OpenRead(Path.Combine(replayRoot, replay.Scenes[index].SourceIdentity));
                SceneAsset cooked = PackagedAssetBinarySerializer.DeserializeSceneAsset(stream);
                Assert.Equal(sharedModelPaths.OrderBy(path => path), cooked.AssetReferences.Select(reference => reference.RelativePath).OrderBy(path => path));
                SceneComponentAssetRecord record = Assert.Single(Assert.Single(cooked.RootEntities).Components);
                using MemoryStream payload = new(record.Payload, false);
                using EngineBinaryReader reader = EngineBinaryReader.Create(payload, EngineBinaryEndianness.LittleEndian);
                Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, reader.ReadByte());
                Assert.Equal(2, reader.ReadInt32());
                SceneAssetReference cubeReference = global::helengine.SceneAssetReferenceFactory.ReadOptionalReference(reader);
                SceneAssetReference importedReference = global::helengine.SceneAssetReferenceFactory.ReadOptionalReference(reader);
                Assert.Equal("cooked/cpu-models/engine/cube.hasset", cubeReference.RelativePath);
                Assert.Equal(importedPath, importedReference.RelativePath);
                Assert.Equal(payload.Length, payload.Position);
                foreach (string dependency in dependencies) {
                    Assert.Equal(File.ReadAllBytes(Path.Combine(buildRoot, dependency)), File.ReadAllBytes(Path.Combine(replayRoot, dependency)));
                    Assert.Equal(File.ReadAllBytes(Path.Combine(buildRoot, dependency)), File.ReadAllBytes(Path.Combine(stabilizedRoot, dependency)));
                }
            }
            using FileStream modelStream = File.OpenRead(Path.Combine(replayRoot, importedPath));
            ModelAsset model = new AssetContentProcessor<ModelAsset>().Read(modelStream);
            Assert.Equal(new[] { float3.Zero, new float3(1f, 0f, 0f), new float3(0f, 1f, 0f) }, model.Positions);
            Assert.Equal(new ushort[] { 0, 1, 2 }, model.Indices16);
            Assert.Equal(new[] { new float2(0f, 0f), new float2(1f, 0f), new float2(0f, 1f) }, model.TexCoords);
        }

        /// <summary>Requires exact scene selection to stage an effective keyboard icon even though its generated source folder is not PS1, and to exclude an existing unused console icon.</summary>
        [Fact]
        public void Ps1SelectedKeyboardTextureStagesWhileUnusedConsoleOutputDoesNot() {
            string projectRoot = PrepareFixture("ps1-selected-keyboard");
            string buildRoot = Path.Combine(projectRoot, "output");
            SceneAssetReference keyboard = Reference("textures/instructions/controls/generated/keyboard/enter.png");
            SceneAssetReference unused = Reference("textures/instructions/controls/generated/ps2/circle.png");
            int red = 32;
            foreach (SceneAssetReference reference in new[] { keyboard, unused }) {
                WriteSource(projectRoot, reference.RelativePath);
                using System.Drawing.Bitmap bitmap = new(1, 1);
                bitmap.SetPixel(0, 0, System.Drawing.Color.FromArgb(255, red, 64, 128));
                red += 64;
                bitmap.Save(Path.Combine(projectRoot, "assets", reference.RelativePath), System.Drawing.Imaging.ImageFormat.Png);
            }
            WriteScene(projectRoot, "Scenes/Keyboard.helen", [keyboard]);
            WriteScene(projectRoot, "Scenes/Unused.helen", [unused]);
            System.Reflection.Assembly assembly = System.Reflection.Assembly.LoadFrom(Ps1BuilderPath);
            Type factory = assembly.GetType("helengine.ps1.builder.PlayStation1PlatformDefinitionFactory", true);
            PlatformDefinition platform = (PlatformDefinition)factory.GetMethod("Create").Invoke(null, null);
            EditorPlatformAssetCookService service = new(projectRoot, "1.0.0-engine", "dependencies", "1.0.0",
                [new TextureImporterRegistration("test-texture", new SceneDependencyTextureImporter(), [".png"])],
                PackagedFontAssetFactory.Create(), null, null, TestGeneratedAssetGraph.CreateShaderLibrary());
            PlatformBuildManifest active = service.Cook(platform, ["Keyboard"], buildRoot, ["ps1"]);
            string unusedRoot = Path.Combine(projectRoot, "unused-output");
            PlatformBuildManifest extra = service.Cook(platform, ["Unused"], unusedRoot, ["ps1"]);
            PlatformCookWorkItem selectedItem = Assert.Single(active.PlatformCookWorkItems);
            PlatformCookWorkItem unusedItem = Assert.Single(extra.PlatformCookWorkItems);
            Assert.NotEqual(selectedItem.OutputRelativePath, unusedItem.OutputRelativePath);
            Type executorType = assembly.GetType("helengine.ps1.builder.PlayStation1PlatformCookWorkItemExecutor", true);
            object executor = Activator.CreateInstance(executorType);
            executorType.GetMethod("Execute").Invoke(executor, [selectedItem, buildRoot]);
            executorType.GetMethod("Execute").Invoke(executor, [unusedItem, unusedRoot]);
            string unusedOutput = Path.Combine(buildRoot, unusedItem.OutputRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(unusedOutput));
            File.Copy(Path.Combine(unusedRoot, unusedItem.OutputRelativePath), unusedOutput);
            string[] dependencies = ReadDependencies(Assert.Single(active.Scenes));
            Assert.Contains(selectedItem.OutputRelativePath, dependencies);
            Assert.DoesNotContain(unusedItem.OutputRelativePath, dependencies);
            string stageRoot = Path.Combine(projectRoot, "staged");
            Type stagerType = assembly.GetType("helengine.ps1.builder.PlayStation1ContentStager", true);
            System.Collections.IEnumerable staged = (System.Collections.IEnumerable)stagerType.GetMethod("StageSelected").Invoke(
                Activator.CreateInstance(stagerType), [active.CookedArtifacts, new[] { selectedItem, unusedItem }, buildRoot,
                    stageRoot, CancellationToken.None, dependencies]);
            List<string> stagedPaths = new();
            foreach (object asset in staged) { stagedPaths.Add((string)asset.GetType().GetProperty("RelativePath").GetValue(asset)); }
            Assert.Equal(active.CookedArtifacts.Select(artifact => artifact.RelativePath).Append(selectedItem.OutputRelativePath)
                .Distinct(StringComparer.Ordinal).OrderBy(path => path), stagedPaths.OrderBy(path => path));
            foreach (string dependency in dependencies) { Assert.Contains(dependency, stagedPaths); }
            Assert.True(File.Exists(unusedOutput));
            Assert.False(File.Exists(Path.Combine(stageRoot, unusedItem.OutputRelativePath)));
            string selectedOutput = Path.Combine(stageRoot, selectedItem.OutputRelativePath);
            Assert.Equal(File.ReadAllBytes(Path.Combine(buildRoot, selectedItem.OutputRelativePath)), File.ReadAllBytes(selectedOutput));
            using FileStream stream = File.OpenRead(selectedOutput);
            TextureAsset texture = new AssetContentProcessor<TextureAsset>().Read(stream);
            Assert.Equal((ushort)1, texture.Width);
            Assert.Equal((ushort)1, texture.Height);
            Assert.NotEmpty(texture.Colors);
        }

        /// <summary>Requires scene preload paths to follow the effective PS1 sprite references while retaining a common texture used by another sprite and an explicit table-only asset.</summary>
        [Fact]
        public void Ps1TextureDependenciesFollowEffectiveOverridesAndRestoreFreshCache() {
            string projectRoot = PrepareFixture("ps1-texture-overrides");
            string buildRoot = Path.Combine(projectRoot, "output");
            SceneAssetReference common = Reference("textures/common.png");
            SceneAssetReference selected = Reference("textures/ps1.png");
            SceneAssetReference inactive = Reference("textures/wii.png");
            SceneAssetReference tableOnly = Reference("textures/table-only.png");
            int sourceByte = 1;
            foreach (SceneAssetReference reference in new[] { common, selected, inactive, tableOnly }) {
                WriteSource(projectRoot, reference.RelativePath);
                using System.Drawing.Bitmap bitmap = new(1, 1);
                bitmap.SetPixel(0, 0, System.Drawing.Color.FromArgb(255, sourceByte++ * 32, 64, 128));
                bitmap.Save(Path.Combine(projectRoot, "assets", reference.RelativePath), System.Drawing.Imaging.ImageFormat.Png);
            }
            WriteScopedTextureScene(projectRoot, "Scenes/First.helen", common, selected, inactive, tableOnly);
            WriteScopedTextureScene(projectRoot, "Scenes/Second.helen", common, selected, inactive, tableOnly);
            System.Reflection.Assembly assembly = System.Reflection.Assembly.LoadFrom(Ps1BuilderPath);
            Type factory = assembly.GetType("helengine.ps1.builder.PlayStation1PlatformDefinitionFactory", true);
            PlatformDefinition platform = (PlatformDefinition)factory.GetMethod("Create").Invoke(null, null);
            EditorPlatformAssetCookService service = new(projectRoot, "1.0.0-engine", "dependencies", "1.0.0",
                [new TextureImporterRegistration("test-texture", new SceneDependencyTextureImporter(), [".png"])],
                PackagedFontAssetFactory.Create(), null, null, TestGeneratedAssetGraph.CreateShaderLibrary());
            PlatformBuildManifest manifest = service.Cook(platform, ["First", "Second"], buildRoot, ["ps1"]);
            string replayRoot = Path.Combine(projectRoot, "output-replay");
            PlatformBuildManifest replay = service.Cook(platform, ["First", "Second"], replayRoot, ["ps1"]);
            string cacheHitRoot = Path.Combine(projectRoot, "output-cache-hit");
            PlatformBuildManifest cached = service.Cook(platform, ["First", "Second"], cacheHitRoot, ["ps1"]);
            AssertPs1TextureOutputs(manifest, buildRoot, assembly);
            AssertPs1TextureOutputs(replay, replayRoot, assembly);
            AssertPs1TextureOutputs(cached, cacheHitRoot, assembly);
            for (int index = 0; index < manifest.Scenes.Length; index++) {
                Assert.Equal(ReadDependencies(manifest.Scenes[index]), ReadDependencies(replay.Scenes[index]));
                Assert.Equal(ReadDependencies(manifest.Scenes[index]), ReadDependencies(cached.Scenes[index]));
                foreach (string path in ReadDependencies(manifest.Scenes[index])) {
                    Assert.Equal(File.ReadAllBytes(Path.Combine(buildRoot, path)), File.ReadAllBytes(Path.Combine(replayRoot, path)));
                    Assert.Equal(File.ReadAllBytes(Path.Combine(buildRoot, path)), File.ReadAllBytes(Path.Combine(cacheHitRoot, path)));
                }
            }
        }

        /// <summary>Requires mesh processing to replace the source model reference in the scene table and preload metadata with its actual cooked tessellation variant.</summary>
        [Fact]
        public void Ps1TessellatedModelDependenciesSelectFinalVariantAndRestoreFreshCache() {
            string projectRoot = PrepareFixture("ps1-tessellated-model");
            string buildRoot = Path.Combine(projectRoot, "output");
            SceneAssetReference modelReference = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateEngineCubeModel();
            AutomaticScriptComponentPersistenceDescriptor descriptor = new(new ScriptComponentReflectionSchemaBuilder());
            EntityComponentSaveState state = new();
            state.SetAssetReference(nameof(MeshComponent.Model), modelReference);
            SceneComponentAssetRecord record = descriptor.SerializeComponent(new MeshComponent { Model = new TestRuntimeModel() }, 0, state);
            new MeshComponentModifierStackService().SetStack(state, "ps1", [new MeshComponentModifier(MeshComponentModifier.TessellateKind) {
                MaxEdgeLength = 0.75, AtCookTime = true
            }]);
            state.GetOrCreatePlatformOverride("ps1").Payload = record.Payload;
            SceneComponentAssetRecord wrapped = new ComponentPlatformOverridePayloadService().Wrap(record, state);
            string sourcePath = Path.Combine(projectRoot, "assets/Scenes/First.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
            using (FileStream stream = File.Create(sourcePath)) {
                AssetSerializer.Serialize(stream, new SceneAsset {
                    Id = "Scenes/First.helen", AuthoringAssetId = Identity("Scenes/First.helen"), AssetReferences = [modelReference],
                    RootEntities = [new SceneEntityAsset {
                        Id = 1, Name = "Processed mesh", LocalScale = float3.One, LocalOrientation = float4.Identity,
                        Components = [wrapped], Children = []
                    }]
                });
            }
            System.Reflection.Assembly assembly = System.Reflection.Assembly.LoadFrom(Ps1BuilderPath);
            Type factory = assembly.GetType("helengine.ps1.builder.PlayStation1PlatformDefinitionFactory", true);
            PlatformDefinition platform = (PlatformDefinition)factory.GetMethod("Create").Invoke(null, null);
            EditorPlatformAssetCookService service = new(projectRoot, "1.0.0-engine", "dependencies", "1.0.0", [],
                PackagedFontAssetFactory.Create(), null, null, TestGeneratedAssetGraph.CreateShaderLibrary());
            PlatformBuildManifest manifest = service.Cook(platform, ["First"], buildRoot, ["ps1"]);
            string stabilizationRoot = Path.Combine(projectRoot, "output-stabilized");
            PlatformBuildManifest stabilized = service.Cook(platform, ["First"], stabilizationRoot, ["ps1"]);
            string replayRoot = Path.Combine(projectRoot, "output-replay");
            PlatformBuildManifest replay = service.Cook(platform, ["First"], replayRoot, ["ps1"]);
            PlatformBuildScene scene = Assert.Single(manifest.Scenes);
            string[] dependencies = ReadDependencies(scene);
            string variantPath = Assert.Single(dependencies, path => path.StartsWith("cooked/generated/models/tessellation/", StringComparison.Ordinal));
            Assert.Equal(new[] { variantPath, scene.SourceIdentity }.OrderBy(path => path), dependencies.OrderBy(path => path));
            Assert.DoesNotContain("cooked/engine/models/cube.hasset", dependencies);
            Assert.DoesNotContain(modelReference.RelativePath, dependencies);
            Assert.Equal(dependencies, ReadDependencies(Assert.Single(stabilized.Scenes)));
            Assert.Equal(dependencies, ReadDependencies(Assert.Single(replay.Scenes)));
            AssertScenePayloadContract(scene, dependencies, buildRoot);
            AssertScenePayloadContract(Assert.Single(replay.Scenes), dependencies, replayRoot);
            using (FileStream stream = File.OpenRead(Path.Combine(replayRoot, variantPath))) {
                ModelAsset model = new AssetContentProcessor<ModelAsset>().Read(stream);
                Assert.True(model.Positions.Length > 24);
                Assert.NotEmpty(model.Indices16);
            }
            using (FileStream stream = File.OpenRead(Path.Combine(replayRoot, scene.SourceIdentity))) {
                SceneAsset cooked = PackagedAssetBinarySerializer.DeserializeSceneAsset(stream);
                Assert.Equal(variantPath, Assert.Single(cooked.AssetReferences).RelativePath);
                SceneComponentAssetRecord cookedRecord = Assert.Single(Assert.Single(cooked.RootEntities).Components);
                ModelDependencyAssetValueReader valueReader = new(variantPath);
                ScenePersistenceValueWalker walker = new(valueReader, "Processed model dependency test");
                MeshComponent mesh = new();
                IReadOnlyList<System.Reflection.MemberInfo> members = ScenePersistenceValueWalker.GetSerializableMembers(typeof(MeshComponent));
                using MemoryStream payload = new(cookedRecord.Payload, false);
                using EngineBinaryReader reader = EngineBinaryReader.Create(payload, EngineBinaryEndianness.LittleEndian);
                Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, reader.ReadByte());
                Assert.Equal(members.Count, reader.ReadInt32());
                foreach (System.Reflection.MemberInfo member in members) {
                    ScenePersistenceValueWalker.SetMemberValue(member, mesh, walker.ReadValue(reader, ScenePersistenceValueWalker.GetMemberValueType(member)));
                }
                Assert.Equal(payload.Length, payload.Position);
                Assert.NotNull(mesh.Model);
                Assert.Equal(1, valueReader.ReadCount);
            }
            foreach (string path in dependencies) {
                Assert.Equal(File.ReadAllBytes(Path.Combine(buildRoot, path)), File.ReadAllBytes(Path.Combine(replayRoot, path)));
                Assert.Equal(File.ReadAllBytes(Path.Combine(buildRoot, path)), File.ReadAllBytes(Path.Combine(stabilizationRoot, path)));
            }
        }

        /// <summary>Requires actual typed animation rewriting to select only its runtime payload in multiple scenes and a fresh-output cache restore.</summary>
        [Fact]
        public void Ps1AnimationDependenciesSelectRewrittenPayloadAndRestoreFreshCache() {
            string projectRoot = PrepareFixture("ps1-animation");
            string buildRoot = Path.Combine(projectRoot, "output");
            const string authoredPath = "animations/demodisclogoidle.hanim";
            const string cookedPath = "cooked/animations/demodisclogoidle.hanim";
            AnimationClipAsset clip = new() {
                Id = authoredPath, AuthoringAssetId = Identity(authoredPath), Duration = 0.75f,
                PositionTracks = [new PositionKeyframeTrackAsset {
                    Keyframes = [new PositionKeyframeAsset(0f, float3.Zero, AnimationInterpolationMode.Linear) { FrameId = "start" },
                        new PositionKeyframeAsset(0.75f, new float3(1f, 2f, 3f), AnimationInterpolationMode.Linear) { FrameId = "end" }]
                }],
                PlatformOverrides = [new AnimationClipPlatformOverrideAsset {
                    PlatformId = "ps1", Mode = AnimationClipPlatformOverrideMode.OverrideFrames,
                    PositionTracks = [new PlatformPositionKeyframeTrackAsset {
                        Keyframes = [new PositionKeyframeAsset(0.75f, new float3(9f, 8f, 7f), AnimationInterpolationMode.Linear) { FrameId = "end" }]
                    }]
                }]
            };
            string sourcePath = Path.Combine(projectRoot, "assets", authoredPath);
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
            File.WriteAllBytes(sourcePath, AssetSerializer.SerializeToBytes(clip));
            SceneAssetReference reference = Reference(authoredPath);
            WriteAnimationScene(projectRoot, "Scenes/First.helen", clip, reference);
            WriteAnimationScene(projectRoot, "Scenes/Second.helen", clip, reference);
            WriteScene(projectRoot, "Scenes/Empty.helen", []);
            System.Reflection.Assembly assembly = System.Reflection.Assembly.LoadFrom(Ps1BuilderPath);
            Type factory = assembly.GetType("helengine.ps1.builder.PlayStation1PlatformDefinitionFactory", true);
            PlatformDefinition platform = (PlatformDefinition)factory.GetMethod("Create").Invoke(null, null);
            EditorPlatformAssetCookService service = new(projectRoot, "1.0.0-engine", "dependencies", "1.0.0", [],
                PackagedFontAssetFactory.Create(), null, null, TestGeneratedAssetGraph.CreateShaderLibrary());
            PlatformBuildManifest manifest = service.Cook(platform, ["First", "Second", "Empty"], buildRoot, ["ps1"]);
            string replayRoot = Path.Combine(projectRoot, "output-replay");
            PlatformBuildManifest replay = service.Cook(platform, ["First", "Second", "Empty"], replayRoot, ["ps1"]);
            for (int index = 0; index < manifest.Scenes.Length; index++) {
                PlatformBuildScene scene = manifest.Scenes[index];
                string[] dependencies = ReadDependencies(scene);
                Assert.Equal(dependencies, ReadDependencies(replay.Scenes[index]));
                Assert.DoesNotContain(authoredPath, dependencies);
                Assert.DoesNotContain(dependencies, path => path.StartsWith("generated/", StringComparison.Ordinal));
                AssertScenePayloadContract(scene, dependencies, buildRoot);
                AssertScenePayloadContract(replay.Scenes[index], dependencies, replayRoot);
                if (scene.SceneId == "Empty") {
                    Assert.Equal([scene.SourceIdentity], dependencies);
                } else {
                    Assert.Contains(cookedPath, dependencies);
                    using FileStream sceneStream = File.OpenRead(Path.Combine(replayRoot, scene.SourceIdentity));
                    SceneAsset cookedScene = PackagedAssetBinarySerializer.DeserializeSceneAsset(sceneStream);
                    SceneAssetReference cookedReference = Assert.Single(cookedScene.AssetReferences);
                    Assert.Equal(cookedPath, cookedReference.RelativePath);
                    SceneComponentAssetRecord record = Assert.Single(Assert.Single(cookedScene.RootEntities).Components);
                    AnimationPlayerComponent player = ReadPackagedAnimationPlayer(record, cookedReference.RelativePath, clip);
                    Assert.Same(clip, player.Clip);
                    Assert.True(player.PlayAutomatically);
                    Assert.True(player.ShouldLoop);
                }
                foreach (string path in dependencies) {
                    Assert.Equal(File.ReadAllBytes(Path.Combine(buildRoot, path)), File.ReadAllBytes(Path.Combine(replayRoot, path)));
                }
            }
            PlatformBuildArtifact artifact = Assert.Single(manifest.CookedArtifacts, value => value.RelativePath == cookedPath);
            PlatformBuildArtifact restored = Assert.Single(replay.CookedArtifacts, value => value.RelativePath == cookedPath);
            Assert.Equal(artifact.ContentHash, restored.ContentHash);
            Assert.DoesNotContain(manifest.CookedArtifacts, value => value.RelativePath == authoredPath);
            using FileStream clipStream = File.OpenRead(Path.Combine(replayRoot, cookedPath));
            AnimationClipAsset restoredClip = new AssetContentProcessor<AnimationClipAsset>().Read(clipStream);
            Assert.Equal(0.75f, restoredClip.Duration);
            PositionKeyframeTrackAsset track = Assert.Single(restoredClip.PositionTracks);
            Assert.Equal(new float3(9f, 8f, 7f), track.Keyframes[1].Value);
            Assert.Empty(restoredClip.PlatformOverrides);
            Assert.Equal(AssetSerializer.SerializeToBytes(clip), File.ReadAllBytes(sourcePath));
        }

        /// <summary>Ensures the second scene retains shared model, material texture, font atlas and audio outputs after global deduplication in either build order.</summary>
        /// <param name="reverseOrder">Reverses scene selection to catch first-scene-only dependency tracking.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SharedDependenciesSurviveGlobalDedupAndDoNotLeakBetweenScenes(bool reverseOrder) {
            string projectRoot = PrepareFixture(reverseOrder ? "reverse" : "forward");
            string buildRoot = Path.Combine(projectRoot, "output");
            WriteSource(projectRoot, "models/shared.obj");
            WriteSource(projectRoot, "audio/shared.wav");
            WriteSource(projectRoot, "textures/unique.png");
            WriteFont(projectRoot);
            WriteMaterialAndTexture(projectRoot);
            SceneAssetReference[] shared = [
                Reference("models/shared.obj"), Reference("materials/shared.hasset"),
                Reference("fonts/shared.hefont"), Reference("audio/shared.wav")
            ];
            WriteScene(projectRoot, "Scenes/First.helen", shared.Concat([Reference("textures/unique.png")]).ToArray());
            WriteScene(projectRoot, "Scenes/Second.helen", shared);
            WriteScene(projectRoot, "Scenes/Empty.helen", []);

            TestPlatformMaterialAssetBuilder builder = new();
            EditorPlatformAssetCookService service = new(projectRoot, "1.0.0-engine", "dependencies", "1.0.0",
                [
                    new ModelImporterRegistration("test-model", new TestModelImporter(), [".obj"]),
                    new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]),
                    new AudioImporterRegistration("test-audio", new EditorSceneDependencyAudioImporter(), [".wav"])
                ], PackagedFontAssetFactory.Create(), null, null, TestGeneratedAssetGraph.CreateShaderLibrary());
            string[] ordered = reverseOrder ? ["Second", "First", "Empty"] : ["First", "Second", "Empty"];
            PlatformBuildManifest manifest = service.Cook(builder.Definition, ordered, buildRoot, ["windows"], builder);

            PlatformBuildScene first = Assert.Single(manifest.Scenes, scene => scene.SceneId == "First");
            PlatformBuildScene second = Assert.Single(manifest.Scenes, scene => scene.SceneId == "Second");
            PlatformBuildScene empty = Assert.Single(manifest.Scenes, scene => scene.SceneId == "Empty");
            string[] firstPaths = ReadDependencies(first);
            string[] secondPaths = ReadDependencies(second);
            AssertScenePayloadContract(first, firstPaths, buildRoot);
            AssertScenePayloadContract(second, secondPaths, buildRoot);
            AssertScenePayloadContract(empty, ReadDependencies(empty), buildRoot);

            Assert.Contains("cooked/imported/models/shared.hasset", secondPaths);
            Assert.Contains("cooked/materials/shared.hasset", secondPaths);
            Assert.Contains("cooked/audio/shared.hasset", secondPaths);
            Assert.Contains("cooked/fonts/shared.hefont", secondPaths);
            Assert.Contains("cooked/imported/material-diffuse", secondPaths);
            using (FileStream fontStream = File.OpenRead(Path.Combine(buildRoot, "cooked/fonts/shared.hefont"))) {
                FontAsset cookedFont = FontAssetBinarySerializer.Deserialize(fontStream);
                Assert.NotNull(cookedFont.SourceTextureAsset);
                FontAsset authoredFont = PackagedFontAssetFactory.Create();
                Assert.Equal(authoredFont.SourceTextureAsset.Colors, cookedFont.SourceTextureAsset.Colors);
                Assert.Equal(authoredFont.Characters['A'], cookedFont.Characters['A']);
                Assert.Equal(authoredFont.LineHeight, cookedFont.LineHeight);
                Assert.Equal(authoredFont.AtlasWidth, cookedFont.AtlasWidth);
                Assert.Equal(authoredFont.AtlasHeight, cookedFont.AtlasHeight);
                Assert.Equal(authoredFont.MeasureString("A A"), cookedFont.MeasureString("A A"));
                fontStream.Position = 0;
                FontAsset playerFont = new FontAssetBinaryContentProcessor(new TestRenderManager2D()).Read(fontStream);
                Assert.NotNull(playerFont.Texture);
                Assert.Equal(authoredFont.AtlasWidth, playerFont.Texture.Width);
                Assert.Equal(authoredFont.AtlasHeight, playerFont.Texture.Height);
                Assert.Equal(authoredFont.Characters['A'], playerFont.Characters['A']);
                Assert.Equal(authoredFont.MeasureString("A A"), playerFont.MeasureString("A A"));
            }
            Assert.Equal(secondPaths.Where(path => path != second.SourceIdentity).OrderBy(path => path),
                firstPaths.Where(path => path != first.SourceIdentity && secondPaths.Contains(path, StringComparer.Ordinal)).OrderBy(path => path));
            Assert.True(firstPaths.Length > secondPaths.Length);
            Assert.Equal([empty.SourceIdentity], ReadDependencies(empty));
            foreach (string path in secondPaths.Where(path => path != second.SourceIdentity)) {
                Assert.Contains(path, firstPaths);
                Assert.Single(Directory.GetFiles(buildRoot, Path.GetFileName(path), SearchOption.AllDirectories), file => Path.GetFullPath(file) == Path.GetFullPath(Path.Combine(buildRoot, path)));
            }
            for (int replay = 0; replay < 2; replay++) {
                string replayRoot = Path.Combine(projectRoot, "output-replay-" + replay);
                PlatformBuildManifest cachedManifest = service.Cook(builder.Definition, ordered, replayRoot, ["windows"], builder);
                for (int index = 0; index < manifest.Scenes.Length; index++) {
                    Assert.Equal(manifest.Scenes[index].SceneId, cachedManifest.Scenes[index].SceneId);
                    Assert.Equal(ReadDependencies(manifest.Scenes[index]), ReadDependencies(cachedManifest.Scenes[index]));
                    AssertScenePayloadContract(cachedManifest.Scenes[index], ReadDependencies(cachedManifest.Scenes[index]), replayRoot);
                    foreach (string path in ReadDependencies(cachedManifest.Scenes[index])) {
                        Assert.Equal(File.ReadAllBytes(Path.Combine(buildRoot, path)), File.ReadAllBytes(Path.Combine(replayRoot, path)));
                    }
                }
            }
        }

        /// <summary>Exercises the actual default font, PS1 atlas profile and texture executor across shared scenes and a fresh-root cache restore.</summary>
        [Fact]
        public void Ps1DefaultFontSelectsOnlyFinalCookedAtlasAcrossScenesAndFreshCacheRestore() {
            string projectRoot = PrepareFixture("ps1-default-font");
            string buildRoot = Path.Combine(projectRoot, "output");
            SceneAssetReference fontReference = EditorSceneAssetReferenceFactory.CreateEditorUiFont();
            WriteScene(projectRoot, "Scenes/First.helen", [fontReference]);
            WriteScene(projectRoot, "Scenes/Second.helen", [fontReference]);
            System.Reflection.Assembly builderAssembly = System.Reflection.Assembly.LoadFrom(Ps1BuilderPath);
            Type factory = builderAssembly.GetType("helengine.ps1.builder.PlayStation1PlatformDefinitionFactory", true);
            PlatformDefinition platform = (PlatformDefinition)factory.GetMethod("Create").Invoke(null, null);
            Assert.True(platform.RuntimeGenerationContract.SupportsSceneCodeModules);
            FontAsset defaultFont;
            using (FileStream fontStream = File.OpenRead(Path.Combine(DefaultFontInputRoot, "default.hefont"))) {
                defaultFont = helengine.files.FontAssetBinarySerializer.Deserialize(fontStream);
            }
            using (FileStream atlasStream = File.OpenRead(Path.Combine(DefaultFontInputRoot, "default-font-atlas.hasset"))) {
                defaultFont.SourceTextureAsset = Assert.IsType<TextureAsset>(AssetSerializer.Deserialize(atlasStream));
                defaultFont.CookedAtlasTextureRelativePath = string.Empty;
            }
            EditorPlatformAssetCookService service = new(projectRoot, "1.0.0-engine", "dependencies", "1.0.0", [],
                defaultFont, null, null, TestGeneratedAssetGraph.CreateShaderLibrary());
            PlatformBuildManifest first = service.Cook(platform, ["First", "Second"], buildRoot, ["ps1"]);
            AssertPs1DefaultFontOutputs(first, buildRoot, builderAssembly);
            string replayRoot = Path.Combine(projectRoot, "output-replay");
            PlatformBuildManifest replay = service.Cook(platform, ["First", "Second"], replayRoot, ["ps1"]);
            AssertPs1DefaultFontOutputs(replay, replayRoot, builderAssembly);
            for (int index = 0; index < first.Scenes.Length; index++) {
                Assert.Equal(ReadDependencies(first.Scenes[index]), ReadDependencies(replay.Scenes[index]));
            }
            Assert.Equal(File.ReadAllBytes(Path.Combine(buildRoot, "cooked/fonts/default.hefont")),
                File.ReadAllBytes(Path.Combine(replayRoot, "cooked/fonts/default.hefont")));
            Assert.Equal(File.ReadAllBytes(Path.Combine(buildRoot, "cooked/fonts/default.hetex")),
                File.ReadAllBytes(Path.Combine(replayRoot, "cooked/fonts/default.hetex")));
        }

        /// <summary>Ensures platform-owned texture and external font atlas work items belong to both scenes even when emitted only once globally.</summary>
        [Fact]
        public void BuilderOwnedWorkItemsAndExternalAtlasRemainSelectedAfterDedup() {
            string projectRoot = PrepareFixture("builder-owned");
            string buildRoot = Path.Combine(projectRoot, "output");
            WriteSource(projectRoot, "textures/shared.png");
            WriteFont(projectRoot);
            SceneAssetReference[] shared = [
                Reference("textures/shared.png"), Reference("fonts/shared.hefont")
            ];
            WriteScene(projectRoot, "Scenes/First.helen", shared);
            WriteScene(projectRoot, "Scenes/Second.helen", shared);
            PlatformDefinition platform = new("windows", "Dependency test", [], [], [], [], [], [], [], [], null, null,
                [new PlatformAssetCookCapabilityDefinition("texture", "runtime-texture", PlatformAssetCookOwnershipKind.BuilderOwned, "test-texture")]);
            using EditorPlatformBuildScenePackager packager = new(projectRoot,
                [new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"])],
                platform, TestGeneratedAssetGraph.CreateShaderLibrary());
            EditorPlatformBuildScenePackagerResult result = packager.Package(["Scenes/First.helen", "Scenes/Second.helen"], buildRoot);
            File.WriteAllText(Path.Combine(projectRoot, "result.json"), System.Text.Json.JsonSerializer.Serialize(result));
            Assert.True(result.PlatformCookWorkItems.Count == 2,
                "Expected texture and font atlas work items; actual: " + string.Join(",", result.PlatformCookWorkItems.Select(item => item.OutputRelativePath)));
            foreach (var item in result.PlatformCookWorkItems) {
                Assert.Contains(item.OutputRelativePath, result.SceneCookedDependencies["Scenes/First.helen"]);
                Assert.Contains(item.OutputRelativePath, result.SceneCookedDependencies["Scenes/Second.helen"]);
            }
            using FileStream fontStream = File.OpenRead(Path.Combine(buildRoot, "cooked/fonts/shared.hefont"));
            FontAsset font = FontAssetBinarySerializer.Deserialize(fontStream);
            Assert.Null(font.SourceTextureAsset);
            Assert.False(string.IsNullOrWhiteSpace(font.CookedAtlasTextureRelativePath));
            Assert.Contains(font.CookedAtlasTextureRelativePath, result.SceneCookedDependencies["Scenes/First.helen"]);
            Assert.Contains(font.CookedAtlasTextureRelativePath, result.SceneCookedDependencies["Scenes/Second.helen"]);
            Assert.DoesNotContain(result.SceneCookedDependencies["Scenes/Second.helen"], path => path.StartsWith("generated/", StringComparison.Ordinal));
        }

        /// <summary>Ensures module-enabled packaging declares selected legacy assets outside cooked/ without silently altering their inline font bytes.</summary>
        [Fact]
        public void ModuleManifestIncludesSelectedLegacyFontOutsideCookedRoot() {
            string projectRoot = PrepareFixture("modules-legacy-font");
            string buildRoot = Path.Combine(projectRoot, "output");
            WriteFont(projectRoot);
            WriteScene(projectRoot, "Scenes/First.helen", [Reference("fonts/shared.hefont")], includeTypedFont: false);
            TestPlatformMaterialAssetBuilder builder = new();
            PlatformDefinition original = builder.Definition;
            RuntimeGenerationContract contract = new(original.RuntimeGenerationContract.MaterialResolutionMode,
                original.RuntimeGenerationContract.SupportsRenderManager2DTextureReleaseFlush,
                original.RuntimeGenerationContract.PackagedPathPolicy, supportsSceneCodeModules: true);
            PlatformDefinition platform = new(original.PlatformId, original.DisplayName, original.BuildProfiles, original.GraphicsProfiles,
                original.AssetRequirements, original.MaterialSchemas, original.ComponentSupportRules, original.CodegenProfiles,
                original.StorageProfiles, original.MediaProfiles, contract, original.HostDebugCapability, original.AssetCookCapabilities);
            EditorPlatformAssetCookService service = new(projectRoot, "1.0.0-engine", "dependencies", "1.0.0", [],
                PackagedFontAssetFactory.Create(), null, null, TestGeneratedAssetGraph.CreateShaderLibrary());
            PlatformBuildManifest manifest = service.Cook(platform, ["First"], buildRoot, ["windows"], builder);
            PlatformBuildScene scene = Assert.Single(manifest.Scenes);
            Assert.Contains("fonts/shared.hefont", ReadDependencies(scene));
            PlatformBuildArtifact artifact = Assert.Single(manifest.CookedArtifacts, value => value.RelativePath == "fonts/shared.hefont");
            byte[] authored = File.ReadAllBytes(Path.Combine(projectRoot, "assets/fonts/shared.hefont"));
            byte[] staged = File.ReadAllBytes(Path.Combine(buildRoot, artifact.RelativePath));
            Assert.NotEmpty(staged);
            Assert.Equal(authored.Length, staged.Length);
            Assert.Equal(authored, staged);
            Assert.Equal("sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(staged)).ToLowerInvariant(), artifact.ContentHash);
            AssertScenePayloadContract(scene, ReadDependencies(scene), buildRoot);
            for (int replayIndex = 0; replayIndex < 2; replayIndex++) {
                string replayRoot = Path.Combine(projectRoot, "output-replay-" + replayIndex);
                PlatformBuildManifest replay = service.Cook(platform, ["First"], replayRoot, ["windows"], builder);
                PlatformBuildScene replayScene = Assert.Single(replay.Scenes);
                Assert.Equal(ReadDependencies(scene), ReadDependencies(replayScene));
                AssertScenePayloadContract(replayScene, ReadDependencies(replayScene), replayRoot);
                Assert.Equal(staged, File.ReadAllBytes(Path.Combine(replayRoot, "fonts/shared.hefont")));
                Assert.Equal(artifact.ContentHash, Assert.Single(replay.CookedArtifacts, value => value.RelativePath == "fonts/shared.hefont").ContentHash);
            }
        }

        /// <summary>Validates the final manifest, executes the real PS1 atlas work item and reads the cooked font through the player content processor.</summary>
        /// <param name="manifest">Manifest returned by the actual editor cook.</param>
        /// <param name="buildRoot">Cook output root containing the materialized font and work-item input.</param>
        /// <param name="builderAssembly">Current PS1 builder assembly supplying the production texture executor.</param>
        static void AssertPs1DefaultFontOutputs(PlatformBuildManifest manifest, string buildRoot, System.Reflection.Assembly builderAssembly) {
            PlatformCookWorkItem workItem = Assert.Single(manifest.PlatformCookWorkItems);
            Assert.Equal("ps1", workItem.TargetPlatformId);
            Assert.Equal("cooked/fonts/default.hetex", workItem.OutputRelativePath);
            Assert.EndsWith("generated/editor/fonts/default-font-atlas.hasset", workItem.SourceAssetPath.Replace('\\', '/'));
            Type serializerType = builderAssembly.GetType("helengine.ps1.builder.PlayStation1TextureCookSettingsSerializer", true);
            TextureAssetProcessorSettings settings = (TextureAssetProcessorSettings)serializerType.GetMethod("DeserializeFontAtlas").Invoke(null, [workItem.SerializedPlatformSettings]);
            Assert.Equal(256, settings.MaxResolution);
            Assert.Equal(TextureAssetColorFormat.Indexed4, settings.ColorFormat);
            Assert.Equal(TextureAssetAlphaPrecision.Binary, settings.AlphaPrecision);
            Assert.DoesNotContain(manifest.CookedArtifacts, artifact => artifact.RelativePath.StartsWith("generated/", StringComparison.Ordinal));
            Type executorType = builderAssembly.GetType("helengine.ps1.builder.PlayStation1PlatformCookWorkItemExecutor", true);
            object executor = Activator.CreateInstance(executorType);
            executorType.GetMethod("Execute").Invoke(executor, [workItem, buildRoot]);
            using (FileStream atlasStream = File.OpenRead(Path.Combine(buildRoot, workItem.OutputRelativePath))) {
                TextureAsset atlas = Assert.IsType<TextureAsset>(AssetSerializer.Deserialize(atlasStream));
                Assert.Equal(TextureAssetColorFormat.Indexed4, atlas.ColorFormat);
                Assert.Equal(TextureAssetAlphaPrecision.Binary, atlas.AlphaPrecision);
                Assert.InRange(atlas.Width, (ushort)1, (ushort)256);
                Assert.InRange(atlas.Height, (ushort)1, (ushort)256);
                Assert.NotEmpty(atlas.Colors);
            }
            using (FileStream fontStream = File.OpenRead(Path.Combine(buildRoot, "cooked/fonts/default.hefont"))) {
                FontAsset font = new FontAssetBinaryContentProcessor(new TestRenderManager2D()).Read(fontStream);
                Assert.Null(font.SourceTextureAsset);
                Assert.Equal("cooked/fonts/default.hetex", font.CookedAtlasTextureRelativePath);
            }
            Assert.Equal(2, manifest.Scenes.Length);
            foreach (PlatformBuildScene scene in manifest.Scenes) {
                string[] dependencies = ReadDependencies(scene);
                Assert.Contains("cooked/fonts/default.hefont", dependencies);
                Assert.Contains(workItem.OutputRelativePath, dependencies);
                Assert.DoesNotContain(dependencies, path => path.StartsWith("generated/", StringComparison.Ordinal));
                AssertScenePayloadContract(scene, dependencies, buildRoot);
            }
        }

        /// <summary>Creates a fixed isolated fixture and removes only its previous test-owned contents.</summary>
        /// <param name="name">Stable test case directory name.</param>
        /// <returns>Absolute project directory under the visible fixture root.</returns>
        static string PrepareFixture(string name) {
            string root = Path.GetFullPath(FixtureRoot);
            string projectRoot = Path.GetFullPath(Path.Combine(root, name));
            if (!projectRoot.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Scene dependency fixture escaped its workspace root.");
            }
            if (Directory.Exists(projectRoot)) { Directory.Delete(projectRoot, true); }
            Directory.CreateDirectory(Path.Combine(projectRoot, "assets"));
            return projectRoot;
        }

        /// <summary>Writes a deterministic source file interpreted by one of the fixture importers.</summary>
        /// <param name="projectRoot">Isolated project root.</param>
        /// <param name="path">Project-relative source asset path.</param>
        static void WriteSource(string projectRoot, string path) {
            string file = Path.Combine(projectRoot, "assets", path);
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllBytes(file, [1, 2, 3, 4]);
        }

        /// <summary>Writes an authored scene with current identity metadata and explicit file references.</summary>
        /// <param name="projectRoot">Isolated project root.</param>
        /// <param name="path">Canonical authored scene path.</param>
        /// <param name="references">References rewritten by the real scene packaging transform.</param>
        /// <param name="includeTypedFont">Adds a typed font consumer; false leaves a genuinely explicit table-only legacy reference.</param>
        static void WriteScene(string projectRoot, string path, SceneAssetReference[] references, bool includeTypedFont = true) {
            string file = Path.Combine(projectRoot, "assets", path);
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            using FileStream stream = File.Create(file);
            SceneAssetReference fontReference = references.FirstOrDefault(reference => reference.RelativePath.EndsWith(".hefont", StringComparison.Ordinal));
            SceneEntityAsset[] roots = [];
            if (fontReference != null && includeTypedFont) {
                EntityComponentSaveState saveState = new();
                saveState.SetAssetReference("Font", fontReference);
                AutomaticScriptComponentPersistenceDescriptor descriptor = new(new ScriptComponentReflectionSchemaBuilder());
                SceneComponentAssetRecord record = descriptor.SerializeComponent(new TextComponent {
                    Font = PackagedFontAssetFactory.Create(), Text = "A", Size = new int2(32, 16), Color = new byte4(255, 255, 255, 255)
                }, 0, saveState);
                roots = [new SceneEntityAsset {
                    Id = 1, Name = "Font owner", LocalScale = float3.One, LocalOrientation = float4.Identity,
                    Components = [record], Children = []
                }];
            }
            AssetSerializer.Serialize(stream, new SceneAsset {
                Id = path,
                AuthoringAssetId = Identity(path),
                AssetReferences = references,
                RootEntities = roots
            });
        }

        /// <summary>Writes a production AnimationPlayer component with its typed clip reference and matching authored asset table entry.</summary>
        static void WriteAnimationScene(string projectRoot, string path, AnimationClipAsset clip, SceneAssetReference reference) {
            EntityComponentSaveState saveState = new();
            saveState.SetAssetReference(nameof(AnimationPlayerComponent.Clip), reference);
            AutomaticScriptComponentPersistenceDescriptor descriptor = new(new ScriptComponentReflectionSchemaBuilder());
            SceneComponentAssetRecord record = descriptor.SerializeComponent(new AnimationPlayerComponent {
                Clip = clip, PlayAutomatically = true, ShouldLoop = true
            }, 0, saveState);
            string file = Path.Combine(projectRoot, "assets", path);
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            using FileStream stream = File.Create(file);
            AssetSerializer.Serialize(stream, new SceneAsset {
                Id = path, AuthoringAssetId = Identity(path), AssetReferences = [reference],
                RootEntities = [new SceneEntityAsset {
                    Id = 1, Name = "Animation owner", LocalScale = float3.One, LocalOrientation = float4.Identity,
                    Components = [record], Children = []
                }]
            });
        }

        /// <summary>Reads the ordinal runtime component payload with the production value walker and packaged reference codec, consuming its complete schema.</summary>
        static AnimationPlayerComponent ReadPackagedAnimationPlayer(SceneComponentAssetRecord record, string expectedPath, AnimationClipAsset clip) {
            AnimationPlayerComponent player = new();
            ScenePersistenceValueWalker walker = new(new AnimationDependencyAssetValueReader(expectedPath, clip), "Animation dependency test");
            IReadOnlyList<System.Reflection.MemberInfo> members = ScenePersistenceValueWalker.GetSerializableMembers(typeof(AnimationPlayerComponent));
            using MemoryStream stream = new(record.Payload, false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, reader.ReadByte());
            Assert.Equal(members.Count, reader.ReadInt32());
            foreach (System.Reflection.MemberInfo member in members) {
                ScenePersistenceValueWalker.SetMemberValue(member, player, walker.ReadValue(reader, ScenePersistenceValueWalker.GetMemberValueType(member)));
            }
            Assert.Equal(stream.Length, stream.Position);
            return player;
        }

        /// <summary>Writes two real sprites; one selects a PS1 override while the other retains the common texture, alongside an inactive Wii alternative.</summary>
        static void WriteScopedTextureScene(string projectRoot, string path, SceneAssetReference common, SceneAssetReference selected, SceneAssetReference inactive, SceneAssetReference tableOnly) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new(new ScriptComponentReflectionSchemaBuilder());
            EntityComponentSaveState commonState = new();
            commonState.SetAssetReference(nameof(SpriteComponent.Texture), common);
            SceneComponentAssetRecord commonRecord = descriptor.SerializeComponent(new SpriteComponent {
                Texture = new TestRuntimeTexture(), Size = new int2(16, 16)
            }, 0, commonState);
            EntityComponentSaveState selectedState = new();
            selectedState.SetAssetReference(nameof(SpriteComponent.Texture), selected);
            SceneComponentAssetRecord selectedRecord = descriptor.SerializeComponent(new SpriteComponent {
                Texture = new TestRuntimeTexture(), Size = new int2(16, 16)
            }, 0, selectedState);
            EntityComponentPlatformOverrideState selectedOverride = new() { Payload = selectedRecord.Payload };
            selectedOverride.SetAssetReference(nameof(SpriteComponent.Texture), selected);
            selectedOverride.SetPropertyOverride(nameof(SpriteComponent.Texture));
            commonState.SetPlatformOverride("ps1", selectedOverride);
            EntityComponentSaveState inactiveState = new();
            inactiveState.SetAssetReference(nameof(SpriteComponent.Texture), inactive);
            SceneComponentAssetRecord inactiveRecord = descriptor.SerializeComponent(new SpriteComponent {
                Texture = new TestRuntimeTexture(), Size = new int2(16, 16)
            }, 0, inactiveState);
            EntityComponentPlatformOverrideState inactiveOverride = new() { Payload = inactiveRecord.Payload };
            inactiveOverride.SetAssetReference(nameof(SpriteComponent.Texture), inactive);
            inactiveOverride.SetPropertyOverride(nameof(SpriteComponent.Texture));
            commonState.SetPlatformOverride("wii", inactiveOverride);
            SceneComponentAssetRecord overridden = new ComponentPlatformOverridePayloadService().Wrap(commonRecord, commonState);
            EntityComponentSaveState retainedState = new();
            retainedState.SetAssetReference(nameof(SpriteComponent.Texture), common);
            SceneComponentAssetRecord retained = descriptor.SerializeComponent(new SpriteComponent {
                Texture = new TestRuntimeTexture(), Size = new int2(16, 16)
            }, 1, retainedState);
            string file = Path.Combine(projectRoot, "assets", path);
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            using FileStream stream = File.Create(file);
            AssetSerializer.Serialize(stream, new SceneAsset {
                Id = path, AuthoringAssetId = Identity(path), AssetReferences = [common, selected, inactive, tableOnly],
                RootEntities = [new SceneEntityAsset {
                    Id = 1, Name = "Scoped sprites", LocalScale = float3.One, LocalOrientation = float4.Identity,
                    Components = [overridden, retained], Children = []
                }]
            });
        }

        /// <summary>Executes the actual PS1 texture cooker, checks final dependency/table selection and consumes each sprite through the runtime reference codec.</summary>
        static void AssertPs1TextureOutputs(PlatformBuildManifest manifest, string buildRoot, System.Reflection.Assembly assembly) {
            PlatformCookWorkItem[] workItems = manifest.PlatformCookWorkItems.ToArray();
            Assert.Equal(3, workItems.Length);
            Assert.DoesNotContain(workItems, item => item.SourceAssetPath.Replace('\\', '/').EndsWith("textures/wii.png", StringComparison.Ordinal));
            Type executorType = assembly.GetType("helengine.ps1.builder.PlayStation1PlatformCookWorkItemExecutor", true);
            object executor = Activator.CreateInstance(executorType);
            foreach (PlatformCookWorkItem item in workItems) {
                executorType.GetMethod("Execute").Invoke(executor, [item, buildRoot]);
            }
            string stagedRoot = Path.Combine(Path.GetDirectoryName(buildRoot), Path.GetFileName(buildRoot) + "-staged");
            Type stagerType = assembly.GetType("helengine.ps1.builder.PlayStation1ContentStager", true);
            object stager = Activator.CreateInstance(stagerType);
            System.Collections.IEnumerable staged = (System.Collections.IEnumerable)stagerType.GetMethod("Stage").Invoke(stager,
                [manifest.CookedArtifacts, manifest.PlatformCookWorkItems, buildRoot, stagedRoot, CancellationToken.None]);
            Dictionary<string, string> stagedPaths = new(StringComparer.Ordinal);
            foreach (object asset in staged) {
                stagedPaths.Add((string)asset.GetType().GetProperty("RelativePath").GetValue(asset),
                    (string)asset.GetType().GetProperty("SourcePath").GetValue(asset));
            }
            Dictionary<string, RuntimeTexture> textures = new(StringComparer.Ordinal);
            foreach (PlatformCookWorkItem item in workItems) {
                Assert.True(stagedPaths.TryGetValue(item.OutputRelativePath, out string texturePath));
                Assert.Equal(File.ReadAllBytes(Path.Combine(buildRoot, item.OutputRelativePath)), File.ReadAllBytes(texturePath));
                using FileStream stream = File.OpenRead(texturePath);
                TextureAsset texture = new AssetContentProcessor<TextureAsset>().Read(stream);
                Assert.InRange(texture.Width, (ushort)1, (ushort)256);
                Assert.NotEmpty(texture.Colors);
                textures.Add(item.OutputRelativePath, new TestRuntimeTexture());
            }
            foreach (PlatformBuildScene scene in manifest.Scenes) {
                string[] dependencies = ReadDependencies(scene);
                Assert.Equal(4, dependencies.Length);
                AssertScenePayloadContract(scene, dependencies, buildRoot);
                foreach (string path in dependencies) {
                    Assert.True(stagedPaths.ContainsKey(path), path);
                    Assert.Equal(File.ReadAllBytes(Path.Combine(buildRoot, path)), File.ReadAllBytes(stagedPaths[path]));
                }
                foreach (string path in textures.Keys) { Assert.Contains(path, dependencies); }
                using FileStream stream = File.OpenRead(stagedPaths[scene.SourceIdentity]);
                SceneAsset asset = PackagedAssetBinarySerializer.DeserializeSceneAsset(stream);
                Assert.Equal(textures.Keys.OrderBy(path => path), asset.AssetReferences.Select(reference => reference.RelativePath).OrderBy(path => path));
                SceneEntityAsset root = Assert.Single(asset.RootEntities);
                Assert.Equal(2, root.Components.Length);
                TextureDependencyAssetValueReader valueReader = new(textures);
                foreach (SceneComponentAssetRecord record in root.Components) {
                    SpriteComponent sprite = new();
                    ScenePersistenceValueWalker walker = new(valueReader, "Texture dependency test");
                    IReadOnlyList<System.Reflection.MemberInfo> members = ScenePersistenceValueWalker.GetSerializableMembers(typeof(SpriteComponent));
                    using MemoryStream payload = new(record.Payload, false);
                    using EngineBinaryReader reader = EngineBinaryReader.Create(payload, EngineBinaryEndianness.LittleEndian);
                    Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, reader.ReadByte());
                    Assert.Equal(members.Count, reader.ReadInt32());
                    foreach (System.Reflection.MemberInfo member in members) {
                        ScenePersistenceValueWalker.SetMemberValue(member, sprite, walker.ReadValue(reader, ScenePersistenceValueWalker.GetMemberValueType(member)));
                    }
                    Assert.Equal(payload.Length, payload.Position);
                    Assert.Contains(sprite.Texture, textures.Values);
                    Assert.Equal(new int2(16, 16), sprite.Size);
                }
                string selectedPath = Assert.Single(workItems, item => item.SourceAssetPath.Replace('\\', '/').EndsWith("textures/ps1.png", StringComparison.Ordinal)).OutputRelativePath;
                string commonPath = Assert.Single(workItems, item => item.SourceAssetPath.Replace('\\', '/').EndsWith("textures/common.png", StringComparison.Ordinal)).OutputRelativePath;
                Assert.Equal([selectedPath, commonPath], valueReader.ReadPaths);
            }
        }

        /// <summary>Writes a packaged source font whose atlas is externalized by the actual cook.</summary>
        /// <param name="projectRoot">Isolated project root.</param>
        static void WriteFont(string projectRoot) {
            string path = Path.Combine(projectRoot, "assets/fonts/shared.hefont");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            FontAsset font = PackagedFontAssetFactory.Create();
            using FileStream stream = File.Create(path);
            FontAssetBinarySerializer.Serialize(stream, font);
        }

        /// <summary>Writes an authored material and its imported cached texture, exercising nested material dependencies.</summary>
        /// <param name="projectRoot">Isolated project root.</param>
        static void WriteMaterialAndTexture(string projectRoot) {
            string texturePath = Path.Combine(projectRoot, "cache/material-diffuse");
            Directory.CreateDirectory(Path.GetDirectoryName(texturePath));
            using (FileStream stream = File.Create(texturePath)) {
                AssetSerializer.Serialize(stream, new TextureAsset {
                    Width = 1, Height = 1, Colors = [255, 128, 64, 255], AuthoringAssetId = Identity("material-diffuse")
                });
            }
            MaterialAssetImportSettings settings = new();
            settings.Importer.ImporterId = "helengine.material";
            settings.Importer.AssetId = "materials/shared.hasset";
            settings.Processor.Platforms["windows"] = new MaterialAssetProcessorSettings {
                SchemaId = "standard-shader",
                FieldValues = new Dictionary<string, string> {
                    ["use-custom-shader"] = "false", ["texture-id"] = "material-diffuse", ["base-color"] = "#FFFFFFFF",
                    ["roughness"] = "1.0", ["casts-shadow"] = "true", ["receives-shadow"] = "true"
                }
            };
            string materialPath = Path.Combine(projectRoot, "assets/materials/shared.hasset");
            Directory.CreateDirectory(Path.GetDirectoryName(materialPath));
            new MaterialAssetSettingsService(projectRoot).Save(materialPath, settings);
        }

        /// <summary>Reads the public manifest dependency selection produced by the real cook boundary.</summary>
        /// <param name="scene">Packaged scene manifest entry.</param>
        /// <returns>Exact dependency paths to be preloaded for that scene.</returns>
        static string[] ReadDependencies(PlatformBuildScene scene) {
            string serialized = Assert.Single(scene.ResolvedMetadata, pair => pair.Key == "cooked-dependency-paths").Value;
            return System.Text.Json.JsonSerializer.Deserialize<string[]>(serialized);
        }

        /// <summary>Ensures preload metadata expands dependencies without changing the legacy single-scene payload contract.</summary>
        /// <param name="scene">Packaged scene entry.</param>
        /// <param name="paths">Preload selection emitted for that scene.</param>
        /// <param name="buildRoot">Actual output root used to verify selected files exist.</param>
        static void AssertScenePayloadContract(PlatformBuildScene scene, string[] paths, string buildRoot) {
            Assert.Contains(scene.SourceIdentity, paths);
            Assert.Equal(paths.Length, paths.Distinct(StringComparer.Ordinal).Count());
            PlatformBuildPayloadReference payload = Assert.Single(scene.PayloadReferences);
            Assert.Equal(scene.SourceIdentity, payload.LogicalId);
            Assert.Equal(scene.SourceIdentity, payload.SourceIdentity);
            foreach (string path in paths) { Assert.True(File.Exists(Path.Combine(buildRoot, path)), path); }
        }

        /// <summary>Generates deterministic valid authored identity metadata for serialized fixtures.</summary>
        /// <param name="path">Stable fixture asset path.</param>
        /// <returns>Hexadecimal 128-bit authored identity.</returns>
        static string Identity(string path) {
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path)))[..32].ToLowerInvariant();
        }

        /// <summary>Creates distinct valid reference identities so unrelated fixture assets never collide in global work-item deduplication.</summary>
        /// <param name="path">Canonical authored asset path.</param>
        /// <returns>File reference consumed by the actual identity resolver.</returns>
        static SceneAssetReference Reference(string path) {
            return global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(Identity(path), path,
                "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
        }
    }
}
