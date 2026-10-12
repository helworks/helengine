using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Records native module acquisitions and lets tests delay or reject registration.</summary>
    internal sealed class SceneModuleTestProvider : IRuntimeCodeModuleProvider {
        /// <summary>Ordered lifecycle evidence shared by module adapters and components.</summary>
        public readonly List<string> Events = new List<string>();
        /// <summary>Number of advances required by each simulated asynchronous load.</summary>
        public int AdvancesRequired = 1;
        /// <summary>Module whose registration should throw after installing its factory.</summary>
        public string FailingModule = string.Empty;
        /// <summary>Last pending operation, used to prove one poll per frame.</summary>
        public SceneModuleTestLoadOperation LastOperation;
        /// <summary>Starts a fresh owned adapter for the exact manifest entry.</summary>
        /// <param name="entry">Validated module declaration.</param>
        /// <returns>Owned recorded load operation.</returns>
        public IRuntimeCodeModuleLoadOperation BeginLoad(RuntimeCodeModuleManifestEntry entry) {
            Events.Add("begin:" + entry.ModuleId);
            LastOperation = new SceneModuleTestLoadOperation(this, entry.ModuleId, AdvancesRequired);
            return LastOperation;
        }
    }
}
