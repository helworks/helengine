using System.Diagnostics;
using System.Text;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Profiles;

namespace helengine.editor {
    /// <summary>
    /// Regenerates the shared native source tree for core and portable input before platform builds run.
    /// </summary>
    public sealed class EditorGeneratedCoreRegenerationService {
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
        /// <param name="cancellationToken">Cancellation token that can stop regeneration cooperatively.</param>
        public void Regenerate(
            PlatformDefinition platformDefinition,
            PlatformCodegenProfileDefinition codegenProfile,
            IReadOnlyDictionary<string, string> selectedCodegenOptionValues,
            string generatedCoreRootPath,
            string codegenToolPath,
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
            string helengineInputProjectPath = Path.Combine(helEngineRootPath, "engine", "helengine.input", "helengine.input.csproj");
            string tempRoot = Path.Combine(Path.GetTempPath(), "helengine-generated-core", platformDefinition.PlatformId, Guid.NewGuid().ToString("N"));
            string portableInputOutputRoot = Path.Combine(tempRoot, "portable-input");
            string logPath = Path.Combine(tempRoot, "regeneration.log");
            StringBuilder logBuilder = new();

            if (!File.Exists(helengineCoreProjectPath)) {
                throw new FileNotFoundException($"Could not find helengine.core project at '{helengineCoreProjectPath}'.", helengineCoreProjectPath);
            }
            if (!File.Exists(helengineInputProjectPath)) {
                throw new FileNotFoundException($"Could not find helengine.input project at '{helengineInputProjectPath}'.", helengineInputProjectPath);
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
                RegenerateProject(
                    fullCodegenToolPath,
                    helengineCoreProjectPath,
                    generatedCoreOutputRoot,
                    platformDefinition,
                    codegenProfile,
                    selectedCodegenOptionValues,
                    portableInputPreprocessorSymbols,
                    logBuilder,
                    cancellationToken);
                RegenerateProject(
                    fullCodegenToolPath,
                    helengineInputProjectPath,
                    portableInputOutputRoot,
                    platformDefinition,
                    codegenProfile,
                    selectedCodegenOptionValues,
                    portableInputPreprocessorSymbols,
                    logBuilder,
                    cancellationToken);
                MergeGeneratedSourceTree(portableInputOutputRoot, generatedCoreOutputRoot);
                NormalizeGeneratedNativeSources(generatedCoreOutputRoot);
                RewriteUnityTranslationUnit(generatedCoreOutputRoot);
            } finally {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? tempRoot);
                File.WriteAllText(logPath, logBuilder.ToString());
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
                codegenProfile.Endianness == PlatformSerializationEndianness.LittleEndian ? "little" : "big"
            ];

