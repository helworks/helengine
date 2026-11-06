using helengine.animation.runtime;
using helengine.editor;
using helengine.editor.assimp;

namespace helengine.animation.tests {
    /// <summary>
    /// Verifies the editor side of the animation plugin: glTF animations imported in scene mode become <c>.hanimclip</c>
    /// clips bound to the blueprint entities, the blueprint root gets a paused player, and packaging for webgpu cooks the
    /// clip and keeps the player bound after blueprint expansion.
    /// </summary>
    public class GltfAnimationSceneContributorTests : IDisposable {
        /// <summary>
        /// Assets-relative path of the generated folder of the hinged box fixture.
        /// </summary>
        const string GeneratedFolder = "hinged_box.gltf.generated";

        /// <summary>
        /// Path of the generated clip inside the generated folder; the fixture's only animation is named Open.
        /// </summary>
        const string ClipPathInFolder = "anim/Open.hanimclip";

        /// <summary>
        /// Canonical packaged path the player loads the clip from. It lies under cooked/, the only build folder platform
        /// builders ship as content.
        /// </summary>
        const string PackagedClipPath = "cooked/hinged_box.gltf.generated/anim/open.hanimclip";

        /// <summary>
        /// Build-relative path the packager writes the Scenes/Main.helen scene to: the authored scenes root becomes cooked/scenes.
        /// </summary>
        const string PackagedScenePath = "cooked/scenes/main.hasset";

        /// <summary>
        /// Tolerance for key values read back from the fixture.
        /// </summary>
        const int Precision = 5;

        /// <summary>
        /// Temporary project root used for each test.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Temporary assets root holding the copied fixture.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Temporary build output root.
        /// </summary>
        readonly string BuildRootPath;

        /// <summary>
        /// Initializes isolated project directories for each test case.
        /// </summary>
        public GltfAnimationSceneContributorTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-animation-plugin-tests", Guid.NewGuid().ToString("N"));
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            BuildRootPath = Path.Combine(ProjectRootPath, "Build");
            Directory.CreateDirectory(AssetsRootPath);
            Directory.CreateDirectory(BuildRootPath);
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
        /// Ensures the fixture's animation becomes one clip whose track targets the channel's node path with key times in seconds,
        /// and that no animation is left unconsumed.
        /// </summary>
        [Fact]
        public void Animations_becomeClipsWithNodePathTargets() {
            AssetImportManager manager = ImportFixture(true);

            HierarchyAnimationClipAsset clip = (HierarchyAnimationClipAsset)ReadAsset(Path.Combine(AssetsRootPath, GeneratedFolder, "anim", "Open.hanimclip"));
            Assert.Equal(1d, clip.Duration, Precision);
            HierarchyAnimationTrack track = Assert.Single(clip.Tracks);
            Assert.Equal("Base/Lid", track.TargetPath);
            Assert.Equal(AnimationInterpolationMode.Linear, track.Interpolation);
            Assert.Equal(3, track.RotationTimes.Length);
            Assert.Equal(0d, track.RotationTimes[0], Precision);
            Assert.Equal(0.5d, track.RotationTimes[1], Precision);
            Assert.Equal(1d, track.RotationTimes[2], Precision);
            Assert.Equal(0.7071068d, (double)track.RotationValues[1].Z, Precision);
            Assert.Equal(0.7071068d, (double)track.RotationValues[1].W, Precision);
            Assert.Equal(1d, (double)track.RotationValues[2].Z, Precision);
            Assert.Equal(track.PositionTimes.Length, track.PositionValues.Length);
            Assert.Equal(track.ScaleTimes.Length, track.ScaleValues.Length);
            Assert.Empty(manager.LastSceneImportUnconsumed);
        }

