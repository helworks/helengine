using helengine.baseplatform.Manifest;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the editor runtime feature manifest service aggregates collector output into one build manifest payload.
/// </summary>
public sealed class EditorRuntimeFeatureManifestServiceTests {
    /// <summary>
    /// Ensures the service preserves collector registration order when aggregating required features.
    /// </summary>
    [Fact]
    public void Build_preserves_collector_registration_order() {
        EditorRuntimeFeatureManifestService service = new(
            [
                new FakeEditorRuntimeFeatureRequirementCollector(
                    new PlatformBuildRequiredRuntimeFeature(
                        "render3d.material.textured_lit",
                        RuntimeFeatureRequirementSourceKind.Material,
                        "Materials/cube_textured_lit",
                        "material schema requires textured lit rendering")),
                new FakeEditorRuntimeFeatureRequirementCollector(
                    new PlatformBuildRequiredRuntimeFeature(
                        "physics3d.box_box_contact",
                        RuntimeFeatureRequirementSourceKind.Scene,
                        "Scenes/test_scene_dynamic_stack_boxes.helen",
                        "scene serialized rigid-body box collision"),
                    new PlatformBuildRequiredRuntimeFeature(
                        "physics3d.character_controller",
                        RuntimeFeatureRequirementSourceKind.Scene,
                        "Scenes/test_scene_character_controller.helen",
                        "scene serialized a character controller"))
            ]);

        PlatformBuildRuntimeFeatureManifest manifest = service.Build(CreateBuildManifest());

        Assert.Collection(
            manifest.RequiredFeatures,
            requirement => Assert.Equal("render3d.material.textured_lit", requirement.FeatureId),
            requirement => Assert.Equal("physics3d.box_box_contact", requirement.FeatureId),
            requirement => Assert.Equal("physics3d.character_controller", requirement.FeatureId));
    }

    /// <summary>
    /// Ensures the service returns the shared empty manifest when no collectors are registered.
    /// </summary>
    [Fact]
    public void Build_without_collectors_returns_empty_manifest() {
        EditorRuntimeFeatureManifestService service = new([]);

        PlatformBuildRuntimeFeatureManifest manifest = service.Build(CreateBuildManifest());

        Assert.Same(PlatformBuildRuntimeFeatureManifest.Empty, manifest);
    }

    /// <summary>
    /// Creates one minimal build manifest for runtime feature aggregation tests.
    /// </summary>
    /// <returns>Minimal build manifest for runtime feature aggregation tests.</returns>
    static PlatformBuildManifest CreateBuildManifest() {
        return new PlatformBuildManifest(
            1,
            "project",
            "1.0.0",
            "1.0.0-engine",
            "windows",
            "1.0.0",
            "Scenes/Main.helen",
            Array.Empty<PlatformBuildScene>(),
            Array.Empty<PlatformBuildAsset>(),
            Array.Empty<PlatformBuildArtifact>(),
            Array.Empty<PlatformBuildCodeModule>(),
            Array.Empty<PlatformArtifactPlacement>(),
            new PlatformContainerWritePlan(string.Empty, Array.Empty<PlatformContainerArtifact>()));
    }
}
