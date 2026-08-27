using System.Reflection;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies interactive project browsers borrow one session-owned identity graph.
/// </summary>
public sealed class EditorInteractiveAuthoringOwnershipTests : IDisposable {
    readonly string ProjectRootPath;

    /// <summary>
    /// Initializes one isolated project.
    /// </summary>
    public EditorInteractiveAuthoringOwnershipTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-interactive-authoring-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
    }

    /// <summary>
    /// Removes isolated project state.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    /// <summary>
    /// Ensures each interactive modal accepts the session resolver through an internal composition constructor.
    /// </summary>
    [Fact]
    public void InteractiveDialogs_ExposeResolverCompositionConstructors() {
        Assert.Contains(typeof(AssetPickerModal).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), HasResolverParameter);
        Assert.Contains(typeof(SaveFileDialog).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), HasResolverParameter);
        Assert.Contains(typeof(OpenFileDialog).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), HasResolverParameter);
    }

    /// <summary>
    /// Ensures a borrowed browser manager does not flush or release its session cache.
    /// </summary>
    [Fact]
    public void BorrowedBrowserManager_DoesNotDisposeSessionCache() {
        string assetPath = Path.Combine(ProjectRootPath, "assets", "Borrowed.obj");
        File.WriteAllBytes(assetPath, new byte[] { 1, 2, 3 });
        using EditorAssetHashCache cache = new EditorAssetHashCache(ProjectRootPath);
        using EditorAssetIdentityIndex identityIndex = new EditorAssetIdentityIndex(ProjectRootPath, null, null, cache);
        using EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(ProjectRootPath, identityIndex, cache);
        using EditorAssetManager manager = new EditorAssetManager(ProjectRootPath, resolver);

        List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();
        manager.LoadEntries(entries);
        manager.Dispose();

        Assert.False(File.Exists(cache.CachePath));
        cache.GetContentHash(assetPath);
    }

    static bool HasResolverParameter(ConstructorInfo constructor) {
        ParameterInfo[] parameters = constructor.GetParameters();
        return parameters.Any(parameter => parameter.ParameterType == typeof(EditorAssetReferenceResolver));
    }
}