        /// <summary>
        /// Ensures the blueprint root carries a paused, non-looping player whose clip path names the generated clip, whose single
        /// track target references the Lid entity, and whose clip is listed in the blueprint's reference table.
        /// </summary>
        [Fact]
        public void RootEntity_getsPausedPlayerForFirstAnimation() {
            ImportFixture(true);

            BlueprintAsset blueprint = ReadBlueprint();
            SceneComponentAssetRecord record = Assert.Single(blueprint.RootEntity.Components, IsPlayerRecord);
            HierarchyAnimationPlayerComponent player = (HierarchyAnimationPlayerComponent)new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder())
                .DeserializeComponent(record, null, null);
            Assert.Equal(PackagedClipPath, player.ClipPath);
            Assert.False(player.Playing);
            Assert.False(player.Loop);
            Assert.Equal(1d, player.Speed);
            SceneEntityReference target = Assert.Single(player.TrackTargets);
            Assert.Equal(FindEntity(blueprint.RootEntity, "Base", "Lid").Id, target.EntityId);
            Assert.Contains(blueprint.AssetReferences, reference => reference.RelativePath == GeneratedFolder + "/" + ClipPathInFolder);
        }

        /// <summary>
        /// Ensures reimporting keeps the clip's identity, so references to it survive a reimport.
        /// </summary>
        [Fact]
        public void Reimport_keepsClipIdentity() {
            AssetImportManager manager = ImportFixture(true);
            string clipFullPath = Path.Combine(AssetsRootPath, GeneratedFolder, "anim", "Open.hanimclip");
            string firstId = new AssetIdentityMetadataService(ProjectRootPath).Load(clipFullPath).AssetId;

            manager.ImportModel(Path.Combine(AssetsRootPath, "hinged_box.gltf"));

            Assert.Equal(firstId, new AssetIdentityMetadataService(ProjectRootPath).Load(clipFullPath).AssetId);
            Assert.Contains(ReadBlueprint().AssetReferences, reference => reference.AssetId == firstId);
        }

        /// <summary>
        /// Counter-test: with only the core contributor the animation is reported unconsumed and nothing animation-related is written.
        /// </summary>
        [Fact]
        public void WithoutPlugin_animationsAreReportedUnconsumed() {
            AssetImportManager manager = ImportFixture(false);

            Assert.Equal(new[] { "1 animations not imported: no animation plugin in the project" }, manager.LastSceneImportUnconsumed);
            Assert.False(Directory.Exists(Path.Combine(AssetsRootPath, GeneratedFolder, "anim")));
            Assert.DoesNotContain(ReadBlueprint().RootEntity.Components, IsPlayerRecord);
        }

        /// <summary>
        /// Ensures the registration wires the payload serializer, the packaged cooker and the scene contributor.
        /// </summary>
        [Fact]
        public void Registration_registersSerializerCookerAndContributor() {
            AssetImportManager manager = CreateManager();

            new AnimationEditorRegistration().Register(manager.ImporterRegistry);

            Assert.IsType<HierarchyAnimationClipCooker>(manager.ImporterRegistry.FindPackagedAssetCooker(".HANIMCLIP"));
            Assert.Contains(manager.ImporterRegistry.GetSceneContributors(), contributor => contributor is GltfAnimationSceneContributor);
            Assert.IsType<HierarchyAnimationClipPayloadSerializer>(
                global::helengine.files.EditorAssetPayloadSerializerRegistry.FindByValueKind(EditorAssetBinaryValueKind.HierarchyAnimationClipAsset));
        }

