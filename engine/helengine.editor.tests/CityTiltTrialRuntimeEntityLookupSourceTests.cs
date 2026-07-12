namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored Tilt Trial runtime components resolve generated scene bindings without depending on editor-only entity names.
/// </summary>
public sealed class CityTiltTrialRuntimeEntityLookupSourceTests {
    /// <summary>
    /// Ensures the Tilt Trial runtime gameplay components no longer access the editor-only <c>Entity.Name</c> property.
    /// </summary>
    [Fact]
    public void City_tilt_trial_runtime_components_do_not_depend_on_entity_name() {
        string levelSelectSource = File.ReadAllText(@"C:\dev\helprojs\city\assets\codebase\game\TiltTrialLevelSelectComponent.cs");
        string sessionSource = File.ReadAllText(@"C:\dev\helprojs\city\assets\codebase\game\TiltTrialSessionComponent.cs");

        Assert.DoesNotContain("entity.Name", levelSelectSource, StringComparison.Ordinal);
        Assert.DoesNotContain("entity.Name", sessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("is TComponent typedComponent", sessionSource, StringComparison.Ordinal);
    }
}
