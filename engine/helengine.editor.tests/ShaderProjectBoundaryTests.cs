namespace helengine.editor.tests;

/// <summary>
/// Verifies shader runtime contracts and managed shader compiler implementation are owned by distinct projects.
/// </summary>
public sealed class ShaderProjectBoundaryTests {
    /// <summary>
    /// Ensures the runtime project excludes compiler implementation while retaining runtime target and binding contracts.
    /// </summary>
    [Fact]
    public void Shader_runtime_and_compilation_sources_have_distinct_project_boundaries() {
        string repositoryRootPath = ResolveRepositoryRootPath();
        string runtimeProjectPath = Path.Combine(
            repositoryRootPath,
            "engine",
            "helengine.shader",
            "helengine.shader.csproj");
        string compilationProjectPath = Path.Combine(
            repositoryRootPath,
            "engine",
            "helengine.shader.compilation",
            "helengine.shader.compilation.csproj");
        string runtimeSourceRootPath = Path.Combine(
            repositoryRootPath,
            "engine",
            "helengine.shader",
            "shaders",
            "runtime");

        Assert.True(File.Exists(compilationProjectPath));

        string runtimeProject = File.ReadAllText(runtimeProjectPath);
        string compilationProject = File.ReadAllText(compilationProjectPath);

        Assert.Contains("shaders\\compilation\\**\\*.cs", runtimeProject, StringComparison.Ordinal);
        Assert.Contains("shaders\\packages\\**\\*.cs", runtimeProject, StringComparison.Ordinal);
        Assert.Contains("Compile Remove", runtimeProject, StringComparison.Ordinal);
        Assert.Contains("..\\helengine.shader\\shaders\\compilation\\**\\*.cs", compilationProject, StringComparison.Ordinal);
        Assert.Contains("..\\helengine.shader\\shaders\\packages\\**\\*.cs", compilationProject, StringComparison.Ordinal);
        Assert.Contains("..\\helengine.shader\\helengine.shader.csproj", compilationProject, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(runtimeSourceRootPath, "ShaderCompileTarget.cs")));
        Assert.True(File.Exists(Path.Combine(runtimeSourceRootPath, "ShaderTargetNames.cs")));
        Assert.True(File.Exists(Path.Combine(runtimeSourceRootPath, "ShaderBindingPolicy.cs")));
        Assert.True(File.Exists(Path.Combine(runtimeSourceRootPath, "ShaderBindingPolicies.cs")));
    }

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

    /// <summary>
    /// Resolves the HelEngine repository root by walking upward from the test assembly directory.
    /// </summary>
    /// <returns>Absolute path to the HelEngine repository root.</returns>
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

        throw new InvalidOperationException("Could not resolve the HelEngine repository root from the current test assembly location.");
    }
}
