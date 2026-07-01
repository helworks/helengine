using System.Text.Json;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the local editor platform catalog registers the installed PS Vita builder.
/// </summary>
public sealed class EditorInstalledPsVitaPlatformSourceTests {
    /// <summary>
    /// Ensures the shared editor platform catalog contains one PS Vita entry that points at the checked-out builder assembly.
    /// </summary>
    [Fact]
    public void Editor_installed_platform_catalog_registers_psvita_builder() {
        string sourcePath = @"C:\dev\helworks\helengine\user_settings\platforms.json";
        string source = File.ReadAllText(sourcePath);
        using JsonDocument document = JsonDocument.Parse(source);
        JsonElement platforms = document.RootElement.GetProperty("platforms");
        JsonElement psVitaPlatform = platforms.EnumerateArray().Single(platform => string.Equals(platform.GetProperty("platformId").GetString(), "psvita", StringComparison.Ordinal));

        Assert.Equal("PS Vita", psVitaPlatform.GetProperty("displayName").GetString());
        Assert.Equal("../../helengine-psvita/builder/bin/Debug/net9.0/helengine.psvita.builder.dll", psVitaPlatform.GetProperty("builderAssemblyPath").GetString());
        Assert.Equal("../../helengine-psvita", psVitaPlatform.GetProperty("playerSourceRootPath").GetString());
    }
}
