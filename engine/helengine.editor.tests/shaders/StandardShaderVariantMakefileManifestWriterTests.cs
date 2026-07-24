using Xunit;

namespace helengine.editor.tests.shaders;

/// <summary>
/// Verifies the generated Makefile manifest that exposes shared Standard Shader variants to non-.NET platform toolchains.
/// </summary>
public sealed class StandardShaderVariantMakefileManifestWriterTests {
    /// <summary>
    /// Ensures the Makefile manifest contains every canonical Standard Shader variant in catalog order.
    /// </summary>
    [Fact]
    public void Write_whenStandardShaderVariantsAreExported_writesTheCanonicalMakefileVariable() {
        string manifest = new StandardShaderVariantMakefileManifestWriter().Write();

        Assert.Equal("STANDARD_SHADER_VARIANTS := ForwardStandard ForwardStandardShadowed ShadowDepth\n", manifest);
    }
}
