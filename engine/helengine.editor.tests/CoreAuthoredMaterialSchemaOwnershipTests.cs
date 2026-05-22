namespace helengine.editor.tests;

/// <summary>
/// Verifies core-authored materials no longer own shader-specific schema meaning.
/// </summary>
public sealed class CoreAuthoredMaterialSchemaOwnershipTests {
    /// <summary>
    /// Ensures the core material asset no longer declares shader or texture-slot authored fields.
    /// </summary>
    [Fact]
    public void MaterialAsset_does_not_declare_shader_or_texture_slot_fields() {
        string source = File.ReadAllText(Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.core",
            "assets",
            "raw",
            "material",
            "MaterialAsset.cs"));

        Assert.DoesNotContain("ShaderAssetId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("VertexProgram", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PixelProgram", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Variant", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DiffuseTextureAssetId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NormalTextureAssetId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EmissiveTextureAssetId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ConstantBuffers", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the editor material settings service no longer mirrors schema field values into core material fields.
    /// </summary>
    [Fact]
    public void MaterialAssetSettingsService_does_not_apply_shader_or_texture_fields_to_core_materials() {
        string source = File.ReadAllText(Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.editor",
            "managers",
            "asset",
            "MaterialAssetSettingsService.cs"));

        Assert.DoesNotContain("ApplyPlatformRuntimeFields", source, StringComparison.Ordinal);
        Assert.DoesNotContain("materialAsset.ShaderAssetId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("materialAsset.VertexProgram", source, StringComparison.Ordinal);
        Assert.DoesNotContain("materialAsset.PixelProgram", source, StringComparison.Ordinal);
        Assert.DoesNotContain("materialAsset.DiffuseTextureAssetId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("materialAsset.ConstantBuffers", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves the repository root from the current test assembly location.
    /// </summary>
    /// <returns>Absolute helengine repository root path.</returns>
    static string ResolveRepositoryRootPath() {
        string currentPath = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(currentPath)) {
            string rootMarkerPath = Path.Combine(currentPath, "engine", "helengine.editor", "helengine.editor.csproj");
            if (File.Exists(rootMarkerPath)) {
                return currentPath;
            }

            DirectoryInfo parentDirectory = Directory.GetParent(currentPath);
            if (parentDirectory == null) {
                break;
            }

            currentPath = parentDirectory.FullName;
        }

        throw new InvalidOperationException("Could not resolve the helengine repository root from the current test assembly location.");
    }
}
