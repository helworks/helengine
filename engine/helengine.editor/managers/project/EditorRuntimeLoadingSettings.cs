namespace helengine.editor {
    /// <summary>Contains the validated effective loading settings embedded into one native platform build.</summary>
    public sealed class EditorRuntimeLoadingSettings {
        /// <summary>Gets the cooperative per-frame budget in milliseconds after user and platform inheritance is resolved.</summary>
        public double SceneLoadingBudgetMilliseconds { get; }

        /// <summary>Gets the optional exact runtime scene id used for the loading screen.</summary>
        public string LoadingScreenSceneId { get; }

        /// <summary>Stores a previously validated effective budget and normalized loading-screen id.</summary>
        public EditorRuntimeLoadingSettings(double budgetMilliseconds, string loadingScreenSceneId) {
            EditorRuntimeLoadingSettingsResolver.ValidateBudget(budgetMilliseconds, false);
            SceneLoadingBudgetMilliseconds = budgetMilliseconds;
            LoadingScreenSceneId = loadingScreenSceneId?.Trim() ?? string.Empty;
        }
    }
}
