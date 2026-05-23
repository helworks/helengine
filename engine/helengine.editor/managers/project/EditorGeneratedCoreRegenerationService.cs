using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Profiles;

namespace helengine.editor {
    /// <summary>
    /// Regenerates the shared native source tree for core and portable input before platform builds run.
    /// </summary>
    public class EditorGeneratedCoreRegenerationService {
        /// <summary>
        /// Loads the HelEngine repository root for local source builds.
        /// </summary>
        readonly EditorSourceBuildWorkspaceLocator WorkspaceLocator;

        /// <summary>
        /// Initializes one generated-core regeneration service.
        /// </summary>
        public EditorGeneratedCoreRegenerationService() {
            WorkspaceLocator = new EditorSourceBuildWorkspaceLocator();
        }

        /// <summary>
        /// Regenerates the generated core tree and the portable input tree for one platform build using the builder-provided codegen metadata.
        /// </summary>
        /// <param name="platformDefinition">Typed platform metadata exposed by the active builder.</param>
        /// <param name="codegenProfile">Selected codegen profile metadata.</param>
        /// <param name="selectedCodegenOptionValues">Selected codegen option values persisted by the editor.</param>
        /// <param name="generatedCoreRootPath">Absolute output root for the fresh generated-core tree.</param>
        /// <param name="additionalPreprocessorSymbols">Scene-derived and platform-derived preprocessor symbols that should be forwarded to code generation.</param>
        /// <param name="cancellationToken">Cancellation token that can stop regeneration cooperatively.</param>
        public virtual void Regenerate(
            PlatformDefinition platformDefinition,
            PlatformCodegenProfileDefinition codegenProfile,
            IReadOnlyDictionary<string, string> selectedCodegenOptionValues,
            string generatedCoreRootPath,
            string codegenToolPath,
            IReadOnlyList<string> generatedCoreProjectPaths,
            IReadOnlyList<string> additionalPreprocessorSymbols,
            CancellationToken cancellationToken) {
            if (platformDefinition == null) {
                throw new ArgumentNullException(nameof(platformDefinition));
            }
            if (codegenProfile == null) {
                throw new ArgumentNullException(nameof(codegenProfile));
            }
            if (selectedCodegenOptionValues == null) {
                throw new ArgumentNullException(nameof(selectedCodegenOptionValues));
            }
            if (string.IsNullOrWhiteSpace(codegenToolPath)) {
                throw new ArgumentException("Codegen tool path must be provided.", nameof(codegenToolPath));
            }
            if (generatedCoreProjectPaths == null) {
                throw new ArgumentNullException(nameof(generatedCoreProjectPaths));
            }
            if (additionalPreprocessorSymbols == null) {
                throw new ArgumentNullException(nameof(additionalPreprocessorSymbols));
            }
            if (string.IsNullOrWhiteSpace(generatedCoreRootPath)) {
                throw new ArgumentException("Generated core output root must be provided.", nameof(generatedCoreRootPath));
            }
            if (codegenProfile.OutputLanguage != PlatformCodegenLanguage.Cpp) {
                throw new NotSupportedException($"The editor-owned regeneration service currently supports only C++ output, not '{codegenProfile.OutputLanguage}'.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            string helEngineRootPath = WorkspaceLocator.ResolveHelEngineRootPath();
            string fullCodegenToolPath = Path.GetFullPath(codegenToolPath);
            string generatedCoreOutputRoot = Path.GetFullPath(generatedCoreRootPath);
            string helengineCoreProjectPath = Path.Combine(helEngineRootPath, "engine", "helengine.core", "helengine.core.csproj");
            string helengineShaderProjectPath = Path.Combine(helEngineRootPath, "engine", "helengine.shader", "helengine.shader.csproj");
            string helengineInputProjectPath = Path.Combine(helEngineRootPath, "engine", "helengine.input", "helengine.input.csproj");
            string helenginePhysics3DProjectPath = Path.Combine(helEngineRootPath, "engine", "helengine.physics3d", "helengine.physics3d.csproj");
            string bundledRuntimeSupportRootPath = Path.Combine(
                Path.GetDirectoryName(fullCodegenToolPath) ?? throw new InvalidOperationException($"Unable to resolve the codegen tool directory from '{fullCodegenToolPath}'."),
                ".net.cpp");
            string tempRoot = Path.Combine(Path.GetTempPath(), "helengine-generated-core", platformDefinition.PlatformId, Guid.NewGuid().ToString("N"));
            string shaderOutputRoot = Path.Combine(tempRoot, "shader");
            string portableInputOutputRoot = Path.Combine(tempRoot, "portable-input");
            string physics3DOutputRoot = Path.Combine(tempRoot, "physics3d");
            string externalProjectsOutputRoot = Path.Combine(tempRoot, "external");
            StringBuilder logBuilder = new();
            bool shouldRegeneratePhysics3DProject = ShouldRegeneratePhysics3DProject(additionalPreprocessorSymbols);

            if (!File.Exists(helengineCoreProjectPath)) {
                throw new FileNotFoundException($"Could not find helengine.core project at '{helengineCoreProjectPath}'.", helengineCoreProjectPath);
            }
            if (!File.Exists(helengineShaderProjectPath)) {
                throw new FileNotFoundException($"Could not find helengine.shader project at '{helengineShaderProjectPath}'.", helengineShaderProjectPath);
            }
            if (!File.Exists(helengineInputProjectPath)) {
                throw new FileNotFoundException($"Could not find helengine.input project at '{helengineInputProjectPath}'.", helengineInputProjectPath);
            }
            if (shouldRegeneratePhysics3DProject && !File.Exists(helenginePhysics3DProjectPath)) {
                throw new FileNotFoundException($"Could not find helengine.physics3d project at '{helenginePhysics3DProjectPath}'.", helenginePhysics3DProjectPath);
            }
            if (!File.Exists(fullCodegenToolPath)) {
                throw new FileNotFoundException($"Could not find the bundled csharpcodegen executable at '{fullCodegenToolPath}'.", fullCodegenToolPath);
            }
            if (Directory.Exists(generatedCoreOutputRoot)) {
                Directory.Delete(generatedCoreOutputRoot, true);
            }
            Directory.CreateDirectory(generatedCoreOutputRoot);
            try {
                IReadOnlyList<string> portableInputPreprocessorSymbols = ResolvePortableInputPreprocessorSymbols(platformDefinition);
                IReadOnlyList<string> combinedPreprocessorSymbols = CombineAdditionalPreprocessorSymbols(
                    portableInputPreprocessorSymbols,
                    additionalPreprocessorSymbols);
                RegenerateProject(
                    fullCodegenToolPath,
                    helengineCoreProjectPath,
                    generatedCoreOutputRoot,
                    platformDefinition,
                    codegenProfile,
                    selectedCodegenOptionValues,
                    combinedPreprocessorSymbols,
                    logBuilder,
                    cancellationToken);
                RegenerateProject(
                    fullCodegenToolPath,
                    helengineShaderProjectPath,
                    shaderOutputRoot,
                    platformDefinition,
                    codegenProfile,
                    selectedCodegenOptionValues,
                    combinedPreprocessorSymbols,
                    logBuilder,
                    cancellationToken);
                RegenerateProject(
                    fullCodegenToolPath,
                    helengineInputProjectPath,
                    portableInputOutputRoot,
                    platformDefinition,
                    codegenProfile,
                    selectedCodegenOptionValues,
                    combinedPreprocessorSymbols,
                    logBuilder,
                    cancellationToken);
                MergeGeneratedSourceTree(shaderOutputRoot, generatedCoreOutputRoot);
                MergeGeneratedConversionReport(shaderOutputRoot, generatedCoreOutputRoot);
                MergeGeneratedSourceTree(portableInputOutputRoot, generatedCoreOutputRoot);
                MergeGeneratedConversionReport(portableInputOutputRoot, generatedCoreOutputRoot);
                if (shouldRegeneratePhysics3DProject) {
                    RegenerateProject(
                        fullCodegenToolPath,
                        helenginePhysics3DProjectPath,
                        physics3DOutputRoot,
                        platformDefinition,
                        codegenProfile,
                        selectedCodegenOptionValues,
                        combinedPreprocessorSymbols,
                        logBuilder,
                        cancellationToken);
                    MergeGeneratedSourceTree(physics3DOutputRoot, generatedCoreOutputRoot);
                    MergeGeneratedConversionReport(physics3DOutputRoot, generatedCoreOutputRoot);
                }
                RegenerateAdditionalGeneratedCoreProjects(
                    generatedCoreProjectPaths,
                    fullCodegenToolPath,
                    externalProjectsOutputRoot,
                    generatedCoreOutputRoot,
                    platformDefinition,
                    codegenProfile,
                    selectedCodegenOptionValues,
                    combinedPreprocessorSymbols,
                    logBuilder,
                    cancellationToken);
                MergeBundledRuntimeSupportTree(bundledRuntimeSupportRootPath, generatedCoreOutputRoot);
                EnsureGeneratedRuntimeComponentDeserializerSupport(generatedCoreOutputRoot, platformDefinition.PlatformId);
                EnsureGeneratedIncludeCompatibilityShims(generatedCoreOutputRoot);
                WriteGeneratedCoreTranslationUnit(generatedCoreOutputRoot);
            } finally {
                DeleteDirectoryIfPresent(tempRoot);
            }
        }

        /// <summary>
        /// Regenerates one project into one output tree.
        /// </summary>
        /// <param name="fileName">Path to the codegen executable.</param>
        /// <param name="projectPath">Project path to convert.</param>
        /// <param name="outputRootPath">Output tree that receives the generated files.</param>
        /// <param name="platformDefinition">Typed platform metadata exposed by the active builder.</param>
        /// <param name="codegenProfile">Selected codegen profile metadata.</param>
        /// <param name="selectedCodegenOptionValues">Selected codegen option values persisted by the editor.</param>
        /// <param name="additionalPreprocessorSymbols">Feature symbols injected for portable-input compilation.</param>
        /// <param name="logBuilder">Shared log buffer that records process output for the combined regeneration run.</param>
        /// <param name="cancellationToken">Cancellation token that can stop regeneration cooperatively.</param>
        static void RegenerateProject(
            string fileName,
            string projectPath,
            string outputRootPath,
            PlatformDefinition platformDefinition,
            PlatformCodegenProfileDefinition codegenProfile,
            IReadOnlyDictionary<string, string> selectedCodegenOptionValues,
            IReadOnlyList<string> additionalPreprocessorSymbols,
            StringBuilder logBuilder,
            CancellationToken cancellationToken) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("Codegen executable path must be provided.", nameof(fileName));
            }
            if (string.IsNullOrWhiteSpace(projectPath)) {
                throw new ArgumentException("Project path must be provided.", nameof(projectPath));
            }
            if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("Output root path must be provided.", nameof(outputRootPath));
            }
            if (platformDefinition == null) {
                throw new ArgumentNullException(nameof(platformDefinition));
            }
            if (codegenProfile == null) {
                throw new ArgumentNullException(nameof(codegenProfile));
            }
            if (selectedCodegenOptionValues == null) {
                throw new ArgumentNullException(nameof(selectedCodegenOptionValues));
            }
            if (additionalPreprocessorSymbols == null) {
                throw new ArgumentNullException(nameof(additionalPreprocessorSymbols));
            }
            if (logBuilder == null) {
                throw new ArgumentNullException(nameof(logBuilder));
            }

