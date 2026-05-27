using helengine;
using Xunit;

namespace helengine.baseplatform.tests.Paths;

/// <summary>
/// Verifies canonical normalization and validation rules for packaged runtime asset paths.
/// </summary>
public sealed class CanonicalPackagedAssetPathTests {
    /// <summary>
    /// Ensures mixed-case paths that use backslashes normalize into lowercase forward-slash paths.
    /// </summary>
    [Fact]
    public void Normalize_WhenPathUsesBackslashesAndUppercase_ReturnsLowercaseForwardSlashPath() {
        string normalized = CanonicalPackagedAssetPath.Normalize("cooked\\Fonts\\DemoDiscBody.hefont");

        Assert.Equal("cooked/fonts/demodiscbody.hefont", normalized);
    }

    /// <summary>
    /// Ensures validation rejects mixed-case packaged paths that are not already canonical.
    /// </summary>
    [Fact]
    public void ValidateCanonical_WhenPathContainsUppercase_ThrowsInvalidOperationException() {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => CanonicalPackagedAssetPath.ValidateCanonical("cooked/Fonts/DemoDiscBody.hefont"));

        Assert.Contains("cooked/Fonts/DemoDiscBody.hefont", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures validation rejects rooted packaged paths because runtime asset paths must stay relative.
    /// </summary>
    [Fact]
    public void ValidateCanonical_WhenPathIsRooted_ThrowsInvalidOperationException() {
        Assert.Throws<InvalidOperationException>(
            () => CanonicalPackagedAssetPath.ValidateCanonical("/cooked/fonts/default.hefont"));
    }
}