        /// <summary>
        /// Ensures a clip written in the editor asset format reads back through the runtime reader, so the layout lives only there.
        /// </summary>
        [Fact]
        public void Serializer_writesFilesTheRuntimeReaderLoads() {
            new AnimationEditorRegistration().Register(CreateManager().ImporterRegistry);
            HierarchyAnimationClipAsset clip = new HierarchyAnimationClipAsset {
                Duration = 2d,
                Tracks = new HierarchyAnimationTrack[] {
                    new HierarchyAnimationTrack {
                        TargetPath = "Arm/Hand",
                        PositionTimes = new double[] { 0d, 2d },
                        PositionValues = new float3[] { new float3(0f, 0f, 0f), new float3(1f, 2f, 3f) }
                    }
                }
            };
            using MemoryStream stream = new MemoryStream();
            global::helengine.editor.AssetSerializer.Serialize(stream, clip);
            stream.Position = 0;

            HierarchyAnimationClipAsset runtimeClip = HierarchyAnimationClipReader.Deserialize(stream);
            stream.Position = 0;
            HierarchyAnimationClipAsset editorClip = (HierarchyAnimationClipAsset)global::helengine.editor.AssetSerializer.Deserialize(stream);

            Assert.Equal("Arm/Hand", Assert.Single(runtimeClip.Tracks).TargetPath);
            Assert.Equal(3f, (float)runtimeClip.Tracks[0].PositionValues[1].Z);
            Assert.Equal(2d, editorClip.Duration);
            Assert.Equal(2d, Assert.Single(editorClip.Tracks).PositionTimes[1]);
        }

        /// <summary>
        /// Packages a scene that instantiates the generated blueprint for webgpu and ensures the clip is cooked through the
        /// plugin's cooker into a file the runtime reader loads, and that the player's clip path and track target survive
        /// blueprint expansion with the target remapped to the expanded Lid entity.
        /// </summary>
        [Fact]
        public void Cooker_packagesClipForWebgpu() {
            ImportFixture(true);
            RecordingClipCooker cooker = new RecordingClipCooker();

            SceneAsset packagedScene = PackageBlueprintInstance(cooker);

            Assert.Equal(new[] { GeneratedFolder + "/" + ClipPathInFolder }, cooker.CookedRelativePaths);
            string packagedClipFullPath = Path.Combine(BuildRootPath, PackagedClipPath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(packagedClipFullPath), "Missing packaged clip " + PackagedClipPath);
            using (FileStream clipStream = File.OpenRead(packagedClipFullPath)) {
                Assert.Equal("Base/Lid", Assert.Single(HierarchyAnimationClipReader.Deserialize(clipStream).Tracks).TargetPath);
            }

            SceneEntityAsset instanceRoot = Assert.Single(packagedScene.RootEntities);
            SceneEntityAsset blueprintRoot = Assert.Single(instanceRoot.Children);
            SceneComponentAssetRecord record = Assert.Single(blueprintRoot.Components, IsPlayerRecord);
            HierarchyAnimationPlayerComponent player = (HierarchyAnimationPlayerComponent)new AutomaticScriptComponentRuntimeDeserializer(
                record.ComponentTypeId,
                typeof(HierarchyAnimationPlayerComponent)).Deserialize(record, null);
            Assert.Equal(PackagedClipPath, player.ClipPath);
            Assert.False(player.Playing);
            SceneEntityReference target = Assert.Single(player.TrackTargets);
            uint blueprintLidId = FindEntity(ReadBlueprint().RootEntity, "Base", "Lid").Id;
            uint packagedLidId = FindEntity(blueprintRoot, "Base", "Lid").Id;
            Assert.NotEqual(blueprintLidId, packagedLidId);
            Assert.Equal(packagedLidId, target.EntityId);
        }

