using System.Diagnostics;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Executes the current Windows-only source-build pipeline for one queued editor build item.
    /// </summary>
    public sealed class EditorWindowsBuildExecutor : IEditorBuildExecutor {
        /// <summary>
        /// Platform id currently handled by the Windows source-build executor.
        /// </summary>
        const string WindowsPlatformId = "windows";

        /// <summary>
        /// Default executable name emitted by the Windows host build.
        /// </summary>
        const string WindowsExecutableName = "helengine_windows.exe";

        /// <summary>
        /// Default symbol file emitted by the Windows host debug build.
        /// </summary>
        const string WindowsSymbolFileName = "helengine_windows.pdb";

        /// <summary>
        /// Visual Studio bundled CMake path used by the current Windows source-build environment.
        /// </summary>
        const string VisualStudioCMakePath = @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe";

        /// <summary>
        /// Absolute source project root that owns the user-selected scenes.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Resolves sibling source-build repository roots required by the local Windows pipeline.
        /// </summary>
        readonly EditorSourceBuildWorkspaceLocator WorkspaceLocator;

        /// <summary>
        /// Initializes one Windows build executor for the supplied source project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative source project root path.</param>
        public EditorWindowsBuildExecutor(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            WorkspaceLocator = new EditorSourceBuildWorkspaceLocator();
        }

        /// <summary>
        /// Executes one queued Windows build item from generation through native build staging.
        /// </summary>
        /// <param name="queueItem">Queued build item that should be executed.</param>
        /// <returns>Structured execution result describing success or failure.</returns>
        public EditorBuildExecutionResult Execute(EditorBuildQueueItemDocument queueItem) {
            if (queueItem == null) {
                throw new ArgumentNullException(nameof(queueItem));
            }

            try {
                ValidateQueueItem(queueItem);

                EditorWindowsBuildPaths buildPaths = new EditorWindowsBuildPaths(queueItem.OutputDirectoryPath);
                ResetBuildDirectories(buildPaths);
                GenerateCore(buildPaths);
                PackageScenes(queueItem, buildPaths);
                BuildWindowsHost(buildPaths);
                CopyWindowsArtifacts(buildPaths);

                string executablePath = Path.Combine(buildPaths.BuildRootPath, WindowsExecutableName);
                return EditorBuildExecutionResult.Success($"Windows build completed: {executablePath}");
            } catch (Exception ex) {
                return EditorBuildExecutionResult.Failure($"Windows build failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates one queued build item before any filesystem or process work starts.
        /// </summary>
        /// <param name="queueItem">Queued build item to validate.</param>
        void ValidateQueueItem(EditorBuildQueueItemDocument queueItem) {
            if (!string.Equals(queueItem.PlatformId, WindowsPlatformId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Windows build executor cannot handle platform '{queueItem.PlatformId}'.");
            }
            if (queueItem.SelectedSceneIds == null || queueItem.SelectedSceneIds.Count == 0) {
                throw new InvalidOperationException("Windows builds require at least one selected scene.");
            }
            if (string.IsNullOrWhiteSpace(queueItem.OutputDirectoryPath)) {
                throw new InvalidOperationException("Windows builds require an output directory.");
            }
        }

        /// <summary>
        /// Clears and recreates the deployment-root folders used by the current Windows build.
        /// </summary>
        /// <param name="buildPaths">Build paths to reset.</param>
        void ResetBuildDirectories(EditorWindowsBuildPaths buildPaths) {
            DeleteDirectoryIfPresent(buildPaths.GeneratedSourceRootPath);
            DeleteDirectoryIfPresent(buildPaths.IntermediateRootPath);
            DeleteDirectoryIfPresent(buildPaths.BuildRootPath);

            Directory.CreateDirectory(buildPaths.GeneratedSourceRootPath);
            Directory.CreateDirectory(buildPaths.IntermediateRootPath);
            Directory.CreateDirectory(buildPaths.BuildRootPath);
        }

        /// <summary>
        /// Runs `cs2.cpp` through a temporary local audit runner project to regenerate the Windows generated-source tree.
        /// </summary>
        /// <param name="buildPaths">Build paths describing where generated output should be written.</param>
        void GenerateCore(EditorWindowsBuildPaths buildPaths) {
            string helEngineRootPath = WorkspaceLocator.ResolveHelEngineRootPath();
            string cSharpCodegenRootPath = WorkspaceLocator.ResolveCSharpCodegenRootPath();
            Directory.CreateDirectory(buildPaths.AuditRunnerRootPath);

            string auditRunnerProjectPath = Path.Combine(buildPaths.AuditRunnerRootPath, "AuditRunner.csproj");
            string auditRunnerProgramPath = Path.Combine(buildPaths.AuditRunnerRootPath, "Program.cs");
            string helEngineCoreProjectPath = Path.Combine(helEngineRootPath, "engine", "helengine.core", "helengine.core.csproj");
            string cSharpCodegenProjectPath = Path.Combine(cSharpCodegenRootPath, "cs2.cpp", "cs2.cpp.csproj");

            File.WriteAllText(auditRunnerProjectPath, BuildAuditRunnerProjectContents(cSharpCodegenProjectPath));
            File.WriteAllText(auditRunnerProgramPath, BuildAuditRunnerProgramContents(helEngineCoreProjectPath, buildPaths.GeneratedSourceRootPath));

            RunProcess(
                "dotnet",
                string.Concat("run --project \"", auditRunnerProjectPath, "\" -v minimal"),
                buildPaths.AuditRunnerRootPath);
        }

        /// <summary>
        /// Packages the selected scenes and their required runtime assets into the final build root.
        /// </summary>
        /// <param name="queueItem">Queued build item describing which scenes should be packaged.</param>
        /// <param name="buildPaths">Build paths describing the final build root.</param>
        void PackageScenes(EditorBuildQueueItemDocument queueItem, EditorWindowsBuildPaths buildPaths) {
            EditorWindowsBuildScenePackager packager = new EditorWindowsBuildScenePackager(ProjectRootPath);
            packager.Package(queueItem.SelectedSceneIds, buildPaths.BuildRootPath);
        }

        /// <summary>
        /// Configures and builds the Windows host against the freshly generated core output.
        /// </summary>
        /// <param name="buildPaths">Build paths describing generated-source and intermediate roots.</param>
        void BuildWindowsHost(EditorWindowsBuildPaths buildPaths) {
            string helEngineWindowsRootPath = WorkspaceLocator.ResolveHelEngineWindowsRootPath();
            string cmakePath = ResolveCMakePath();

            RunProcess(
                cmakePath,
                BuildCMakeConfigureArguments(helEngineWindowsRootPath, buildPaths),
                helEngineWindowsRootPath);
            RunProcess(
                cmakePath,
                string.Concat("--build \"", buildPaths.CMakeBuildRootPath, "\" --config Debug"),
                helEngineWindowsRootPath);
        }

        /// <summary>
        /// Copies the native Windows build outputs from the CMake build folder into the final build root.
        /// </summary>
        /// <param name="buildPaths">Build paths describing the intermediate and final build roots.</param>
        void CopyWindowsArtifacts(EditorWindowsBuildPaths buildPaths) {
            string debugOutputRootPath = Path.Combine(buildPaths.CMakeBuildRootPath, "Debug");
            if (!Directory.Exists(debugOutputRootPath)) {
                throw new InvalidOperationException($"Windows build output folder '{debugOutputRootPath}' was not produced.");
            }

            CopyFileIfPresent(
                Path.Combine(debugOutputRootPath, WindowsExecutableName),
                Path.Combine(buildPaths.BuildRootPath, WindowsExecutableName),
                true);
            CopyFileIfPresent(
                Path.Combine(debugOutputRootPath, WindowsSymbolFileName),
                Path.Combine(buildPaths.BuildRootPath, WindowsSymbolFileName),
                false);
        }

        /// <summary>
        /// Builds the temporary audit-runner project file contents used to invoke `cs2.cpp`.
        /// </summary>
        /// <param name="cSharpCodegenProjectPath">Absolute `cs2.cpp` project path.</param>
        /// <returns>Audit-runner project file contents.</returns>
        string BuildAuditRunnerProjectContents(string cSharpCodegenProjectPath) {
            if (string.IsNullOrWhiteSpace(cSharpCodegenProjectPath)) {
                throw new ArgumentException("csharpcodegen project path must be provided.", nameof(cSharpCodegenProjectPath));
            }

            return
                "<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
                "  <PropertyGroup>\n" +
                "    <OutputType>Exe</OutputType>\n" +
                "    <TargetFramework>net9.0</TargetFramework>\n" +
                "  </PropertyGroup>\n" +
                "  <ItemGroup>\n" +
                "    <ProjectReference Include=\"" + EscapeForXmlAttribute(cSharpCodegenProjectPath) + "\" />\n" +
                "  </ItemGroup>\n" +
                "</Project>\n";
        }

        /// <summary>
        /// Builds the temporary audit-runner program contents used to invoke `cs2.cpp`.
        /// </summary>
        /// <param name="helEngineCoreProjectPath">Absolute `helengine.core` project path.</param>
        /// <param name="generatedSourceRootPath">Absolute generated-source output path.</param>
        /// <returns>Audit-runner program contents.</returns>
        string BuildAuditRunnerProgramContents(string helEngineCoreProjectPath, string generatedSourceRootPath) {
            if (string.IsNullOrWhiteSpace(helEngineCoreProjectPath)) {
                throw new ArgumentException("HelEngine core project path must be provided.", nameof(helEngineCoreProjectPath));
            }
            if (string.IsNullOrWhiteSpace(generatedSourceRootPath)) {
                throw new ArgumentException("Generated source root path must be provided.", nameof(generatedSourceRootPath));
            }

            return
                "using cs2.cpp;\n" +
                "\n" +
                "CPPConversionOptions options = CPPConversionOptions.CreateDefault();\n" +
                "options.LoadNativeRuntimeMetadata = false;\n" +
                "options.WriteConversionReport = true;\n" +
                "\n" +
                "CPPConversionRules rules = new CPPConversionRules();\n" +
                "CPPCodeConverter converter = new CPPCodeConverter(rules, options);\n" +
                "converter.AddCsproj(@\"" + EscapeForCSharpVerbatimString(helEngineCoreProjectPath) + "\");\n" +
                "converter.WriteOutput(@\"" + EscapeForCSharpVerbatimString(generatedSourceRootPath) + "\");\n";
        }

        /// <summary>
        /// Builds the CMake configure argument string used by the native Windows host.
        /// </summary>
        /// <param name="helEngineWindowsRootPath">Absolute `helengine-windows` repo root path.</param>
        /// <param name="buildPaths">Build paths describing generated-source and intermediate roots.</param>
        /// <returns>CMake configure argument string.</returns>
        string BuildCMakeConfigureArguments(string helEngineWindowsRootPath, EditorWindowsBuildPaths buildPaths) {
            if (string.IsNullOrWhiteSpace(helEngineWindowsRootPath)) {
                throw new ArgumentException("helengine-windows root path must be provided.", nameof(helEngineWindowsRootPath));
            }
            if (buildPaths == null) {
                throw new ArgumentNullException(nameof(buildPaths));
            }

            return string.Concat(
                "-S \"", helEngineWindowsRootPath,
                "\" -B \"", buildPaths.CMakeBuildRootPath,
                "\" -DHELENGINE_WINDOWS_INCLUDE_GENERATED_CORE=ON",
                " -DHELENGINE_CORE_CPP_ROOT=\"", buildPaths.GeneratedSourceRootPath,
                "\" -DHELENGINE_WINDOWS_RENDER_BACKEND=DirectX11");
        }

        /// <summary>
        /// Resolves the CMake executable path used by the local Windows source-build environment.
        /// </summary>
        /// <returns>CMake executable path or command name.</returns>
        string ResolveCMakePath() {
            if (File.Exists(VisualStudioCMakePath)) {
                return VisualStudioCMakePath;
            }

            return "cmake";
        }

        /// <summary>
        /// Runs one external process and throws when it exits unsuccessfully.
        /// </summary>
        /// <param name="fileName">Executable path or command name.</param>
        /// <param name="arguments">Argument string passed to the process.</param>
        /// <param name="workingDirectory">Working directory used by the process.</param>
        void RunProcess(string fileName, string arguments, string workingDirectory) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("Executable path must be provided.", nameof(fileName));
            }
            if (string.IsNullOrWhiteSpace(workingDirectory)) {
                throw new ArgumentException("Working directory must be provided.", nameof(workingDirectory));
            }

            var startInfo = new ProcessStartInfo {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process process = Process.Start(startInfo);
            if (process == null) {
                throw new InvalidOperationException($"Failed to start process '{fileName}'.");
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode == 0) {
                return;
            }

            StringBuilder messageBuilder = new StringBuilder();
            messageBuilder.Append(fileName);
            messageBuilder.Append(" exited with code ");
            messageBuilder.Append(process.ExitCode);
            messageBuilder.Append('.');

            string output = ChooseFailureOutput(stdout, stderr);
            if (!string.IsNullOrWhiteSpace(output)) {
                messageBuilder.Append(' ');
                messageBuilder.Append(output.Trim());
            }

            throw new InvalidOperationException(messageBuilder.ToString());
        }

        /// <summary>
        /// Chooses the most useful captured process output for a failure message.
        /// </summary>
        /// <param name="stdout">Captured standard output.</param>
        /// <param name="stderr">Captured standard error.</param>
        /// <returns>Preferred captured output snippet.</returns>
        string ChooseFailureOutput(string stdout, string stderr) {
            if (!string.IsNullOrWhiteSpace(stderr)) {
                return stderr;
            }

            return stdout ?? string.Empty;
        }

        /// <summary>
        /// Deletes one directory recursively when it already exists.
        /// </summary>
        /// <param name="path">Absolute directory path to delete.</param>
        void DeleteDirectoryIfPresent(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Directory path must be provided.", nameof(path));
            }

            if (Directory.Exists(path)) {
                Directory.Delete(path, true);
            }
        }

        /// <summary>
        /// Copies one file into the final build root when it exists, optionally requiring the file to be present.
        /// </summary>
        /// <param name="sourcePath">Absolute source file path.</param>
        /// <param name="targetPath">Absolute target file path.</param>
        /// <param name="required">True when the source file must exist; otherwise false.</param>
        void CopyFileIfPresent(string sourcePath, string targetPath, bool required) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }
            if (string.IsNullOrWhiteSpace(targetPath)) {
                throw new ArgumentException("Target path must be provided.", nameof(targetPath));
            }

            if (!File.Exists(sourcePath)) {
                if (required) {
                    throw new InvalidOperationException($"Required Windows build output '{sourcePath}' was not produced.");
                }

                return;
            }

            string directoryPath = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Target directory could not be resolved.");
            }

            Directory.CreateDirectory(directoryPath);
            File.Copy(sourcePath, targetPath, true);
        }

        /// <summary>
        /// Escapes one path for use inside a generated XML attribute value.
        /// </summary>
        /// <param name="value">Path string to escape.</param>
        /// <returns>Escaped XML attribute value.</returns>
        string EscapeForXmlAttribute(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new ArgumentException("XML value must be provided.", nameof(value));
            }

            return value
                .Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("\"", "&quot;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal);
        }

        /// <summary>
        /// Escapes one path for use inside a generated C# verbatim string literal.
        /// </summary>
        /// <param name="value">Path string to escape.</param>
        /// <returns>Escaped verbatim-string value.</returns>
        string EscapeForCSharpVerbatimString(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new ArgumentException("C# string value must be provided.", nameof(value));
            }

            return value.Replace("\"", "\"\"", StringComparison.Ordinal);
        }
    }
}
