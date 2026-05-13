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
        /// Ensures the regenerated cube-test scene still contains the expected authored camera, sun, and cube roots.
        /// </summary>
        [Fact]
        public void DeserializeCityCubeTestSceneAsset_ContainsCameraSunAndCubeRoots() {
            SceneAsset sceneAsset = ReadSceneAsset("cube_test.helen");

            Assert.NotNull(FindEntityByName(sceneAsset.RootEntities, "CubeTestCamera"));
            Assert.NotNull(FindEntityByName(sceneAsset.RootEntities, "CubeTestSun"));
            Assert.NotNull(FindEntityByName(sceneAsset.RootEntities, "CubeTestCube"));
        }

        /// <summary>
        /// Ensures the regenerated cube-test cube still carries one mesh component and one authored motion component record.
        /// </summary>
        [Fact]
        public void DeserializeCityCubeTestSceneAsset_CubeRootContainsMeshAndMotionComponent() {
            SceneAsset sceneAsset = ReadSceneAsset("cube_test.helen");
            SceneEntityAsset cubeEntity = FindEntityByName(sceneAsset.RootEntities, "CubeTestCube");

            Assert.NotNull(cubeEntity);
            Assert.Equal(2, (cubeEntity.Components ?? Array.Empty<SceneComponentAssetRecord>()).Length);
        }

        /// <summary>
        /// Ensures the regenerated cube-test cube stores the reusable gameplay axis-rotation component type.
        /// </summary>
        [Fact]
        public void DeserializeCityCubeTestSceneAsset_CubeRootContainsAxisRotationComponent() {
            SceneAsset sceneAsset = ReadSceneAsset("cube_test.helen");

            Assert.Equal(1, CountComponents(sceneAsset.RootEntities, "gameplay.rendering.AxisRotationComponent, gameplay"));
        }

        /// <summary>
        /// Ensures the regenerated cube-test scene no longer stores the stale directional-shadow tower-spin component type.
        /// </summary>
        [Fact]
        public void DeserializeCityCubeTestSceneAsset_DoesNotContainDirectionalShadowTowerSpinComponent() {
            SceneAsset sceneAsset = ReadSceneAsset("cube_test.helen");

            Assert.Equal(0, CountComponents(sceneAsset.RootEntities, "gameplay.rendering.DirectionalShadowTowerSpinComponent, gameplay"));
            Assert.Equal(0, CountComponents(sceneAsset.RootEntities, "city.rendering.DirectionalShadowTowerSpinComponent, gameplay"));
            Assert.Equal(0, CountComponents(sceneAsset.RootEntities, "helengine.DirectionalShadowTowerSpinComponent"));
        }

        /// <summary>
        /// Ensures the authored cube-test scene remains deserializable after regeneration through the live-authoring save path.
        /// </summary>
        [Fact]
        public void DeserializeCityCubeTestSceneAsset_RemainsReadableAfterLiveAuthoringSavePath() {
            SceneAsset sceneAsset = ReadSceneAsset("cube_test.helen");

            Assert.Equal("scenes/rendering/cube_test.helen", sceneAsset.Id);
        }

        /// <summary>
        /// Ensures cube-test authored scene source creation now relies on the host-owned entity factory instead of constructing editor entities directly.
        /// </summary>
        [Fact]
        public void ReadCityCubeTestSceneFactorySource_DoesNotConstructEditorEntitiesOrSetEditorSuppressionDirectly() {
            string source = ReadCitySource("rendering.tools", "CubeTestSceneFactory.cs");

            Assert.DoesNotContain("new EditorEntity {", source, StringComparison.Ordinal);
            Assert.DoesNotContain("new EditorEntity(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("new EditorEntityFactory()", source, StringComparison.Ordinal);
            Assert.DoesNotContain("SuppressUpdateComponentExecutionInEditor", source, StringComparison.Ordinal);
            Assert.Contains("Core.Instance.EntityFactory", source, StringComparison.Ordinal);
            Assert.Contains(".Create(", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the authored demo-disc menu provider includes the platform-info overlay descriptor.
        /// </summary>
        [Fact]
        public void ReadCityDemoDiscMenuDefinitionProviderSource_EmitsPlatformInfoOverlayDescriptor() {
            string source = ReadCitySource("menu", "DemoDiscMenuDefinitionProvider.cs");

            Assert.Contains("new MenuPlatformInfoDefinition(", source, StringComparison.Ordinal);
            Assert.Contains("PlatformInfoTopMargin", source, StringComparison.Ordinal);
            Assert.Contains("PlatformInfoRightMargin", source, StringComparison.Ordinal);
            Assert.Contains("PlatformInfoLineSpacing", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the authored demo-disc runtime binder component populates the platform name and version strings.
        /// </summary>
        [Fact]
        public void ReadCityPlatformInfoTextComponentSource_UsesPlatformInfoValues() {
            string source = ReadCitySource("menu", "PlatformInfoTextComponent.cs");

            Assert.Contains("Core.Instance.PlatformInfo.Name", source, StringComparison.Ordinal);
            Assert.Contains("Core.Instance.PlatformInfo.Version", source, StringComparison.Ordinal);
            Assert.Contains("DemoDiscPlatformInfoNameText", source, StringComparison.Ordinal);
            Assert.Contains("DemoDiscPlatformInfoVersionText", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the colored cube-grid factory now authors live entities instead of serialized editor scene records.
        /// </summary>
        [Fact]
        public void ReadColoredCubeGridSceneFactorySource_DoesNotUseEditorSerializationHelpers() {
            string source = ReadCitySource("rendering.tools", "ColoredCubeGridSceneFactory.cs");

            Assert.DoesNotContain("using helengine.editor;", source, StringComparison.Ordinal);
            Assert.DoesNotContain("SceneComponentAssetRecord", source, StringComparison.Ordinal);
            Assert.DoesNotContain("MeshComponentPersistenceDescriptor", source, StringComparison.Ordinal);
            Assert.Contains("GeneratedAuthoringSceneDefinition", source, StringComparison.Ordinal);
            Assert.Contains("Core.Instance.EntityFactory.Create", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the textured cube-grid factory now authors live entities instead of serialized editor scene records.
        /// </summary>
        [Fact]
        public void ReadTexturedCubeGridSceneFactorySource_DoesNotUseEditorSerializationHelpers() {
            string source = ReadCitySource("rendering.tools", "TexturedCubeGridSceneFactory.cs");

            Assert.DoesNotContain("using helengine.editor;", source, StringComparison.Ordinal);
            Assert.DoesNotContain("SceneComponentAssetRecord", source, StringComparison.Ordinal);
            Assert.DoesNotContain("MeshComponentPersistenceDescriptor", source, StringComparison.Ordinal);
            Assert.Contains("GeneratedAuthoringSceneDefinition", source, StringComparison.Ordinal);
            Assert.Contains("Core.Instance.EntityFactory.Create", source, StringComparison.Ordinal);
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

            Assert.Equal("scenes/rendering/axis_test2.helen", sceneAsset.Id);
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
            Assert.Equal(new float3(14f, 14f, 0.5f), wallEntity.LocalScale);
        }

        /// <summary>
        /// Ensures the authored spotlight street-slice scene references generated racer companion materials through their real `.hasset` assets instead of legacy sidecars.
        /// </summary>
        [Fact]
        public void DeserializeCitySpotlightStreetSliceSceneAsset_RacerMaterialReferencesDoNotUseLegacyMaterialSidecars() {
            SceneAsset sceneAsset = ReadSceneAsset("spotlight_street_slice.helen");

            Assert.DoesNotContain(
                sceneAsset.AssetReferences ?? Array.Empty<SceneAssetReference>(),
                reference => !string.IsNullOrWhiteSpace(reference.RelativePath)
                    && reference.RelativePath.Contains(".helmat.hasset", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                sceneAsset.AssetReferences ?? Array.Empty<SceneAssetReference>(),
                reference => string.Equals(reference.RelativePath, "models/Riemers/racer/x3ds_mat_ruedas.hasset", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Ensures every authored city rendering showcase scene carries one FPS overlay and the generated editor font reference it needs at runtime.
        /// </summary>
        [Fact]
        public void DeserializeCityRenderingSceneAssets_AllShowcaseScenesContainFpsOverlayAndEditorFontReference() {
            string[] sceneFileNames = new[] {
                "cube_test.helen",
                "colored_cube_grid.helen",
                "textured_cube_grid.helen",
                "axis_test.helen",
                "axis_test2.helen",
                "directional_shadow_plaza.helen",
                "spotlight_street_slice.helen"
            };

            for (int index = 0; index < sceneFileNames.Length; index++) {
                SceneAsset sceneAsset = ReadSceneAsset(sceneFileNames[index]);

                Assert.Equal(1, CountComponents(sceneAsset.RootEntities, "helengine.FPSComponent"));
                Assert.Contains(
                    sceneAsset.AssetReferences ?? Array.Empty<SceneAssetReference>(),
                    reference => string.Equals(reference.RelativePath, "generated/editor/fonts/ui.hefont", StringComparison.Ordinal)
                        && string.Equals(reference.ProviderId, "editor", StringComparison.Ordinal)
                        && string.Equals(reference.AssetId, "ui-font", StringComparison.Ordinal));
            }
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
        /// Reads one authored city source file from the project codebase.
        /// </summary>
        /// <param name="relativeFolderPath">Project-relative code folder beneath <c>assets/codebase</c>.</param>
        /// <param name="fileName">File name to read.</param>
        /// <returns>Source text.</returns>
        string ReadCitySource(string relativeFolderPath, string fileName) {
            if (string.IsNullOrWhiteSpace(relativeFolderPath)) {
                throw new ArgumentException("Relative folder path must be provided.", nameof(relativeFolderPath));
            } else if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("File name must be provided.", nameof(fileName));
            }

            string sourcePath = Path.Combine(CityProjectRootPath, "assets", "codebase", relativeFolderPath, fileName);
            Assert.True(File.Exists(sourcePath));
            return File.ReadAllText(sourcePath);
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
