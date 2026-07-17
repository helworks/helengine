using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Locks the object-manager deferred update queue to value semantics so panel toggles do not require one heap allocation per queued operation in native builds.
    /// </summary>
    public class ObjectManagerPendingUpdateOperationTests {
        /// <summary>
        /// Verifies deferred update operations stay as value types so native generated-core backends do not materialize each queued operation as a leaked heap object.
        /// </summary>
        [Fact]
    public void PendingUpdateOperation_WhenUsedByDeferredUpdateQueue_isValueType() {
        Assert.True(typeof(PendingUpdateOperation).IsValueType);
    }

    /// <summary>
    /// Verifies the hot update loop does not allocate a diagnostic stage string for every updateable on every frame.
    /// </summary>
    [Fact]
    public void Update_WhenRunningUpdateables_doesNotReportPerUpdateableStageString() {
        string source = File.ReadAllText(@"C:\dev\helworks\helengine\engine\helengine.core\managers\ObjectManager.cs");

        Assert.DoesNotContain("Core.Instance.ReportSceneTransitionStage($\"ObjectManagerUpdate:", source, StringComparison.Ordinal);
    }
}
}
