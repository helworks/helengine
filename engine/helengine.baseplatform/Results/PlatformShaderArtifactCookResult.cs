using helengine.baseplatform.Manifest;

namespace helengine.baseplatform.Results;

/// <summary>
/// Captures the explicit shader artifact declarations emitted by one platform shader staging operation.
/// </summary>
public sealed class PlatformShaderArtifactCookResult {
    /// <summary>
    /// Stores the copied shader artifact declarations emitted by the platform.
    /// </summary>
    readonly PlatformCookedArtifactDeclaration[] CookedArtifactDeclarationsValue;

    /// <summary>
    /// Initializes one shader artifact staging result.
    /// </summary>
    /// <param name="cookedArtifactDeclarations">Explicit declarations for shader files written by the staging operation.</param>
    public PlatformShaderArtifactCookResult(IReadOnlyList<PlatformCookedArtifactDeclaration> cookedArtifactDeclarations) {
        if (cookedArtifactDeclarations == null) {
            throw new ArgumentNullException(nameof(cookedArtifactDeclarations));
        }

        for (int index = 0; index < cookedArtifactDeclarations.Count; index++) {
            if (cookedArtifactDeclarations[index] == null) {
                throw new ArgumentException("Shader artifact declarations cannot contain null entries.", nameof(cookedArtifactDeclarations));
            } else if (!string.Equals(cookedArtifactDeclarations[index].ArtifactKind, "shader", StringComparison.Ordinal)) {
                throw new ArgumentException("Shader artifact staging can only declare shader artifacts.", nameof(cookedArtifactDeclarations));
            }
        }

        CookedArtifactDeclarationsValue = cookedArtifactDeclarations.ToArray();
    }

    /// <summary>
    /// Gets copied declarations for shader files written by the platform staging operation.
    /// </summary>
    public IReadOnlyList<PlatformCookedArtifactDeclaration> CookedArtifactDeclarations {
        get {
            return CookedArtifactDeclarationsValue;
        }
    }
}
