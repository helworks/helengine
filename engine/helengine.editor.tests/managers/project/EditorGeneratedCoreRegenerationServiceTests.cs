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
            symbol => Assert.Equal("DESKTOP_PLATFORM", symbol),
            symbol => Assert.Equal("HELENGINE_CODEGEN_DISABLE_MENU_REFLECTION", symbol),
            symbol => Assert.Equal("HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION", symbol));
    }

    /// <summary>
    /// Verifies PS2 builds exclude desktop-only input symbols and include the PS2 runtime symbol.
    /// </summary>
    [Fact]
    public void Resolve_portable_input_preprocessor_symbols_returns_ps2_runtime_symbol_without_desktop_input_symbols() {
        PlatformDefinition definition = new(
            "ps2",
            "PS2",
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
            symbol => Assert.Equal("PS2_PLATFORM", symbol),
            symbol => Assert.Equal("HELENGINE_CODEGEN_DISABLE_MENU_REFLECTION", symbol),
            symbol => Assert.Equal("HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION", symbol));
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
    /// Verifies generated-core regeneration disables project-defined preprocessor symbols so platform-specific symbols come only from the selected build target.
    /// </summary>
    [Fact]
    public void Build_arguments_disables_project_defined_preprocessor_symbols() {
        PlatformDefinition platformDefinition = new(
            "ps2",
            "PS2",
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

        IReadOnlyList<string> arguments = EditorGeneratedCoreRegenerationService.BuildArguments(
            @"C:\tmp\fixture.csproj",
            @"C:\tmp\generated",
            platformDefinition,
            codegenProfile,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            []);

        Assert.Contains("--set", arguments);
        Assert.Contains("include-project-defined-preprocessor-symbols=false", arguments);
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

    /// <summary>
    /// Verifies generated-core normalization emits automatic native runtime component deserializers and patches the native registry to register them at startup.
    /// </summary>
    [Fact]
    public void Emit_generated_automatic_runtime_component_deserializers_writes_native_sources_and_patches_registry_registration() {
        string generatedCoreRootPath = Path.Combine(RootPath, "generated-runtime-component-deserializers");
        Directory.CreateDirectory(generatedCoreRootPath);
        File.WriteAllText(
            Path.Combine(generatedCoreRootPath, "RuntimeComponentRegistry.cpp"),
            "#include \"RuntimeComponentRegistry.hpp\"" + Environment.NewLine
            + "::RuntimeComponentRegistry* RuntimeComponentRegistry::CreateDefault()" + Environment.NewLine
            + "{" + Environment.NewLine
            + "::RuntimeComponentRegistry *registry = new ::RuntimeComponentRegistry();" + Environment.NewLine
            + "return registry;}" + Environment.NewLine);

        EditorGeneratedCoreRegenerationService.EmitGeneratedAutomaticRuntimeComponentDeserializers(generatedCoreRootPath);

        Assert.True(File.Exists(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeClipRectComponentDeserializer.hpp")));
        Assert.True(File.Exists(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeClipRectComponentDeserializer.cpp")));
        Assert.True(File.Exists(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeScrollComponentDeserializer.hpp")));
        Assert.True(File.Exists(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeScrollComponentDeserializer.cpp")));
        string registrySource = File.ReadAllText(Path.Combine(generatedCoreRootPath, "RuntimeComponentRegistry.cpp"));
        Assert.Contains("#include \"GeneratedRuntimeComponentDeserializerRegistration.hpp\"", registrySource, StringComparison.Ordinal);
        Assert.Contains("RegisterGeneratedRuntimeComponentDeserializers(registry);", registrySource, StringComparison.Ordinal);
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
    /// Verifies generated camera-render-settings source normalizes enum member access for C++ compilation.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_fixes_camera_render_settings_enum_member_access() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-camera-render-settings-enums");
        Directory.CreateDirectory(generatedCoreRootPath);
        string sourcePath = Path.Combine(generatedCoreRootPath, "CameraRenderSettings.cpp");
        File.WriteAllText(
            sourcePath,
            "CameraRenderSettings::CameraRenderSettings() {" + Environment.NewLine
            + "    this->set_DepthPrepassMode(this->DepthPrepassMode::Auto);" + Environment.NewLine
            + "    this->set_PostProcessTier(this->PostProcessTier::High);" + Environment.NewLine
            + "}" + Environment.NewLine);

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalizedSource = File.ReadAllText(sourcePath);
        Assert.Contains("this->set_DepthPrepassMode(::DepthPrepassMode::Auto);", normalizedSource);
        Assert.Contains("this->set_PostProcessTier(::PostProcessTier::High);", normalizedSource);
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
    /// Verifies generated array runtime support value-initializes allocated storage so pointer elements start as null.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_value_initializes_runtime_array_storage() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-runtime-array");
        Directory.CreateDirectory(Path.Combine(generatedCoreRootPath, "runtime"));
        string arrayPath = Path.Combine(generatedCoreRootPath, "runtime", "array.hpp");
        File.WriteAllText(
            arrayPath,
            "#pragma once\n"
            + "template<typename T>\n"
            + "class Array {\n"
            + "public:\n"
            + "    explicit Array(int32_t length)\n"
            + "        : Length(length), Data(length > 0 ? new T[length] : nullptr) {\n"
            + "    }\n"
            + "\n"
            + "    Array(std::initializer_list<T> values)\n"
            + "        : Length(static_cast<int32_t>(values.size())), Data(values.size() > 0 ? new T[values.size()] : nullptr) {\n"
            + "    }\n"
            + "};\n");

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);
        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalized = File.ReadAllText(arrayPath);
        Assert.Contains("new T[length]()", normalized);
        Assert.Contains("new T[values.size()]()", normalized);
        Assert.DoesNotContain("new T[length]()()", normalized);
        Assert.DoesNotContain("new T[values.size()]()()", normalized);
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
    /// Verifies generated native path support rewrites PS2 device-root path handling so packaged disc assets can be resolved without `std::filesystem`.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_rewrites_path_support_for_ps2_device_roots() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-ps2-path-support");
        Directory.CreateDirectory(Path.Combine(generatedCoreRootPath, "system", "io"));
        string sourcePath = Path.Combine(generatedCoreRootPath, "system", "io", "path.cpp");
        File.WriteAllText(
            sourcePath,
            "#include \"path.hpp\"\n"
            + "\n"
            + "#include \"helcpp_config.hpp\"\n"
            + "\n"
            + "#include <filesystem>\n"
            + "\n"
            + "std::string Path::Combine(const std::string& left, const std::string& right) {\n"
            + "    if (left.empty()) {\n"
            + "        return right;\n"
            + "    }\n"
            + "\n"
            + "    if (right.empty()) {\n"
            + "        return left;\n"
            + "    }\n"
            + "\n"
            + "    return (std::filesystem::path(left) / right).lexically_normal().string();\n"
            + "}\n"
            + "\n"
            + "std::string Path::GetDirectoryName(const std::string& path) {\n"
            + "    if (path.empty()) {\n"
            + "        return std::string();\n"
            + "    }\n"
            + "\n"
            + "    return std::filesystem::path(path).parent_path().string();\n"
            + "}\n"
            + "\n"
            + "std::string Path::GetFileName(const std::string& path) {\n"
            + "    if (path.empty()) {\n"
            + "        return std::string();\n"
            + "    }\n"
            + "\n"
            + "    return std::filesystem::path(path).filename().string();\n"
            + "}\n"
            + "\n"
            + "std::string Path::GetFullPath(const std::string& path) {\n"
            + "#if !HE_CPP_PLATFORM_IS_WINDOWS_HOST\n"
            + "    if (path.empty()) {\n"
            + "        return std::string(\".\");\n"
            + "    }\n"
            + "\n"
            + "    return std::filesystem::path(path).lexically_normal().string();\n"
            + "#else\n"
            + "    if (path.empty()) {\n"
            + "        return std::filesystem::current_path().string();\n"
            + "    }\n"
            + "\n"
            + "    return std::filesystem::absolute(std::filesystem::path(path)).lexically_normal().string();\n"
            + "#endif\n"
            + "}\n"
            + "\n"
            + "bool Path::IsPathRooted(const std::string& path) {\n"
            + "    if (path.empty()) {\n"
            + "        return false;\n"
            + "    }\n"
            + "\n"
            + "    return std::filesystem::path(path).is_absolute();\n"
            + "}\n");

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalized = File.ReadAllText(sourcePath);
        Assert.Contains("#if HE_CPP_PLATFORM_PS2", normalized);
        Assert.Contains("return CombinePs2Path(left, right);", normalized);
        Assert.Contains("return GetPs2DirectoryName(path);", normalized);
        Assert.Contains("return GetPs2FileName(path);", normalized);
        Assert.Contains("return NormalizePs2Path(path);", normalized);
        Assert.Contains("if (IsPs2DevicePath(path)) {", normalized);
    }

    /// <summary>
    /// Verifies generated native file support resolves PS2 packaged file reads through the builder-emitted physical path manifest instead of reconstructing disc aliases at runtime.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_rewrites_file_support_for_ps2_disc_reads() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-ps2-file-support");
        Directory.CreateDirectory(Path.Combine(generatedCoreRootPath, "system", "io"));
        string filePath = Path.Combine(generatedCoreRootPath, "system", "io", "file.cpp");
        string fileStreamHeaderPath = Path.Combine(generatedCoreRootPath, "system", "io", "file-stream.hpp");
        string fileStreamPath = Path.Combine(generatedCoreRootPath, "system", "io", "file-stream.cpp");
        File.WriteAllText(
            filePath,
            "#include \"file.hpp\"\n"
            + "#include <fstream>\n"
            + "\n"
            + "bool File::Exists(const char* fileName) {\n"
            + "\tif (!fileName)\n"
            + "\t{\n"
            + "\t\treturn false;\n"
            + "\t}\n"
            + "\n"
            + "\tstd::ifstream file(fileName);\n"
            + "\treturn file.good();\n"
            + "}\n"
            + "\n"
            + "FileStream* File::OpenRead(const char* filePath)\n"
            + "{\n"
            + "\treturn new FileStream(filePath, FileMode::Open, FileAccess::Read, FileShare::Read);\n"
            + "}\n");
        File.WriteAllText(
            fileStreamHeaderPath,
            "#ifndef FILE_STREAM_HPP\n"
            + "#define FILE_STREAM_HPP\n"
            + "\n"
            + "#include \"stream.hpp\"\n"
            + "#include <cstdio>\n"
            + "#include <string>\n"
            + "\n"
            + "class FileStream : public Stream {\n"
            + "private:\n"
            + "    std::FILE* file;\n"
            + "    size_t position;\n"
            + "    size_t length;\n"
            + "\n"
            + "public:\n"
            + "    FileStream(const char* path, FileMode mode);\n"
            + "};\n"
            + "\n"
            + "#endif // FILE_STREAM_HPP\n");
        File.WriteAllText(
            fileStreamPath,
            "#include \"file-stream.hpp\"\n"
            + "#include <stdexcept>\n"
            + "\n"
            + "FileStream::FileStream(const char* path, FileMode mode) : file(nullptr), position(0), length(0) {\n"
            + "    file = std::fopen(path, GetFileMode(mode));\n"
            + "    if (!file) {\n"
            + "        throw std::runtime_error(std::string(\"Failed to open file: \") + path);\n"
            + "    }\n"
            + "}\n");

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalizedFile = File.ReadAllText(filePath);
        string normalizedFileStreamHeader = File.ReadAllText(fileStreamHeaderPath);
        string normalizedFileStream = File.ReadAllText(fileStreamPath);
        Assert.Contains("#include \"runtime/runtime_ps2_asset_path_manifest.hpp\"", normalizedFile);
        Assert.Contains("#include <cstdio>", normalizedFile);
        Assert.Contains("#include <libcdvd.h>", normalizedFile);
        Assert.Contains("#if HE_CPP_PLATFORM_PS2", normalizedFile);
        Assert.Contains("NormalizePs2RuntimeAssetLookupPath", normalizedFile);
        Assert.Contains("BuildPs2RuntimeAssetLogicalLookupPath", normalizedFile);
        Assert.Contains("he_get_runtime_ps2_asset_physical_path(assetLookupPath.c_str())", normalizedFile);
        Assert.Contains("physicalPath == nullptr || physicalPath[0] == '\\0') && assetLookupPath.rfind(\"cdrom0:\", 0) == 0", normalizedFile);
        Assert.Contains("he_get_runtime_ps2_asset_physical_path(logicalLookupPath.c_str())", normalizedFile);
        Assert.Contains("ResolvePs2DiscSearchPath", normalizedFile);
        Assert.Contains("std::string physicalPs2Path = ResolvePs2DiscSearchPath(fileName);", normalizedFile);
        Assert.Contains("return sceCdSearchFile(&fileInfo, physicalPs2Path.c_str()) != 0;", normalizedFile);
        Assert.Contains("std::FILE* file = std::fopen(ResolvePs2DiscReadPath(fileName).c_str(), \"rb\");", normalizedFile);
        Assert.Contains("return new FileStream(ResolvePs2DiscReadPath(filePath), FileMode::Open, FileAccess::Read, FileShare::Read);", normalizedFile);
        Assert.Contains("#include <vector>", normalizedFileStreamHeader);
        Assert.Contains("bool usesMemoryBuffer;", normalizedFileStreamHeader);
        Assert.Contains("std::vector<uint8_t> memoryBuffer;", normalizedFileStreamHeader);
        Assert.Contains("#include \"runtime/runtime_ps2_asset_path_manifest.hpp\"", normalizedFileStream);
        Assert.Contains("#include <libcdvd.h>", normalizedFileStream);
        Assert.Contains("#include <malloc.h>", normalizedFileStream);
        Assert.Contains("#if HE_CPP_PLATFORM_PS2", normalizedFileStream);
        Assert.Contains("NormalizePs2RuntimeAssetLookupPath", normalizedFileStream);
        Assert.Contains("BuildPs2RuntimeAssetLogicalLookupPath", normalizedFileStream);
        Assert.Contains("he_get_runtime_ps2_asset_physical_path(assetLookupPath.c_str())", normalizedFileStream);
        Assert.Contains("physicalPath == nullptr || physicalPath[0] == '\\0') && assetLookupPath.rfind(\"cdrom0:\", 0) == 0", normalizedFileStream);
        Assert.Contains("he_get_runtime_ps2_asset_physical_path(logicalLookupPath.c_str())", normalizedFileStream);
        Assert.Contains("ResolvePs2DiscSearchPath", normalizedFileStream);
        Assert.Contains("std::vector<uint8_t> ReadPs2DiscFile(const std::string& path)", normalizedFileStream);
        Assert.Contains("sceCdSearchFile(&fileInfo, ResolvePs2DiscSearchPath(path).c_str()) == 0", normalizedFileStream);
        Assert.Contains("if (!ResolvePs2DiscPhysicalPath(path).empty())", normalizedFileStream);
        Assert.Contains("memoryBuffer = ReadPs2DiscFile(path);", normalizedFileStream);
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
    /// Verifies bundled native number support exposes finite-check helpers used by generated core validation code.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_adds_native_number_finite_helpers() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-native-number-finite-helpers");
        Directory.CreateDirectory(Path.Combine(generatedCoreRootPath, "system"));
        string headerPath = Path.Combine(generatedCoreRootPath, "system", "number.hpp");
        File.WriteAllText(
            headerPath,
            "#pragma once\n"
            + "#include <cmath>\n"
            + "class Number {\n"
            + "public:\n"
            + "    static bool IsPositiveInfinity(float value) {\n"
            + "        return std::isinf(value) && value > 0.0f;\n"
            + "    }\n"
            + "\n"
            + "    static bool IsPositiveInfinity(double value) {\n"
            + "        return std::isinf(value) && value > 0.0;\n"
            + "    }\n"
            + "};\n");

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalizedHeader = File.ReadAllText(headerPath);
        Assert.Contains("static bool IsNaN(float value)", normalizedHeader);
        Assert.Contains("return std::isnan(value);", normalizedHeader);
        Assert.Contains("static bool IsNaN(double value)", normalizedHeader);
        Assert.Contains("static bool IsInfinity(float value)", normalizedHeader);
        Assert.Contains("static bool IsInfinity(double value)", normalizedHeader);
    }

    /// <summary>
    /// Verifies generated animation-player looping code rewrites floating-point modulo into a valid native fmod call.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_rewrites_animation_player_floating_point_modulo() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-animation-player-floating-point-modulo");
        Directory.CreateDirectory(generatedCoreRootPath);
        string sourcePath = Path.Combine(generatedCoreRootPath, "AnimationPlayerComponent.cpp");
        File.WriteAllText(
            sourcePath,
            "#include \"AnimationPlayerComponent.hpp\"\n"
            + "float AnimationPlayerComponent::ResolvePlaybackTime(float time)\n"
            + "{\n"
            + "    const double duration = this->currentClip->get_Duration();\n"
            + "    double wrapped = time % duration;\n"
            + "    return static_cast<float>(wrapped);\n"
            + "}\n");

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalizedSource = File.ReadAllText(sourcePath);
        Assert.Contains("#include <cmath>", normalizedSource);
        Assert.Contains("double wrapped = std::fmod(static_cast<double>(time), duration);", normalizedSource);
    }

    /// <summary>
    /// Verifies generated menu-host source rewrites captured Action lambdas into valid native delegate construction.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_rewrites_menu_host_action_lambdas() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-menu-host-lambdas");
        Directory.CreateDirectory(generatedCoreRootPath);
        string sourcePath = Path.Combine(generatedCoreRootPath, "MenuComponent.cpp");
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
        string sourcePath = Path.Combine(generatedCoreRootPath, "MenuComponent.cpp");
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
        string headerPath = Path.Combine(generatedCoreRootPath, "MenuComponent.hpp");
        string sourcePath = Path.Combine(generatedCoreRootPath, "MenuComponent.cpp");
        File.WriteAllText(
            headerPath,
            "class MenuItemComponent;\n"
            + "class MenuComponent {\n"
            + "    InputGamepadState* PreviousGamepadState;\n"
            + "    InputGamepadState* ReadPrimaryGamepadState();\n"
            + "    bool WasGamepadButtonPressed(InputGamepadState* currentState, InputGamepadState* previousState, InputGamepadButton button);\n"
            + "};\n");
        File.WriteAllText(
            sourcePath,
            "InputGamepadState *currentGamepadState = inputSystem->GetGamepadState(0);\n"
            + "if (!currentGamepadState->Connected) { }\n"
            + "bool MenuComponent::WasGamepadButtonPressed(InputGamepadState* currentState, InputGamepadState* previousState, InputGamepadButton button) {\n"
            + "    return currentState->IsButtonDown(button) && !previousState->IsButtonDown(button);\n"
            + "}\n"
            + "InputGamepadState* MenuComponent::ReadPrimaryGamepadState() {\n"
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
        Assert.Contains("bool MenuComponent::WasGamepadButtonPressed(InputGamepadState currentState, InputGamepadState previousState, InputGamepadButton button)", normalizedSource);
        Assert.Contains("return currentState.IsButtonDown(button) && !previousState.IsButtonDown(button);", normalizedSource);
        Assert.Contains("InputGamepadState MenuComponent::ReadPrimaryGamepadState()", normalizedSource);
    }

    /// <summary>
    /// Verifies generated input-system source rewrites pointer delta mutations into setter-based assignments.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_rewrites_input_system_pointer_delta_assignments() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-input-system-pointer-deltas");
        Directory.CreateDirectory(generatedCoreRootPath);
        string sourcePath = Path.Combine(generatedCoreRootPath, "InputSystem.cpp");
        File.WriteAllText(
            sourcePath,
            "pointer.get_DeltaX() += pointerWrapDeltaOffset.X;\n"
            + "pointer.get_DeltaY() += pointerWrapDeltaOffset.Y;\n");

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalizedSource = File.ReadAllText(sourcePath);
        Assert.Contains("pointer.set_DeltaX(pointer.get_DeltaX() + pointerWrapDeltaOffset.X);", normalizedSource);
        Assert.Contains("pointer.set_DeltaY(pointer.get_DeltaY() + pointerWrapDeltaOffset.Y);", normalizedSource);
    }

    /// <summary>
    /// Verifies generated demo menu source keeps gamepad reads value-typed and preserves pointer-based component lookups.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_rewrites_demo_menu_component_template_arguments() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-demo-menu-component-template-arguments");
        Directory.CreateDirectory(generatedCoreRootPath);
        string headerPath = Path.Combine(generatedCoreRootPath, "MenuComponent.hpp");
        string sourcePath = Path.Combine(generatedCoreRootPath, "MenuComponent.cpp");
        File.WriteAllText(
            headerPath,
            "class MenuComponent {\n"
            + "    InputGamepadState* PreviousGamepadState;\n"
            + "    InputGamepadState* ReadPrimaryGamepadState();\n"
            + "    bool WasGamepadButtonPressed(InputGamepadState* currentState, InputGamepadState* previousState, InputGamepadButton button);\n"
            + "};\n");
        File.WriteAllText(
            sourcePath,
            "CollectEntitiesWithComponent<MenuItemComponent*>(panelEntity, itemEntities);\n"
            + "MenuItemComponent *itemComponent = FindRequiredComponent<MenuItemComponent*>(itemEntity);\n"
            + "RoundedRectComponent *backgroundComponent = FindRequiredComponent<RoundedRectComponent*>(itemEntity);\n"
            + "CollectEntitiesWithComponent<MenuPanelComponent*>(generatedRootEntity, panelEntities);\n"
            + "MenuPanelComponent *panelComponent = FindRequiredComponent<MenuPanelComponent*>(panelEntity);\n"
            + "CollectEntitiesWithComponent<MenuSelectedDescriptionComponent*>(panelEntity, markerEntities);\n"
            + "return FindRequiredComponent<TextComponent*>((*markerEntities)[0]);\n"
            + "InputGamepadState MenuComponent::ReadPrimaryGamepadState()\n"
            + "{\n"
            + "    if (Core::get_Instance() == nullptr || Core::get_Instance()->get_Input() == nullptr)\n"
            + "    {\n"
            + "return nullptr;    }\n"
            + "return Core::get_Instance()->get_Input()->GetGamepadState(0);}\n");

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalizedHeader = File.ReadAllText(headerPath);
        string normalizedSource = File.ReadAllText(sourcePath);
        Assert.Contains("InputGamepadState ReadPrimaryGamepadState();", normalizedHeader);
        Assert.Contains("CollectEntitiesWithComponent<MenuItemComponent*>(panelEntity, itemEntities);", normalizedSource);
        Assert.Contains("MenuItemComponent *itemComponent = FindRequiredComponent<MenuItemComponent*>(itemEntity);", normalizedSource);
        Assert.Contains("RoundedRectComponent *backgroundComponent = FindRequiredComponent<RoundedRectComponent*>(itemEntity);", normalizedSource);
        Assert.Contains("CollectEntitiesWithComponent<MenuPanelComponent*>(generatedRootEntity, panelEntities);", normalizedSource);
        Assert.Contains("MenuPanelComponent *panelComponent = FindRequiredComponent<MenuPanelComponent*>(panelEntity);", normalizedSource);
        Assert.Contains("CollectEntitiesWithComponent<MenuSelectedDescriptionComponent*>(panelEntity, markerEntities);", normalizedSource);
        Assert.Contains("return FindRequiredComponent<TextComponent*>((*markerEntities)[0]);", normalizedSource);
        Assert.Contains("return InputGamepadState();", normalizedSource);
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
    /// Verifies generated feature-manifest sources are rewritten from the conversion report instead of preserving stale disabled entries.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_rewrites_feature_manifest_entries_from_conversion_report() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-feature-manifest-source");
        Directory.CreateDirectory(Path.Combine(generatedCoreRootPath, "runtime"));
        string reportPath = Path.Combine(generatedCoreRootPath, "cpp-conversion-report.json");
        string sourcePath = Path.Combine(generatedCoreRootPath, "runtime", "feature_manifest.cpp");
        File.WriteAllText(
            reportPath,
            "{\n"
            + "  \"buildFeatures\": {\n"
            + "    \"decisions\": [\n"
            + "      { \"feature\": \"DebugOverlay\", \"enabled\": false, \"origin\": \"NotIncluded\" },\n"
            + "      { \"feature\": \"Render2D\", \"enabled\": true, \"origin\": \"AutoDetected\" },\n"
            + "      { \"feature\": \"Shaders\", \"enabled\": true, \"origin\": \"AutoDetected\" },\n"
            + "      { \"feature\": \"Sprites\", \"enabled\": true, \"origin\": \"AutoDetected\" },\n"
            + "      { \"feature\": \"Text2D\", \"enabled\": true, \"origin\": \"AutoDetected\" }\n"
            + "    ]\n"
            + "  }\n"
            + "}\n");
        File.WriteAllText(
            sourcePath,
            "#include \"feature_manifest.hpp\"\n"
            + "\n"
            + "static const HEFeatureEntry kFeatureEntries[] = {\n"
            + "    { HEFeature::DebugOverlay, false, HEFeatureDecisionOrigin::NotIncluded, \"DebugOverlay\" },\n"
            + "    { HEFeature::Render2D, false, HEFeatureDecisionOrigin::NotIncluded, \"Render2D\" },\n"
            + "    { HEFeature::Shaders, false, HEFeatureDecisionOrigin::NotIncluded, \"Shaders\" },\n"
            + "    { HEFeature::Sprites, false, HEFeatureDecisionOrigin::NotIncluded, \"Sprites\" },\n"
            + "    { HEFeature::Text2D, false, HEFeatureDecisionOrigin::NotIncluded, \"Text2D\" },\n"
            + "};\n");

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalizedSource = File.ReadAllText(sourcePath);
        Assert.Contains("{ HEFeature::Render2D, true, HEFeatureDecisionOrigin::AutoDetected, \"Render2D\" }", normalizedSource);
        Assert.Contains("{ HEFeature::Shaders, true, HEFeatureDecisionOrigin::AutoDetected, \"Shaders\" }", normalizedSource);
        Assert.Contains("{ HEFeature::Sprites, true, HEFeatureDecisionOrigin::AutoDetected, \"Sprites\" }", normalizedSource);
        Assert.Contains("{ HEFeature::Text2D, true, HEFeatureDecisionOrigin::AutoDetected, \"Text2D\" }", normalizedSource);
        Assert.Contains("{ HEFeature::DebugOverlay, false, HEFeatureDecisionOrigin::NotIncluded, \"DebugOverlay\" }", normalizedSource);
    }

    /// <summary>
    /// Verifies amalgamated translation regeneration excludes only the separately linked runtime manifest sources.
    /// </summary>
    [Fact]
    public void Rewrite_amalgamated_translation_unit_excludes_only_runtime_manifest_sources() {
        string generatedCoreRootPath = Path.Combine(RootPath, "rewrite-unity-exclusions");
        Directory.CreateDirectory(Path.Combine(generatedCoreRootPath, "runtime"));
        File.WriteAllText(Path.Combine(generatedCoreRootPath, "Foo.cpp"), "// foo\n");
        File.WriteAllText(Path.Combine(generatedCoreRootPath, "RendererBackendCapabilityProfile.cpp"), "// profile\n");
        File.WriteAllText(Path.Combine(generatedCoreRootPath, "Ps2MaterialAsset.cpp"), "// material\n");
        File.WriteAllText(Path.Combine(generatedCoreRootPath, "runtime", "runtime_startup_manifest.cpp"), "// startup\n");
        File.WriteAllText(Path.Combine(generatedCoreRootPath, "runtime", "runtime_code_module_manifest.cpp"), "// code module\n");
        File.WriteAllText(Path.Combine(generatedCoreRootPath, "helengine_core_amalgamated.cpp"), "// old amalgamated\n");

        EditorGeneratedCoreRegenerationService.RewriteAmalgamatedTranslationUnit(generatedCoreRootPath);

        string amalgamatedSource = File.ReadAllText(Path.Combine(generatedCoreRootPath, "helengine_core_amalgamated.cpp"));
        Assert.Contains("#include \"Foo.cpp\"", amalgamatedSource);
        Assert.Contains("#include \"RendererBackendCapabilityProfile.cpp\"", amalgamatedSource);
        Assert.Contains("#include \"Ps2MaterialAsset.cpp\"", amalgamatedSource);
        Assert.DoesNotContain("runtime/runtime_startup_manifest.cpp", amalgamatedSource);
        Assert.DoesNotContain("runtime/runtime_code_module_manifest.cpp", amalgamatedSource);
        Assert.False(File.Exists(Path.Combine(generatedCoreRootPath, "helengine_core_unity.cpp")));
    }

    /// <summary>
    /// Verifies generated editor-only inspector attribute sources are removed before runtime native compilation is normalized.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_removes_editor_only_attribute_sources() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-editor-only-attribute-sources");
        Directory.CreateDirectory(generatedCoreRootPath);
        File.WriteAllText(Path.Combine(generatedCoreRootPath, "EditorPropertyDisplayNameAttribute.hpp"), "#include \"Attribute.hpp\"\n");
        File.WriteAllText(Path.Combine(generatedCoreRootPath, "EditorPropertyDisplayNameAttribute.cpp"), "#include \"EditorPropertyDisplayNameAttribute.hpp\"\n");
        File.WriteAllText(Path.Combine(generatedCoreRootPath, "EditorPropertyHiddenAttribute.hpp"), "#include \"Attribute.hpp\"\n");
        File.WriteAllText(Path.Combine(generatedCoreRootPath, "EditorPropertyOrderAttribute.hpp"), "#include \"Attribute.hpp\"\n");
        File.WriteAllText(Path.Combine(generatedCoreRootPath, "Foo.cpp"), "// kept\n");

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        Assert.False(File.Exists(Path.Combine(generatedCoreRootPath, "EditorPropertyDisplayNameAttribute.hpp")));
        Assert.False(File.Exists(Path.Combine(generatedCoreRootPath, "EditorPropertyDisplayNameAttribute.cpp")));
        Assert.False(File.Exists(Path.Combine(generatedCoreRootPath, "EditorPropertyHiddenAttribute.hpp")));
        Assert.False(File.Exists(Path.Combine(generatedCoreRootPath, "EditorPropertyOrderAttribute.hpp")));
        Assert.True(File.Exists(Path.Combine(generatedCoreRootPath, "Foo.cpp")));
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
    /// Verifies generated component and entity headers drop the heavy includes that create the native component inheritance cycle during Windows export.
    /// </summary>
    [Fact]
    public void Normalize_generated_native_sources_removes_component_entity_include_cycle_headers() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-component-entity-include-cycle");
        Directory.CreateDirectory(generatedCoreRootPath);
        string componentHeaderPath = Path.Combine(generatedCoreRootPath, "Component.hpp");
        string entityHeaderPath = Path.Combine(generatedCoreRootPath, "Entity.hpp");
        File.WriteAllText(
            componentHeaderPath,
            "#pragma once\n"
            + "class Entity;\n"
            + "#include \"Entity.hpp\"\n"
            + "class Component {};\n");
        File.WriteAllText(
            entityHeaderPath,
            "#pragma once\n"
            + "class Component;\n"
            + "class Core;\n"
            + "class ObjectManager;\n"
            + "class ComponentExecutionPolicy;\n"
            + "#include \"Component.hpp\"\n"
            + "#include \"ComponentExecutionPolicy.hpp\"\n"
            + "#include \"Core.hpp\"\n"
            + "#include \"ObjectManager.hpp\"\n"
            + "#include \"float4.hpp\"\n"
            + "class Entity {};\n");

        EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath);

        string normalizedComponentHeader = File.ReadAllText(componentHeaderPath);
        string normalizedEntityHeader = File.ReadAllText(entityHeaderPath);
        Assert.DoesNotContain("#include \"Entity.hpp\"", normalizedComponentHeader, StringComparison.Ordinal);
        Assert.DoesNotContain("#include \"Component.hpp\"", normalizedEntityHeader, StringComparison.Ordinal);
        Assert.DoesNotContain("#include \"ComponentExecutionPolicy.hpp\"", normalizedEntityHeader, StringComparison.Ordinal);
        Assert.DoesNotContain("#include \"Core.hpp\"", normalizedEntityHeader, StringComparison.Ordinal);
        Assert.DoesNotContain("#include \"ObjectManager.hpp\"", normalizedEntityHeader, StringComparison.Ordinal);
        Assert.Contains("#include \"float4.hpp\"", normalizedEntityHeader, StringComparison.Ordinal);
    }
}
