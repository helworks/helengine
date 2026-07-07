using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies the committed rendering demo scenes deserialize and preserve their authored renderer-facing intent.
    /// </summary>
    public class RenderingSceneCatalogTests : IDisposable {
        /// <summary>
        /// Absolute path to the committed rendering scene directory.
        /// </summary>
        readonly string RenderingSceneRootPath;
        /// <summary>
        /// Absolute path to the committed rendering material directory.
        /// </summary>
        readonly string RenderingMaterialRootPath;
        /// <summary>
        /// Absolute path to the committed rendering code directory.
        /// </summary>
        readonly string RenderingCodeRootPath;
        /// <summary>
        /// Core instance used while deserializing persisted camera payloads.
        /// </summary>
        readonly Core Core;

        /// <summary>
        /// Initializes the committed scene catalog test fixture.
        /// </summary>
        public RenderingSceneCatalogTests() {
            string repositoryRootPath = new EditorSourceBuildWorkspaceLocator().ResolveHelEngineRootPath();
            RenderingSceneRootPath = Path.Combine(repositoryRootPath, "test-project", "assets", "Scenes", "rendering");
            RenderingMaterialRootPath = Path.Combine(repositoryRootPath, "test-project", "assets", "Materials", "rendering");
            RenderingCodeRootPath = Path.Combine(repositoryRootPath, "test-project", "assets", "codebase", "rendering");

            Core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(repositoryRootPath)
            });
            Core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Releases the test core after each scene catalog assertion.
        /// </summary>
        public void Dispose() {
            Core.Dispose();
        }

        /// <summary>
        /// Ensures the opaque basics scene exists and contains one camera plus multiple mesh entities.
        /// </summary>
        [Fact]
        public void SceneCatalog_WhenLoadingOpaqueBasics_IncludesCameraAndMultipleMeshes() {
            SceneAsset sceneAsset = LoadSceneAsset("opaque-basics.helen");

            Assert.NotNull(FindFirstComponent(sceneAsset.RootEntities, "helengine.CameraComponent"));
            Assert.True(CountComponents(sceneAsset.RootEntities, "helengine.MeshComponent") >= 4);
        }

        /// <summary>
        /// Ensures the transparency ordering scene references the authored transparent material variant.
        /// </summary>
        [Fact]
        public void SceneCatalog_WhenLoadingTransparencyOrder_ReferencesTransparentMaterial() {
            SceneAsset sceneAsset = LoadSceneAsset("transparency-order.helen");

            Assert.Contains(
                sceneAsset.AssetReferences,
                reference => reference.RelativePath == "Materials/rendering/TransparentStandard.helmat");
            Assert.True(File.Exists(Path.Combine(RenderingMaterialRootPath, "TransparentStandard.helmat")));
        }

        /// <summary>
        /// Ensures the depth prepass scene persists the camera render settings required by this renderer slice.
        /// </summary>
        [Fact]
        public void SceneCatalog_WhenLoadingDepthPrepass_CameraUsesAlwaysDepthPrepass() {
            SceneAsset sceneAsset = LoadSceneAsset("depth-prepass.helen");
            SceneComponentAssetRecord cameraRecord = FindFirstComponent(sceneAsset.RootEntities, "helengine.CameraComponent");
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());

            CameraComponent cameraComponent = (CameraComponent)descriptor.DeserializeComponent(cameraRecord, null, null);

            Assert.Equal(DepthPrepassMode.Always, cameraComponent.RenderSettings.DepthPrepassMode);
            Assert.Equal(PostProcessTier.Disabled, cameraComponent.RenderSettings.PostProcessTier);
        }

        /// <summary>
        /// Ensures the material inputs scene references the authored double-sided material variant.
        /// </summary>
        [Fact]
        public void SceneCatalog_WhenLoadingMaterialInputs_ReferencesDoubleSidedMaterial() {
            SceneAsset sceneAsset = LoadSceneAsset("material-inputs.helen");

            Assert.Contains(
                sceneAsset.AssetReferences,
                reference => reference.RelativePath == "Materials/rendering/DoubleSidedStandard.helmat");
            Assert.True(File.Exists(Path.Combine(RenderingMaterialRootPath, "DoubleSidedStandard.helmat")));
        }

        /// <summary>
        /// Ensures the point-shadow smoke scene exists and contains one camera, one point light, and multiple mesh entities.
        /// </summary>
        [Fact]
        public void SceneCatalog_WhenLoadingPointShadow_IncludesCameraPointLightAndMultipleMeshes() {
            SceneAsset sceneAsset = LoadSceneAsset("point-shadow.helen");
            SceneComponentAssetRecord cameraRecord = FindFirstComponent(sceneAsset.RootEntities, "helengine.CameraComponent");
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            CameraComponent cameraComponent = (CameraComponent)descriptor.DeserializeComponent(cameraRecord, null, null);

            Assert.NotNull(cameraRecord);
            Assert.Equal(1, CountComponents(sceneAsset.RootEntities, "helengine.PointLightComponent"));
            Assert.True(CountComponents(sceneAsset.RootEntities, "helengine.MeshComponent") >= 3);
            Assert.Equal(DepthPrepassMode.Auto, cameraComponent.RenderSettings.DepthPrepassMode);
            Assert.Equal(60f, cameraComponent.RenderSettings.ShadowDistance);
            Assert.Equal(PostProcessTier.Disabled, cameraComponent.RenderSettings.PostProcessTier);
        }

        /// <summary>
        /// Ensures the point-shadow lab scene exists and contains one enclosed debug room, one point light, and multiple receiver markers.
        /// </summary>
        [Fact]
        public void SceneCatalog_WhenLoadingPointShadowLab_IncludesCameraPointLightAndEnclosedDebugRoom() {
            SceneAsset sceneAsset = LoadSceneAsset("point-shadow-lab.helen");
            SceneComponentAssetRecord cameraRecord = FindFirstComponent(sceneAsset.RootEntities, "helengine.CameraComponent");
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            CameraComponent cameraComponent = (CameraComponent)descriptor.DeserializeComponent(cameraRecord, null, null);

            Assert.NotNull(cameraRecord);
            Assert.Equal(1, CountComponents(sceneAsset.RootEntities, "helengine.PointLightComponent"));
            Assert.True(CountComponents(sceneAsset.RootEntities, "helengine.MeshComponent") >= 14);
            Assert.Equal(DepthPrepassMode.Auto, cameraComponent.RenderSettings.DepthPrepassMode);
            Assert.Equal(60f, cameraComponent.RenderSettings.ShadowDistance);
            Assert.Equal(PostProcessTier.Disabled, cameraComponent.RenderSettings.PostProcessTier);
        }

        /// <summary>
        /// Ensures the spot-shadow lab scene exists and contains one camera, one spot light, and multiple room meshes for cone-shadow debugging.
        /// </summary>
        [Fact]
        public void SceneCatalog_WhenLoadingSpotShadowLab_IncludesCameraSpotLightAndEnclosedDebugRoom() {
            SceneAsset sceneAsset = LoadSceneAsset("spot-shadow-lab.helen");
            SceneComponentAssetRecord cameraRecord = FindFirstComponent(sceneAsset.RootEntities, "helengine.CameraComponent");
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            CameraComponent cameraComponent = (CameraComponent)descriptor.DeserializeComponent(cameraRecord, null, null);
            int meshComponentCount = CountComponents(sceneAsset.RootEntities, "helengine.MeshComponent");

            Assert.NotNull(cameraRecord);
            Assert.Equal(1, CountComponents(sceneAsset.RootEntities, "helengine.SpotLightComponent"));
            Assert.True(meshComponentCount >= 12, "Expected at least 12 mesh components in spot-shadow-lab.helen but found " + meshComponentCount + ".");
            Assert.Equal(DepthPrepassMode.Auto, cameraComponent.RenderSettings.DepthPrepassMode);
            Assert.Equal(60f, cameraComponent.RenderSettings.ShadowDistance);
            Assert.Equal(PostProcessTier.Disabled, cameraComponent.RenderSettings.PostProcessTier);
        }

        /// <summary>
        /// Ensures the directional-shadow lab scene exists and contains one camera, one directional light, and multiple outdoor receiver meshes.
        /// </summary>
        [Fact]
        public void SceneCatalog_WhenLoadingDirectionalShadowLab_IncludesCameraDirectionalLightAndOutdoorDebugLayout() {
            SceneAsset sceneAsset = LoadSceneAsset("directional-shadow-lab.helen");
            SceneComponentAssetRecord cameraRecord = FindFirstComponent(sceneAsset.RootEntities, "helengine.CameraComponent");
            SceneComponentAssetRecord directionalLightRecord = FindFirstComponent(sceneAsset.RootEntities, "helengine.DirectionalLightComponent");
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            AutomaticScriptComponentPersistenceDescriptor lightDescriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            CameraComponent cameraComponent = (CameraComponent)descriptor.DeserializeComponent(cameraRecord, null, null);
            DirectionalLightComponent directionalLightComponent = (DirectionalLightComponent)lightDescriptor.DeserializeComponent(directionalLightRecord, null, null);

            Assert.NotNull(cameraRecord);
            Assert.NotNull(directionalLightRecord);
            Assert.Equal(1, CountComponents(sceneAsset.RootEntities, "helengine.DirectionalLightComponent"));
            Assert.True(CountComponents(sceneAsset.RootEntities, "helengine.MeshComponent") >= 8);
            Assert.Equal(DepthPrepassMode.Auto, cameraComponent.RenderSettings.DepthPrepassMode);
            Assert.Equal(60f, cameraComponent.RenderSettings.ShadowDistance);
            Assert.Equal(PostProcessTier.Disabled, cameraComponent.RenderSettings.PostProcessTier);
            Assert.Equal(60f, directionalLightComponent.ShadowDistance);
        }

        /// <summary>
        /// Ensures the directional-shadow plaza showcase scene exists and contains the expected authored attract-mode runtime components.
        /// </summary>
        [Fact]
        public void SceneCatalog_WhenLoadingDirectionalShadowPlaza_IncludesDirectionalLightCameraAndAttractModeComponents() {
            SceneAsset sceneAsset = LoadSceneAsset("directional-shadow-plaza.helen");
            SceneComponentAssetRecord cameraRecord = FindFirstComponent(sceneAsset.RootEntities, "helengine.CameraComponent");
            SceneComponentAssetRecord directionalLightRecord = FindFirstComponent(sceneAsset.RootEntities, "helengine.DirectionalLightComponent");
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            CameraComponent cameraComponent = (CameraComponent)descriptor.DeserializeComponent(cameraRecord, null, null);

            Assert.NotNull(cameraRecord);
            Assert.NotNull(directionalLightRecord);
            Assert.Equal(1, CountComponents(sceneAsset.RootEntities, "helengine.DirectionalLightComponent"));
            Assert.Equal(1, CountComponents(sceneAsset.RootEntities, "helengine.CameraComponent"));
            Assert.Equal(3, CountComponents(sceneAsset.RootEntities, "gameplay.rendering.DirectionalShadowTowerSpinComponent, gameplay"));
            Assert.Equal(1, CountComponents(sceneAsset.RootEntities, "gameplay.rendering.DirectionalShadowOrbitComponent, gameplay"));
            Assert.Equal(1, CountComponents(sceneAsset.RootEntities, "gameplay.rendering.DirectionalShadowSunSweepComponent, gameplay"));
            Assert.Equal(1, CountComponents(sceneAsset.RootEntities, "gameplay.rendering.DirectionalShadowCameraOrbitComponent, gameplay"));
            Assert.Equal(DepthPrepassMode.Auto, cameraComponent.RenderSettings.DepthPrepassMode);
            Assert.Equal(60f, cameraComponent.RenderSettings.ShadowDistance);
            Assert.Equal(PostProcessTier.Disabled, cameraComponent.RenderSettings.PostProcessTier);
            Assert.True(File.Exists(Path.Combine(RenderingCodeRootPath, "DirectionalShadowTowerSpinComponent.cs")));
            Assert.True(File.Exists(Path.Combine(RenderingCodeRootPath, "DirectionalShadowOrbitComponent.cs")));
            Assert.True(File.Exists(Path.Combine(RenderingCodeRootPath, "DirectionalShadowSunSweepComponent.cs")));
            Assert.True(File.Exists(Path.Combine(RenderingCodeRootPath, "DirectionalShadowCameraOrbitComponent.cs")));
        }

        /// <summary>
        /// Loads one committed rendering scene asset from disk.
        /// </summary>
        /// <param name="sceneFileName">Scene file name under the committed rendering scene directory.</param>
        /// <returns>Deserialized scene asset.</returns>
        SceneAsset LoadSceneAsset(string sceneFileName) {
            if (string.IsNullOrWhiteSpace(sceneFileName)) {
                throw new ArgumentException("Scene file name must be provided.", nameof(sceneFileName));
            }

            string scenePath = Path.Combine(RenderingSceneRootPath, sceneFileName);
            Assert.True(File.Exists(scenePath), "Expected committed rendering scene was not found: " + scenePath);

            using FileStream stream = File.OpenRead(scenePath);
            return Assert.IsType<SceneAsset>(helengine.editor.AssetSerializer.Deserialize(stream));
        }

        /// <summary>
        /// Counts the number of serialized components with one specific type id across the full scene hierarchy.
        /// </summary>
        /// <param name="entities">Serialized root or child entities to inspect.</param>
        /// <param name="componentTypeId">Stable serialized component type id to count.</param>
        /// <returns>Total number of matching components in the supplied hierarchy.</returns>
        int CountComponents(SceneEntityAsset[] entities, string componentTypeId) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            } else if (string.IsNullOrWhiteSpace(componentTypeId)) {
                throw new ArgumentException("Component type id must be provided.", nameof(componentTypeId));
            }

            int count = 0;
            for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++) {
                SceneEntityAsset entity = entities[entityIndex];
                if (entity == null) {
                    continue;
                }

                for (int componentIndex = 0; componentIndex < entity.Components.Length; componentIndex++) {
                    SceneComponentAssetRecord component = entity.Components[componentIndex];
                    if (component != null && component.ComponentTypeId == componentTypeId) {
                        count++;
                    }
                }

                count += CountComponents(entity.Children, componentTypeId);
            }

            return count;
        }

        /// <summary>
        /// Finds the first serialized component with one specific type id across the full scene hierarchy.
        /// </summary>
        /// <param name="entities">Serialized root or child entities to inspect.</param>
        /// <param name="componentTypeId">Stable serialized component type id to locate.</param>
        /// <returns>First matching serialized component record.</returns>
        SceneComponentAssetRecord FindFirstComponent(SceneEntityAsset[] entities, string componentTypeId) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            } else if (string.IsNullOrWhiteSpace(componentTypeId)) {
                throw new ArgumentException("Component type id must be provided.", nameof(componentTypeId));
            }

            for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++) {
                SceneEntityAsset entity = entities[entityIndex];
                if (entity == null) {
                    continue;
                }

                for (int componentIndex = 0; componentIndex < entity.Components.Length; componentIndex++) {
                    SceneComponentAssetRecord component = entity.Components[componentIndex];
                    if (component != null && component.ComponentTypeId == componentTypeId) {
                        return component;
                    }
                }

                SceneComponentAssetRecord childComponent = FindFirstComponent(entity.Children, componentTypeId);
                if (childComponent != null) {
                    return childComponent;
                }
            }

            return null;
        }
    }
}

