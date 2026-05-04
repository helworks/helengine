using System.Diagnostics;
using System.Text;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;

namespace helengine.editor {
    /// <summary>
    /// Compiles authored project code modules through the bundled codegen CLI for the shared build graph.
    /// </summary>
    internal sealed class EditorPlatformCodeCookService {
        readonly string ProjectRootPath;
        readonly IEditorCodegenToolRunner CodegenToolRunner;

        public EditorPlatformCodeCookService(string projectRootPath, IEditorCodegenToolRunner codegenToolRunner = null) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            CodegenToolRunner = codegenToolRunner ?? ProcessEditorCodegenToolRunner.Instance;
        }

        public PlatformBuildCodeModule[] CompileModules(
            EditorCodeModuleManifestDocument manifestDocument,
            string platformId,
            string runtimeSpecializationId,
            string codegenToolPath,
            PlatformCodegenProfileDefinition codegenProfile,
            IReadOnlyList<string> selectedModuleIds,
            IReadOnlyDictionary<string, string> selectedOptionValues,
            string outputRootPath) {
            if (manifestDocument == null) {
                throw new ArgumentNullException(nameof(manifestDocument));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (string.IsNullOrWhiteSpace(runtimeSpecializationId)) {
                throw new ArgumentException("Runtime specialization id must be provided.", nameof(runtimeSpecializationId));
            }
            if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("Output root path must be provided.", nameof(outputRootPath));
            }

            Directory.CreateDirectory(outputRootPath);
            List<PlatformBuildCodeModule> compiledModules = [];
            EditorCodeModuleManifestEntry[] modulesToCompile = ResolveModulesToCompile(manifestDocument, selectedModuleIds);

            for (int index = 0; index < modulesToCompile.Length; index++) {
                EditorCodeModuleManifestEntry moduleEntry = modulesToCompile[index];
                if (!ModuleContainsAnyScripts(moduleEntry)) {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(codegenToolPath)) {
                    throw new InvalidOperationException($"Code module '{moduleEntry.ModuleId}' requires a codegen tool path.");
                }
                if (codegenProfile == null) {
                    throw new InvalidOperationException($"Code module '{moduleEntry.ModuleId}' requires a selected codegen profile.");
                }
                if (codegenProfile.OutputLanguage != PlatformCodegenLanguage.Cpp) {
                    throw new NotSupportedException($"Code module '{moduleEntry.ModuleId}' requested unsupported output language '{codegenProfile.OutputLanguage}'.");
                }

                string moduleRootPath = Path.Combine(outputRootPath, moduleEntry.ModuleId);
                string projectFilePath = WriteModuleProjectFile(moduleEntry, moduleRootPath);
                string languageToken = "cpp";
                string endiannessToken = codegenProfile.Endianness == helengine.baseplatform.Profiles.PlatformSerializationEndianness.BigEndian
                    ? "big"
                    : "little";
                List<string> arguments = [
                    "--cpp",
                    "--project", projectFilePath,
                    "--output", moduleRootPath,
                    "--platform", platformId,
                    "--set", $"runtime-specialization={runtimeSpecializationId}",
                    "--language", languageToken,
                    "--endianness", endiannessToken
                ];

                if (selectedOptionValues != null) {
                    foreach (KeyValuePair<string, string> selectedOption in selectedOptionValues.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)) {
                        if (string.IsNullOrWhiteSpace(selectedOption.Key) || string.IsNullOrWhiteSpace(selectedOption.Value)) {
                            continue;
                        }

                        arguments.Add("--set");
                        arguments.Add($"{selectedOption.Key}={selectedOption.Value}");
                    }
                }

                CodegenToolRunner.Run(codegenToolPath, arguments, ProjectRootPath);
                compiledModules.Add(new PlatformBuildCodeModule(
                    moduleEntry.ModuleId,
                    NormalizeRelativePath(Path.Combine("code", moduleEntry.ModuleId)),
                    runtimeSpecializationId,
                    moduleEntry.LoadScopes,
                    moduleEntry.DependencyModuleIds));
            }