            List<string> arguments = BuildArguments(
                projectPath,
                outputRootPath,
                platformDefinition,
                codegenProfile,
                selectedCodegenOptionValues,
                additionalPreprocessorSymbols);

            RunProcess(fileName, arguments, Path.GetDirectoryName(fileName) ?? Directory.GetCurrentDirectory(), logBuilder, cancellationToken);
        }

        /// <summary>
        /// Regenerates the additional managed projects declared by one external platform plugin manifest and merges them into generated-core.
        /// </summary>
        /// <param name="generatedCoreProjectPaths">Managed project paths declared for generated-core merging.</param>
        /// <param name="fileName">Path to the codegen executable.</param>
        /// <param name="externalProjectsOutputRoot">Temporary root that receives generated output from the external projects.</param>
        /// <param name="generatedCoreOutputRoot">Combined generated-core output root.</param>
        /// <param name="platformDefinition">Typed platform metadata exposed by the active builder.</param>
        /// <param name="codegenProfile">Selected codegen profile metadata.</param>
        /// <param name="selectedCodegenOptionValues">Selected codegen option values persisted by the editor.</param>
        /// <param name="additionalPreprocessorSymbols">Feature symbols injected for conversion.</param>
        /// <param name="logBuilder">Shared log buffer that records process output for the combined regeneration run.</param>
        /// <param name="cancellationToken">Cancellation token that can stop regeneration cooperatively.</param>
        static void RegenerateAdditionalGeneratedCoreProjects(
            IReadOnlyList<string> generatedCoreProjectPaths,
            string fileName,
            string externalProjectsOutputRoot,
            string generatedCoreOutputRoot,
            PlatformDefinition platformDefinition,
            PlatformCodegenProfileDefinition codegenProfile,
            IReadOnlyDictionary<string, string> selectedCodegenOptionValues,
            IReadOnlyList<string> additionalPreprocessorSymbols,
            StringBuilder logBuilder,
            CancellationToken cancellationToken) {
            if (generatedCoreProjectPaths == null) {
                throw new ArgumentNullException(nameof(generatedCoreProjectPaths));
            }
            if (generatedCoreProjectPaths.Count == 0) {
                return;
            }

            for (int index = 0; index < generatedCoreProjectPaths.Count; index++) {
                string projectPath = generatedCoreProjectPaths[index];
                if (string.IsNullOrWhiteSpace(projectPath)) {
                    throw new InvalidOperationException("Generated-core project paths must not contain empty entries.");
                }
                if (!File.Exists(projectPath)) {
                    throw new FileNotFoundException($"Generated-core managed project '{projectPath}' was not found.", projectPath);
                }

                string projectOutputRoot = Path.Combine(externalProjectsOutputRoot, index.ToString("D2"));
                RegenerateProject(
                    fileName,
                    projectPath,
                    projectOutputRoot,
                    platformDefinition,
                    codegenProfile,
                    selectedCodegenOptionValues,
                    additionalPreprocessorSymbols,
                    logBuilder,
                    cancellationToken);
                MergeExternalGeneratedProjectSourceTree(projectPath, projectOutputRoot, generatedCoreOutputRoot);
                MergeGeneratedConversionReport(projectOutputRoot, generatedCoreOutputRoot);
            }
        }

        /// <summary>
        /// Builds one codegen command-line argument list from the selected platform and codegen settings.
        /// </summary>
        /// <param name="projectPath">Project path to convert.</param>
        /// <param name="outputRootPath">Output tree that receives the generated files.</param>
        /// <param name="platformDefinition">Typed platform metadata exposed by the active builder.</param>
        /// <param name="codegenProfile">Selected codegen profile metadata.</param>
        /// <param name="selectedCodegenOptionValues">Selected codegen option values persisted by the editor.</param>
        /// <param name="additionalPreprocessorSymbols">Feature symbols injected for portable-input compilation.</param>
        /// <returns>Ordered codegen process arguments.</returns>
        internal static List<string> BuildArguments(
            string projectPath,
            string outputRootPath,
            PlatformDefinition platformDefinition,
            PlatformCodegenProfileDefinition codegenProfile,
            IReadOnlyDictionary<string, string> selectedCodegenOptionValues,
            IReadOnlyList<string> additionalPreprocessorSymbols) {
            if (string.IsNullOrWhiteSpace(projectPath)) {
                throw new ArgumentException("Project path must be provided.", nameof(projectPath));
            }
            if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("Output root path must be provided.", nameof(outputRootPath));
            }
            if (platformDefinition == null) {
                throw new ArgumentNullException(nameof(platformDefinition));
            }
            if (codegenProfile == null) {
                throw new ArgumentNullException(nameof(codegenProfile));
            }
            if (selectedCodegenOptionValues == null) {
                throw new ArgumentNullException(nameof(selectedCodegenOptionValues));
            }
            if (additionalPreprocessorSymbols == null) {
                throw new ArgumentNullException(nameof(additionalPreprocessorSymbols));
            }

            List<string> arguments = [
                "--cpp",
                "--project",
                projectPath,
                "--output",
                outputRootPath,
                "--platform",
                platformDefinition.PlatformId,
                "--language",
                codegenProfile.OutputLanguage.ToString().ToLowerInvariant(),
                "--endianness",
                codegenProfile.Endianness == PlatformSerializationEndianness.LittleEndian ? "little" : "big",
                "--set",
                "include-project-defined-preprocessor-symbols=false"
            ];

            if (selectedCodegenOptionValues.TryGetValue(PlatformCodegenSettingIds.PresetId, out string presetId)
                && !string.IsNullOrWhiteSpace(presetId)) {
                arguments.Add("--preset");
                arguments.Add(presetId);
            }

