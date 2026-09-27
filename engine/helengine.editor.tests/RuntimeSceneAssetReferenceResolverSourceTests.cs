namespace helengine.editor.tests;

/// <summary>
/// Verifies runtime scene-asset resolver accessors declare borrowed renderer ownership in their compiled metadata.
/// </summary>
public sealed class RuntimeSceneAssetReferenceResolverSourceTests {

    /// <summary>
    /// Ensures the renderer requirement helper declares its borrowed return boundary for native ownership analysis.
    /// </summary>
    [Fact]
    public void RequireRenderManager2D_return_value_is_declared_borrowed() {
        System.Reflection.MethodInfo method = typeof(RuntimeSceneAssetReferenceResolver).GetMethod(
            "RequireRenderManager2D",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);
        Assert.NotEmpty(method.GetCustomAttributes(typeof(NativeBorrowedReturnAttribute), false));
    }

    /// <summary>
    /// Ensures the 3D renderer requirement helper declares its borrowed return boundary for native ownership analysis.
    /// </summary>
    [Fact]
    public void RequireRenderManager3D_return_value_is_declared_borrowed() {
        System.Reflection.MethodInfo method = typeof(RuntimeSceneAssetReferenceResolver).GetMethod(
            "RequireRenderManager3D",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);
        Assert.NotEmpty(method.GetCustomAttributes(typeof(NativeBorrowedReturnAttribute), false));
    }
}
