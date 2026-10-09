namespace helengine.timeline.runtime {
    /// <summary>
    /// Opt-in bootstrap of the timeline runtime module. Registering adds the cooked timeline content processor to the
    /// core's content manager so <see cref="TimelinePlayerComponent.TimelinePath"/> and
    /// <c>ContentManager.Load&lt;CookedTimelineAsset&gt;</c> can read <c>.hctimeline</c> files. Generated native cores
    /// call it through the assembly's <see cref="GeneratedRuntimeModuleManifestAttribute"/> only when cooked content uses
    /// <see cref="TimelinePlayerComponent"/>; a game that never references this module contains none of it.
    /// </summary>
    public static class TimelineRuntimeRegistration {
        /// <summary>
        /// Stable processor id of the cooked timeline content processor.
        /// </summary>
        public const string CookedTimelineProcessorId = "runtime.cooked-timeline-asset";

        /// <summary>
        /// Registers the module with one initialized core. Repeated calls are harmless.
        /// </summary>
        /// <param name="core">Core whose default content manager receives the processor.</param>
        public static void Register(Core core) {
            if (core == null) {
                throw new ArgumentNullException(nameof(core));
            }

            RegisterContent(core.GetContentManager());
        }

        /// <summary>
        /// Registers the cooked timeline processor with one content manager unless it is already registered.
        /// </summary>
        /// <param name="contentManager">Content manager that should load cooked timelines.</param>
        public static void RegisterContent(ContentManager contentManager) {
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            } else if (contentManager.IsProcessorRegistered(CookedTimelineProcessorId)) {
                return;
            }

            contentManager.RegisterProcessor(
                CookedTimelineProcessorId,
                new BinaryContentProcessor<CookedTimelineAsset>(CookedTimelineAssetReader.Deserialize),
                new string[] { CookedTimelineAsset.FileExtension });
        }
    }
}
