using System.Collections.Generic;

namespace helengine.editor.launcher.Models;

/// <summary>
/// Captures the planner output for one engine-platform install selection.
/// </summary>
public sealed class PlatformInstallPlan {
    /// <summary>
    /// Gets the artifacts that are already installed and can be reused without downloading again.
    /// </summary>
    public List<PlatformInstallPlanArtifactStatus> ReusableArtifacts { get; } = new();

    /// <summary>
    /// Gets the artifacts that are required but not installed locally yet.
    /// </summary>
    public List<PlatformInstallPlanArtifactStatus> MissingArtifacts { get; } = new();

    /// <summary>
    /// Gets the blocking issues that prevent the launcher from continuing with install planning.
    /// </summary>
    public List<string> BlockingIssues { get; } = new();
}
