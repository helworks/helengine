using helengine.baseplatform.Definitions;
using helengine.baseplatform.Profiles;
using helengine.editor.tests.testing;
using System.Text.Json;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the shared generated-core regeneration service merges portable input output, exposes deterministic codegen inputs, and no longer rewrites generated native C++ sources.
/// </summary>
public sealed class EditorGeneratedCoreRegenerationServiceTests : IDisposable {
    /// <summary>
    /// Temporary workspace used by the tests.
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
    /// Verifies merged generated-core reports promote shader feature detection from shader-only generated projects into the combined report consumed by build summaries and feature manifests.
    /// </summary>
    [Fact]
    public void Merge_generated_conversion_report_promotes_shader_feature_from_shader_project() {
        string sourceRootPath = Path.Combine(RootPath, "shader-source");
        string destinationRootPath = Path.Combine(RootPath, "generated-core");
        Directory.CreateDirectory(sourceRootPath);
        Directory.CreateDirectory(destinationRootPath);

        File.WriteAllText(
            Path.Combine(destinationRootPath, "cpp-conversion-report.json"),
            "{\n"
            + "  \"assemblyName\": \"helengine.core\",\n"
            + "  \"buildFeatures\": {\n"
            + "    \"decisions\": [\n"
            + "      { \"feature\": \"Render2D\", \"enabled\": true, \"origin\": \"AutoDetected\" },\n"
            + "      { \"feature\": \"Shaders\", \"enabled\": false, \"origin\": \"NotIncluded\" }\n"
            + "    ],\n"
            + "    \"detectedRoots\": [\n"
            + "      { \"feature\": \"Render2D\", \"rootId\": \"helengine.RenderManager2D\", \"sourceKind\": \"TypeReference\" }\n"
            + "    ],\n"
            + "    \"conflicts\": []\n"
            + "  }\n"
            + "}\n");
        File.WriteAllText(
            Path.Combine(sourceRootPath, "cpp-conversion-report.json"),
            "{\n"
            + "  \"assemblyName\": \"helengine.shader\",\n"
            + "  \"buildFeatures\": {\n"
            + "    \"decisions\": [\n"
            + "      { \"feature\": \"Shaders\", \"enabled\": true, \"origin\": \"AutoDetected\" }\n"
            + "    ],\n"
            + "    \"detectedRoots\": [\n"
            + "      { \"feature\": \"Shaders\", \"rootId\": \"helengine.ShaderRuntimeMaterial\", \"sourceKind\": \"TypeReference\" }\n"
            + "    ],\n"
            + "    \"conflicts\": []\n"
            + "  }\n"
            + "}\n");

        EditorGeneratedCoreRegenerationService.MergeGeneratedConversionReport(sourceRootPath, destinationRootPath);

        string combinedReport = File.ReadAllText(Path.Combine(destinationRootPath, "cpp-conversion-report.json"));
        using JsonDocument document = JsonDocument.Parse(combinedReport);
        Assert.Equal("helengine.core", document.RootElement.GetProperty("assemblyName").GetString());
        Assert.Contains(
            document.RootElement.GetProperty("buildFeatures").GetProperty("decisions").EnumerateArray(),
            decision => decision.GetProperty("feature").GetString() == "Shaders"
                && decision.GetProperty("enabled").GetBoolean()
                && decision.GetProperty("origin").GetString() == "AutoDetected");
        Assert.Contains(
            document.RootElement.GetProperty("buildFeatures").GetProperty("detectedRoots").EnumerateArray(),
            root => root.GetProperty("feature").GetString() == "Shaders"
                && root.GetProperty("rootId").GetString() == "helengine.ShaderRuntimeMaterial");
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
        PlatformDefinition definition = CreatePlatformDefinition("windows", runtimeGenerationContract: null);

        IReadOnlyList<string> symbols = EditorGeneratedCoreRegenerationService.ResolvePortableInputPreprocessorSymbols(definition);

        Assert.Collection(
            symbols,
            symbol => Assert.Equal("HELENGINE_INPUT_KEYBOARD", symbol),
            symbol => Assert.Equal("HELENGINE_INPUT_MOUSE", symbol),
            symbol => Assert.Equal("DESKTOP_PLATFORM", symbol),
            symbol => Assert.Equal(EditorPlatformPreprocessorSymbolService.RuntimeSupportsRenderManager2DTextureReleaseFlushSymbol, symbol),
            symbol => Assert.Equal("HELENGINE_CODEGEN_DISABLE_MENU_REFLECTION", symbol),
            symbol => Assert.Equal("HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION", symbol));
    }

