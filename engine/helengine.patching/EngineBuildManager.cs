using System.Diagnostics;

namespace helengine.patching {
    /// <summary>
    /// Builds engine variants based on a selected patch set.
    /// </summary>
    public sealed class EngineBuildManager {
        readonly EngineBuildPlanBuilder planBuilder;
        readonly EngineProjectWriter projectWriter;

        /// <summary>
        /// Initializes a new engine build manager.
        /// </summary>
        public EngineBuildManager() {
            planBuilder = new EngineBuildPlanBuilder();
            projectWriter = new EngineProjectWriter();
        }

        /// <summary>
        /// Executes a build for the provided request.
        /// </summary>
        /// <param name="request">Build request.</param>
        /// <returns>Build result.</returns>
        public EngineBuildResult Build(EngineBuildRequest request) {
            if (request == null) {
                var missing = new EngineBuildResult(false, string.Empty, string.Empty, string.Empty);
                missing.AddError("Build request is required.");
                return missing;
            }

            string patchRoot = request.PatchRootPath;
            if (!string.IsNullOrWhiteSpace(patchRoot)) {
                EnginePatchPaths.EnsureDirectory(patchRoot);
            }

            var catalog = new EnginePatchCatalog();
            catalog.LoadFromRoot(request.PatchRootPath);
            var catalogErrors = catalog.Errors;
            if (catalogErrors.Count > 0) {
                var result = new EngineBuildResult(false, string.Empty, string.Empty, string.Empty);
                for (int i = 0; i < catalogErrors.Count; i++) {
                    result.AddError(catalogErrors[i]);
                }
                return result;
            }

            var resolver = new EnginePatchResolver(catalog);
            EnginePatchResolution resolution = resolver.Resolve(request.PatchIds);
            if (!resolution.Success) {
                var failed = new EngineBuildResult(false, string.Empty, string.Empty, string.Empty);
                IReadOnlyList<string> errors = resolution.Errors;
                for (int i = 0; i < errors.Count; i++) {
                    failed.AddError(errors[i]);
                }
                return failed;
            }

            EngineBuildPlan plan = planBuilder.Build(request, resolution);
            var cache = new EngineBuildCache(request.OutputRootPath);
            string assemblyPath = cache.GetAssemblyPath(plan);
            if (!request.ForceRebuild && cache.IsBuildAvailable(plan)) {
                var cached = new EngineBuildResult(true, plan.BuildId, plan.OutputPath, assemblyPath);
                cached.AddLog("Using cached engine build.");
                return cached;
            }

            projectWriter.WriteProject(plan);

            EngineBuildResult buildResult = RunBuild(plan, assemblyPath);
            if (buildResult.Success) {
                buildResult.AddLog("Engine build completed successfully.");
            }

            return buildResult;
        }

        /// <summary>
        /// Executes the dotnet build process for a plan.
        /// </summary>
        /// <param name="plan">Build plan to execute.</param>
        /// <param name="assemblyPath">Expected assembly output path.</param>
        /// <returns>Build result with logs.</returns>
        EngineBuildResult RunBuild(EngineBuildPlan plan, string assemblyPath) {
            var result = new EngineBuildResult(false, plan.BuildId, plan.OutputPath, assemblyPath);

            Directory.CreateDirectory(plan.OutputPath);

            var startInfo = new ProcessStartInfo {
                FileName = "dotnet",
                Arguments = BuildDotnetArguments(plan),
                WorkingDirectory = plan.BuildRootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try {
                using var process = Process.Start(startInfo);
                if (process == null) {
                    result.AddError("Failed to start dotnet build process.");
                    return result;
                }

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                AppendOutput(result, stdout, false);
                AppendOutput(result, stderr, true);

                if (process.ExitCode == 0) {
                    result.SetSuccess(true);
                } else {
                    result.AddError($"dotnet build failed with exit code {process.ExitCode}.");
                }
            } catch (Exception ex) {
                result.AddError($"Build failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Builds the dotnet build arguments for a plan.
        /// </summary>
        /// <param name="plan">Build plan.</param>
        /// <returns>Argument string.</returns>
        string BuildDotnetArguments(EngineBuildPlan plan) {
            string configuration = string.IsNullOrWhiteSpace(plan.Configuration) ? "Debug" : plan.Configuration;
            return $"build \"{plan.ProjectPath}\" -c {configuration} -o \"{plan.OutputPath}\"";
        }

        /// <summary>
        /// Appends process output into the result log lists.
        /// </summary>
        /// <param name="result">Build result.</param>
        /// <param name="output">Output string.</param>
        /// <param name="isError">True when output is error content.</param>
        void AppendOutput(EngineBuildResult result, string output, bool isError) {
            if (string.IsNullOrWhiteSpace(output)) {
                return;
            }

            string[] lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++) {
                if (isError) {
                    result.AddError(lines[i]);
                } else {
                    result.AddLog(lines[i]);
                }
            }
        }
    }
}
