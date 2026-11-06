using System.Diagnostics;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Stands in for the C++ converter by running a real <c>dotnet build</c> of the synthetic module project the cook service
/// hands to the converter, so tests prove the module compiles against its declared engine references.
/// </summary>
sealed class ManagedCompileCodegenToolRunner : IEditorCodegenToolRunner {
    /// <summary>
    /// MSBuild variables the test host sets for itself; inheriting them would point the child build at the wrong SDK.
    /// </summary>
    static readonly string[] InheritedMsBuildVariables = [
        "MSBuildExtensionsPath",
        "MSBuildSDKsPath",
        "MSBUILD_EXE_PATH",
        "MSBuildLoadMicrosoftTargetsReadOnly"
    ];

    /// <summary>
    /// Gets the exit code of each managed build, in invocation order.
    /// </summary>
    public List<int> ExitCodes { get; } = [];

    /// <summary>
    /// Gets the combined standard output and error of each managed build, in invocation order.
    /// </summary>
    public List<string> Outputs { get; } = [];

    /// <summary>
    /// Builds the project named by the converter's <c>--project</c> argument and records the result.
    /// </summary>
    /// <param name="toolPath">Converter path the service would have launched; unused by the managed build.</param>
    /// <param name="arguments">Converter arguments, which must contain <c>--project</c>.</param>
    /// <param name="workingDirectory">Working directory for the build process.</param>
    public void Run(string toolPath, IReadOnlyList<string> arguments, string workingDirectory) {
        string projectPath = ResolveProjectPath(arguments);
        ProcessStartInfo startInfo = new("dotnet") {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("-nologo");
        startInfo.ArgumentList.Add("-v:q");
        for (int index = 0; index < InheritedMsBuildVariables.Length; index++) {
            startInfo.Environment.Remove(InheritedMsBuildVariables[index]);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"dotnet build could not be started for '{projectPath}'.");
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
        string standardOutput = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        ExitCodes.Add(process.ExitCode);
        Outputs.Add(standardOutput + standardErrorTask.Result);
    }

    /// <summary>
    /// Finds the synthetic module project passed to the converter.
    /// </summary>
    /// <param name="arguments">Converter arguments.</param>
    /// <returns>The value following <c>--project</c>.</returns>
    static string ResolveProjectPath(IReadOnlyList<string> arguments) {
        for (int index = 0; index < arguments.Count - 1; index++) {
            if (string.Equals(arguments[index], "--project", StringComparison.OrdinalIgnoreCase)) {
                return arguments[index + 1];
            }
        }

        throw new InvalidOperationException("Expected the module codegen invocation to supply `--project`.");
    }
}
