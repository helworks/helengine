using helengine.files;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the city-owned demo-disc scene generation flow persists the intended showcase scenes and menu overlays.
    /// </summary>
    public sealed class CityRenderingSceneAuthoringTests {
        /// <summary>
        /// Default local city project path used by environment-backed rendering-scene regressions.
        /// </summary>
        const string DefaultCityProjectRootPath = @"C:\dev\helprojs\city";

        /// <summary>
        /// Gets the city project path used by source and authored-asset regressions.
        /// </summary>
        static string CityProjectRootPath {
            get {
                string environmentProjectRootPath = Environment.GetEnvironmentVariable("HELENGINE_CITY_PROJECT_ROOT");
                if (string.IsNullOrWhiteSpace(environmentProjectRootPath)) {
                    return DefaultCityProjectRootPath;
                }

                return environmentProjectRootPath;
            }
        }

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
        /// Ensures the city physics generation command uses the city-owned physics scene generator.
        /// </summary>
        [Fact]
        public void ReadCityGeneratePhysicsScenesCommandSource_UsesPhysicsSceneGenerator() {
            string source = ReadCitySource("menu.tools", "GeneratePhysicsScenesCommand.cs");

            Assert.Contains("new PhysicsSceneGenerator()", source, StringComparison.Ordinal);
            Assert.DoesNotContain("PhysicsValidationSceneFactory", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the city physics scene generator no longer delegates generated scene ownership back to the editor validation factory.
        /// </summary>
        [Fact]
        public void ReadCityPhysicsSceneGeneratorSource_UsesCityPhysicsSceneFactory() {
            string source = ReadCitySource("physics.tools", "PhysicsSceneGenerator.cs");

            Assert.Contains("new PhysicsSceneFactory()", source, StringComparison.Ordinal);
            Assert.Contains("factory.WriteScenes(projectRootPath);", source, StringComparison.Ordinal);
            Assert.DoesNotContain("PhysicsValidationSceneFactory", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the city physics tools module owns the generated physics scene catalog source.
        /// </summary>
        [Fact]
        public void ReadCityPhysicsSceneCatalogSource_DeclaresStablePhysicsSceneIds() {
            string source = ReadCitySource("physics.tools", "PhysicsSceneCatalog.cs");

            Assert.Contains("public static class PhysicsSceneCatalog", source, StringComparison.Ordinal);
            Assert.Contains("CharacterSlopeSceneId", source, StringComparison.Ordinal);
            Assert.Contains("DynamicStackBoxesSceneId", source, StringComparison.Ordinal);
            Assert.Contains("TriggerVolumeSceneId", source, StringComparison.Ordinal);
            Assert.Contains("public static string[] GetSceneIds()", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the city physics tools module owns the generated physics scene factory source.
        /// </summary>
        [Fact]
        public void ReadCityPhysicsSceneFactorySource_DeclaresCitySceneFactory() {
            string source = ReadCitySource("physics.tools", "PhysicsSceneFactory.cs");

            Assert.Contains("public sealed class PhysicsSceneFactory", source, StringComparison.Ordinal);
            Assert.Contains("public SceneAsset CreateSceneAsset(string sceneId)", source, StringComparison.Ordinal);
            Assert.Contains("public void WriteScenes(string projectRootPath)", source, StringComparison.Ordinal);
            Assert.DoesNotContain("PhysicsValidationSceneFactory", source, StringComparison.Ordinal);
            Assert.DoesNotContain("PhysicsValidationSceneCatalog", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the generated city physics scenes exist as normal authored project scene assets.
        /// </summary>
        [Fact]
        public void DeserializeCityPhysicsScenes_AllGeneratedPhysicsScenesExist() {
            string[] sceneFileNames = new[] {
                "test_scene_character_slope.helen",
                "test_scene_character_steps.helen",
                "test_scene_character_moving_platform.helen",
                "test_scene_dynamic_stack_boxes.helen",
                "test_scene_dynamic_sphere_ramp.helen",
                "test_scene_kinematic_push.helen",
                "test_scene_mesh_ground_stability.helen",
                "test_scene_trigger_volume.helen"
            };

            for (int index = 0; index < sceneFileNames.Length; index++) {
                SceneAsset sceneAsset = ReadPhysicsSceneAsset(sceneFileNames[index]);

                Assert.Contains(sceneAsset.RootEntities, entity => string.Equals(entity.Name, "Camera", StringComparison.Ordinal));
                Assert.Contains(sceneAsset.RootEntities, entity => string.Equals(entity.Name, "Scenario", StringComparison.Ordinal));
            }
        }

        /// <summary>
        /// Ensures every generated city physics scene includes a debug overlay for runtime crash and freeze inspection.
        /// </summary>
        [Fact]
        public void DeserializeCityPhysicsScenes_AllGeneratedPhysicsScenesContainDebugComponent() {
            string[] sceneFileNames = new[] {
                "test_scene_character_slope.helen",
                "test_scene_character_steps.helen",
                "test_scene_character_moving_platform.helen",
                "test_scene_dynamic_stack_boxes.helen",
                "test_scene_dynamic_sphere_ramp.helen",
                "test_scene_kinematic_push.helen",
                "test_scene_mesh_ground_stability.helen",
                "test_scene_trigger_volume.helen"
            };

            for (int index = 0; index < sceneFileNames.Length; index++) {
                SceneAsset sceneAsset = ReadPhysicsSceneAsset(sceneFileNames[index]);

                Assert.Equal(1, CountComponents(sceneAsset.RootEntities, "helengine.DebugComponent"));
            }
        }

        /// <summary>
        /// Ensures the first city physics scene builds a readable two-box offset stack over a static ground body.
        /// </summary>
        [Fact]
        public void DeserializeCityDynamicStackBoxesPhysicsScene_ContainsTwoBoxOffsetStackAndGround() {
            SceneAsset sceneAsset = ReadPhysicsSceneAsset("test_scene_dynamic_stack_boxes.helen");

            Assert.Equal(3, CountComponents(sceneAsset.RootEntities, "helengine.RigidBody3DComponent"));
            Assert.Equal(3, CountComponents(sceneAsset.RootEntities, "helengine.BoxCollider3DComponent"));
            SceneEntityAsset firstBoxEntity = FindEntityByName(sceneAsset.RootEntities, "StackBox01");
            SceneEntityAsset secondBoxEntity = FindEntityByName(sceneAsset.RootEntities, "StackBox02");

            Assert.InRange(firstBoxEntity.LocalPosition.X, -0.35f, -0.33f);
            Assert.InRange(firstBoxEntity.LocalPosition.Z, -0.07f, -0.05f);
            Assert.InRange(secondBoxEntity.LocalPosition.X, 0.49f, 0.51f);
            Assert.InRange(secondBoxEntity.LocalPosition.Z, 0.05f, 0.07f);
            Assert.True(HasHorizontalBoxOverlap(firstBoxEntity, secondBoxEntity));
            Assert.True(HasReadableSideOffset(firstBoxEntity, secondBoxEntity));
            Assert.InRange(ResolveUnitBoxHorizontalOverlapX(firstBoxEntity, secondBoxEntity), 0.15d, 0.17d);
            Assert.Throws<InvalidOperationException>(() => FindEntityByName(sceneAsset.RootEntities, "StackBox03"));
            Assert.Throws<InvalidOperationException>(() => FindEntityByName(sceneAsset.RootEntities, "StackBox04"));
        }

        /// <summary>
        /// Ensures a representative city character physics scene contains serialized character-controller records.
        /// </summary>
        [Fact]
        public void DeserializeCityCharacterSlopePhysicsScene_ContainsCharacterControllerRecord() {
            SceneAsset sceneAsset = ReadPhysicsSceneAsset("test_scene_character_slope.helen");

            Assert.True(CountComponents(sceneAsset.RootEntities, "helengine.CharacterController3DComponent") >= 1);
        }

        /// <summary>
        /// Ensures the city physics generation flow emits the shared support shader and materials beside the project assets.
        /// </summary>
        [Fact]
        public void ReadCityPhysicsSupportAssets_EmitsShaderAndMaterials() {
            string shaderPath = Path.Combine(CityProjectRootPath, "assets", "Shaders", "physics", "PhysicsDemoMesh.hlsl");
            string neutralMaterialPath = Path.Combine(CityProjectRootPath, "assets", "Materials", "physics", "PhysicsDemoNeutral.hasset");
            string blueMaterialPath = Path.Combine(CityProjectRootPath, "assets", "Materials", "physics", "PhysicsDemoBlue.hasset");

            Assert.True(File.Exists(shaderPath));
            Assert.True(File.Exists(neutralMaterialPath));
            Assert.True(File.Exists(blueMaterialPath));
            Assert.Contains("cbuffer MaterialColorBuffer", File.ReadAllText(shaderPath), StringComparison.Ordinal);
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
        /// Ensures the cube-test sun remains static so the lighting test isolates rotating mesh normals.
        /// </summary>
        [Fact]
        public void ReadCityCubeTestSceneFactorySource_KeepsSunStatic() {
            string source = ReadCitySource("rendering.tools", "CubeTestSceneFactory.cs");

            int sunStartIndex = source.IndexOf("Entity CreateDirectionalLightEntity()", StringComparison.Ordinal);
            int cubeStartIndex = source.IndexOf("Entity CreateCubeEntity", StringComparison.Ordinal);
            Assert.True(sunStartIndex >= 0);
            Assert.True(cubeStartIndex > sunStartIndex);

            string sunSource = source.Substring(sunStartIndex, cubeStartIndex - sunStartIndex);
            Assert.DoesNotContain("entity.AddComponent(new AxisRotationComponent", sunSource, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the generated authoring-scene definition exposes explicit Nintendo DS companion-scene metadata.
        /// </summary>
        [Fact]
        public void ReadCityGeneratedAuthoringSceneDefinitionSource_DeclaresDsCompanionSceneMetadata() {
            string source = ReadCitySource("rendering.tools", "GeneratedAuthoringSceneDefinition.cs");

            Assert.Contains("public GeneratedDsSceneDefinition NintendoDsScene { get; set; }", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the generated Nintendo DS companion-scene definition exposes explicit bottom-overlay customization metadata.
        /// </summary>
        [Fact]
        public void ReadCityGeneratedDsSceneDefinitionSource_DeclaresBottomOverlayCustomizationContract() {
            string source = ReadCitySource("rendering.tools", "GeneratedDsSceneDefinition.cs");

            Assert.Contains("public string SceneId { get; set; }", source, StringComparison.Ordinal);
            Assert.Contains("public bool UseDefaultBottomOverlay { get; set; }", source, StringComparison.Ordinal);
            Assert.Contains("public Entity[] BottomScreenRootEntities { get; set; }", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the shared Nintendo DS scaffold helper builds the expected dual-screen camera and default overlay shape.
        /// </summary>
        [Fact]
        public void ReadCityNintendoDsRenderingSceneScaffoldFactorySource_BuildsDualScreenDebugAndBackOverlay() {
            string source = ReadCitySource("rendering.tools", "NintendoDsRenderingSceneScaffoldFactory.cs");

            Assert.Contains("DemoDiscTopScreenCamera", source, StringComparison.Ordinal);
            Assert.Contains("DemoDiscBottomScreenCamera", source, StringComparison.Ordinal);
            Assert.Contains("new DebugComponent()", source, StringComparison.Ordinal);
            Assert.Contains("RemoveReturnToMenuComponents(entity);", source, StringComparison.Ordinal);
            Assert.Contains("void RemoveReturnToMenuComponents(Entity entity)", source, StringComparison.Ordinal);
            Assert.Contains("new InteractableComponent", source, StringComparison.Ordinal);
            Assert.Contains("new NintendoDsReturnOverlayComponent()", source, StringComparison.Ordinal);
            Assert.DoesNotContain("new DemoDiscReturnToMenuComponent", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the city-owned Nintendo DS return overlay component owns both the DS back-button bind and pointer click return flow.
        /// </summary>
        [Fact]
        public void ReadCityNintendoDsReturnOverlayComponentSource_OwnsDsBackButtonAndPointerReturn() {
            string source = ReadCitySource("menu", "NintendoDsReturnOverlayComponent.cs");

            Assert.Contains("public sealed class NintendoDsReturnOverlayComponent", source, StringComparison.Ordinal);
            Assert.Contains("WasGamepadButtonPressed(0, InputGamepadButton.East)", source, StringComparison.Ordinal);
            Assert.Contains("BoundInteractable.CursorEvent += HandleCursorEvent;", source, StringComparison.Ordinal);
            Assert.Contains("throw new InvalidOperationException(\"NintendoDsReturnOverlayComponent requires a sibling InteractableComponent.\")", source, StringComparison.Ordinal);
            Assert.Contains("SceneMapComponent.ResolveSceneId(MainMenuSceneId)", source, StringComparison.Ordinal);
            Assert.Contains("SceneLoadWasRequested", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the rendering-scene generator declares stable Nintendo DS companion-scene identifiers for the generated showcase set.
        /// </summary>
        [Fact]
        public void ReadCityRenderingSceneGeneratorSource_DeclaresDsCompanionSceneIds() {
            string source = ReadCitySource("rendering.tools", "RenderingSceneGenerator.cs");

            Assert.Contains("CubeTestNintendoDsSceneId", source, StringComparison.Ordinal);
            Assert.Contains("AxisTestNintendoDsSceneId", source, StringComparison.Ordinal);
            Assert.Contains("SceneMemoryProbeNintendoDsSceneId", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the textured cube-grid material generator authors Nintendo DS texture paths instead of falling back to untextured defaults.
        /// </summary>
        [Fact]
        public void ReadCityTexturedCubeGridSceneFactorySource_ProvidesNintendoDsTextureMaterialSettings() {
            string source = ReadCitySource("rendering.tools", "TexturedCubeGridSceneFactory.cs");

            Assert.Contains("const string DsMaterialSchemaId = \"ds-standard-textured\";", source, StringComparison.Ordinal);
            Assert.Contains("const string DsTextureRelativePathFieldId = \"texture-relative-path\";", source, StringComparison.Ordinal);
            Assert.Contains("const string LightingModeFieldId = \"lighting-mode\";", source, StringComparison.Ordinal);
            Assert.Contains("dsSettings.SchemaId = DsMaterialSchemaId;", source, StringComparison.Ordinal);
            Assert.Contains("dsSettings.FieldValues[TextureIdFieldId] = CubeTextureAssetIds[cubeIndex];", source, StringComparison.Ordinal);
            Assert.Contains("dsSettings.FieldValues[DsTextureRelativePathFieldId] = \"cooked/imported/\" + CubeTextureAssetIds[cubeIndex];", source, StringComparison.Ordinal);
            Assert.Contains("dsSettings.FieldValues[LightingModeFieldId] = \"lit\";", source, StringComparison.Ordinal);
            Assert.Contains("settings.Processor.Platforms[\"ds\"] = dsSettings;", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the cube-test scene factory attaches explicit Nintendo DS companion-scene metadata.
        /// </summary>
        [Fact]
        public void ReadCityCubeTestSceneFactorySource_AttachesNintendoDsSceneDefinition() {
            string source = ReadCitySource("rendering.tools", "CubeTestSceneFactory.cs");

            Assert.Contains("NintendoDsScene = new GeneratedDsSceneDefinition", source, StringComparison.Ordinal);
            Assert.Contains("UseDefaultBottomOverlay = true", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the authored axis-test showcase stores explicit file-backed save references for the directional-light arrow mesh.
        /// </summary>
        [Fact]
        public void ReadCityAxisTestSceneFactorySource_AttachesExplicitArrowMeshSaveReferences() {
            string source = ReadCitySource("rendering.tools", "AxisTestSceneFactory.cs");

            Assert.Contains("ApplyArrowMeshAssetReferences(entity);", source, StringComparison.Ordinal);
            Assert.Contains("MeshModelReferenceName", source, StringComparison.Ordinal);
            Assert.Contains("CreateFileReference(ArrowModelRelativePath)", source, StringComparison.Ordinal);
            Assert.Contains("CreateFileReference(MarkerMaterialRelativePath)", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the authored axis-test-2 showcase stores explicit file-backed save references for the directional-light arrow mesh.
        /// </summary>
        [Fact]
        public void ReadCityAxisTest2SceneFactorySource_AttachesExplicitArrowMeshSaveReferences() {
            string source = ReadCitySource("rendering.tools", "AxisTest2SceneFactory.cs");

            Assert.Contains("ApplyArrowMeshAssetReferences(entity);", source, StringComparison.Ordinal);
            Assert.Contains("MeshModelReferenceName", source, StringComparison.Ordinal);
            Assert.Contains("CreateFileReference(ArrowModelRelativePath)", source, StringComparison.Ordinal);
            Assert.Contains("CreateFileReference(MarkerMaterialRelativePath)", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the spotlight street-slice imported meshes store explicit file-backed model and racer-material save references.
        /// </summary>
        [Fact]
        public void ReadCitySpotlightStreetSliceSceneFactorySource_AttachesExplicitImportedMeshSaveReferences() {
            string source = ReadCitySource("rendering.tools", "SpotlightStreetSliceSceneFactory.cs");

            Assert.Contains("LamppostModelRelativePath", source, StringComparison.Ordinal);
            Assert.Contains("RacerModelRelativePath", source, StringComparison.Ordinal);
            Assert.Contains("RacerMaterialRelativePaths", source, StringComparison.Ordinal);
            Assert.Contains("ApplyImportedMeshAssetReferences(entity, meshComponent, modelRelativePath, materialRelativePaths);", source, StringComparison.Ordinal);
            Assert.Contains("saveComponent.SetAssetReference(component, MeshModelReferenceName, CreateFileReference(modelRelativePath));", source, StringComparison.Ordinal);
            Assert.Contains("BuildMaterialReferenceName(materialIndex)", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the generated authoring-scene writer emits Nintendo DS companion scenes through the shared scaffold helper.
        /// </summary>
        [Fact]
        public void ReadCityGeneratedAuthoringSceneWriteServiceSource_WritesNintendoDsCompanionScenes() {
            string source = ReadCitySource("rendering.tools", "GeneratedAuthoringSceneWriteService.cs");

            Assert.Contains("sceneDefinition.NintendoDsScene", source, StringComparison.Ordinal);
            Assert.Contains("NintendoDsRenderingSceneScaffoldFactory", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the rendering-scene asset preparation path prefers one preview-capable Windows material platform instead of the last active project platform.
        /// </summary>
        [Fact]
        public void ReadCityRenderingSceneAssetPreparationServiceSource_PrefersWindowsPreviewPlatformForMaterialResolution() {
            string source = ReadCitySource("rendering.tools", "RenderingSceneAssetPreparationService.cs");

            Assert.Contains("const string PreferredEditorPreviewPlatformId = \"windows\";", source, StringComparison.Ordinal);
            Assert.Contains("ResolveMaterialPreviewPlatformId", source, StringComparison.Ordinal);
            Assert.Contains("supportedPlatforms[index], PreferredEditorPreviewPlatformId", source, StringComparison.Ordinal);
            Assert.Contains("BuildPreviewRuntimeMaterial", source, StringComparison.Ordinal);
            Assert.Contains("if (string.IsNullOrWhiteSpace(materialAsset.ShaderAssetId))", source, StringComparison.Ordinal);
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
        /// Ensures the authored demo-disc runtime binder component populates the platform name and version strings on the generated child text rows.
        /// </summary>
        [Fact]
        public void ReadCityPlatformInfoTextComponentSource_UsesPlatformInfoValues() {
            string source = ReadCitySource("menu", "PlatformInfoTextComponent.cs");

            Assert.Contains("Core.Instance.PlatformInfo.Name", source, StringComparison.Ordinal);
            Assert.Contains("Core.Instance.PlatformInfo.Version", source, StringComparison.Ordinal);
            Assert.Contains("FindRequiredChildEntity(entity, 0)", source, StringComparison.Ordinal);
            Assert.Contains("FindRequiredChildEntity(entity, 1)", source, StringComparison.Ordinal);
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
        /// Ensures the authored city probe scene contains one fixed non-looping main-menu versus cube-test memory probe sequence.
        /// </summary>
        [Fact]
        public void DeserializeCitySceneMemoryProbeSceneAsset_RootContainsSceneMemoryProbeComponentWithSteps() {
            SceneAsset sceneAsset = ReadSceneAsset("scene_memory_probe.helen");
            string componentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(SceneMemoryProbeComponent));
            SceneEntityAsset probeRoot = Assert.Single(
                sceneAsset.RootEntities,
                entity => (entity.Components ?? Array.Empty<SceneComponentAssetRecord>())
                    .Any(component => string.Equals(component.ComponentTypeId, componentTypeId, StringComparison.Ordinal)));
            SceneComponentAssetRecord componentRecord = Assert.Single(
                probeRoot.Components ?? Array.Empty<SceneComponentAssetRecord>(),
                component => string.Equals(component.ComponentTypeId, componentTypeId, StringComparison.Ordinal));
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            SceneMemoryProbeComponent component = Assert.IsType<SceneMemoryProbeComponent>(
                descriptor.DeserializeComponent(componentRecord, null, null));

            Assert.False(component.Loop);
            Assert.True(component.StartAutomatically);
            Assert.Equal("menu-cube-memory-probe", component.ProbeName);
            Assert.Equal(82, component.Steps.Length);
            for (int roundTripIndex = 1; roundTripIndex <= 20; roundTripIndex++) {
                int stepIndex = (roundTripIndex - 1) * 4;
                AssertProbeStep(component.Steps[stepIndex], SceneMemoryProbeActionKind.LoadSceneSingle, "DemoDiscMainMenu", 0d, "load-menu-" + roundTripIndex);
                AssertProbeStep(component.Steps[stepIndex + 1], SceneMemoryProbeActionKind.Wait, string.Empty, 10.0d, "idle-menu-" + roundTripIndex);
                AssertProbeStep(component.Steps[stepIndex + 2], SceneMemoryProbeActionKind.LoadSceneSingle, "cube_test", 0d, "load-cube-" + roundTripIndex);
                AssertProbeStep(component.Steps[stepIndex + 3], SceneMemoryProbeActionKind.Wait, string.Empty, 10.0d, "idle-cube-" + roundTripIndex);
            }

            AssertProbeStep(component.Steps[80], SceneMemoryProbeActionKind.LoadSceneSingle, "DemoDiscMainMenu", 0d, "load-menu-21");
            AssertProbeStep(component.Steps[81], SceneMemoryProbeActionKind.Wait, string.Empty, 10.0d, "idle-menu-21");
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
        /// Reads one generated city physics scene asset from the authored project scene folder.
        /// </summary>
        /// <param name="sceneFileName">File name of the authored physics scene.</param>
        /// <returns>Deserialized scene asset.</returns>
        SceneAsset ReadPhysicsSceneAsset(string sceneFileName) {
            string scenePath = Path.Combine(CityProjectRootPath, "assets", "scenes", "physics", sceneFileName);
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

        /// <summary>
        /// Finds one named entity in a serialized scene hierarchy and fails the test when it is missing.
        /// </summary>
        /// <param name="entities">Scene entities to inspect.</param>
        /// <param name="entityName">Authored entity name to find.</param>
        /// <returns>Matching scene entity.</returns>
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
                }

                if (string.Equals(entity.Name, entityName, StringComparison.Ordinal)) {
                    return entity;
                }

                SceneEntityAsset match = FindEntityByNameOrDefault(entity.Children ?? Array.Empty<SceneEntityAsset>(), entityName);
                if (match != null) {
                    return match;
                }
            }

            throw new InvalidOperationException($"Scene entity '{entityName}' was not found.");
        }

        /// <summary>
        /// Finds one named entity in a serialized scene hierarchy when present.
        /// </summary>
        /// <param name="entities">Scene entities to inspect.</param>
        /// <param name="entityName">Authored entity name to find.</param>
        /// <returns>Matching scene entity when present; otherwise null.</returns>
        SceneEntityAsset FindEntityByNameOrDefault(SceneEntityAsset[] entities, string entityName) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            } else if (string.IsNullOrWhiteSpace(entityName)) {
                throw new ArgumentException("Entity name must be provided.", nameof(entityName));
            }

            for (int index = 0; index < entities.Length; index++) {
                SceneEntityAsset entity = entities[index];
                if (entity == null) {
                    continue;
                }

                if (string.Equals(entity.Name, entityName, StringComparison.Ordinal)) {
                    return entity;
                }

                SceneEntityAsset match = FindEntityByNameOrDefault(entity.Children ?? Array.Empty<SceneEntityAsset>(), entityName);
                if (match != null) {
                    return match;
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether two unit-box scene entities overlap in both horizontal axes at spawn time.
        /// </summary>
        /// <param name="firstEntity">First authored unit-box entity.</param>
        /// <param name="secondEntity">Second authored unit-box entity.</param>
        /// <returns>True when the two authored unit boxes can vertically support each other at spawn time.</returns>
        static bool HasHorizontalBoxOverlap(SceneEntityAsset firstEntity, SceneEntityAsset secondEntity) {
            if (firstEntity == null) {
                throw new ArgumentNullException(nameof(firstEntity));
            } else if (secondEntity == null) {
                throw new ArgumentNullException(nameof(secondEntity));
            }

            return Math.Abs(firstEntity.LocalPosition.X - secondEntity.LocalPosition.X) < 1f &&
                Math.Abs(firstEntity.LocalPosition.Z - secondEntity.LocalPosition.Z) < 1f;
        }

        /// <summary>
        /// Resolves the authored horizontal X-axis overlap between two unit boxes so edge-contact scenes can be validated directly.
        /// </summary>
        /// <param name="firstEntity">First authored unit-box entity.</param>
        /// <param name="secondEntity">Second authored unit-box entity.</param>
        /// <returns>Positive overlap distance along the X axis for two unit-wide boxes.</returns>
        static double ResolveUnitBoxHorizontalOverlapX(SceneEntityAsset firstEntity, SceneEntityAsset secondEntity) {
            if (firstEntity == null) {
                throw new ArgumentNullException(nameof(firstEntity));
            } else if (secondEntity == null) {
                throw new ArgumentNullException(nameof(secondEntity));
            }

            return 1d - Math.Abs((double)firstEntity.LocalPosition.X - secondEntity.LocalPosition.X);
        }

        /// <summary>
        /// Determines whether two unit-box scene entities are visibly staggered instead of being perfectly centered.
        /// </summary>
        /// <param name="firstEntity">First authored unit-box entity.</param>
        /// <param name="secondEntity">Second authored unit-box entity.</param>
        /// <returns>True when the authored boxes have a visible horizontal center offset.</returns>
        static bool HasReadableSideOffset(SceneEntityAsset firstEntity, SceneEntityAsset secondEntity) {
            if (firstEntity == null) {
                throw new ArgumentNullException(nameof(firstEntity));
            } else if (secondEntity == null) {
                throw new ArgumentNullException(nameof(secondEntity));
            }

            return Math.Abs(firstEntity.LocalPosition.X - secondEntity.LocalPosition.X) > 0.5f ||
                Math.Abs(firstEntity.LocalPosition.Z - secondEntity.LocalPosition.Z) > 0.5f;
        }

        /// <summary>
        /// Verifies one authored probe step matches the expected action, scene id, duration, and label.
        /// </summary>
        /// <param name="step">Authored probe step to validate.</param>
        /// <param name="actionKind">Expected action kind.</param>
        /// <param name="sceneId">Expected scene id.</param>
        /// <param name="durationSeconds">Expected duration in seconds.</param>
        /// <param name="label">Expected stable step label.</param>
        static void AssertProbeStep(SceneMemoryProbeStep step, SceneMemoryProbeActionKind actionKind, string sceneId, double durationSeconds, string label) {
            Assert.NotNull(step);
            Assert.Equal(actionKind, step.ActionKind);
            Assert.Equal(sceneId, step.SceneId);
            Assert.Equal(durationSeconds, step.DurationSeconds);
            Assert.Equal(label, step.Label);
        }
    }
}
