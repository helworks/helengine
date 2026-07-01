namespace helengine.editor.tests;

/// <summary>
/// Verifies the renderer source reads the exact entity world matrix instead of recomposing a lossy decomposed transform.
/// </summary>
public sealed class RendererWorldMatrixSourceTests {
    /// <summary>
    /// Ensures the DirectX11 renderer reads the exact entity world matrix so parented non-uniform transforms preserve their authored composition.
    /// </summary>
    [Fact]
    public void DirectX11_renderer_source_uses_exact_entity_world_matrix() {
        string sourcePath = @"C:\dev\helworks\helengine\engine\helengine.directx11\DirectX11Renderer3D.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("return drawable.Parent.WorldTransformMatrix;", source, StringComparison.Ordinal);
        Assert.Contains("float4x4 world = parent.WorldTransformMatrix;", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the Vulkan renderer matches the same exact world-matrix source as the DirectX11 backend.
    /// </summary>
    [Fact]
    public void Vulkan_renderer_source_uses_exact_entity_world_matrix() {
        string sourcePath = @"C:\dev\helworks\helengine\engine\helengine.vulkan\VulkanRenderer3D.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Equal(2, CountOccurrences(source, "float4x4 world = entity.WorldTransformMatrix;"));
    }

    /// <summary>
    /// Ensures the DirectX11 standard-mesh path transposes the inverse-transpose normal matrix before shader upload so normal transforms follow the same HLSL matrix convention as position transforms.
    /// </summary>
    [Fact]
    public void DirectX11_renderer_source_transposes_uploaded_normal_matrix() {
        string sourcePath = @"C:\dev\helworks\helengine\engine\helengine.directx11\DirectX11Renderer3D.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("float4x4.Transpose(ref inverseTransposeNormalMatrix, out float4x4 normalMatrixTransposed);", source, StringComparison.Ordinal);
        Assert.Contains("NormalMatrix = normalMatrixTransposed,", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the Vulkan standard-mesh path matches the DirectX11 normal-matrix upload convention.
    /// </summary>
    [Fact]
    public void Vulkan_renderer_source_transposes_uploaded_normal_matrix() {
        string sourcePath = @"C:\dev\helworks\helengine\engine\helengine.vulkan\VulkanRenderer3D.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("float4x4.Transpose(ref normalMatrix, out float4x4 normalMatrixTransposed);", source, StringComparison.Ordinal);
        Assert.Contains("NormalMatrix = normalMatrixTransposed,", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Counts how many times one exact source fragment appears within one source file.
    /// </summary>
    /// <param name="source">Source text to scan.</param>
    /// <param name="fragment">Exact source fragment to count.</param>
    /// <returns>Occurrence count for the requested fragment.</returns>
    static int CountOccurrences(string source, string fragment) {
        if (string.IsNullOrEmpty(source)) {
            throw new ArgumentException("Source text must be provided.", nameof(source));
        }
        if (string.IsNullOrEmpty(fragment)) {
            throw new ArgumentException("Source fragment must be provided.", nameof(fragment));
        }

        int count = 0;
        int searchIndex = 0;
        while (true) {
            int foundIndex = source.IndexOf(fragment, searchIndex, StringComparison.Ordinal);
            if (foundIndex < 0) {
                return count;
            }

            count++;
            searchIndex = foundIndex + fragment.Length;
        }
    }
}
