using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Generates one C# solution for the current game project and opens it in the configured IDE.
    /// </summary>
    public sealed class EditorGameSolutionService {
        /// <summary>
        /// Project folder name used by the generated C# project to enumerate game scripts.
        /// </summary>
        const string AssetsFolderName = "assets";

        /// <summary>
        /// Solution file extension used by the generated IDE workspace.
        /// </summary>
        const string SolutionFileExtension = ".sln";

        /// <summary>
        /// SDK-style project type GUID used by Visual Studio solutions for C# projects.
        /// </summary>
        const string CSharpProjectTypeGuid = "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC";

        /// <summary>
        /// Default framework used by the generated game project.
        /// </summary>
        const string TargetFrameworkValue = "net9.0";

        /// <summary>
        /// Legacy intermediate folder that should not remain under the assets project root.
        /// </summary>
        const string LegacyIntermediateFolderName = "obj";

        /// <summary>
        /// Legacy binary folder that should not remain under the assets project root.
        /// </summary>
        const string LegacyBinaryFolderName = "bin";

        /// <summary>
        /// Absolute path to the game project root.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Sanitized identifier used for file names and assembly metadata.
        /// </summary>
        readonly string ProjectIdentifier;

        /// <summary>
        /// Generated solution file path.
        /// </summary>
        readonly string SolutionFilePath;

        /// <summary>
        /// IDE launcher used after generating the solution files.
        /// </summary>
        readonly IEditorIdeLauncher IdeLauncher;

        /// <summary>
        /// Detector used to skip reopening a solution that is already active in the IDE.
        /// </summary>
        readonly IEditorIdeSolutionDetector SolutionDetector;

        /// <summary>
        /// Authored code-module manifest service used to discover generated script projects.
        /// </summary>
        readonly EditorCodeModuleManifestService CodeModuleManifestService;

        /// <summary>
        /// Builder used to convert authored modules into generated code project descriptions.
        /// </summary>
        readonly EditorGeneratedCodeSolutionBuilder GeneratedCodeSolutionBuilder;

        /// <summary>
        /// Cached generated code solution description from the most recent generation pass.
        /// </summary>
        EditorGeneratedCodeSolution GeneratedCodeSolutionValue;

        /// <summary>
        /// Optional explicit output root used by generated module projects for isolated headless builds.
        /// </summary>
        readonly string GeneratedOutputRootPath;

        /// <summary>
        /// Initializes one solution generator for the supplied game project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative game project root path.</param>
        /// <param name="projectName">Display name of the game project.</param>
        /// <param name="ideLauncher">Launcher used to open the generated solution.</param>
        public EditorGameSolutionService(string projectRootPath, string projectName, IEditorIdeLauncher ideLauncher)
            : this(projectRootPath, projectName, ideLauncher, new EditorVisualStudioLauncher(), string.Empty) {
        }

        /// <summary>
        /// Initializes one solution generator for the supplied game project root and explicit generated output root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative game project root path.</param>
        /// <param name="projectName">Display name of the game project.</param>
        /// <param name="ideLauncher">Launcher used to open the generated solution.</param>
        /// <param name="generatedOutputRootPath">Explicit generated output root used by the generated projects.</param>
        public EditorGameSolutionService(string projectRootPath, string projectName, IEditorIdeLauncher ideLauncher, string generatedOutputRootPath)
            : this(projectRootPath, projectName, ideLauncher, new EditorVisualStudioLauncher(), generatedOutputRootPath) {
        }

        /// <summary>
        /// Initializes one solution generator for the supplied game project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative game project root path.</param>
        /// <param name="projectName">Display name of the game project.</param>
        /// <param name="ideLauncher">Launcher used to open the generated solution.</param>
        /// <param name="solutionDetector">Detector used to skip reopening an already-open solution.</param>
        public EditorGameSolutionService(string projectRootPath, string projectName, IEditorIdeLauncher ideLauncher, IEditorIdeSolutionDetector solutionDetector) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (string.IsNullOrWhiteSpace(projectName)) {
                throw new ArgumentException("Project name must be provided.", nameof(projectName));
            }
            if (ideLauncher == null) {
                throw new ArgumentNullException(nameof(ideLauncher));
            }
            if (solutionDetector == null) {
                throw new ArgumentNullException(nameof(solutionDetector));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            ProjectIdentifier = SanitizeIdentifier(projectName);
            if (string.IsNullOrWhiteSpace(ProjectIdentifier)) {
                ProjectIdentifier = "Game";
            }

            SolutionFilePath = Path.Combine(ProjectRootPath, ProjectIdentifier + SolutionFileExtension);
            IdeLauncher = ideLauncher;
            SolutionDetector = solutionDetector;
            CodeModuleManifestService = new EditorCodeModuleManifestService(ProjectRootPath);
            GeneratedCodeSolutionBuilder = new EditorGeneratedCodeSolutionBuilder();
            GeneratedOutputRootPath = string.Empty;
        }

        /// <summary>
        /// Initializes one solution generator for the supplied game project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative game project root path.</param>
        /// <param name="projectName">Display name of the game project.</param>
        /// <param name="ideLauncher">Launcher used to open the generated solution.</param>
        /// <param name="solutionDetector">Detector used to skip reopening an already-open solution.</param>
        /// <param name="generatedOutputRootPath">Explicit generated output root used by the generated projects.</param>
        public EditorGameSolutionService(
            string projectRootPath,
            string projectName,
            IEditorIdeLauncher ideLauncher,
            IEditorIdeSolutionDetector solutionDetector,
            string generatedOutputRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (string.IsNullOrWhiteSpace(projectName)) {
                throw new ArgumentException("Project name must be provided.", nameof(projectName));
            }
            if (ideLauncher == null) {
                throw new ArgumentNullException(nameof(ideLauncher));
            }
            if (solutionDetector == null) {
                throw new ArgumentNullException(nameof(solutionDetector));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            ProjectIdentifier = SanitizeIdentifier(projectName);
            if (string.IsNullOrWhiteSpace(ProjectIdentifier)) {
                ProjectIdentifier = "Game";
            }

            SolutionFilePath = Path.Combine(ProjectRootPath, ProjectIdentifier + SolutionFileExtension);
            IdeLauncher = ideLauncher;
            SolutionDetector = solutionDetector;
            CodeModuleManifestService = new EditorCodeModuleManifestService(ProjectRootPath);
            GeneratedCodeSolutionBuilder = new EditorGeneratedCodeSolutionBuilder();
            GeneratedOutputRootPath = string.IsNullOrWhiteSpace(generatedOutputRootPath)
                ? string.Empty
                : Path.GetFullPath(generatedOutputRootPath);
        }

        /// <summary>
        /// Gets the absolute path to the generated project file.
        /// </summary>
        public string GeneratedProjectFilePath => GetPrimaryModuleProject().ProjectFilePath;

        /// <summary>
        /// Gets the absolute path to the generated solution file.
        /// </summary>
        public string GeneratedSolutionFilePath => SolutionFilePath;

        /// <summary>
        /// Gets the output directory where the generated scripting project writes compiled binaries.
        /// </summary>
        public string GeneratedOutputDirectoryPath {
            get {
                return GetPrimaryModuleProject().OutputDirectoryPath;
            }
        }

        /// <summary>
        /// Gets the absolute path to the generated scripting assembly.
        /// </summary>
        public string GeneratedOutputAssemblyPath {
            get {
                EditorGeneratedCodeModuleProject primaryModuleProject = GetPrimaryModuleProject();
                return Path.Combine(primaryModuleProject.OutputDirectoryPath, primaryModuleProject.ModuleId + ".dll");
            }
        }

        /// <summary>
        /// Gets the ordered generated module projects included in the current solution.
        /// </summary>
        public IReadOnlyList<EditorGeneratedCodeModuleProject> GeneratedModuleProjects {
            get {
                if (GeneratedCodeSolutionValue == null) {
                    GeneratedCodeSolutionValue = BuildGeneratedCodeSolution();
                }

                return GeneratedCodeSolutionValue.ModuleProjects;
            }
        }

        /// <summary>
        /// Generates the solution and project files, overwriting any older copies in place.
        /// </summary>
        /// <returns>Absolute path to the generated solution file.</returns>
        public string GenerateSolutionFiles() {
            Directory.CreateDirectory(ProjectRootPath);
            GeneratedCodeSolutionValue = BuildGeneratedCodeSolution();
            for (int index = 0; index < GeneratedCodeSolutionValue.ModuleProjects.Count; index++) {
                EditorGeneratedCodeModuleProject moduleProject = GeneratedCodeSolutionValue.ModuleProjects[index];
                string projectDirectoryPath = Path.GetDirectoryName(moduleProject.ProjectFilePath);
                if (!string.IsNullOrWhiteSpace(projectDirectoryPath)) {
                    Directory.CreateDirectory(projectDirectoryPath);
                }

                File.WriteAllText(moduleProject.GeneratedGlobalUsingsFilePath, BuildGlobalUsingsFileContents(moduleProject));
                File.WriteAllText(moduleProject.ProjectFilePath, BuildProjectFileContents(moduleProject));
            }

            File.WriteAllText(SolutionFilePath, BuildSolutionFileContents(GeneratedCodeSolutionValue));
            return SolutionFilePath;
        }

        /// <summary>
        /// Generates the solution files and opens the solution in the configured IDE.
        /// </summary>
        public void OpenSolutionInIde() {
            string solutionPath = GenerateSolutionFiles();
            if (SolutionDetector.IsSolutionAlreadyOpen(solutionPath)) {
                return;
            }

            IdeLauncher.OpenSolution(solutionPath);
        }

        /// <summary>
        /// Builds the SDK-style project file for the game's C# script sources.
        /// </summary>
        /// <returns>Project file contents.</returns>
        string BuildProjectFileContents(EditorGeneratedCodeModuleProject moduleProject) {
            if (moduleProject == null) {
                throw new ArgumentNullException(nameof(moduleProject));
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
            builder.AppendLine("  <PropertyGroup>");
            builder.AppendLine("    <TargetFramework>" + moduleProject.TargetFramework + "</TargetFramework>");
            builder.AppendLine("    <OutputType>Library</OutputType>");
            builder.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
            builder.AppendLine("    <Nullable>disable</Nullable>");
            builder.AppendLine("    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>");
            builder.AppendLine("    <EnableDefaultNoneItems>false</EnableDefaultNoneItems>");
            builder.AppendLine("    <EnableDefaultContentItems>false</EnableDefaultContentItems>");
            builder.AppendLine("    <EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>");
            builder.AppendLine("    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>");
            builder.AppendLine("    <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>");
            builder.AppendLine("    <BaseIntermediateOutputPath>" + EscapeXml(moduleProject.BaseIntermediateOutputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar) + "</BaseIntermediateOutputPath>");
            builder.AppendLine("    <BaseOutputPath>" + EscapeXml(moduleProject.BaseOutputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar) + "</BaseOutputPath>");
            builder.AppendLine("    <AssemblyName>" + EscapeXml(moduleProject.ModuleId) + "</AssemblyName>");
            builder.AppendLine("    <RootNamespace>" + EscapeXml(moduleProject.ModuleId) + "</RootNamespace>");
            builder.AppendLine("  </PropertyGroup>");
            AppendProjectReferences(builder, moduleProject);
            AppendAssemblyReferences(builder, moduleProject);
            builder.AppendLine("  <ItemGroup>");
            builder.AppendLine("    <Compile Include=\"" + EscapeXml(moduleProject.GeneratedGlobalUsingsFilePath) + "\" />");
            builder.AppendLine("    <Compile Include=\"" + EscapeXml(Path.Combine(ResolveProjectPath(moduleProject.SourceFolderPath), "**", "*.cs")) + "\" />");
            for (int index = 0; index < moduleProject.NestedSourceFolderPaths.Count; index++) {
                builder.AppendLine("    <Compile Remove=\"" + EscapeXml(Path.Combine(ResolveProjectPath(moduleProject.NestedSourceFolderPaths[index]), "**", "*.cs")) + "\" />");
            }
            builder.AppendLine("  </ItemGroup>");
            builder.AppendLine("</Project>");
            return builder.ToString();
        }

        /// <summary>
        /// Builds the Visual Studio solution file for the generated project.
        /// </summary>
        /// <returns>Solution file contents.</returns>
        string BuildSolutionFileContents(EditorGeneratedCodeSolution generatedCodeSolution) {
            if (generatedCodeSolution == null) {
                throw new ArgumentNullException(nameof(generatedCodeSolution));
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
            builder.AppendLine("# Visual Studio Version 17");
            builder.AppendLine("VisualStudioVersion = 17.0.31903.59");
            builder.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");
            for (int index = 0; index < generatedCodeSolution.ModuleProjects.Count; index++) {
                EditorGeneratedCodeModuleProject moduleProject = generatedCodeSolution.ModuleProjects[index];
                string relativeProjectFileName = Path.GetRelativePath(ProjectRootPath, moduleProject.ProjectFilePath).Replace('\\', '/');
                string projectGuidText = moduleProject.ProjectGuid.ToString("B").ToUpperInvariant();
                builder.AppendLine("Project(\"{" + CSharpProjectTypeGuid + "}\") = \"" + EscapeSolutionText(moduleProject.ModuleId) + "\", \"" + EscapeSolutionText(relativeProjectFileName) + "\", \"" + projectGuidText + "\"");
                builder.AppendLine("EndProject");
            }

            builder.AppendLine("Global");
            builder.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
            builder.AppendLine("\t\tDebug|Any CPU = Debug|Any CPU");
            builder.AppendLine("\t\tRelease|Any CPU = Release|Any CPU");
            builder.AppendLine("\tEndGlobalSection");
            builder.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
            for (int index = 0; index < generatedCodeSolution.ModuleProjects.Count; index++) {
                string projectGuidText = generatedCodeSolution.ModuleProjects[index].ProjectGuid.ToString("B").ToUpperInvariant();
                builder.AppendLine("\t\t" + projectGuidText + ".Debug|Any CPU.ActiveCfg = Debug|Any CPU");
                builder.AppendLine("\t\t" + projectGuidText + ".Debug|Any CPU.Build.0 = Debug|Any CPU");
                builder.AppendLine("\t\t" + projectGuidText + ".Release|Any CPU.ActiveCfg = Release|Any CPU");
                builder.AppendLine("\t\t" + projectGuidText + ".Release|Any CPU.Build.0 = Release|Any CPU");
            }
            builder.AppendLine("\tEndGlobalSection");
            builder.AppendLine("EndGlobal");
            return builder.ToString();
        }

        /// <summary>
        /// Builds the generated global-usings file contents for one module project.
        /// </summary>
        /// <param name="moduleProject">Generated module project whose global-usings file should be emitted.</param>
        /// <returns>Generated global-usings file contents.</returns>
        string BuildGlobalUsingsFileContents(EditorGeneratedCodeModuleProject moduleProject) {
            if (moduleProject == null) {
                throw new ArgumentNullException(nameof(moduleProject));
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("global using helengine;");
            if (moduleProject.ModuleKind == EditorCodeModuleKind.Editor) {
                builder.AppendLine("global using helengine.editor;");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Appends generated project references for one module's declared dependencies.
        /// </summary>
        /// <param name="builder">Project file string builder being populated.</param>
        /// <param name="moduleProject">Generated module project whose dependencies should be emitted.</param>
        void AppendProjectReferences(StringBuilder builder, EditorGeneratedCodeModuleProject moduleProject) {
            if (builder == null) {
                throw new ArgumentNullException(nameof(builder));
            }
            if (moduleProject == null) {
                throw new ArgumentNullException(nameof(moduleProject));
            }
            if (moduleProject.DependencyModuleIds.Count == 0) {
                return;
            }

            builder.AppendLine("  <ItemGroup>");
            for (int index = 0; index < moduleProject.DependencyModuleIds.Count; index++) {
                EditorGeneratedCodeModuleProject dependencyProject = FindGeneratedModuleProject(moduleProject.DependencyModuleIds[index]);
                string relativeProjectPath = Path.GetRelativePath(
                    Path.GetDirectoryName(moduleProject.ProjectFilePath) ?? ProjectRootPath,
                    dependencyProject.ProjectFilePath);
                builder.AppendLine("    <ProjectReference Include=\"" + EscapeXml(relativeProjectPath) + "\" />");
            }
            builder.AppendLine("  </ItemGroup>");
        }

        /// <summary>
        /// Appends assembly references required by the generated project.
        /// </summary>
        /// <param name="builder">Project file string builder being populated.</param>
        /// <param name="moduleProject">Generated module project whose references should be emitted.</param>
        void AppendAssemblyReferences(StringBuilder builder, EditorGeneratedCodeModuleProject moduleProject) {
            if (builder == null) {
                throw new ArgumentNullException(nameof(builder));
            }
            if (moduleProject == null) {
                throw new ArgumentNullException(nameof(moduleProject));
            }

            builder.AppendLine("  <ItemGroup>");
            builder.AppendLine("    <Reference Include=\"helengine.core\">");
            builder.AppendLine("      <HintPath>" + EscapeXml(typeof(Core).Assembly.Location) + "</HintPath>");
            builder.AppendLine("    </Reference>");
            builder.AppendLine("    <Reference Include=\"helengine.shader\">");
            builder.AppendLine("      <HintPath>" + EscapeXml(typeof(ShaderRuntimeMaterial).Assembly.Location) + "</HintPath>");
            builder.AppendLine("    </Reference>");
            builder.AppendLine("    <Reference Include=\"helengine.input\">");
            builder.AppendLine("      <HintPath>" + EscapeXml(typeof(InputSystem).Assembly.Location) + "</HintPath>");
            builder.AppendLine("    </Reference>");
            builder.AppendLine("    <Reference Include=\"helengine.physics3d\">");
            builder.AppendLine("      <HintPath>" + EscapeXml(typeof(RigidBody3DComponent).Assembly.Location) + "</HintPath>");
            builder.AppendLine("    </Reference>");
            if (moduleProject.ModuleKind == EditorCodeModuleKind.Editor) {
                builder.AppendLine("    <Reference Include=\"helengine.editor\">");
                builder.AppendLine("      <HintPath>" + EscapeXml(typeof(EditorGameSolutionService).Assembly.Location) + "</HintPath>");
                builder.AppendLine("    </Reference>");
            }
            builder.AppendLine("  </ItemGroup>");
        }

        /// <summary>
        /// Builds the generated code solution description for the current authored module layout.
        /// </summary>
        /// <returns>Generated code solution description.</returns>
        EditorGeneratedCodeSolution BuildGeneratedCodeSolution() {
            EditorProjectPaths.Initialize(ProjectRootPath);
            EditorCodeModuleManifestDocument manifestDocument = CodeModuleManifestService.Load();
            if (string.IsNullOrWhiteSpace(GeneratedOutputRootPath)) {
                return GeneratedCodeSolutionBuilder.Build(ProjectRootPath, manifestDocument);
            }

            return GeneratedCodeSolutionBuilder.Build(ProjectRootPath, manifestDocument, GeneratedOutputRootPath);
        }

        /// <summary>
        /// Returns the primary generated module project for callers that use the first generated module entry.
        /// </summary>
        /// <returns>Primary generated module project.</returns>
        EditorGeneratedCodeModuleProject GetPrimaryModuleProject() {
            if (GeneratedCodeSolutionValue == null) {
                GeneratedCodeSolutionValue = BuildGeneratedCodeSolution();
            }

            return GeneratedCodeSolutionValue.PrimaryModuleProject;
        }

        /// <summary>
        /// Resolves one generated module project by stable module id.
        /// </summary>
        /// <param name="moduleId">Stable module id to resolve.</param>
        /// <returns>Resolved generated module project.</returns>
        EditorGeneratedCodeModuleProject FindGeneratedModuleProject(string moduleId) {
            if (string.IsNullOrWhiteSpace(moduleId)) {
                throw new ArgumentException("Module id must be provided.", nameof(moduleId));
            }
            if (GeneratedCodeSolutionValue == null) {
                GeneratedCodeSolutionValue = BuildGeneratedCodeSolution();
            }

            for (int index = 0; index < GeneratedCodeSolutionValue.ModuleProjects.Count; index++) {
                EditorGeneratedCodeModuleProject moduleProject = GeneratedCodeSolutionValue.ModuleProjects[index];
                if (string.Equals(moduleProject.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase)) {
                    return moduleProject;
                }
            }

            throw new InvalidOperationException($"Generated code project '{moduleId}' was not found.");
        }

        /// <summary>
        /// Resolves one project-relative or absolute path beneath the current project root.
        /// </summary>
        /// <param name="relativeOrAbsolutePath">Project-relative or absolute path to resolve.</param>
        /// <returns>Absolute resolved path.</returns>
        string ResolveProjectPath(string relativeOrAbsolutePath) {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath)) {
                throw new ArgumentException("Path must be provided.", nameof(relativeOrAbsolutePath));
            }

            string resolvedPath = Path.IsPathRooted(relativeOrAbsolutePath)
                ? relativeOrAbsolutePath
                : Path.Combine(ProjectRootPath, relativeOrAbsolutePath);
            return Path.GetFullPath(resolvedPath);
        }

        /// <summary>
        /// Escapes one text value for inclusion in XML text content.
        /// </summary>
        /// <param name="value">Text value to escape.</param>
        /// <returns>XML-safe text value.</returns>
        static string EscapeXml(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        /// <summary>
        /// Escapes one solution string for safe inclusion in a quoted field.
        /// </summary>
        /// <param name="value">Text value to escape.</param>
        /// <returns>Solution-safe text value.</returns>
        static string EscapeSolutionText(string value) {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\"", "\"\"");
        }

        /// <summary>
        /// Converts one arbitrary project name into a file-safe and assembly-safe identifier.
        /// </summary>
        /// <param name="value">Original project name.</param>
        /// <returns>Sanitized identifier string.</returns>
        static string SanitizeIdentifier(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++) {
                char ch = value[i];
                if (char.IsLetterOrDigit(ch)) {
                    builder.Append(ch);
                } else {
                    builder.Append('_');
                }
            }

            string result = builder.ToString().Trim('_');
            return result.Replace("__", "_");
        }
    }
}
