using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;

namespace helengine.baseplatform.Builders;

/// <summary>
/// Defines the optional platform capability that resolves material-reported shader dependencies into independently written shader artifacts.
/// </summary>
public interface IPlatformShaderArtifactBuilder {
    /// <summary>
    /// Writes target-specific shader files for the supplied dependency identifiers and declares every written output.
    /// </summary>
    /// <param name="request">Cook-root and dependency information for the shader staging operation.</param>
    /// <returns>Explicit declarations for the independently written shader artifacts.</returns>
    PlatformShaderArtifactCookResult CookShaderArtifacts(PlatformShaderArtifactCookRequest request);
}
