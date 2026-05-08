using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
            string helengineInputProjectPath = Path.Combine(helEngineRootPath, "engine", "helengine.input", "helengine.input.csproj");
            string bundledRuntimeSupportRootPath = Path.Combine(
                Path.GetDirectoryName(fullCodegenToolPath) ?? throw new InvalidOperationException($"Unable to resolve the codegen tool directory from '{fullCodegenToolPath}'."),
                ".net.cpp");
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
                    helengineInputProjectPath,
                    portableInputOutputRoot,
                    platformDefinition,
                    codegenProfile,
                    selectedCodegenOptionValues,
                    combinedPreprocessorSymbols,
                    logBuilder,
                    cancellationToken);
                MergeGeneratedSourceTree(portableInputOutputRoot, generatedCoreOutputRoot);
                MergeBundledRuntimeSupportTree(bundledRuntimeSupportRootPath, generatedCoreOutputRoot);
                NormalizeGeneratedNativeSources(generatedCoreOutputRoot);
                RewriteAmalgamatedTranslationUnit(generatedCoreOutputRoot);
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
            if (platformDefinition == null) {
                throw new ArgumentNullException(nameof(platformDefinition));
            }

            if (string.Equals(platformDefinition.PlatformId, "windows", StringComparison.OrdinalIgnoreCase)) {
                return [
                    "HELENGINE_INPUT_KEYBOARD",
                    "HELENGINE_INPUT_MOUSE",
                    "DESKTOP_PLATFORM",
                    "HELENGINE_CODEGEN_DISABLE_MENU_REFLECTION",
                    "HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION"
                ];
            }

            if (string.Equals(platformDefinition.PlatformId, "ps2", StringComparison.OrdinalIgnoreCase)) {
                return [
                    "PS2_PLATFORM",
                    "HELENGINE_CODEGEN_DISABLE_MENU_REFLECTION",
                    "HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION"
                ];
            }

            return [
                "HELENGINE_CODEGEN_DISABLE_MENU_REFLECTION",
                "HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION"
            ];
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

            RemoveEditorOnlyGeneratedSourceFiles(generatedCoreRootPath);
            EmitGeneratedAutomaticRuntimeComponentDeserializers(generatedCoreRootPath);
            IReadOnlyList<string> featureManifestEntries = LoadGeneratedFeatureManifestEntries(generatedCoreRootPath);
            string[] sourceFiles = Directory.GetFiles(generatedCoreRootPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(sourceFiles, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < sourceFiles.Length; index++) {
                string sourceFilePath = sourceFiles[index];
                string extension = Path.GetExtension(sourceFilePath);
                if (!string.Equals(extension, ".cpp", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(extension, ".hpp", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(extension, ".tpp", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                string fileName = Path.GetFileName(sourceFilePath);
                string contents = File.ReadAllText(sourceFilePath);
                string updatedContents = NormalizeGeneratedNativeSource(fileName, contents, featureManifestEntries);
                if (!string.Equals(contents, updatedContents, StringComparison.Ordinal)) {
                    File.WriteAllText(sourceFilePath, updatedContents);
                }
            }
        }

        /// <summary>
        /// Emits generated native runtime component deserializers for engine-owned components that use automatic packaged scene persistence.
        /// </summary>
        /// <param name="generatedCoreRootPath">Absolute generated core output root that should receive the generated native files.</param>
        internal static void EmitGeneratedAutomaticRuntimeComponentDeserializers(string generatedCoreRootPath) {
            if (string.IsNullOrWhiteSpace(generatedCoreRootPath)) {
                throw new ArgumentException("Generated core root path must be provided.", nameof(generatedCoreRootPath));
            }

            Directory.CreateDirectory(generatedCoreRootPath);
            ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator();
            ScriptComponentReflectionSchemaBuilder schemaBuilder = new ScriptComponentReflectionSchemaBuilder();
            IReadOnlyList<ScriptComponentReflectionSchema> schemas = DiscoverAutomaticRuntimeComponentSchemas(schemaBuilder, generator);
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
            PatchRuntimeComponentRegistryForGeneratedDeserializers(generatedCoreRootPath);
        }

        /// <summary>
        /// Discovers the engine-owned component schemas that can participate in generated native runtime deserializer emission.
        /// </summary>
        /// <param name="schemaBuilder">Reflected schema builder used for component discovery.</param>
        /// <param name="generator">Native deserializer generator used to validate supported schemas.</param>
        /// <returns>Deterministically ordered schemas eligible for generated native runtime deserializer emission.</returns>
        static IReadOnlyList<ScriptComponentReflectionSchema> DiscoverAutomaticRuntimeComponentSchemas(
            ScriptComponentReflectionSchemaBuilder schemaBuilder,
            ScriptComponentPlayerDeserializerGenerator generator) {
            if (schemaBuilder == null) {
                throw new ArgumentNullException(nameof(schemaBuilder));
            }
            if (generator == null) {
                throw new ArgumentNullException(nameof(generator));
            }

            Type[] componentTypes = typeof(Component).Assembly
                .GetTypes()
                .Where(IsEligibleAutomaticRuntimeComponentType)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();
            List<ScriptComponentReflectionSchema> schemas = new List<ScriptComponentReflectionSchema>(componentTypes.Length);
            for (int index = 0; index < componentTypes.Length; index++) {
                ScriptComponentReflectionSchema schema = schemaBuilder.Build(componentTypes[index]);
                if (generator.CanGenerateNativeDeserializer(schema)) {
                    schemas.Add(schema);
                }
            }

            return schemas;
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
        /// Patches the generated native runtime component registry so it registers emitted automatic component deserializers during startup.
        /// </summary>
        /// <param name="generatedCoreRootPath">Absolute generated core output root whose registry source should be patched.</param>
        static void PatchRuntimeComponentRegistryForGeneratedDeserializers(string generatedCoreRootPath) {
            string registrySourcePath = Path.Combine(generatedCoreRootPath, "RuntimeComponentRegistry.cpp");
            if (!File.Exists(registrySourcePath)) {
                return;
            }

            string contents = File.ReadAllText(registrySourcePath);
            string updatedContents = contents;
            const string registrationHeaderInclude = "#include \"GeneratedRuntimeComponentDeserializerRegistration.hpp\"";
            if (!updatedContents.Contains(registrationHeaderInclude, StringComparison.Ordinal)) {
                updatedContents = updatedContents.Replace(
                    "#include \"RuntimeComponentRegistry.hpp\"",
                    "#include \"RuntimeComponentRegistry.hpp\"" + Environment.NewLine + registrationHeaderInclude,
                    StringComparison.Ordinal);
            }

            const string registrationCall = "RegisterGeneratedRuntimeComponentDeserializers(registry);";
            if (!updatedContents.Contains(registrationCall, StringComparison.Ordinal)) {
                updatedContents = updatedContents.Replace(
                    "return registry;}",
                    registrationCall + Environment.NewLine + "return registry;}",
                    StringComparison.Ordinal);
            }

            if (!string.Equals(contents, updatedContents, StringComparison.Ordinal)) {
                File.WriteAllText(registrySourcePath, updatedContents);
            }
        }

        /// <summary>
        /// Deletes generated editor-only attribute sources that should never participate in runtime native builds.
        /// </summary>
        /// <param name="generatedCoreRootPath">Absolute generated core output root.</param>
        static void RemoveEditorOnlyGeneratedSourceFiles(string generatedCoreRootPath) {
            if (string.IsNullOrWhiteSpace(generatedCoreRootPath)) {
                throw new ArgumentException("Generated core root path must be provided.", nameof(generatedCoreRootPath));
            }

            string[] editorOnlyTypeNames = [
                "EditorPropertyDisplayNameAttribute",
                "EditorPropertyHiddenAttribute",
                "EditorPropertyOrderAttribute"
            ];
            string[] generatedExtensions = [
                ".hpp",
                ".cpp",
                ".tpp"
            ];

            for (int typeIndex = 0; typeIndex < editorOnlyTypeNames.Length; typeIndex++) {
                string typeName = editorOnlyTypeNames[typeIndex];
                for (int extensionIndex = 0; extensionIndex < generatedExtensions.Length; extensionIndex++) {
                    string generatedPath = Path.Combine(generatedCoreRootPath, typeName + generatedExtensions[extensionIndex]);
                    if (!File.Exists(generatedPath)) {
                        continue;
                    }

                    File.Delete(generatedPath);
                }
            }
        }

        /// <summary>
        /// Applies file-specific source fixes needed by the generated native Windows build.
        /// </summary>
        /// <param name="fileName">File name being normalized.</param>
        /// <param name="contents">Current file contents.</param>
        /// <param name="featureManifestEntries">Feature-manifest entries derived from the generated conversion report.</param>
        /// <returns>Updated file contents.</returns>
        static string NormalizeGeneratedNativeSource(string fileName, string contents, IReadOnlyList<string> featureManifestEntries) {
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrEmpty(contents)) {
                return contents;
            }

            if (string.Equals(fileName, "Component.hpp", StringComparison.OrdinalIgnoreCase)) {
                return RemoveGeneratedIncludeLine(contents, "#include \"Entity.hpp\"");
            }

            if (string.Equals(fileName, "Entity.hpp", StringComparison.OrdinalIgnoreCase)) {
                string updatedContents = RemoveGeneratedIncludeLine(contents, "#include \"Component.hpp\"");
                updatedContents = RemoveGeneratedIncludeLine(updatedContents, "#include \"ComponentExecutionPolicy.hpp\"");
                updatedContents = RemoveGeneratedIncludeLine(updatedContents, "#include \"Core.hpp\"");
                updatedContents = RemoveGeneratedIncludeLine(updatedContents, "#include \"ObjectManager.hpp\"");
                return updatedContents;
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

            if (string.Equals(fileName, "InputSystem.cpp", StringComparison.OrdinalIgnoreCase)) {
                string updatedContents = contents.Replace(
                    "pointer.get_DeltaX() += pointerWrapDeltaOffset.X;",
                    "pointer.set_DeltaX(pointer.get_DeltaX() + pointerWrapDeltaOffset.X);",
                    StringComparison.Ordinal);
                updatedContents = updatedContents.Replace(
                    "pointer.get_DeltaY() += pointerWrapDeltaOffset.Y;",
                    "pointer.set_DeltaY(pointer.get_DeltaY() + pointerWrapDeltaOffset.Y);",
                    StringComparison.Ordinal);
                return updatedContents;
            }

            if (string.Equals(fileName, "MenuComponent.cpp", StringComparison.OrdinalIgnoreCase)) {
                string updatedContents = Regex.Replace(
                    contents,
                    @"=\s*\(\)\s*=>\s*this->ActivateItem\(runtimeItem\);",
                    "= new Action<>([&]() { this->ActivateItem(runtimeItem); });",
                    RegexOptions.CultureInvariant);
                updatedContents = Regex.Replace(
                    updatedContents,
                    @"button->Hovered\s*\+=\s*\(\)\s*=>\s*this->HandleItemHovered\(runtimeItem\);",
                    "button->Hovered += [&]() { this->HandleItemHovered(runtimeItem); };",
                    RegexOptions.CultureInvariant);
                updatedContents = updatedContents.Replace(
                    "relativePath.Replace('/', Path::DirectorySeparatorChar).Replace('\\\\', Path::DirectorySeparatorChar)",
                    "String::Replace(String::Replace(relativePath, '/', Path::DirectorySeparatorChar), '\\\\', Path::DirectorySeparatorChar)",
                    StringComparison.Ordinal);
                updatedContents = updatedContents.Replace(
                    "relativePath.Replace('\\\\', '/')",
                    "String::Replace(relativePath, '\\\\', '/')",
                    StringComparison.Ordinal);
                updatedContents = updatedContents.Replace(
                    "InputGamepadState *currentGamepadState = inputSystem->GetGamepadState(0);",
                    "InputGamepadState currentGamepadState = inputSystem->GetGamepadState(0);",
                    StringComparison.Ordinal);
                updatedContents = updatedContents.Replace(
                    "InputGamepadState* MenuComponent::ReadPrimaryGamepadState()",
                    "InputGamepadState MenuComponent::ReadPrimaryGamepadState()",
                    StringComparison.Ordinal);
                updatedContents = updatedContents.Replace(
                    "return nullptr;    }\nreturn Core::get_Instance()->get_Input()->GetGamepadState(0);}",
                    "return InputGamepadState();    }\nreturn Core::get_Instance()->get_Input()->GetGamepadState(0);}",
                    StringComparison.Ordinal);
                updatedContents = updatedContents.Replace(
                    "bool MenuComponent::WasGamepadButtonPressed(InputGamepadState* currentState, InputGamepadState* previousState, InputGamepadButton button)",
                    "bool MenuComponent::WasGamepadButtonPressed(InputGamepadState currentState, InputGamepadState previousState, InputGamepadButton button)",
                    StringComparison.Ordinal);
                updatedContents = updatedContents.Replace("currentGamepadState->", "currentGamepadState.", StringComparison.Ordinal);
                updatedContents = updatedContents.Replace("currentState->", "currentState.", StringComparison.Ordinal);
                updatedContents = updatedContents.Replace("previousState->", "previousState.", StringComparison.Ordinal);
                return updatedContents;
            }

            if (string.Equals(fileName, "MenuComponent.hpp", StringComparison.OrdinalIgnoreCase)) {
                string updatedContents = contents.Replace(
                    "InputGamepadState* PreviousGamepadState;",
                    "InputGamepadState PreviousGamepadState;",
                    StringComparison.Ordinal);
                if (!updatedContents.Contains("class MenuSelectedDescriptionComponent;")) {
                    updatedContents = updatedContents.Replace(
                        "class MenuItemComponent;",
                        "class MenuItemComponent;\nclass MenuSelectedDescriptionComponent;",
                        StringComparison.Ordinal);
                }
                updatedContents = updatedContents.Replace(
                    "InputGamepadState* ReadPrimaryGamepadState();",
                    "InputGamepadState ReadPrimaryGamepadState();",
                    StringComparison.Ordinal);
                updatedContents = updatedContents.Replace(
                    "bool WasGamepadButtonPressed(InputGamepadState* currentState, InputGamepadState* previousState, InputGamepadButton button);",
                    "bool WasGamepadButtonPressed(InputGamepadState currentState, InputGamepadState previousState, InputGamepadButton button);",
                    StringComparison.Ordinal);
                return updatedContents;
            }

            if (string.Equals(fileName, "DirectionalLightComponent.cpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "PointLightComponent.cpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "SpotLightComponent.cpp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "LightComponent.cpp", StringComparison.OrdinalIgnoreCase)) {
                string updatedContents = contents.Replace("LightType.", "LightType::");
                updatedContents = updatedContents.Replace("this->ShadowMapMode::Auto", "::ShadowMapMode::Auto");
                return updatedContents;
            }

            if (string.Equals(fileName, "CameraRenderSettings.cpp", StringComparison.OrdinalIgnoreCase)) {
                string updatedContents = contents.Replace("this->DepthPrepassMode::", "::DepthPrepassMode::");
                updatedContents = updatedContents.Replace("this->PostProcessTier::", "::PostProcessTier::");
                return updatedContents;
            }

            if (string.Equals(fileName, "CoreInitializationOptions.cpp", StringComparison.OrdinalIgnoreCase)
                && contents.Contains("AppContext::BaseDirectory", StringComparison.Ordinal)
                && !contents.Contains("#include \"system/app_context.hpp\"", StringComparison.Ordinal)) {
                return InsertIncludeAfterOwnHeader(contents, "#include \"system/app_context.hpp\"");
            }

            if (string.Equals(fileName, "path.hpp", StringComparison.OrdinalIgnoreCase)
                && !contents.Contains("ChangeExtension", StringComparison.Ordinal)) {
                return InsertPathChangeExtensionDeclaration(contents);
            }

            if (string.Equals(fileName, "number.hpp", StringComparison.OrdinalIgnoreCase)
                && !contents.Contains("static bool IsNaN(float value)", StringComparison.Ordinal)) {
                return InsertNativeNumberFiniteHelpers(contents);
            }

            if (string.Equals(fileName, "math.hpp", StringComparison.OrdinalIgnoreCase)
                && !contents.Contains("static double Atan2(", StringComparison.Ordinal)) {
                return InsertNativeMathAtan2Helpers(contents);
            }

            if (string.Equals(fileName, "path.cpp", StringComparison.OrdinalIgnoreCase)
                && !contents.Contains("Path::ChangeExtension", StringComparison.Ordinal)) {
                return InsertPs2PathSupport(InsertPathChangeExtensionImplementation(contents));
            }

            if (string.Equals(fileName, "path.cpp", StringComparison.OrdinalIgnoreCase)) {
                return InsertPs2PathSupport(contents);
            }

            if (string.Equals(fileName, "file.cpp", StringComparison.OrdinalIgnoreCase)) {
                return InsertPs2FileSupport(contents);
            }

            if (string.Equals(fileName, "file-stream.cpp", StringComparison.OrdinalIgnoreCase)) {
                return InsertPs2FileStreamSupport(contents);
            }

            if (string.Equals(fileName, "native_dictionary.hpp", StringComparison.OrdinalIgnoreCase)
                && !contents.Contains("void Clear()", StringComparison.Ordinal)) {
                return InsertNativeDictionaryClearHelper(contents);
            }

            if (string.Equals(fileName, "array.hpp", StringComparison.OrdinalIgnoreCase)
                && contents.Contains("new T[length]", StringComparison.Ordinal)) {
                return InsertRuntimeArrayValueInitialization(contents);
            }

            if (string.Equals(fileName, "native_string.hpp", StringComparison.OrdinalIgnoreCase)
                && !contents.Contains("static std::string Replace(const std::string& value, char oldValue, char newValue)", StringComparison.Ordinal)) {
                return InsertNativeStringReplaceHelper(contents);
            }

            if (string.Equals(fileName, "feature_manifest.hpp", StringComparison.OrdinalIgnoreCase)
                && !contents.Contains("HostFileSystem", StringComparison.Ordinal)) {
                return InsertMissingFeatureManifestEntries(contents);
            }

            if (string.Equals(fileName, "feature_manifest.cpp", StringComparison.OrdinalIgnoreCase)
                && featureManifestEntries.Count > 0) {
                return RewriteFeatureManifestEntries(contents, featureManifestEntries);
            }

            if (string.Equals(fileName, "ShaderFilesystemIncludeResolver.cpp", StringComparison.OrdinalIgnoreCase)
                && contents.Contains("Directory::Exists", StringComparison.Ordinal)
                && !contents.Contains("#include \"system/io/directory.hpp\"", StringComparison.Ordinal)) {
                return InsertIncludeAfterOwnHeader(contents, "#include \"system/io/directory.hpp\"");
            }

            if (string.Equals(fileName, "action.hpp", StringComparison.OrdinalIgnoreCase)
                && !contents.Contains("std::function<void(TArgs...)> func{}", StringComparison.Ordinal)) {
                return InsertActionCallableSupportHeader(contents);
            }

            if (string.Equals(fileName, "action.tpp", StringComparison.OrdinalIgnoreCase)
                && !contents.Contains("Action<TArgs...>::Action(TCallable f) : func(f) {}", StringComparison.Ordinal)) {
                return InsertActionCallableSupportImplementation(contents);
            }

            if (string.Equals(fileName, "AnimationPlayerComponent.cpp", StringComparison.OrdinalIgnoreCase)
                && contents.Contains("double wrapped = time % duration;", StringComparison.Ordinal)) {
                return RewriteAnimationPlayerFloatingPointModulo(contents);
            }

            return contents;
        }

        /// <summary>
        /// Rewrites the bundled native Array support so newly allocated storage value-initializes every slot.
        /// </summary>
        /// <param name="contents">Current array runtime header contents.</param>
        /// <returns>Updated array runtime header contents.</returns>
        static string InsertRuntimeArrayValueInitialization(string contents) {
            if (string.IsNullOrEmpty(contents)) {
                return contents;
            }

            string updatedContents = contents.Replace(
                "new T[length] : nullptr",
                "new T[length]() : nullptr",
                StringComparison.Ordinal);
            updatedContents = updatedContents.Replace(
                "new T[values.size()] : nullptr",
                "new T[values.size()]() : nullptr",
                StringComparison.Ordinal);
            return updatedContents;
        }

        /// <summary>
        /// Loads feature-manifest entries from the generated conversion report when it is present.
        /// </summary>
        /// <param name="generatedCoreRootPath">Absolute path to the generated core output root.</param>
        /// <returns>Ordered manifest entries derived from the generated feature decisions.</returns>
        static IReadOnlyList<string> LoadGeneratedFeatureManifestEntries(string generatedCoreRootPath) {
            string reportPath = Path.Combine(generatedCoreRootPath, "cpp-conversion-report.json");
            if (!File.Exists(reportPath)) {
                return [];
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(reportPath));
            if (!document.RootElement.TryGetProperty("buildFeatures", out JsonElement buildFeatures)
                || !buildFeatures.TryGetProperty("decisions", out JsonElement decisions)
                || decisions.ValueKind != JsonValueKind.Array) {
                return [];
            }

            List<string> entries = new();
            foreach (JsonElement decision in decisions.EnumerateArray()) {
                if (!decision.TryGetProperty("feature", out JsonElement featureElement)
                    || !decision.TryGetProperty("enabled", out JsonElement enabledElement)
                    || !decision.TryGetProperty("origin", out JsonElement originElement)) {
                    continue;
                }

                string feature = featureElement.GetString() ?? string.Empty;
                string origin = originElement.GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(feature)
                    || string.IsNullOrWhiteSpace(origin)
                    || (enabledElement.ValueKind != JsonValueKind.False && enabledElement.ValueKind != JsonValueKind.True)) {
                    continue;
                }

                string enabled = enabledElement.GetBoolean() ? "true" : "false";
                entries.Add($"    {{ HEFeature::{feature}, {enabled}, HEFeatureDecisionOrigin::{origin}, \"{feature}\" }},");
            }

            return entries;
        }

        /// <summary>
        /// Inserts one additional include immediately after the generated file's primary self-include.
        /// </summary>
        /// <param name="contents">Current generated file contents.</param>
        /// <param name="includeLine">Include line that should be inserted.</param>
        /// <returns>Updated generated file contents.</returns>
        static string InsertIncludeAfterOwnHeader(string contents, string includeLine) {
            if (string.IsNullOrEmpty(contents) || string.IsNullOrWhiteSpace(includeLine)) {
                return contents;
            }

            string[] lines = contents.Split(["\r\n", "\n"], StringSplitOptions.None);
            for (int index = 0; index < lines.Length; index++) {
                string line = lines[index];
                if (!line.StartsWith("#include \"", StringComparison.Ordinal)) {
                    continue;
                }

                List<string> updatedLines = new(lines.Length + 1);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++) {
                    updatedLines.Add(lines[lineIndex]);
                    if (lineIndex == index) {
                        updatedLines.Add(includeLine);
                    }
                }

                return string.Join(Environment.NewLine, updatedLines);
            }

            return includeLine + Environment.NewLine + contents;
        }

        /// <summary>
        /// Inserts the missing dictionary Clear helper into bundled native dictionary support.
        /// </summary>
        /// <param name="contents">Current native dictionary support contents.</param>
        /// <returns>Updated native dictionary support contents.</returns>
        static string InsertNativeDictionaryClearHelper(string contents) {
            if (string.IsNullOrEmpty(contents) || contents.Contains("void Clear()", StringComparison.Ordinal)) {
                return contents;
            }

            string newline = contents.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            string clearMethod = "    void Clear() {" + newline
                + "        this->clear();" + newline
                + "    }" + newline + newline;

            if (contents.Contains("    bool TryGetValue(", StringComparison.Ordinal)) {
                return contents.Replace("    bool TryGetValue(", clearMethod + "    bool TryGetValue(", StringComparison.Ordinal);
            }

            if (contents.Contains("    std::vector<TKey> Keys() const {", StringComparison.Ordinal)) {
                return contents.Replace("    std::vector<TKey> Keys() const {", clearMethod + "    std::vector<TKey> Keys() const {", StringComparison.Ordinal);
            }

            if (contents.Contains("};", StringComparison.Ordinal)) {
                return contents.Replace("};", clearMethod + "};", StringComparison.Ordinal);
            }

            return contents + newline + clearMethod;
        }

        /// <summary>
        /// Inserts the missing native finite-check helpers required by transpiled `double.IsNaN` and `double.IsInfinity` calls.
        /// </summary>
        /// <param name="contents">Current native number support contents.</param>
        /// <returns>Updated native number support contents.</returns>
        static string InsertNativeNumberFiniteHelpers(string contents) {
            if (string.IsNullOrEmpty(contents) || contents.Contains("static bool IsNaN(float value)", StringComparison.Ordinal)) {
                return contents;
            }

            string newline = contents.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            string helperMethods = "    static bool IsNaN(float value) {" + newline
                + "        return std::isnan(value);" + newline
                + "    }" + newline + newline
                + "    static bool IsNaN(double value) {" + newline
                + "        return std::isnan(value);" + newline
                + "    }" + newline + newline
                + "    static bool IsInfinity(float value) {" + newline
                + "        return std::isinf(value);" + newline
                + "    }" + newline + newline
                + "    static bool IsInfinity(double value) {" + newline
                + "        return std::isinf(value);" + newline
                + "    }" + newline + newline;

            if (contents.Contains("    static bool IsPositiveInfinity(float value)", StringComparison.Ordinal)) {
                return contents.Replace("    static bool IsPositiveInfinity(float value)", helperMethods + "    static bool IsPositiveInfinity(float value)", StringComparison.Ordinal);
            }

            if (contents.Contains("};", StringComparison.Ordinal)) {
                return contents.Replace("};", helperMethods + "};", StringComparison.Ordinal);
            }

            return contents + newline + helperMethods;
        }

        /// <summary>
        /// Inserts the missing native `Math.Atan2` and `MathF.Atan2` helpers required by transpiled orbit and gizmo code.
        /// </summary>
        /// <param name="contents">Current native math support contents.</param>
        /// <returns>Updated native math support contents.</returns>
        static string InsertNativeMathAtan2Helpers(string contents) {
            if (string.IsNullOrEmpty(contents) || contents.Contains("static double Atan2(", StringComparison.Ordinal)) {
                return contents;
            }

            string updatedContents = contents.Replace(
                "    template <typename TValue>\r\n    static double Sqrt(TValue value) {",
                "    template <typename TY, typename TX>\r\n    static double Atan2(TY y, TX x) {\r\n        return std::atan2(static_cast<double>(y), static_cast<double>(x));\r\n    }\r\n\r\n    template <typename TValue>\r\n    static double Sqrt(TValue value) {",
                StringComparison.Ordinal);
            updatedContents = updatedContents.Replace(
                "    template <typename TValue>\n    static double Sqrt(TValue value) {",
                "    template <typename TY, typename TX>\n    static double Atan2(TY y, TX x) {\n        return std::atan2(static_cast<double>(y), static_cast<double>(x));\n    }\n\n    template <typename TValue>\n    static double Sqrt(TValue value) {",
                StringComparison.Ordinal);
            updatedContents = updatedContents.Replace(
                "    template <typename TValue>\r\n    static float Sqrt(TValue value) {",
                "    template <typename TY, typename TX>\r\n    static float Atan2(TY y, TX x) {\r\n        return static_cast<float>(Math::Atan2(y, x));\r\n    }\r\n\r\n    template <typename TValue>\r\n    static float Sqrt(TValue value) {",
                StringComparison.Ordinal);
            updatedContents = updatedContents.Replace(
                "    template <typename TValue>\n    static float Sqrt(TValue value) {",
                "    template <typename TY, typename TX>\n    static float Atan2(TY y, TX x) {\n        return static_cast<float>(Math::Atan2(y, x));\n    }\n\n    template <typename TValue>\n    static float Sqrt(TValue value) {",
                StringComparison.Ordinal);
            return updatedContents;
        }

        /// <summary>
        /// Upgrades bundled native Action support to store arbitrary callables needed by captured generated lambdas.
        /// </summary>
        /// <param name="contents">Current action header contents.</param>
        /// <returns>Updated action header contents.</returns>
        static string InsertActionCallableSupportHeader(string contents) {
            if (string.IsNullOrEmpty(contents)) {
                return contents;
            }

            string updatedContents = contents;
            if (!updatedContents.Contains("#include <functional>", StringComparison.Ordinal)) {
                updatedContents = updatedContents.Replace(
                    "#define ACTION_HPP",
                    "#define ACTION_HPP" + Environment.NewLine + Environment.NewLine + "#include <functional>",
                    StringComparison.Ordinal);
            }

            updatedContents = updatedContents.Replace(
                "    FuncType func = nullptr;",
                "    std::function<void(TArgs...)> func{};",
                StringComparison.Ordinal);

            if (!updatedContents.Contains("template<typename TCallable>", StringComparison.Ordinal)) {
                updatedContents = updatedContents.Replace(
                    "    explicit Action(FuncType f);",
                    "    explicit Action(FuncType f);" + Environment.NewLine
                    + "    template<typename TCallable>" + Environment.NewLine
                    + "    explicit Action(TCallable f) : func(f) {}",
                    StringComparison.Ordinal);
            }

            return updatedContents;
        }

        /// <summary>
        /// Upgrades bundled native Action implementation to support arbitrary captured callables.
        /// </summary>
        /// <param name="contents">Current action template implementation contents.</param>
        /// <returns>Updated action template implementation contents.</returns>
        static string InsertActionCallableSupportImplementation(string contents) {
            if (string.IsNullOrEmpty(contents)) {
                return contents;
            }

            string updatedContents = contents;
            if (updatedContents.Contains("template<typename TCallable>", StringComparison.Ordinal)
                && updatedContents.Contains("Action<TArgs...>::Action(TCallable f) : func(f) {}", StringComparison.Ordinal)) {
                updatedContents = Regex.Replace(
                    updatedContents,
                    @"template<typename\.\.\. TArgs>\r?\ntemplate<typename TCallable>\r?\nAction<TArgs\.\.\.>::Action\(TCallable f\) : func\(f\) \{\}\r?\n\r?\n",
                    string.Empty,
                    RegexOptions.CultureInvariant);
            }

            updatedContents = updatedContents.Replace(
                "    return func != nullptr;",
                "    return static_cast<bool>(func);",
                StringComparison.Ordinal);

            return updatedContents;
        }

        /// <summary>
        /// Rewrites floating-point modulo in generated animation looping code into a valid `std::fmod` call for C++.
        /// </summary>
        /// <param name="contents">Current generated animation-player source contents.</param>
        /// <returns>Updated animation-player source contents.</returns>
        static string RewriteAnimationPlayerFloatingPointModulo(string contents) {
            if (string.IsNullOrEmpty(contents)) {
                return contents;
            }

            string updatedContents = contents;
            if (!updatedContents.Contains("#include <cmath>", StringComparison.Ordinal)) {
                updatedContents = InsertIncludeAfterOwnHeader(updatedContents, "#include <cmath>");
            }

            updatedContents = updatedContents.Replace(
                "double wrapped = time % duration;",
                "double wrapped = std::fmod(static_cast<double>(time), duration);",
                StringComparison.Ordinal);
            return updatedContents;
        }

        /// <summary>
        /// Inserts the missing managed-style single-character replace helper into bundled native string support.
        /// </summary>
        /// <param name="contents">Current native string support contents.</param>
        /// <returns>Updated native string support contents.</returns>
        static string InsertNativeStringReplaceHelper(string contents) {
            if (string.IsNullOrEmpty(contents) || contents.Contains("static std::string Replace(const std::string& value, char oldValue, char newValue)", StringComparison.Ordinal)) {
                return contents;
            }

            string newline = contents.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            string replaceMethod = "    static std::string Replace(const std::string& value, char oldValue, char newValue) {" + newline
                + "        std::string replaced = value;" + newline
                + "        std::replace(replaced.begin(), replaced.end(), oldValue, newValue);" + newline
                + "        return replaced;" + newline
                + "    }" + newline + newline;

            if (contents.Contains("    static std::string Insert(", StringComparison.Ordinal)) {
                return contents.Replace("    static std::string Insert(", replaceMethod + "    static std::string Insert(", StringComparison.Ordinal);
            }

            if (contents.Contains("};", StringComparison.Ordinal)) {
                return contents.Replace("};", replaceMethod + "};", StringComparison.Ordinal);
            }

            return contents + newline + replaceMethod;
        }

        /// <summary>
        /// Inserts runtime feature enum entries that generated manifest bodies already reference during native validation builds.
        /// </summary>
        /// <param name="contents">Current feature-manifest header contents.</param>
        /// <returns>Updated feature-manifest header contents.</returns>
        static string InsertMissingFeatureManifestEntries(string contents) {
            if (string.IsNullOrEmpty(contents) || contents.Contains("HostFileSystem", StringComparison.Ordinal)) {
                return contents;
            }

            int enumStartIndex = contents.IndexOf("enum class HEFeature {", StringComparison.Ordinal);
            if (enumStartIndex < 0) {
                return contents;
            }

            int enumEndIndex = contents.IndexOf("};", enumStartIndex, StringComparison.Ordinal);
            if (enumEndIndex < 0) {
                return contents;
            }

            string replacement = "enum class HEFeature {" + Environment.NewLine
                + "    Render2D," + Environment.NewLine
                + "    Sprites," + Environment.NewLine
                + "    Text2D," + Environment.NewLine
                + "    Shaders," + Environment.NewLine
                + "    DebugOverlay," + Environment.NewLine
                + "    HostFileSystem," + Environment.NewLine
                + "    ReflectionLikeRuntime," + Environment.NewLine
                + "    RuntimeJson," + Environment.NewLine
                + "    TextProcessing" + Environment.NewLine
                + "};";

            return contents.Substring(0, enumStartIndex)
                + replacement
                + contents.Substring(enumEndIndex + 2);
        }

        /// <summary>
        /// Rewrites the generated feature-manifest body so it matches the feature decisions recorded by conversion.
        /// </summary>
        /// <param name="contents">Current feature-manifest source contents.</param>
        /// <param name="featureManifestEntries">Feature-manifest entries derived from the generated conversion report.</param>
        /// <returns>Updated feature-manifest source contents.</returns>
        static string RewriteFeatureManifestEntries(string contents, IReadOnlyList<string> featureManifestEntries) {
            if (string.IsNullOrEmpty(contents) || featureManifestEntries == null || featureManifestEntries.Count == 0) {
                return contents;
            }

            const string manifestArrayDeclaration = "static const HEFeatureEntry kFeatureEntries[] = {";
            int declarationIndex = contents.IndexOf(manifestArrayDeclaration, StringComparison.Ordinal);
            if (declarationIndex < 0) {
                return contents;
            }

            int arrayEndIndex = contents.IndexOf("};", declarationIndex + manifestArrayDeclaration.Length, StringComparison.Ordinal);
            if (arrayEndIndex < 0) {
                return contents;
            }

            string newline = contents.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            StringBuilder replacementBuilder = new();
            replacementBuilder.Append(manifestArrayDeclaration);
            replacementBuilder.Append(newline);
            for (int index = 0; index < featureManifestEntries.Count; index++) {
                replacementBuilder.Append(featureManifestEntries[index]);
                replacementBuilder.Append(newline);
            }

            replacementBuilder.Append("};");
            return contents.Substring(0, declarationIndex)
                + replacementBuilder.ToString()
                + contents.Substring(arrayEndIndex + 2);
        }

        /// <summary>
        /// Inserts the missing Path.ChangeExtension declaration into bundled path support.
        /// </summary>
        /// <param name="contents">Current path header contents.</param>
        /// <returns>Updated path header contents.</returns>
        static string InsertPathChangeExtensionDeclaration(string contents) {
            if (string.IsNullOrEmpty(contents) || contents.Contains("ChangeExtension", StringComparison.Ordinal)) {
                return contents;
            }

            string newline = contents.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            string declaration = "    static std::string ChangeExtension(const std::string& path, const std::string& extension);" + newline + newline;

            if (contents.Contains("    static bool IsPathRooted", StringComparison.Ordinal)) {
                return contents.Replace("    static bool IsPathRooted", declaration + "    static bool IsPathRooted", StringComparison.Ordinal);
            }

            if (contents.Contains("};", StringComparison.Ordinal)) {
                return contents.Replace("};", declaration + "};", StringComparison.Ordinal);
            }

            return contents + newline + declaration;
        }

        /// <summary>
        /// Inserts the missing Path.ChangeExtension implementation into bundled path support.
        /// </summary>
        /// <param name="contents">Current path source contents.</param>
        /// <returns>Updated path source contents.</returns>
        static string InsertPathChangeExtensionImplementation(string contents) {
            if (string.IsNullOrEmpty(contents) || contents.Contains("Path::ChangeExtension", StringComparison.Ordinal)) {
                return contents;
            }

            string newline = contents.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            string implementation = "std::string Path::ChangeExtension(const std::string& path, const std::string& extension) {" + newline
                + "    if (path.empty()) {" + newline
                + "        return std::string();" + newline
                + "    }" + newline + newline
                + "    std::filesystem::path updatedPath(path);" + newline
                + "    updatedPath.replace_extension(extension);" + newline
                + "    return updatedPath.string();" + newline
                + "}" + newline + newline;

            if (contents.Contains("bool Path::IsPathRooted", StringComparison.Ordinal)) {
                return contents.Replace("bool Path::IsPathRooted", implementation + "bool Path::IsPathRooted", StringComparison.Ordinal);
            }

            return contents + newline + implementation;
        }

        /// <summary>
        /// Inserts PS2-specific device-path handling into generated native path support so `cdrom0:` roots avoid `std::filesystem` normalization.
        /// </summary>
        /// <param name="contents">Current path source contents.</param>
        /// <returns>Updated path source contents.</returns>
        static string InsertPs2PathSupport(string contents) {
            if (string.IsNullOrEmpty(contents) || contents.Contains("CombinePs2Path", StringComparison.Ordinal)) {
                return contents;
            }

            string newline = contents.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            string helpers = "#if defined(PS2_PLATFORM)" + newline
                + "namespace {" + newline
                + "    bool IsPs2DevicePath(const std::string& path) {" + newline
                + "        return path.rfind(\"cdrom0:\", 0) == 0" + newline
                + "            || path.rfind(\"host:\", 0) == 0" + newline
                + "            || path.rfind(\"mc0:\", 0) == 0" + newline
                + "            || path.rfind(\"mc1:\", 0) == 0" + newline
                + "            || path.rfind(\"mass:\", 0) == 0;" + newline
                + "    }" + newline + newline
                + "    std::string NormalizePs2Path(const std::string& path) {" + newline
                + "        if (path.empty()) {" + newline
                + "            return path;" + newline
                + "        }" + newline
                + "        std::string normalized = path;" + newline
                + "        std::replace(normalized.begin(), normalized.end(), '/', '\\\\');" + newline
                + "        const std::size_t deviceSeparatorIndex = normalized.find(':');" + newline
                + "        if (deviceSeparatorIndex == std::string::npos) {" + newline
                + "            return normalized;" + newline
                + "        }" + newline
                + "        std::string prefix = normalized.substr(0, deviceSeparatorIndex + 1);" + newline
                + "        std::string suffix = normalized.substr(deviceSeparatorIndex + 1);" + newline
                + "        while (!suffix.empty() && suffix.front() == '\\\\') {" + newline
                + "            suffix.erase(suffix.begin());" + newline
                + "        }" + newline
                + "        std::string collapsedSuffix;" + newline
                + "        bool previousWasSeparator = false;" + newline
                + "        for (char character : suffix) {" + newline
                + "            if (character == '\\\\') {" + newline
                + "                if (!previousWasSeparator) {" + newline
                + "                    collapsedSuffix.push_back(character);" + newline
                + "                }" + newline
                + "                previousWasSeparator = true;" + newline
                + "                continue;" + newline
                + "            }" + newline
                + "            collapsedSuffix.push_back(character);" + newline
                + "            previousWasSeparator = false;" + newline
                + "        }" + newline
                + "        if (collapsedSuffix.empty()) {" + newline
                + "            return prefix + \"\\\\\";" + newline
                + "        }" + newline
                + "        return prefix + \"\\\\\" + collapsedSuffix;" + newline
                + "    }" + newline + newline
                + "    std::string CombinePs2Path(const std::string& left, const std::string& right) {" + newline
                + "        if (left.empty()) {" + newline
                + "            return NormalizePs2Path(right);" + newline
                + "        }" + newline
                + "        if (right.empty()) {" + newline
                + "            return NormalizePs2Path(left);" + newline
                + "        }" + newline
                + "        if (IsPs2DevicePath(right)) {" + newline
                + "            return NormalizePs2Path(right);" + newline
                + "        }" + newline
                + "        std::string normalizedLeft = NormalizePs2Path(left);" + newline
                + "        std::string normalizedRight = NormalizePs2Path(right);" + newline
                + "        while (!normalizedRight.empty() && normalizedRight.front() == '\\\\') {" + newline
                + "            normalizedRight.erase(normalizedRight.begin());" + newline
                + "        }" + newline
                + "        if (normalizedLeft.back() != '\\\\') {" + newline
                + "            normalizedLeft.push_back('\\\\');" + newline
                + "        }" + newline
                + "        return normalizedLeft + normalizedRight;" + newline
                + "    }" + newline + newline
                + "    std::string GetPs2DirectoryName(const std::string& path) {" + newline
                + "        std::string normalized = NormalizePs2Path(path);" + newline
                + "        std::size_t separatorIndex = normalized.find_last_of(\"\\\\/\");" + newline
                + "        if (separatorIndex == std::string::npos) {" + newline
                + "            return std::string();" + newline
                + "        }" + newline
                + "        if (separatorIndex > 0 && normalized[separatorIndex - 1] == ':') {" + newline
                + "            return normalized.substr(0, separatorIndex + 1);" + newline
                + "        }" + newline
                + "        return normalized.substr(0, separatorIndex);" + newline
                + "    }" + newline + newline
                + "    std::string GetPs2FileName(const std::string& path) {" + newline
                + "        std::string normalized = NormalizePs2Path(path);" + newline
                + "        std::size_t separatorIndex = normalized.find_last_of(\"\\\\/\");" + newline
                + "        std::string fileName = separatorIndex == std::string::npos ? normalized : normalized.substr(separatorIndex + 1);" + newline
                + "        std::size_t versionSeparatorIndex = fileName.find(';');" + newline
                + "        if (versionSeparatorIndex != std::string::npos) {" + newline
                + "            fileName = fileName.substr(0, versionSeparatorIndex);" + newline
                + "        }" + newline
                + "        return fileName;" + newline
                + "    }" + newline
                + "}" + newline
                + "#endif" + newline + newline;
            string updatedContents = contents;
            if (!updatedContents.Contains("#include <algorithm>", StringComparison.Ordinal)) {
                updatedContents = InsertIncludeAfterOwnHeader(updatedContents, "#include <algorithm>");
            }
            if (updatedContents.Contains("#include <filesystem>" + newline, StringComparison.Ordinal)) {
                updatedContents = updatedContents.Replace("#include <filesystem>" + newline, "#include <filesystem>" + newline + newline + helpers, StringComparison.Ordinal);
            } else if (updatedContents.Contains("#include <filesystem>\n", StringComparison.Ordinal)) {
                updatedContents = updatedContents.Replace("#include <filesystem>\n", "#include <filesystem>\n\n" + helpers.Replace(newline, "\n", StringComparison.Ordinal), StringComparison.Ordinal);
            } else {
                updatedContents += newline + helpers;
            }

            updatedContents = updatedContents.Replace(
                "    return (std::filesystem::path(left) / right).lexically_normal().string();",
                "#if defined(PS2_PLATFORM)" + newline
                + "    if (IsPs2DevicePath(left) || IsPs2DevicePath(right)) {" + newline
                + "        return CombinePs2Path(left, right);" + newline
                + "    }" + newline
                + "#endif" + newline
                + "    return (std::filesystem::path(left) / right).lexically_normal().string();",
                StringComparison.Ordinal);
            updatedContents = updatedContents.Replace(
                "    return std::filesystem::path(path).parent_path().string();",
                "#if defined(PS2_PLATFORM)" + newline
                + "    if (IsPs2DevicePath(path)) {" + newline
                + "        return GetPs2DirectoryName(path);" + newline
                + "    }" + newline
                + "#endif" + newline
                + "    return std::filesystem::path(path).parent_path().string();",
                StringComparison.Ordinal);
            updatedContents = updatedContents.Replace(
                "    return std::filesystem::path(path).filename().string();",
                "#if defined(PS2_PLATFORM)" + newline
                + "    if (IsPs2DevicePath(path)) {" + newline
                + "        return GetPs2FileName(path);" + newline
                + "    }" + newline
                + "#endif" + newline
                + "    return std::filesystem::path(path).filename().string();",
                StringComparison.Ordinal);
            updatedContents = updatedContents.Replace(
                "    return std::filesystem::path(path).lexically_normal().string();",
                "#if defined(PS2_PLATFORM)" + newline
                + "    if (IsPs2DevicePath(path)) {" + newline
                + "        return NormalizePs2Path(path);" + newline
                + "    }" + newline
                + "#endif" + newline
                + "    return std::filesystem::path(path).lexically_normal().string();",
                StringComparison.Ordinal);
            updatedContents = updatedContents.Replace(
                "    return std::filesystem::path(path).is_absolute();",
                "#if defined(PS2_PLATFORM)" + newline
                + "    if (IsPs2DevicePath(path)) {" + newline
                + "        return true;" + newline
                + "    }" + newline
                + "#endif" + newline
                + "    return std::filesystem::path(path).is_absolute();",
                StringComparison.Ordinal);
            return updatedContents;
        }

        /// <summary>
        /// Inserts PS2-specific disc-read handling into generated native file helpers so existence checks and open calls resolve ISO9660 versioned paths.
        /// </summary>
        /// <param name="contents">Current file helper source contents.</param>
        /// <returns>Updated file helper source contents.</returns>
        static string InsertPs2FileSupport(string contents) {
            if (string.IsNullOrEmpty(contents) || contents.Contains("ResolvePs2DiscReadPath", StringComparison.Ordinal)) {
                return contents;
            }

            string newline = contents.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            string helpers = "#if defined(PS2_PLATFORM)" + newline
                + "namespace {" + newline
                + "    std::string ResolvePs2DiscReadPath(const std::string& path) {" + newline
                + "        if (path.rfind(\"cdrom0:\", 0) != 0) {" + newline
                + "            return path;" + newline
                + "        }" + newline
                + "        if (path.find(';') != std::string::npos) {" + newline
                + "            return path;" + newline
                + "        }" + newline
                + "        return path + \";1\";" + newline
                + "    }" + newline
                + "}" + newline
                + "#endif" + newline + newline;
            string updatedContents = contents;
            if (!updatedContents.Contains("#include <cstdio>", StringComparison.Ordinal)) {
                updatedContents = InsertIncludeAfterOwnHeader(updatedContents, "#include <cstdio>");
            }
            if (updatedContents.Contains("#include <fstream>", StringComparison.Ordinal)) {
                updatedContents = updatedContents.Replace("#include <fstream>" + newline, "#include <fstream>" + newline + helpers, StringComparison.Ordinal);
            } else {
                updatedContents = helpers + updatedContents;
            }

            updatedContents = Regex.Replace(
                updatedContents,
                @"std::ifstream file\(fileName\);\s*return file\.good\(\);",
                "#if defined(PS2_PLATFORM)" + newline
                + "\tstd::FILE* file = std::fopen(ResolvePs2DiscReadPath(fileName).c_str(), \"rb\");" + newline
                + "\tif (file == nullptr) {" + newline
                + "\t\treturn false;" + newline
                + "\t}" + newline
                + "\tstd::fclose(file);" + newline
                + "\treturn true;" + newline
                + "#else" + newline
                + "\tstd::ifstream file(fileName);" + newline
                + "\treturn file.good();" + newline
                + "#endif",
                RegexOptions.CultureInvariant);
            updatedContents = updatedContents.Replace(
                "\treturn new FileStream(filePath, FileMode::Open, FileAccess::Read, FileShare::Read);",
                "#if defined(PS2_PLATFORM)" + newline
                + "\treturn new FileStream(ResolvePs2DiscReadPath(filePath), FileMode::Open, FileAccess::Read, FileShare::Read);" + newline
                + "#else" + newline
                + "\treturn new FileStream(filePath, FileMode::Open, FileAccess::Read, FileShare::Read);" + newline
                + "#endif",
                StringComparison.Ordinal);
            return updatedContents;
        }

        /// <summary>
        /// Inserts PS2-specific disc-read handling into generated file-stream construction so direct native stream creation can open ISO9660 versioned paths.
        /// </summary>
        /// <param name="contents">Current file-stream source contents.</param>
        /// <returns>Updated file-stream source contents.</returns>
        static string InsertPs2FileStreamSupport(string contents) {
            if (string.IsNullOrEmpty(contents) || contents.Contains("ResolvePs2DiscReadPath", StringComparison.Ordinal)) {
                return contents;
            }

            string newline = contents.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            string helpers = "#if defined(PS2_PLATFORM)" + newline
                + "namespace {" + newline
                + "    std::string ResolvePs2DiscReadPath(const std::string& path) {" + newline
                + "        if (path.rfind(\"cdrom0:\", 0) != 0) {" + newline
                + "            return path;" + newline
                + "        }" + newline
                + "        if (path.find(';') != std::string::npos) {" + newline
                + "            return path;" + newline
                + "        }" + newline
                + "        return path + \";1\";" + newline
                + "    }" + newline
                + "}" + newline
                + "#endif" + newline + newline;
            string updatedContents = contents;
            if (updatedContents.Contains("#include <stdexcept>" + newline, StringComparison.Ordinal)) {
                updatedContents = updatedContents.Replace("#include <stdexcept>" + newline, "#include <stdexcept>" + newline + helpers, StringComparison.Ordinal);
            } else {
                updatedContents = helpers + updatedContents;
            }

            updatedContents = updatedContents.Replace(
                "    file = std::fopen(path, GetFileMode(mode));",
                "#if defined(PS2_PLATFORM)" + newline
                + "    file = std::fopen(ResolvePs2DiscReadPath(path).c_str(), GetFileMode(mode));" + newline
                + "#else" + newline
                + "    file = std::fopen(path, GetFileMode(mode));" + newline
                + "#endif",
                StringComparison.Ordinal);
            return updatedContents;
        }

        /// <summary>
        /// Removes every exact generated include line from one file while preserving the surrounding newline style.
        /// </summary>
        /// <param name="contents">Current generated file contents.</param>
        /// <param name="includeLine">Exact include directive that should be removed.</param>
        /// <returns>Updated file contents without the include directive.</returns>
        static string RemoveGeneratedIncludeLine(string contents, string includeLine) {
            if (string.IsNullOrEmpty(contents) || string.IsNullOrWhiteSpace(includeLine)) {
                return contents;
            }

            string updatedContents = contents.Replace(includeLine + "\r\n", string.Empty, StringComparison.Ordinal);
            updatedContents = updatedContents.Replace(includeLine + "\n", string.Empty, StringComparison.Ordinal);
            return updatedContents.Replace(includeLine, string.Empty, StringComparison.Ordinal);
        }

        /// <summary>
        /// Writes a full amalgamated translation unit that includes every generated native core source file in a stable order.
        /// </summary>
        /// <param name="generatedCoreRootPath">Absolute path to the generated core output root.</param>
        internal static void RewriteAmalgamatedTranslationUnit(string generatedCoreRootPath) {
            if (string.IsNullOrWhiteSpace(generatedCoreRootPath)) {
                throw new ArgumentException("Generated core root path must be provided.", nameof(generatedCoreRootPath));
            }

            if (!Directory.Exists(generatedCoreRootPath)) {
                return;
            }

            string amalgamatedSourcePath = Path.Combine(generatedCoreRootPath, "helengine_core_amalgamated.cpp");
            string legacyUnitySourcePath = Path.Combine(generatedCoreRootPath, "helengine_core_unity.cpp");
            string[] excludedAmalgamatedSourceRelativePaths = new[] {
                "runtime/runtime_startup_manifest.cpp",
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

            StringBuilder amalgamatedBuilder = new();
            amalgamatedBuilder.AppendLine("// Generated compile-validation amalgamated translation unit.");
            amalgamatedBuilder.AppendLine();
            for (int index = 0; index < sourceFiles.Count; index++) {
                string relativePath = Path.GetRelativePath(generatedCoreRootPath, sourceFiles[index]).Replace('\\', '/');
                bool excludedSource = false;
                for (int excludeIndex = 0; excludeIndex < excludedAmalgamatedSourceRelativePaths.Length; excludeIndex++) {
                    if (string.Equals(relativePath, excludedAmalgamatedSourceRelativePaths[excludeIndex], StringComparison.OrdinalIgnoreCase)) {
                        excludedSource = true;
                        break;
                    }
                }

                if (excludedSource) {
                    continue;
                }

                amalgamatedBuilder.Append("#include \"");
                amalgamatedBuilder.Append(relativePath);
                amalgamatedBuilder.AppendLine("\"");
            }

            string amalgamatedSourceContents = amalgamatedBuilder.ToString();
            File.WriteAllText(amalgamatedSourcePath, amalgamatedSourceContents);
            File.WriteAllText(legacyUnitySourcePath, amalgamatedSourceContents);
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
