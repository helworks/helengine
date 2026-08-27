using System.Runtime.CompilerServices;

namespace helengine.editor.tests;

/// <summary>
/// Verifies workspace controllers release panel-owned resources at most once.
/// </summary>
public sealed class SessionWorkspacePanelControllerTests {
    /// <summary>
    /// Ensures repeated close and teardown paths do not dispose one panel resource graph twice.
    /// </summary>
    [Fact]
    public void Dispose_ReleasesPanelResourcesExactlyOnce() {
        DockableEntity dockable = (DockableEntity)RuntimeHelpers.GetUninitializedObject(typeof(DockableEntity));
        int disposeCount = 0;
        SessionWorkspacePanelController controller = new SessionWorkspacePanelController(
            dockable,
            SessionWorkspacePanelController.NoState,
            SessionWorkspacePanelController.NoRestore,
            () => disposeCount++);

        controller.Dispose();
        controller.Dispose();

        Assert.Equal(1, disposeCount);
    }
}
