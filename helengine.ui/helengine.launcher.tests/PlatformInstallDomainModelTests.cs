using helengine.editor.launcher.Models;
using Xunit;

namespace helengine.editor.launcher.tests;

/// <summary>
/// Verifies the shared launcher install-domain model types expose the expected identity and default collection behavior.
/// </summary>
public sealed class PlatformInstallDomainModelTests {
    /// <summary>
    /// Ensures artifact identity equality requires an exact match on kind, id, and version.
    /// </summary>
    [Fact]
    public void ArtifactIdentity_EqualsOnlyWhenKindIdAndVersionMatch() {
        ArtifactIdentity first = new ArtifactIdentity(PlatformArtifactKind.Sdk, "android-sdk", "34.0");
        ArtifactIdentity second = new ArtifactIdentity(PlatformArtifactKind.Sdk, "android-sdk", "34.0");
        ArtifactIdentity differentVersion = new ArtifactIdentity(PlatformArtifactKind.Sdk, "android-sdk", "35.0");

        Assert.Equal(first, second);
        Assert.NotEqual(first, differentVersion);
    }

    /// <summary>
    /// Ensures a new platform install plan starts with empty reusable, missing, and blocking collections.
    /// </summary>
    [Fact]
    public void PlatformInstallPlan_StartsWithEmptyCollections() {
        PlatformInstallPlan plan = new PlatformInstallPlan();

        Assert.Empty(plan.ReusableArtifacts);
        Assert.Empty(plan.MissingArtifacts);
        Assert.Empty(plan.BlockingIssues);
    }
}
