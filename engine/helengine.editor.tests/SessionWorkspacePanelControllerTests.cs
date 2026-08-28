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
    public void Dispose_WhenCleanupFailsOnce_RetriesOnlyTheFailedCleanup() {
        DockableEntity dockable = (DockableEntity)RuntimeHelpers.GetUninitializedObject(typeof(DockableEntity));
        int disposeCount = 0;
        bool failOnce = true;
        SessionWorkspacePanelController controller = new SessionWorkspacePanelController(
            dockable,
            SessionWorkspacePanelController.NoState,
            SessionWorkspacePanelController.NoRestore,
            () => {
                disposeCount++;
                if (failOnce) {
                    failOnce = false;
                    throw new InvalidOperationException("cleanup failed once");
                }
            });

        Assert.Throws<InvalidOperationException>(() => controller.Dispose());
        controller.Dispose();
        controller.Dispose();

        Assert.Equal(2, disposeCount);
    }
}
