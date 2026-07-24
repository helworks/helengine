using System.Text;

namespace helengine;

/// <summary>
/// Serializes the shared Standard Shader variant catalog into the Makefile variable consumed by non-.NET platform shader toolchains.
/// </summary>
public sealed class StandardShaderVariantMakefileManifestWriter {
    /// <summary>
    /// Writes the canonical Standard Shader variant Makefile variable in catalog order.
    /// </summary>
    /// <returns>Complete UTF-8 text content for one Makefile include file.</returns>
    public string Write() {
        StringBuilder builder = new("STANDARD_SHADER_VARIANTS :=");
        for (int index = 0; index < StandardShaderVariants.All.Count; index++) {
            builder.Append(' ').Append(StandardShaderVariants.All[index].Name);
        }

        builder.Append('\n');
        return builder.ToString();
    }
}
