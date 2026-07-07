using helengine.baseplatform.Manifest;

namespace helengine.baseplatform.tests.Manifest;

/// <summary>
/// Verifies runtime feature manifests preserve required feature records and reject invalid inputs.
/// </summary>
public sealed class PlatformBuildRuntimeFeatureManifestTests {
    /// <summary>
    /// Ensures one runtime feature manifest preserves the supplied ordered requirement records.
    /// </summary>
    [Fact]
    public void Ctor_preserves_ordered_required_feature_records() {
        PlatformBuildRequiredRuntimeFeature[] requirements = [
            new PlatformBuildRequiredRuntimeFeature(
                "render3d.material.textured_lit",
                RuntimeFeatureRequirementSourceKind.Material,
                "Materials/rendering/test/Cube00",
                "material schema requires textured lit 3D runtime path"),
            new PlatformBuildRequiredRuntimeFeature(
                "physics3d.box_box_contact",
                RuntimeFeatureRequirementSourceKind.Scene,
                "Scenes/rendering/physics_stack_boxes",
                "scene serialized rigid-body and box collider contact pair")
        ];

        PlatformBuildRuntimeFeatureManifest manifest = new(requirements);

        Assert.Collection(
            manifest.RequiredFeatures,
            requirement => {
                Assert.Equal("render3d.material.textured_lit", requirement.FeatureId);
                Assert.Equal(RuntimeFeatureRequirementSourceKind.Material, requirement.SourceKind);
                Assert.Equal("Materials/rendering/test/Cube00", requirement.SourceId);
                Assert.Equal("material schema requires textured lit 3D runtime path", requirement.Reason);
            },
            requirement => {
                Assert.Equal("physics3d.box_box_contact", requirement.FeatureId);
                Assert.Equal(RuntimeFeatureRequirementSourceKind.Scene, requirement.SourceKind);
                Assert.Equal("Scenes/rendering/physics_stack_boxes", requirement.SourceId);
                Assert.Equal("scene serialized rigid-body and box collider contact pair", requirement.Reason);
            });
    }

    /// <summary>
    /// Ensures one runtime feature manifest rejects missing requirement entries.
    /// </summary>
    [Fact]
    public void Ctor_throws_when_requirement_collection_contains_null_entry() {
        PlatformBuildRequiredRuntimeFeature[] requirements = [
            new PlatformBuildRequiredRuntimeFeature(
                "render3d.material.unlit",
                RuntimeFeatureRequirementSourceKind.Material,
                "Materials/rendering/test/Cube01",
                "material schema requires unlit 3D runtime path"),
            null
        ];

        ArgumentException exception = Assert.Throws<ArgumentException>(() => new PlatformBuildRuntimeFeatureManifest(requirements));

        Assert.Equal("requiredFeatures", exception.ParamName);
    }
}
