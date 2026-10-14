namespace helengine {
    /// <summary>Declares common runtime controls while allowing each platform to add its own setting contracts.</summary>
    public static class CommonPlatformRuntimeSettings {
        /// <summary>Enables authored shadows for the entire renderer.</summary>
        public const string ShadowsEnabled = "shadows-enabled";
        /// <summary>Limits selected visible lights; the renderer also respects its hardware limit.</summary>
        public const string MaximumVisibleLights = "maximum-visible-lights";
        /// <summary>Controls the cooperative scene preparation budget in milliseconds per frame.</summary>
        public const string SceneLoadingBudget = "scene-loading-budget-ms";
        /// <summary>Returns independent common definitions used to seed new user-authored presets.</summary>
        public static PlatformRuntimeSettingDefinition[] CreateDefinitions() => [
            new(ShadowsEnabled, "Shadows Enabled", "Rendering", PlatformSettingValue.FromBoolean(true), true, 0, 0, []),
            new(MaximumVisibleLights, "Maximum Visible Lights", "Rendering", PlatformSettingValue.FromInteger(32), true, 0, 256, []),
            new(SceneLoadingBudget, "Scene Loading Budget (ms)", "Runtime", PlatformSettingValue.FromNumber(8), true, 0.01, 1000, [])
        ];
        /// <summary>Reports whether the shared core knows how to apply one definition.</summary>
        public static bool IsCommon(string id) => id == ShadowsEnabled || id == MaximumVisibleLights || id == SceneLoadingBudget;
    }
}
