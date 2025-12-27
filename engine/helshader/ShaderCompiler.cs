namespace helshader {
    /// <summary>
    /// Compiles shader sources using external toolchains.
    /// </summary>
    public class ShaderCompiler {
        /// <summary>
        /// Tool path configuration from the manifest.
        /// </summary>
        readonly ShaderManifestTools tools;

        /// <summary>
        /// Process runner used to invoke external tools.
        /// </summary>
        readonly ShaderProcessRunner processRunner;

        /// <summary>
        /// Initializes a new shader compiler.
        /// </summary>
        /// <param name="tools">Shader tool paths.</param>
        public ShaderCompiler(ShaderManifestTools tools) {
            if (tools == null) {
                throw new ArgumentNullException(nameof(tools));
            }

            this.tools = tools;
            processRunner = new ShaderProcessRunner();
        }

        /// <summary>
        /// Compiles a shader using the appropriate backend compiler.
        /// </summary>
        /// <param name="item">Shader compile item.</param>
        /// <param name="workingDirectory">Working directory for the compiler.</param>
        public void Compile(ShaderCompileItem item, string workingDirectory) {
            if (item == null) {
                throw new ArgumentNullException(nameof(item));
            }

            if (string.IsNullOrWhiteSpace(workingDirectory)) {
                throw new ArgumentException("Working directory must be provided.", nameof(workingDirectory));
            }

            if (string.Equals(item.Target, "dx9", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Target, "dx11", StringComparison.OrdinalIgnoreCase)) {
                CompileWithFxc(item, workingDirectory);
                return;
            }

            if (string.Equals(item.Target, "metal", StringComparison.OrdinalIgnoreCase)) {
                CompileMetal(item, workingDirectory);
                return;
            }

            CompileWithDxc(item, workingDirectory, string.Equals(item.Target, "vulkan", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Compiles a shader using the fxc compiler.
        /// </summary>
        /// <param name="item">Compile item.</param>
        /// <param name="workingDirectory">Working directory.</param>
        void CompileWithFxc(ShaderCompileItem item, string workingDirectory) {
            EnsureToolPath(tools.Fxc, "fxc");

            string args = BuildFxcArguments(item);
            ShaderProcessResult result = processRunner.Run(tools.Fxc, args, workingDirectory);
            EnsureSuccess(result, "fxc", item.OutputPath);
        }

        /// <summary>
        /// Compiles a shader using the dxc compiler.
        /// </summary>
        /// <param name="item">Compile item.</param>
        /// <param name="workingDirectory">Working directory.</param>
        /// <param name="emitSpirv">True to emit SPIR-V output.</param>
        void CompileWithDxc(ShaderCompileItem item, string workingDirectory, bool emitSpirv) {
            EnsureToolPath(tools.Dxc, "dxc");

            string args = BuildDxcArguments(item, emitSpirv, item.OutputPath);
            ShaderProcessResult result = processRunner.Run(tools.Dxc, args, workingDirectory);
            EnsureSuccess(result, "dxc", item.OutputPath);
        }

        /// <summary>
        /// Compiles a shader for Metal by emitting SPIR-V and translating to MSL.
        /// </summary>
        /// <param name="item">Compile item.</param>
        /// <param name="workingDirectory">Working directory.</param>
        void CompileMetal(ShaderCompileItem item, string workingDirectory) {
            EnsureToolPath(tools.Dxc, "dxc");
            EnsureToolPath(tools.SpirvCross, "spirv-cross");

            string spirvPath = Path.ChangeExtension(item.OutputPath, "spirv");
            string dxcArgs = BuildDxcArguments(item, true, spirvPath);
            ShaderProcessResult dxcResult = processRunner.Run(tools.Dxc, dxcArgs, workingDirectory);
            EnsureSuccess(dxcResult, "dxc", spirvPath);

            string spirvArgs = $"\"{spirvPath}\" --msl -o \"{item.OutputPath}\"";
            ShaderProcessResult mslResult = processRunner.Run(tools.SpirvCross, spirvArgs, workingDirectory);
            EnsureSuccess(mslResult, "spirv-cross", item.OutputPath);
        }

        /// <summary>
        /// Builds fxc compiler arguments.
        /// </summary>
        /// <param name="item">Compile item.</param>
        /// <returns>Argument string.</returns>
        string BuildFxcArguments(ShaderCompileItem item) {
            string defineArgs = BuildDefineArguments(item.Defines, "/D");
            string includeArgs = BuildIncludeArguments(item.IncludeDirs, "/I");
            return $"/T {item.Profile} /E {item.EntryPoint} /Fo \"{item.OutputPath}\" {defineArgs} {includeArgs} \"{item.SourcePath}\"";
        }

        /// <summary>
        /// Builds dxc compiler arguments.
        /// </summary>
        /// <param name="item">Compile item.</param>
        /// <param name="emitSpirv">True to emit SPIR-V.</param>
        /// <param name="outputPath">Output path for the compiler.</param>
        /// <returns>Argument string.</returns>
        string BuildDxcArguments(ShaderCompileItem item, bool emitSpirv, string outputPath) {
            string defineArgs = BuildDefineArguments(item.Defines, "-D");
            string includeArgs = BuildIncludeArguments(item.IncludeDirs, "-I");
            string spirvArg = emitSpirv ? "-spirv" : string.Empty;
            return $"-T {item.Profile} -E {item.EntryPoint} -Fo \"{outputPath}\" {spirvArg} {defineArgs} {includeArgs} \"{item.SourcePath}\"";
        }

        /// <summary>
        /// Builds define arguments for the compiler.
        /// </summary>
        /// <param name="defines">Define list.</param>
        /// <param name="prefix">Compiler prefix.</param>
        /// <returns>Argument string.</returns>
        string BuildDefineArguments(string[] defines, string prefix) {
            if (defines == null || defines.Length == 0) {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < defines.Length; i++) {
                string define = defines[i];
                if (string.IsNullOrWhiteSpace(define)) {
                    continue;
                }

                parts.Add($"{prefix} \"{define}\"");
            }

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Builds include directory arguments for the compiler.
        /// </summary>
        /// <param name="includeDirs">Include directories.</param>
        /// <param name="prefix">Compiler prefix.</param>
        /// <returns>Argument string.</returns>
        string BuildIncludeArguments(string[] includeDirs, string prefix) {
            if (includeDirs == null || includeDirs.Length == 0) {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < includeDirs.Length; i++) {
                string dir = includeDirs[i];
                if (string.IsNullOrWhiteSpace(dir)) {
                    continue;
                }

                parts.Add($"{prefix} \"{dir}\"");
            }

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Ensures the tool path is provided.
        /// </summary>
        /// <param name="toolPath">Tool path.</param>
        /// <param name="toolName">Tool name for error messages.</param>
        void EnsureToolPath(string toolPath, string toolName) {
            if (string.IsNullOrWhiteSpace(toolPath)) {
                throw new InvalidOperationException($"Tool path for '{toolName}' must be provided.");
            }
        }

        /// <summary>
        /// Ensures the process result completed successfully.
        /// </summary>
        /// <param name="result">Process result.</param>
        /// <param name="toolName">Tool name for error messages.</param>
        /// <param name="outputPath">Output path for error context.</param>
        void EnsureSuccess(ShaderProcessResult result, string toolName, string outputPath) {
            if (result.ExitCode != 0) {
                throw new InvalidOperationException($"{toolName} failed for '{outputPath}': {result.ErrorOutput}");
            }
        }
    }
}
