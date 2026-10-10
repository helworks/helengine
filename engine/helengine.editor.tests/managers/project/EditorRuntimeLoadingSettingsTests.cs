using System.Globalization;
using System.Reflection;
using System.Text.Json;
using helengine.baseplatform.Manifest;
using helengine.baseplatform.Requests;
using helengine.editor.tests.testing;
using helengine.files;
using helengine.platforms;

namespace helengine.editor.tests.managers.project {
    /// <summary>Tests persisted loading policy and the real final native-manifest generation path.</summary>
    public sealed class EditorRuntimeLoadingSettingsTests {
        /// <summary>Verifies user defaults, selected overrides and explicit loading-screen disable remain independent.</summary>
        [Fact]
        public void Resolve_preserves_platform_isolation_and_inheritance() {
            EditorBuildConfigDocument document = new() {
                SceneLoadingBudgetMilliseconds = 6.5,
                LoadingScreenSceneId = "SceneLoadingScreen",
                Platforms = [
                    new EditorBuildPlatformConfigDocument { PlatformId = "wii", SceneLoadingBudgetMilliseconds = 2.25 },
                    new EditorBuildPlatformConfigDocument { PlatformId = "ps2", SceneLoadingBudgetMilliseconds = 0, OverridesLoadingScreenSceneId = true }
                ]
            };
            EditorRuntimeLoadingSettings wii = EditorRuntimeLoadingSettingsResolver.Resolve(document, "WII");
            EditorRuntimeLoadingSettings ps2 = EditorRuntimeLoadingSettingsResolver.Resolve(document, "ps2");
            Assert.Equal(2.25, wii.SceneLoadingBudgetMilliseconds);
            Assert.Equal("SceneLoadingScreen", wii.LoadingScreenSceneId);
            Assert.Equal(6.5, ps2.SceneLoadingBudgetMilliseconds);
            Assert.Equal(string.Empty, ps2.LoadingScreenSceneId);
            Assert.Equal(6.5, document.SceneLoadingBudgetMilliseconds);
            Assert.Equal(0, document.Platforms[1].SceneLoadingBudgetMilliseconds);
        }

