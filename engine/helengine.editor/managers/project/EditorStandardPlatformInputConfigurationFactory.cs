namespace helengine.editor {
    /// <summary>
    /// Converts persisted project platform input settings into the engine-owned standard platform input configuration used at runtime.
    /// </summary>
    internal sealed class EditorStandardPlatformInputConfigurationFactory {
        /// <summary>
        /// Builds one runtime standard platform input configuration from the supplied platform profile settings.
        /// </summary>
        /// <param name="platformSettings">Platform profile settings that own the persisted standard action bindings.</param>
        /// <returns>Runtime configuration translated from the supplied platform settings.</returns>
        public StandardPlatformInputConfiguration Create(EditorPlatformProfileSettingsDocument platformSettings) {
            if (platformSettings == null) {
                throw new ArgumentNullException(nameof(platformSettings));
            }

            List<StandardPlatformActionBinding> bindings = [];
            EditorStandardPlatformActionSettingsDocument standardActions = platformSettings.Input?.StandardActions;
            if (standardActions?.Accept != null) {
                bindings.Add(new StandardPlatformActionBinding(
                    StandardPlatformAction.Accept,
                    CreateControlId(standardActions.Accept)));
            }
            if (standardActions?.Return != null) {
                bindings.Add(new StandardPlatformActionBinding(
                    StandardPlatformAction.Return,
                    CreateControlId(standardActions.Return)));
            }

            return new StandardPlatformInputConfiguration(bindings);
        }

        /// <summary>
        /// Builds one runtime control identifier from the persisted editor settings document.
        /// </summary>
        /// <param name="controlSettings">Persisted physical control reference that should be converted.</param>
        /// <returns>Runtime input control identifier for the supplied persisted control reference.</returns>
        InputControlId CreateControlId(EditorInputControlSettingsDocument controlSettings) {
            if (controlSettings == null) {
                throw new ArgumentNullException(nameof(controlSettings));
            }

            return new InputControlId(
                controlSettings.DeviceKind,
                controlSettings.ControlKind,
                controlSettings.DeviceIndex,
                controlSettings.ControlIndex);
        }
    }
}
