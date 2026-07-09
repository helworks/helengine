using helengine.directx11;
using helengine.editor.tests.testing;
using helengine.ui;
using helengine.vulkan;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies scene authoring behavior for blueprint instance roots and inherited expansion.
    /// </summary>
    public class BlueprintSceneEmbeddingTests : IDisposable {
        /// <summary>
        /// Temporary project root used by blueprint scene embedding tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes a temporary project root and the core services required for scene authoring tests.
        /// </summary>
        public BlueprintSceneEmbeddingTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-blueprint-scene-embedding-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scenes"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Blueprints"));

            EditorCore core = new EditorCore(new Project {
                Name = "Blueprint Scene Embedding",
                Path = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"), new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempProjectRootPath)
            });
            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            shaderBackendRegistry.Register(new VulkanShaderBackend());
            EditorBuiltInShaderAssetLibrary.ConfigureShaderBackends(shaderBackendRegistry);
        }

        /// <summary>
        /// Deletes temporary project state after each test.
        /// </summary>
        public void Dispose() {
            EditorCameraVisualResources.ResetForTests();
            EditorPointLightVisualResources.ResetForTests();
            EditorDirectionalLightVisualResources.ResetForTests();
            EditorSpotLightVisualResources.ResetForTests();
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures scene load expands one referenced blueprint beneath the scene-owned instance root.
        /// </summary>
        [Fact]
        public void Load_WhenSceneContainsBlueprintInstance_ExpandsInheritedSubtree() {
            WriteBlueprintAsset("Blueprints/TestBlueprint.hblueprint");
            string scenePath = SaveSceneWithBlueprintInstance("Scenes/BlueprintInstance.helen", "Blueprints/TestBlueprint.hblueprint");
            SceneFileLoadService loadService = new SceneFileLoadService(TempProjectRootPath, new ComponentPersistenceRegistry(), new TestSceneAssetReferenceResolver());

            LoadedEditorSceneDocument loaded = loadService.Load(scenePath);

            EditorEntity instanceRoot = Assert.Single(loaded.RootEntities);
            Assert.Equal("Blueprint Instance", instanceRoot.Name);
            BlueprintInstanceComponent instanceComponent = Assert.IsType<BlueprintInstanceComponent>(Assert.Single(instanceRoot.Components, component => component is BlueprintInstanceComponent));
            Assert.Equal("Blueprints/TestBlueprint.hblueprint", instanceComponent.BlueprintAssetPath);

            EditorEntity inheritedRoot = Assert.IsType<EditorEntity>(Assert.Single(instanceRoot.Children));
            Assert.Equal("Blueprint Root", inheritedRoot.Name);
            Assert.NotNull(Assert.Single(inheritedRoot.Components, component => component is BlueprintInheritedEntityComponent));

            EditorEntity inheritedChild = Assert.IsType<EditorEntity>(Assert.Single(inheritedRoot.Children));
            Assert.Equal("Blueprint Child", inheritedChild.Name);
            Assert.NotNull(Assert.Single(inheritedChild.Components, component => component is BlueprintInheritedEntityComponent));
        }

        /// <summary>
        /// Ensures scene save does not serialize expanded inherited blueprint content as scene-owned child entities.
        /// </summary>
        [Fact]
        public void Save_WhenSceneContainsExpandedBlueprintInstance_SerializesOnlyInstanceRoot() {
            WriteBlueprintAsset("Blueprints/TestBlueprint.hblueprint");
            string scenePath = SaveSceneWithBlueprintInstance("Scenes/BlueprintInstance.helen", "Blueprints/TestBlueprint.hblueprint");
            SceneFileLoadService loadService = new SceneFileLoadService(TempProjectRootPath, new ComponentPersistenceRegistry(), new TestSceneAssetReferenceResolver());
            LoadedEditorSceneDocument loaded = loadService.Load(scenePath);

            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, new ComponentPersistenceRegistry());
            string roundTripScenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "BlueprintInstanceRoundTrip.helen");

            saveService.Save(roundTripScenePath);

            SceneAsset savedAsset;
            using (FileStream stream = File.OpenRead(roundTripScenePath)) {
                savedAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneEntityAsset instanceRootAsset = Assert.Single(savedAsset.RootEntities);
            Assert.Equal("Blueprint Instance", instanceRootAsset.Name);
            Assert.Empty(instanceRootAsset.Children);
            Assert.Single(instanceRootAsset.Components);
            Assert.Contains(typeof(BlueprintInstanceComponent).FullName, instanceRootAsset.Components[0].ComponentTypeId, StringComparison.Ordinal);
        }

        /// <summary>
        /// Writes one minimal blueprint asset used by the scene embedding tests.
        /// </summary>
        /// <param name="relativeBlueprintPath">Project-relative blueprint path to create.</param>
        void WriteBlueprintAsset(string relativeBlueprintPath) {
            string blueprintPath = Path.Combine(TempProjectRootPath, "assets", relativeBlueprintPath.Replace('/', Path.DirectorySeparatorChar));
            string blueprintDirectoryPath = Path.GetDirectoryName(blueprintPath);
            if (string.IsNullOrWhiteSpace(blueprintDirectoryPath)) {
                throw new InvalidOperationException("Blueprint directory could not be resolved.");
            }

            Directory.CreateDirectory(blueprintDirectoryPath);
            using FileStream stream = File.Create(blueprintPath);
            AssetSerializer.Serialize(stream, new BlueprintAsset {
                Id = relativeBlueprintPath,
                RootEntity = new SceneEntityAsset {
                    Id = 1u,
                    Name = "Blueprint Root",
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
                }
            });
        }

        /// <summary>
        /// Saves one scene containing a single blueprint instance root.
        /// </summary>
        /// <param name="relativeScenePath">Project-relative scene path to write.</param>
        /// <param name="blueprintAssetPath">Project-relative blueprint asset path referenced by the instance root.</param>
        /// <returns>Absolute path to the saved scene file.</returns>
        string SaveSceneWithBlueprintInstance(string relativeScenePath, string blueprintAssetPath) {
            EditorEntity instanceRoot = Assert.IsType<EditorEntity>(Core.Instance.EntityFactory.Create("Blueprint Instance"));
            instanceRoot.LocalPosition = new float3(4f, 5f, 6f);
            instanceRoot.AddComponent(new BlueprintInstanceComponent {
                BlueprintAssetPath = blueprintAssetPath
            });

            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, new ComponentPersistenceRegistry());
            string fullScenePath = Path.Combine(TempProjectRootPath, "assets", relativeScenePath.Replace('/', Path.DirectorySeparatorChar));
            saveService.Save(fullScenePath);
            instanceRoot.Enabled = false;
            Core.Instance.ObjectManager.RemoveEntity(instanceRoot);
            return fullScenePath;
        }
    }
}
