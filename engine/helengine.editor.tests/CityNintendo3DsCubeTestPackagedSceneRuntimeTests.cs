using helengine.editor.tests.testing;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the current packaged Nintendo 3DS cube-test scene preserves the authored top-camera entity data during packaging.
/// </summary>
public sealed class CityNintendo3DsCubeTestPackagedSceneRuntimeTests {
    /// <summary>
    /// Absolute packaged scene path for the current Nintendo 3DS diagnostic build workspace.
    /// </summary>
    const string PackagedScenePath = @"C:\Users\beatriz\AppData\Local\Temp\helengine-builds\a1520f01edd0e0ae710746d92aa1d694\3ds\workspace\f35c4867d30b4a21ba923a42ebb9200e\builder\package-source\scenes\rendering\ds\cube_test_ds.hasset";

    /// <summary>
    /// Stable serialized component id for built-in cameras.
    /// </summary>
    const string CameraComponentTypeId = "helengine.CameraComponent";

    /// <summary>
    /// Stable serialized component id for built-in FPS overlays.
    /// </summary>
    const string FpsComponentTypeId = "helengine.FPSComponent";

    /// <summary>
    /// Absolute authoring-scene path for the generated Nintendo DS companion cube-test scene consumed by Nintendo 3DS builds.
    /// </summary>
    const string GeneratedDsScenePath = @"C:\dev\helprojs\city\assets\scenes\rendering\ds\cube_test_ds.helen";

    /// <summary>
    /// Absolute authoring-scene path for the generated Nintendo DS companion colored-cube-grid scene consumed by Nintendo 3DS builds.
    /// </summary>
    const string GeneratedColoredCubeGridDsScenePath = @"C:\dev\helprojs\city\assets\scenes\rendering\ds\colored_cube_grid_ds.helen";

    /// <summary>
    /// Absolute authoring-scene path for the generated Nintendo DS companion dynamic-stack-boxes physics scene consumed by Nintendo 3DS builds.
    /// </summary>
    const string GeneratedPhysicsDynamicStackBoxesDsScenePath = @"C:\dev\helprojs\city\assets\scenes\physics\test_scene_dynamic_stack_boxes_ds.helen";

    /// <summary>
    /// Ensures the packaged Nintendo 3DS cube-test scene keeps the authored top camera root at the expected transform.
    /// </summary>
    [Fact]
    public void Nintendo3Ds_packaged_cube_test_scene_keeps_authored_top_camera_root_transform() {
        Assert.True(File.Exists(PackagedScenePath), $"Expected packaged scene asset '{PackagedScenePath}' to exist.");

        using FileStream stream = File.OpenRead(PackagedScenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));

        SceneEntityAsset topCameraEntity = Assert.Single(sceneAsset.RootEntities, static entity =>
            entity != null
            && entity.Components != null
            && entity.Components.Any(static component => string.Equals(component.ComponentTypeId, CameraComponentTypeId, StringComparison.Ordinal))
            && string.Equals(entity.Name, "DemoDiscTopScreenCamera", StringComparison.Ordinal));

        Assert.Equal(0f, topCameraEntity.LocalPosition.X, 3);
        Assert.Equal(0f, topCameraEntity.LocalPosition.Y, 3);
        Assert.Equal(5f, topCameraEntity.LocalPosition.Z, 3);

        SceneComponentAssetRecord cameraRecord = FindComponentRecord(topCameraEntity.Components, CameraComponentTypeId);
        CameraComponent cameraComponent = DeserializeAutomaticComponent<CameraComponent>(cameraRecord);
        Assert.Equal(0f, cameraComponent.Viewport.X, 3);
        Assert.Equal(0f, cameraComponent.Viewport.Y, 3);
        Assert.Equal(1f, cameraComponent.Viewport.Z, 3);
        Assert.Equal(1f, cameraComponent.Viewport.W, 3);
        Assert.Equal(0.1f, cameraComponent.NearPlaneDistance, 3);
        Assert.Equal(64f, cameraComponent.FarPlaneDistance, 3);

