using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.project;

/// <summary>Verifies fresh synthetic module projects resolve framework references before native conversion starts.</summary>
[Collection(EditorBuildCacheEnvironmentCollection.Name)]
public sealed class EditorCodegenProjectRestoreTests {
    /// <summary>Uses an observing child process to require dependency assets at tool startup, with both supported intermediate layouts.</summary>
    /// <param name="externalArtifacts">Whether the child inherits a central MSBuild artifacts directory.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_WithFreshProject_RestoresFrameworkAssetsBeforeInvokingTool(bool externalArtifacts) {
        string originalArtifacts = Environment.GetEnvironmentVariable("ArtifactsPath");
        string root = Environment.GetEnvironmentVariable("CSHARPCODEGEN_TEST_ARTIFACT_ROOT")
            ?? Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "builds", "codegen-tests"));
        string fixture = Path.Combine(root, "project-restore", externalArtifacts ? "central" : "local");
        if (Directory.Exists(fixture)) {
            Directory.Delete(fixture, true);
        }
        Directory.CreateDirectory(fixture);
        string project = Path.Combine(fixture, "Fixture.csproj");
        File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>");
        string artifacts = Path.Combine(fixture, "artifacts");
        string assets = externalArtifacts
            ? Path.Combine(artifacts, "obj", "Fixture", "project.assets.json")
            : Path.Combine(fixture, "obj", "project.assets.json");
        string observer = Path.Combine(fixture, OperatingSystem.IsWindows() ? "observe.cmd" : "observe.sh");
        File.WriteAllText(observer, OperatingSystem.IsWindows()
            ? "@echo off\r\nif not exist \"%~1\" exit /b 23\r\nexit /b 0\r\n"
            : "#!/bin/sh\ntest -f \"$1\" || exit 23\n");
        try {
            Environment.SetEnvironmentVariable("ArtifactsPath", externalArtifacts ? artifacts : null);
            string tool = OperatingSystem.IsWindows()
                ? Environment.GetEnvironmentVariable("ComSpec") ?? throw new InvalidOperationException("Windows command processor is required for the observing child.")
                : "/bin/sh";
            string[] arguments = OperatingSystem.IsWindows()
                ? ["/d", "/c", observer, assets, "--project", project]
                : [observer, assets, "--project", project];

            ProcessEditorCodegenToolRunner.Instance.Run(tool, arguments, fixture);

            Assert.Contains("Microsoft.NETCore.App", File.ReadAllText(assets), StringComparison.Ordinal);
        } finally {
            Environment.SetEnvironmentVariable("ArtifactsPath", originalArtifacts);
        }
    }
}