        /// <summary>
        /// Copies the hinged box fixture, imports it in scene mode and returns the manager.
        /// </summary>
        /// <param name="withPlugin">Whether the animation plugin is registered.</param>
        /// <returns>Manager that ran the import.</returns>
        AssetImportManager ImportFixture(bool withPlugin) {
            string sourcePath = Path.Combine(AssetsRootPath, "hinged_box.gltf");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "fixtures", "scene", "hinged_box.gltf"), sourcePath);
            AssetImportManager manager = CreateManager();
            if (withPlugin) {
                new AnimationEditorRegistration().Register(manager.ImporterRegistry);
            }

            ModelAssetImportSettings settings = manager.LoadOrCreateModelImportSettings(sourcePath);
            settings.Mode = ModelImportMode.Scene;
            manager.SaveModelImportSettings(sourcePath, settings);
            manager.ImportModel(sourcePath);
            return manager;
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
        /// Writes a scene with one blueprint instance of the generated blueprint and packages it for webgpu with the recording cooker.
        /// </summary>
        /// <param name="cooker">Cooker the packager uses for clips.</param>
        /// <returns>Packaged scene.</returns>
        SceneAsset PackageBlueprintInstance(RecordingClipCooker cooker) {
            string sceneId = "Scenes/Main.helen";
            string sceneFullPath = Path.Combine(AssetsRootPath, "Scenes", "Main.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(sceneFullPath));
            BlueprintInstanceComponent instance = new BlueprintInstanceComponent {
                BlueprintAssetReference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                    "00112233445566778899aabbccddeeff",
                    GeneratedFolder + "/hinged_box.hblueprint",
                    "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")
            };
            SceneAsset scene = new SceneAsset {
                Id = sceneId,
                AuthoringAssetId = Guid.NewGuid().ToString("N"),
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 100u,
                        Name = "Instance Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        LayerMask = EditorLayerMasks.SceneObjects,
                        Components = new[] {
                            new ComponentPersistenceRegistry().GetDescriptor(instance).SerializeComponent(instance, 0, new EntityComponentSaveState())
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            };
            using (FileStream stream = File.Create(sceneFullPath)) {
                global::helengine.editor.AssetSerializer.Serialize(stream, scene);
            }

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                new IAssetImporterRegistration[] { new RecordingClipCookerRegistration(cooker) },
                "webgpu",
                CreateShaderLibrary());
            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedPath = Path.Combine(BuildRootPath, PackagedScenePath.Replace('/', Path.DirectorySeparatorChar));
            using FileStream packagedStream = File.OpenRead(packagedPath);
            return global::helengine.PackagedAssetBinarySerializer.DeserializeSceneAsset(packagedStream);
        }

        /// <summary>
        /// Creates the built-in shader library the packager compiles standard materials with.
        /// </summary>
        /// <returns>Shader library with the DirectX 11 and Vulkan backends.</returns>
        static EditorBuiltInShaderAssetLibrary CreateShaderLibrary() {
            ShaderBackendRegistry backendRegistry = new ShaderBackendRegistry();
            backendRegistry.Register(new global::helengine.directx11.DirectX11ShaderBackend());
            backendRegistry.Register(new global::helengine.vulkan.VulkanShaderBackend());
            return new EditorBuiltInShaderAssetLibrary(backendRegistry);
        }

        /// <summary>
        /// Returns whether a component record is a hierarchy animation player.
        /// </summary>
        /// <param name="record">Component record.</param>
        /// <returns>True for player records.</returns>
        static bool IsPlayerRecord(SceneComponentAssetRecord record) {
            return record.ComponentTypeId == AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(HierarchyAnimationPlayerComponent));
        }

        /// <summary>
        /// Follows child names down from an entity.
        /// </summary>
        /// <param name="entity">Entity to start from.</param>
        /// <param name="names">Child names, outermost first.</param>
        /// <returns>The entity at the end of the path.</returns>
        static SceneEntityAsset FindEntity(SceneEntityAsset entity, params string[] names) {
            SceneEntityAsset current = entity;
            for (int index = 0; index < names.Length; index++) {
                current = Assert.Single(current.Children, child => child.Name == names[index]);
            }

            return current;
        }

        /// <summary>
        /// Deserializes one native asset file.
        /// </summary>
        /// <param name="path">Absolute file path.</param>
        /// <returns>Deserialized asset.</returns>
        static Asset ReadAsset(string path) {
            using FileStream stream = File.OpenRead(path);
            return global::helengine.editor.AssetSerializer.Deserialize(stream);
        }

        /// <summary>
        /// Reads the generated blueprint.
        /// </summary>
        /// <returns>Deserialized blueprint.</returns>
        BlueprintAsset ReadBlueprint() {
            return (BlueprintAsset)ReadAsset(Path.Combine(AssetsRootPath, GeneratedFolder, "hinged_box.hblueprint"));
        }
    }
}
