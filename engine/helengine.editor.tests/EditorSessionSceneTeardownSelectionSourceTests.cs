namespace helengine.editor.tests;

/// <summary>
/// Verifies editor-session teardown clears session selection before disposing authored scene entities.
/// </summary>
public sealed class EditorSessionSceneTeardownSelectionSourceTests {
    /// <summary>
    /// Ensures close, scene-open replacement, and new-scene reset invalidate selection before authored roots are torn down.
    /// </summary>
    [Fact]
    public void Editor_session_source_clears_selection_before_scene_teardown() {
        string sourcePath = Path.Combine(TestSourceRepositoryLocator.ResolveHelEngineRootPath(), "engine", "helengine.editor", "EditorSession.cs");
        string source = File.ReadAllText(sourcePath).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains(
            "ledger.Register(ClearSceneSelectionBeforeTeardown, EditorSessionCleanupPhase.Reset);",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RegisterDetacher(ConstructionLedger, () => panel.AssetSelected -= HandleAssetSelected);",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RegisterDetacher(ConstructionLedger, () => ((PropertiesPanel)instance.Dockable).ImportSettingsApplyRequested -= HandleImportSettingsApplyRequested);",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RuntimeSceneOwnedAssetSet previousOwnedAssets = CurrentSceneOwnedAssets;\n                UntrackCurrentSceneFromSceneManager();\n                ClearSceneSelectionBeforeTeardown();\n                ClearUserSceneEntities(existingSceneEntities);",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "void ResetToNewScene() {\n            UntrackCurrentSceneFromSceneManager();\n            ClearSceneSelectionBeforeTeardown();\n            ClearUserSceneEntities();",
            source,
            StringComparison.Ordinal);
    }
}
