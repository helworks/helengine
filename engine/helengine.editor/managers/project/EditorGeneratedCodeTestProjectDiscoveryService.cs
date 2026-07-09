using System.Security.Cryptography;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Discovers inferred generated test projects from raw sibling .tests folders beneath assets/codebase.
    /// </summary>
    public sealed class EditorGeneratedCodeTestProjectDiscoveryService {
        /// <summary>
        /// Folder suffix used to identify generated test surfaces.
        /// </summary>
        const string TestsSuffix = ".tests";

        /// <summary>
        /// Discovers generated test projects rooted beneath the project's assets/codebase folder.
        /// </summary>
        /// <param name="projectRootPath">Absolute game project root path.</param>
        /// <param name="generatedOutputRootPath">Absolute generated output root path.</param>
        /// <param name="productionProjects">Ordered generated production projects already resolved for the project.</param>
        /// <returns>Ordered inferred generated test projects.</returns>
        public IReadOnlyList<EditorGeneratedCodeModuleProject> Discover(
            string projectRootPath,
            string generatedOutputRootPath,
            IReadOnlyList<EditorGeneratedCodeModuleProject> productionProjects) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (string.IsNullOrWhiteSpace(generatedOutputRootPath)) {
                throw new ArgumentException("Generated output root path must be provided.", nameof(generatedOutputRootPath));
            }
            if (productionProjects == null) {
                throw new ArgumentNullException(nameof(productionProjects));
            }

            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            string fullGeneratedOutputRootPath = Path.GetFullPath(generatedOutputRootPath);
            string codebaseRootPath = Path.Combine(fullProjectRootPath, "assets", "codebase");
            if (!Directory.Exists(codebaseRootPath)) {
                return [];
            }

            Dictionary<string, EditorGeneratedCodeModuleProject> productionProjectsById = productionProjects
                .Where(static project => project.ProjectKind == EditorGeneratedCodeProjectKind.Production)
                .ToDictionary(static project => project.ModuleId, StringComparer.OrdinalIgnoreCase);
            List<EditorGeneratedCodeModuleProject> discoveredProjects = [];
            foreach (string testFolderPath in Directory.EnumerateDirectories(codebaseRootPath, "*" + TestsSuffix, SearchOption.TopDirectoryOnly)) {
                string testSurfaceId = Path.GetFileName(testFolderPath);
                string productionSurfaceId = testSurfaceId[..^TestsSuffix.Length];
                if (!productionProjectsById.TryGetValue(productionSurfaceId, out EditorGeneratedCodeModuleProject? productionProject)) {
                    throw new InvalidOperationException(
                        $"Generated test surface '{testSurfaceId}' expected production surface '{productionSurfaceId}', but no matching generated production project exists for '{testFolderPath}'.");
                }

                string relativeSourceFolderPath = Path.GetRelativePath(fullProjectRootPath, testFolderPath).Replace('\\', '/');
                discoveredProjects.Add(CreateTestProject(fullProjectRootPath, fullGeneratedOutputRootPath, productionProject, testSurfaceId, relativeSourceFolderPath));
            }

            discoveredProjects.Sort(static (left, right) => string.Compare(left.ModuleId, right.ModuleId, StringComparison.OrdinalIgnoreCase));
            return discoveredProjects;
        }

        /// <summary>
        /// Creates one generated test project description for a discovered sibling .tests folder.
        /// </summary>
        /// <param name="projectRootPath">Absolute game project root path.</param>
        /// <param name="generatedOutputRootPath">Absolute generated output root path.</param>
        /// <param name="productionProject">Matching generated production project.</param>
        /// <param name="testSurfaceId">Stable generated test surface id.</param>
        /// <param name="relativeSourceFolderPath">Project-relative discovered test folder path.</param>
        /// <returns>Generated project description for the test surface.</returns>
        static EditorGeneratedCodeModuleProject CreateTestProject(
            string projectRootPath,
            string generatedOutputRootPath,
            EditorGeneratedCodeModuleProject productionProject,
            string testSurfaceId,
            string relativeSourceFolderPath) {
            string projectDirectoryPath = Path.Combine(projectRootPath, "user_settings", "generated_code", "projects", testSurfaceId);
            string projectFilePath = Path.Combine(projectDirectoryPath, testSurfaceId + ".csproj");
            string generatedGlobalUsingsFilePath = Path.Combine(projectDirectoryPath, "GlobalUsings.g.cs");
            string baseIntermediateOutputPath = Path.Combine(generatedOutputRootPath, "generated_code", "obj", testSurfaceId);
            string baseOutputPath = Path.Combine(generatedOutputRootPath, "generated_code", "bin", testSurfaceId);
            string targetFramework = productionProject.TargetFramework;
            string outputDirectoryPath = Path.Combine(baseOutputPath, "Debug", targetFramework);
            Guid projectGuid = CreateStableGuid(projectRootPath + "|" + testSurfaceId);
            return new EditorGeneratedCodeModuleProject(
                testSurfaceId,
                relativeSourceFolderPath,
                [],
                [],
                projectFilePath,
                generatedGlobalUsingsFilePath,
                baseIntermediateOutputPath,
                baseOutputPath,
                targetFramework,
                outputDirectoryPath,
                projectGuid,
                productionProject.ModuleKind,
                EditorGeneratedCodeProjectKind.Test,
                productionProject.ModuleId);
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
