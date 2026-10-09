using System.Text.Json;
namespace helengine.vfx.cli.tests;
/// <summary>Checks composition subcommand routing and controlled JSON failures without GPU allocation.</summary>
public sealed class CompositionCliTests {
    /// <summary>The catalog command produces the exact versioned capability document.</summary>
    [Fact] public void CapabilitiesAreVersionedJson() {using var json=JsonDocument.Parse(CompositionCliRunner.CapabilitiesJson());Assert.Equal("helengine.media.capabilities.v1",json.RootElement.GetProperty("schema").GetString());Assert.Contains(json.RootElement.GetProperty("effects").EnumerateArray(),effect=>effect.GetProperty("id").GetString()=="crossfade");}
    /// <summary>The catalog lists the built-in scene arrangements with their regions and roles, so planners can choose them.</summary>
    [Fact] public void CapabilitiesListSceneArrangements() {using var json=JsonDocument.Parse(CompositionCliRunner.CapabilitiesJson());var arrangements=json.RootElement.GetProperty("arrangements").EnumerateArray().ToList();Assert.Equal(["full","stack","split","take_with_graphic","graphic_only"],arrangements.Select(item=>item.GetProperty("id").GetString()));var stack=arrangements[1];Assert.False(string.IsNullOrWhiteSpace(stack.GetProperty("description").GetString()));Assert.Equal(["top:graphic","main:picture"],stack.GetProperty("regions").EnumerateArray().Select(region=>region.GetProperty("name").GetString()+":"+region.GetProperty("role").GetString()));}
    /// <summary>Unknown options and missing required input yield controlled nonzero exit codes.</summary>
    [Theory] [InlineData("unknown")] [InlineData("validate")] [InlineData("render")] public void InvalidInvocationReturnsFailure(string command) {Assert.Equal(1,VfxCliRunner.Run(["composition",command,"--unexpected","value"]));}
}
