using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Models asynchronous content preparation followed by a live scene cache lease.</summary>
    internal sealed class SceneModuleTestContentOperation : IRuntimeSceneContentLoadOperation {
        /// <summary>Borrowed cache recorder.</summary>
        readonly SceneModuleTestContent Owner;
        /// <summary>Stable scene identity.</summary>
        readonly string SceneId;
        /// <summary>Required polls.</summary>
        readonly int Required;
        /// <summary>Observed nonblocking polls.</summary>
        public int Advances;
        /// <summary>Records cache release.</summary>
        public bool Disposed;
        /// <summary>Initializes a retained cache operation.</summary>
        /// <param name="owner">Cache recorder.</param>
        /// <param name="sceneId">Scene identity.</param>
        /// <param name="required">Required polls.</param>
        public SceneModuleTestContentOperation(SceneModuleTestContent owner, string sceneId, int required) { Owner = owner; SceneId = sceneId; Required = required; }
        /// <summary>Gets readiness for synchronous scene readers.</summary>
        public bool IsCompleted => Advances >= Required;
        /// <summary>Performs a single deterministic nonblocking step.</summary>
        public void Advance() {
            Advances++; Owner.Events.Add("advance:" + SceneId);
            if (Owner.FailAdvance) { throw new IOException("preload failed"); }
        }
        /// <summary>Records release of the cache after scene objects and callbacks are finished.</summary>
        public void Dispose() { if (Disposed) { return; } Disposed = true; Owner.Events.Add("release:" + SceneId); }
    }
}
