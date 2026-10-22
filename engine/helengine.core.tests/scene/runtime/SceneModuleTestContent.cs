using System.IO;
using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Refuses reads outside completed preparation scopes and records byte release independently of scene unload.</summary>
    internal sealed class SceneModuleTestContent : IRuntimeSceneContentProvider, IContentStreamSource {
        /// <summary>Lifecycle evidence for cache preparation and release.</summary>
        public readonly List<string> Events = new List<string>();
        /// <summary>Number of nonblocking polls required.</summary>
        public int AdvancesRequired = 2;
        /// <summary>Injects failure at a nonblocking preload step to exercise scene cancellation.</summary>
        public bool FailAdvance;
        /// <summary>Retained cache leases keyed by scene identity.</summary>
        public readonly Dictionary<string, SceneModuleTestContentOperation> Operations = new Dictionary<string, SceneModuleTestContentOperation>();
        /// <summary>Starts exact catalog-based content preloading.</summary>
        /// <param name="entry">Packaged scene with declared dependencies.</param>
        /// <returns>Owned lease retained after completion.</returns>
        public IRuntimeSceneContentLoadOperation BeginPreloadScene(RuntimeSceneCatalogEntry entry) {
            Events.Add("preload:" + entry.SceneId);
            SceneModuleTestContentOperation operation = new SceneModuleTestContentOperation(this, entry.SceneId, AdvancesRequired);
            if (Operations.TryGetValue(entry.SceneId, out SceneModuleTestContentOperation previous)) { Assert.True(previous.Disposed); }
            Operations[entry.SceneId] = operation;
            return operation;
        }
        /// <summary>Allows synchronous readers only when the corresponding cache remains retained.</summary>
        /// <param name="assetPath">Cooked path.</param>
        /// <returns>Owned sentinel stream handled by the scene processor.</returns>
        public Stream OpenRead(string assetPath) {
            string id = Path.GetFileNameWithoutExtension(assetPath);
            Assert.True(Operations[id].IsCompleted);
            Assert.False(Operations[id].Disposed);
            Events.Add("read:" + id);
            return new MemoryStream();
        }
    }
}
