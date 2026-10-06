namespace helengine.editor.tests;

/// <summary>
/// Verifies shader runtime contracts and managed shader compiler implementation are owned by distinct projects.
/// </summary>
public sealed class ShaderProjectBoundaryTests {

    /// <summary>
    /// Ensures runtime shader contracts and managed compiler services are emitted by their intended assemblies.
    /// </summary>
    [Fact]
    public void Runtime_contracts_and_compiler_services_are_emitted_by_separate_assemblies() {
        System.Reflection.Assembly runtimeAssembly = typeof(ShaderCompileTarget).Assembly;
        System.Reflection.Assembly compilationAssembly = typeof(ShaderCompileService).Assembly;

        Assert.Equal("helengine.shader", runtimeAssembly.GetName().Name);
        Assert.Equal("helengine.shader.compilation", compilationAssembly.GetName().Name);
        Assert.Same(runtimeAssembly, typeof(ShaderBindingPolicy).Assembly);
        Assert.Null(runtimeAssembly.GetType("helengine.ShaderCompileService"));
        Assert.Null(runtimeAssembly.GetType("helengine.HlslShaderBindingParser"));
        Assert.Null(runtimeAssembly.GetType("helengine.ShaderModulePackageReader"));
        Assert.Null(runtimeAssembly.GetType("helengine.ShaderModulePackage"));
        Assert.NotNull(compilationAssembly.GetType("helengine.ShaderCompileService"));
        Assert.NotNull(compilationAssembly.GetType("helengine.HlslShaderBindingParser"));
        Assert.NotNull(compilationAssembly.GetType("helengine.ShaderModulePackageReader"));
        Assert.NotNull(compilationAssembly.GetType("helengine.ShaderModulePackage"));
    }
}