    /// <summary>
    /// Verifies PS2 builds exclude desktop-only input symbols and include the PS2 runtime symbol.
    /// </summary>
    [Fact]
    public void Resolve_portable_input_preprocessor_symbols_returns_ps2_runtime_symbol_without_desktop_input_symbols() {
        PlatformDefinition definition = CreatePlatformDefinition(
            "ps2",
            new RuntimeGenerationContract(
                RuntimeMaterialResolutionMode.CookedPlatformOwned,
                false,
                PackagedPathPolicy.RootedOrContentRelative));

        IReadOnlyList<string> symbols = EditorGeneratedCoreRegenerationService.ResolvePortableInputPreprocessorSymbols(definition);

        Assert.Collection(
            symbols,
            symbol => Assert.Equal("PS2_PLATFORM", symbol),
            symbol => Assert.Equal(EditorPlatformPreprocessorSymbolService.RuntimeMaterialResolutionCookedPlatformOwnedSymbol, symbol),
            symbol => Assert.Equal(EditorPlatformPreprocessorSymbolService.RuntimeTextureResolutionCookedPlatformOwnedSymbol, symbol),
            symbol => Assert.Equal(EditorPlatformPreprocessorSymbolService.RuntimeModelResolutionCookedPlatformOwnedSymbol, symbol),
            symbol => Assert.Equal(EditorPlatformPreprocessorSymbolService.RuntimeAllowRootedPackagedPathsSymbol, symbol),
            symbol => Assert.Equal("HELENGINE_CODEGEN_DISABLE_MENU_REFLECTION", symbol),
            symbol => Assert.Equal("HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION", symbol));
    }

    /// <summary>
    /// Verifies portable input symbol resolution no longer injects PSP-specific runtime symbols.
    /// </summary>
    [Fact]
    public void ResolveSymbols_does_not_inject_psp_platform_symbol() {
        PlatformDefinition definition = CreatePlatformDefinition("psp", runtimeGenerationContract: null);

        IReadOnlyList<string> symbols = EditorGeneratedCoreRegenerationService.ResolvePortableInputPreprocessorSymbols(definition);

        Assert.DoesNotContain("PSP_PLATFORM", symbols);
    }

    /// <summary>
    /// Verifies the generated-core regeneration service forwards a selected codegen preset through the dedicated preset argument.
    /// </summary>
    [Fact]
    public void Build_arguments_includes_selected_preset_id() {
        PlatformDefinition platformDefinition = CreatePlatformDefinition("windows", runtimeGenerationContract: null);
        PlatformCodegenProfileDefinition codegenProfile = CreateDefaultCodegenProfile();
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase) {
            [PlatformCodegenSettingIds.PresetId] = "ps2-lite"
        };

