namespace helengine.animation.runtime {
    /// <summary>
    /// Opt-in bootstrap of the hierarchical animation runtime module. Registering adds the hierarchy animation clip
    /// content processor to the core's content manager so <see cref="HierarchyAnimationPlayerComponent.ClipPath"/> can
    /// read <c>.hanimclip</c> files. Generated native cores call it through the assembly's
    /// <see cref="GeneratedRuntimeModuleManifestAttribute"/> only when cooked content uses
    /// <see cref="HierarchyAnimationPlayerComponent"/>; a game that never references this module contains none of it.
    /// </summary>
    public static class AnimationRuntimeRegistration {
        /// <summary>
        /// Stable processor id of the hierarchy animation clip content processor.
        /// </summary>
        public const string ClipProcessorId = "runtime.hierarchy-animation-clip";

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
        /// Registers the clip processor with one content manager unless it is already registered.
        /// </summary>
        /// <param name="contentManager">Content manager that should load hierarchy animation clips.</param>
        public static void RegisterContent(ContentManager contentManager) {
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            } else if (contentManager.IsProcessorRegistered(ClipProcessorId)) {
                return;
            }

            contentManager.RegisterProcessor(
                ClipProcessorId,
                new BinaryContentProcessor<HierarchyAnimationClipAsset>(HierarchyAnimationClipReader.Deserialize),
                new string[] { HierarchyAnimationClipAsset.FileExtension });
        }
    }
}
