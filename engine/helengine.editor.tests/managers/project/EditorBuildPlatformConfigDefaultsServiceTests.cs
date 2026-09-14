using helengine.baseplatform.Definitions;
using helengine.baseplatform.Profiles;
using helengine.editor;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Covers the build-dialog defaults resolution that normalizes one persisted platform configuration against builder-provided profile metadata.
/// </summary>
public class EditorBuildPlatformConfigDefaultsServiceTests {
    /// <summary>
    /// Builds one selection model exposing canonical debug/release build profiles bound to distinct graphics and codegen profiles.
    /// </summary>
    /// <returns>Selection model usable by the defaults service.</returns>
    static EditorPlatformBuildSelectionModel CreateSelectionModel() {
        PlatformSettingDefinition buildSetting = new(
            "build.symbols",
            "Symbols",
            PlatformSettingKind.Text,
            "default-symbols",
            false,
            []);
        PlatformSettingDefinition graphicsSetting = new(
            "graphics.msaa",
            "MSAA",
            PlatformSettingKind.Text,
            "4",
            false,
            []);
        PlatformSettingDefinition codegenSetting = new(
            "codegen.optimize",
            "Optimize",
            PlatformSettingKind.Text,
            "shared-default",
            false,
            []);

        PlatformDefinition definition = new(
            "windows",
            "Windows",
            [
                new PlatformBuildProfileDefinition(
                    "debug",
                    "Debug",
                    "Debug build",
                    "graphics-debug",
                    "codegen-debug",
                    [buildSetting],
                    new Dictionary<string, string> { { "codegen.optimize", "debug-optimize" } }),
                new PlatformBuildProfileDefinition(
                    "release",
                    "Release",
                    "Release build",
                    "graphics-release",
                    "codegen-release",
                    [buildSetting],
                    new Dictionary<string, string> { { "codegen.optimize", "release-optimize" } })
            ],
            [
                new PlatformGraphicsProfileDefinition("graphics-release", "Graphics Release", "Release graphics", [graphicsSetting]),
                new PlatformGraphicsProfileDefinition("graphics-debug", "Graphics Debug", "Debug graphics", [graphicsSetting])
            ],
            [],
            [],
            [],
            [
                new PlatformCodegenProfileDefinition(
                    "codegen-release",
                    "Codegen Release",
                    "Release codegen",
                    PlatformCodegenLanguage.Cpp,
                    PlatformSerializationEndianness.LittleEndian,
                    [codegenSetting]),
                new PlatformCodegenProfileDefinition(
                    "codegen-debug",
                    "Codegen Debug",
                    "Debug codegen",
                    PlatformCodegenLanguage.Cpp,
                    PlatformSerializationEndianness.LittleEndian,
                    [codegenSetting])
            ],
            [
                new PlatformStorageProfileDefinition(
                    "loose-files",
                    "Loose Files",
                    PlatformStorageProfileKind.LooseFiles,
                    "windows-loose-files",
                    false)
            ],
            [
                new PlatformMediaProfileDefinition(
                    "windows-install-tree",
                    "Windows Install Tree",
                    PlatformMediaLayoutKind.InstallTree,
                    true,
                    false)
            ]);

        return EditorPlatformBuildSelectionModel.From(definition);
    }

    [Fact]
    public void EnsurePlatformSelectionDefaults_when_release_platform_has_no_selections_seeds_every_bound_profile() {
        EditorBuildPlatformConfigDocument platformConfig = new() {
            PlatformId = "windows"
        };

        EditorBuildPlatformConfigDefaultsService.EnsurePlatformSelectionDefaults(platformConfig, CreateSelectionModel());

        Assert.Equal("release", platformConfig.SelectedEnvironmentId);
        Assert.Equal("release", platformConfig.SelectedBuildProfileId);
        Assert.Equal("graphics-release", platformConfig.SelectedGraphicsProfileId);
        Assert.Equal("codegen-release", platformConfig.SelectedCodegenProfileId);
        Assert.Equal("loose-files", platformConfig.SelectedStorageProfileId);
        Assert.Equal("windows-install-tree", platformConfig.SelectedMediaProfileId);
        Assert.Equal("default-symbols", platformConfig.SelectedBuildOptionValues["build.symbols"]);
        Assert.Equal("4", platformConfig.SelectedGraphicsOptionValues["graphics.msaa"]);
        Assert.Equal("release-optimize", platformConfig.SelectedCodegenOptionValues["codegen.optimize"]);
    }

