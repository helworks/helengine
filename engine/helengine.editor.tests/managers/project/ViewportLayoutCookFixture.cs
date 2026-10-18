namespace helengine.editor.tests.managers.project {
    /// <summary>Supplies actual detached components to the pure layout cook pass without a running Core.</summary>
    internal sealed class ViewportLayoutCookFixture {
        /// <summary>Detached effective participants indexed by their unchanged source records.</summary>
        readonly Dictionary<SceneComponentAssetRecord, Component> Components = new Dictionary<SceneComponentAssetRecord, Component>();
        /// <summary>Counts any accidental attempt to resolve a gameplay record during layout processing.</summary>
        public int GameplayResolveCount { get; private set; }
        /// <summary>Creates one serialized entity with a detached real component.</summary>
        public SceneEntityAsset CreateEntity(string name, Component component) {
            IComponentPersistenceDescriptor descriptor = new ComponentPersistenceRegistry().GetDescriptor(component);
            SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, new EntityComponentSaveState());
            Components.Add(record, component);
            return new SceneEntityAsset { Name = name, Components = [record], LocalScale = float3.One, LocalOrientation = float4.Identity };
        }
        /// <summary>Resolves the real component while rejecting gameplay participation.</summary>
        public Component Resolve(SceneComponentAssetRecord record, IReadOnlyList<SceneOverrideScopeStepKind> levelOrder) {
            if (!Components.TryGetValue(record, out Component component)) {
                GameplayResolveCount++;
                throw new InvalidOperationException("Layout must not instantiate gameplay components.");
            }
            return component;
        }
    }
}
