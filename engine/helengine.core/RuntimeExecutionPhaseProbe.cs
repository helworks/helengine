namespace helengine {
    /// <summary>
    /// Stores one low-overhead runtime phase marker so native hosts can report the last active managed execution boundary during fatal failures.
    /// </summary>
    public static class RuntimeExecutionPhaseProbe {
        /// <summary>
        /// Stores the phase identifier recorded before the core begins one update.
        /// </summary>
        public const int BeforeInputEarlyUpdatePhaseId = 10;

        /// <summary>
        /// Stores the phase identifier recorded after the early input update completes.
        /// </summary>
        public const int AfterInputEarlyUpdatePhaseId = 20;

        /// <summary>
        /// Stores the phase identifier recorded before the FPS tracker updates.
        /// </summary>
        public const int BeforeFpsRecordUpdateFramePhaseId = 30;

        /// <summary>
        /// Stores the phase identifier recorded after the FPS tracker updates.
        /// </summary>
        public const int AfterFpsRecordUpdateFramePhaseId = 40;

        /// <summary>
        /// Stores the phase identifier recorded before object-manager updates begin.
        /// </summary>
        public const int BeforeObjectManagerUpdatePhaseId = 50;

        /// <summary>
        /// Stores the phase identifier recorded after object-manager updates complete.
        /// </summary>
        public const int AfterObjectManagerUpdatePhaseId = 60;

        /// <summary>
        /// Stores the phase identifier recorded before the fixed-step physics pipeline begins.
        /// </summary>
        public const int BeforeUpdatePhysicsPhaseId = 70;

        /// <summary>
        /// Stores the phase identifier recorded after the fixed-step physics pipeline completes.
        /// </summary>
        public const int AfterUpdatePhysicsPhaseId = 80;

        /// <summary>
        /// Stores the phase identifier recorded before the main input update begins.
        /// </summary>
        public const int BeforeInputUpdatePhaseId = 90;

        /// <summary>
        /// Stores the phase identifier recorded after the main input update completes.
        /// </summary>
        public const int AfterInputUpdatePhaseId = 100;

        /// <summary>
        /// Stores the phase identifier recorded before pointer interaction updates begin.
        /// </summary>
        public const int BeforePointerInteractionSystemUpdatePhaseId = 110;

        /// <summary>
        /// Stores the phase identifier recorded after pointer interaction updates complete.
        /// </summary>
        public const int AfterPointerInteractionSystemUpdatePhaseId = 120;

        /// <summary>
        /// Stores the phase identifier recorded immediately before one BEPU timestep executes.
        /// </summary>
        public const int BeforeBepuTimestepPhaseId = 200;

        /// <summary>
        /// Stores the phase identifier recorded immediately after one BEPU timestep executes and before entity synchronization begins.
        /// </summary>
        public const int AfterBepuTimestepBeforeSyncPhaseId = 210;

        /// <summary>
        /// Stores the phase identifier recorded immediately after one BEPU timestep has synchronized active body state back to entities.
        /// </summary>
        public const int AfterBepuSyncPhaseId = 220;

        /// <summary>
        /// Gets the most recent runtime phase identifier recorded by the managed engine.
        /// </summary>
        public static int CurrentPhaseId { get; private set; }

        /// <summary>
        /// Records one new runtime phase identifier for native fatal diagnostics.
        /// </summary>
        /// <param name="phaseId">Stable phase identifier describing the current execution boundary.</param>
        public static void SetCurrentPhaseId(int phaseId) {
            CurrentPhaseId = phaseId;
        }
    }
}