    [Fact]
    public void EnsurePlatformSelectionDefaults_when_debug_build_is_requested_seeds_debug_environment_and_bound_profiles() {
        EditorBuildPlatformConfigDocument platformConfig = new() {
            PlatformId = "windows",
            DebugBuild = true
        };

        EditorBuildPlatformConfigDefaultsService.EnsurePlatformSelectionDefaults(platformConfig, CreateSelectionModel());

        Assert.Equal("debug", platformConfig.SelectedEnvironmentId);
        Assert.Equal("debug", platformConfig.SelectedBuildProfileId);
        Assert.Equal("graphics-debug", platformConfig.SelectedGraphicsProfileId);
        Assert.Equal("codegen-debug", platformConfig.SelectedCodegenProfileId);
        Assert.Equal("debug-optimize", platformConfig.SelectedCodegenOptionValues["codegen.optimize"]);
    }

    [Fact]
    public void EnsurePlatformSelectionDefaults_when_switching_to_debug_repoints_inherited_but_not_overridden_selections() {
        EditorBuildPlatformConfigDocument platformConfig = new() {
            PlatformId = "windows",
            SelectedBuildProfileId = "release",
            SelectedGraphicsProfileId = "graphics-release",
            SelectedCodegenProfileId = "codegen-release",
            DebugBuild = true
        };

        EditorBuildPlatformConfigDefaultsService.EnsurePlatformSelectionDefaults(platformConfig, CreateSelectionModel());

        Assert.Equal("debug", platformConfig.SelectedBuildProfileId);
        Assert.Equal("graphics-debug", platformConfig.SelectedGraphicsProfileId);
        Assert.Equal("codegen-debug", platformConfig.SelectedCodegenProfileId);
    }

    [Fact]
    public void EnsurePlatformSelectionDefaults_keeps_explicit_graphics_override_when_build_profile_changes() {
        EditorBuildPlatformConfigDocument platformConfig = new() {
            PlatformId = "windows",
            SelectedBuildProfileId = "release",
            SelectedGraphicsProfileId = "graphics-debug",
            DebugBuild = true
        };

        EditorBuildPlatformConfigDefaultsService.EnsurePlatformSelectionDefaults(platformConfig, CreateSelectionModel());

        Assert.Equal("debug", platformConfig.SelectedBuildProfileId);
        Assert.Equal("graphics-debug", platformConfig.SelectedGraphicsProfileId);
    }

    [Fact]
    public void EnsurePlatformSelectionDefaults_without_selection_model_still_seeds_the_environment_only() {
        EditorBuildPlatformConfigDocument platformConfig = new() {
            PlatformId = "windows",
            DebugBuild = true
        };

        EditorBuildPlatformConfigDefaultsService.EnsurePlatformSelectionDefaults(platformConfig, null);

        Assert.Equal("debug", platformConfig.SelectedEnvironmentId);
        Assert.Equal(string.Empty, platformConfig.SelectedBuildProfileId);
        Assert.Equal(string.Empty, platformConfig.SelectedGraphicsProfileId);
    }

    [Fact]
    public void EnsurePlatformSelectionDefaults_preserves_an_already_selected_environment() {
        EditorBuildPlatformConfigDocument platformConfig = new() {
            PlatformId = "windows",
            SelectedEnvironmentId = "staging",
            DebugBuild = true
        };

        EditorBuildPlatformConfigDefaultsService.EnsurePlatformSelectionDefaults(platformConfig, CreateSelectionModel());

        Assert.Equal("staging", platformConfig.SelectedEnvironmentId);
    }

