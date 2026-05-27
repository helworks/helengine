using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.Paths;

/// <summary>
/// Resolves packaged asset paths into the final runtime form required by one platform contract.
/// </summary>
public static class PlatformPackagedAssetPathResolver {
    /// <summary>
    /// Resolves one packaged asset path into the runtime path form required by the supplied platform contract.
    /// </summary>
    /// <param name="platformId">Stable target platform identifier.</param>
    /// <param name="runtimeGenerationContract">Runtime-generation contract that declares the supported packaged path policy.</param>
    /// <param name="packagedAssetPath">Logical packaged asset path emitted by the shared content pipeline.</param>
    /// <returns>Final runtime path consumed by the player runtime for the target platform.</returns>
    public static string ResolveRuntimeReferencePath(string platformId, RuntimeGenerationContract runtimeGenerationContract, string packagedAssetPath) {
        if (string.IsNullOrWhiteSpace(platformId)) {
            throw new ArgumentException("Platform id must be provided.", nameof(platformId));
        } else if (runtimeGenerationContract == null) {
            throw new ArgumentNullException(nameof(runtimeGenerationContract));
        } else if (string.IsNullOrWhiteSpace(packagedAssetPath)) {
            throw new ArgumentException("Packaged asset path must be provided.", nameof(packagedAssetPath));
        }

        string canonicalPackagedAssetPath = CanonicalPackagedAssetPath.ValidateCanonical(packagedAssetPath);
        if (runtimeGenerationContract.PackagedPathPolicy == PackagedPathPolicy.ContentRelativeOnly) {
            return canonicalPackagedAssetPath;
        }
        if (runtimeGenerationContract.PackagedPathPolicy == PackagedPathPolicy.RootedOrContentRelative) {
            if (string.Equals(platformId, "ps2", StringComparison.OrdinalIgnoreCase)) {
                return Ps2DiscPathResolver.ResolveRuntimePath(canonicalPackagedAssetPath);
            }

            throw new InvalidOperationException($"Platform '{platformId}' requires rooted packaged paths, but no rooted runtime path resolver is registered.");
        }

        throw new InvalidOperationException($"Unsupported packaged path policy '{runtimeGenerationContract.PackagedPathPolicy}'.");
    }
}
