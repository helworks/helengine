using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies generated physics scenes satisfy the native authored-asset identity contract.
/// </summary>
public sealed class PhysicsValidationSceneIdentityTests : IDisposable {
    readonly string ProjectRootPath;

    public PhysicsValidationSceneIdentityTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-physics-scene-identity-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
    }

    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    /// <summary>
    /// Ensures first export embeds identities and later exports preserve them without sidecars.
    /// </summary>
    [Fact]
    public void WriteScenes_EmbedsAndPreservesNativeIdentityWithoutSidecars() {
        PhysicsValidationSceneFactory factory = new PhysicsValidationSceneFactory();
        AssetIdentityMetadataService metadataService = new AssetIdentityMetadataService();

        factory.WriteScenes(ProjectRootPath);
        Dictionary<string, string> firstIds = ReadSceneIds(metadataService);

        factory.WriteScenes(ProjectRootPath);
        Dictionary<string, string> secondIds = ReadSceneIds(metadataService);

        Assert.Equal(firstIds, secondIds);
        Assert.DoesNotContain(
            Directory.EnumerateFiles(Path.Combine(ProjectRootPath, "assets"), "*.hmeta", SearchOption.AllDirectories),
            path => path.EndsWith(".helen.hmeta", StringComparison.OrdinalIgnoreCase));
    }

    Dictionary<string, string> ReadSceneIds(AssetIdentityMetadataService metadataService) {
        Dictionary<string, string> identities = new Dictionary<string, string>(StringComparer.Ordinal);
        string[] sceneIds = PhysicsValidationSceneCatalog.GetSceneIds();
        for (int index = 0; index < sceneIds.Length; index++) {
            string relativePath = sceneIds[index];
            string fullPath = Path.Combine(ProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            AssetIdentityMetadataDocument identity = metadataService.Load(fullPath);
            Assert.Matches("^[0-9a-f]{32}$", identity.AssetId);
            identities.Add(relativePath, identity.AssetId);
        }
        return identities;
    }
}
