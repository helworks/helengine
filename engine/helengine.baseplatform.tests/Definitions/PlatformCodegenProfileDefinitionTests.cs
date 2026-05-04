using helengine.baseplatform.Definitions;
using helengine.baseplatform.Profiles;

namespace helengine.baseplatform.tests.Definitions;

/// <summary>
/// Verifies the typed codegen profile metadata model preserves builder metadata.
/// </summary>
public class PlatformCodegenProfileDefinitionTests {
    /// <summary>
    /// Verifies a codegen profile retains language, endianness, and settings metadata.
    /// </summary>
    [Fact]
    public void PlatformCodegenProfileDefinition_preserves_metadata() {
        PlatformCodegenProfileDefinition definition = new(
            "default",
            "Default",
            "Default codegen profile",
            PlatformCodegenLanguage.Cpp,
            PlatformSerializationEndianness.LittleEndian,
            [
                new PlatformSettingDefinition(
                    "emit-symbols",
                    "Emit Symbols",
                    PlatformSettingKind.Boolean,
                    "true",
                    true,
                    [])
            ]);

        Assert.Equal("default", definition.ProfileId);
        Assert.Equal("Default", definition.DisplayName);
        Assert.Equal("Default codegen profile", definition.Description);
        Assert.Equal(PlatformCodegenLanguage.Cpp, definition.OutputLanguage);
        Assert.Equal(PlatformSerializationEndianness.LittleEndian, definition.Endianness);
        Assert.Equal("emit-symbols", definition.Settings[0].SettingId);
    }
}
