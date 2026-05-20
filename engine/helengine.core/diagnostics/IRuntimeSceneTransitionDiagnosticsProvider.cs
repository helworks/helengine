namespace helengine {
    /// <summary>
    /// Receives scene-manager transition stage notifications from hosts that need live diagnostics during deferred scene operation flushing.
    /// </summary>
    public interface IRuntimeSceneTransitionDiagnosticsProvider {
        /// <summary>
        /// Reports the latest scene-manager transition boundary while scene loading or unloading is still executing.
        /// </summary>
        /// <param name="stage">Short scene-manager stage label.</param>
        /// <param name="sceneId">Stable scene id associated with the current transition, or an empty value for aggregate stages.</param>
        /// <param name="loadedSceneCount">Number of loaded scene records at the time of the report.</param>
        /// <param name="pendingOperationCount">Number of deferred scene operations waiting at the time of the report.</param>
        void ReportSceneTransitionStage(string stage, string sceneId, int loadedSceneCount, int pendingOperationCount);
    }
}
