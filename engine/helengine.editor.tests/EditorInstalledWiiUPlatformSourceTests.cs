using System.Text.Json;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the local editor platform catalog registers the installed Wii U builder.
/// </summary>
public sealed class EditorInstalledWiiUPlatformSourceTests {
    /// <summary>
    /// Ensures the shared editor platform catalog contains one Wii U entry that points at the checked-out builder assembly.
    /// </summary>
    [Fact]
    public void Editor_installed_platform_catalog_registers_wiiu_builder() {
        string sourcePath = @"C:\dev\helworks\helengine\user_settings\platforms.json";
        string source = File.ReadAllText(sourcePath);
        using JsonDocument document = JsonDocument.Parse(source);
        JsonElement platforms = document.RootElement.GetProperty("platforms");
        JsonElement wiiuPlatform = platforms.EnumerateArray().Single(platform => string.Equals(platform.GetProperty("platformId").GetString(), "wiiu", StringComparison.Ordinal));

        Assert.Equal("Nintendo Wii U", wiiuPlatform.GetProperty("displayName").GetString());
        Assert.Equal("../../helengine-wiiu/builder/bin/Debug/net9.0-windows/helengine.wiiu.builder.dll", wiiuPlatform.GetProperty("builderAssemblyPath").GetString());
        Assert.Equal("../../helengine-wiiu", wiiuPlatform.GetProperty("playerSourceRootPath").GetString());
    }
}
