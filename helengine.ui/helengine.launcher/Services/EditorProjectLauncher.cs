using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using helengine.editor.launcher.Models;

namespace helengine.editor.launcher.Services;

/// <summary>
/// Launches the installed helengine editor host required by one recent project.
/// </summary>
public sealed class EditorProjectLauncher {
    /// <summary>
    /// Starts the installed editor that matches the recent project's required engine version.
    /// </summary>
    /// <param name="project">Recent project that should be opened in the editor.</param>
    /// <param name="installedEngines">Installed engine builds currently known by the launcher.</param>
    public void Launch(RecentProject project, IReadOnlyList<EngineInstall> installedEngines) {
        if (project == null) {
            throw new ArgumentNullException(nameof(project));
        }

        if (installedEngines == null) {
            throw new ArgumentNullException(nameof(installedEngines));
        }

        string projectFilePath = Path.GetFullPath(project.Path);
        if (string.IsNullOrWhiteSpace(project.RequiredEngineVersion)) {
            throw new InvalidOperationException("Project does not define a required engine version.");
        }

        EngineInstall install = ResolveMatchingInstall(project.RequiredEngineVersion, installedEngines);
        ProcessStartInfo startInfo = BuildStartInfo(install, projectFilePath);

        try {
            Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the required helengine editor.");
        } catch (InvalidOperationException) {
            throw;
        } catch (Exception exception) {
            throw new InvalidOperationException($"Failed to start the required helengine editor. {exception.Message}");
        }
    }

    /// <summary>
    /// Resolves the installed engine that matches the exact required version declared by the project file.
    /// </summary>
    /// <param name="requiredEngineVersion">Exact engine version required by the project file.</param>
    /// <param name="installedEngines">Installed engine builds currently known by the launcher.</param>
    /// <returns>Matching installed engine build.</returns>
    static EngineInstall ResolveMatchingInstall(string requiredEngineVersion, IReadOnlyList<EngineInstall> installedEngines) {
        EngineInstall install = installedEngines.FirstOrDefault(
            currentInstall => string.Equals(currentInstall.Version, requiredEngineVersion, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Required engine version {requiredEngineVersion} is not installed.");

        if (string.IsNullOrWhiteSpace(install.InstallPath) || !Directory.Exists(install.InstallPath)) {
            throw new InvalidOperationException($"Installed engine version {requiredEngineVersion} is missing its install path.");
        }

        return install;
    }

    /// <summary>
    /// Builds the process-start configuration for the editor host found inside one engine install.
    /// </summary>
    /// <param name="install">Installed engine build that should open the project.</param>
    /// <param name="projectFilePath">Canonical project-file path passed to the editor.</param>
    /// <returns>Process-start configuration for the resolved editor host.</returns>
    static ProcessStartInfo BuildStartInfo(EngineInstall install, string projectFilePath) {
        string installPath = Path.GetFullPath(install.InstallPath);
        string projectArgument = $"\"{projectFilePath}\"";

        if (OperatingSystem.IsWindows()) {
            string executablePath = Path.Combine(installPath, "helengine.editor.app.exe");
            if (File.Exists(executablePath)) {
                return BuildDirectStartInfo(executablePath, projectArgument, installPath);
            }

            string commandPath = Path.Combine(installPath, "helengine.editor.app.cmd");
            if (File.Exists(commandPath)) {
                return new ProcessStartInfo {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"\"{commandPath}\" {projectArgument}\"",
                    WorkingDirectory = installPath,
                    UseShellExecute = false
                };
            }
        } else {
            string executablePath = Path.Combine(installPath, "helengine.editor.app");
            if (File.Exists(executablePath)) {
                return BuildDirectStartInfo(executablePath, projectArgument, installPath);
            }
        }

        string managedHostPath = Path.Combine(installPath, "helengine.editor.app.dll");
        if (File.Exists(managedHostPath)) {
            return new ProcessStartInfo {
                FileName = "dotnet",
                Arguments = $"\"{managedHostPath}\" {projectArgument}",
                WorkingDirectory = installPath,
                UseShellExecute = false
            };
        }

        throw new InvalidOperationException($"Could not find an editor host in {installPath}.");
    }

    /// <summary>
    /// Builds the process-start configuration for one directly executable editor host.
    /// </summary>
    /// <param name="executablePath">Resolved executable path inside the engine install.</param>
    /// <param name="projectArgument">Quoted project-file argument passed to the editor host.</param>
    /// <param name="workingDirectory">Working directory assigned to the launched process.</param>
    /// <returns>Process-start configuration for the direct executable.</returns>
    static ProcessStartInfo BuildDirectStartInfo(string executablePath, string projectArgument, string workingDirectory) {
        return new ProcessStartInfo {
            FileName = executablePath,
            Arguments = projectArgument,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };
    }
}
