using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Creates a component whose teardown logs whether its module is still registered.</summary>
    internal sealed class SceneModuleTestDeserializer : IRuntimeComponentDeserializer {
        /// <summary>Borrowed owning core.</summary>
        readonly Core Owner;
        /// <summary>Borrowed lifecycle recorder.</summary>
        readonly SceneModuleTestProvider Provider;
        /// <summary>Exact overlay identity.</summary>
        readonly string ModuleId;
        /// <summary>Creates a factory implemented by the simulated module.</summary>
        /// <param name="core">Owning core.</param>
        /// <param name="provider">Recorder.</param>
        /// <param name="moduleId">Owning module.</param>
        public SceneModuleTestDeserializer(Core core, SceneModuleTestProvider provider, string moduleId) { Owner = core; Provider = provider; ModuleId = moduleId; }
        /// <summary>Gets the stable persisted component identity.</summary>
        public string ComponentTypeId => "module." + ModuleId;
        /// <summary>Constructs the component only after code and factory registration.</summary>
        /// <param name="record">Persisted component payload.</param>
        /// <param name="referenceResolver">Borrowed asset resolver.</param>
        /// <param name="fixups">Borrowed reference fixups.</param>
        /// <returns>Owned component.</returns>
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver, RuntimeSceneReferenceFixups fixups = null) {
            Provider.Events.Add("create:" + ModuleId);
            return new SceneModuleTestComponent(Owner, Provider, ModuleId);
        }
    }
}
