using helengine.editor.tests.testing;
using helengine.directx11;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies end-to-end scene packaging expands blueprint instances before the cooked scene is written.
    /// </summary>
    public class BlueprintBuildPackagingIntegrationTests : IDisposable {
        readonly string ProjectRootPath;
        readonly string BuildRootPath;

        public BlueprintBuildPackagingIntegrationTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-blueprint-build-packaging-tests", Guid.NewGuid().ToString("N"));
            BuildRootPath = Path.Combine(ProjectRootPath, "Build");
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Blueprints"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scenes"));
            Directory.CreateDirectory(BuildRootPath);

            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            EditorBuiltInShaderAssetLibrary.ConfigureShaderBackends(shaderBackendRegistry);
        }

        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        [Fact]
        public void Package_WhenSceneContainsBlueprintInstance_ExpandsBlueprintContentIntoCookedScene() {
            SceneAssetReference blueprintMaterialReference = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateEngineStandardMaterial();
            WriteBlueprintAsset("Blueprints/TestBlueprint.hblueprint", blueprintMaterialReference);

            string sceneId = "Scenes/TestScene.helen";
            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = [
                    new SceneEntityAsset {
                        Id = 100u,
                        Name = "Instance Root",
                        LocalPosition = new float3(4f, 5f, 6f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        LayerMask = EditorLayerMasks.SceneObjects,
                        Components = [
                            SerializeComponent(new BlueprintInstanceComponent {
                                BlueprintAssetPath = "Blueprints/TestBlueprint.hblueprint"
                            })
                        ],
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                ],
                AssetReferences = Array.Empty<SceneAssetReference>()
            });

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(ProjectRootPath);

            packager.Package([sceneId], BuildRootPath);

            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(GetPackagedScenePath(sceneId))) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneEntityAsset instanceRoot = Assert.Single(packagedScene.RootEntities);
            Assert.Equal(new float3(4f, 5f, 6f), instanceRoot.LocalPosition);
            Assert.Empty(instanceRoot.Components);

            SceneEntityAsset blueprintRoot = Assert.Single(instanceRoot.Children);
            Assert.Equal("Blueprint Root", blueprintRoot.Name);
            Assert.Equal("Blueprint Child", Assert.Single(blueprintRoot.Children).Name);

            Assert.Single(packagedScene.AssetReferences);
            Assert.Equal(blueprintMaterialReference.AssetId, packagedScene.AssetReferences[0].AssetId);
        }

        [Fact]
        public void Package_WhenPlatformPrunesBlueprintInstance_ExcludesItsExpandedEntitiesAndAssetReferences() {
            SceneAssetReference consoleOnlyReference = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemMaterial("Blueprints/console-only.bin");
            SceneAssetReference handheldOnlyReference = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemMaterial("Blueprints/handheld-only.bin");
            File.WriteAllBytes(Path.Combine(ProjectRootPath, "assets", "Blueprints", "console-only.bin"), [1]);
            File.WriteAllBytes(Path.Combine(ProjectRootPath, "assets", "Blueprints", "handheld-only.bin"), [2]);
            WriteBlueprintAsset("Blueprints/ConsolePresentation.hblueprint", consoleOnlyReference, "Console Presentation Root");
            WriteBlueprintAsset("Blueprints/HandheldPresentation.hblueprint", handheldOnlyReference, "Handheld Presentation Root");

            string sceneId = "Scenes/TiltTrialPresentation.helen";
            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = [
                    CreateBlueprintInstance(100u, "Console Presentation", "Blueprints/ConsolePresentation.hblueprint", "windows", false),
                    CreateBlueprintInstance(200u, "Handheld Presentation", "Blueprints/HandheldPresentation.hblueprint", "ds", false)
                ],
                AssetReferences = Array.Empty<SceneAssetReference>()
            });

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(ProjectRootPath);
            packager.Package([sceneId], BuildRootPath);

            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(GetPackagedScenePath(sceneId))) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.DoesNotContain(packagedScene.RootEntities, entity => entity.Name == "Console Presentation");
            SceneEntityAsset handheldRoot = Assert.Single(packagedScene.RootEntities, entity => entity.Name == "Handheld Presentation");
            Assert.Equal("Handheld Presentation Root", Assert.Single(handheldRoot.Children).Name);
            Assert.Single(packagedScene.AssetReferences);
            Assert.True(string.Equals(handheldOnlyReference.RelativePath, packagedScene.AssetReferences[0].RelativePath, StringComparison.OrdinalIgnoreCase));
            Assert.False(File.Exists(Path.Combine(BuildRootPath, "Blueprints", "console-only.bin")));
            Assert.True(File.Exists(Path.Combine(BuildRootPath, "Blueprints", "handheld-only.bin")));

            HashSet<uint> entityIds = new HashSet<uint>();
            AssertUniqueEntityIds(packagedScene.RootEntities, entityIds);
        }

        static void AssertUniqueEntityIds(SceneEntityAsset[] entities, HashSet<uint> entityIds) {
            SceneEntityAsset[] resolvedEntities = entities ?? Array.Empty<SceneEntityAsset>();
            for (int index = 0; index < resolvedEntities.Length; index++) {
                SceneEntityAsset entity = resolvedEntities[index];
                Assert.NotNull(entity);
                Assert.True(entityIds.Add(entity.Id), $"Entity id '{entity.Id}' was registered more than once.");
                AssertUniqueEntityIds(entity.Children, entityIds);
            }
        }

        static SceneEntityAsset CreateBlueprintInstance(
            uint id,
            string name,
            string blueprintAssetPath,
            string platformId,
            bool exists) {
            return new SceneEntityAsset {
                Id = id,
                Name = name,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                LayerMask = EditorLayerMasks.SceneObjects,
                Components = [
                    SerializeComponent(new BlueprintInstanceComponent {
                        BlueprintAssetPath = blueprintAssetPath
                    })
                ],
                PlatformExistenceOverrides = [
                    new SceneEntityPlatformExistenceOverrideAsset {
                        PlatformId = platformId,
                        Exists = exists
                    }
                ],
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        void WriteBlueprintAsset(string relativePath, SceneAssetReference materialReference, string rootName = "Blueprint Root") {
            string fullPath = Path.Combine(ProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Blueprint directory could not be resolved."));

            using FileStream stream = File.Create(fullPath);
            AssetSerializer.Serialize(stream, new BlueprintAsset {
                Id = relativePath,
                RootEntity = new SceneEntityAsset {
                    Id = 1u,
                    Name = rootName,
                    LayerMask = EditorLayerMasks.SceneObjects,
                    LocalPosition = float3.Zero,
                    LocalScale = float3.One,
                    LocalOrientation = float4.Identity,
                    Children = [
                        new SceneEntityAsset {
                            Id = 2u,
                            Name = "Blueprint Child",
                            LayerMask = EditorLayerMasks.SceneObjects,
                            LocalPosition = float3.Zero,
                            LocalScale = float3.One,
                            LocalOrientation = float4.Identity,
                            Children = Array.Empty<SceneEntityAsset>()
                        }
                    ]
                },
                AssetReferences = [materialReference]
            });
        }

        void WriteSceneAsset(string sceneId, SceneAsset sceneAsset) {
            string fullPath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Scene directory could not be resolved."));

            using FileStream stream = File.Create(fullPath);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        string GetPackagedScenePath(string sceneId) {
            return Path.Combine(BuildRootPath, PackagedScenePathResolver.BuildRelativePath(sceneId, 0).Replace('/', Path.DirectorySeparatorChar));
        }

        static SceneComponentAssetRecord SerializeComponent(Component component) {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            return registry.GetDescriptor(component).SerializeComponent(component, 0, new EntityComponentSaveState());
        }
    }
}
