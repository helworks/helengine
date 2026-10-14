using helengine.baseplatform.Definitions;

namespace helengine.editor.tests.managers.project {
    /// <summary>Verifies complete platform-owned project documents and editor conversion for every supported value kind.</summary>
    public sealed class PlatformSettingsAuthoringTests {
        /// <summary>Presets survive shared-profile persistence independently on different platforms, including explicit empty text.</summary>
        [Fact]
        public void PlatformPresetsPersistIndependentValuesWithoutInheritance() {
            string root = Path.Combine(AppContext.BaseDirectory, "platform-presets-tests", Guid.NewGuid().ToString("N"));
            PlatformRuntimeSettingDefinition text = new("device.label", "Label", "Platform", PlatformSettingValue.FromText("seed"), true, 0, 0, []);
            PlatformRuntimeSettingDefinition[] definitions = [.. CommonPlatformRuntimeSettings.CreateDefinitions(), text];
            EditorPlatformProfileSettingsDocument android = new() { PlatformId = "android" };
            EditorPlatformProfileSettingsDocument windows = new() { PlatformId = "windows" };
            EditorRuntimeSettingsAuthoringService.Initialize(android, definitions); EditorRuntimeSettingsAuthoringService.Initialize(windows, definitions);
            android.RuntimeSettings.Presets[0].Values[CommonPlatformRuntimeSettings.ShadowsEnabled] = "false";
            android.RuntimeSettings.Presets[0].Values[text.Id] = "";
            try {
                EditorProfileSettingsService service = new(root);
                service.Save(new EditorProfileSettingsDocument { Platforms = [android, windows] });
                EditorProfileSettingsDocument loaded = service.TryLoadExisting();
                EditorPlatformProfileSettingsDocument savedAndroid = loaded.Platforms.Single(platform => platform.PlatformId == "android");
                Assert.Equal("false", savedAndroid.RuntimeSettings.Presets[0].Values[CommonPlatformRuntimeSettings.ShadowsEnabled]);
                Assert.Equal("true", loaded.Platforms.Single(platform => platform.PlatformId == "windows").RuntimeSettings.Presets[0].Values[CommonPlatformRuntimeSettings.ShadowsEnabled]);
                using PlatformSettingsConfiguration configuration = EditorRuntimeSettingsAuthoringService.BuildConfiguration("android", savedAndroid.RuntimeSettings, definitions);
                using PlatformSettingsConfiguration decoded = PlatformSettingsSerializer.ReadBytes(PlatformSettingsSerializer.WriteBytes(configuration));
                Assert.Equal("", decoded.GetPreset("default").Values.GetValue(text.Id).Text);
                Assert.False(EditorRuntimeSettingsAuthoringService.CreateEditorDefinitions([text])[0].Required);
                savedAndroid.RuntimeSettings.Presets[0].Values.Remove(text.Id);
                Assert.Throws<InvalidOperationException>(() => EditorRuntimeSettingsAuthoringService.BuildConfiguration("android", savedAndroid.RuntimeSettings, definitions));
            } finally { if (Directory.Exists(root)) { Directory.Delete(root, true); } }
        }
        /// <summary>Every kind is parsed with its declared type and numeric editor input is invariant.</summary>
        [Fact]
        public void TypedDefinitionsRoundTripChoicesTextAndNumbers() {
            PlatformRuntimeSettingDefinition[] definitions = [
                new("enabled", "Enabled", "Device", PlatformSettingValue.FromBoolean(true), true, 0, 0, []),
                new("integer", "Integer", "Device", PlatformSettingValue.FromInteger(3), true, 0, 10, []),
                new("number", "Number", "Device", PlatformSettingValue.FromNumber(1.5), true, 0, 10, []),
                new("choice", "Choice", "Device", PlatformSettingValue.FromChoice("linear"), true, 0, 0, ["linear", "nearest"]),
                new("text", "Text", "Device", PlatformSettingValue.FromText(""), true, 0, 0, [])
            ];
            EditorRuntimeSettingsDocument document = new() { InitialPresetId = "quality", Presets = [EditorRuntimeSettingsAuthoringService.CreatePreset(definitions, "quality", "Quality")] };
            using PlatformSettingsConfiguration configuration = EditorRuntimeSettingsAuthoringService.BuildConfiguration("device", document, definitions);
            using PlatformSettingsConfiguration decoded = PlatformSettingsSerializer.ReadBytes(PlatformSettingsSerializer.WriteBytes(configuration));
            Assert.Equal("linear", decoded.GetPreset("quality").Values.GetValue("choice").Text);
            Assert.Equal(1.5, decoded.GetPreset("quality").Values.GetValue("number").Number);
            Assert.Throws<InvalidOperationException>(() => EditorRuntimeSettingsAuthoringService.ParseValue(definitions[2], "1,5"));
            Assert.Equal("", EditorRuntimeSettingsAuthoringService.CreateEditorDefinitions([definitions[4]])[0].DefaultValue);
        }
    }
}
