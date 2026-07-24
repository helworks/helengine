using Xunit;

namespace helengine.editor.tests.shaders;

/// <summary>
/// Verifies the shared Standard Shader variant catalog consumed by every shader-capable platform.
/// </summary>
public sealed class StandardShaderVariantsTests {
    /// <summary>
    /// Ensures Standard Shader exposes its forward, shadow-receiving, and depth-only variants in one stable order.
    /// </summary>
    [Fact]
    public void All_whenStandardShaderVariantsAreRequested_returnsTheThreeVariantsInStableOrder() {
        IReadOnlyList<StandardShaderVariant> variants = StandardShaderVariants.All;

        Assert.Collection(
            variants,
            variant => Assert.Equal("ForwardStandard", variant.Name),
            variant => Assert.Equal("ForwardStandardShadowed", variant.Name),
            variant => Assert.Equal("ShadowDepth", variant.Name));
        Assert.Equal("VS", variants[2].VertexEntryPoint);
        Assert.Equal("ShadowDepthPS", variants[2].PixelEntryPoint);
    }
}
