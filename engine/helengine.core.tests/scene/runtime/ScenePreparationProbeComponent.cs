using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Observes initialization and readiness without allowing the loader to warm gameplay or input updates.</summary>
    internal sealed class ScenePreparationProbeComponent : UpdateComponent {
        /// <summary>Gets the number of initialization callbacks received.</summary>
        public int InitializedCount { get; private set; }
        /// <summary>Gets the number of readiness callbacks received.</summary>
        public int PreparedCount { get; private set; }
        /// <summary>Gets whether the owner remained inactive throughout both callbacks.</summary>
        public bool StayedSuppressed { get; private set; } = true;
        /// <summary>Gets whether the loader incorrectly invoked a gameplay update.</summary>
        public bool Updated { get; private set; }
        /// <summary>Gets or sets whether readiness should fail to test publication on error.</summary>
        public bool FailPreparation { get; set; }
        /// <summary>Gets or sets whether initialization removes this component from its owner.</summary>
        public bool RemoveDuringInitialization { get; set; }
        /// <summary>Gets the text width already resolved when readiness runs.</summary>
        public int PreparedTextWidth { get; private set; }
        /// <summary>Records initialization while the unfinished target remains inactive.</summary>
        public override void ComponentInitialized(Entity entity) {
            InitializedCount++;
            StayedSuppressed = StayedSuppressed && !entity.IsHierarchyEnabled;
            if (RemoveDuringInitialization) { entity.RemoveComponent(this); }
        }
        /// <summary>Records resolved layout and optionally fails before publication.</summary>
        public override void ScenePrepared(Entity entity) {
            PreparedCount++;
            StayedSuppressed = StayedSuppressed && !entity.IsHierarchyEnabled;
            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is TextComponent text) {
                    PreparedTextWidth = text.Size.X;
                }
            }
            if (FailPreparation) { throw new InvalidOperationException("preparation failure"); }
        }
        /// <summary>Records accidental gameplay/input warmup, which must never be used for readiness.</summary>
        public override void Update() { Updated = true; }
    }
}
