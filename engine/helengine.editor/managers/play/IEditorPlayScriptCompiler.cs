namespace helengine.editor {
    /// <summary>
    /// Compiles a project's runtime script modules for editor Play and names where they are published. The
    /// <see cref="EditorPlayScriptBuilder"/> decides whether a compile is needed; this seam performs it, so the
    /// skip/rebuild decision can be verified without running <c>dotnet build</c>.
    /// </summary>
    public interface IEditorPlayScriptCompiler {
        /// <summary>
        /// Resolves the per-platform Play compiler-output root; everything a compile publishes lives beneath it.
        /// </summary>
        /// <param name="platformId">Platform whose Play output root is resolved.</param>
        /// <returns>Absolute Play output root for the platform.</returns>
        string ResolvePlayOutputRootPath(string platformId);

        /// <summary>
        /// Resolves the directory laid out as <c>&lt;root&gt;/&lt;moduleId&gt;/&lt;moduleId&gt;.dll</c> that the player
        /// loads runtime modules from.
        /// </summary>
        /// <param name="platformId">Platform whose runtime module root is resolved.</param>
        /// <returns>Absolute runtime module root inside the Play output root.</returns>
        string ResolveRuntimeModulesRootPath(string platformId);

        /// <summary>
        /// Compiles the project's runtime modules with the platform's and environment's gameplay symbols.
        /// </summary>
        /// <param name="platformId">Platform whose gameplay symbols are defined.</param>
        /// <param name="environmentId">Selected project environment, or empty when none is selected.</param>
        /// <param name="runtimeModulesRootPath">Receives the runtime module root.</param>
        /// <returns>Compile outcome.</returns>
        EditorBuildExecutionResult Build(string platformId, string environmentId, out string runtimeModulesRootPath);
    }
}
