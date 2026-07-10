namespace helengine.editor.tests;

/// <summary>
/// Verifies the runtime scene manager source keeps scene-owned audio assets in the same tracked release flow as other scene-owned resources.
/// </summary>
public sealed class SceneManagerSourceTests {
    /// <summary>
    /// Ensures scene-owned audio assets are registered, reference-counted, and released through the shared transient-audio helper.
    /// </summary>
    [Fact]
    public void SceneManager_source_tracks_and_releases_owned_audio_assets() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.core",
            "scene",
            "runtime",
            "SceneManager.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("readonly Dictionary<AudioAsset, int> ActiveOwnedAudioReferenceCounts;", source, StringComparison.Ordinal);
        Assert.Contains("public int ActiveOwnedAudioReferenceCount => ActiveOwnedAudioReferenceCounts.Count;", source, StringComparison.Ordinal);
        Assert.Contains("NativeOwnership.Delete(releasedOwnedAssets.OwnedAudio);", source, StringComparison.Ordinal);
        Assert.Contains("RegisterOwnedAudio(ownedAssets.OwnedAudio);", source, StringComparison.Ordinal);
        Assert.Contains("ReleaseOwnedAudio(ownedAssets.OwnedAudio);", source, StringComparison.Ordinal);
        Assert.Contains("RuntimeSceneAssetReferenceResolver.ReleaseTransientAudioAsset(ownedAsset);", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves the helengine repository root from the current test assembly location.
    /// </summary>
    /// <returns>Absolute repository root path.</returns>
    static string ResolveRepositoryRootPath() {
        string currentPath = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(currentPath)) {
            string rootMarkerPath = Path.Combine(currentPath, "engine", "helengine.editor", "helengine.editor.csproj");
            if (File.Exists(rootMarkerPath)) {
                return currentPath;
            }

            DirectoryInfo parentDirectory = Directory.GetParent(currentPath);
            if (parentDirectory == null) {
                break;
            }

            currentPath = parentDirectory.FullName;
        }

        throw new InvalidOperationException("Could not resolve the helengine repository root from the current test assembly location.");
    }
}
