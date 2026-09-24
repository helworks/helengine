namespace helengine {
    /// <summary>
    /// Stable numeric identifiers for the Core phases exposed to native CPU profilers.
    /// </summary>
    public enum RuntimeCpuProfileStage : byte {
        EarlyInput = 1,
        FrameCounters = 2,
        ObjectManagerUpdate = 3,
        Audio = 4,
        Physics = 5,
        LateInput = 6,
        PointerInteraction = 7,
        FrameBoundary = 8,
        RenderManager3D = 9
    }
}