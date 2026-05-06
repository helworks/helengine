using System.Security.Cryptography;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Converts discovered authored code modules into generated C# project descriptions.
    /// </summary>
    public sealed class EditorGeneratedCodeSolutionBuilder {
        /// <summary>
        /// Default target framework used by generated script projects.
        /// </summary>
        const string TargetFrameworkValue = "net9.0";

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
            List<EditorGeneratedCodeModuleProject> moduleProjects = [];
            for (int index = 0; index < manifestDocument.Modules.Length; index++) {
                EditorCodeModuleManifestEntry module = manifestDocument.Modules[index];
                string projectDirectoryPath = Path.Combine(fullProjectRootPath, "user_settings", "generated_code", "projects", module.ModuleId);
                string projectFilePath = Path.Combine(projectDirectoryPath, module.ModuleId + ".csproj");
                string generatedGlobalUsingsFilePath = Path.Combine(projectDirectoryPath, "GlobalUsings.g.cs");
                string baseIntermediateOutputPath = Path.Combine(fullProjectRootPath, "user_settings", "generated_code", "obj", module.ModuleId);
                string baseOutputPath = Path.Combine(fullProjectRootPath, "user_settings", "generated_code", "bin", module.ModuleId);
                string outputDirectoryPath = Path.Combine(baseOutputPath, "Debug", TargetFrameworkValue);
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
                    outputDirectoryPath,
                    projectGuid,
                    module.ModuleKind));
            }

            return new EditorGeneratedCodeSolution(moduleProjects);
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
