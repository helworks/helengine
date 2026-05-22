namespace helengine.platforms;

/// <summary>
/// Describes one platform that can be selected in editor build settings.
/// </summary>
public sealed class AvailablePlatformDescriptor {
    /// <summary>
    /// Initializes one available-platform descriptor.
    /// </summary>
    /// <param name="id">Stable platform identifier written into project files.</param>
    /// <param name="displayName">Readable platform name shown in editor UI.</param>
    /// <param name="builderAssemblyPath">Absolute or relative path to the platform builder assembly, when available.</param>
    /// <param name="playerSourceRootPath">Absolute or relative path to the platform player source root, when available.</param>
    /// <param name="generatedCoreCppRootPath">Absolute or relative path to the generated core C++ root, when available.</param>
    /// <param name="codegenToolPath">Absolute or relative path to the bundled csharpcodegen executable, when available.</param>
    /// <param name="isInstalled">True when the platform payload exists on the current machine.</param>
    /// <param name="generatedCoreProjectPaths">Absolute project paths that should also be codegenned and merged into generated-core for this platform.</param>
    public AvailablePlatformDescriptor(
        string id,
        string displayName,
        string builderAssemblyPath = "",
        string playerSourceRootPath = "",
        bool isInstalled = true,
        string generatedCoreCppRootPath = "",
        string codegenToolPath = "",
        IReadOnlyList<string> generatedCoreProjectPaths = null) {
        Id = id;
        DisplayName = displayName;
        BuilderAssemblyPath = builderAssemblyPath ?? string.Empty;
        PlayerSourceRootPath = playerSourceRootPath ?? string.Empty;
        IsInstalled = isInstalled;
        GeneratedCoreCppRootPath = generatedCoreCppRootPath ?? string.Empty;
        CodegenToolPath = codegenToolPath ?? string.Empty;
        GeneratedCoreProjectPaths = generatedCoreProjectPaths ?? Array.Empty<string>();
    }

    /// <summary>
    /// Gets the stable platform identifier written into project files.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the readable platform name shown in editor UI.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the path to the platform builder assembly when the catalog provides one.
    /// </summary>
    public string BuilderAssemblyPath { get; }

    /// <summary>
    /// Gets the path to the platform player source root when the catalog provides one.
    /// </summary>
    public string PlayerSourceRootPath { get; }

    /// <summary>
    /// Gets the path to the generated core C++ root when the catalog provides one.
    /// </summary>
    public string GeneratedCoreCppRootPath { get; }

    /// <summary>
    /// Gets the path to the bundled csharpcodegen executable when the catalog provides one.
    /// </summary>
    public string CodegenToolPath { get; }

    /// <summary>
    /// Gets whether the platform payload exists on the current machine.
    /// </summary>
    public bool IsInstalled { get; }

    /// <summary>
    /// Gets the absolute managed project paths that should be merged into generated-core for this platform.
    /// </summary>
    public IReadOnlyList<string> GeneratedCoreProjectPaths { get; }
}