        CameraComponent runtimeCameraComponent = DeserializeRuntimeAutomaticComponent<CameraComponent>(cameraRecord, CameraComponentTypeId);
        Assert.Equal(0f, runtimeCameraComponent.Viewport.X, 3);
        Assert.Equal(0f, runtimeCameraComponent.Viewport.Y, 3);
        Assert.Equal(1f, runtimeCameraComponent.Viewport.Z, 3);
        Assert.Equal(1f, runtimeCameraComponent.Viewport.W, 3);
        Assert.Equal(0.1f, runtimeCameraComponent.NearPlaneDistance, 3);
        Assert.Equal(64f, runtimeCameraComponent.FarPlaneDistance, 3);
    }

    /// <summary>
    /// Ensures the generated Nintendo DS companion cube-test scene serializes the bottom-screen FPS overlay with the authored one-times font scale.
    /// </summary>
    [Fact]
    public void Nintendo3Ds_generated_ds_cube_test_scene_serializes_bottom_screen_fps_with_one_times_font_scale() {
        Assert.True(File.Exists(GeneratedDsScenePath), $"Expected generated DS scene asset '{GeneratedDsScenePath}' to exist.");

        using FileStream stream = File.OpenRead(GeneratedDsScenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));

        SceneEntityAsset fpsEntity = FindEntityWithComponent(sceneAsset.RootEntities, FpsComponentTypeId);

        SceneComponentAssetRecord fpsRecord = FindComponentRecord(fpsEntity.Components, FpsComponentTypeId);
        FPSComponent runtimeFpsComponent = DeserializeRuntimeAutomaticComponent<FPSComponent>(fpsRecord, FpsComponentTypeId);
        Assert.Equal(1f, runtimeFpsComponent.FontScale, 3);
    }

    /// <summary>
    /// Ensures representative generated Nintendo DS companion scenes include the canonical scaffold-owned bottom-screen controls.
    /// </summary>
    [Fact]
    public void Nintendo3Ds_generated_ds_scenes_include_canonical_bottom_screen_controls() {
        string[] scenePaths = {
            GeneratedDsScenePath,
            GeneratedColoredCubeGridDsScenePath,
            GeneratedPhysicsDynamicStackBoxesDsScenePath
        };

        for (int index = 0; index < scenePaths.Length; index++) {
            AssertSceneContainsCanonicalBottomScreenControls(scenePaths[index]);
        }
    }

    /// <summary>
    /// Finds one component record with the requested component type.
    /// </summary>
    /// <param name="components">Component records to inspect.</param>
    /// <param name="componentTypeId">Component type id to search for.</param>
    /// <returns>Matching component record.</returns>
    static SceneComponentAssetRecord FindComponentRecord(IReadOnlyList<SceneComponentAssetRecord> components, string componentTypeId) {
        if (components == null) {
            throw new ArgumentNullException(nameof(components));
        } else if (string.IsNullOrWhiteSpace(componentTypeId)) {
            throw new ArgumentException("A component type id must be provided.", nameof(componentTypeId));
        }

        for (int index = 0; index < components.Count; index++) {
            SceneComponentAssetRecord candidate = components[index];
            if (candidate != null && string.Equals(candidate.ComponentTypeId, componentTypeId, StringComparison.Ordinal)) {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Expected one component record with type id '{componentTypeId}'.");
    }

    /// <summary>
    /// Finds one entity across the supplied scene subtree that owns the requested component type.
    /// </summary>
    /// <param name="entities">Root entities to inspect.</param>
    /// <param name="componentTypeId">Stable component type id to search for.</param>
    /// <returns>Matching entity.</returns>
    static SceneEntityAsset FindEntityWithComponent(IReadOnlyList<SceneEntityAsset> entities, string componentTypeId) {
        if (entities == null) {
            throw new ArgumentNullException(nameof(entities));
        } else if (string.IsNullOrWhiteSpace(componentTypeId)) {
            throw new ArgumentException("A component type id must be provided.", nameof(componentTypeId));
        }

        for (int index = 0; index < entities.Count; index++) {
            SceneEntityAsset match = FindEntityWithComponentRecursive(entities[index], componentTypeId);
            if (match != null) {
                return match;
            }
        }

        throw new InvalidOperationException($"Expected one entity with component type id '{componentTypeId}'.");
    }

    /// <summary>
    /// Recursively finds one entity that owns the requested component type.
    /// </summary>
    /// <param name="entity">Current entity being inspected.</param>
    /// <param name="componentTypeId">Stable component type id to search for.</param>
    /// <returns>Matching entity, or null when this subtree does not contain the component.</returns>
    static SceneEntityAsset FindEntityWithComponentRecursive(SceneEntityAsset entity, string componentTypeId) {
        if (entity == null) {
            return null;
        }

        if (entity.Components != null && entity.Components.Any(component => component != null && string.Equals(component.ComponentTypeId, componentTypeId, StringComparison.Ordinal))) {
            return entity;
        }

        if (entity.Children == null) {
            return null;
        }

        for (int index = 0; index < entity.Children.Length; index++) {
            SceneEntityAsset match = FindEntityWithComponentRecursive(entity.Children[index], componentTypeId);
            if (match != null) {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// Deserializes one automatic reflected component record into a live component instance.
    /// </summary>
    /// <typeparam name="T">Expected component type.</typeparam>
    /// <param name="record">Component record to deserialize.</param>
    /// <returns>Deserialized component instance.</returns>
    static T DeserializeAutomaticComponent<T>(SceneComponentAssetRecord record) where T : Component {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }

        AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
        TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
        EntitySaveComponent saveComponent = new EntitySaveComponent();
        using TestClockDrivenCore core = new TestClockDrivenCore(new CoreInitializationOptions());
        return Assert.IsType<T>(descriptor.DeserializeComponent(record, saveComponent, resolver));
    }

    /// <summary>
    /// Deserializes one packaged runtime payload through the strict player runtime automatic deserializer.
    /// </summary>
    /// <typeparam name="T">Expected component type.</typeparam>
    /// <param name="record">Packaged runtime component record.</param>
    /// <param name="componentTypeId">Stable component type id expected by the runtime deserializer.</param>
    /// <returns>Strict runtime-deserialized component instance.</returns>
    static T DeserializeRuntimeAutomaticComponent<T>(SceneComponentAssetRecord record, string componentTypeId) where T : Component {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        } else if (string.IsNullOrWhiteSpace(componentTypeId)) {
            throw new ArgumentException("A component type id must be provided.", nameof(componentTypeId));
        }

        using TestClockDrivenCore core = new TestClockDrivenCore(new CoreInitializationOptions());
        AutomaticScriptComponentRuntimeDeserializer runtimeDeserializer = new AutomaticScriptComponentRuntimeDeserializer(componentTypeId, typeof(T));
        return Assert.IsType<T>(runtimeDeserializer.Deserialize(record, null));
    }

    /// <summary>
    /// Ensures the supplied generated Nintendo DS companion scene text contains the scaffold-owned canonical bottom-screen controls.
    /// </summary>
    /// <param name="scenePath">Absolute generated scene path to inspect.</param>
    static void AssertSceneContainsCanonicalBottomScreenControls(string scenePath) {
        if (string.IsNullOrWhiteSpace(scenePath)) {
            throw new ArgumentException("A scene path must be provided.", nameof(scenePath));
        }

        Assert.True(File.Exists(scenePath), $"Expected generated DS scene asset '{scenePath}' to exist.");

        string sceneText = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(scenePath));
        Assert.Contains("DemoDiscBottomScreenLightButton", sceneText, StringComparison.Ordinal);
        Assert.Contains("DemoDiscBottomScreenLightButtonLabel", sceneText, StringComparison.Ordinal);
        Assert.Contains("DemoDiscBottomScreenLightSwatch", sceneText, StringComparison.Ordinal);
        Assert.Contains("DemoDiscBottomScreenBackButton", sceneText, StringComparison.Ordinal);
        Assert.Contains("helengine.FPSComponent", sceneText, StringComparison.Ordinal);
    }
}
