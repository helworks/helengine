using System.Security.Cryptography;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Converts discovered authored code modules into generated C# project descriptions.
    /// </summary>
    public sealed class EditorGeneratedCodeSolutionBuilder {
        /// <summary>
        /// Discovery service used to infer generated sibling test projects from raw .tests folders.
        /// </summary>
        readonly EditorGeneratedCodeTestProjectDiscoveryService TestProjectDiscoveryService = new EditorGeneratedCodeTestProjectDiscoveryService();

        /// <summary>
        /// Default target framework used by generated runtime script projects.
        /// </summary>
        const string RuntimeTargetFrameworkValue = "net9.0";

        /// <summary>
        /// Target framework used by generated editor-only script projects that bind against the shared editor assemblies.
        /// </summary>
        const string EditorTargetFrameworkValue = "net9.0";

        /// <summary>
        /// Builds the generated code solution description for the supplied authored modules.
        /// </summary>
        /// <param name="projectRootPath">Absolute game project root path.</param>
        /// <param name="manifestDocument">Discovered authored code-module manifest document.</param>
        /// <returns>Generated code solution description.</returns>
        public EditorGeneratedCodeSolution Build(string projectRootPath, EditorCodeModuleManifestDocument manifestDocument) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (manifestDocument == null) {
                throw new ArgumentNullException(nameof(manifestDocument));
            }

            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            return Build(fullProjectRootPath, manifestDocument, ResolveGeneratedOutputRootPath(fullProjectRootPath));
        }

        /// <summary>
        /// Builds the generated code solution description for the supplied authored modules and explicit output root.
        /// </summary>
        /// <param name="projectRootPath">Absolute game project root path.</param>
        /// <param name="manifestDocument">Discovered authored code-module manifest document.</param>
        /// <param name="generatedOutputRootPath">Absolute output root used by generated module projects.</param>
        /// <returns>Generated code solution description.</returns>
        public EditorGeneratedCodeSolution Build(string projectRootPath, EditorCodeModuleManifestDocument manifestDocument, string generatedOutputRootPath) {
            return Build(
                projectRootPath,
                manifestDocument,
                generatedOutputRootPath,
                ResolveGeneratedWorkspaceRootPath(Path.GetFullPath(projectRootPath)),
                EditorScriptCompilationMode.EditorFull);
        }

        /// <summary>
        /// Builds the generated code solution description with independent roots for generated project files and compiler outputs.
        /// </summary>
        /// <param name="projectRootPath">Absolute game project root path.</param>
        /// <param name="manifestDocument">Discovered authored code-module manifest document.</param>
        /// <param name="generatedOutputRootPath">Absolute output root used by generated module projects.</param>
        /// <param name="generatedWorkspaceRootPath">Absolute workspace root used for generated solution, project, and global-using files.</param>
        /// <returns>Generated code solution description.</returns>
        public EditorGeneratedCodeSolution Build(
            string projectRootPath,
            EditorCodeModuleManifestDocument manifestDocument,
            string generatedOutputRootPath,
            string generatedWorkspaceRootPath) {
            return Build(
                projectRootPath,
                manifestDocument,
                generatedOutputRootPath,
                generatedWorkspaceRootPath,
                EditorScriptCompilationMode.EditorFull);
        }

        /// <summary>
        /// Builds the generated code solution with independent roots and an explicit script-compilation mode.
        /// </summary>
        /// <param name="projectRootPath">Absolute game project root path.</param>
        /// <param name="manifestDocument">Discovered authored code-module manifest document.</param>
        /// <param name="generatedOutputRootPath">Absolute output root used by generated module projects.</param>
        /// <param name="generatedWorkspaceRootPath">Absolute workspace root used for generated solution, project, and global-using files.</param>
        /// <param name="compilationMode">Authored script surfaces included in the generated solution.</param>
        /// <returns>Generated code solution description.</returns>
        public EditorGeneratedCodeSolution Build(
            string projectRootPath,
            EditorCodeModuleManifestDocument manifestDocument,
            string generatedOutputRootPath,
            string generatedWorkspaceRootPath,
            EditorScriptCompilationMode compilationMode) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (manifestDocument == null) {
                throw new ArgumentNullException(nameof(manifestDocument));
            }
            if (string.IsNullOrWhiteSpace(generatedOutputRootPath)) {
                throw new ArgumentException("Generated output root path must be provided.", nameof(generatedOutputRootPath));
            }
            if (string.IsNullOrWhiteSpace(generatedWorkspaceRootPath)) {
                throw new ArgumentException("Generated workspace root path must be provided.", nameof(generatedWorkspaceRootPath));
            }
            if (manifestDocument.Modules.Length == 0) {
                throw new InvalidOperationException("At least one code module must exist before generating script projects.");
            }

            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            string fullGeneratedOutputRootPath = Path.GetFullPath(generatedOutputRootPath);
            string fullGeneratedWorkspaceRootPath = Path.GetFullPath(generatedWorkspaceRootPath);
            EditorCodeModuleManifestEntry[] selectedModules = SelectModules(manifestDocument.Modules, compilationMode);
            if (selectedModules.Length == 0) {
                throw new InvalidOperationException($"Script compilation mode '{compilationMode}' did not select any authored code modules.");
            }
            List<EditorGeneratedCodeModuleProject> moduleProjects = [];
            for (int index = 0; index < selectedModules.Length; index++) {
                EditorCodeModuleManifestEntry module = selectedModules[index];
                string projectDirectoryPath = Path.Combine(fullGeneratedWorkspaceRootPath, "projects", module.ModuleId);
                string projectFilePath = Path.Combine(projectDirectoryPath, module.ModuleId + ".csproj");
                string generatedGlobalUsingsFilePath = Path.Combine(projectDirectoryPath, "GlobalUsings.g.cs");
                string baseIntermediateOutputPath = Path.Combine(fullGeneratedOutputRootPath, "generated_code", "obj", module.ModuleId);
                string baseOutputPath = Path.Combine(fullGeneratedOutputRootPath, "generated_code", "bin", module.ModuleId);
                string targetFramework = module.ModuleKind == EditorCodeModuleKind.Editor
                    ? EditorTargetFrameworkValue
                    : RuntimeTargetFrameworkValue;
                string outputDirectoryPath = Path.Combine(baseOutputPath, "Debug", targetFramework);
                Guid projectGuid = CreateStableGuid(fullProjectRootPath + "|" + module.ModuleId);
                moduleProjects.Add(new EditorGeneratedCodeModuleProject(
                    module.ModuleId,
                    module.FolderPath,
                    module.DependencyModuleIds,
                    module.NestedModuleFolderPaths,
                    projectFilePath,
                    generatedGlobalUsingsFilePath,
                    baseIntermediateOutputPath,
                    baseOutputPath,
                    targetFramework,
                    outputDirectoryPath,
                    projectGuid,
                    module.ModuleKind,
                    EditorGeneratedCodeProjectKind.Production,
                    string.Empty));
            }

            IReadOnlyList<EditorGeneratedCodeModuleProject> testProjects = [];
            IReadOnlyList<EditorGeneratedCodeModuleProject> filteredModuleProjects = moduleProjects;
            if (compilationMode == EditorScriptCompilationMode.EditorFull) {
                testProjects = TestProjectDiscoveryService.Discover(
                    fullProjectRootPath,
                    fullGeneratedOutputRootPath,
                    moduleProjects);
                filteredModuleProjects = ApplyTestFolderExclusions(moduleProjects, testProjects);
            }

            return new EditorGeneratedCodeSolution(filteredModuleProjects, testProjects);
        }

        /// <summary>
        /// Selects production module manifests applicable to one script-compilation mode.
        /// </summary>
        /// <param name="modules">All discovered authored module manifests.</param>
        /// <param name="compilationMode">Requested generated-code compilation mode.</param>
        /// <returns>Ordered manifests included in the generated solution.</returns>
        static EditorCodeModuleManifestEntry[] SelectModules(
            IReadOnlyList<EditorCodeModuleManifestEntry> modules,
            EditorScriptCompilationMode compilationMode) {
            if (modules == null) {
                throw new ArgumentNullException(nameof(modules));
            }

            if (compilationMode == EditorScriptCompilationMode.EditorFull) {
                return [.. modules];
            }
            if (compilationMode == EditorScriptCompilationMode.RuntimeOnly) {
                return [.. modules.Where(static module => module.ModuleKind == EditorCodeModuleKind.Runtime)];
            }

            throw new ArgumentOutOfRangeException(nameof(compilationMode), compilationMode, "The script compilation mode is not supported.");
        }

        /// <summary>
        /// Applies inferred test-folder compile exclusions to any generated production project whose source boundary owns the test folder path.
        /// </summary>
        /// <param name="moduleProjects">Ordered generated production projects.</param>
        /// <param name="testProjects">Ordered inferred generated test projects.</param>
        /// <returns>Generated production projects updated with additional compile-remove folder boundaries where required.</returns>
        static IReadOnlyList<EditorGeneratedCodeModuleProject> ApplyTestFolderExclusions(
            IReadOnlyList<EditorGeneratedCodeModuleProject> moduleProjects,
            IReadOnlyList<EditorGeneratedCodeModuleProject> testProjects) {
            List<EditorGeneratedCodeModuleProject> updatedProjects = [];
            for (int projectIndex = 0; projectIndex < moduleProjects.Count; projectIndex++) {
                EditorGeneratedCodeModuleProject moduleProject = moduleProjects[projectIndex];
                List<string> nestedSourceFolderPaths = [.. moduleProject.NestedSourceFolderPaths];
                for (int testIndex = 0; testIndex < testProjects.Count; testIndex++) {
                    string testSourceFolderPath = testProjects[testIndex].SourceFolderPath;
                    if (IsDescendantFolder(moduleProject.SourceFolderPath, testSourceFolderPath)
                        && !nestedSourceFolderPaths.Contains(testSourceFolderPath, StringComparer.OrdinalIgnoreCase)) {
                        nestedSourceFolderPaths.Add(testSourceFolderPath);
                    }
                }

                nestedSourceFolderPaths.Sort(StringComparer.OrdinalIgnoreCase);
                updatedProjects.Add(CloneWithNestedSourceFolderPaths(moduleProject, nestedSourceFolderPaths));
            }

            return updatedProjects;
        }

        /// <summary>
        /// Resolves the shared sibling output root used for generated script project binaries and intermediate artifacts.
        /// </summary>
        /// <param name="fullProjectRootPath">Absolute authored project root path.</param>
        /// <returns>Absolute output root path for the authored project.</returns>
        static string ResolveGeneratedOutputRootPath(string fullProjectRootPath) {
            if (string.IsNullOrWhiteSpace(fullProjectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(fullProjectRootPath));
            }

            string projectFolderName = new DirectoryInfo(fullProjectRootPath).Name;
            DirectoryInfo? parentDirectory = Directory.GetParent(fullProjectRootPath);
            if (string.IsNullOrWhiteSpace(projectFolderName) ||
                parentDirectory == null ||
                string.IsNullOrWhiteSpace(parentDirectory.FullName)) {
                return Path.Combine(fullProjectRootPath, "output");
            }

            return Path.Combine(parentDirectory.FullName, "output", projectFolderName);
        }

        /// <summary>
        /// Resolves the default authored-project location for generated solution workspace files.
        /// </summary>
        /// <param name="fullProjectRootPath">Absolute game project root path.</param>
        /// <returns>Absolute generated workspace root path.</returns>
        static string ResolveGeneratedWorkspaceRootPath(string fullProjectRootPath) {
            return Path.Combine(fullProjectRootPath, "user_settings", "generated_code");
        }

        /// <summary>
        /// Creates one stable GUID from the supplied seed string.
        /// </summary>
        /// <param name="seed">Seed value used to derive the GUID.</param>
        /// <returns>Deterministic GUID.</returns>
        static Guid CreateStableGuid(string seed) {
            byte[] hash = SHA1.HashData(Encoding.UTF8.GetBytes(seed ?? string.Empty));
            Span<byte> guidBytes = stackalloc byte[16];
            hash.AsSpan(0, guidBytes.Length).CopyTo(guidBytes);
            guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50);
            guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
            return new Guid(guidBytes);
        }

        /// <summary>
        /// Clones one generated project description with a replacement nested-source-folder list.
        /// </summary>
        /// <param name="moduleProject">Original generated project description.</param>
        /// <param name="nestedSourceFolderPaths">Replacement nested source folder path list.</param>
        /// <returns>Cloned generated project description.</returns>
        static EditorGeneratedCodeModuleProject CloneWithNestedSourceFolderPaths(
            EditorGeneratedCodeModuleProject moduleProject,
            IReadOnlyList<string> nestedSourceFolderPaths) {
            return new EditorGeneratedCodeModuleProject(
                moduleProject.ModuleId,
                moduleProject.SourceFolderPath,
                moduleProject.DependencyModuleIds,
                nestedSourceFolderPaths,
                moduleProject.ProjectFilePath,
                moduleProject.GeneratedGlobalUsingsFilePath,
                moduleProject.BaseIntermediateOutputPath,
                moduleProject.BaseOutputPath,
                moduleProject.TargetFramework,
                moduleProject.OutputDirectoryPath,
                moduleProject.ProjectGuid,
                moduleProject.ModuleKind,
                moduleProject.ProjectKind,
                moduleProject.ReferencedProductionModuleId);
        }

        /// <summary>
        /// Determines whether one project-relative folder path is nested beneath another.
        /// </summary>
        /// <param name="parentFolderPath">Candidate parent folder path.</param>
        /// <param name="candidateFolderPath">Candidate nested folder path.</param>
        /// <returns><c>true</c> when the candidate path is nested beneath the parent path.</returns>
        static bool IsDescendantFolder(string parentFolderPath, string candidateFolderPath) {
            if (string.IsNullOrWhiteSpace(parentFolderPath) || string.IsNullOrWhiteSpace(candidateFolderPath)) {
                return false;
            }

            if (string.Equals(parentFolderPath, candidateFolderPath, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            string prefix = parentFolderPath.Replace('\\', '/').TrimEnd('/') + "/";
            string normalizedCandidatePath = candidateFolderPath.Replace('\\', '/');
            return normalizedCandidatePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
