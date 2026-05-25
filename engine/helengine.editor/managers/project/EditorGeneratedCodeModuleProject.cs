namespace helengine.editor {
    /// <summary>
    /// Describes one generated C# project for a single authored code module.
    /// </summary>
    public sealed class EditorGeneratedCodeModuleProject {
        /// <summary>
        /// Initializes one generated module project description.
        /// </summary>
        /// <param name="moduleId">Stable code-module id and CLR assembly name.</param>
        /// <param name="sourceFolderPath">Project-relative source folder path owned by the module.</param>
        /// <param name="dependencyModuleIds">Stable module ids referenced by the authored module manifest.</param>
        /// <param name="nestedSourceFolderPaths">Project-relative nested module folders excluded from this project's glob.</param>
        /// <param name="projectFilePath">Absolute generated project file path.</param>
        /// <param name="generatedGlobalUsingsFilePath">Absolute path to the generated global-usings file emitted for the project.</param>
        /// <param name="baseIntermediateOutputPath">Absolute base intermediate output path.</param>
        /// <param name="baseOutputPath">Absolute base output path.</param>
        /// <param name="targetFramework">Target framework emitted into the generated project file and output layout.</param>
        /// <param name="outputDirectoryPath">Absolute final output directory path for the module DLL.</param>
        /// <param name="projectGuid">Stable project GUID emitted into the solution.</param>
        /// <param name="moduleKind">Declares whether the generated project is runtime or editor-only.</param>
        public EditorGeneratedCodeModuleProject(
            string moduleId,
            string sourceFolderPath,
            IReadOnlyList<string> dependencyModuleIds,
            IReadOnlyList<string> nestedSourceFolderPaths,
            string projectFilePath,
            string generatedGlobalUsingsFilePath,
            string baseIntermediateOutputPath,
            string baseOutputPath,
            string targetFramework,
            string outputDirectoryPath,
            Guid projectGuid,
            EditorCodeModuleKind moduleKind) {
            if (string.IsNullOrWhiteSpace(moduleId)) {
                throw new ArgumentException("Module id must be provided.", nameof(moduleId));
            }
            if (string.IsNullOrWhiteSpace(sourceFolderPath)) {
                throw new ArgumentException("Source folder path must be provided.", nameof(sourceFolderPath));
            }
            if (nestedSourceFolderPaths == null) {
                throw new ArgumentNullException(nameof(nestedSourceFolderPaths));
            }
            if (dependencyModuleIds == null) {
                throw new ArgumentNullException(nameof(dependencyModuleIds));
            }
            if (string.IsNullOrWhiteSpace(projectFilePath)) {
                throw new ArgumentException("Project file path must be provided.", nameof(projectFilePath));
            }
            if (string.IsNullOrWhiteSpace(generatedGlobalUsingsFilePath)) {
                throw new ArgumentException("Generated global usings file path must be provided.", nameof(generatedGlobalUsingsFilePath));
            }
            if (string.IsNullOrWhiteSpace(baseIntermediateOutputPath)) {
                throw new ArgumentException("Base intermediate output path must be provided.", nameof(baseIntermediateOutputPath));
            }
            if (string.IsNullOrWhiteSpace(baseOutputPath)) {
                throw new ArgumentException("Base output path must be provided.", nameof(baseOutputPath));
            }
            if (string.IsNullOrWhiteSpace(targetFramework)) {
                throw new ArgumentException("Target framework must be provided.", nameof(targetFramework));
            }
            if (string.IsNullOrWhiteSpace(outputDirectoryPath)) {
                throw new ArgumentException("Output directory path must be provided.", nameof(outputDirectoryPath));
            }

            ModuleId = moduleId;
            SourceFolderPath = sourceFolderPath;
            DependencyModuleIds = dependencyModuleIds;
            NestedSourceFolderPaths = nestedSourceFolderPaths;
            ProjectFilePath = projectFilePath;
            GeneratedGlobalUsingsFilePath = generatedGlobalUsingsFilePath;
            BaseIntermediateOutputPath = baseIntermediateOutputPath;
            BaseOutputPath = baseOutputPath;
            TargetFramework = targetFramework;
            OutputDirectoryPath = outputDirectoryPath;
            ProjectGuid = projectGuid;
            ModuleKind = moduleKind;
        }

        /// <summary>
        /// Gets the stable code-module id and assembly name.
        /// </summary>
        public string ModuleId { get; }

        /// <summary>
        /// Gets the project-relative source folder path owned by the module.
        /// </summary>
        public string SourceFolderPath { get; }

        /// <summary>
        /// Gets the stable module ids referenced by the authored module manifest.
        /// </summary>
        public IReadOnlyList<string> DependencyModuleIds { get; }

        /// <summary>
        /// Gets the project-relative nested module folders excluded from this project's compile glob.
        /// </summary>
        public IReadOnlyList<string> NestedSourceFolderPaths { get; }

        /// <summary>
        /// Gets the absolute generated project file path.
        /// </summary>
        public string ProjectFilePath { get; }

        /// <summary>
        /// Gets the absolute path to the generated global-usings file emitted for the project.
        /// </summary>
        public string GeneratedGlobalUsingsFilePath { get; }

        /// <summary>
        /// Gets the absolute base intermediate output path for the generated project.
        /// </summary>
        public string BaseIntermediateOutputPath { get; }

        /// <summary>
        /// Gets the absolute base output path for the generated project.
        /// </summary>
        public string BaseOutputPath { get; }

        /// <summary>
        /// Gets the target framework emitted into the generated project file and output layout.
        /// </summary>
        public string TargetFramework { get; }

        /// <summary>
        /// Gets the absolute final output directory path for the generated module assembly.
        /// </summary>
        public string OutputDirectoryPath { get; }

        /// <summary>
        /// Gets the stable project GUID emitted into the generated solution.
        /// </summary>
        public Guid ProjectGuid { get; }

        /// <summary>
        /// Gets whether the generated project is runtime or editor-only.
        /// </summary>
        public EditorCodeModuleKind ModuleKind { get; }
    }
}
