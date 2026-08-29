using helengine.editor;
using Xunit;

namespace helengine.editor.tests.serialization;

/// <summary>
/// Locks the save boundary to the project authoring session.  Save services
/// must not grow a second resolver/cache/writer graph behind the host's back.
/// </summary>
public sealed class AuthoringSessionSaveBoundaryTests {
    [Fact]
    public void SceneSaveService_ConsumesProjectAuthoringSession() {
        Assert.Contains(
            typeof(SceneSaveService).GetConstructors(),
            constructor => constructor.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(IEditorProjectAuthoringSession)));
    }

    [Fact]
    public void BlueprintSaveService_ConsumesProjectAuthoringSession() {
        Assert.Contains(
            typeof(BlueprintSaveService).GetConstructors(),
            constructor => constructor.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(IEditorProjectAuthoringSession)));
    }
}
