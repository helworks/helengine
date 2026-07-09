using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Verifies raw scene-asset expansion for blueprint instances during build packaging.
    /// </summary>
    public class BlueprintPackagedSceneExpansionTests : IDisposable {
        /// <summary>
        /// Temporary project root used by blueprint packaged-scene expansion tests.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes a temporary project root with an assets folder.
        /// </summary>
        public BlueprintPackagedSceneExpansionTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-blueprint-packaged-scene-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Blueprints"));
        }

        /// <summary>
        /// Deletes temporary project state after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures blueprint instance roots expand into ordinary child entities and merge blueprint dependencies.
        /// </summary>
        [Fact]
        public void Expand_WhenSceneContainsBlueprintInstance_AppendsBlueprintRootAndMergesReferences() {
            SceneAssetReference blueprintMaterialReference = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateEngineStandardMaterial();
            WriteBlueprintAsset("Blueprints/TestBlueprint.hblueprint", blueprintMaterialReference);
            SceneAsset sceneAsset = new SceneAsset {
                Id = "Scenes/TestScene.helen",
                RootEntities = [
                    new SceneEntityAsset {
                        Id = 100u,
                        Name = "Instance Root",
                        LayerMask = EditorLayerMasks.SceneObjects,
                        LocalPosition = new float3(4f, 5f, 6f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = [
                            SerializeComponent(new BlueprintInstanceComponent {
                                BlueprintAssetPath = "Blueprints/TestBlueprint.hblueprint"
                            })
                        ],
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                ],
                AssetReferences = Array.Empty<SceneAssetReference>()
            };

            BlueprintPackagedSceneExpansionService service = new BlueprintPackagedSceneExpansionService(ProjectRootPath, new ComponentPersistenceRegistry());

            service.Expand(sceneAsset);

            SceneEntityAsset instanceRoot = Assert.Single(sceneAsset.RootEntities);
            Assert.Empty(instanceRoot.Components);
            SceneEntityAsset expandedBlueprintRoot = Assert.Single(instanceRoot.Children);
            Assert.Equal("Blueprint Root", expandedBlueprintRoot.Name);
            Assert.Equal(1u, expandedBlueprintRoot.Id);
            Assert.Equal("Blueprint Child", Assert.Single(expandedBlueprintRoot.Children).Name);
            Assert.Single(sceneAsset.AssetReferences);
            Assert.Equal(blueprintMaterialReference.AssetId, sceneAsset.AssetReferences[0].AssetId);
        }

        /// <summary>
        /// Ensures nested blueprint instances inside blueprint source content are rejected during packaging.
        /// </summary>
        [Fact]
        public void Expand_WhenBlueprintContainsNestedBlueprintInstance_ThrowsInvalidOperationException() {
            WriteNestedBlueprintAsset("Blueprints/NestedBlueprint.hblueprint");
            SceneAsset sceneAsset = new SceneAsset {
                Id = "Scenes/TestScene.helen",
                RootEntities = [
                    new SceneEntityAsset {
                        Id = 100u,
                        Name = "Instance Root",
                        LayerMask = EditorLayerMasks.SceneObjects,
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = [
                            SerializeComponent(new BlueprintInstanceComponent {
                                BlueprintAssetPath = "Blueprints/NestedBlueprint.hblueprint"
                            })
                        ],
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                ]
            };

            BlueprintPackagedSceneExpansionService service = new BlueprintPackagedSceneExpansionService(ProjectRootPath, new ComponentPersistenceRegistry());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.Expand(sceneAsset));

            Assert.Contains("nested blueprint instances", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Writes one blueprint asset with a regular child subtree and one merged asset reference.
        /// </summary>
        /// <param name="relativePath">Project-relative blueprint path to write.</param>
        /// <param name="materialReference">Scene asset reference merged from the blueprint dependency list.</param>
        void WriteBlueprintAsset(string relativePath, SceneAssetReference materialReference) {
            string fullPath = Path.Combine(ProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Blueprint directory could not be resolved."));

            using FileStream stream = File.Create(fullPath);
            AssetSerializer.Serialize(stream, new BlueprintAsset {
                Id = relativePath,
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
                },
                AssetReferences = [materialReference]
            });
        }

        /// <summary>
        /// Writes one blueprint asset whose source content illegally contains a nested blueprint instance record.
        /// </summary>
        /// <param name="relativePath">Project-relative blueprint path to write.</param>
        void WriteNestedBlueprintAsset(string relativePath) {
            string fullPath = Path.Combine(ProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Blueprint directory could not be resolved."));

            using FileStream stream = File.Create(fullPath);
            AssetSerializer.Serialize(stream, new BlueprintAsset {
                Id = relativePath,
                RootEntity = new SceneEntityAsset {
                    Id = 1u,
                    Name = "Blueprint Root",
                    LayerMask = EditorLayerMasks.SceneObjects,
                    LocalPosition = float3.Zero,
                    LocalScale = float3.One,
                    LocalOrientation = float4.Identity,
                    Components = [
                        SerializeComponent(new BlueprintInstanceComponent {
                            BlueprintAssetPath = "Blueprints/Other.hblueprint"
                        })
                    ],
                    Children = Array.Empty<SceneEntityAsset>()
                }
            });
        }

        /// <summary>
        /// Serializes one live component into a scene component record using the standard editor persistence registry.
        /// </summary>
        /// <param name="component">Component instance to serialize.</param>
        /// <returns>Serialized scene component record.</returns>
        static SceneComponentAssetRecord SerializeComponent(Component component) {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            return registry.GetDescriptor(component).SerializeComponent(component, 0, new EntityComponentSaveState());
        }
    }
}