        IReadOnlyList<string> arguments = EditorGeneratedCoreRegenerationService.BuildArguments(
            @"C:\tmp\fixture.csproj",
            @"C:\tmp\generated",
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
        IReadOnlyList<string> arguments = EditorGeneratedCoreRegenerationService.BuildArguments(
            @"C:\tmp\fixture.csproj",
            @"C:\tmp\generated",
            CreatePlatformDefinition("ps2", runtimeGenerationContract: null),
            CreateDefaultCodegenProfile(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            []);

        Assert.Contains("--set", arguments);
        Assert.Contains("include-project-defined-preprocessor-symbols=false", arguments);
        Assert.Contains("--feature-catalog", arguments);
        Assert.Contains(
            arguments,
            argument => argument.EndsWith("helengine-feature-catalog.json", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies regeneration argument building preserves authored symbols while appending scene-derived stripping symbols.
    /// </summary>
    [Fact]
    public void Build_arguments_combines_selected_and_scene_feature_preprocessor_symbols() {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase) {
            ["additional-preprocessor-symbols"] = "EXISTING_SYMBOL"
        };

        IReadOnlyList<string> arguments = EditorGeneratedCoreRegenerationService.BuildArguments(
            @"C:\tmp\fixture.csproj",
            @"C:\tmp\generated",
            CreatePlatformDefinition("windows", runtimeGenerationContract: null),
            CreateDefaultCodegenProfile(),
            values,
            [
                "HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES",
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
    /// Verifies physics generated-core support is requested only when scene-derived physics symbols are present.
    /// </summary>
    [Fact]
    public void Should_regenerate_physics3d_project_returns_true_only_for_scene_physics_symbols() {
        Assert.True(EditorGeneratedCoreRegenerationService.ShouldRegeneratePhysics3DProject([
            PhysicsSceneFeatureSymbolCatalog3D.SceneFeatureStrippingSymbol,
            PhysicsSceneFeatureSymbolCatalog3D.BoxBoxContactSymbol
        ]));

        Assert.False(EditorGeneratedCoreRegenerationService.ShouldRegeneratePhysics3DProject([
            "HELENGINE_INPUT_KEYBOARD",
            "DESKTOP_PLATFORM"
        ]));
    }

    /// <summary>
    /// Verifies regeneration failure does not leave one generated-core scratch workspace behind in the system temp folder.
    /// </summary>
    [Fact]
    public void Regenerate_when_codegen_tool_is_missing_deletes_scratch_workspace() {
        string platformId = "temp-cleanup-failure-" + Guid.NewGuid().ToString("N");
        string platformScratchRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-core", platformId);
        string generatedCoreRootPath = Path.Combine(RootPath, "regenerate-failure-output");
        EditorGeneratedCoreRegenerationService service = new();

        try {
            Assert.Throws<FileNotFoundException>(() => service.Regenerate(
                CreatePlatformDefinition(platformId, runtimeGenerationContract: null),
                CreateDefaultCodegenProfile(),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                generatedCoreRootPath,
                Path.Combine(RootPath, "missing-codegen.cmd"),
                [],
                [],
                CancellationToken.None));

            AssertScratchWorkspaceIsClean(platformScratchRootPath);
        } finally {
            DeleteDirectoryIfPresent(platformScratchRootPath);
        }
    }

    /// <summary>
    /// Verifies successful regeneration removes one generated-core scratch workspace after merging the final output tree.
    /// </summary>
    [Fact]
    public void Regenerate_when_codegen_succeeds_deletes_scratch_workspace() {
        string platformId = "temp-cleanup-success-" + Guid.NewGuid().ToString("N");
        string platformScratchRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-core", platformId);
        string generatedCoreRootPath = Path.Combine(RootPath, "regenerate-success-output");
        string codegenRootPath = Path.Combine(RootPath, "fake-codegen");
        string fakeCodegenPath = CreateFakeCodegenTool(codegenRootPath);
        EditorGeneratedCoreRegenerationService service = new();

        try {
            service.Regenerate(
                CreatePlatformDefinition(platformId, runtimeGenerationContract: null),
                CreateDefaultCodegenProfile(),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                generatedCoreRootPath,
                fakeCodegenPath,
                [],
                [],
                CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeComponentDeserializerRegistration.cpp")));
            Assert.True(File.Exists(Path.Combine(generatedCoreRootPath, "helengine_core_unity.cpp")));
            AssertScratchWorkspaceIsClean(platformScratchRootPath);
        } finally {
            DeleteDirectoryIfPresent(platformScratchRootPath);
        }
    }

    /// <summary>
    /// Verifies generated automatic runtime component emission writes the generated deserializer files without mutating the generated runtime registry source.
    /// </summary>
    [Fact]
    public void Emit_generated_automatic_runtime_component_deserializers_writes_native_sources_without_patching_runtime_registry() {
        string generatedCoreRootPath = Path.Combine(RootPath, "generated-runtime-component-deserializers");
        Directory.CreateDirectory(generatedCoreRootPath);
        string registrySourcePath = Path.Combine(generatedCoreRootPath, "RuntimeComponentRegistry.cpp");
        File.WriteAllText(
            registrySourcePath,
            "#include \"RuntimeComponentRegistry.hpp\"" + Environment.NewLine
            + "::RuntimeComponentRegistry* RuntimeComponentRegistry::CreateDefault()" + Environment.NewLine
            + "{" + Environment.NewLine
            + "::RuntimeComponentRegistry *registry = new ::RuntimeComponentRegistry();" + Environment.NewLine
            + "return registry;}" + Environment.NewLine);
        string originalRegistrySource = File.ReadAllText(registrySourcePath);

        EditorGeneratedCoreRegenerationService.EmitGeneratedAutomaticRuntimeComponentDeserializers(generatedCoreRootPath);

        Assert.True(File.Exists(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeClipRectComponentDeserializer.hpp")));
        Assert.True(File.Exists(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeClipRectComponentDeserializer.cpp")));
        Assert.True(File.Exists(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeScrollComponentDeserializer.hpp")));
        Assert.True(File.Exists(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeScrollComponentDeserializer.cpp")));
        Assert.Equal(originalRegistrySource, File.ReadAllText(registrySourcePath));

        string registrationSource = File.ReadAllText(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeComponentDeserializerRegistration.cpp"));
        Assert.Contains("RegisterGeneratedRuntimeComponentDeserializers(::RuntimeComponentRegistry* registry)", registrationSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies automatic runtime deserializer generation excludes component types that still have explicit hand-authored runtime deserializers.
    /// </summary>
    [Fact]
    public void Emit_generated_automatic_runtime_component_deserializers_excludes_components_with_explicit_runtime_deserializers() {
        string generatedCoreRootPath = Path.Combine(RootPath, "generated-runtime-component-deserializers-explicit-runtime-overlap");
        Directory.CreateDirectory(generatedCoreRootPath);

        EditorGeneratedCoreRegenerationService.EmitGeneratedAutomaticRuntimeComponentDeserializers(generatedCoreRootPath);

        string registrationSource = File.ReadAllText(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeComponentDeserializerRegistration.cpp"));
        Assert.DoesNotContain("GeneratedRuntimeMeshComponentDeserializer", registrationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedRuntimeCameraComponentDeserializer", registrationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedRuntimeSceneMapComponentDeserializer", registrationSource, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeMeshComponentDeserializer.cpp")));
        Assert.False(File.Exists(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeCameraComponentDeserializer.cpp")));
        Assert.False(File.Exists(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeSceneMapComponentDeserializer.cpp")));
    }

    /// <summary>
    /// Verifies cooked scenes that serialize assembly-qualified scripted component type ids cause matching native runtime deserializers to be generated.
    /// </summary>
    [Fact]
    public void Emit_cooked_scene_automatic_runtime_component_deserializers_includes_scene_referenced_project_component_types() {
        string generatedCoreRootPath = Path.Combine(RootPath, "generated-runtime-component-deserializers-from-scenes");
        Directory.CreateDirectory(generatedCoreRootPath);

        string scenePath = CreateCookedScene(
            "project-component-scene.hasset",
            AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(TestUpdateOnlyScriptComponent)));

        EditorGeneratedCoreRegenerationService.EmitCookedSceneAutomaticRuntimeComponentDeserializers(
            generatedCoreRootPath,
            [scenePath],
            null);

        Assert.True(File.Exists(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeTestUpdateOnlyScriptComponentDeserializer.hpp")));
        Assert.True(File.Exists(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeTestUpdateOnlyScriptComponentDeserializer.cpp")));
        string registrationSource = File.ReadAllText(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeComponentDeserializerRegistration.cpp"));
        Assert.Contains("GeneratedRuntimeTestUpdateOnlyScriptComponentDeserializer", registrationSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies cooked scenes that reference the real scene-memory probe component emit generated runtime deserializers that include nested step-array support.
    /// </summary>
    [Fact]
    public void Regenerate_WhenCoreContainsSceneMemoryProbeComponent_EmitsGeneratedRuntimeDeserializerSupport() {
        string generatedCoreRootPath = Path.Combine(RootPath, "generated-runtime-component-deserializers-scene-memory-probe");
        Directory.CreateDirectory(generatedCoreRootPath);

        string scenePath = CreateCookedScene(
            "scene-memory-probe-scene.hasset",
            AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(SceneMemoryProbeComponent)));

        EditorGeneratedCoreRegenerationService.EmitCookedSceneAutomaticRuntimeComponentDeserializers(
            generatedCoreRootPath,
            [scenePath],
            null);

        string headerPath = Path.Combine(generatedCoreRootPath, "GeneratedRuntimeSceneMemoryProbeComponentDeserializer.hpp");
        string sourcePath = Path.Combine(generatedCoreRootPath, "GeneratedRuntimeSceneMemoryProbeComponentDeserializer.cpp");
        Assert.True(File.Exists(headerPath));
        Assert.True(File.Exists(sourcePath));

        string headerSource = File.ReadAllText(headerPath);
        string source = File.ReadAllText(sourcePath);
        Assert.Contains("SceneMemoryProbeComponent", headerSource, StringComparison.Ordinal);
        Assert.Contains("SceneMemoryProbeStep", source, StringComparison.Ordinal);
        Assert.Contains("ReadDouble()", source, StringComparison.Ordinal);
        Assert.Contains("SceneMemoryProbeActionKind", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies project scripted component ids participate in generated runtime deserializer emission instead of being filtered out as engine-owned compatibility types.
    /// </summary>
    [Fact]
    public void Emit_cooked_scene_automatic_runtime_component_deserializers_includes_city_scripted_component_type_ids() {
        string generatedCoreRootPath = Path.Combine(RootPath, "generated-runtime-component-deserializers-project-components");
        Directory.CreateDirectory(generatedCoreRootPath);
        string scenePath = CreateCookedScene(
            "project-components-scene.hasset",
            "project.menu.SceneReturnComponent, gameplay",
            "project.menu.PlatformInfoComponent, gameplay");

        DictionaryScriptTypeResolver scriptTypeResolver = new DictionaryScriptTypeResolver();
        scriptTypeResolver.Register("project.menu.SceneReturnComponent, gameplay", typeof(TestUpdateOnlyScriptComponent));
        scriptTypeResolver.Register("project.menu.PlatformInfoComponent, gameplay", typeof(TestUpdateOnlyScriptComponent));

        EditorGeneratedCoreRegenerationService.EmitCookedSceneAutomaticRuntimeComponentDeserializers(
            generatedCoreRootPath,
            [scenePath],
            scriptTypeResolver);

        string registrationSource = File.ReadAllText(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeComponentDeserializerRegistration.cpp"));
        Assert.Contains("GeneratedRuntimeTestUpdateOnlyScriptComponentDeserializer", registrationSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies cooked scenes that serialize scripted component ids infer the owning runtime module assembly names.
    /// </summary>
    [Fact]
    public void DiscoverReferencedRuntimeModuleIdsFromCookedScenes_with_scripted_component_returns_owning_assembly_name() {
        DictionaryScriptTypeResolver scriptTypeResolver = new DictionaryScriptTypeResolver();
        string componentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(TestAxisRotationScriptComponent));
        scriptTypeResolver.Register(componentTypeId, typeof(TestAxisRotationScriptComponent));
        string scenePath = CreateCookedScene("module-discovery-scene.hasset", componentTypeId);

        IReadOnlyList<string> moduleIds = EditorGeneratedCoreRegenerationService.DiscoverReferencedRuntimeModuleIdsFromCookedScenes(
            [scenePath],
            scriptTypeResolver);

        Assert.Equal([typeof(TestAxisRotationScriptComponent).Assembly.GetName().Name ?? string.Empty], moduleIds);
    }

    /// <summary>
    /// Verifies cooked scenes without scripted components infer no runtime modules.
    /// </summary>
    [Fact]
    public void DiscoverReferencedRuntimeModuleIdsFromCookedScenes_without_scripted_components_returns_empty_list() {
        string scenePath = CreateCookedScene("module-discovery-empty-scene.hasset", "helengine.TransformComponent");

        IReadOnlyList<string> moduleIds = EditorGeneratedCoreRegenerationService.DiscoverReferencedRuntimeModuleIdsFromCookedScenes(
            [scenePath],
            null);

        Assert.Empty(moduleIds);
    }

    /// <summary>
    /// Verifies generated native source preparation only emits missing generated runtime component support files and leaves existing generated C++ files untouched.
    /// </summary>
    [Fact]
    public void Ensure_generated_runtime_component_deserializer_support_does_not_mutate_existing_generated_cpp_files() {
        string generatedCoreRootPath = Path.Combine(RootPath, "normalize-generated-native-sources-no-native-mutation");
        Directory.CreateDirectory(generatedCoreRootPath);
        string sceneManagerPath = Path.Combine(generatedCoreRootPath, "SceneManager.cpp");
        string runtimeSceneResolverPath = Path.Combine(generatedCoreRootPath, "RuntimeSceneAssetReferenceResolver.cpp");
        File.WriteAllText(sceneManagerPath, "#include \"SceneManager.hpp\"\nvoid SceneManager::FlushPendingOperations()\n{\n}\n");
        File.WriteAllText(runtimeSceneResolverPath, "#include \"RuntimeSceneAssetReferenceResolver.hpp\"\nvoid RuntimeSceneAssetReferenceResolver::Touch()\n{\n}\n");

        string originalSceneManager = File.ReadAllText(sceneManagerPath);
        string originalRuntimeSceneResolver = File.ReadAllText(runtimeSceneResolverPath);

        EditorGeneratedCoreRegenerationService.EnsureGeneratedRuntimeComponentDeserializerSupport(generatedCoreRootPath, "ds");

        Assert.Equal(originalSceneManager, File.ReadAllText(sceneManagerPath));
        Assert.Equal(originalRuntimeSceneResolver, File.ReadAllText(runtimeSceneResolverPath));
        Assert.True(File.Exists(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeComponentDeserializerRegistration.cpp")));
        Assert.True(File.Exists(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeComponentDeserializerRegistration.hpp")));
    }

    /// <summary>
     /// Verifies the end-state inventory where engine-side native ownership rewrites and registry patching have been removed completely.
     /// </summary>
    [Fact]
    public void Generated_core_regeneration_service_contains_no_native_cpp_rewrite_inventory() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.editor",
            "managers",
            "project",
            "EditorGeneratedCoreRegenerationService.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("static void PatchRuntimeComponentRegistryForGeneratedDeserializers(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("static string NormalizeGeneratedNativeSource(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("static void NormalizeGeneratedNativeSources(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("static void RewriteAmalgamatedTranslationUnit(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("static void RemoveEditorOnlyGeneratedSourceFiles(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("static void RemoveRuntimeScriptReflectionGeneratedSourceFiles(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteGeneratedAssetArray(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteGeneratedSceneArray(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("delete loadResult;", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies generated unity translation regeneration excludes only the separately linked runtime manifest sources.
    /// </summary>
    [Fact]
    public void Write_generated_core_translation_unit_excludes_only_runtime_manifest_sources() {
        string generatedCoreRootPath = Path.Combine(RootPath, "rewrite-unity-exclusions");
        Directory.CreateDirectory(Path.Combine(generatedCoreRootPath, "runtime"));
        File.WriteAllText(Path.Combine(generatedCoreRootPath, "Foo.cpp"), "// foo");
        File.WriteAllText(Path.Combine(generatedCoreRootPath, "RendererBackendCapabilityProfile.cpp"), "// keep");
        File.WriteAllText(Path.Combine(generatedCoreRootPath, "ExternalPlatformMaterialAsset.cpp"), "// keep");
        File.WriteAllText(Path.Combine(generatedCoreRootPath, "runtime", "runtime_startup_manifest.cpp"), "// exclude");
        File.WriteAllText(Path.Combine(generatedCoreRootPath, "runtime", "runtime_scene_catalog_manifest.cpp"), "// exclude");
        File.WriteAllText(Path.Combine(generatedCoreRootPath, "runtime", "runtime_code_module_manifest.cpp"), "// exclude");
        File.WriteAllText(Path.Combine(generatedCoreRootPath, "helengine_core_unity.cpp"), "// old unity\n");

        EditorGeneratedCoreRegenerationService.WriteGeneratedCoreTranslationUnit(generatedCoreRootPath);

        string unitySource = File.ReadAllText(Path.Combine(generatedCoreRootPath, "helengine_core_unity.cpp"));
        Assert.Contains("#include \"Foo.cpp\"", unitySource);
        Assert.Contains("#include \"RendererBackendCapabilityProfile.cpp\"", unitySource);
        Assert.Contains("#include \"ExternalPlatformMaterialAsset.cpp\"", unitySource);
        Assert.DoesNotContain("runtime/runtime_startup_manifest.cpp", unitySource, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime/runtime_scene_catalog_manifest.cpp", unitySource, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime/runtime_code_module_manifest.cpp", unitySource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Creates one minimal platform definition for symbol and argument tests.
    /// </summary>
    /// <param name="platformId">Platform id to assign.</param>
    /// <param name="runtimeGenerationContract">Optional runtime generation contract.</param>
    /// <returns>One minimal platform definition.</returns>
    static PlatformDefinition CreatePlatformDefinition(string platformId, RuntimeGenerationContract runtimeGenerationContract) {
        return new PlatformDefinition(
            platformId,
            platformId.ToUpperInvariant(),
            Array.Empty<PlatformBuildProfileDefinition>(),
            Array.Empty<PlatformGraphicsProfileDefinition>(),
            Array.Empty<PlatformAssetRequirementDefinition>(),
            Array.Empty<PlatformMaterialSchemaDefinition>(),
            Array.Empty<PlatformComponentSupportRule>(),
            Array.Empty<PlatformCodegenProfileDefinition>(),
            Array.Empty<PlatformStorageProfileDefinition>(),
            Array.Empty<PlatformMediaProfileDefinition>(),
            runtimeGenerationContract);
    }

    /// <summary>
    /// Creates the default codegen profile used by argument tests.
    /// </summary>
    /// <returns>One default C++ codegen profile.</returns>
    static PlatformCodegenProfileDefinition CreateDefaultCodegenProfile() {
        return new PlatformCodegenProfileDefinition(
            "default",
            "Default",
            "Default codegen profile",
            PlatformCodegenLanguage.Cpp,
            PlatformSerializationEndianness.LittleEndian,
            []);
    }

    /// <summary>
    /// Creates one fake codegen tool that emits the minimal generated output required by regeneration tests.
    /// </summary>
    /// <param name="codegenRootPath">Directory that should contain the fake codegen tool and bundled runtime support root.</param>
    /// <returns>Absolute path to the fake codegen command file.</returns>
    static string CreateFakeCodegenTool(string codegenRootPath) {
        Directory.CreateDirectory(codegenRootPath);
        Directory.CreateDirectory(Path.Combine(codegenRootPath, ".net.cpp"));

        string fakeCodegenPath = Path.Combine(codegenRootPath, "fake-codegen.cmd");
        File.WriteAllText(
            fakeCodegenPath,
            "@echo off\r\n"
            + "setlocal EnableDelayedExpansion\r\n"
            + "set OUTPUT=\r\n"
            + ":parse\r\n"
            + "if \"%~1\"==\"\" goto done\r\n"
            + "if /I \"%~1\"==\"--output\" (\r\n"
            + "  set OUTPUT=%~2\r\n"
            + "  shift\r\n"
            + ")\r\n"
            + "shift\r\n"
            + "goto parse\r\n"
            + ":done\r\n"
            + "if \"%OUTPUT%\"==\"\" exit /b 2\r\n"
            + "if not exist \"%OUTPUT%\" mkdir \"%OUTPUT%\"\r\n"
            + "> \"%OUTPUT%\\GeneratedMarker.cpp\" echo // generated\r\n"
            + "> \"%OUTPUT%\\cpp-conversion-report.json\" (\r\n"
            + "  echo {\r\n"
            + "  echo   \"assemblyName\": \"fake\",\r\n"
            + "  echo   \"buildFeatures\": {\r\n"
            + "  echo     \"decisions\": [],\r\n"
            + "  echo     \"detectedRoots\": [],\r\n"
            + "  echo     \"conflicts\": []\r\n"
            + "  echo   }\r\n"
            + "  echo }\r\n"
            + ")\r\n"
            + "exit /b 0\r\n");
        return fakeCodegenPath;
    }

    /// <summary>
    /// Verifies one platform scratch workspace root is absent or empty after regeneration completes.
    /// </summary>
    /// <param name="platformScratchRootPath">Platform-specific scratch workspace parent path.</param>
    static void AssertScratchWorkspaceIsClean(string platformScratchRootPath) {
        if (!Directory.Exists(platformScratchRootPath)) {
            return;
        }

        Assert.Empty(Directory.GetDirectories(platformScratchRootPath));
        Assert.Empty(Directory.GetFiles(platformScratchRootPath));
    }

    /// <summary>
    /// Deletes one directory tree when it exists.
    /// </summary>
    /// <param name="path">Directory path to delete.</param>
    static void DeleteDirectoryIfPresent(string path) {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) {
            Directory.Delete(path, true);
        }
    }

    /// <summary>
    /// Writes one cooked scene asset that contains exactly one serialized component type id.
    /// </summary>
    /// <param name="fileName">Scene file name under the temporary workspace.</param>
    /// <param name="componentTypeId">Serialized component type id to write.</param>
    /// <returns>Absolute scene file path.</returns>
    string CreateCookedScene(string fileName, string componentTypeId) {
        string scenePath = Path.Combine(RootPath, fileName);
        using FileStream stream = File.Create(scenePath);
        AssetSerializer.Serialize(
            stream,
            new SceneAsset {
                RootEntities = [
                    new SceneEntityAsset {
                        Components = [
                            new SceneComponentAssetRecord {
                                ComponentTypeId = componentTypeId,
                                Payload = Array.Empty<byte>()
                            }
                        ]
                    }
                ]
            });
        return scenePath;
    }

    /// <summary>
    /// Writes one cooked scene asset that contains one serialized record for each supplied component type id.
    /// </summary>
    /// <param name="fileName">Scene file name under the temporary workspace.</param>
    /// <param name="componentTypeIds">Serialized component type ids to write.</param>
    /// <returns>Absolute scene file path.</returns>
    string CreateCookedScene(string fileName, params string[] componentTypeIds) {
        if (componentTypeIds == null) {
            throw new ArgumentNullException(nameof(componentTypeIds));
        }

        string scenePath = Path.Combine(RootPath, fileName);
        SceneComponentAssetRecord[] componentRecords = new SceneComponentAssetRecord[componentTypeIds.Length];
        for (int componentIndex = 0; componentIndex < componentTypeIds.Length; componentIndex++) {
            componentRecords[componentIndex] = new SceneComponentAssetRecord {
                ComponentTypeId = componentTypeIds[componentIndex],
                Payload = Array.Empty<byte>()
            };
        }

        using FileStream stream = File.Create(scenePath);
        AssetSerializer.Serialize(
            stream,
            new SceneAsset {
                RootEntities = [
                    new SceneEntityAsset {
                        Components = componentRecords
                    }
                ]
            });
        return scenePath;
    }

    /// <summary>
    /// Resolves the helengine repository root from the current test assembly location.
    /// </summary>
    /// <returns>Absolute repository root path.</returns>
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
