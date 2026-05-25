using System.Collections.Generic;
using helengine.baseplatform.Manifest;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the generated runtime native manifest writer emits C++ source for startup scenes, runtime scene catalogs, scene physics, and code modules.
/// </summary>
public sealed class EditorRuntimeNativeManifestWriterTests : IDisposable {
    /// <summary>
    /// Temporary workspace used by the manifest writer test.
    /// </summary>
    readonly string RootPath;

    /// <summary>
    /// Initializes the test workspace.
    /// </summary>
    public EditorRuntimeNativeManifestWriterTests() {
        RootPath = Path.Combine(Path.GetTempPath(), "helengine-runtime-native-manifest-writer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    /// <summary>
    /// Releases the temporary workspace used by the test.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(RootPath)) {
            Directory.Delete(RootPath, true);
        }
    }

    /// <summary>
        /// Ensures the writer emits native startup, scene-catalog, scene-physics, and code-module source files with the resolved cooked scene path.
        /// </summary>
        [Fact]
        public void Write_emits_startup_scene_scene_catalog_scene_physics_and_code_module_cpp_sources() {
        string generatedCoreRootPath = Path.Combine(RootPath, "generated-core");
        Directory.CreateDirectory(generatedCoreRootPath);

        PlatformBuildScene startupScene = new(
            "NewScene",
            "NewScene",
            "Scenes/NewScene.helen",
            Array.Empty<PlatformBuildPayloadReference>(),
            [
                new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.CookedRelativePath, "cooked/scenes/NewScene.hasset"),
                new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.Physics3DSceneFeatureFlags, "33")
            ]);

        PlatformBuildCodeModule[] codeModules = [
            new PlatformBuildCodeModule("core", "core-artifact", "default", ["always-loaded"], []),
            new PlatformBuildCodeModule("ui", "ui-artifact", "default", ["scene-loaded"], ["core"])
        ];

        PlatformBuildManifest manifest = new(
            1,
            "project",
            "1.0.0",
            "1.0.0",
            "windows",
            "2026.05.12",
            "NewScene",
            [startupScene],
            Array.Empty<PlatformBuildAsset>(),
            Array.Empty<PlatformBuildArtifact>(),
            codeModules,
            Array.Empty<PlatformArtifactPlacement>(),
            new PlatformContainerWritePlan(string.Empty, Array.Empty<PlatformContainerArtifact>()));
        manifest.StandardPlatformInputConfiguration = new StandardPlatformInputConfiguration([
            new StandardPlatformActionBinding(
                StandardPlatformAction.Accept,
                new InputControlId(InputDeviceKind.Gamepad, InputControlKind.Button, 0, (int)InputGamepadButton.South)),
            new StandardPlatformActionBinding(
                StandardPlatformAction.Return,
                new InputControlId(InputDeviceKind.Gamepad, InputControlKind.Button, 0, (int)InputGamepadButton.North))
        ]);

        EditorRuntimeNativeManifestWriter writer = new();
        writer.Write(generatedCoreRootPath, manifest);

        string runtimeRootPath = Path.Combine(generatedCoreRootPath, "runtime");
        string startupHeaderPath = Path.Combine(runtimeRootPath, "runtime_startup_manifest.hpp");
        string startupSourcePath = Path.Combine(runtimeRootPath, "runtime_startup_manifest.cpp");
        string sceneCatalogHeaderPath = Path.Combine(runtimeRootPath, "runtime_scene_catalog_manifest.hpp");
        string sceneCatalogSourcePath = Path.Combine(runtimeRootPath, "runtime_scene_catalog_manifest.cpp");
        string codeModuleHeaderPath = Path.Combine(runtimeRootPath, "runtime_code_module_manifest.hpp");
        string codeModuleSourcePath = Path.Combine(runtimeRootPath, "runtime_code_module_manifest.cpp");
        string physicsHeaderPath = Path.Combine(runtimeRootPath, "runtime_physics3d_scene_feature_manifest.hpp");
        string physicsSourcePath = Path.Combine(runtimeRootPath, "runtime_physics3d_scene_feature_manifest.cpp");
        string standardPlatformInputHeaderPath = Path.Combine(runtimeRootPath, "runtime_standard_platform_input_manifest.hpp");
        string standardPlatformInputSourcePath = Path.Combine(runtimeRootPath, "runtime_standard_platform_input_manifest.cpp");

        Assert.True(File.Exists(startupHeaderPath));
        Assert.True(File.Exists(startupSourcePath));
        Assert.True(File.Exists(sceneCatalogHeaderPath));
        Assert.True(File.Exists(sceneCatalogSourcePath));
        Assert.True(File.Exists(codeModuleHeaderPath));
        Assert.True(File.Exists(codeModuleSourcePath));
        Assert.True(File.Exists(physicsHeaderPath));
        Assert.True(File.Exists(physicsSourcePath));
        Assert.True(File.Exists(standardPlatformInputHeaderPath));
        Assert.True(File.Exists(standardPlatformInputSourcePath));
        Assert.False(File.Exists(Path.Combine(runtimeRootPath, "runtime-startup.json")));
        Assert.False(File.Exists(Path.Combine(runtimeRootPath, "runtime-scene-catalog.json")));

        string startupSource = File.ReadAllText(startupSourcePath);
        string sceneCatalogSource = File.ReadAllText(sceneCatalogSourcePath);
        string codeModuleSource = File.ReadAllText(codeModuleSourcePath);
        string physicsSource = File.ReadAllText(physicsSourcePath);
        string standardPlatformInputSource = File.ReadAllText(standardPlatformInputSourcePath);

        Assert.Contains("he_get_runtime_startup_scene_relative_path", startupSource);
        Assert.Contains("cooked/scenes/NewScene.hasset", startupSource);
        Assert.Contains("he_get_runtime_platform_name", startupSource);
        Assert.Contains("he_get_runtime_platform_version", startupSource);
        Assert.Contains("\"windows\"", startupSource);
        Assert.Contains("\"2026.05.12\"", startupSource);
        Assert.Contains("he_runtime_scene_catalog_entries", sceneCatalogSource);
        Assert.Contains("he_runtime_scene_cooked_relative_path", sceneCatalogSource);
        Assert.Contains("\"NewScene\"", sceneCatalogSource);
        Assert.DoesNotContain("\"Scenes/NewScene.helen\"", sceneCatalogSource, StringComparison.Ordinal);
        Assert.Contains("\"cooked/scenes/NewScene.hasset\"", sceneCatalogSource);
        Assert.Contains("he_runtime_physics3d_scene_feature_flags", physicsSource);
        Assert.Contains("\"NewScene\"", physicsSource);
        Assert.DoesNotContain("\"Scenes/NewScene.helen\"", physicsSource, StringComparison.Ordinal);
        Assert.Contains("33u", physicsSource);
        Assert.Contains("kRuntimeCodeModuleDependencies_1", codeModuleSource);
        Assert.Contains("HERuntimeCodeModuleLoadState::ResidentAtStartup", codeModuleSource);
        Assert.Contains("HERuntimeCodeModuleLoadState::SceneResident", codeModuleSource);
        Assert.Contains("he_runtime_code_module_can_unload", codeModuleSource);
        Assert.Contains("he_runtime_standard_platform_action_entries", standardPlatformInputSource);
        Assert.Contains("kRuntimeStandardPlatformActionEntryCount", standardPlatformInputSource);
        Assert.Contains("0, 0, 0, 0, 0", standardPlatformInputSource);
        Assert.Contains("1, 0, 0, 0, 3", standardPlatformInputSource);
    }

    /// <summary>
    /// Ensures writing runtime native manifest sources refreshes the generated-core unity translation unit so native builds compile the emitted standard-platform-input manifest.
    /// </summary>
    [Fact]
    public void Write_refreshes_generated_core_unity_translation_unit_for_standard_platform_input_manifest() {
        string generatedCoreRootPath = Path.Combine(RootPath, "generated-core");
        Directory.CreateDirectory(generatedCoreRootPath);
        File.WriteAllText(Path.Combine(generatedCoreRootPath, "ExistingComponent.cpp"), "void existing_component() {}");
        EditorGeneratedCoreRegenerationService.WriteGeneratedCoreTranslationUnit(generatedCoreRootPath);

        PlatformBuildScene startupScene = new(
            "NewScene",
            "NewScene",
            "Scenes/NewScene.helen",
            Array.Empty<PlatformBuildPayloadReference>(),
            [
                new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.CookedRelativePath, "cooked/scenes/NewScene.hasset"),
                new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.Physics3DSceneFeatureFlags, "0")
            ]);

        PlatformBuildManifest manifest = new(
            1,
            "project",
            "1.0.0",
            "1.0.0",
            "ds",
            "2026.05.25",
            "NewScene",
            [startupScene],
            Array.Empty<PlatformBuildAsset>(),
            Array.Empty<PlatformBuildArtifact>(),
            Array.Empty<PlatformBuildCodeModule>(),
            Array.Empty<PlatformArtifactPlacement>(),
            new PlatformContainerWritePlan(string.Empty, Array.Empty<PlatformContainerArtifact>()));
        manifest.StandardPlatformInputConfiguration = new StandardPlatformInputConfiguration([
            new StandardPlatformActionBinding(
                StandardPlatformAction.Accept,
                new InputControlId(InputDeviceKind.Gamepad, InputControlKind.Button, 0, (int)InputGamepadButton.South))
        ]);

        EditorRuntimeNativeManifestWriter writer = new();
        writer.Write(generatedCoreRootPath, manifest);

        string unitySourcePath = Path.Combine(generatedCoreRootPath, "helengine_core_unity.cpp");
        string unitySourceContents = File.ReadAllText(unitySourcePath);
        Assert.Contains("#include \"runtime/runtime_standard_platform_input_manifest.cpp\"", unitySourceContents);
    }
}
