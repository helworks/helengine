using System.Text.Json;
namespace helengine.media.tests;
/// <summary>Ensures capability data validates malformed model parameters without exceptions.</summary>
public sealed class MediaCapabilitiesTests {
    /// <summary>Non-numeric RGB components produce rejection rather than an unexpected parser exception.</summary>
    [Fact] public void ColorComponentsRequireNumericTypes() {
        var descriptor=new MediaParameterDescriptor{Type="color"};
        Assert.False(descriptor.Accepts(JsonSerializer.SerializeToElement(new[]{"1","0","0"})));
        Assert.True(descriptor.Accepts(JsonSerializer.SerializeToElement(new[]{1,0,0})));
    }
    /// <summary>The public catalog contains finite editable ranges and versioned curve names.</summary>
    [Fact] public void CatalogPublishesTypedRangesAndExactCurves() {
        var catalog=MediaCapabilities.Basic();var json=catalog.Describe();
        Assert.Equal("helengine.media.capabilities.v1",json.GetProperty("schema").GetString());
        Assert.Contains("smoothstep.v1",catalog.Curves);Assert.Equal(2.5,catalog.LayerProperties["zoom"].Maximum);
    }
}
