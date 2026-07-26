using helengine.baseplatform.Manifest;
using Xunit;

namespace helengine.baseplatform.tests.Manifest;

/// <summary>
/// Verifies explicit cooked artifact declarations preserve material and shader identity before manifest collection.
/// </summary>
public sealed class PlatformCookedArtifactDeclarationTests {
    /// <summary>
    /// Ensures a material declaration keeps the producer-supplied path, logical identity, kind, and variant.
    /// </summary>
    [Fact]
    public void Constructor_whenMaterialDeclarationIsValid_preservesExplicitIdentity() {
        PlatformCookedArtifactDeclaration declaration = new(
            "cooked/materials/standard.hasset",
            "engine:material:standard",
            "material",
            "shared");

        Assert.Equal("cooked/materials/standard.hasset", declaration.RelativePath);
        Assert.Equal("engine:material:standard", declaration.LogicalArtifactId);
        Assert.Equal("material", declaration.ArtifactKind);
        Assert.Equal("shared", declaration.VariantId);
    }
}
