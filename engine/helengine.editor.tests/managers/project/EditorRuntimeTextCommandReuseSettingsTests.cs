using System.Reflection;
using System.Text.Json;
using helengine.baseplatform.Manifest;
using helengine.baseplatform.Requests;
using helengine.editor.tests.testing;
using helengine.files;
using helengine.platforms;

namespace helengine.editor.tests.managers.project {
    /// <summary>Validates the per-platform byte budget through persistence, UI parsing and actual PS2 native-manifest transport.</summary>
    public sealed class EditorRuntimeTextCommandReuseSettingsTests {
        /// <summary>Preserves the disabled default for existing projects without a budget field.</summary>
        [Fact]
        public void Missing_budget_preserves_disabled_default() {
            string root = FixtureRoot(nameof(Missing_budget_preserves_disabled_default));
            File.WriteAllText(Path.Combine(root, "user_settings", "build_config.json"), "{\"platforms\":[{\"platformId\":\"ps2\"}]}");
            File.WriteAllText(Path.Combine(root, "settings", "build_config.json"), "{\"platforms\":[{\"platformId\":\"ps2\"}]}");
            Assert.Equal(0, EditorRuntimeLoadingSettingsResolver.Load(root, "ps2").Render2DTextCommandReuseMemoryBudgetBytes);
        }

        /// <summary>Uses a project budget when the legacy user field is absent and persists the composed value without converting it into zero.</summary>
        [Fact]
        public void Omitted_user_budget_uses_project_value_and_survives_service_roundtrip() {
            string root = FixtureRoot(nameof(Omitted_user_budget_uses_project_value_and_survives_service_roundtrip));
            File.WriteAllText(Path.Combine(root, "settings", "build_config.json"), "{\"platforms\":[{\"platformId\":\"ps2\",\"render2DTextCommandReuseMemoryBudgetBytes\":8192}]}");
            File.WriteAllText(Path.Combine(root, "user_settings", "build_config.json"), "{\"platforms\":[{\"platformId\":\"ps2\",\"selectedEnvironmentId\":\"release\"}]}");
            Assert.Equal(8192, EditorRuntimeLoadingSettingsResolver.Load(root, "ps2").Render2DTextCommandReuseMemoryBudgetBytes);
            EditorBuildConfigService service = new(root);
            EditorBuildConfigDocument composed = service.TryLoadExisting();
            Assert.Equal(8192, composed.Platforms[0].Render2DTextCommandReuseMemoryBudgetBytes);
            service.Save(composed);
            Assert.Equal(8192, service.TryLoadExisting().Platforms[0].Render2DTextCommandReuseMemoryBudgetBytes);
            Assert.Equal(8192, EditorRuntimeLoadingSettingsResolver.Load(root, "ps2").Render2DTextCommandReuseMemoryBudgetBytes);
        }

        /// <summary>Keeps an explicitly serialized user zero as disabled even when the project requests positive storage.</summary>
        [Fact]
        public void Explicit_user_zero_disables_project_budget() {
            string root = FixtureRoot(nameof(Explicit_user_zero_disables_project_budget));
            File.WriteAllText(Path.Combine(root, "settings", "build_config.json"), "{\"platforms\":[{\"platformId\":\"ps2\",\"render2DTextCommandReuseMemoryBudgetBytes\":8192}]}");
            File.WriteAllText(Path.Combine(root, "user_settings", "build_config.json"), "{\"platforms\":[{\"platformId\":\"ps2\",\"render2DTextCommandReuseMemoryBudgetBytes\":0}]}");
            Assert.Equal(0, EditorRuntimeLoadingSettingsResolver.Load(root, "ps2").Render2DTextCommandReuseMemoryBudgetBytes);
            Assert.Equal(0, new EditorBuildConfigService(root).TryLoadExisting().Platforms[0].Render2DTextCommandReuseMemoryBudgetBytes);
        }

        /// <summary>Roundtrips independent platform values through the production document schema without enabling an unconfigured platform.</summary>
        [Fact]
        public void Serialized_budget_remains_per_platform() {
            EditorBuildConfigDocument document = new() { Platforms = [
                new EditorBuildPlatformConfigDocument { PlatformId = "ps2", Render2DTextCommandReuseMemoryBudgetBytes = 8192 },
                new EditorBuildPlatformConfigDocument { PlatformId = "ps1" }
            ] };
            JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            string json = JsonSerializer.Serialize(document, options);
            Assert.Contains("\"render2DTextCommandReuseMemoryBudgetBytes\":8192", json);
            EditorBuildConfigDocument restored = JsonSerializer.Deserialize<EditorBuildConfigDocument>(json, options);
            Assert.Equal(8192, EditorRuntimeLoadingSettingsResolver.Resolve(restored, "ps2").Render2DTextCommandReuseMemoryBudgetBytes);
            Assert.Equal(0, EditorRuntimeLoadingSettingsResolver.Resolve(restored, "ps1").Render2DTextCommandReuseMemoryBudgetBytes);
        }

        /// <summary>Rejects negative integer budgets before constructing a manifest or committing any neighboring UI settings.</summary>
        [Theory]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void Negative_budget_rejected_before_manifest(int budget) {
            Assert.Throws<InvalidOperationException>(() => EditorRuntimeLoadingSettingsResolver.Resolve(
                new EditorBuildConfigDocument { Platforms = [new EditorBuildPlatformConfigDocument { PlatformId = "ps2", Render2DTextCommandReuseMemoryBudgetBytes = budget }] }, "ps2"));
            Assert.Throws<InvalidOperationException>(() => new EditorRuntimeLoadingSettings(8, string.Empty, budget));
        }

