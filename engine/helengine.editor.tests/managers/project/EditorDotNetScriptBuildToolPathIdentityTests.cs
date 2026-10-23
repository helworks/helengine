using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace helengine.editor.tests.managers.project;

/// <summary>Checks that MSBuild receives one physical identity for a generated solution and its workspace.</summary>
public sealed class EditorDotNetScriptBuildToolPathIdentityTests : IDisposable {
    /// <summary>Owns only this class's fixed temporary fixture beneath the configured test temporary root.</summary>
    readonly string Root = Path.Combine(Path.GetTempPath(), "script-build-path-identity");

    /// <summary>Creates an ordinary workspace used as the target of a directory link.</summary>
    public EditorDotNetScriptBuildToolPathIdentityTests() {
        Directory.CreateDirectory(Path.Combine(Root, "physical", "workspace"));
    }

    /// <summary>Ensures a linked ancestor cannot give solution configuration and project references different identities.</summary>
    [Fact]
    public void CreateBuildStartInfo_resolves_linked_workspace_and_solution_to_the_same_physical_tree() {
        string physicalWorkspace = Path.Combine(Root, "physical", "workspace");
        string solution = Path.Combine(physicalWorkspace, "DemoDisc.sln");
        File.WriteAllText(solution, string.Empty);
        CreateDirectoryLink(Path.Combine(Root, "alias"), Path.Combine(Root, "physical"));

        ProcessStartInfo start = CreateBuildStartInfo(Path.Combine(Root, "alias", "workspace", "DemoDisc.sln"),
            Path.Combine(Root, "alias", "workspace"));

        Assert.Equal(solution, start.ArgumentList[1]);
        Assert.Equal(physicalWorkspace, start.WorkingDirectory);
        Assert.Equal(new[] { "build", solution, "--configuration", "Debug", "-p:UseArtifactsOutput=false", "--nologo" }, start.ArgumentList);
        Assert.DoesNotContain(start.ArgumentList, argument => argument.StartsWith("-maxcpucount", StringComparison.Ordinal)
            || argument.StartsWith("-m:", StringComparison.Ordinal));
    }

    /// <summary>Preserves normal absolute paths and CLI arguments when the workspace has no links.</summary>
    [Fact]
    public void CreateBuildStartInfo_preserves_an_ordinary_workspace() {
        string workspace = Path.Combine(Root, "physical", "workspace");
        string solution = Path.Combine(workspace, "DemoDisc.slnf");

        ProcessStartInfo start = CreateBuildStartInfo(solution, workspace);

        Assert.Equal(solution, start.ArgumentList[1]);
        Assert.Equal(workspace, start.WorkingDirectory);
    }

    /// <summary>Rejects an unresolved link rather than reverting silently to its logical spelling.</summary>
    [Fact]
    public void CreateBuildStartInfo_rejects_a_dangling_workspace_link() {
        string target = Path.Combine(Root, "dangling-target");
        Directory.CreateDirectory(target);
        CreateDirectoryLink(Path.Combine(Root, "dangling"), target);
        Directory.Delete(target);

        Assert.ThrowsAny<IOException>(() => CreateBuildStartInfo(Path.Combine(Root, "dangling", "DemoDisc.sln"),
            Path.Combine(Root, "dangling")));
    }

    /// <summary>Removes links themselves before deleting only this class's owned fixture tree.</summary>
    public void Dispose() {
        foreach (string name in new[] { "alias", "dangling" }) {
            string path = Path.Combine(Root, name);
            if (new DirectoryInfo(path).LinkTarget != null) {
                if (OperatingSystem.IsWindows()) {
                    Directory.Delete(path);
                } else {
                    // Unix directory symlinks are unlinked as files even when their target is missing.
                    File.Delete(path);
                }
            }
        }
        Directory.Delete(Root, true);
    }

    /// <summary>Invokes the production process description without starting dotnet or building fixture projects.</summary>
    static ProcessStartInfo CreateBuildStartInfo(string solution, string workspace) {
        MethodInfo method = typeof(EditorDotNetScriptBuildTool).GetMethod("CreateBuildStartInfo", BindingFlags.Static | BindingFlags.NonPublic);
        try {
            return (ProcessStartInfo)method.Invoke(null, new object[] { solution, workspace });
        } catch (TargetInvocationException exception) when (exception.InnerException != null) {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    /// <summary>Creates a real Unix directory symlink or an unprivileged Windows directory junction for the fixture.</summary>
    static void CreateDirectoryLink(string link, string target) {
        if (OperatingSystem.IsWindows()) {
            ProcessStartInfo start = new ProcessStartInfo("cmd.exe") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("/d");
            start.ArgumentList.Add("/c");
            start.ArgumentList.Add("mklink");
            start.ArgumentList.Add("/J");
            start.ArgumentList.Add(link);
            start.ArgumentList.Add(target);
            using Process process = Process.Start(start);
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd());
        } else {
            Directory.CreateSymbolicLink(link, target);
        }
    }
}
