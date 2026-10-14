namespace helengine.editor {
    /// <summary>Contains the validated effective loading settings embedded into one native platform build.</summary>
    public sealed class EditorRuntimeLoadingSettings {
        /// <summary>Gets the cooperative per-frame budget in milliseconds after user and platform inheritance is resolved.</summary>
        public double SceneLoadingBudgetMilliseconds { get; }

        /// <summary>Gets the optional exact runtime scene id used for the loading screen.</summary>
        public string LoadingScreenSceneId { get; }

        /// <summary>Gets the selected platform's validated text-command reuse metadata budget; zero disables reuse.</summary>
        public int Render2DTextCommandReuseMemoryBudgetBytes { get; }

        /// <summary>Stores a previously validated effective budget and normalized loading-screen id.</summary>
        public EditorRuntimeLoadingSettings(double budgetMilliseconds, string loadingScreenSceneId, int textCommandReuseMemoryBudgetBytes = 0) {
            EditorRuntimeLoadingSettingsResolver.ValidateBudget(budgetMilliseconds, false);
            EditorRuntimeTextCommandReuseSettingsResolver.ValidateBudget(textCommandReuseMemoryBudgetBytes);
            SceneLoadingBudgetMilliseconds = budgetMilliseconds;
            LoadingScreenSceneId = loadingScreenSceneId?.Trim() ?? string.Empty;
            Render2DTextCommandReuseMemoryBudgetBytes = textCommandReuseMemoryBudgetBytes;
        }
    }
}
