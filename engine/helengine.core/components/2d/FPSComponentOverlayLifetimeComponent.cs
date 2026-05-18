namespace helengine {
    /// <summary>
    /// Notifies an owning <see cref="FPSComponent"/> when its generated overlay host subtree is being removed externally.
    /// </summary>
    public sealed class FPSComponentOverlayLifetimeComponent : Component {
        /// <summary>
        /// Owning FPS component whose cached overlay references must be released before native deletion completes.
        /// </summary>
        readonly FPSComponent Owner;

        /// <summary>
        /// Initializes a new overlay-lifetime sentinel for the supplied FPS component owner.
        /// </summary>
        /// <param name="owner">FPS component that owns the generated overlay hierarchy.</param>
        public FPSComponentOverlayLifetimeComponent(FPSComponent owner) {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>
        /// Releases the owner's cached overlay references when the overlay-host subtree is being removed.
        /// </summary>
        /// <param name="entity">Overlay-host entity being detached.</param>
        public override void ComponentRemoved(Entity entity) {
            Owner.ReleaseOverlayReferencesFromDisposedHierarchy();
            base.ComponentRemoved(entity);
        }
    }
}
