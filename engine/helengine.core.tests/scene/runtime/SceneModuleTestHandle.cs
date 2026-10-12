using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Models an overlay whose factory and components must die before its code allocation.</summary>
    internal sealed class SceneModuleTestHandle : IRuntimeCodeModule {
        /// <summary>Borrowed lifecycle recorder.</summary>
        readonly SceneModuleTestProvider Provider;
        /// <summary>Stable owning module identity.</summary>
        readonly string ModuleId;
        /// <summary>Borrowed registry used to verify unregister ordering.</summary>
        RuntimeComponentRegistry Registry;
        /// <summary>Creates a ready module handle.</summary>
        /// <param name="provider">Lifecycle recorder.</param>
        /// <param name="moduleId">Exact module identity.</param>
        public SceneModuleTestHandle(SceneModuleTestProvider provider, string moduleId) { Provider = provider; ModuleId = moduleId; }
        /// <summary>Registers the module factory before any scene component is decoded.</summary>
        /// <param name="core">Owning core.</param>
        /// <param name="registry">Factory owner.</param>
        public void Register(Core core, RuntimeComponentRegistry registry) {
            Registry = registry;
            Provider.Events.Add("register:" + ModuleId);
            registry.RegisterForModule(ModuleId, new SceneModuleTestDeserializer(core, Provider, ModuleId));
            if (Provider.FailingModule == ModuleId) { throw new InvalidOperationException("registration rejected"); }
        }
        /// <summary>Detaches callbacks while the factory is still callable.</summary>
        /// <param name="core">Owning core.</param>
        /// <param name="registry">Factory owner.</param>
        public void Unregister(Core core, RuntimeComponentRegistry registry) {
            Assert.True(registry.TryGet("module." + ModuleId, out _));
            Provider.Events.Add("unregister:" + ModuleId);
        }
        /// <summary>Checks that factories are gone before executable storage is freed.</summary>
        public void Dispose() {
            Assert.False(Registry.TryGet("module." + ModuleId, out _));
            Provider.Events.Add("dispose:" + ModuleId);
        }
    }
}
