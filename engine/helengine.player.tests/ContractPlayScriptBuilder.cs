using helengine.editor;

namespace helengine.player.tests {
    /// <summary>
    /// Minimal script build step for the editor-to-player argument contract test: every build succeeds and reports a
    /// fixed runtime module root.
    /// </summary>
    public sealed class ContractPlayScriptBuilder : IEditorPlayScriptBuilder {
        /// <summary>Runtime module root reported by every build.</summary>
        public const string RuntimeModulesRootPath = @"C:\project\cache\play\modules";

        /// <summary>
        /// Returns a successful build and reports the fixed runtime module root.
        /// </summary>
        /// <param name="platformId">Platform the session service asked to build scripts for; unused.</param>
        /// <param name="runtimeModulesRootPath">Receives <see cref="RuntimeModulesRootPath"/>.</param>
        /// <param name="compileSkipped">Receives false; every contract build reports a compile.</param>
        /// <returns>The successful build outcome.</returns>
        public EditorBuildExecutionResult Build(string platformId, out string runtimeModulesRootPath, out bool compileSkipped) {
            runtimeModulesRootPath = RuntimeModulesRootPath;
            compileSkipped = false;
            return EditorBuildExecutionResult.Success("Scripts built.");
        }
    }
}
