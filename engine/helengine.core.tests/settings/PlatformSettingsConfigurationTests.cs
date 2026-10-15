using helengine;
using Xunit;

namespace helengine.core.tests.settings {
    /// <summary>
    /// Verifies the platform settings catalog contract shared by the managed core and the transpiled native core.
    /// </summary>
    /// <remarks>
    /// The native string type cannot tell a null string from an empty one, and the C++ generator lowers every
    /// <c>string == null</c> check to <c>String::IsNullOrEmpty</c>. These tests pin a catalog contract that never
    /// depends on that distinction, so a host that passes no configuration boots on every platform.
    /// </remarks>
    public sealed class PlatformSettingsConfigurationTests {
        /// <summary>
        /// Ensures a core whose host passes no configuration gets a valid, empty and usable settings session.
        /// </summary>
        [Fact]
        public void Initialize_WhenHostPassesNoConfiguration_ProvidesAnEmptyUsableCatalog() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });

            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));

            PlatformSettingsConfiguration configuration = core.PlatformSettings.Configuration;
            Assert.Equal("test", configuration.PlatformId);
            Assert.Equal(0, configuration.SettingCount);
            Assert.Equal(0, configuration.PresetCount);
            Assert.Equal(string.Empty, configuration.InitialPresetId);
            Assert.Equal(string.Empty, core.PlatformSettings.ActivePresetId);
            core.PlatformSettings.ApplyValues(new Dictionary<string, PlatformSettingValue>());
            Assert.Equal(0, core.PlatformSettings.Active.Count);
            Assert.Throws<ArgumentException>(() => core.PlatformSettings.SetBoolean(CommonPlatformRuntimeSettings.ShadowsEnabled, true));
        }

        /// <summary>
        /// Ensures the empty catalog survives the portable payload that native hosts decode at startup.
        /// </summary>
        [Fact]
        public void EmptyCatalog_RoundTripsThroughThePortablePayload() {
            using PlatformSettingsConfiguration configuration = new("test", [], [], string.Empty);

            using PlatformSettingsConfiguration decoded = PlatformSettingsSerializer.ReadBytes(PlatformSettingsSerializer.WriteBytes(configuration));

            Assert.Equal("test", decoded.PlatformId);
            Assert.Equal(0, decoded.SettingCount);
            Assert.Equal(0, decoded.PresetCount);
            Assert.Equal(string.Empty, decoded.InitialPresetId);
        }

        /// <summary>
        /// Ensures an empty catalog treats a null initial preset exactly like an empty one, as the native core must.
        /// </summary>
        [Fact]
        public void EmptyCatalog_TreatsNullAndEmptyInitialPresetAlike() {
            using PlatformSettingsConfiguration fromNull = new("test", [], [], null);
            using PlatformSettingsConfiguration fromEmpty = new("test", [], [], string.Empty);

            Assert.Equal(string.Empty, fromNull.InitialPresetId);
            Assert.Equal(string.Empty, fromEmpty.InitialPresetId);
        }

        /// <summary>
        /// Ensures a configuration that does author presets still requires an explicit initial preset.
        /// </summary>
        /// <param name="initialPresetId">Missing initial preset in either of its equivalent spellings.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ConfigurationWithPresets_WhenInitialPresetIsMissing_ThrowsNamingInitialPresetId(string initialPresetId) {
            PlatformRuntimeSettingDefinition[] definitions = CommonPlatformRuntimeSettings.CreateDefinitions();
            using PlatformSettingsPreset preset = CreatePreset();

            ArgumentException exception = Assert.Throws<ArgumentException>(() => new PlatformSettingsConfiguration("test", definitions, [preset], initialPresetId));

            Assert.Equal("initialPresetId", exception.ParamName);
        }

        /// <summary>
        /// Ensures a catalog without presets cannot name an initial preset it cannot select.
        /// </summary>
        [Fact]
        public void EmptyCatalog_WhenInitialPresetIsNamed_ThrowsNamingInitialPresetId() {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => new PlatformSettingsConfiguration("test", [], [], "high"));

            Assert.Equal("initialPresetId", exception.ParamName);
        }

        /// <summary>
        /// Ensures declared settings without any complete preset are rejected rather than silently defaulted.
        /// </summary>
        [Fact]
        public void DefinitionsWithoutPresets_ThrowNamingPresets() {
            PlatformRuntimeSettingDefinition[] definitions = CommonPlatformRuntimeSettings.CreateDefinitions();

            ArgumentException exception = Assert.Throws<ArgumentException>(() => new PlatformSettingsConfiguration("test", definitions, [], string.Empty));

            Assert.Equal("presets", exception.ParamName);
        }

        /// <summary>
        /// Ensures each missing collection is reported under its own parameter name.
        /// </summary>
        [Fact]
        public void MissingCollections_ThrowNamingTheMissingParameter() {
            using PlatformSettingsPreset preset = CreatePreset();

            ArgumentNullException missingDefinitions = Assert.Throws<ArgumentNullException>(() => new PlatformSettingsConfiguration("test", null, [preset], preset.Id));
            ArgumentNullException missingPresets = Assert.Throws<ArgumentNullException>(() => new PlatformSettingsConfiguration("test", CommonPlatformRuntimeSettings.CreateDefinitions(), null, preset.Id));

            Assert.Equal("definitions", missingDefinitions.ParamName);
            Assert.Equal("presets", missingPresets.ParamName);
        }

        /// <summary>
        /// Creates one complete preset covering every common setting.
        /// </summary>
        /// <returns>Preset owned by the calling test.</returns>
        static PlatformSettingsPreset CreatePreset() => new("high", "High", new Dictionary<string, PlatformSettingValue> {
            [CommonPlatformRuntimeSettings.ShadowsEnabled] = PlatformSettingValue.FromBoolean(true),
            [CommonPlatformRuntimeSettings.MaximumVisibleLights] = PlatformSettingValue.FromInteger(32),
            [CommonPlatformRuntimeSettings.SceneLoadingBudget] = PlatformSettingValue.FromNumber(8)
        });
    }
}
