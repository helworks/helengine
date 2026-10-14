using System.Globalization;
using System.Text.Json;
using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>Edits independent platform presets and converts their explicit values into the portable runtime catalog.</summary>
    public static class EditorRuntimeSettingsAuthoringService {
        /// <summary>Creates the first complete preset for older projects; existing presets are never filled through inheritance.</summary>
        public static void Initialize(EditorPlatformProfileSettingsDocument platform, PlatformRuntimeSettingDefinition[] definitions) {
            if (platform == null || definitions == null) { throw new ArgumentNullException(nameof(platform)); }
            platform.RuntimeSettings ??= new(); ValidateStructure(platform.RuntimeSettings);
            if (platform.RuntimeSettings.Presets.Count == 0) {
                EditorRuntimePresetDocument preset = CreatePreset(definitions, "default", "Default");
                if (platform.Graphics?.SelectedOptionValues != null
                    && platform.Graphics.SelectedOptionValues.TryGetValue(CommonPlatformRuntimeSettings.ShadowsEnabled, out string shadows)
                    && preset.Values.ContainsKey(CommonPlatformRuntimeSettings.ShadowsEnabled)) {
                    preset.Values[CommonPlatformRuntimeSettings.ShadowsEnabled] = shadows;
                }
                platform.RuntimeSettings.Presets.Add(preset); platform.RuntimeSettings.InitialPresetId = preset.Id;
            }
        }
        /// <summary>Creates a complete standalone preset by copying each setting's creation default exactly once.</summary>
        public static EditorRuntimePresetDocument CreatePreset(PlatformRuntimeSettingDefinition[] definitions, string id, string name) {
            EditorRuntimePresetDocument preset = new() { Id = id, Name = name };
            foreach (PlatformRuntimeSettingDefinition definition in definitions) { preset.Values.Add(definition.Id, FormatValue(definition.DefaultValue)); }
            return preset;
        }
        /// <summary>Clones the serialized document so canceling edits does not change the project or other presets.</summary>
        public static EditorRuntimeSettingsDocument Clone(EditorRuntimeSettingsDocument source) {
            EditorRuntimeSettingsDocument result = new();
            if (source == null) { return result; }
            result.InitialPresetId = source.InitialPresetId;
            foreach (EditorRuntimePresetDocument preset in source.Presets) {
                result.Presets.Add(new EditorRuntimePresetDocument { Id = preset.Id, Name = preset.Name, Values = new(preset.Values) });
            }
            return result;
        }
        /// <summary>Rejects malformed identifiers and initial selections without requiring platform metadata during persistence.</summary>
        public static void ValidateStructure(EditorRuntimeSettingsDocument document) {
            if (document == null || document.Presets == null || document.InitialPresetId == null) { throw new InvalidOperationException("Runtime settings must contain a preset list and initial selection."); }
            HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (EditorRuntimePresetDocument preset in document.Presets) {
                if (preset == null || string.IsNullOrWhiteSpace(preset.Id) || string.IsNullOrWhiteSpace(preset.Name) || preset.Values == null || !ids.Add(preset.Id)) {
                    throw new InvalidOperationException("Runtime preset identifiers must be nonempty and unique, with names and explicit values.");
                }
            }
            if (document.Presets.Count > 0 && !ids.Contains(document.InitialPresetId)) { throw new InvalidOperationException("The initial runtime preset must exist on this platform."); }
            if (document.Presets.Count == 0 && document.InitialPresetId.Length != 0) { throw new InvalidOperationException("An empty preset list cannot select an initial preset."); }
        }
        /// <summary>Converts complete editor values into a strict runtime catalog; missing settings and unknown values are errors.</summary>
        public static PlatformSettingsConfiguration BuildConfiguration(string platformId, EditorRuntimeSettingsDocument document,
            PlatformRuntimeSettingDefinition[] definitions) {
            ValidateStructure(document);
            PlatformSettingsPreset[] presets = new PlatformSettingsPreset[document.Presets.Count];
            for (int index = 0; index < presets.Length; index++) {
                EditorRuntimePresetDocument preset = document.Presets[index];
                if (preset.Values.Count != definitions.Length) { throw new InvalidOperationException("Preset '" + preset.Name + "' must contain exactly every platform setting."); }
                Dictionary<string, PlatformSettingValue> values = new();
                foreach (PlatformRuntimeSettingDefinition definition in definitions) {
                    if (!preset.Values.TryGetValue(definition.Id, out string text)) { throw new InvalidOperationException("Preset '" + preset.Name + "' is missing setting '" + definition.Id + "'."); }
                    PlatformSettingValue value = ParseValue(definition, text); definition.Validate(value); values.Add(definition.Id, value);
                }
                presets[index] = new(preset.Id, preset.Name, values);
            }
            return new PlatformSettingsConfiguration(platformId, definitions, presets, document.InitialPresetId);
        }
        /// <summary>Loads exactly the requested platform file, preserving authored errors instead of substituting unrelated defaults.</summary>
        public static PlatformSettingsConfiguration Load(string projectRoot, PlatformDefinition definition) {
            string path = Path.Combine(projectRoot, "settings", "platform." + definition.PlatformId + ".json");
            EditorPlatformProfileSettingsDocument document = File.Exists(path)
                ? JsonSerializer.Deserialize<EditorPlatformProfileSettingsDocument>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                    ?? throw new InvalidOperationException("Platform settings cannot be null.")
                : new EditorPlatformProfileSettingsDocument { PlatformId = definition.PlatformId };
            if (!string.Equals(document.PlatformId, definition.PlatformId, StringComparison.OrdinalIgnoreCase)) { throw new InvalidOperationException("Runtime presets belong to another platform."); }
            Initialize(document, definition.RuntimeSettings);
            return BuildConfiguration(definition.PlatformId, document.RuntimeSettings, definition.RuntimeSettings);
        }
        /// <summary>Maps typed runtime definitions to the existing generic editor controls, retaining category labels.</summary>
        public static PlatformSettingDefinition[] CreateEditorDefinitions(PlatformRuntimeSettingDefinition[] definitions) {
            return definitions.Select(definition => new PlatformSettingDefinition(definition.Id, definition.DisplayName,
                definition.Kind == PlatformRuntimeSettingKind.Boolean ? PlatformSettingKind.Boolean
                    : definition.Kind == PlatformRuntimeSettingKind.Choice ? PlatformSettingKind.Choice : PlatformSettingKind.Text,
                FormatValue(definition.DefaultValue), definition.Kind != PlatformRuntimeSettingKind.Text, definition.GetChoices())).ToArray();
        }
        /// <summary>Formats portable values for authoring with invariant numeric text.</summary>
        public static string FormatValue(PlatformSettingValue value) {
            if (value.Kind == PlatformRuntimeSettingKind.Boolean) { return value.Boolean ? "true" : "false"; }
            if (value.Kind == PlatformRuntimeSettingKind.Integer || value.Kind == PlatformRuntimeSettingKind.Number) { return value.Number.ToString("R", CultureInfo.InvariantCulture); }
            return value.Text;
        }
        /// <summary>Parses editor text into the exact declared kind, rejecting invalid inputs before export.</summary>
        public static PlatformSettingValue ParseValue(PlatformRuntimeSettingDefinition definition, string text) {
            if (definition.Kind == PlatformRuntimeSettingKind.Boolean && bool.TryParse(text, out bool boolean)) { return PlatformSettingValue.FromBoolean(boolean); }
            if (definition.Kind == PlatformRuntimeSettingKind.Integer && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer)) { return PlatformSettingValue.FromInteger(integer); }
            if (definition.Kind == PlatformRuntimeSettingKind.Number && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) { return PlatformSettingValue.FromNumber(number); }
            if (definition.Kind == PlatformRuntimeSettingKind.Choice) { return PlatformSettingValue.FromChoice(text); }
            if (definition.Kind == PlatformRuntimeSettingKind.Text) { return PlatformSettingValue.FromText(text); }
            throw new InvalidOperationException("Setting '" + definition.Id + "' has an invalid " + definition.Kind + " value.");
        }
    }
}