        /// <summary>Rejects invalid effective time budgets rather than treating them as an uncapped load request.</summary>
        [Theory]
        [InlineData(0d)]
        [InlineData(-1d)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void Resolve_rejects_invalid_user_budgets(double value) {
            Assert.Throws<InvalidOperationException>(() => EditorRuntimeLoadingSettingsResolver.Resolve(
                new EditorBuildConfigDocument { SceneLoadingBudgetMilliseconds = value }, "wii"));
        }

        /// <summary>Allows only zero to represent inheritance in a platform-specific budget.</summary>
        [Theory]
        [InlineData(-0.5d)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void Resolve_rejects_invalid_platform_overrides(double value) {
            Assert.Throws<InvalidOperationException>(() => EditorRuntimeLoadingSettingsResolver.Resolve(
                new EditorBuildConfigDocument { Platforms = [new EditorBuildPlatformConfigDocument { PlatformId = "wii", SceneLoadingBudgetMilliseconds = value }] }, "wii"));
        }

        /// <summary>Ensures editor input validation is atomic and exposes inheritance and disabled screens explicitly.</summary>
        [Fact]
        public void Edit_rejects_invalid_input_without_partial_mutation() {
            EditorBuildConfigDocument document = new();
            EditorBuildPlatformConfigDocument platform = new() { PlatformId = "wii" };
            Assert.False(EditorRuntimeLoadingSettingsEditService.TryApply(document, platform, "2.5", "NaN", "changed", "changed"));
            Assert.Equal(8d, document.SceneLoadingBudgetMilliseconds);
            Assert.Equal(string.Empty, document.LoadingScreenSceneId);
            Assert.Equal(0, platform.SceneLoadingBudgetMilliseconds);
            Assert.True(EditorRuntimeLoadingSettingsEditService.TryApply(document, platform, "3.5", "1.25", "SceneLoadingScreen", "-"));
            Assert.Equal(3.5, document.SceneLoadingBudgetMilliseconds);
            Assert.Equal(1.25, platform.SceneLoadingBudgetMilliseconds);
            Assert.True(platform.OverridesLoadingScreenSceneId);
            Assert.Equal(string.Empty, platform.LoadingScreenSceneId);
            Assert.True(EditorRuntimeLoadingSettingsEditService.TryApply(document, platform, "3.5", "", "SceneLoadingScreen", ""));
            Assert.Equal(0, platform.SceneLoadingBudgetMilliseconds);
            Assert.False(platform.OverridesLoadingScreenSceneId);
        }

        /// <summary>Round-trips the existing local settings service while keeping unrelated platforms and the project scene package untouched.</summary>
        [Fact]
        public void Save_round_trips_loading_settings_in_the_local_document() {
            string root = FixtureRoot(nameof(Save_round_trips_loading_settings_in_the_local_document));
            EditorBuildConfigDocument document = new() {
                SceneLoadingBudgetMilliseconds = 4.5,
                LoadingScreenSceneId = "SceneLoadingScreen",
                Platforms = [new EditorBuildPlatformConfigDocument { PlatformId = "wii", SceneLoadingBudgetMilliseconds = 1.75, OverridesLoadingScreenSceneId = true, LoadingScreenSceneId = "WiiLoading" }]
            };
            EditorBuildConfigService service = new(root);
            service.Save(document);
            EditorBuildConfigDocument restored = service.TryLoadExisting();
            Assert.Equal(4.5, restored.SceneLoadingBudgetMilliseconds);
            Assert.Equal("SceneLoadingScreen", restored.LoadingScreenSceneId);
            Assert.Equal(1.75, restored.Platforms[0].SceneLoadingBudgetMilliseconds);
            Assert.True(restored.Platforms[0].OverridesLoadingScreenSceneId);
            Assert.Equal("WiiLoading", restored.Platforms[0].LoadingScreenSceneId);
            Assert.DoesNotContain("sceneLoadingBudgetMilliseconds", File.ReadAllText(Path.Combine(root, "settings", "build_config.json")));
            Assert.Equal(1.75, EditorRuntimeLoadingSettingsResolver.Load(root, "wii").SceneLoadingBudgetMilliseconds);
        }

        /// <summary>Verifies missing settings retain the Core default, while malformed persisted settings fail rather than silently defaulting.</summary>
        [Fact]
        public void Load_rejects_malformed_and_invalid_persisted_settings() {
            string root = FixtureRoot(nameof(Load_rejects_malformed_and_invalid_persisted_settings));
            string path = Path.Combine(root, "user_settings", "build_config.json");
            if (File.Exists(path)) { File.Delete(path); }
            Assert.Equal(8d, EditorRuntimeLoadingSettingsResolver.Load(root, "wii").SceneLoadingBudgetMilliseconds);
            File.WriteAllText(path, "{bad-json}");
            Assert.Throws<JsonException>(() => EditorRuntimeLoadingSettingsResolver.Load(root, "wii"));
            File.WriteAllText(path, "{\"sceneLoadingBudgetMilliseconds\":0}");
            Assert.Throws<InvalidOperationException>(() => EditorRuntimeLoadingSettingsResolver.Load(root, "wii"));
        }

        /// <summary>Rejects a configured loading screen that was not included in the real packaged scene catalog.</summary>
        [Fact]
        public void Apply_rejects_missing_loading_screen_before_changing_manifest() {
            PlatformBuildManifest manifest = Manifest();
            Assert.Throws<InvalidOperationException>(() => EditorRuntimeLoadingSettingsResolver.Apply(manifest, new EditorRuntimeLoadingSettings(2, "MissingScreen")));
            Assert.Equal(8d, manifest.SceneLoadingBudgetMilliseconds);
            Assert.Equal(string.Empty, manifest.LoadingScreenSceneId);
        }

        /// <summary>Uses the real project catalog conversion to stamp an authored path alias as the operational basename scene ID.</summary>
        [Fact]
        public void Apply_resolves_authored_path_through_actual_project_catalog() {
            string root = FixtureRoot(nameof(Apply_resolves_authored_path_through_actual_project_catalog));
            string fullPath = CreateAuthoredScene(root, "Scenes/SceneLoadingScreen.helen");
            EditorProjectSceneCatalogService catalog = new(root);
            Assert.Equal("SceneLoadingScreen", catalog.ResolveSceneId(fullPath));
            Assert.Equal("Scenes/SceneLoadingScreen.helen", catalog.ResolveScenePath("SceneLoadingScreen"));
            EditorRuntimeLoadingSettings settings = new(2.25, "Scenes/SceneLoadingScreen.helen");
            PlatformBuildManifest manifest = Manifest();
            EditorRuntimeLoadingSettingsResolver.Apply(manifest, settings, root);
            Assert.Equal("SceneLoadingScreen", manifest.LoadingScreenSceneId);
            Assert.Equal("Scenes/SceneLoadingScreen.helen", settings.LoadingScreenSceneId);
        }

        /// <summary>Does not collapse an unrelated folder or traversal path to the name of an existing packaged scene.</summary>
        [Theory]
        [InlineData("Other/SceneLoadingScreen.helen")]
        [InlineData("scenes/SceneLoadingScreen.helen")]
        [InlineData("Scenes/../Scenes/SceneLoadingScreen.helen")]
        public void Apply_rejects_unrelated_or_noncanonical_authored_paths(string alias) {
            string root = FixtureRoot(nameof(Apply_rejects_unrelated_or_noncanonical_authored_paths));
            CreateAuthoredScene(root, "Scenes/SceneLoadingScreen.helen");
            PlatformBuildManifest manifest = Manifest();
            Assert.Throws<InvalidOperationException>(() => EditorRuntimeLoadingSettingsResolver.Apply(manifest, new EditorRuntimeLoadingSettings(2, alias), root));
            Assert.Equal(8d, manifest.SceneLoadingBudgetMilliseconds);
            Assert.Equal(string.Empty, manifest.LoadingScreenSceneId);
        }

        /// <summary>Rejects ambiguous authored basenames, but an exact packaged runtime ID remains authoritative without alias lookup.</summary>
        [Fact]
        public void Apply_rejects_ambiguous_authored_alias_and_prefers_exact_runtime_id() {
            string root = FixtureRoot(nameof(Apply_rejects_ambiguous_authored_alias_and_prefers_exact_runtime_id));
            CreateAuthoredScene(root, "Scenes/SceneLoadingScreen.helen");
            CreateAuthoredScene(root, "Other/SceneLoadingScreen.helen");
            PlatformBuildManifest manifest = Manifest();
            Assert.Throws<InvalidOperationException>(() => EditorRuntimeLoadingSettingsResolver.Apply(manifest, new EditorRuntimeLoadingSettings(2, "Scenes/SceneLoadingScreen.helen"), root));
            Assert.Equal(string.Empty, manifest.LoadingScreenSceneId);
            EditorRuntimeLoadingSettingsResolver.Apply(manifest, new EditorRuntimeLoadingSettings(2, "SceneLoadingScreen"), root);
            Assert.Equal("SceneLoadingScreen", manifest.LoadingScreenSceneId);
        }

        /// <summary>Rejects duplicate operational IDs rather than selecting the first matching packaged scene.</summary>
        [Fact]
        public void Apply_rejects_ambiguous_packaged_runtime_ids() {
            PlatformBuildManifest manifest = Manifest();
            PlatformBuildManifest duplicate = new(1, "project", "1", "1", "wii", "1", "DemoDiscMainMenu",
                [manifest.Scenes[0], manifest.Scenes[1], manifest.Scenes[1]], [], [], [], [],
                new PlatformContainerWritePlan(string.Empty, []), [], PlatformBuildRuntimeFeatureManifest.Empty);
            Assert.Throws<InvalidOperationException>(() => EditorRuntimeLoadingSettingsResolver.Apply(duplicate, new EditorRuntimeLoadingSettings(2, "SceneLoadingScreen")));
            Assert.Equal(8d, duplicate.SceneLoadingBudgetMilliseconds);
            Assert.Equal(string.Empty, duplicate.LoadingScreenSceneId);
        }

        /// <summary>Uses graph finalization and the real builder-request clone to prove packaged loading policy survives both native-manifest boundaries.</summary>
        [Fact]
        public void Final_graph_manifest_transports_effective_wii_settings_with_invariant_numbers() {
            string root = FixtureRoot(nameof(Final_graph_manifest_transports_effective_wii_settings_with_invariant_numbers));
            string fullScenePath = CreateAuthoredScene(root, "Scenes/SceneLoadingScreen.helen");
            Assert.Equal("SceneLoadingScreen", new EditorProjectSceneCatalogService(root).ResolveSceneId(fullScenePath));
            File.WriteAllText(Path.Combine(root, "user_settings", "build_config.json"), "{\"sceneLoadingBudgetMilliseconds\":8,\"platforms\":[{\"platformId\":\"wii\",\"sceneLoadingBudgetMilliseconds\":2.25,\"overridesLoadingScreenSceneId\":true,\"loadingScreenSceneId\":\"Scenes/SceneLoadingScreen.helen\"}]}");
            PlatformBuildManifest manifest = Manifest();
            EditorPlatformBuildGraphRunner runner = new(root, "1", "project", "1", Array.Empty<IAssetImporterRegistration>(),
                new AvailablePlatformDescriptor("wii", "Nintendo Wii", typeof(EditorRuntimeLoadingSettingsTests).Assembly.Location, string.Empty, true, Path.Combine(root, "generated")),
                null, new EditorPlatformAssetBuilderLoader(), new EditorGeneratedCoreRegenerationService(), null,
                TestGeneratedAssetGraph.CreateShaderLibrary(), new EngineCodegenToolProvider());
            MethodInfo method = typeof(EditorPlatformBuildGraphRunner).GetMethod("WriteRuntimeNativeManifestSources", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            CultureInfo previous = CultureInfo.CurrentCulture;
            try {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
                method.Invoke(runner, [manifest, Path.Combine(root, "generated")]);
            } finally { CultureInfo.CurrentCulture = previous; }
            string source = File.ReadAllText(Path.Combine(root, "generated", "runtime", "runtime_startup_manifest.cpp"));
            string header = File.ReadAllText(Path.Combine(root, "generated", "runtime", "runtime_startup_manifest.hpp"));
            Assert.Contains("kRuntimeSceneLoadingBudgetMilliseconds = 2.25;", source);
            Assert.Contains("kRuntimeLoadingScreenSceneId[] = \"SceneLoadingScreen\";", source);
            Assert.Contains("double he_get_runtime_scene_loading_budget_milliseconds();", header);
            Assert.Contains("const char* he_get_runtime_loading_screen_scene_id();", header);
            Assert.Equal(2.25, manifest.SceneLoadingBudgetMilliseconds);
            Assert.Equal("SceneLoadingScreen", manifest.LoadingScreenSceneId);

            string stagingRoot = Path.Combine(root, "staged");
            Directory.CreateDirectory(stagingRoot);
            File.WriteAllText(Path.Combine(stagingRoot, "loading.hasset"), "fixture payload");
            MethodInfo buildRequest = typeof(EditorPlatformBuildGraphRunner).GetMethod("BuildRequest", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(buildRequest);
            PlatformBuildRequest request = (PlatformBuildRequest)buildRequest.Invoke(runner, [
                new EditorBuildQueueItemDocument { OutputDirectoryPath = Path.Combine(root, "output") },
                manifest, stagingRoot, Path.Combine(root, "builder"), "release", "graphics", "codegen",
                new Dictionary<string, string>(), "media", Path.Combine(root, "generated"), "storage", Path.Combine(root, "objects")
            ]);
            Assert.NotSame(manifest, request.Manifest);
            Assert.Equal(2.25, request.Manifest.SceneLoadingBudgetMilliseconds);
            Assert.Equal("SceneLoadingScreen", request.Manifest.LoadingScreenSceneId);
            Assert.Equal(manifest.Scenes.Select(scene => scene.SceneId), request.Manifest.Scenes.Select(scene => scene.SceneId));
            new EditorRuntimeNativeManifestWriter().Write(Path.Combine(root, "builder-generated"), request.Manifest);
            string builderSource = File.ReadAllText(Path.Combine(root, "builder-generated", "runtime", "runtime_startup_manifest.cpp"));
            Assert.Contains("kRuntimeSceneLoadingBudgetMilliseconds = 2.25;", builderSource);
            Assert.Contains("kRuntimeLoadingScreenSceneId[] = \"SceneLoadingScreen\";", builderSource);
        }

        /// <summary>Creates fixed, visible validation paths and overwrites only these test-owned fixtures.</summary>
        static string FixtureRoot(string name) {
            string root = Path.Combine("C:/dev/helworks/builds/builder-coordination/menu-20261007/loading-settings-tests/fixtures", name);
            Directory.CreateDirectory(Path.Combine(root, "user_settings"));
            return root;
        }

        /// <summary>Creates a test-owned authored scene path for the production project catalog to enumerate without requiring payload parsing.</summary>
        static string CreateAuthoredScene(string root, string relativePath) {
            string path = Path.Combine(root, "assets", relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, string.Empty);
            return path;
        }

        /// <summary>Supplies a real two-scene Wii package with canonical runtime scene identities.</summary>
        static PlatformBuildManifest Manifest() {
            return new PlatformBuildManifest(1, "project", "1", "1", "wii", "1", "DemoDiscMainMenu", [
                new PlatformBuildScene("DemoDiscMainMenu", "Menu", "Scenes/Menu.helen", [], [new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.CookedRelativePath, "cooked/scenes/menu.hasset")]),
                new PlatformBuildScene("SceneLoadingScreen", "Loading", "cooked/scenes/loading.hasset", [], [new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.CookedRelativePath, "cooked/scenes/loading.hasset")])
            ], [], [], [], [], new PlatformContainerWritePlan(string.Empty, []), [], PlatformBuildRuntimeFeatureManifest.Empty);
        }
    }
}