    [Fact]
    public void EnsurePlatformSelectionDefaults_replaces_missing_option_maps_with_empty_dictionaries() {
        EditorBuildPlatformConfigDocument platformConfig = new() {
            PlatformId = "windows",
            SelectedBuildOptionValues = null,
            SelectedGraphicsOptionValues = null,
            SelectedCodegenOptionValues = null
        };

        EditorBuildPlatformConfigDefaultsService.EnsurePlatformSelectionDefaults(platformConfig, CreateSelectionModel());

        Assert.NotNull(platformConfig.SelectedBuildOptionValues);
        Assert.NotNull(platformConfig.SelectedGraphicsOptionValues);
        Assert.NotNull(platformConfig.SelectedCodegenOptionValues);
    }

    [Fact]
    public void ResolveGraphicsProfile_falls_back_to_the_build_profile_binding_when_none_is_selected() {
        EditorBuildPlatformConfigDocument platformConfig = new() {
            PlatformId = "windows"
        };
        EditorPlatformBuildSelectionModel selectionModel = CreateSelectionModel();
        PlatformBuildProfileDefinition buildProfile = selectionModel.TryResolveBuildProfileExact("debug");

        PlatformGraphicsProfileDefinition graphicsProfile = EditorBuildPlatformConfigDefaultsService.ResolveGraphicsProfile(platformConfig, buildProfile, selectionModel);

        Assert.Equal("graphics-debug", graphicsProfile.ProfileId);
    }

    [Fact]
    public void ResolveCodegenProfile_falls_back_to_the_build_profile_binding_when_none_is_selected() {
        EditorBuildPlatformConfigDocument platformConfig = new() {
            PlatformId = "windows"
        };
        EditorPlatformBuildSelectionModel selectionModel = CreateSelectionModel();
        PlatformBuildProfileDefinition buildProfile = selectionModel.TryResolveBuildProfileExact("debug");

        PlatformCodegenProfileDefinition codegenProfile = EditorBuildPlatformConfigDefaultsService.ResolveCodegenProfile(platformConfig, buildProfile, selectionModel);

        Assert.Equal("codegen-debug", codegenProfile.ProfileId);
    }

    [Fact]
    public void ResolveBuildProfile_returns_the_selected_build_profile() {
        EditorBuildPlatformConfigDocument platformConfig = new() {
            PlatformId = "windows",
            SelectedBuildProfileId = "debug"
        };

        PlatformBuildProfileDefinition buildProfile = EditorBuildPlatformConfigDefaultsService.ResolveBuildProfile(platformConfig, CreateSelectionModel());

        Assert.Equal("debug", buildProfile.ProfileId);
    }

    [Fact]
    public void ResolveStorageAndMediaProfiles_return_null_without_a_selection_model() {
        EditorBuildPlatformConfigDocument platformConfig = new() {
            PlatformId = "windows"
        };

        Assert.Null(EditorBuildPlatformConfigDefaultsService.ResolveStorageProfile(platformConfig, null));
        Assert.Null(EditorBuildPlatformConfigDefaultsService.ResolveMediaProfile(platformConfig, null));
    }

    [Fact]
    public void EnsureSettingDefaults_only_fills_blank_values() {
        Dictionary<string, string> values = new() {
            { "graphics.msaa", "8" },
            { "graphics.vsync", "  " }
        };
        PlatformSettingDefinition[] settings = [
            new PlatformSettingDefinition("graphics.msaa", "MSAA", PlatformSettingKind.Text, "4", false, []),
            new PlatformSettingDefinition("graphics.vsync", "VSync", PlatformSettingKind.Text, "true", false, [])
        ];

        EditorBuildPlatformConfigDefaultsService.EnsureSettingDefaults(values, settings);

        Assert.Equal("8", values["graphics.msaa"]);
        Assert.Equal("true", values["graphics.vsync"]);
    }
}