            return [.. compiledModules];
        }

        static HashSet<string> BuildSelectedModuleIdSet(IReadOnlyList<string> selectedModuleIds) {
            HashSet<string> selectedModuleIdSet = new(StringComparer.OrdinalIgnoreCase);
            if (selectedModuleIds == null) {
                return selectedModuleIdSet;
            }

            for (int index = 0; index < selectedModuleIds.Count; index++) {
                string selectedModuleId = selectedModuleIds[index];
                if (!string.IsNullOrWhiteSpace(selectedModuleId)) {
                    selectedModuleIdSet.Add(selectedModuleId);
                }
            }

            return selectedModuleIdSet;
        }

        static EditorCodeModuleManifestEntry[] ResolveModulesToCompile(
            EditorCodeModuleManifestDocument manifestDocument,
            IReadOnlyList<string> selectedModuleIds) {
            if (manifestDocument == null) {
                throw new ArgumentNullException(nameof(manifestDocument));
            }

            Dictionary<string, EditorCodeModuleManifestEntry> modulesById = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < manifestDocument.Modules.Length; index++) {
                EditorCodeModuleManifestEntry moduleEntry = manifestDocument.Modules[index];
                modulesById[moduleEntry.ModuleId] = moduleEntry;
            }

            List<EditorCodeModuleManifestEntry> orderedModules = [];
            HashSet<string> visitedModuleIds = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> activeModuleIds = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> selectedModuleIdSet = BuildSelectedModuleIdSet(selectedModuleIds);
            if (selectedModuleIdSet.Count > 0) {
                List<string> missingSelectedModuleIds = [];
                foreach (string selectedModuleId in selectedModuleIdSet) {
                    if (!modulesById.ContainsKey(selectedModuleId)) {
                        missingSelectedModuleIds.Add(selectedModuleId);
                    }
                }

                if (missingSelectedModuleIds.Count > 0) {
                    throw new InvalidOperationException(
                        $"Selected code module id(s) {string.Join(", ", missingSelectedModuleIds)} were not found in the authored module manifest.");
                }
            }

            IEnumerable<EditorCodeModuleManifestEntry> rootModules = manifestDocument.Modules;
            if (selectedModuleIdSet.Count > 0) {
                rootModules = manifestDocument.Modules.Where(module => selectedModuleIdSet.Contains(module.ModuleId));
            }

            foreach (EditorCodeModuleManifestEntry moduleEntry in rootModules) {
                VisitModule(moduleEntry, modulesById, orderedModules, visitedModuleIds, activeModuleIds);
            }

            return [.. orderedModules];
        }

        static void VisitModule(
            EditorCodeModuleManifestEntry moduleEntry,
            IReadOnlyDictionary<string, EditorCodeModuleManifestEntry> modulesById,
            List<EditorCodeModuleManifestEntry> orderedModules,
            HashSet<string> visitedModuleIds,
            HashSet<string> activeModuleIds) {
            if (moduleEntry == null) {
                return;
            }

            if (visitedModuleIds.Contains(moduleEntry.ModuleId)) {
                return;
            }

            if (!activeModuleIds.Add(moduleEntry.ModuleId)) {
                throw new InvalidOperationException($"Code module dependency cycle detected at '{moduleEntry.ModuleId}'.");
            }

            for (int index = 0; index < moduleEntry.DependencyModuleIds.Length; index++) {
                string dependencyModuleId = moduleEntry.DependencyModuleIds[index];
                if (string.IsNullOrWhiteSpace(dependencyModuleId)) {
                    continue;
                }

                if (!modulesById.TryGetValue(dependencyModuleId, out EditorCodeModuleManifestEntry? dependencyModuleEntry)) {
                    throw new InvalidOperationException(
                        $"Code module '{moduleEntry.ModuleId}' depends on missing module '{dependencyModuleId}'.");
                }

                VisitModule(dependencyModuleEntry, modulesById, orderedModules, visitedModuleIds, activeModuleIds);
            }

            activeModuleIds.Remove(moduleEntry.ModuleId);
            visitedModuleIds.Add(moduleEntry.ModuleId);
            orderedModules.Add(moduleEntry);
        }

        bool ModuleContainsAnyScripts(EditorCodeModuleManifestEntry moduleEntry) {
            return EnumerateModuleScriptFiles(moduleEntry).Any();
        }

        string WriteModuleProjectFile(EditorCodeModuleManifestEntry moduleEntry, string moduleRootPath) {
            string projectRootPath = Path.Combine(moduleRootPath, "_project");
            Directory.CreateDirectory(projectRootPath);
            string intermediateRootPath = Path.Combine(projectRootPath, "obj");
            string outputPath = Path.Combine(projectRootPath, moduleEntry.ModuleId + ".csproj");
            StringBuilder projectBuilder = new();
            projectBuilder.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
            projectBuilder.AppendLine("  <PropertyGroup>");
            projectBuilder.AppendLine("    <TargetFramework>net9.0</TargetFramework>");
            projectBuilder.AppendLine("    <OutputType>Library</OutputType>");
            projectBuilder.AppendLine("    <ImplicitUsings>disable</ImplicitUsings>");
            projectBuilder.AppendLine("    <Nullable>disable</Nullable>");
            projectBuilder.AppendLine("    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>");
            projectBuilder.AppendLine("    <EnableDefaultNoneItems>false</EnableDefaultNoneItems>");
            projectBuilder.AppendLine("    <EnableDefaultContentItems>false</EnableDefaultContentItems>");
            projectBuilder.AppendLine("    <EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>");
            projectBuilder.AppendLine("    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>");
            projectBuilder.AppendLine("    <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>");
            projectBuilder.AppendLine($"    <BaseIntermediateOutputPath>{EscapeXml(intermediateRootPath)}{Path.DirectorySeparatorChar}</BaseIntermediateOutputPath>");
            projectBuilder.AppendLine($"    <MSBuildProjectExtensionsPath>{EscapeXml(intermediateRootPath)}{Path.DirectorySeparatorChar}</MSBuildProjectExtensionsPath>");
            projectBuilder.AppendLine("  </PropertyGroup>");
            projectBuilder.AppendLine("  <ItemGroup>");

            string compileGlob = Path.Combine(ResolveProjectPath(moduleEntry.FolderPath), "**", "*.cs");
            projectBuilder.AppendLine($"    <Compile Include=\"{EscapeXml(compileGlob)}\" />");
            for (int index = 0; index < moduleEntry.NestedModuleFolderPaths.Length; index++) {
                string nestedCompileGlob = Path.Combine(ResolveProjectPath(moduleEntry.NestedModuleFolderPaths[index]), "**", "*.cs");
                projectBuilder.AppendLine($"    <Compile Remove=\"{EscapeXml(nestedCompileGlob)}\" />");
            }

            projectBuilder.AppendLine("  </ItemGroup>");
            projectBuilder.AppendLine("</Project>");
            File.WriteAllText(outputPath, projectBuilder.ToString());
            return outputPath;
        }

        IEnumerable<string> EnumerateModuleScriptFiles(EditorCodeModuleManifestEntry moduleEntry) {
            string moduleRootPath = ResolveProjectPath(moduleEntry.FolderPath);
            if (!Directory.Exists(moduleRootPath)) {
                yield break;
            }

            HashSet<string> excludedNestedFolderPaths = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < moduleEntry.NestedModuleFolderPaths.Length; index++) {
                excludedNestedFolderPaths.Add(ResolveProjectPath(moduleEntry.NestedModuleFolderPaths[index]));
            }

            foreach (string scriptPath in Directory.EnumerateFiles(moduleRootPath, "*.cs", SearchOption.AllDirectories)) {
                bool isInNestedModuleFolder = false;
                foreach (string nestedFolderPath in excludedNestedFolderPaths) {
                    if (scriptPath.StartsWith(nestedFolderPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(Path.GetDirectoryName(scriptPath), nestedFolderPath, StringComparison.OrdinalIgnoreCase)) {
                        isInNestedModuleFolder = true;
                        break;
                    }
                }

                if (!isInNestedModuleFolder) {
                    yield return scriptPath;
                }
            }
        }

        string ResolveProjectPath(string relativeOrAbsolutePath) {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath)) {
                throw new ArgumentException("Module source root must be provided.", nameof(relativeOrAbsolutePath));
            }

            string resolvedPath = Path.IsPathRooted(relativeOrAbsolutePath)
                ? relativeOrAbsolutePath
                : Path.Combine(ProjectRootPath, relativeOrAbsolutePath);
            return Path.GetFullPath(resolvedPath);
        }

        static string NormalizeRelativePath(string relativePath) {
            return relativePath.Replace('\\', '/');
        }

        static string EscapeXml(string value) {
            return System.Security.SecurityElement.Escape(value) ?? string.Empty;
        }
    }

    public interface IEditorCodegenToolRunner {
        void Run(string toolPath, IReadOnlyList<string> arguments, string workingDirectory);
    }

    internal sealed class ProcessEditorCodegenToolRunner : IEditorCodegenToolRunner {
        public static ProcessEditorCodegenToolRunner Instance { get; } = new ProcessEditorCodegenToolRunner();

        public void Run(string toolPath, IReadOnlyList<string> arguments, string workingDirectory) {
            if (string.IsNullOrWhiteSpace(toolPath)) {
                throw new ArgumentException("Codegen tool path must be provided.", nameof(toolPath));
            }

            ProcessStartInfo startInfo = new() {
                FileName = toolPath,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            for (int index = 0; index < arguments.Count; index++) {
                startInfo.ArgumentList.Add(arguments[index]);
            }

            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start codegen tool '{toolPath}'.");
            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) {
                throw new InvalidOperationException(
                    $"Codegen tool '{toolPath}' failed with exit code {process.ExitCode}.{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}".Trim());
            }
        }
    }
}
