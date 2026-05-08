using System.Collections.Generic;
using helengine.baseplatform.Manifest;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the generated runtime native manifest writer emits C++ source for startup scenes, scene physics, and code modules.
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
    /// Ensures the writer emits native startup, scene-physics, and code-module source files with the resolved cooked scene path.
    /// </summary>
    [Fact]
    public void Write_emits_startup_scene_scene_physics_and_code_module_cpp_sources() {
        string generatedCoreRootPath = Path.Combine(RootPath, "generated-core");
        Directory.CreateDirectory(generatedCoreRootPath);

        PlatformBuildScene startupScene = new(
            "Scenes/NewScene.helen",
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
            "Scenes/NewScene.helen",
            [startupScene],
            Array.Empty<PlatformBuildAsset>(),
            Array.Empty<PlatformBuildArtifact>(),
            codeModules,
            Array.Empty<PlatformArtifactPlacement>(),
            new PlatformContainerWritePlan(string.Empty, Array.Empty<PlatformContainerArtifact>()));

        EditorRuntimeNativeManifestWriter writer = new();
        writer.Write(generatedCoreRootPath, manifest);

        string runtimeRootPath = Path.Combine(generatedCoreRootPath, "runtime");
        string startupHeaderPath = Path.Combine(runtimeRootPath, "runtime_startup_manifest.hpp");
        string startupSourcePath = Path.Combine(runtimeRootPath, "runtime_startup_manifest.cpp");
        string codeModuleHeaderPath = Path.Combine(runtimeRootPath, "runtime_code_module_manifest.hpp");
        string codeModuleSourcePath = Path.Combine(runtimeRootPath, "runtime_code_module_manifest.cpp");
        string physicsHeaderPath = Path.Combine(runtimeRootPath, "runtime_physics3d_scene_feature_manifest.hpp");
        string physicsSourcePath = Path.Combine(runtimeRootPath, "runtime_physics3d_scene_feature_manifest.cpp");

        Assert.True(File.Exists(startupHeaderPath));
        Assert.True(File.Exists(startupSourcePath));
        Assert.True(File.Exists(codeModuleHeaderPath));
        Assert.True(File.Exists(codeModuleSourcePath));
        Assert.True(File.Exists(physicsHeaderPath));
        Assert.True(File.Exists(physicsSourcePath));
        Assert.False(File.Exists(Path.Combine(runtimeRootPath, "runtime-startup.json")));
        Assert.False(File.Exists(Path.Combine(runtimeRootPath, "runtime-code-modules.json")));

        string startupSource = File.ReadAllText(startupSourcePath);
        string codeModuleSource = File.ReadAllText(codeModuleSourcePath);
        string physicsSource = File.ReadAllText(physicsSourcePath);

        Assert.Contains("he_get_runtime_startup_scene_relative_path", startupSource);
        Assert.Contains("cooked/scenes/NewScene.hasset", startupSource);
        Assert.Contains("he_runtime_physics3d_scene_feature_flags", physicsSource);
        Assert.Contains("\"Scenes/NewScene.helen\"", physicsSource);
        Assert.Contains("33u", physicsSource);
        Assert.Contains("kRuntimeCodeModuleDependencies_1", codeModuleSource);
        Assert.Contains("HERuntimeCodeModuleLoadState::ResidentAtStartup", codeModuleSource);
        Assert.Contains("HERuntimeCodeModuleLoadState::SceneResident", codeModuleSource);
        Assert.Contains("he_runtime_code_module_can_unload", codeModuleSource);
    }
}
