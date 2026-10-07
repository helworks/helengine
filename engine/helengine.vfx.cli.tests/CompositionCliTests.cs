using System.Text.Json;
namespace helengine.vfx.cli.tests;
/// <summary>Checks composition subcommand routing and controlled JSON failures without GPU allocation.</summary>
public sealed class CompositionCliTests {
    /// <summary>The catalog command produces the exact versioned capability document.</summary>
    [Fact] public void CapabilitiesAreVersionedJson() {using var json=JsonDocument.Parse(CompositionCliRunner.CapabilitiesJson());Assert.Equal("helengine.media.capabilities.v1",json.RootElement.GetProperty("schema").GetString());Assert.Contains(json.RootElement.GetProperty("effects").EnumerateArray(),effect=>effect.GetProperty("id").GetString()=="crossfade");}
    /// <summary>Unknown options and missing required input yield controlled nonzero exit codes.</summary>
    [Theory] [InlineData("unknown")] [InlineData("validate")] [InlineData("render")] public void InvalidInvocationReturnsFailure(string command) {Assert.Equal(1,VfxCliRunner.Run(["composition",command,"--unexpected","value"]));}
}
