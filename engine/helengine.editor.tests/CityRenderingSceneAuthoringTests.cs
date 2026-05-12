using helengine.files;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies authored city rendering scenes keep cube-grid showcase entities aligned to identity orientation.
    /// </summary>
    public sealed class CityRenderingSceneAuthoringTests {
        /// <summary>
        /// Gets the local city project path used by environment-backed rendering-scene regressions.
        /// </summary>
        const string CityProjectRootPath = @"C:\dev\helprojs\city";

        /// <summary>
        /// Ensures the authored city demo-disc scene catalog places the axis test as the fourth playable scene entry.
        /// </summary>
        [Fact]
        public void ReadCityDemoDiscSceneCatalogSource_AxisTestIsTheFourthPlayableScene() {
            string sceneCatalogSource = ReadDemoDiscSceneCatalogSource();

            int cubeTestIndex = sceneCatalogSource.IndexOf("\"cube_test\"", StringComparison.Ordinal);
            int coloredCubeGridIndex = sceneCatalogSource.IndexOf("\"colored_cube_grid\"", StringComparison.Ordinal);
            int texturedCubeGridIndex = sceneCatalogSource.IndexOf("\"textured_cube_grid\"", StringComparison.Ordinal);
            int axisTestIndex = sceneCatalogSource.IndexOf("\"axis_test\"", StringComparison.Ordinal);
            int directionalShadowPlazaIndex = sceneCatalogSource.IndexOf("\"directional_shadow_plaza\"", StringComparison.Ordinal);

            Assert.True(cubeTestIndex >= 0);
            Assert.True(coloredCubeGridIndex > cubeTestIndex);
            Assert.True(texturedCubeGridIndex > coloredCubeGridIndex);
            Assert.True(axisTestIndex > texturedCubeGridIndex);
            Assert.True(directionalShadowPlazaIndex > axisTestIndex);
        }

        /// <summary>
        /// Ensures the authored city Windows build configuration includes the axis test scene in the selected scene set.
        /// </summary>
        [Fact]
        public void ReadCityWindowsBuildConfig_WindowsSelectedScenesContainAxisTest() {
            string buildConfigSource = ReadBuildConfigSource();

            int windowsPlatformIndex = buildConfigSource.IndexOf("\"platformId\":  \"windows\"", StringComparison.Ordinal);
            int axisTestIndex = buildConfigSource.IndexOf("\"axis_test\"", StringComparison.Ordinal);
            int pspPlatformIndex = buildConfigSource.IndexOf("\"platformId\":  \"psp\"", StringComparison.Ordinal);

            Assert.True(windowsPlatformIndex >= 0);
            Assert.True(axisTestIndex > windowsPlatformIndex);
            Assert.True(pspPlatformIndex < 0 || axisTestIndex < pspPlatformIndex);
        }

        /// <summary>
        /// Ensures the authored colored cube-grid scene stores identity orientation for each generated showcase cube.
        /// </summary>
        [Fact]
        public void DeserializeCityColoredCubeGridSceneAsset_AuthoredCubeOrientationsAreIdentity() {
            SceneAsset sceneAsset = ReadSceneAsset("colored_cube_grid.helen");

            AssertCubeOrientationsAreIdentity(sceneAsset, "ColoredCubeGridCube");
        }

        /// <summary>
        /// Ensures the authored textured cube-grid scene stores identity orientation for each generated showcase cube.
        /// </summary>
        [Fact]
        public void DeserializeCityTexturedCubeGridSceneAsset_AuthoredCubeOrientationsAreIdentity() {
            SceneAsset sceneAsset = ReadSceneAsset("textured_cube_grid.helen");

            AssertCubeOrientationsAreIdentity(sceneAsset, "TexturedCubeGridCube");
        }

        /// <summary>
        /// Ensures the authored city axis-test-2 scene exists as a generated rendering validation asset.
        /// </summary>
        [Fact]
        public void DeserializeCityAxisTest2SceneAsset_GeneratedSceneExists() {
            SceneAsset sceneAsset = ReadSceneAsset("axis_test2.helen");

            Assert.Equal("axis_test2", sceneAsset.Id);
        }

        /// <summary>
        /// Ensures the authored city axis-test-2 scene stores one directional light and one camera-forward-axis spin component.
        /// </summary>
        [Fact]
        public void DeserializeCityAxisTest2SceneAsset_ContainsDirectionalLightRigAndCameraForwardSpinComponent() {
            SceneAsset sceneAsset = ReadSceneAsset("axis_test2.helen");

            Assert.Equal(1, CountComponents(sceneAsset.RootEntities, "helengine.DirectionalLightComponent"));
            Assert.Equal(1, CountComponents(sceneAsset.RootEntities, "gameplay.rendering.AxisTestCameraForwardSpinComponent, gameplay"));
        }

        /// <summary>
        /// Ensures the authored city axis-test-2 camera mirrors the original axis-test camera distance on the right side.
        /// </summary>
        [Fact]
        public void DeserializeCityAxisTest2SceneAsset_CameraUsesRightSideMirroredDistance() {
            SceneAsset sceneAsset = ReadSceneAsset("axis_test2.helen");
            SceneEntityAsset cameraEntity = FindEntityByName(sceneAsset.RootEntities, "AxisTest2Camera");

            Assert.NotNull(cameraEntity);
            Assert.Equal(new float3(30f, 6f, 5f), cameraEntity.LocalPosition);
        }

        /// <summary>
        /// Ensures the authored city axis-test-2 right wall extends along the mirrored camera forward axis instead of across depth.
        /// </summary>
        [Fact]
        public void DeserializeCityAxisTest2SceneAsset_RightWallExtendsAlongCameraForwardAxis() {
            SceneAsset sceneAsset = ReadSceneAsset("axis_test2.helen");
            SceneEntityAsset wallEntity = FindEntityByName(sceneAsset.RootEntities, "AxisTest2Ground");

            Assert.NotNull(wallEntity);
            Assert.Equal(new float3(14f, 12f, 0.5f), wallEntity.LocalScale);
        }

        /// <summary>
        /// Ensures the authored spotlight street-slice scene references generated racer companion materials through their real `.helmat` assets instead of import-settings sidecars.
        /// </summary>
        [Fact]
        public void DeserializeCitySpotlightStreetSliceSceneAsset_RacerMaterialReferencesDoNotUseHelmatHassetSidecars() {
            SceneAsset sceneAsset = ReadSceneAsset("spotlight_street_slice.helen");

            Assert.DoesNotContain(
                sceneAsset.AssetReferences ?? Array.Empty<SceneAssetReference>(),
                reference => !string.IsNullOrWhiteSpace(reference.RelativePath)
                    && reference.RelativePath.Contains(".helmat.hasset", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                sceneAsset.AssetReferences ?? Array.Empty<SceneAssetReference>(),
                reference => string.Equals(reference.RelativePath, "models/Riemers/racer/x3ds_mat_ruedas.helmat", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Reads one city rendering scene asset from the authored project scene folder.
        /// </summary>
        /// <param name="sceneFileName">File name of the authored rendering scene.</param>
        /// <returns>Deserialized scene asset.</returns>
        SceneAsset ReadSceneAsset(string sceneFileName) {
            string scenePath = Path.Combine(CityProjectRootPath, "assets", "scenes", "rendering", sceneFileName);
            Assert.True(File.Exists(scenePath));

            using FileStream stream = File.OpenRead(scenePath);
            return Assert.IsType<SceneAsset>(EditorAssetBinarySerializer.Deserialize(stream));
        }

        /// <summary>
        /// Reads the authored city demo-disc scene catalog source file.
        /// </summary>
        /// <returns>Demo-disc scene catalog source text.</returns>
        string ReadDemoDiscSceneCatalogSource() {
            string sceneCatalogPath = Path.Combine(CityProjectRootPath, "assets", "codebase", "menu", "DemoDiscSceneCatalog.cs");
            Assert.True(File.Exists(sceneCatalogPath));
            return File.ReadAllText(sceneCatalogPath);
        }

        /// <summary>
        /// Reads the authored city build configuration source file.
        /// </summary>
        /// <returns>Build configuration source text.</returns>
        string ReadBuildConfigSource() {
            string buildConfigPath = Path.Combine(CityProjectRootPath, "user_settings", "build_config.json");
            Assert.True(File.Exists(buildConfigPath));
            return File.ReadAllText(buildConfigPath);
        }

        /// <summary>
        /// Asserts all generated showcase cubes beneath the supplied scene share identity local orientation.
        /// </summary>
        /// <param name="sceneAsset">Scene asset whose cube entities should be inspected.</param>
        /// <param name="cubeNamePrefix">Stable name prefix used by the generated cube entities.</param>
        void AssertCubeOrientationsAreIdentity(SceneAsset sceneAsset, string cubeNamePrefix) {
            if (sceneAsset == null) {
                throw new ArgumentNullException(nameof(sceneAsset));
            } else if (string.IsNullOrWhiteSpace(cubeNamePrefix)) {
                throw new ArgumentException("Cube name prefix must be provided.", nameof(cubeNamePrefix));
            }

            List<SceneEntityAsset> cubeEntities = new List<SceneEntityAsset>();
            CollectCubeEntities(sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>(), cubeNamePrefix, cubeEntities);

            Assert.Equal(16, cubeEntities.Count);
            for (int index = 0; index < cubeEntities.Count; index++) {
                Assert.Equal(float4.Identity, cubeEntities[index].LocalOrientation);
            }
        }

        /// <summary>
        /// Counts matching component records throughout one scene hierarchy.
        /// </summary>
        /// <param name="entities">Scene entities to inspect.</param>
        /// <param name="componentTypeId">Serialized component type identifier to count.</param>
        /// <returns>Total matching component count.</returns>
        int CountComponents(SceneEntityAsset[] entities, string componentTypeId) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            } else if (string.IsNullOrWhiteSpace(componentTypeId)) {
                throw new ArgumentException("Component type id must be provided.", nameof(componentTypeId));
            }

            int count = 0;
            for (int index = 0; index < entities.Length; index++) {
                SceneEntityAsset entity = entities[index];
                if (entity == null) {
                    continue;
                }

                SceneComponentAssetRecord[] components = entity.Components ?? Array.Empty<SceneComponentAssetRecord>();
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++) {
                    if (string.Equals(components[componentIndex].ComponentTypeId, componentTypeId, StringComparison.Ordinal)) {
                        count++;
                    }
                }

                count += CountComponents(entity.Children ?? Array.Empty<SceneEntityAsset>(), componentTypeId);
            }

            return count;
        }

        /// <summary>
        /// Finds one entity by exact display name within the supplied hierarchy.
        /// </summary>
        /// <param name="entities">Scene entities to inspect.</param>
        /// <param name="entityName">Exact display name to locate.</param>
        /// <returns>Matching scene entity when found; otherwise null.</returns>
        SceneEntityAsset FindEntityByName(SceneEntityAsset[] entities, string entityName) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            } else if (string.IsNullOrWhiteSpace(entityName)) {
                throw new ArgumentException("Entity name must be provided.", nameof(entityName));
            }

            for (int index = 0; index < entities.Length; index++) {
                SceneEntityAsset entity = entities[index];
                if (entity == null) {
                    continue;
                } else if (string.Equals(entity.Name, entityName, StringComparison.Ordinal)) {
                    return entity;
                }

                SceneEntityAsset childMatch = FindEntityByName(entity.Children ?? Array.Empty<SceneEntityAsset>(), entityName);
                if (childMatch != null) {
                    return childMatch;
                }
            }

            return null;
        }

        /// <summary>
        /// Collects generated showcase cube entities from the supplied scene hierarchy.
        /// </summary>
        /// <param name="entities">Scene entities to inspect.</param>
        /// <param name="cubeNamePrefix">Stable name prefix used by the generated cube entities.</param>
        /// <param name="results">Destination list receiving matching cube entities.</param>
        void CollectCubeEntities(SceneEntityAsset[] entities, string cubeNamePrefix, List<SceneEntityAsset> results) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            } else if (string.IsNullOrWhiteSpace(cubeNamePrefix)) {
                throw new ArgumentException("Cube name prefix must be provided.", nameof(cubeNamePrefix));
            } else if (results == null) {
                throw new ArgumentNullException(nameof(results));
            }

            for (int index = 0; index < entities.Length; index++) {
                SceneEntityAsset entity = entities[index];
                if (entity != null && !string.IsNullOrWhiteSpace(entity.Name) && entity.Name.StartsWith(cubeNamePrefix, StringComparison.Ordinal)) {
                    results.Add(entity);
                }

                if (entity != null) {
                    CollectCubeEntities(entity.Children ?? Array.Empty<SceneEntityAsset>(), cubeNamePrefix, results);
                }
            }
        }
    }
}
