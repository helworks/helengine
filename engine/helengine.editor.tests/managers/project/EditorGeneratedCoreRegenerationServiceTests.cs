using helengine.baseplatform.Definitions;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the shared generated-core regeneration service merges portable input output and resolves platform feature symbols.
/// </summary>
public sealed class EditorGeneratedCoreRegenerationServiceTests : IDisposable {
    /// <summary>
    /// Temporary workspace used by the merge-helper test.
    /// </summary>
    readonly string RootPath;

    /// <summary>
    /// Initializes the test workspace.
    /// </summary>
    public EditorGeneratedCoreRegenerationServiceTests() {
        RootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-core-service-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    /// <summary>
    /// Releases the temporary workspace used by the tests.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(RootPath)) {
            Directory.Delete(RootPath, true);
        }
    }

    /// <summary>
    /// Verifies the merge helper copies portable native source files while skipping non-source support files.
    /// </summary>
    [Fact]
    public void Merge_generated_source_tree_copies_cpp_and_hpp_files_but_skips_support_files() {
        string sourceRootPath = Path.Combine(RootPath, "source");
        string destinationRootPath = Path.Combine(RootPath, "destination");
        Directory.CreateDirectory(Path.Combine(sourceRootPath, "nested"));
        Directory.CreateDirectory(destinationRootPath);

        File.WriteAllText(Path.Combine(sourceRootPath, "InputSystem.cpp"), "// source");
        File.WriteAllText(Path.Combine(sourceRootPath, "InputSystem.hpp"), "// header");
        File.WriteAllText(Path.Combine(sourceRootPath, "helcpp_config.hpp"), "// config");
        File.WriteAllText(Path.Combine(sourceRootPath, "readme.txt"), "skip");
        File.WriteAllText(Path.Combine(sourceRootPath, "nested", "KeyboardState.cpp"), "// nested");

        EditorGeneratedCoreRegenerationService.MergeGeneratedSourceTree(sourceRootPath, destinationRootPath);

        Assert.True(File.Exists(Path.Combine(destinationRootPath, "InputSystem.cpp")));
        Assert.True(File.Exists(Path.Combine(destinationRootPath, "InputSystem.hpp")));
        Assert.True(File.Exists(Path.Combine(destinationRootPath, "nested", "KeyboardState.cpp")));
        Assert.False(File.Exists(Path.Combine(destinationRootPath, "helcpp_config.hpp")));
        Assert.False(File.Exists(Path.Combine(destinationRootPath, "readme.txt")));
    }

    /// <summary>
    /// Verifies Windows builds keep keyboard and mouse enabled for portable input conversion.
    /// </summary>
    [Fact]
    public void Resolve_portable_input_preprocessor_symbols_returns_keyboard_and_mouse_for_windows() {
        PlatformDefinition definition = new(
            "windows",
            "Windows",
            Array.Empty<PlatformBuildProfileDefinition>(),
            Array.Empty<PlatformGraphicsProfileDefinition>(),
            Array.Empty<PlatformAssetRequirementDefinition>(),
            Array.Empty<PlatformMaterialSchemaDefinition>(),
            Array.Empty<PlatformComponentCompatibilityDefinition>(),
            Array.Empty<PlatformCodegenProfileDefinition>(),
            Array.Empty<PlatformStorageProfileDefinition>(),
            Array.Empty<PlatformMediaProfileDefinition>());

        IReadOnlyList<string> symbols = EditorGeneratedCoreRegenerationService.ResolvePortableInputPreprocessorSymbols(definition);

        Assert.Collection(
            symbols,
            symbol => Assert.Equal("HELENGINE_INPUT_KEYBOARD", symbol),
            symbol => Assert.Equal("HELENGINE_INPUT_MOUSE", symbol));
    }
}
