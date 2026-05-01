using helengine.baseplatform.Builders;

namespace helengine.baseplatform.tests.Builders;

/// <summary>
/// Verifies that the shared builder contract exposes typed platform metadata.
/// </summary>
public class IPlatformAssetBuilderMetadataTests {
    /// <summary>
    /// Verifies a builder implementation can expose a typed platform definition.
    /// </summary>
    [Fact]
    public void Builder_contract_exposes_platform_definition() {
        IPlatformAssetBuilder builder = new TestPlatformAssetBuilder();

        Assert.Equal("windows", builder.Definition.PlatformId);
        Assert.Equal("debug", builder.Definition.BuildProfiles[0].ProfileId);
    }
}
