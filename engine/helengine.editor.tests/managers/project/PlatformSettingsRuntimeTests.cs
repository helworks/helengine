using helengine.editor.tests.testing;

namespace helengine.editor.tests.managers.project {
    /// <summary>Exercises actual per-core application, portable serialization and independent authored presets.</summary>
    public sealed class PlatformSettingsRuntimeTests {
        /// <summary>Switching quality applies complete values; runtime edits never mutate authored presets or another session.</summary>
        [Fact]
        public void RuntimeSelectionAppliesIndependentPresetsAndKeepsProjectValuesImmutable() {
            using PlatformSettingsConfiguration configuration = CreateConfiguration("android");
            using Core core = CreateCore(configuration, null);
            Assert.Equal("high", core.PlatformSettings.ActivePresetId);
            core.PlatformSettings.SetBoolean(CommonPlatformRuntimeSettings.ShadowsEnabled, false);
            Assert.False(core.RenderManager3D.ShadowsEnabled);
            Assert.Empty(core.PlatformSettings.ActivePresetId);
            Assert.True(configuration.GetPreset("high").Values.GetValue(CommonPlatformRuntimeSettings.ShadowsEnabled).Boolean);
            core.PlatformSettings.ApplyPreset("low");
            Assert.Equal(4, core.RenderManager3D.MaximumVisibleLights);
            Assert.Equal(2, core.SceneManager.SceneLoadingBudgetMilliseconds);
            core.PlatformSettings.ApplyPreset("high");
            Assert.True(core.RenderManager3D.ShadowsEnabled);
            Assert.Equal(32, core.RenderManager3D.MaximumVisibleLights);
            using Core other = CreateCore(configuration, null);
            core.PlatformSettings.SetInteger(CommonPlatformRuntimeSettings.MaximumVisibleLights, 1);
            Assert.Equal(32, other.RenderManager3D.MaximumVisibleLights);
        }
        /// <summary>Bad values and unknown identifiers cannot partially change the active configuration.</summary>
        [Fact]
        public void InvalidEditsPreserveTheActiveState() {
            using PlatformSettingsConfiguration configuration = CreateConfiguration("android");
            using Core core = CreateCore(configuration, null);
            Assert.Throws<InvalidOperationException>(() => core.PlatformSettings.SetNumber(CommonPlatformRuntimeSettings.SceneLoadingBudget, double.NaN));
            Assert.Throws<InvalidOperationException>(() => core.PlatformSettings.SetInteger(CommonPlatformRuntimeSettings.MaximumVisibleLights, -1));
            Assert.Throws<InvalidOperationException>(() => core.PlatformSettings.SetInteger(CommonPlatformRuntimeSettings.ShadowsEnabled, 1));
            Assert.Throws<ArgumentException>(() => core.PlatformSettings.ApplyPreset("missing"));
            Assert.Throws<ArgumentException>(() => core.PlatformSettings.SetBoolean("missing", true));
            Assert.Equal("high", core.PlatformSettings.ActivePresetId);
            Assert.True(core.RenderManager3D.ShadowsEnabled);
            Assert.Equal(8, core.SceneManager.SceneLoadingBudgetMilliseconds);
        }
        /// <summary>Serialized presets retain every explicit typed value and reject truncated or extra data.</summary>
        [Fact]
        public void CatalogRoundTripsWithoutInheritanceOrPlatformLeakage() {
            using PlatformSettingsConfiguration configuration = CreateConfiguration("android");
            byte[] bytes = PlatformSettingsSerializer.WriteBytes(configuration);
            using PlatformSettingsConfiguration decoded = PlatformSettingsSerializer.ReadBytes(bytes);
            Assert.Equal("android", decoded.PlatformId);
            Assert.Equal(2, decoded.PresetCount);
            Assert.Equal(3, decoded.GetPreset("low").Values.Count);
            Assert.False(decoded.GetPreset("low").Values.GetValue(CommonPlatformRuntimeSettings.ShadowsEnabled).Boolean);
            Assert.True(decoded.GetPreset("high").Values.GetValue(CommonPlatformRuntimeSettings.ShadowsEnabled).Boolean);
            Assert.Throws<InvalidOperationException>(() => PlatformSettingsSerializer.ReadBytes([.. bytes, 1]));
            Assert.Throws<EndOfStreamException>(() => PlatformSettingsSerializer.ReadBytes(bytes[..^3]));
        }
        /// <summary>Omitting a setting is an error even if another preset and the schema contain a usable default.</summary>
        [Fact]
        public void IncompletePresetsDoNotInheritValues() {
            PlatformRuntimeSettingDefinition[] definitions = CommonPlatformRuntimeSettings.CreateDefinitions();
            Dictionary<string, PlatformSettingValue> incomplete = CreateValues(true, 32, 8);
            incomplete.Remove(CommonPlatformRuntimeSettings.SceneLoadingBudget);
            using PlatformSettingsPreset preset = new("incomplete", "Incomplete", incomplete);
            Assert.Throws<InvalidOperationException>(() => new PlatformSettingsConfiguration("android", definitions, [preset], preset.Id));
        }
        /// <summary>Platform contracts can declare additional settings; the engineer's explicit API call runs their backend.</summary>
        [Fact]
        public void PlatformSpecificSettingsApplyThroughTheBackendAndFailBeforePublication() {
            PlatformRuntimeSettingDefinition[] definitions = [.. CommonPlatformRuntimeSettings.CreateDefinitions(),
                new("device.filter", "Device Filter", "Platform", PlatformSettingValue.FromChoice("fast"), true, 0, 0, ["fast", "precise"])];
            Dictionary<string, PlatformSettingValue> values = CreateValues(true, 32, 8);
            values.Add("device.filter", PlatformSettingValue.FromChoice("fast"));
            using PlatformSettingsPreset preset = new("custom", "Custom", values);
            using PlatformSettingsConfiguration configuration = new("test", definitions, [preset], preset.Id);
            TestPlatformSettingsBackend backend = new();
            using Core core = CreateCore(configuration, backend);
            Assert.Equal(1, backend.AppliedCount);
            core.PlatformSettings.SetChoice("device.filter", "precise");
            Assert.Equal(2, backend.AppliedCount);
            Assert.Equal("precise", backend.LastFilter);
            backend.RejectChanges = true;
            Assert.Throws<InvalidOperationException>(() => core.PlatformSettings.SetChoice("device.filter", "fast"));
            Assert.Equal("precise", core.PlatformSettings.GetValue("device.filter").Text);
            Assert.Equal(2, backend.AppliedCount);
        }
        /// <summary>Startup-only options are applied once and cannot be changed by runtime preset switches.</summary>
        [Fact]
        public void StartupOnlySettingsCannotBeChangedAfterInitialization() {
            PlatformRuntimeSettingDefinition definition = new("device.filter", "Filter", "Platform", PlatformSettingValue.FromChoice("fast"), false, 0, 0, ["fast", "precise"]);
            using PlatformSettingsPreset first = new("first", "First", new() { [definition.Id] = PlatformSettingValue.FromChoice("fast") });
            using PlatformSettingsPreset second = new("second", "Second", new() { [definition.Id] = PlatformSettingValue.FromChoice("precise") });
            using PlatformSettingsConfiguration configuration = new("test", [definition], [first, second], first.Id);
            TestPlatformSettingsBackend backend = new();
            using Core core = CreateCore(configuration, backend);
            Assert.Throws<InvalidOperationException>(() => core.PlatformSettings.ApplyPreset("second"));
            Assert.Equal("first", core.PlatformSettings.ActivePresetId);
            Assert.Equal(1, backend.AppliedCount);
        }
        /// <summary>Builds an actual core session over fake graphics/content adapters without a GPU or emulator.</summary>
        static Core CreateCore(PlatformSettingsConfiguration configuration, IPlatformSettingsBackend backend) {
            CoreInitializationOptions options = new() { ContentStreamSource = new FakeContentStreamSource(),
                PlatformSettingsConfiguration = configuration, PlatformSettingsBackend = backend, SceneCatalog = new RuntimeSceneCatalog([]) };
            Core core = new(options); core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo(configuration.PlatformId, "test"));
            return core;
        }
        /// <summary>Creates two complete presets with distinct graphics and loading values.</summary>
        static PlatformSettingsConfiguration CreateConfiguration(string platformId) => new(platformId, CommonPlatformRuntimeSettings.CreateDefinitions(), [
            new PlatformSettingsPreset("high", "High", CreateValues(true, 32, 8)),
            new PlatformSettingsPreset("low", "Low", CreateValues(false, 4, 2))], "high");
        /// <summary>Creates a full common configuration with no missing or inherited values.</summary>
        static Dictionary<string, PlatformSettingValue> CreateValues(bool shadows, int lights, double budget) => new() {
            [CommonPlatformRuntimeSettings.ShadowsEnabled] = PlatformSettingValue.FromBoolean(shadows),
            [CommonPlatformRuntimeSettings.MaximumVisibleLights] = PlatformSettingValue.FromInteger(lights),
            [CommonPlatformRuntimeSettings.SceneLoadingBudget] = PlatformSettingValue.FromNumber(budget)
        };
    }
}
