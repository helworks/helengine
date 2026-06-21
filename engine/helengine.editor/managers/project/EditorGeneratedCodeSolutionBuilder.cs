using System.Security.Cryptography;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Converts discovered authored code modules into generated C# project descriptions.
    /// </summary>
    public sealed class EditorGeneratedCodeSolutionBuilder {
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
            if (manifestDocument.Modules.Length == 0) {
                throw new InvalidOperationException("At least one code module must exist before generating script projects.");
            }

            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            string generatedOutputRootPath = ResolveGeneratedOutputRootPath(fullProjectRootPath);
            List<EditorGeneratedCodeModuleProject> moduleProjects = [];
            for (int index = 0; index < manifestDocument.Modules.Length; index++) {
                EditorCodeModuleManifestEntry module = manifestDocument.Modules[index];
                string projectDirectoryPath = Path.Combine(fullProjectRootPath, "user_settings", "generated_code", "projects", module.ModuleId);
                string projectFilePath = Path.Combine(projectDirectoryPath, module.ModuleId + ".csproj");
                string generatedGlobalUsingsFilePath = Path.Combine(projectDirectoryPath, "GlobalUsings.g.cs");
                string baseIntermediateOutputPath = Path.Combine(generatedOutputRootPath, "generated_code", "obj", module.ModuleId);
                string baseOutputPath = Path.Combine(generatedOutputRootPath, "generated_code", "bin", module.ModuleId);
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
                    module.ModuleKind));
            }

            return new EditorGeneratedCodeSolution(moduleProjects);
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
    }
}