            foreach (KeyValuePair<string, string> selectedOption in selectedCodegenOptionValues.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)) {
                if (string.IsNullOrWhiteSpace(selectedOption.Key)
                    || string.Equals(selectedOption.Key, "additional-preprocessor-symbols", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(selectedOption.Key, PlatformCodegenSettingIds.PresetId, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                arguments.Add("--set");
                arguments.Add($"{selectedOption.Key}={selectedOption.Value}");
            }

            string combinedPreprocessorSymbols = CombinePreprocessorSymbols(
                selectedCodegenOptionValues,
                additionalPreprocessorSymbols);
            if (!string.IsNullOrWhiteSpace(combinedPreprocessorSymbols)) {
                arguments.Add("--set");
                arguments.Add($"additional-preprocessor-symbols={combinedPreprocessorSymbols}");
            }

            return arguments;
        }

        /// <summary>
        /// Returns the feature symbols used by portable input codegen for one platform.
        /// </summary>
        /// <param name="platformDefinition">Typed platform metadata exposed by the active builder.</param>
        /// <returns>Preprocessor symbols that should be supplied to the input codegen run.</returns>
        internal static IReadOnlyList<string> ResolvePortableInputPreprocessorSymbols(PlatformDefinition platformDefinition) {
            return EditorPlatformPreprocessorSymbolService.ResolvePortableInputSymbols(platformDefinition);
        }

        /// <summary>
        /// Combines multiple preprocessor symbol sources into one ordered unique list.
        /// </summary>
        /// <param name="firstSymbols">Primary symbol source.</param>
        /// <param name="secondSymbols">Secondary symbol source.</param>
        /// <returns>Ordered unique symbol list.</returns>
        internal static IReadOnlyList<string> CombineAdditionalPreprocessorSymbols(
            IReadOnlyList<string> firstSymbols,
            IReadOnlyList<string> secondSymbols) {
            if (firstSymbols == null) {
                throw new ArgumentNullException(nameof(firstSymbols));
            }
            if (secondSymbols == null) {
                throw new ArgumentNullException(nameof(secondSymbols));
            }

            List<string> combinedSymbols = new List<string>();
            AddUniqueSymbols(combinedSymbols, firstSymbols);
            AddUniqueSymbols(combinedSymbols, secondSymbols);
            return combinedSymbols;
        }

        /// <summary>
        /// Returns whether scene-derived preprocessor symbols require the 3D physics project in the generated native core.
        /// </summary>
        /// <param name="additionalPreprocessorSymbols">Scene-derived symbols forwarded to generated-core regeneration.</param>
        /// <returns>True when physics scene features require native physics runtime sources.</returns>
        internal static bool ShouldRegeneratePhysics3DProject(IReadOnlyList<string> additionalPreprocessorSymbols) {
            if (additionalPreprocessorSymbols == null) {
                throw new ArgumentNullException(nameof(additionalPreprocessorSymbols));
            }

            for (int index = 0; index < additionalPreprocessorSymbols.Count; index++) {
                string symbol = additionalPreprocessorSymbols[index];
                if (string.Equals(symbol, PhysicsSceneFeatureSymbolCatalog3D.SceneFeatureStrippingSymbol, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Runs one process and throws when it fails.
        /// </summary>
        /// <param name="fileName">Executable path.</param>
        /// <param name="arguments">Process arguments.</param>
        /// <param name="workingDirectory">Current working directory for the process.</param>
        /// <param name="logBuilder">Shared log buffer that receives process output.</param>
        /// <param name="cancellationToken">Cancellation token that can stop the process wait loop.</param>
        static void RunProcess(string fileName, IReadOnlyList<string> arguments, string workingDirectory, StringBuilder logBuilder, CancellationToken cancellationToken) {
            string displayArguments = string.Join(" ", arguments.Select(QuoteArgument));
            ProcessStartInfo startInfo = new ProcessStartInfo {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            for (int index = 0; index < arguments.Count; index++) {
                startInfo.ArgumentList.Add(arguments[index]);
            }

            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start '{fileName}'.");
            process.OutputDataReceived += (_, eventArgs) => {
                if (!string.IsNullOrEmpty(eventArgs.Data)) {
                    logBuilder.AppendLine(eventArgs.Data);
                }
            };
            process.ErrorDataReceived += (_, eventArgs) => {
                if (!string.IsNullOrEmpty(eventArgs.Data)) {
                    logBuilder.AppendLine(eventArgs.Data);
                }
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            while (!process.HasExited) {
                cancellationToken.ThrowIfCancellationRequested();
                process.WaitForExit(100);
            }

            process.WaitForExit();

            if (process.ExitCode != 0) {
                throw new InvalidOperationException($"Process '{fileName} {displayArguments}' failed with exit code {process.ExitCode}.");
            }
        }

        /// <summary>
        /// Deletes one regeneration scratch directory tree when it exists.
        /// </summary>
        /// <param name="path">Scratch directory path to delete.</param>
        static void DeleteDirectoryIfPresent(string path) {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) {
                Directory.Delete(path, true);
            }
        }

        /// <summary>
        /// Copies the portable input source files into the combined native output tree.
        /// </summary>
        /// <param name="sourceRootPath">Temporary portable-input output root.</param>
        /// <param name="destinationRootPath">Combined generated-core output root.</param>
        internal static void MergeGeneratedSourceTree(string sourceRootPath, string destinationRootPath) {
            if (string.IsNullOrWhiteSpace(sourceRootPath)) {
                throw new ArgumentException("Source root path must be provided.", nameof(sourceRootPath));
            }
            if (string.IsNullOrWhiteSpace(destinationRootPath)) {
                throw new ArgumentException("Destination root path must be provided.", nameof(destinationRootPath));
            }

            if (!Directory.Exists(sourceRootPath)) {
                return;
            }

            string[] sourceFiles = Directory.GetFiles(sourceRootPath, "*", SearchOption.AllDirectories);
            Array.Sort(sourceFiles, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < sourceFiles.Length; index++) {
                string sourceFilePath = sourceFiles[index];
                if (!ShouldMergeGeneratedSourceFile(sourceFilePath)) {
                    continue;
                }

                string relativePath = Path.GetRelativePath(sourceRootPath, sourceFilePath);
                string destinationFilePath = Path.Combine(destinationRootPath, relativePath);
                string? destinationDirectoryPath = Path.GetDirectoryName(destinationFilePath);
                if (!string.IsNullOrWhiteSpace(destinationDirectoryPath)) {
                    Directory.CreateDirectory(destinationDirectoryPath);
                }

                File.Copy(sourceFilePath, destinationFilePath, true);
            }
        }

        /// <summary>
        /// Merges only the generated native files that correspond to source files physically owned by one external generated-core project.
        /// </summary>
        /// <param name="projectPath">Managed project path that produced the generated source tree.</param>
        /// <param name="sourceRootPath">Generated output root for the external project.</param>
        /// <param name="destinationRootPath">Combined generated-core output root.</param>
        internal static void MergeExternalGeneratedProjectSourceTree(string projectPath, string sourceRootPath, string destinationRootPath) {
            if (string.IsNullOrWhiteSpace(projectPath)) {
                throw new ArgumentException("Project path must be provided.", nameof(projectPath));
            }
            if (string.IsNullOrWhiteSpace(sourceRootPath)) {
                throw new ArgumentException("Source root path must be provided.", nameof(sourceRootPath));
            }
            if (string.IsNullOrWhiteSpace(destinationRootPath)) {
                throw new ArgumentException("Destination root path must be provided.", nameof(destinationRootPath));
            }
            if (!Directory.Exists(sourceRootPath)) {
                return;
            }

            string projectDirectoryPath = Path.GetDirectoryName(projectPath)
                ?? throw new InvalidOperationException($"Could not resolve the directory for generated-core project '{projectPath}'.");
            HashSet<string> ownedGeneratedBaseNames = DiscoverExternalGeneratedProjectOwnedBaseNames(projectDirectoryPath);
            if (ownedGeneratedBaseNames.Count < 1) {
                return;
            }

            string[] sourceFiles = Directory.GetFiles(sourceRootPath, "*", SearchOption.AllDirectories);
            Array.Sort(sourceFiles, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> sourceFilesByFileName = BuildGeneratedSourceLookup(sourceFiles);
            HashSet<string> copiedRelativePaths = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < sourceFiles.Length; index++) {
                string sourceFilePath = sourceFiles[index];
                if (!ShouldMergeGeneratedSourceFile(sourceFilePath)) {
                    continue;
                }

                string sourceBaseName = Path.GetFileNameWithoutExtension(sourceFilePath);
                if (!ownedGeneratedBaseNames.Contains(sourceBaseName)) {
                    continue;
                }

                CopyGeneratedSourceFile(sourceRootPath, destinationRootPath, sourceFilePath, copiedRelativePaths);
            }

            CopyTransitiveExternalGeneratedDependencies(sourceRootPath, destinationRootPath, sourceFilesByFileName, copiedRelativePaths);
        }

        /// <summary>
        /// Discovers the generated native base names that belong to one external managed project by enumerating the project-owned source files under its directory.
        /// </summary>
        /// <param name="projectDirectoryPath">Directory that owns the external generated-core project file.</param>
        /// <returns>Set of generated native file base names that should be merged into generated-core.</returns>
        static HashSet<string> DiscoverExternalGeneratedProjectOwnedBaseNames(string projectDirectoryPath) {
            if (string.IsNullOrWhiteSpace(projectDirectoryPath)) {
                throw new ArgumentException("Project directory path must be provided.", nameof(projectDirectoryPath));
            }
            if (!Directory.Exists(projectDirectoryPath)) {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            HashSet<string> ownedBaseNames = new(StringComparer.Ordinal);
            string[] sourceFiles = Directory.GetFiles(projectDirectoryPath, "*.cs", SearchOption.AllDirectories);
            Array.Sort(sourceFiles, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < sourceFiles.Length; index++) {
                string sourceFilePath = sourceFiles[index];
                if (IsGeneratedCoreProjectInfrastructurePath(sourceFilePath)) {
                    continue;
                }

                ownedBaseNames.Add(Path.GetFileNameWithoutExtension(sourceFilePath));
            }

            return ownedBaseNames;
        }

        /// <summary>
        /// Returns whether one source path belongs to build output or intermediate directories that should not influence external generated-core ownership.
        /// </summary>
        /// <param name="sourceFilePath">Absolute source file path under the external project directory.</param>
        /// <returns>True when the file belongs to infrastructure directories rather than authored project code.</returns>
        static bool IsGeneratedCoreProjectInfrastructurePath(string sourceFilePath) {
            if (string.IsNullOrWhiteSpace(sourceFilePath)) {
                return true;
            }

            string normalizedPath = sourceFilePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            string binSegment = Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar;
            if (normalizedPath.IndexOf(binSegment, StringComparison.OrdinalIgnoreCase) >= 0) {
                return true;
            }

            string objSegment = Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar;
            if (normalizedPath.IndexOf(objSegment, StringComparison.OrdinalIgnoreCase) >= 0) {
                return true;
            }

            string pluginSegment = Path.DirectorySeparatorChar + "Plugin" + Path.DirectorySeparatorChar;
            if (normalizedPath.IndexOf(pluginSegment, StringComparison.OrdinalIgnoreCase) >= 0) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Builds one lookup keyed by generated file name for all source artifacts emitted by one generated project conversion.
        /// </summary>
        /// <param name="sourceFiles">All generated source artifacts emitted for one project.</param>
        /// <returns>Dictionary keyed by generated file name.</returns>
        static Dictionary<string, string> BuildGeneratedSourceLookup(string[] sourceFiles) {
            Dictionary<string, string> sourceFilesByFileName = new(StringComparer.Ordinal);
            for (int index = 0; index < sourceFiles.Length; index++) {
                string sourceFilePath = sourceFiles[index];
                if (!ShouldMergeGeneratedSourceFile(sourceFilePath)) {
                    continue;
                }

                string fileName = Path.GetFileName(sourceFilePath);
                if (!sourceFilesByFileName.ContainsKey(fileName)) {
                    sourceFilesByFileName.Add(fileName, sourceFilePath);
                }
            }

            return sourceFilesByFileName;
        }

        /// <summary>
        /// Copies one generated source artifact into the combined generated-core root and tracks the copied relative path for dependency expansion.
        /// </summary>
        /// <param name="sourceRootPath">Generated output root for the external project.</param>
        /// <param name="destinationRootPath">Combined generated-core output root.</param>
        /// <param name="sourceFilePath">Generated source artifact that should be copied.</param>
        /// <param name="copiedRelativePaths">Relative paths already copied from the external project output.</param>
        static void CopyGeneratedSourceFile(
            string sourceRootPath,
            string destinationRootPath,
            string sourceFilePath,
            HashSet<string> copiedRelativePaths) {
            string relativePath = Path.GetRelativePath(sourceRootPath, sourceFilePath);
            string destinationFilePath = Path.Combine(destinationRootPath, relativePath);
            string? destinationDirectoryPath = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrWhiteSpace(destinationDirectoryPath)) {
                Directory.CreateDirectory(destinationDirectoryPath);
            }

            File.Copy(sourceFilePath, destinationFilePath, true);
            copiedRelativePaths.Add(relativePath.Replace('\\', '/'));
        }

        /// <summary>
        /// Copies only the generated dependency files that are actually referenced by the external project artifacts already merged into generated-core.
        /// </summary>
        /// <param name="sourceRootPath">Generated output root for the external project.</param>
        /// <param name="destinationRootPath">Combined generated-core output root.</param>
        /// <param name="sourceFilesByFileName">Generated source lookup keyed by file name.</param>
        /// <param name="copiedRelativePaths">Relative paths already copied from the external project output.</param>
        static void CopyTransitiveExternalGeneratedDependencies(
            string sourceRootPath,
            string destinationRootPath,
            Dictionary<string, string> sourceFilesByFileName,
            HashSet<string> copiedRelativePaths) {
            Queue<string> pendingRelativePaths = new Queue<string>(copiedRelativePaths.OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
            while (pendingRelativePaths.Count > 0) {
                string relativePath = pendingRelativePaths.Dequeue();
                string destinationFilePath = Path.Combine(destinationRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(destinationFilePath)) {
                    continue;
                }

                string extension = Path.GetExtension(destinationFilePath);
                if (!string.Equals(extension, ".cpp", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(extension, ".hpp", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                string[] lines = File.ReadAllLines(destinationFilePath);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++) {
                    string includeFileName = TryParseQuotedIncludeFileName(lines[lineIndex]);
                    if (string.IsNullOrWhiteSpace(includeFileName)) {
                        continue;
                    }
                    if (includeFileName.IndexOf('/') >= 0 || includeFileName.IndexOf('\\') >= 0) {
                        continue;
                    }
                    if (!includeFileName.EndsWith(".hpp", StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }
                    if (File.Exists(Path.Combine(destinationRootPath, includeFileName))) {
                        continue;
                    }
                    if (!sourceFilesByFileName.TryGetValue(includeFileName, out string dependencyHeaderPath)) {
                        continue;
                    }

                    string dependencyHeaderRelativePath = Path.GetRelativePath(sourceRootPath, dependencyHeaderPath).Replace('\\', '/');
                    if (!copiedRelativePaths.Contains(dependencyHeaderRelativePath)) {
                        CopyGeneratedSourceFile(sourceRootPath, destinationRootPath, dependencyHeaderPath, copiedRelativePaths);
                        pendingRelativePaths.Enqueue(dependencyHeaderRelativePath);
                    }

                    string dependencySourceFileName = Path.GetFileNameWithoutExtension(includeFileName) + ".cpp";
                    if (!sourceFilesByFileName.TryGetValue(dependencySourceFileName, out string dependencySourcePath)) {
                        continue;
                    }

                    string dependencySourceRelativePath = Path.GetRelativePath(sourceRootPath, dependencySourcePath).Replace('\\', '/');
                    if (copiedRelativePaths.Contains(dependencySourceRelativePath)) {
                        continue;
                    }

                    CopyGeneratedSourceFile(sourceRootPath, destinationRootPath, dependencySourcePath, copiedRelativePaths);
                    pendingRelativePaths.Enqueue(dependencySourceRelativePath);
                }
            }
        }

        /// <summary>
        /// Merges one generated conversion report into the combined generated-core report so feature summaries and native feature manifests reflect all merged projects.
        /// </summary>
        /// <param name="sourceRootPath">Temporary generated output root that may contain a conversion report.</param>
        /// <param name="destinationRootPath">Combined generated-core output root that should receive the merged report.</param>
        internal static void MergeGeneratedConversionReport(string sourceRootPath, string destinationRootPath) {
            if (string.IsNullOrWhiteSpace(sourceRootPath)) {
                throw new ArgumentException("Source root path must be provided.", nameof(sourceRootPath));
            }
            if (string.IsNullOrWhiteSpace(destinationRootPath)) {
                throw new ArgumentException("Destination root path must be provided.", nameof(destinationRootPath));
            }

            string sourceReportPath = Path.Combine(sourceRootPath, "cpp-conversion-report.json");
            if (!File.Exists(sourceReportPath)) {
                return;
            }

            string destinationReportPath = Path.Combine(destinationRootPath, "cpp-conversion-report.json");
            if (!File.Exists(destinationReportPath)) {
                File.Copy(sourceReportPath, destinationReportPath, false);
                return;
            }

            JsonObject sourceReport = JsonNode.Parse(File.ReadAllText(sourceReportPath)) as JsonObject
                ?? throw new InvalidOperationException($"Generated conversion report '{sourceReportPath}' did not contain a valid JSON object.");
            JsonObject destinationReport = JsonNode.Parse(File.ReadAllText(destinationReportPath)) as JsonObject
                ?? throw new InvalidOperationException($"Generated conversion report '{destinationReportPath}' did not contain a valid JSON object.");

            JsonObject destinationBuildFeatures = GetOrCreateBuildFeaturesObject(destinationReport);
            JsonObject sourceBuildFeatures = GetOrCreateBuildFeaturesObject(sourceReport);
            MergeFeatureDecisionArray(destinationBuildFeatures, sourceBuildFeatures);
            MergeFeatureMetadataArray(destinationBuildFeatures, sourceBuildFeatures, "detectedRoots");
            MergeFeatureMetadataArray(destinationBuildFeatures, sourceBuildFeatures, "conflicts");

            File.WriteAllText(destinationReportPath, destinationReport.ToJsonString(new JsonSerializerOptions {
                WriteIndented = true
            }));
        }

        /// <summary>
        /// Copies bundled codegen runtime support files into the generated output tree when conversion output omitted them.
        /// </summary>
        /// <param name="bundledRuntimeSupportRootPath">Bundled runtime support root that ships beside the codegen executable.</param>
        /// <param name="destinationRootPath">Combined generated-core output root that must become self-contained.</param>
        internal static void MergeBundledRuntimeSupportTree(string bundledRuntimeSupportRootPath, string destinationRootPath) {
            if (string.IsNullOrWhiteSpace(bundledRuntimeSupportRootPath)) {
                throw new ArgumentException("Bundled runtime support root path must be provided.", nameof(bundledRuntimeSupportRootPath));
            }
            if (string.IsNullOrWhiteSpace(destinationRootPath)) {
                throw new ArgumentException("Destination root path must be provided.", nameof(destinationRootPath));
            }

            if (!Directory.Exists(bundledRuntimeSupportRootPath)) {
                return;
            }

            string[] sourceFiles = Directory.GetFiles(bundledRuntimeSupportRootPath, "*", SearchOption.AllDirectories);
            Array.Sort(sourceFiles, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < sourceFiles.Length; index++) {
                string sourceFilePath = sourceFiles[index];
                if (!ShouldMergeGeneratedSourceFile(sourceFilePath)) {
                    continue;
                }

                string relativePath = Path.GetRelativePath(bundledRuntimeSupportRootPath, sourceFilePath);
                string destinationFilePath = Path.Combine(destinationRootPath, relativePath);
                if (File.Exists(destinationFilePath)) {
                    continue;
                }

                string? destinationDirectoryPath = Path.GetDirectoryName(destinationFilePath);
                if (!string.IsNullOrWhiteSpace(destinationDirectoryPath)) {
                    Directory.CreateDirectory(destinationDirectoryPath);
                }

                File.Copy(sourceFilePath, destinationFilePath, false);
            }
        }

        /// <summary>
        /// Ensures generated runtime component deserializer support files exist in one generated core source tree.
        /// </summary>
        /// <param name="generatedCoreRootPath">Absolute path to the generated core output root.</param>
        /// <param name="platformId">Target platform identifier reserved for future generator-owned specialization hooks.</param>
        internal static void EnsureGeneratedRuntimeComponentDeserializerSupport(string generatedCoreRootPath, string platformId) {
            if (string.IsNullOrWhiteSpace(generatedCoreRootPath)) {
                throw new ArgumentException("Generated core root path must be provided.", nameof(generatedCoreRootPath));
            }

            if (!Directory.Exists(generatedCoreRootPath)) {
                return;
            }

            string generatedRuntimeComponentRegistrationSourcePath = Path.Combine(
                generatedCoreRootPath,
                "GeneratedRuntimeComponentDeserializerRegistration.cpp");
            if (!File.Exists(generatedRuntimeComponentRegistrationSourcePath)) {
                EmitGeneratedAutomaticRuntimeComponentDeserializers(generatedCoreRootPath);
            }
        }

        /// <summary>
        /// Emits root-level compatibility headers for generated include statements that target PascalCase filenames while the merged runtime support tree ships only nested snake_case headers.
        /// </summary>
        /// <param name="generatedCoreRootPath">Absolute generated core output root that should receive missing include shims.</param>
        internal static void EnsureGeneratedIncludeCompatibilityShims(string generatedCoreRootPath) {
            if (string.IsNullOrWhiteSpace(generatedCoreRootPath)) {
                throw new ArgumentException("Generated core root path must be provided.", nameof(generatedCoreRootPath));
            }
            if (!Directory.Exists(generatedCoreRootPath)) {
                return;
            }

            string[] generatedSourceFiles = Directory.GetFiles(generatedCoreRootPath, "*", SearchOption.AllDirectories);
            Dictionary<string, string> availableHeadersByFileName = BuildGeneratedHeaderLookup(generatedCoreRootPath, generatedSourceFiles);
            HashSet<string> missingHeaderNames = DiscoverMissingGeneratedHeaderIncludes(generatedCoreRootPath, generatedSourceFiles, availableHeadersByFileName);
            if (missingHeaderNames.Count < 1) {
                return;
            }

            string[] availableHeaders = Directory.GetFiles(generatedCoreRootPath, "*.hpp", SearchOption.AllDirectories);
            for (int index = 0; index < availableHeaders.Length; index++) {
                string headerPath = availableHeaders[index];
                string headerFileName = Path.GetFileName(headerPath);
                if (!availableHeadersByFileName.ContainsKey(headerFileName)) {
                    availableHeadersByFileName.Add(headerFileName, headerPath);
                }
            }

            foreach (string missingHeaderName in missingHeaderNames.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)) {
                if (availableHeadersByFileName.ContainsKey(missingHeaderName)) {
                    continue;
                }

                string canonicalHeaderFileName = ToSnakeCaseHeaderFileName(missingHeaderName);
                string resolvedHeaderPath = ResolveCompatibilityHeaderSourcePath(generatedCoreRootPath, availableHeaders, canonicalHeaderFileName);
                if (string.IsNullOrWhiteSpace(resolvedHeaderPath)) {
                    continue;
                }

                string shimHeaderPath = Path.Combine(generatedCoreRootPath, missingHeaderName);
                string relativeIncludePath = Path.GetRelativePath(generatedCoreRootPath, resolvedHeaderPath).Replace('\\', '/');
                File.WriteAllText(
                    shimHeaderPath,
                    "#pragma once" + Environment.NewLine
                    + "#include \"" + relativeIncludePath + "\"" + Environment.NewLine);
                availableHeadersByFileName[missingHeaderName] = shimHeaderPath;
            }
        }

        /// <summary>
        /// Builds one file-name lookup for generated root headers so include resolution can detect what already exists.
        /// </summary>
        /// <param name="generatedCoreRootPath">Absolute generated core output root.</param>
        /// <param name="generatedSourceFiles">All generated files currently present under the output root.</param>
        /// <returns>Dictionary keyed by case-sensitive header file name.</returns>
        static Dictionary<string, string> BuildGeneratedHeaderLookup(string generatedCoreRootPath, string[] generatedSourceFiles) {
            Dictionary<string, string> headersByFileName = new(StringComparer.Ordinal);
            for (int index = 0; index < generatedSourceFiles.Length; index++) {
                string filePath = generatedSourceFiles[index];
                if (!string.Equals(Path.GetExtension(filePath), ".hpp", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                string relativePath = Path.GetRelativePath(generatedCoreRootPath, filePath);
                if (relativePath.IndexOf(Path.DirectorySeparatorChar) >= 0 || relativePath.IndexOf(Path.AltDirectorySeparatorChar) >= 0) {
                    continue;
                }

                string fileName = Path.GetFileName(filePath);
                if (!headersByFileName.ContainsKey(fileName)) {
                    headersByFileName.Add(fileName, filePath);
                }
            }

            return headersByFileName;
        }

        /// <summary>
        /// Discovers generated root-header includes whose target files are not currently present at the generated core root.
        /// </summary>
        /// <param name="generatedCoreRootPath">Absolute generated core output root.</param>
        /// <param name="generatedSourceFiles">All generated files currently present under the output root.</param>
        /// <param name="availableHeadersByFileName">Existing root-header lookup keyed by file name.</param>
        /// <returns>Set of missing PascalCase header names requested by generated code.</returns>
        static HashSet<string> DiscoverMissingGeneratedHeaderIncludes(
            string generatedCoreRootPath,
            string[] generatedSourceFiles,
            Dictionary<string, string> availableHeadersByFileName) {
            HashSet<string> missingHeaderNames = new(StringComparer.Ordinal);
            for (int fileIndex = 0; fileIndex < generatedSourceFiles.Length; fileIndex++) {
                string filePath = generatedSourceFiles[fileIndex];
                string extension = Path.GetExtension(filePath);
                if (!string.Equals(extension, ".cpp", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(extension, ".hpp", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                string[] lines = File.ReadAllLines(filePath);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++) {
                    string includeFileName = TryParseQuotedIncludeFileName(lines[lineIndex]);
                    if (string.IsNullOrWhiteSpace(includeFileName)) {
                        continue;
                    }
                    if (includeFileName.IndexOf('/') >= 0 || includeFileName.IndexOf('\\') >= 0) {
                        continue;
                    }
                    if (!includeFileName.EndsWith(".hpp", StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }
                    if (availableHeadersByFileName.ContainsKey(includeFileName)) {
                        continue;
                    }

                    string rootHeaderPath = Path.Combine(generatedCoreRootPath, includeFileName);
                    if (File.Exists(rootHeaderPath)) {
                        availableHeadersByFileName[includeFileName] = rootHeaderPath;
                        continue;
                    }

                    missingHeaderNames.Add(includeFileName);
                }
            }

            return missingHeaderNames;
        }

        /// <summary>
        /// Resolves one missing root header include to a unique snake_case runtime support header already present under generated-core.
        /// </summary>
        /// <param name="generatedCoreRootPath">Absolute generated core output root.</param>
        /// <param name="availableHeaders">All merged header files under generated-core.</param>
        /// <param name="canonicalHeaderFileName">Canonical snake_case header file name expected on disk.</param>
        /// <returns>Absolute path to the unique backing header, or an empty string when no safe match exists.</returns>
        static string ResolveCompatibilityHeaderSourcePath(string generatedCoreRootPath, string[] availableHeaders, string canonicalHeaderFileName) {
            if (string.IsNullOrWhiteSpace(canonicalHeaderFileName)) {
                return string.Empty;
            }

            string resolvedPath = string.Empty;
            for (int index = 0; index < availableHeaders.Length; index++) {
                string headerPath = availableHeaders[index];
                if (!string.Equals(Path.GetFileName(headerPath), canonicalHeaderFileName, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                string relativePath = Path.GetRelativePath(generatedCoreRootPath, headerPath).Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(resolvedPath)) {
                    resolvedPath = headerPath;
                    continue;
                }

                string resolvedRelativePath = Path.GetRelativePath(generatedCoreRootPath, resolvedPath).Replace('\\', '/');
                if (!string.Equals(resolvedRelativePath, relativePath, StringComparison.OrdinalIgnoreCase)) {
                    return string.Empty;
                }
            }

            return resolvedPath;
        }

        /// <summary>
        /// Parses one quoted include directive and returns its included file name when the line declares one.
        /// </summary>
        /// <param name="sourceLine">One generated C++ source line.</param>
        /// <returns>Included file name when the line is a quoted include; otherwise an empty string.</returns>
        static string TryParseQuotedIncludeFileName(string sourceLine) {
            if (string.IsNullOrWhiteSpace(sourceLine)) {
                return string.Empty;
            }

            int includeIndex = sourceLine.IndexOf("#include \"", StringComparison.Ordinal);
            if (includeIndex < 0) {
                return string.Empty;
            }

            int pathStartIndex = includeIndex + "#include \"".Length;
            int pathEndIndex = sourceLine.IndexOf('"', pathStartIndex);
            if (pathEndIndex <= pathStartIndex) {
                return string.Empty;
            }

            return sourceLine.Substring(pathStartIndex, pathEndIndex - pathStartIndex);
        }

        /// <summary>
        /// Converts one PascalCase generated header request into the snake_case runtime-support filename produced by the codegen support tree.
        /// </summary>
        /// <param name="headerFileName">Generated header file name requested by one include directive.</param>
        /// <returns>Snake_case header file name expected on disk.</returns>
        static string ToSnakeCaseHeaderFileName(string headerFileName) {
            if (string.IsNullOrWhiteSpace(headerFileName)) {
                return string.Empty;
            }

            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(headerFileName);
            StringBuilder builder = new StringBuilder(fileNameWithoutExtension.Length + 8);
            for (int index = 0; index < fileNameWithoutExtension.Length; index++) {
                char current = fileNameWithoutExtension[index];
                if (char.IsUpper(current) && index > 0) {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }

            builder.Append(".hpp");
            return builder.ToString();
        }

        /// <summary>
        /// Emits generated native runtime component deserializers for engine-owned components that use automatic packaged scene persistence.
        /// </summary>
        /// <param name="generatedCoreRootPath">Absolute generated core output root that should receive the generated native files.</param>
        internal static void EmitGeneratedAutomaticRuntimeComponentDeserializers(string generatedCoreRootPath, IReadOnlyList<Type> additionalComponentTypes = null) {
            if (string.IsNullOrWhiteSpace(generatedCoreRootPath)) {
                throw new ArgumentException("Generated core root path must be provided.", nameof(generatedCoreRootPath));
            }

            Directory.CreateDirectory(generatedCoreRootPath);
            ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator();
            ScriptComponentReflectionSchemaBuilder schemaBuilder = new ScriptComponentReflectionSchemaBuilder();
            IReadOnlyList<ScriptComponentReflectionSchema> schemas = DiscoverAutomaticRuntimeComponentSchemas(schemaBuilder, generator, additionalComponentTypes);
            if (schemas.Count == 0) {
                return;
            }

            for (int index = 0; index < schemas.Count; index++) {
                ScriptComponentReflectionSchema schema = schemas[index];
                string className = generator.BuildNativeDeserializerClassName(schema);
                File.WriteAllText(
                    Path.Combine(generatedCoreRootPath, className + ".hpp"),
                    generator.GenerateNativeDeserializerHeader(schema));
                File.WriteAllText(
                    Path.Combine(generatedCoreRootPath, className + ".cpp"),
                    generator.GenerateNativeDeserializerSource(schema));
            }

            File.WriteAllText(
                Path.Combine(generatedCoreRootPath, "GeneratedRuntimeComponentDeserializerRegistration.hpp"),
                BuildGeneratedRuntimeComponentDeserializerRegistrationHeader());
            File.WriteAllText(
                Path.Combine(generatedCoreRootPath, "GeneratedRuntimeComponentDeserializerRegistration.cpp"),
                BuildGeneratedRuntimeComponentDeserializerRegistrationSource(schemas, generator));
        }


        /// <summary>
        /// Regenerates automatic native runtime component deserializers for the assembly-qualified scripted component types referenced by cooked scenes.
        /// </summary>
        /// <param name="generatedCoreRootPath">Absolute generated core output root that should receive the generated native files.</param>
        /// <param name="cookedSceneAssetPaths">Cooked scene asset paths whose serialized component records should drive generated deserializer coverage.</param>
        /// <param name="scriptTypeResolver">Optional shared script type resolver used for loaded gameplay modules.</param>
        internal static void EmitCookedSceneAutomaticRuntimeComponentDeserializers(
            string generatedCoreRootPath,
            IReadOnlyList<string> cookedSceneAssetPaths,
            IScriptTypeResolver scriptTypeResolver) {
            if (string.IsNullOrWhiteSpace(generatedCoreRootPath)) {
                throw new ArgumentException("Generated core root path must be provided.", nameof(generatedCoreRootPath));
            }
            if (cookedSceneAssetPaths == null) {
                throw new ArgumentNullException(nameof(cookedSceneAssetPaths));
            }

            EmitGeneratedAutomaticRuntimeComponentDeserializers(
                generatedCoreRootPath,
                DiscoverAutomaticRuntimeComponentTypesFromCookedScenes(cookedSceneAssetPaths, scriptTypeResolver));
        }

        /// <summary>
        /// Discovers the component schemas that can participate in generated native runtime deserializer emission.
        /// </summary>
        /// <param name="schemaBuilder">Reflected schema builder used for component discovery.</param>
        /// <param name="generator">Native deserializer generator used to validate supported schemas.</param>
        /// <param name="additionalComponentTypes">Additional scene-referenced scripted component types that must participate in generated native runtime deserializer emission.</param>
        /// <returns>Deterministically ordered schemas eligible for generated native runtime deserializer emission.</returns>
        static IReadOnlyList<ScriptComponentReflectionSchema> DiscoverAutomaticRuntimeComponentSchemas(
            ScriptComponentReflectionSchemaBuilder schemaBuilder,
            ScriptComponentPlayerDeserializerGenerator generator,
            IReadOnlyList<Type> additionalComponentTypes) {
            if (schemaBuilder == null) {
                throw new ArgumentNullException(nameof(schemaBuilder));
            }
            if (generator == null) {
                throw new ArgumentNullException(nameof(generator));
            }

            HashSet<Type> requiredAdditionalComponentTypes = new HashSet<Type>();
            if (additionalComponentTypes != null) {
                for (int index = 0; index < additionalComponentTypes.Count; index++) {
                    Type additionalComponentType = additionalComponentTypes[index];
                    if (additionalComponentType == null) {
                        continue;
                    }
                    if (!IsEligibleAutomaticRuntimeComponentType(additionalComponentType)) {
                        throw new InvalidOperationException($"Scene-referenced scripted component type '{additionalComponentType.FullName}' is not eligible for automatic native runtime deserializer generation.");
                    }

                    requiredAdditionalComponentTypes.Add(additionalComponentType);
                }
            }

            HashSet<Type> componentTypes = new HashSet<Type>(
                typeof(Component).Assembly
                .GetTypes()
                .Where(IsEligibleAutomaticRuntimeComponentType)
                .OrderBy(type => type.FullName, StringComparer.Ordinal));
            foreach (Type additionalComponentType in requiredAdditionalComponentTypes) {
                componentTypes.Add(additionalComponentType);
            }

            List<Type> orderedComponentTypes = componentTypes
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToList();
            List<ScriptComponentReflectionSchema> schemas = new List<ScriptComponentReflectionSchema>(orderedComponentTypes.Count);
            for (int index = 0; index < orderedComponentTypes.Count; index++) {
                Type componentType = orderedComponentTypes[index];
                ScriptComponentReflectionSchema schema = schemaBuilder.Build(componentType);
                if (generator.CanGenerateNativeDeserializer(schema)) {
                    schemas.Add(schema);
                    continue;
                }
                if (requiredAdditionalComponentTypes.Contains(componentType)) {
                    throw new InvalidOperationException($"Native runtime deserializer generation does not support scene-referenced scripted component type '{componentType.FullName}'.");
                }
            }

            return schemas;
        }

        /// <summary>
        /// Discovers the assembly-qualified automatic scripted component runtime types referenced by cooked scene payloads.
        /// </summary>
        /// <param name="cookedSceneAssetPaths">Cooked scene asset paths whose serialized component records should be inspected.</param>
        /// <param name="scriptTypeResolver">Optional shared script type resolver used for loaded gameplay modules.</param>
        /// <returns>Distinct scene-referenced automatic scripted component runtime types.</returns>
        internal static IReadOnlyList<Type> DiscoverAutomaticRuntimeComponentTypesFromCookedScenes(
            IReadOnlyList<string> cookedSceneAssetPaths,
            IScriptTypeResolver scriptTypeResolver) {
            if (cookedSceneAssetPaths == null) {
                throw new ArgumentNullException(nameof(cookedSceneAssetPaths));
            }

            HashSet<Type> componentTypes = new HashSet<Type>();
            for (int index = 0; index < cookedSceneAssetPaths.Count; index++) {
                string cookedSceneAssetPath = cookedSceneAssetPaths[index];
                if (string.IsNullOrWhiteSpace(cookedSceneAssetPath)) {
                    continue;
                }
                if (!File.Exists(cookedSceneAssetPath)) {
                    throw new FileNotFoundException($"Cooked scene asset '{cookedSceneAssetPath}' was not found.", cookedSceneAssetPath);
                }

                try {
                    using FileStream stream = File.OpenRead(cookedSceneAssetPath);
                    Asset asset = AssetSerializer.Deserialize(stream);
                    if (asset is not SceneAsset sceneAsset) {
                        throw new InvalidOperationException($"Cooked scene '{cookedSceneAssetPath}' did not deserialize into a SceneAsset.");
                    }

                    CollectAutomaticRuntimeComponentTypes(sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>(), scriptTypeResolver, componentTypes);
                } catch (Exception ex) when (ex is not InvalidOperationException || !ex.Message.Contains(cookedSceneAssetPath, StringComparison.Ordinal)) {
                    throw new InvalidOperationException($"Cooked scene asset '{cookedSceneAssetPath}' could not be deserialized while discovering automatic runtime components.", ex);
                }
            }

            return componentTypes
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Discovers the distinct runtime module ids referenced by scripted components in the supplied cooked scenes.
        /// </summary>
        /// <param name="cookedSceneAssetPaths">Cooked scene asset paths whose serialized component records should be inspected.</param>
        /// <param name="scriptTypeResolver">Optional shared script type resolver used for loaded gameplay modules.</param>
        /// <returns>Distinct scene-referenced runtime module ids.</returns>
        internal static IReadOnlyList<string> DiscoverReferencedRuntimeModuleIdsFromCookedScenes(
            IReadOnlyList<string> cookedSceneAssetPaths,
            IScriptTypeResolver scriptTypeResolver) {
            IReadOnlyList<Type> componentTypes = DiscoverAutomaticRuntimeComponentTypesFromCookedScenes(
                cookedSceneAssetPaths,
                scriptTypeResolver);
            SortedSet<string> moduleIds = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < componentTypes.Count; index++) {
                Type componentType = componentTypes[index];
                string moduleId = componentType.Assembly.GetName().Name ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(moduleId)) {
                    moduleIds.Add(moduleId);
                }
            }

            return [.. moduleIds];
        }

        /// <summary>
        /// Recursively collects the assembly-qualified automatic scripted component types referenced by one entity tree.
        /// </summary>
        /// <param name="entities">Entity tree whose serialized component records should be inspected.</param>
        /// <param name="scriptTypeResolver">Optional shared script type resolver used for loaded gameplay modules.</param>
        /// <param name="componentTypes">Set that receives distinct scene-referenced component types.</param>
        static void CollectAutomaticRuntimeComponentTypes(
            IReadOnlyList<SceneEntityAsset> entities,
            IScriptTypeResolver scriptTypeResolver,
            HashSet<Type> componentTypes) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            }
            if (componentTypes == null) {
                throw new ArgumentNullException(nameof(componentTypes));
            }

            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
                SceneEntityAsset entity = entities[entityIndex];
                if (entity == null) {
                    continue;
                }

                SceneComponentAssetRecord[] components = entity.Components ?? Array.Empty<SceneComponentAssetRecord>();
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++) {
                    SceneComponentAssetRecord componentRecord = components[componentIndex];
                    if (componentRecord == null || string.IsNullOrWhiteSpace(componentRecord.ComponentTypeId) || !componentRecord.ComponentTypeId.Contains(',')) {
                        continue;
                    }
                    Type componentType = ResolveAutomaticRuntimeComponentType(componentRecord.ComponentTypeId, scriptTypeResolver);
                    if (!typeof(Component).IsAssignableFrom(componentType)) {
                        throw new InvalidOperationException($"Scene-referenced scripted component type '{componentRecord.ComponentTypeId}' does not derive from Component.");
                    }

                    componentTypes.Add(componentType);
                }

                CollectAutomaticRuntimeComponentTypes(entity.Children ?? Array.Empty<SceneEntityAsset>(), scriptTypeResolver, componentTypes);
            }
        }

        /// <summary>
        /// Resolves one assembly-qualified automatic scripted component type id back to the loaded runtime type.
        /// </summary>
        /// <param name="componentTypeId">Assembly-qualified scripted component type id.</param>
        /// <param name="scriptTypeResolver">Optional shared script type resolver used for loaded gameplay modules.</param>
        /// <returns>Resolved scripted component runtime type.</returns>
        static Type ResolveAutomaticRuntimeComponentType(string componentTypeId, IScriptTypeResolver scriptTypeResolver) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                throw new ArgumentException("Component type id must be provided.", nameof(componentTypeId));
            }

            Type componentType = Type.GetType(componentTypeId, false);
            if (componentType == null && scriptTypeResolver != null) {
                componentType = scriptTypeResolver.Resolve(componentTypeId);
            }
            if (componentType == null) {
                throw new InvalidOperationException($"Scene-referenced scripted component type '{componentTypeId}' could not be resolved for native runtime deserializer generation.");
            }

            return componentType;
        }

        /// <summary>
        /// Returns whether one engine-owned component type is eligible for generated native runtime deserializer emission.
        /// </summary>
        /// <param name="componentType">Component type to inspect.</param>
        /// <returns>True when the component type can participate in generated native runtime deserializer emission.</returns>
        static bool IsEligibleAutomaticRuntimeComponentType(Type componentType) {
            if (componentType == null) {
                return false;
            }
            if (componentType == typeof(Component) || componentType == typeof(UpdateComponent)) {
                return false;
            }
            if (!typeof(Component).IsAssignableFrom(componentType)) {
                return false;
            }
            if (!componentType.IsClass || componentType.IsAbstract || componentType.ContainsGenericParameters) {
                return false;
            }
            if (string.IsNullOrWhiteSpace(componentType.FullName)) {
                return false;
            }

            return componentType.GetConstructor(Type.EmptyTypes) != null;
        }

        /// <summary>
        /// Builds the generated native registration header used to install all emitted automatic runtime component deserializers.
        /// </summary>
        /// <returns>Generated native registration header text.</returns>
        static string BuildGeneratedRuntimeComponentDeserializerRegistrationHeader() {
            return "#pragma once" + Environment.NewLine
                + "#ifdef DrawText" + Environment.NewLine
                + "#undef DrawText" + Environment.NewLine
                + "#endif" + Environment.NewLine
                + "class RuntimeComponentRegistry;" + Environment.NewLine + Environment.NewLine
                + "void RegisterGeneratedRuntimeComponentDeserializers(::RuntimeComponentRegistry* registry);" + Environment.NewLine;
        }

        /// <summary>
        /// Builds the generated native registration source used to install all emitted automatic runtime component deserializers.
        /// </summary>
        /// <param name="schemas">Reflected component schemas whose generated deserializers should be registered.</param>
        /// <param name="generator">Native deserializer generator used to resolve generated class names.</param>
        /// <returns>Generated native registration source text.</returns>
        static string BuildGeneratedRuntimeComponentDeserializerRegistrationSource(
            IReadOnlyList<ScriptComponentReflectionSchema> schemas,
            ScriptComponentPlayerDeserializerGenerator generator) {
            if (schemas == null) {
                throw new ArgumentNullException(nameof(schemas));
            }
            if (generator == null) {
                throw new ArgumentNullException(nameof(generator));
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("#ifdef DrawText");
            builder.AppendLine("#undef DrawText");
            builder.AppendLine("#endif");
            builder.AppendLine("#include \"GeneratedRuntimeComponentDeserializerRegistration.hpp\"");
            builder.AppendLine("#include \"RuntimeComponentRegistry.hpp\"");
            builder.AppendLine("#include \"runtime/native_exceptions.hpp\"");
            for (int index = 0; index < schemas.Count; index++) {
                builder.AppendLine($"#include \"{generator.BuildNativeDeserializerClassName(schemas[index])}.hpp\"");
            }

            builder.AppendLine();
            builder.AppendLine("void RegisterGeneratedRuntimeComponentDeserializers(::RuntimeComponentRegistry* registry)");
            builder.AppendLine("{");
            builder.AppendLine("    if (registry == nullptr)");
            builder.AppendLine("    {");
            builder.AppendLine("throw new ArgumentNullException(\"registry\");");
            builder.AppendLine("    }");
            for (int index = 0; index < schemas.Count; index++) {
                builder.AppendLine($"registry->Register(new ::{generator.BuildNativeDeserializerClassName(schemas[index])}());");
            }

            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// Writes the final generated-core unity translation unit that includes every generated implementation file once in a stable order.
        /// </summary>
        /// <param name="generatedCoreRootPath">Absolute path to the generated core output root.</param>
        internal static void WriteGeneratedCoreTranslationUnit(string generatedCoreRootPath) {
            if (string.IsNullOrWhiteSpace(generatedCoreRootPath)) {
                throw new ArgumentException("Generated core root path must be provided.", nameof(generatedCoreRootPath));
            }

            if (!Directory.Exists(generatedCoreRootPath)) {
                return;
            }

            string unitySourcePath = Path.Combine(generatedCoreRootPath, "helengine_core_unity.cpp");
            string[] excludedSourceRelativePaths = new[] {
                "runtime/runtime_startup_manifest.cpp",
                "runtime/runtime_scene_catalog_manifest.cpp",
                "runtime/runtime_code_module_manifest.cpp"
            };
            List<string> sourceFiles = new();
            string[] discoveredFiles = Directory.GetFiles(generatedCoreRootPath, "*.cpp", SearchOption.AllDirectories);
            for (int index = 0; index < discoveredFiles.Length; index++) {
                string sourceFilePath = discoveredFiles[index];
                if (string.Equals(Path.GetFileName(sourceFilePath), "helengine_core_amalgamated.cpp", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Path.GetFileName(sourceFilePath), "helengine_core_unity.cpp", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                sourceFiles.Add(sourceFilePath);
            }

            sourceFiles.Sort(CompareAmalgamatedSourcePaths);

            StringBuilder unityBuilder = new();
            unityBuilder.AppendLine("// Generated compile-validation unity translation unit.");
            unityBuilder.AppendLine();
            for (int index = 0; index < sourceFiles.Count; index++) {
                string relativePath = Path.GetRelativePath(generatedCoreRootPath, sourceFiles[index]).Replace('\\', '/');
                bool excludedSource = false;
                for (int excludeIndex = 0; excludeIndex < excludedSourceRelativePaths.Length; excludeIndex++) {
                    if (string.Equals(relativePath, excludedSourceRelativePaths[excludeIndex], StringComparison.OrdinalIgnoreCase)) {
                        excludedSource = true;
                        break;
                    }
                }

                if (excludedSource) {
                    continue;
                }

                unityBuilder.Append("#include \"");
                unityBuilder.Append(relativePath);
                unityBuilder.AppendLine("\"");
            }

            string unitySourceContents = unityBuilder.ToString();
            File.WriteAllText(unitySourcePath, unitySourceContents);
        }

        /// <summary>
        /// Compares two generated source paths so foundational definitions appear before dependent code.
        /// </summary>
        /// <param name="left">Left source path.</param>
        /// <param name="right">Right source path.</param>
        /// <returns>Sort comparison result.</returns>
        static int CompareAmalgamatedSourcePaths(string left, string right) {
            int leftPriority = GetAmalgamatedSourcePriority(left);
            int rightPriority = GetAmalgamatedSourcePriority(right);
            if (leftPriority != rightPriority) {
                return leftPriority.CompareTo(rightPriority);
            }

            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the sort priority used by the amalgamated translation unit generator.
        /// </summary>
        /// <param name="sourcePath">Absolute generated source path.</param>
        /// <returns>Lower values sort earlier.</returns>
        static int GetAmalgamatedSourcePriority(string sourcePath) {
            string fileName = Path.GetFileName(sourcePath);
            if (string.Equals(fileName, "ButtonState.cpp", StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(fileName, "KeyState.cpp", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(fileName, "Keys.cpp", StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(fileName, "int2.cpp", StringComparison.OrdinalIgnoreCase)) return 3;
            if (string.Equals(fileName, "float2.cpp", StringComparison.OrdinalIgnoreCase)) return 4;
            if (string.Equals(fileName, "float3.cpp", StringComparison.OrdinalIgnoreCase)) return 5;
            if (string.Equals(fileName, "float4.cpp", StringComparison.OrdinalIgnoreCase)) return 6;
            if (string.Equals(fileName, "float4x4.cpp", StringComparison.OrdinalIgnoreCase)) return 7;
            if (string.Equals(fileName, "Component.cpp", StringComparison.OrdinalIgnoreCase)) return 8;
            if (string.Equals(fileName, "Entity.cpp", StringComparison.OrdinalIgnoreCase)) return 9;
            if (string.Equals(fileName, "IUpdateable.cpp", StringComparison.OrdinalIgnoreCase)) return 10;
            if (string.Equals(fileName, "UpdateComponent.cpp", StringComparison.OrdinalIgnoreCase)) return 11;
            if (string.Equals(fileName, "CoreInitializationOptions.cpp", StringComparison.OrdinalIgnoreCase)) return 12;
            if (string.Equals(fileName, "ObjectManager.cpp", StringComparison.OrdinalIgnoreCase)) return 13;
            if (string.Equals(fileName, "RenderManager2D.cpp", StringComparison.OrdinalIgnoreCase)) return 14;
            if (string.Equals(fileName, "RenderManager3D.cpp", StringComparison.OrdinalIgnoreCase)) return 15;
            if (string.Equals(fileName, "IInputBackend.cpp", StringComparison.OrdinalIgnoreCase)) return 16;
            if (string.Equals(fileName, "InputActionId.cpp", StringComparison.OrdinalIgnoreCase)) return 17;
            if (string.Equals(fileName, "InputActionState.cpp", StringComparison.OrdinalIgnoreCase)) return 18;
            if (string.Equals(fileName, "InputBinding.cpp", StringComparison.OrdinalIgnoreCase)) return 19;
            if (string.Equals(fileName, "InputContextId.cpp", StringComparison.OrdinalIgnoreCase)) return 20;
            if (string.Equals(fileName, "InputControlId.cpp", StringComparison.OrdinalIgnoreCase)) return 21;
            if (string.Equals(fileName, "InputControlKind.cpp", StringComparison.OrdinalIgnoreCase)) return 22;
            if (string.Equals(fileName, "InputDeviceKind.cpp", StringComparison.OrdinalIgnoreCase)) return 23;
            if (string.Equals(fileName, "InputFrameState.cpp", StringComparison.OrdinalIgnoreCase)) return 24;
            if (string.Equals(fileName, "InputGamepadButton.cpp", StringComparison.OrdinalIgnoreCase)) return 25;
            if (string.Equals(fileName, "InputGamepadState.cpp", StringComparison.OrdinalIgnoreCase)) return 26;
            if (string.Equals(fileName, "InputPointerButton.cpp", StringComparison.OrdinalIgnoreCase)) return 27;
            if (string.Equals(fileName, "InputPointerState.cpp", StringComparison.OrdinalIgnoreCase)) return 28;
            if (string.Equals(fileName, "InputTextState.cpp", StringComparison.OrdinalIgnoreCase)) return 29;
            if (string.Equals(fileName, "InputSystem.cpp", StringComparison.OrdinalIgnoreCase)) return 30;
            if (string.Equals(fileName, "PointerInteraction.cpp", StringComparison.OrdinalIgnoreCase)) return 31;
            if (string.Equals(fileName, "PointerCursorKind.cpp", StringComparison.OrdinalIgnoreCase)) return 32;
            if (string.Equals(fileName, "PointerInteractionSystem.cpp", StringComparison.OrdinalIgnoreCase)) return 33;
            if (string.Equals(fileName, "Core.cpp", StringComparison.OrdinalIgnoreCase)) return 34;
            return 1000;
        }

        /// <summary>
        /// Returns whether one generated file should be merged into the combined native output tree.
        /// </summary>
        /// <param name="sourceFilePath">Candidate source file path.</param>
        /// <returns>True when the file is a native source artifact that should be copied.</returns>
        static bool ShouldMergeGeneratedSourceFile(string sourceFilePath) {
            string extension = Path.GetExtension(sourceFilePath);
            if (!string.Equals(extension, ".cpp", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".hpp", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            string fileName = Path.GetFileName(sourceFilePath);
            if (string.Equals(fileName, "helcpp_config.hpp", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the build-features object from one parsed conversion report, creating an empty object when it does not exist yet.
        /// </summary>
        /// <param name="reportObject">Parsed conversion report object.</param>
        /// <returns>Build-features object used for decision and metadata merging.</returns>
        static JsonObject GetOrCreateBuildFeaturesObject(JsonObject reportObject) {
            if (reportObject == null) {
                throw new ArgumentNullException(nameof(reportObject));
            }

            JsonObject buildFeatures = reportObject["buildFeatures"] as JsonObject;
            if (buildFeatures != null) {
                return buildFeatures;
            }

            buildFeatures = new JsonObject();
            reportObject["buildFeatures"] = buildFeatures;
            return buildFeatures;
        }

        /// <summary>
        /// Merges feature enablement decisions from one generated report into the combined destination report by promoting any feature enabled by a merged project.
        /// </summary>
        /// <param name="destinationBuildFeatures">Destination build-features object that should be updated in place.</param>
        /// <param name="sourceBuildFeatures">Source build-features object that may enable additional features.</param>
        static void MergeFeatureDecisionArray(JsonObject destinationBuildFeatures, JsonObject sourceBuildFeatures) {
            if (destinationBuildFeatures == null) {
                throw new ArgumentNullException(nameof(destinationBuildFeatures));
            }
            if (sourceBuildFeatures == null) {
                throw new ArgumentNullException(nameof(sourceBuildFeatures));
            }

            JsonArray destinationDecisions = GetOrCreateFeatureArray(destinationBuildFeatures, "decisions");
            JsonArray sourceDecisions = GetOrCreateFeatureArray(sourceBuildFeatures, "decisions");
            for (int index = 0; index < sourceDecisions.Count; index++) {
                JsonObject sourceDecision = sourceDecisions[index] as JsonObject;
                if (sourceDecision == null) {
                    continue;
                }

                string featureName = ReadJsonString(sourceDecision, "feature");
                if (string.IsNullOrWhiteSpace(featureName)) {
                    continue;
                }

                int destinationIndex = FindFeatureDecisionIndex(destinationDecisions, featureName);
                if (destinationIndex < 0) {
                    destinationDecisions.Add(sourceDecision.DeepClone());
                    continue;
                }

                JsonObject destinationDecision = destinationDecisions[destinationIndex] as JsonObject;
                if (destinationDecision == null) {
                    destinationDecisions[destinationIndex] = sourceDecision.DeepClone();
                    continue;
                }

                bool sourceEnabled = ReadJsonBoolean(sourceDecision, "enabled");
                bool destinationEnabled = ReadJsonBoolean(destinationDecision, "enabled");
                if (!sourceEnabled || destinationEnabled) {
                    continue;
                }

                destinationDecision["enabled"] = true;
                destinationDecision["origin"] = ReadJsonString(sourceDecision, "origin");
            }
        }

        /// <summary>
        /// Merges one metadata array such as detected roots or conflicts into the destination report while preserving existing entries and appending new unique entries.
        /// </summary>
        /// <param name="destinationBuildFeatures">Destination build-features object that should be updated in place.</param>
        /// <param name="sourceBuildFeatures">Source build-features object that may contribute additional metadata records.</param>
        /// <param name="propertyName">Metadata array property name, such as <c>detectedRoots</c> or <c>conflicts</c>.</param>
        static void MergeFeatureMetadataArray(JsonObject destinationBuildFeatures, JsonObject sourceBuildFeatures, string propertyName) {
            if (destinationBuildFeatures == null) {
                throw new ArgumentNullException(nameof(destinationBuildFeatures));
            }
            if (sourceBuildFeatures == null) {
                throw new ArgumentNullException(nameof(sourceBuildFeatures));
            }
            if (string.IsNullOrWhiteSpace(propertyName)) {
                throw new ArgumentException("Property name must be provided.", nameof(propertyName));
            }

            JsonArray destinationArray = GetOrCreateFeatureArray(destinationBuildFeatures, propertyName);
            JsonArray sourceArray = GetOrCreateFeatureArray(sourceBuildFeatures, propertyName);
            HashSet<string> existingEntries = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < destinationArray.Count; index++) {
                JsonNode destinationNode = destinationArray[index];
                if (destinationNode == null) {
                    continue;
                }

                existingEntries.Add(destinationNode.ToJsonString());
            }

            for (int index = 0; index < sourceArray.Count; index++) {
                JsonNode sourceNode = sourceArray[index];
                if (sourceNode == null) {
                    continue;
                }

                string serializedEntry = sourceNode.ToJsonString();
                if (existingEntries.Contains(serializedEntry)) {
                    continue;
                }

                destinationArray.Add(sourceNode.DeepClone());
                existingEntries.Add(serializedEntry);
            }
        }

        /// <summary>
        /// Gets one JSON array from the supplied object, creating an empty array when the property does not exist yet.
        /// </summary>
        /// <param name="parentObject">Parent JSON object that owns the requested array property.</param>
        /// <param name="propertyName">Array property name to resolve.</param>
        /// <returns>Resolved JSON array instance.</returns>
        static JsonArray GetOrCreateFeatureArray(JsonObject parentObject, string propertyName) {
            if (parentObject == null) {
                throw new ArgumentNullException(nameof(parentObject));
            }
            if (string.IsNullOrWhiteSpace(propertyName)) {
                throw new ArgumentException("Property name must be provided.", nameof(propertyName));
            }

            JsonArray jsonArray = parentObject[propertyName] as JsonArray;
            if (jsonArray != null) {
                return jsonArray;
            }

            jsonArray = new JsonArray();
            parentObject[propertyName] = jsonArray;
            return jsonArray;
        }

        /// <summary>
        /// Finds the zero-based index of one feature-decision record in the supplied decision array.
        /// </summary>
        /// <param name="decisions">Decision array to search.</param>
        /// <param name="featureName">Feature name to match.</param>
        /// <returns>Zero-based index when found; otherwise <c>-1</c>.</returns>
        static int FindFeatureDecisionIndex(JsonArray decisions, string featureName) {
            if (decisions == null) {
                throw new ArgumentNullException(nameof(decisions));
            }
            if (string.IsNullOrWhiteSpace(featureName)) {
                throw new ArgumentException("Feature name must be provided.", nameof(featureName));
            }

            for (int index = 0; index < decisions.Count; index++) {
                JsonObject decisionObject = decisions[index] as JsonObject;
                if (decisionObject == null) {
                    continue;
                }

                if (string.Equals(ReadJsonString(decisionObject, "feature"), featureName, StringComparison.Ordinal)) {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// Reads one string property from a JSON object and returns an empty string when the property is missing.
        /// </summary>
        /// <param name="jsonObject">JSON object to inspect.</param>
        /// <param name="propertyName">Property name to read.</param>
        /// <returns>Resolved string value, or an empty string when the property is absent.</returns>
        static string ReadJsonString(JsonObject jsonObject, string propertyName) {
            if (jsonObject == null) {
                throw new ArgumentNullException(nameof(jsonObject));
            }
            if (string.IsNullOrWhiteSpace(propertyName)) {
                throw new ArgumentException("Property name must be provided.", nameof(propertyName));
            }

            JsonNode valueNode = jsonObject[propertyName];
            return valueNode?.GetValue<string>() ?? string.Empty;
        }

        /// <summary>
        /// Reads one boolean property from a JSON object and returns false when the property is missing or not a boolean value.
        /// </summary>
        /// <param name="jsonObject">JSON object to inspect.</param>
        /// <param name="propertyName">Property name to read.</param>
        /// <returns>Resolved boolean value when present; otherwise false.</returns>
        static bool ReadJsonBoolean(JsonObject jsonObject, string propertyName) {
            if (jsonObject == null) {
                throw new ArgumentNullException(nameof(jsonObject));
            }
            if (string.IsNullOrWhiteSpace(propertyName)) {
                throw new ArgumentException("Property name must be provided.", nameof(propertyName));
            }

            JsonNode valueNode = jsonObject[propertyName];
            if (valueNode == null) {
                return false;
            }

            try {
                return valueNode.GetValue<bool>();
            } catch (InvalidOperationException) {
                return false;
            } catch (FormatException) {
                return false;
            }
        }

        /// <summary>
        /// Combines the selected codegen option values with the platform feature symbols used by portable input conversion.
        /// </summary>
        /// <param name="selectedCodegenOptionValues">Selected codegen option values persisted by the editor.</param>
        /// <param name="additionalPreprocessorSymbols">Feature symbols injected for portable-input compilation.</param>
        /// <returns>Combined preprocessor symbol string ready for the codegen CLI.</returns>
        static string CombinePreprocessorSymbols(
            IReadOnlyDictionary<string, string> selectedCodegenOptionValues,
            IReadOnlyList<string> additionalPreprocessorSymbols) {
            List<string> symbols = new();
            if (selectedCodegenOptionValues != null
                && selectedCodegenOptionValues.TryGetValue("additional-preprocessor-symbols", out string selectedSymbols)
                && !string.IsNullOrWhiteSpace(selectedSymbols)) {
                symbols.AddRange(selectedSymbols.Split([';', ',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }

            for (int index = 0; index < additionalPreprocessorSymbols.Count; index++) {
                string symbol = additionalPreprocessorSymbols[index];
                if (string.IsNullOrWhiteSpace(symbol)) {
                    continue;
                }

                if (!symbols.Contains(symbol, StringComparer.OrdinalIgnoreCase)) {
                    symbols.Add(symbol);
                }
            }

            return symbols.Count > 0 ? string.Join(';', symbols) : string.Empty;
        }

        /// <summary>
        /// Adds unique symbols from one ordered source list into one mutable destination list.
        /// </summary>
        /// <param name="symbols">Destination list receiving unique symbols.</param>
        /// <param name="symbolsToAdd">Ordered source symbols.</param>
        static void AddUniqueSymbols(List<string> symbols, IReadOnlyList<string> symbolsToAdd) {
            if (symbols == null) {
                throw new ArgumentNullException(nameof(symbols));
            }
            if (symbolsToAdd == null) {
                throw new ArgumentNullException(nameof(symbolsToAdd));
            }

            for (int index = 0; index < symbolsToAdd.Count; index++) {
                string symbol = symbolsToAdd[index];
                if (string.IsNullOrWhiteSpace(symbol)) {
                    continue;
                }

                if (!symbols.Contains(symbol, StringComparer.OrdinalIgnoreCase)) {
                    symbols.Add(symbol);
                }
            }
        }

        /// <summary>
        /// Quotes one process argument for display purposes.
        /// </summary>
        static string QuoteArgument(string argument) {
            if (string.IsNullOrEmpty(argument)) {
                return "\"\"";
            }

            if (argument.IndexOfAny([' ', '\t', '"']) < 0) {
                return argument;
            }

            return "\"" + argument.Replace("\"", "\\\"") + "\"";
        }
    }
}

