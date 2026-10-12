using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Records component teardown while its overlay factory must still exist.</summary>
    internal sealed class SceneModuleTestComponent : Component {
        /// <summary>Borrowed owning core.</summary>
        readonly Core Owner;
        /// <summary>Borrowed lifecycle recorder.</summary>
        readonly SceneModuleTestProvider Provider;
        /// <summary>Exact owning overlay.</summary>
        readonly string ModuleId;
        /// <summary>Creates one observable scene component.</summary>
        /// <param name="core">Owning core.</param>
        /// <param name="provider">Recorder.</param>
        /// <param name="moduleId">Owning overlay.</param>
        public SceneModuleTestComponent(Core core, SceneModuleTestProvider provider, string moduleId) { Owner = core; Provider = provider; ModuleId = moduleId; }
        /// <summary>Records teardown before registry unregistration and native module disposal.</summary>
        public override void Dispose() {
            Assert.True(Owner.SceneRuntimeComponentRegistry.TryGet("module." + ModuleId, out _));
            Provider.Events.Add("component-dispose:" + ModuleId);
            base.Dispose();
        }
    }
}
