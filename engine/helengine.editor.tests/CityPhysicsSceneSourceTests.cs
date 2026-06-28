namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored city physics scene source keeps the intended validation-scene layouts.
/// </summary>
public sealed class CityPhysicsSceneSourceTests {
    /// <summary>
    /// Ensures the authored city stack-box scene offsets each higher cube slightly farther along positive X.
    /// </summary>
    [Fact]
    public void City_dynamic_stack_boxes_source_uses_incremental_positive_x_offsets() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("CreatePhysicsBoxMeshEntity(\"dynamic_stack_boxes.box01\", \"StackBox01\", new float3(0f, 0.5f, 0f)", source, StringComparison.Ordinal);
        Assert.Contains("CreatePhysicsBoxMeshEntity(\"dynamic_stack_boxes.box02\", \"StackBox02\", new float3(0.5f, 1.5f, 0f)", source, StringComparison.Ordinal);
        Assert.Contains("CreatePhysicsBoxMeshEntity(\"dynamic_stack_boxes.box03\", \"StackBox03\", new float3(1.0f, 2.5f, 0f)", source, StringComparison.Ordinal);
        Assert.Contains("CreatePhysicsBoxMeshEntity(\"dynamic_stack_boxes.box04\", \"StackBox04\", new float3(1.5f, 3.5f, 0f)", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the authored city falling-cube scene keeps one static ground box and one elevated dynamic cube for the minimal BEPU repro.
    /// </summary>
    [Fact]
    public void City_single_falling_cube_source_uses_ground_and_elevated_dynamic_cube() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("CreatePhysicsBoxMeshEntity(\"single_falling_cube.ground\", \"Ground\", new float3(0f, -0.5f, 0f), new float3(14f, 1f, 14f), float4.Identity, StaticBodyKindCode, false", source, StringComparison.Ordinal);
        Assert.Contains("CreatePhysicsBoxMeshEntity(\"single_falling_cube.box01\", \"FallingCube\", new float3(0f, 5f, 0f), new float3(1f, 1f, 1f), float4.Identity, DynamicBodyKindCode, true", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the authored city dynamic sphere stack uses one shared tiled texture across distinct colored standard materials so sphere rotation reads clearly.
    /// </summary>
    [Fact]
    public void City_dynamic_sphere_stack_source_uses_shared_tiled_standard_materials() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("const string PhysicsDemoSphereTileTextureRelativePath = \"Images/physics/PhysicsDemoSphereTile.bmp\";", source, StringComparison.Ordinal);
        Assert.Contains("WriteSphereTileTextureAssets(projectRootPath);", source, StringComparison.Ordinal);
        Assert.Contains("WriteTexturedMaterialAsset(projectRootPath, PhysicsDemoSphereStackBlueMaterialRelativePath, \"PhysicsDemoSphereStackBlue\"", source, StringComparison.Ordinal);
        Assert.Contains("WriteTexturedMaterialAsset(projectRootPath, PhysicsDemoSphereStackPurpleMaterialRelativePath, \"PhysicsDemoSphereStackPurple\"", source, StringComparison.Ordinal);
        Assert.Contains("windowsSettings.SetFieldValue(TextureAssetIdFieldId, PhysicsDemoSphereTileTextureAssetId);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("const string PhysicsDemoShaderRelativePath", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the strict rotated-box comparison scene keeps one render-only control box beside one physics-backed rotated box.
    /// </summary>
    [Fact]
    public void City_strict_rotated_box_compare_scene_source_uses_control_and_physics_boxes_side_by_side() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("CreateStrictRotatedBoxCompareScene()", source, StringComparison.Ordinal);
        Assert.Contains("CreateCubeMeshEntity(\"strict_rotated_box_compare.control_box\", \"ControlVisualBox\"", source, StringComparison.Ordinal);
        Assert.Contains("CreatePhysicsBoxMeshEntity(\"strict_rotated_box_compare.physics_box\", \"PhysicsVisualBox\"", source, StringComparison.Ordinal);
        Assert.Contains("CreatePhysicsBoxMeshEntity(\"strict_rotated_box_compare.flat_box\", \"PhysicsFlatBox\"", source, StringComparison.Ordinal);
        Assert.Contains("CreatePhysicsSphereMeshEntity(\"strict_rotated_box_compare.physics_probe\", \"PhysicsProbe\"", source, StringComparison.Ordinal);
        Assert.Contains("PhysicsSceneCatalog.StrictRotatedBoxCompareSceneId", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the authored city mixed stack also assigns the tiled sphere-stack materials to its sphere layers so their rotation stays readable beside the box layers.
    /// </summary>
    [Fact]
    public void City_dynamic_mixed_stack_source_uses_tiled_sphere_materials_for_spheres() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("CreatePhysicsSphereMeshEntity(\"dynamic_mixed_stack.sphere01\", \"StackSphere01\", new float3(0.08f, 1.5f, -0.04f), float4.Identity, DynamicBodyKindCode, true, CreatePhysicsDemoMaterialReference(PhysicsDemoSphereStackGreenMaterialRelativePath))", source, StringComparison.Ordinal);
        Assert.Contains("CreatePhysicsSphereMeshEntity(\"dynamic_mixed_stack.sphere02\", \"StackSphere02\", new float3(0.05f, 3.5f, 0.08f), float4.Identity, DynamicBodyKindCode, true, CreatePhysicsDemoMaterialReference(PhysicsDemoSphereStackYellowMaterialRelativePath))", source, StringComparison.Ordinal);
        Assert.Contains("CreatePhysicsSphereMeshEntity(\"dynamic_mixed_stack.sphere03\", \"StackSphere03\", new float3(-0.05f, 5.5f, 0.04f), float4.Identity, DynamicBodyKindCode, true, CreatePhysicsDemoMaterialReference(PhysicsDemoSphereStackRedMaterialRelativePath))", source, StringComparison.Ordinal);
        Assert.Contains("CreatePhysicsSphereMeshEntity(\"dynamic_mixed_stack.sphere04\", \"StackSphere04\", new float3(-0.04f, 7.5f, -0.05f), float4.Identity, DynamicBodyKindCode, true, CreatePhysicsDemoMaterialReference(PhysicsDemoSphereStackPurpleMaterialRelativePath))", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures playable physics showcase scene reload uses an editor-configured asset content manager instead of the runtime core manager so textured file-backed materials can resolve imported textures.
    /// </summary>
    [Fact]
    public void City_playable_physics_showcase_scene_reload_uses_editor_asset_content_manager() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("ContentManager assetContentManager = new ContentManager(Path.Combine(projectRootPath, \"assets\"));", source, StringComparison.Ordinal);
        Assert.Contains("EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(assetContentManager);", source, StringComparison.Ordinal);
        Assert.Contains("EditorSceneAssetReferenceResolver referenceResolver = new EditorSceneAssetReferenceResolver(assetContentManager, projectRootPath);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new EditorSceneAssetReferenceResolver(Core.Instance.ContentManager, projectRootPath);", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the playable physics showcase UI attaches the shared light-indicator overlay beneath the same UI root that owns the light toggle.
    /// </summary>
    [Fact]
    public void City_playable_physics_showcase_ui_source_normalizes_editor_font_reference_and_attaches_shared_light_indicator_overlay() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("Font = ResolveRequiredEditorFont(),", source, StringComparison.Ordinal);
        Assert.Contains("entity.AddComponent(new city.rendering.DemoDiscLightToggleComponent());", source, StringComparison.Ordinal);
        Assert.Contains("ApplyEditorFontReference(entity, fpsComponent);", source, StringComparison.Ordinal);
        Assert.Contains("NormalizeGeneratedEditorFontReference(component, saveState);", source, StringComparison.Ordinal);
        Assert.Contains("DemoDiscLightIndicatorOverlayFactory lightIndicatorOverlayFactory = new DemoDiscLightIndicatorOverlayFactory();", source, StringComparison.Ordinal);
        Assert.Contains("lightIndicatorOverlayFactory.AttachToSceneUi(entity, ResolveRequiredEditorFont());", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the packaged playable physics showcase scene serializes the generated UI root so the light toggle keeps its authored indicator subtree.
    /// </summary>
    [Fact]
    public void City_playable_physics_showcase_scene_asset_source_serializes_generated_ui_root() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("EditorEntity physicsShowcaseUiEntity = CreateLivePhysicsShowcaseUiEntity();", source, StringComparison.Ordinal);
        Assert.Contains("rootEntities.Add(SerializeGeneratedEditorEntity(physicsShowcaseUiEntity, assetReferences, assetReferenceKeys));", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the manually serialized physics showcase entities keep the shared scene-object layer mask so the runtime camera can see their 3D drawables.
    /// </summary>
    [Fact]
    public void City_playable_physics_showcase_source_serializes_scene_object_layer_masks() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("LayerMask = EditorLayerMasks.SceneObjects,", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the city physics scene source emits mesh payload field names that match the current reflected mesh persistence contract.
    /// </summary>
    [Fact]
    public void City_playable_physics_showcase_source_uses_current_mesh_reference_field_names() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("const string MeshModelReferenceFieldName = \"Model\";", source, StringComparison.Ordinal);
        Assert.Contains("const string MeshMaterialReferencesFieldName = \"Materials\";", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the static-mesh follow camera resolves its tracked target from a serialized scene-entity reference and runtime scene ids.
    /// </summary>
    [Fact]
    public void City_static_mesh_follow_camera_source_uses_scene_entity_reference_and_runtime_id_lookup() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering\DemoFollowCameraComponent.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("public SceneEntityReference TargetEntityReference { get; set; }", source, StringComparison.Ordinal);
        Assert.Contains("SceneEntityRuntimeIdComponent", source, StringComparison.Ordinal);
        Assert.Contains("Core.Instance.ObjectManager.Entities", source, StringComparison.Ordinal);
        Assert.Contains("TargetEntityReference.EntityId", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the static-mesh showcase uses the dedicated follow camera for both packaged and direct-launch playable scene paths.
    /// </summary>
    [Fact]
    public void City_static_mesh_showcase_source_uses_demo_follow_camera_for_packaged_and_live_paths() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("CreateStaticMeshShowcaseCameraEntity(", source, StringComparison.Ordinal);
        Assert.Contains("CreateLiveStaticMeshShowcaseCameraEntity(", source, StringComparison.Ordinal);
        Assert.Contains("new city.rendering.DemoFollowCameraComponent", source, StringComparison.Ordinal);
        Assert.Contains("TargetEntityReference = new SceneEntityReference {", source, StringComparison.Ordinal);
        Assert.Contains("FindRequiredSceneEntityAssetByName(scenarioChildren, \"PlayerSphere\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateLivePhysicsShowcaseCameraEntity(\r\n                    \"StaticMeshShowcaseCamera\"", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the generated static-mesh showcase scene rebinds the follow camera after fresh live save ids are assigned so the serialized target reference matches the persisted player sphere.
    /// </summary>
    [Fact]
    public void City_static_mesh_showcase_source_rebinds_follow_camera_after_live_id_assignment() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("RebindStaticMeshShowcaseCameraTarget(cameraEntity, scenarioRoots);", source, StringComparison.Ordinal);
        Assert.Contains("EditorEntity playerSphereEntity = FindRequiredEditorEntityByName(scenarioRoots, \"PlayerSphere\");", source, StringComparison.Ordinal);
        Assert.Contains("EntitySaveComponent playerSphereSaveComponent = FindRequiredEntitySaveComponent(playerSphereEntity);", source, StringComparison.Ordinal);
        Assert.Contains("EntityId = playerSphereSaveComponent.EntityId", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the physics scene catalog exports the minimal static-mesh playable scene.
    /// </summary>
    [Fact]
    public void City_physics_scene_catalog_source_exports_static_mesh_minimal_scene() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneCatalog.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("public const string StaticMeshMinimalSceneId = \"scenes/physics/test_scene_static_mesh_minimal.helen\";", source, StringComparison.Ordinal);
        Assert.Contains("StaticMeshMinimalSceneId,", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the physics scene catalog exports the strict rotated-box comparison scene.
    /// </summary>
    [Fact]
    public void City_physics_scene_catalog_source_exports_strict_rotated_box_compare_scene() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneCatalog.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("public const string StrictRotatedBoxCompareSceneId = \"scenes/physics/test_scene_strict_rotated_box_compare.helen\";", source, StringComparison.Ordinal);
        Assert.Contains("StrictRotatedBoxCompareSceneId,", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the physics scene catalog and factory expose the render-only slope comparison scene used to isolate non-BEPU transform rendering.
    /// </summary>
    [Fact]
    public void City_render_only_slope_scene_source_is_exposed_by_catalog_and_factory() {
        string catalogSourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneCatalog.cs";
        string catalogSource = File.ReadAllText(catalogSourcePath);
        string factorySourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs";
        string factorySource = File.ReadAllText(factorySourcePath);

        Assert.Contains("public const string RenderOnlySlopeSceneId = \"scenes/physics/test_scene_render_only_slope.helen\";", catalogSource, StringComparison.Ordinal);
        Assert.Contains("RenderOnlySlopeSceneId,", catalogSource, StringComparison.Ordinal);
        Assert.Contains("CreateRenderOnlySlopeScene()", factorySource, StringComparison.Ordinal);
        Assert.Contains("PhysicsSceneCatalog.RenderOnlySlopeSceneId", factorySource, StringComparison.Ordinal);
        Assert.Contains("\"render_only_slope.ramp\"", factorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreatePhysicsBoxMeshEntity(\"render_only_slope.ramp\"", factorySource, StringComparison.Ordinal);
        Assert.Contains("CreateCubeMeshEntity(\"render_only_slope.ramp\"", factorySource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the render-matrix probe scene is exposed through the catalog and factory as a visual-only cube comparison.
    /// </summary>
    [Fact]
    public void City_render_matrix_probe_scene_source_is_exposed_as_render_only_visual_cases() {
        string catalogSourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneCatalog.cs";
        string catalogSource = File.ReadAllText(catalogSourcePath);
        string factorySourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs";
        string factorySource = File.ReadAllText(factorySourcePath);

        Assert.Contains("public const string RenderMatrixProbeSceneId = \"scenes/physics/test_scene_render_matrix_probe.helen\";", catalogSource, StringComparison.Ordinal);
        Assert.Contains("RenderMatrixProbeSceneId,", catalogSource, StringComparison.Ordinal);
        Assert.Contains("CreateRenderMatrixProbeScene()", factorySource, StringComparison.Ordinal);
        Assert.Contains("\"render_matrix_probe.flat_control\"", factorySource, StringComparison.Ordinal);
        Assert.Contains("\"render_matrix_probe.rotated_only\"", factorySource, StringComparison.Ordinal);
        Assert.Contains("\"render_matrix_probe.scaled_only\"", factorySource, StringComparison.Ordinal);
        Assert.Contains("\"render_matrix_probe.rotated_scaled\"", factorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreatePhysicsBoxMeshEntity(\"render_matrix_probe", factorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreatePhysicsSphereMeshEntity(\"render_matrix_probe", factorySource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the render-motion probe scene is exposed by the catalog and factory as a visual-only scene with one animated hero cube.
    /// </summary>
    [Fact]
    public void City_render_motion_probe_scene_source_is_exposed_as_visual_only_with_motion_probe_component() {
        string catalogSourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneCatalog.cs";
        string catalogSource = File.ReadAllText(catalogSourcePath);
        string factorySourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs";
        string factorySource = File.ReadAllText(factorySourcePath);

        Assert.Contains("public const string RenderMotionProbeSceneId = \"scenes/physics/test_scene_render_motion_probe.helen\";", catalogSource, StringComparison.Ordinal);
        Assert.Contains("RenderMotionProbeSceneId,", catalogSource, StringComparison.Ordinal);
        Assert.Contains("CreateRenderMotionProbeScene()", factorySource, StringComparison.Ordinal);
        Assert.Contains("\"render_motion_probe.hero\"", factorySource, StringComparison.Ordinal);
        Assert.Contains("\"render_motion_probe.flat_control\"", factorySource, StringComparison.Ordinal);
        Assert.Contains("\"render_motion_probe.rotated_scaled_reference\"", factorySource, StringComparison.Ordinal);
        Assert.Contains("new city.rendering.RenderMotionProbeComponent", factorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreatePhysicsBoxMeshEntity(\"render_motion_probe", factorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreatePhysicsSphereMeshEntity(\"render_motion_probe", factorySource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the minimal static-mesh playable scene keeps only a ground cube, player sphere, and follow-camera playable path.
    /// </summary>
    [Fact]
    public void City_static_mesh_minimal_scene_source_uses_ground_player_sphere_and_follow_camera() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("CreateStaticMeshMinimalScene()", source, StringComparison.Ordinal);
        Assert.Contains("CreatePhysicsBoxMeshEntity(", source, StringComparison.Ordinal);
        Assert.Contains("\"static_mesh_minimal.ground\",", source, StringComparison.Ordinal);
        Assert.Contains("\"Ground\",", source, StringComparison.Ordinal);
        Assert.Contains("\"static_mesh_minimal.player\",", source, StringComparison.Ordinal);
        Assert.Contains("\"PlayerSphere\",", source, StringComparison.Ordinal);
        Assert.Contains("PhysicsSceneCatalog.StaticMeshMinimalSceneId", source, StringComparison.Ordinal);
        Assert.Contains("\"test_scene_static_mesh_minimal\"", source, StringComparison.Ordinal);
        Assert.Contains("CreateLiveStaticMeshShowcaseCameraEntity(", source, StringComparison.Ordinal);
        Assert.Contains("RebindStaticMeshShowcaseCameraTarget(cameraEntity, scenarioRoots);", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the minimal static-mesh scene is exposed through the demo-disc physics catalog and Nintendo DS playable-scene generator.
    /// </summary>
    [Fact]
    public void City_static_mesh_minimal_scene_source_is_exposed_to_menu_and_ds_generation() {
        string sceneCatalogSourcePath = @"C:\dev\helprojs\city\assets\codebase\menu\DemoDiscSceneCatalog.cs";
        string sceneCatalogSource = File.ReadAllText(sceneCatalogSourcePath);
        string dsGeneratorSourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsNintendoDsSceneGenerator.cs";
        string dsGeneratorSource = File.ReadAllText(dsGeneratorSourcePath);

        Assert.Contains("\"test_scene_static_mesh_minimal\"", sceneCatalogSource, StringComparison.Ordinal);
        Assert.Contains("\"test_scene_static_mesh_minimal_ds\"", sceneCatalogSource, StringComparison.Ordinal);
        Assert.Contains("\"test_scene_static_mesh_minimal\"", dsGeneratorSource, StringComparison.Ordinal);
    }
}
