using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Simulates a nonblocking native IO operation with deterministic completion.</summary>
    internal sealed class SceneModuleTestLoadOperation : IRuntimeCodeModuleLoadOperation {
        /// <summary>Borrowed event recorder.</summary>
        readonly SceneModuleTestProvider Provider;
        /// <summary>Stable module identity.</summary>
        readonly string ModuleId;
        /// <summary>Required polls before code can be taken.</summary>
        readonly int Required;
        /// <summary>Observed polls.</summary>
        public int Advances;
        /// <summary>Observed cancellation or consumed-operation disposal.</summary>
        public bool Disposed;
        /// <summary>Creates an operation with no actual IO or code allocation.</summary>
        /// <param name="provider">Event recorder.</param>
        /// <param name="moduleId">Exact module identity.</param>
        /// <param name="required">Required polls.</param>
        public SceneModuleTestLoadOperation(SceneModuleTestProvider provider, string moduleId, int required) { Provider = provider; ModuleId = moduleId; Required = required; }
        /// <summary>Gets whether the simulated transfer has finished.</summary>
        public bool IsCompleted => Advances >= Required;
        /// <summary>Performs one IO poll.</summary>
        public void Advance() { Advances++; }
        /// <summary>Transfers one ready recorded code adapter.</summary>
        /// <returns>Owned module adapter.</returns>
        public IRuntimeCodeModule TakeModule() { Assert.True(IsCompleted); return new SceneModuleTestHandle(Provider, ModuleId); }
        /// <summary>Records disposal of the adapter after completion or cancellation.</summary>
        public void Dispose() { Disposed = true; }
    }
}
