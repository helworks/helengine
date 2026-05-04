using helengine.baseplatform.Definitions;
using helengine.baseplatform.Profiles;

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
    /// Verifies the regeneration service supplements missing bundled runtime support files without overwriting generated files.
    /// </summary>
    [Fact]
    public void Merge_bundled_runtime_support_tree_copies_missing_runtime_files_without_overwriting_existing_files() {
        string bundledRootPath = Path.Combine(RootPath, "bundled");
        string destinationRootPath = Path.Combine(RootPath, "generated");
        Directory.CreateDirectory(Path.Combine(bundledRootPath, "runtime"));
        Directory.CreateDirectory(Path.Combine(bundledRootPath, "system"));
        Directory.CreateDirectory(Path.Combine(destinationRootPath, "runtime"));

        File.WriteAllText(Path.Combine(bundledRootPath, "runtime", "native_type.hpp"), "// bundled type");
        File.WriteAllText(Path.Combine(bundledRootPath, "system", "guid.hpp"), "// bundled guid");
        File.WriteAllText(Path.Combine(destinationRootPath, "runtime", "array.hpp"), "// generated array");
        File.WriteAllText(Path.Combine(destinationRootPath, "runtime", "native_type.hpp"), "// generated type");

        EditorGeneratedCoreRegenerationService.MergeBundledRuntimeSupportTree(bundledRootPath, destinationRootPath);

        Assert.Equal("// generated type", File.ReadAllText(Path.Combine(destinationRootPath, "runtime", "native_type.hpp")));
        Assert.Equal("// bundled guid", File.ReadAllText(Path.Combine(destinationRootPath, "system", "guid.hpp")));
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
            symbol => Assert.Equal("HELENGINE_INPUT_MOUSE", symbol),
            symbol => Assert.Equal("HELENGINE_CODEGEN_DISABLE_MENU_REFLECTION", symbol));
    }

    /// <summary>
    /// Verifies the generated-core regeneration service forwards a selected codegen preset through the dedicated preset argument.
    /// </summary>
    [Fact]
    public void Build_arguments_includes_selected_preset_id() {
        PlatformDefinition platformDefinition = new(
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
        PlatformCodegenProfileDefinition codegenProfile = new(
            "default",
            "Default",
            "Default codegen profile",
            PlatformCodegenLanguage.Cpp,
            PlatformSerializationEndianness.LittleEndian,
            []);
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase) {
            [PlatformCodegenSettingIds.PresetId] = "ps2-lite"
        };
        string projectPath = @"C:\tmp\fixture.csproj";
        string outputRootPath = @"C:\tmp\generated";

        IReadOnlyList<string> arguments = EditorGeneratedCoreRegenerationService.BuildArguments(
            projectPath,
            outputRootPath,
            platformDefinition,
            codegenProfile,
            values,
            []);

        Assert.Contains("--preset", arguments);
        Assert.Contains("ps2-lite", arguments);
        Assert.DoesNotContain($"{PlatformCodegenSettingIds.PresetId}=ps2-lite", arguments);
    }

    /// <summary>
    /// Verifies generated sources that reference AppContext receive the bundled AppContext include during normalization.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_adds_app_context_include_when_core_initialization_options_uses_base_directory() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-app-context");
        Directory.CreateDirectory(generatedCoreRootPath);
        string sourcePath = Path.Combine(generatedCoreRootPath, "CoreInitializationOptions.cpp");
        File.WriteAllText(
            sourcePath,
            "#include \"CoreInitializationOptions.hpp\"" + Environment.NewLine
            + "CoreInitializationOptions::CoreInitializationOptions() : ContentRootPath(AppContext::BaseDirectory) {}" + Environment.NewLine);

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalized = File.ReadAllText(sourcePath);
        Assert.Contains("#include \"system/app_context.hpp\"", normalized);
    }

    /// <summary>
    /// Verifies generated light-component sources normalize enum member access for C++ compilation.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_fixes_light_component_enum_member_access() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-light-enums");
        Directory.CreateDirectory(generatedCoreRootPath);
        string directionalPath = Path.Combine(generatedCoreRootPath, "DirectionalLightComponent.cpp");
        string lightBasePath = Path.Combine(generatedCoreRootPath, "LightComponent.cpp");
        File.WriteAllText(
            directionalPath,
            "DirectionalLightComponent::DirectionalLightComponent() : LightComponent(LightType.Directional) {}" + Environment.NewLine);
        File.WriteAllText(
            lightBasePath,
            "void LightComponent::Reset() { this->set_ShadowMapMode(this->ShadowMapMode::Auto); }" + Environment.NewLine);

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        Assert.Contains("LightType::Directional", File.ReadAllText(directionalPath));
        Assert.Contains("::ShadowMapMode::Auto", File.ReadAllText(lightBasePath));
    }

    /// <summary>
    /// Verifies generated dictionary runtime support gains a Clear helper required by converted menu code.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_adds_clear_helper_to_native_dictionary_support() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-native-dictionary");
        Directory.CreateDirectory(Path.Combine(generatedCoreRootPath, "runtime"));
        string dictionaryPath = Path.Combine(generatedCoreRootPath, "runtime", "native_dictionary.hpp");
        File.WriteAllText(
            dictionaryPath,
            "#pragma once\n"
            + "template<typename TKey, typename TValue>\n"
            + "class Dictionary {\n"
            + "public:\n"
            + "    bool Remove(const TKey& key) {\n"
            + "        return this->erase(key) > 0;\n"
            + "    }\n"
            + "\n"
            + "    bool TryGetValue(const TKey& key, TValue& value) const {\n"
            + "        return false;\n"
            + "    }\n"
            + "};\n");

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalized = File.ReadAllText(dictionaryPath);
        Assert.Contains("void Clear()", normalized);
        Assert.Contains("this->clear();", normalized);
    }

    /// <summary>
    /// Verifies generated path runtime support gains ChangeExtension required by converted menu code.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_adds_change_extension_to_path_support() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-path-change-extension");
        Directory.CreateDirectory(Path.Combine(generatedCoreRootPath, "system", "io"));
        string headerPath = Path.Combine(generatedCoreRootPath, "system", "io", "path.hpp");
        string sourcePath = Path.Combine(generatedCoreRootPath, "system", "io", "path.cpp");
        File.WriteAllText(
            headerPath,
            "#ifndef PATH_HPP\n"
            + "#define PATH_HPP\n"
            + "#include <string>\n"
            + "class Path {\n"
            + "public:\n"
            + "    static std::string Combine(const std::string& left, const std::string& right);\n"
            + "    static std::string GetFileName(const std::string& path);\n"
            + "};\n"
            + "#endif // PATH_HPP\n");
        File.WriteAllText(
            sourcePath,
            "#include \"path.hpp\"\n"
            + "#include <filesystem>\n"
            + "std::string Path::Combine(const std::string& left, const std::string& right) {\n"
            + "    return (std::filesystem::path(left) / right).string();\n"
            + "}\n"
            + "std::string Path::GetFileName(const std::string& path) {\n"
            + "    return std::filesystem::path(path).filename().string();\n"
            + "}\n");

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalizedHeader = File.ReadAllText(headerPath);
        string normalizedSource = File.ReadAllText(sourcePath);
        Assert.Contains("static std::string ChangeExtension(const std::string& path, const std::string& extension);", normalizedHeader);
        Assert.Contains("std::string Path::ChangeExtension(const std::string& path, const std::string& extension)", normalizedSource);
        Assert.Contains("replace_extension", normalizedSource);
    }

    /// <summary>
    /// Verifies bundled native Action support accepts captured callables emitted by generated UI code.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_adds_callable_action_support() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-action-callable-support");
        Directory.CreateDirectory(Path.Combine(generatedCoreRootPath, "system"));
        string headerPath = Path.Combine(generatedCoreRootPath, "system", "action.hpp");
        string implementationPath = Path.Combine(generatedCoreRootPath, "system", "action.tpp");
        File.WriteAllText(
            headerPath,
            "#ifndef ACTION_HPP\n"
            + "#define ACTION_HPP\n"
            + "\n"
            + "template<typename... TArgs>\n"
            + "class Action {\n"
            + "private:\n"
            + "    using FuncType = void(*)(TArgs...);\n"
            + "    FuncType func = nullptr;\n"
            + "\n"
            + "public:\n"
            + "    Action() = default;\n"
            + "    explicit Action(FuncType f);\n"
            + "    void operator()(TArgs... args) const;\n"
            + "    explicit operator bool() const;\n"
            + "};\n"
            + "\n"
            + "#include \"action.tpp\"\n"
            + "\n"
            + "#endif // ACTION_HPP\n");
        File.WriteAllText(
            implementationPath,
            "#ifndef ACTION_TPP\n"
            + "#define ACTION_TPP\n"
            + "\n"
            + "#include \"action.hpp\"\n"
            + "\n"
            + "template<typename... TArgs>\n"
            + "Action<TArgs...>::Action(FuncType f) : func(f) {}\n"
            + "\n"
            + "template<typename... TArgs>\n"
            + "void Action<TArgs...>::operator()(TArgs... args) const {\n"
            + "    if (func) {\n"
            + "        func(args...);\n"
            + "    }\n"
            + "}\n"
            + "\n"
            + "template<typename... TArgs>\n"
            + "Action<TArgs...>::operator bool() const {\n"
            + "    return func != nullptr;\n"
            + "}\n"
            + "\n"
            + "#endif // ACTION_TPP\n");

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalizedHeader = File.ReadAllText(headerPath);
        string normalizedImplementation = File.ReadAllText(implementationPath);
        Assert.Contains("#include <functional>", normalizedHeader);
        Assert.Contains("std::function<void(TArgs...)> func{};", normalizedHeader);
        Assert.Contains("template<typename TCallable>", normalizedHeader);
        Assert.Contains("explicit Action(TCallable f) : func(f) {}", normalizedHeader);
        Assert.Contains("return static_cast<bool>(func);", normalizedImplementation);
    }

    /// <summary>
    /// Verifies bundled native string support exposes the managed-style single-character replace helper.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_adds_native_string_replace_support() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-native-string-replace-support");
        Directory.CreateDirectory(Path.Combine(generatedCoreRootPath, "runtime"));
        string headerPath = Path.Combine(generatedCoreRootPath, "runtime", "native_string.hpp");
        File.WriteAllText(
            headerPath,
            "#pragma once\n"
            + "#include <string>\n"
            + "class String {\n"
            + "public:\n"
            + "    static std::string Insert(const std::string& value, int32_t startIndex, const std::string& insertion) {\n"
            + "        return value;\n"
            + "    }\n"
            + "};\n");

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalizedHeader = File.ReadAllText(headerPath);
        Assert.Contains("static std::string Replace(const std::string& value, char oldValue, char newValue)", normalizedHeader);
        Assert.Contains("std::replace(replaced.begin(), replaced.end(), oldValue, newValue);", normalizedHeader);
    }

    /// <summary>
    /// Verifies generated menu-host source rewrites captured Action lambdas into valid native delegate construction.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_rewrites_menu_host_action_lambdas() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-menu-host-lambdas");
        Directory.CreateDirectory(generatedCoreRootPath);
        string sourcePath = Path.Combine(generatedCoreRootPath, "MenuHostComponent.cpp");
        File.WriteAllText(
            sourcePath,
            "::ButtonComponent *button = ([&]() {\n"
            + "auto __ctor_arg_1 = () => this->ActivateItem(runtimeItem);\n"
            + ";\n"
            + "return new ::ButtonComponent(label, size, font, __ctor_arg_1, 2);\n"
            + "})();\n"
            + "button->Hovered += () => this->HandleItemHovered(runtimeItem);\n"
            + ";\n");

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalized = File.ReadAllText(sourcePath);
        Assert.Contains("auto __ctor_arg_1 = new Action<>([&]() { this->ActivateItem(runtimeItem); });", normalized);
        Assert.Contains("button->Hovered += [&]() { this->HandleItemHovered(runtimeItem); };", normalized);
    }

    /// <summary>
    /// Verifies generated menu-host source rewrites managed string replace calls into bundled native string helpers.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_rewrites_menu_host_string_replace_calls() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-menu-host-replace-calls");
        Directory.CreateDirectory(generatedCoreRootPath);
        string sourcePath = Path.Combine(generatedCoreRootPath, "MenuHostComponent.cpp");
        File.WriteAllText(
            sourcePath,
            "const std::string normalizedRelativePath = relativePath.Replace('/', Path::DirectorySeparatorChar).Replace('\\\\', Path::DirectorySeparatorChar);\n"
            + "return relativePath.Replace('\\\\', '/');\n");

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalized = File.ReadAllText(sourcePath);
        Assert.Contains("const std::string normalizedRelativePath = String::Replace(String::Replace(relativePath, '/', Path::DirectorySeparatorChar), '\\\\', Path::DirectorySeparatorChar);", normalized);
        Assert.Contains("return String::Replace(relativePath, '\\\\', '/');", normalized);
    }

    /// <summary>
    /// Verifies generated menu-host source keeps value semantics for portable gamepad state access.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_rewrites_menu_host_gamepad_state_value_semantics() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-menu-host-gamepad-state");
        Directory.CreateDirectory(generatedCoreRootPath);
        string headerPath = Path.Combine(generatedCoreRootPath, "MenuHostComponent.hpp");
        string sourcePath = Path.Combine(generatedCoreRootPath, "MenuHostComponent.cpp");
        File.WriteAllText(
            headerPath,
            "class MenuHostComponent {\n"
            + "    InputGamepadState* PreviousGamepadState;\n"
            + "    InputGamepadState* ReadPrimaryGamepadState();\n"
            + "    bool WasGamepadButtonPressed(InputGamepadState* currentState, InputGamepadState* previousState, InputGamepadButton button);\n"
            + "};\n");
        File.WriteAllText(
            sourcePath,
            "InputGamepadState *currentGamepadState = inputSystem->GetGamepadState(0);\n"
            + "if (!currentGamepadState->Connected) { }\n"
            + "bool MenuHostComponent::WasGamepadButtonPressed(InputGamepadState* currentState, InputGamepadState* previousState, InputGamepadButton button) {\n"
            + "    return currentState->IsButtonDown(button) && !previousState->IsButtonDown(button);\n"
            + "}\n"
            + "InputGamepadState* MenuHostComponent::ReadPrimaryGamepadState() {\n"
            + "    return this->ResolveInputSystem()->GetGamepadState(0);\n"
            + "}\n");

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalizedHeader = File.ReadAllText(headerPath);
        string normalizedSource = File.ReadAllText(sourcePath);
        Assert.Contains("InputGamepadState PreviousGamepadState;", normalizedHeader);
        Assert.Contains("InputGamepadState ReadPrimaryGamepadState();", normalizedHeader);
        Assert.Contains("bool WasGamepadButtonPressed(InputGamepadState currentState, InputGamepadState previousState, InputGamepadButton button);", normalizedHeader);
        Assert.Contains("InputGamepadState currentGamepadState = inputSystem->GetGamepadState(0);", normalizedSource);
        Assert.Contains("if (!currentGamepadState.Connected)", normalizedSource);
        Assert.Contains("bool MenuHostComponent::WasGamepadButtonPressed(InputGamepadState currentState, InputGamepadState previousState, InputGamepadButton button)", normalizedSource);
        Assert.Contains("return currentState.IsButtonDown(button) && !previousState.IsButtonDown(button);", normalizedSource);
        Assert.Contains("InputGamepadState MenuHostComponent::ReadPrimaryGamepadState()", normalizedSource);
    }

    /// <summary>
    /// Verifies generated feature-manifest headers include every runtime feature emitted by the generated manifest body.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_adds_missing_feature_manifest_entries() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-feature-manifest-entries");
        Directory.CreateDirectory(Path.Combine(generatedCoreRootPath, "runtime"));
        string headerPath = Path.Combine(generatedCoreRootPath, "runtime", "feature_manifest.hpp");
        File.WriteAllText(
            headerPath,
            "#pragma once\n"
            + "#include <cstddef>\n"
            + "enum class HEFeature {\n"
            + "    Render2D,\n"
            + "    Sprites,\n"
            + "    Text2D,\n"
            + "    Shaders,\n"
            + "    DebugOverlay\n"
            + "};\n");

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalizedHeader = File.ReadAllText(headerPath);
        Assert.Contains("    DebugOverlay,", normalizedHeader);
        Assert.Contains("    HostFileSystem,", normalizedHeader);
        Assert.Contains("    ReflectionLikeRuntime,", normalizedHeader);
        Assert.Contains("    RuntimeJson,", normalizedHeader);
        Assert.Contains("    TextProcessing", normalizedHeader);
    }

    /// <summary>
    /// Verifies generated shader include resolvers include bundled directory helpers before calling Directory::Exists.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_adds_directory_include_to_shader_filesystem_include_resolver() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-shader-filesystem-include-resolver");
        Directory.CreateDirectory(generatedCoreRootPath);
        string sourcePath = Path.Combine(generatedCoreRootPath, "ShaderFilesystemIncludeResolver.cpp");
        File.WriteAllText(
            sourcePath,
            "#include \"ShaderFilesystemIncludeResolver.hpp\"\n"
            + "#include \"system/io/path.hpp\"\n"
            + "#include \"system/io/file.hpp\"\n"
            + "void Test() {\n"
            + "    if (!Directory::Exists(rootDirectory)) { }\n"
            + "}\n");

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalizedSource = File.ReadAllText(sourcePath);
        Assert.Contains("#include \"system/io/directory.hpp\"", normalizedSource);
    }

    /// <summary>
    /// Verifies regeneration argument building preserves authored symbols while appending scene-derived stripping symbols.
    /// </summary>
    [Fact]
    public void Build_arguments_combines_selected_and_scene_feature_preprocessor_symbols() {
        PlatformDefinition platformDefinition = new(
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
        PlatformCodegenProfileDefinition codegenProfile = new(
            "default",
            "Default",
            "Default codegen profile",
            PlatformCodegenLanguage.Cpp,
            PlatformSerializationEndianness.LittleEndian,
            []);
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase) {
            ["additional-preprocessor-symbols"] = "EXISTING_SYMBOL"
        };

        IReadOnlyList<string> arguments = EditorGeneratedCoreRegenerationService.BuildArguments(
            @"C:\tmp\fixture.csproj",
            @"C:\tmp\generated",
            platformDefinition,
            codegenProfile,
            values,
            [
                PhysicsSceneFeatureSymbolCatalog3D.SceneFeatureStrippingSymbol,
                PhysicsSceneFeatureSymbolCatalog3D.BoxBoxContactSymbol
            ]);

        Assert.Contains("--set", arguments);
        Assert.Contains(
            "additional-preprocessor-symbols=EXISTING_SYMBOL;HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES;HELENGINE_PHYSICS3D_FEATURE_BOX_BOX_CONTACT",
            arguments);
    }

    /// <summary>
    /// Verifies portable input symbols and scene-derived stripping symbols merge into one ordered unique list.
    /// </summary>
    [Fact]
    public void Combine_additional_preprocessor_symbols_merges_ordered_unique_values() {
        IReadOnlyList<string> symbols = EditorGeneratedCoreRegenerationService.CombineAdditionalPreprocessorSymbols(
            ["HELENGINE_INPUT_KEYBOARD", "HELENGINE_INPUT_MOUSE"],
            ["HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES", "HELENGINE_INPUT_MOUSE"]);

        Assert.Collection(
            symbols,
            symbol => Assert.Equal("HELENGINE_INPUT_KEYBOARD", symbol),
            symbol => Assert.Equal("HELENGINE_INPUT_MOUSE", symbol),
            symbol => Assert.Equal("HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES", symbol));
    }
}
