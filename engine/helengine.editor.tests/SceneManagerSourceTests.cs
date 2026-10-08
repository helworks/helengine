namespace helengine.editor.tests;

/// <summary>
/// Verifies native-only transient cleanup calls that managed disposal tests cannot observe.
/// </summary>
public sealed class SceneManagerSourceTests {

    /// <summary>
    /// Ensures the asynchronous transfer deletes its temporary wrapper after releasing the operation's borrowed result reference.
    /// </summary>
    [Fact]
    public void AsyncTransfer_DeletesWrapperAfterOperationReleaseWithoutDeletingTransferredAssets() {
        string source = File.ReadAllText(Path.Combine(GeneratedHelengineSourceRoot.Path,
            "engine", "helengine.core", "scene", "runtime", "SceneManager.cs"));
        int start = source.IndexOf("void AdvanceSceneTransition()", StringComparison.Ordinal);
        int end = source.IndexOf("void UnloadSceneImmediate(", start, StringComparison.Ordinal);
        string transition = source.Substring(start, end - start);
        Assert.Contains("RuntimeSceneLoadResult loadResult = TransitionLoadOperation.TakeResult()", transition);
        int transfer = transition.IndexOf("TrackLoadedSceneRecord(loadedSceneRecord)", StringComparison.Ordinal);
        int releaseOperation = transition.IndexOf("NativeOwnership.DisposeAndRelease(ref TransitionLoadOperation)", StringComparison.Ordinal);
        int deleteWrapper = transition.IndexOf("NativeOwnership.Delete(loadResult)", StringComparison.Ordinal);
        Assert.True(transfer >= 0 && releaseOperation > transfer && deleteWrapper > releaseOperation);
        Assert.DoesNotContain("NativeOwnership.Delete(loadResult.RootEntities)", transition);
        Assert.DoesNotContain("NativeOwnership.DisposeAndDelete(loadResult.OwnedAssets)", transition);
    }

    /// <summary>
    /// Ensures heap-backed authored orders use the singleton-safe native array cleanup before their entity record is deleted.
    /// </summary>
    [Fact]
    public void TransientEntityCleanup_ReleasesOverrideOrderBeforeDeletingRecord() {
        string source = File.ReadAllText(Path.Combine(GeneratedHelengineSourceRoot.Path,
            "engine", "helengine.core", "scene", "runtime", "SceneManager.cs"));
        int start = source.IndexOf("static void ReleaseTransientSceneEntityAsset(", StringComparison.Ordinal);
        int end = source.IndexOf("static void ReleaseTransientSceneSettingsAsset(", start, StringComparison.Ordinal);
        string cleanup = source.Substring(start, end - start);
        int detach = cleanup.IndexOf("asset.OverrideLevelOrder = null", StringComparison.Ordinal);
        int release = cleanup.IndexOf("DeleteTransientArray(overrideLevelOrder)", StringComparison.Ordinal);
        int deleteRecord = cleanup.IndexOf("NativeOwnership.Delete(asset)", StringComparison.Ordinal);
        Assert.Contains("SceneOverrideScopeStepKind[] overrideLevelOrder = asset.OverrideLevelOrder", cleanup);
        Assert.True(detach >= 0 && release > detach && deleteRecord > release);
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