        /// <summary>Parses bytes as an integer and keeps loading fields unchanged when the new UI input is invalid.</summary>
        [Fact]
        public void Ui_budget_edit_is_atomic_and_zero_is_valid() {
            EditorBuildConfigDocument document = new();
            EditorBuildPlatformConfigDocument platform = new() { Render2DTextCommandReuseMemoryBudgetBytes = 8192 };
            Assert.False(EditorRuntimeLoadingSettingsEditService.TryApply(document, platform, "6", "2", "Loading", "Loading", "-1"));
            Assert.Equal(8, document.SceneLoadingBudgetMilliseconds);
            Assert.Equal(8192, platform.Render2DTextCommandReuseMemoryBudgetBytes);
            Assert.False(EditorRuntimeLoadingSettingsEditService.TryParseTextCommandReuseBudget("1.5", out _));
            Assert.False(EditorRuntimeLoadingSettingsEditService.TryParseTextCommandReuseBudget("2147483648", out _));
            Assert.True(EditorRuntimeLoadingSettingsEditService.TryApply(document, platform, "6", "2", "Loading", "Loading", "0"));
            Assert.Equal(0, platform.Render2DTextCommandReuseMemoryBudgetBytes);
        }

        /// <summary>Transports PS2's effective budget through the actual final manifest writer and cloned builder request with invariant integer output.</summary>
        [Fact]
        public void Ps2_final_manifest_and_builder_request_preserve_budget() {
            string root = FixtureRoot(nameof(Ps2_final_manifest_and_builder_request_preserve_budget));
            File.WriteAllText(Path.Combine(root, "user_settings", "build_config.json"), "{\"platforms\":[{\"platformId\":\"ps2\",\"render2DTextCommandReuseMemoryBudgetBytes\":8192}]}");
            PlatformBuildManifest manifest = Manifest();
            EditorPlatformBuildGraphRunner runner = new(root, "1", "project", "1", Array.Empty<IAssetImporterRegistration>(),
                new AvailablePlatformDescriptor("ps2", "PlayStation 2", typeof(EditorRuntimeTextCommandReuseSettingsTests).Assembly.Location, string.Empty, true, Path.Combine(root, "generated")),
                null, new EditorPlatformAssetBuilderLoader(), new EditorGeneratedCoreRegenerationService(), null,
                TestGeneratedAssetGraph.CreateShaderLibrary(), new EngineCodegenToolProvider());
            MethodInfo write = typeof(EditorPlatformBuildGraphRunner).GetMethod("WriteRuntimeNativeManifestSources", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(write);
            write.Invoke(runner, [manifest, Path.Combine(root, "generated")]);
            Assert.Equal(8192, manifest.Render2DTextCommandReuseMemoryBudgetBytes);
            string source = File.ReadAllText(Path.Combine(root, "generated", "runtime", "runtime_startup_manifest.cpp"));
            string header = File.ReadAllText(Path.Combine(root, "generated", "runtime", "runtime_startup_manifest.hpp"));
            Assert.Contains("static const int kRuntimeRender2DTextCommandReuseMemoryBudgetBytes = 8192;", source);
            Assert.Contains("int he_get_runtime_render2d_text_command_reuse_memory_budget_bytes();", header);
            Assert.Contains("return kRuntimeRender2DTextCommandReuseMemoryBudgetBytes;", source);
            string staging = Path.Combine(root, "staged");
            Directory.CreateDirectory(staging);
            MethodInfo build = typeof(EditorPlatformBuildGraphRunner).GetMethod("BuildRequest", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(build);
            PlatformBuildRequest request = (PlatformBuildRequest)build.Invoke(runner, [
                new EditorBuildQueueItemDocument { OutputDirectoryPath = Path.Combine(root, "output") }, manifest, staging,
                Path.Combine(root, "builder"), "release", "graphics", "codegen", new Dictionary<string, string>(),
                "media", Path.Combine(root, "generated"), "storage", Path.Combine(root, "objects")
            ]);
            Assert.Equal(8192, request.Manifest.Render2DTextCommandReuseMemoryBudgetBytes);
            new EditorRuntimeNativeManifestWriter().Write(Path.Combine(root, "builder-generated"), request.Manifest);
            Assert.Contains("kRuntimeRender2DTextCommandReuseMemoryBudgetBytes = 8192;", File.ReadAllText(Path.Combine(root, "builder-generated", "runtime", "runtime_startup_manifest.cpp")));
        }

        /// <summary>Creates deterministic, visible test-owned directories without changing a user's project or temporary system paths.</summary>
        static string FixtureRoot(string name) {
            string root = Path.Combine("C:/dev/helworks/builds/helengine-ps2/menu-20261007/text-command-reuse/policy-tests", name);
            Directory.CreateDirectory(Path.Combine(root, "user_settings"));
            Directory.CreateDirectory(Path.Combine(root, "settings"));
            return root;
        }

        /// <summary>Supplies the PS2 startup scene with its real cooked-path metadata contract for production writer validation.</summary>
        static PlatformBuildManifest Manifest() {
            return new PlatformBuildManifest(1, "project", "1", "1", "ps2", "1", "DemoDiscMainMenu", [
                new PlatformBuildScene("DemoDiscMainMenu", "Menu", "Scenes/Menu.helen", [], [new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.CookedRelativePath, "cooked/scenes/menu.hasset")])
            ], [], [], [], [], new PlatformContainerWritePlan(string.Empty, []), [], PlatformBuildRuntimeFeatureManifest.Empty);
        }
    }
}
