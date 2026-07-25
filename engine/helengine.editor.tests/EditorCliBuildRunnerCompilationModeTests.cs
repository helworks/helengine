namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the generated script graph selected by native platform build entry points.
    /// </summary>
    public sealed class EditorCliBuildRunnerCompilationModeTests {
        /// <summary>
        /// Ensures native platform cooks generate and load runtime production scripts without editor or test surfaces.
        /// </summary>
        [Fact]
        public void Build_WhenPreparingProjectScripts_UsesRuntimeOnlyCompilationMode() {
            EditorScriptCompilationMode compilationMode = EditorCliBuildRunner.ResolveProjectScriptCompilationMode();

            Assert.Equal(EditorScriptCompilationMode.RuntimeOnly, compilationMode);
        }
    }
}
