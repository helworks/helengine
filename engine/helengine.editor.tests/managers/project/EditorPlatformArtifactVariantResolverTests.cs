using helengine.baseplatform.Manifest;
using Xunit;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies cooked artifact variant resolution groups identical payloads into shared entries.
/// </summary>
public sealed class EditorPlatformArtifactVariantResolverTests {
        /// <summary>
        /// Ensures two artifacts with the same logical identity but different variant ids resolve into a shared entry.
        /// </summary>
        [Fact]
        public void Resolve_WhenArtifactsShareIdentityAndDifferByVariant_ProducesSharedArtifactEntry() {
            EditorPlatformArtifactVariantResolver resolver = new();
            PlatformBuildArtifact[] artifacts = [
            new PlatformBuildArtifact("cooked/scenes/Bootstrap.hasset", "scene-startup", "hash-123", "scene", "windows"),
            new PlatformBuildArtifact("cooked/scenes/Bootstrap.hasset", "scene-startup", "hash-123", "scene", "linux")
        ];

        EditorResolvedArtifactSet resolved = resolver.Resolve(artifacts);

        Assert.Single(resolved.SharedArtifacts);
        Assert.Empty(resolved.PlatformVariants);
        Assert.Equal("shared", resolved.SharedArtifacts[0].VariantId);
        Assert.Equal("scene-startup", resolved.SharedArtifacts[0].LogicalArtifactId);
        Assert.Equal("hash-123", resolved.SharedArtifacts[0].ContentHash);
    }
}