            foreach (KeyValuePair<string, string> selectedOption in selectedCodegenOptionValues.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)) {
                if (string.IsNullOrWhiteSpace(selectedOption.Key) || string.Equals(selectedOption.Key, "additional-preprocessor-symbols", StringComparison.OrdinalIgnoreCase)) {
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

            RunProcess(fileName, arguments, Path.GetDirectoryName(fileName) ?? Directory.GetCurrentDirectory(), logBuilder, cancellationToken);
        }

        /// <summary>
        /// Returns the feature symbols used by portable input codegen for one platform.
        /// </summary>
        /// <param name="platformDefinition">Typed platform metadata exposed by the active builder.</param>
        /// <returns>Preprocessor symbols that should be supplied to the input codegen run.</returns>
        internal static IReadOnlyList<string> ResolvePortableInputPreprocessorSymbols(PlatformDefinition platformDefinition) {
            if (platformDefinition == null) {
                throw new ArgumentNullException(nameof(platformDefinition));
            }

            if (string.Equals(platformDefinition.PlatformId, "windows", StringComparison.OrdinalIgnoreCase)) {
                return ["HELENGINE_INPUT_KEYBOARD", "HELENGINE_INPUT_MOUSE"];
            }

            return [];
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
        /// Normalizes generated source files that still need post-processing for the native Windows build.
        /// </summary>
        /// <param name="generatedCoreRootPath">Absolute path to the generated core output root.</param>
        internal static void NormalizeGeneratedNativeSources(string generatedCoreRootPath) {
            if (string.IsNullOrWhiteSpace(generatedCoreRootPath)) {
                throw new ArgumentException("Generated core root path must be provided.", nameof(generatedCoreRootPath));
            }

            if (!Directory.Exists(generatedCoreRootPath)) {
                return;
            }

            string[] sourceFiles = Directory.GetFiles(generatedCoreRootPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(sourceFiles, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < sourceFiles.Length; index++) {
                string sourceFilePath = sourceFiles[index];
                string extension = Path.GetExtension(sourceFilePath);
                if (!string.Equals(extension, ".cpp", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(extension, ".hpp", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                string fileName = Path.GetFileName(sourceFilePath);
                string contents = File.ReadAllText(sourceFilePath);
                string updatedContents = NormalizeGeneratedNativeSource(fileName, contents);
                if (!string.Equals(contents, updatedContents, StringComparison.Ordinal)) {
                    File.WriteAllText(sourceFilePath, updatedContents);
                }
            }
        }

        /// <summary>
        /// Applies file-specific source fixes needed by the generated native Windows build.
        /// </summary>
        /// <param name="fileName">File name being normalized.</param>
        /// <param name="contents">Current file contents.</param>
        /// <returns>Updated file contents.</returns>
        static string NormalizeGeneratedNativeSource(string fileName, string contents) {
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrEmpty(contents)) {
                return contents;
            }

            if (string.Equals(fileName, "ButtonComponent.hpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "ComboBoxComponent.hpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "ScrollComponent.hpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "TextBoxComponent.hpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "TextComponent.hpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "ButtonComponent.cpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "CheckBoxComponent.hpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "CheckBoxComponent.cpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "ComboBoxItemVisual.hpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "ComboBoxItemVisual.cpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "ComboBoxComponent.hpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "ComboBoxComponent.cpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "IFocusTarget.hpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "IFocusGroup.hpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "InteractableComponent.hpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "InteractableComponent.cpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "IInteractable2D.hpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "TextBoxComponent.cpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "TextComponent.cpp", StringComparison.OrdinalIgnoreCase)) {
                string updatedContents = contents.Replace("ContainsScreenPoint(int2* point)", "ContainsScreenPoint(int2 point)");
                updatedContents = updatedContents.Replace("point->", "point.");
                updatedContents = updatedContents.Replace("mousePosition->", "mousePosition.");
                updatedContents = updatedContents.Replace("pointer->", "pointer.");
                return updatedContents;
            }

            if (string.Equals(fileName, "ButtonComponent.cpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "ComboBoxComponent.cpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "ScrollComponent.cpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "TextBoxComponent.cpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "TextComponent.cpp", StringComparison.OrdinalIgnoreCase)) {
                string updatedContents = contents.Replace("ContainsScreenPoint(int2* point)", "ContainsScreenPoint(int2 point)");
                if (string.Equals(fileName, "ButtonComponent.cpp", StringComparison.OrdinalIgnoreCase)) {
                    updatedContents = updatedContents.Replace(
                        "RoundedRectCorners::TopLeft | RoundedRectCorners::TopRight",
                        "static_cast<::RoundedRectCorners>(static_cast<int32_t>(RoundedRectCorners::TopLeft) | static_cast<int32_t>(RoundedRectCorners::TopRight))");
                }
                updatedContents = updatedContents.Replace("point->", "point.");
                return updatedContents;
            }

            if (string.Equals(fileName, "PointerInteractionSystem.cpp", StringComparison.OrdinalIgnoreCase)) {
                return contents;
            }

            if (string.Equals(fileName, "TextBoxUpdateComponent.cpp", StringComparison.OrdinalIgnoreCase)) {
                return contents.Replace("int2 *pointer = input->GetMousePosition();", "int2 pointer = input->GetMousePosition();");
            }

            return contents;
        }

        /// <summary>
        /// Writes a full unity translation unit that includes every generated native core source file in a stable order.
        /// </summary>
        /// <param name="generatedCoreRootPath">Absolute path to the generated core output root.</param>
        internal static void RewriteUnityTranslationUnit(string generatedCoreRootPath) {
            if (string.IsNullOrWhiteSpace(generatedCoreRootPath)) {
                throw new ArgumentException("Generated core root path must be provided.", nameof(generatedCoreRootPath));
            }

            if (!Directory.Exists(generatedCoreRootPath)) {
                return;
            }

            string unitySourcePath = Path.Combine(generatedCoreRootPath, "helengine_core_unity.cpp");
            List<string> sourceFiles = new();
            string[] discoveredFiles = Directory.GetFiles(generatedCoreRootPath, "*.cpp", SearchOption.AllDirectories);
            for (int index = 0; index < discoveredFiles.Length; index++) {
                string sourceFilePath = discoveredFiles[index];
                if (string.Equals(Path.GetFileName(sourceFilePath), "helengine_core_unity.cpp", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                sourceFiles.Add(sourceFilePath);
            }

            sourceFiles.Sort(CompareUnitySourcePaths);

            StringBuilder unityBuilder = new();
            unityBuilder.AppendLine("// Generated compile-validation unity translation unit.");
            unityBuilder.AppendLine();
            for (int index = 0; index < sourceFiles.Count; index++) {
                string relativePath = Path.GetRelativePath(generatedCoreRootPath, sourceFiles[index]).Replace('\\', '/');
                unityBuilder.Append("#include \"");
                unityBuilder.Append(relativePath);
                unityBuilder.AppendLine("\"");
            }

            File.WriteAllText(unitySourcePath, unityBuilder.ToString());
        }

        /// <summary>
        /// Compares two generated source paths so foundational definitions appear before dependent code.
        /// </summary>
        /// <param name="left">Left source path.</param>
        /// <param name="right">Right source path.</param>
        /// <returns>Sort comparison result.</returns>
        static int CompareUnitySourcePaths(string left, string right) {
            int leftPriority = GetUnitySourcePriority(left);
            int rightPriority = GetUnitySourcePriority(right);
            if (leftPriority != rightPriority) {
                return leftPriority.CompareTo(rightPriority);
            }

            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the sort priority used by the unity translation unit generator.
        /// </summary>
        /// <param name="sourcePath">Absolute generated source path.</param>
        /// <returns>Lower values sort earlier.</returns>
        static int GetUnitySourcePriority(string sourcePath) {
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
