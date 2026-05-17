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
    }
}
