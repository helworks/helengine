using helengine.baseplatform.Definitions;
using Xunit;

namespace helengine.editor.tests.managers.project;

/// <summary>Verifies cook naming and audio policies are data-driven and platform-id independent.</summary>
public sealed class PlatformCookPolicyTests {
    [Fact]
    public void AudioLimits_ExposeThePublishedValues() {
        PlatformAudioLimits limits = new PlatformAudioLimits(22050, 1);
        Assert.Equal(22050, limits.MaximumSampleRate);
        Assert.Equal(1, limits.MaximumChannels);
    }

    [Fact]
    public void Capability_DefaultsToAuthoredNaming() {
        PlatformAssetCookCapabilityDefinition capability = new PlatformAssetCookCapabilityDefinition(
            "texture", "runtime-texture", PlatformAssetCookOwnershipKind.EditorOwned, "fictional-texture");
        Assert.Equal(PlatformAssetNamingPolicy.PreserveAssetId, capability.NamingPolicy);
        Assert.Null(capability.AudioLimits);
    }

    [Fact]
    public void Capability_CanPublishRuntimeIdNamingAndAudioLimits() {
        PlatformAssetCookCapabilityDefinition capability = new PlatformAssetCookCapabilityDefinition(
            "texture", "runtime-texture", PlatformAssetCookOwnershipKind.BuilderOwned, "fictional-texture", "", null, ".hetex", PlatformAssetNamingPolicy.RuntimeAssetIdHex16, new PlatformAudioLimits(22050, 1));
        Assert.Equal(PlatformAssetNamingPolicy.RuntimeAssetIdHex16, capability.NamingPolicy);
        Assert.Equal(".hetex", capability.OutputFileExtension);
        Assert.Equal(1, capability.AudioLimits.MaximumChannels);
    }
}