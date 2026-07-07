using helengine.baseplatform.Manifest;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies disabled runtime feature validation fails when the build requires a user-disabled feature.
/// </summary>
public sealed class EditorRuntimeFeatureManifestValidationServiceTests {
    /// <summary>
    /// Verifies validation rejects any disabled feature that is still required by the build manifest.
    /// </summary>
    [Fact]
    public void Validate_whenDisabledFeatureIsRequired_throws() {
        EditorRuntimeFeatureManifestValidationService service = new();
        PlatformBuildRuntimeFeatureManifest manifest = new(
            [
                new PlatformBuildRequiredRuntimeFeature(
                    "physics3d.box_box_contact",
                    RuntimeFeatureRequirementSourceKind.Scene,
                    "PhysicsScene",
                    "Scene requires box contact.")
            ]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.Validate(
            manifest,
            ["physics3d.box_box_contact"]));

        Assert.Contains("physics3d.box_box_contact", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PhysicsScene", exception.Message, StringComparison.Ordinal);
    }
}
