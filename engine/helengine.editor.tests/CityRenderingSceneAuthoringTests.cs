using helengine.files;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the city-owned demo-disc scene generation flow persists the intended showcase scenes and menu overlays.
    /// </summary>
    public sealed class CityRenderingSceneAuthoringTests {
        /// <summary>
        /// Gets the local city project path used by environment-backed rendering-scene regressions.
        /// </summary>
        const string CityProjectRootPath = @"C:\dev\helprojs\city";

        /// <summary>
        /// Ensures the authored city demo-disc scene catalog exposes the intended showcase lineup before the back action.
        /// </summary>
        [Fact]
        public void ReadCityDemoDiscSceneCatalogSource_ListsRestoredShowcaseLineupBeforeBack() {
            string sceneCatalogSource = ReadDemoDiscSceneCatalogSource();

            int cubeTestIndex = sceneCatalogSource.IndexOf("\"cube_test\"", StringComparison.Ordinal);
            int coloredCubeGridIndex = sceneCatalogSource.IndexOf("\"colored_cube_grid\"", StringComparison.Ordinal);
            int texturedCubeGridIndex = sceneCatalogSource.IndexOf("\"textured_cube_grid\"", StringComparison.Ordinal);
            int axisTestIndex = sceneCatalogSource.IndexOf("\"axis_test\"", StringComparison.Ordinal);
            int axisTest2Index = sceneCatalogSource.IndexOf("\"axis_test2\"", StringComparison.Ordinal);
            int directionalShadowPlazaIndex = sceneCatalogSource.IndexOf("\"directional_shadow_plaza\"", StringComparison.Ordinal);
            int spotlightStreetSliceIndex = sceneCatalogSource.IndexOf("\"spotlight_street_slice\"", StringComparison.Ordinal);
            int backIndex = sceneCatalogSource.IndexOf("MenuActionKind.Back", StringComparison.Ordinal);

            Assert.True(cubeTestIndex >= 0);
            Assert.True(coloredCubeGridIndex > cubeTestIndex);
            Assert.True(texturedCubeGridIndex > coloredCubeGridIndex);
            Assert.True(axisTestIndex > texturedCubeGridIndex);
            Assert.True(axisTest2Index > axisTestIndex);
            Assert.True(directionalShadowPlazaIndex > axisTest2Index);
            Assert.True(spotlightStreetSliceIndex > directionalShadowPlazaIndex);
            Assert.True(backIndex > spotlightStreetSliceIndex);
        }

        /// <summary>
        /// Ensures the city rendering generation command uses the full city-owned rendering scene generator.
        /// </summary>
        [Fact]
        public void ReadCityGenerateRenderingScenesCommandSource_UsesRenderingSceneGenerator() {
            string source = ReadCitySource("menu.tools", "GenerateRenderingScenesCommand.cs");

            Assert.DoesNotContain("helengine.demo_disc_scene_writer", source, StringComparison.Ordinal);
            Assert.Contains("new RenderingSceneGenerator()", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DemoDiscRenderingSceneGenerationService", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the cube-test authored scene factory includes the demo-disc return component so packaged console builds can navigate back to the main menu.
        /// </summary>
        [Fact]
        public void ReadCityCubeTestSceneFactorySource_AttachesDemoDiscReturnToMenuComponent() {
            string source = ReadCitySource("rendering.tools", "CubeTestSceneFactory.cs");

            Assert.Contains("DemoDiscReturnToMenuComponent", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the city demo-disc menu regeneration command is owned by project code instead of the editor-only engine regeneration service.
        /// </summary>
        [Fact]
        public void ReadCityMenuRegenerationCommandSource_DoesNotDependOnEngineMenuSceneRegenerationService() {
            string source = ReadCitySource("menu.tools", "RegenerateDemoDiscMainMenuCommand.cs");

            Assert.DoesNotContain("MenuSceneRegenerationService", source, StringComparison.Ordinal);
            Assert.Contains("DemoDiscSceneGenerator", source, StringComparison.Ordinal);
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
        /// Ensures the two authored city demo-disc rendering showcase scenes carry one FPS overlay and the generated editor font reference they need at runtime.
        /// </summary>
        [Fact]
        public void DeserializeCityRenderingSceneAssets_DemoDiscShowcasesContainFpsOverlayAndEditorFontReference() {
            string[] sceneFileNames = new[] {
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
        /// Ensures the authored top-level demo-disc menu scene carries the FPS overlay under the generated fitted UI subtree.
        /// </summary>
        [Fact]
        public void DeserializeCityDemoDiscMainMenuSceneAsset_GeneratedRootContainsFpsComponent() {
            SceneAsset sceneAsset = ReadTopLevelSceneAsset("DemoDiscMainMenu.helen");
            SceneEntityAsset menuRoot = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
            SceneEntityAsset generatedRoot = Assert.Single(menuRoot.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);

            Assert.Contains(
                generatedRoot.Components ?? Array.Empty<SceneComponentAssetRecord>(),
                component => string.Equals(component.ComponentTypeId, "helengine.FPSComponent", StringComparison.Ordinal));
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
        /// Reads one authored top-level city scene asset from the project scene folder.
        /// </summary>
        /// <param name="sceneFileName">File name of the authored top-level scene.</param>
        /// <returns>Deserialized scene asset.</returns>
        SceneAsset ReadTopLevelSceneAsset(string sceneFileName) {
            string scenePath = Path.Combine(CityProjectRootPath, "assets", "Scenes", sceneFileName);
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
    }
}
