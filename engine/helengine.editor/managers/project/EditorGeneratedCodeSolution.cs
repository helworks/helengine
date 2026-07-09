namespace helengine.editor {
    /// <summary>
    /// Describes the generated multi-project script solution for one game project.
    /// </summary>
    public sealed class EditorGeneratedCodeSolution {
        /// <summary>
        /// Initializes one generated code solution description.
        /// </summary>
        /// <param name="moduleProjects">Ordered generated module projects included in the solution.</param>
        /// <param name="testProjects">Ordered inferred generated test projects included in the solution.</param>
        public EditorGeneratedCodeSolution(
            IReadOnlyList<EditorGeneratedCodeModuleProject> moduleProjects,
            IReadOnlyList<EditorGeneratedCodeModuleProject> testProjects) {
            if (moduleProjects == null) {
                throw new ArgumentNullException(nameof(moduleProjects));
            }
            if (testProjects == null) {
                throw new ArgumentNullException(nameof(testProjects));
            }
            if (moduleProjects.Count == 0) {
                throw new InvalidOperationException("Generated code solutions must include at least one module project.");
            }

            ModuleProjects = moduleProjects;
            TestProjects = testProjects;
            Projects = [.. ModuleProjects, .. TestProjects];
        }

        /// <summary>
        /// Gets the ordered generated module projects included in the solution.
        /// </summary>
        public IReadOnlyList<EditorGeneratedCodeModuleProject> ModuleProjects { get; }

        /// <summary>
        /// Gets the ordered inferred generated test projects included in the solution.
        /// </summary>
        public IReadOnlyList<EditorGeneratedCodeModuleProject> TestProjects { get; }

        /// <summary>
        /// Gets the ordered generated production and test projects included in the solution.
        /// </summary>
        public IReadOnlyList<EditorGeneratedCodeModuleProject> Projects { get; }

        /// <summary>
        /// Gets the primary module project for callers that use the first generated module entry.
        /// </summary>
        public EditorGeneratedCodeModuleProject PrimaryModuleProject => ModuleProjects[0];
    }
}
