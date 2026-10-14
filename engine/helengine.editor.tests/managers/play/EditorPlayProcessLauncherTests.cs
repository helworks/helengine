using System.Diagnostics;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies how the real player launcher maps a launch request onto the operating-system process start description.
/// </summary>
public sealed class EditorPlayProcessLauncherTests {
    /// <summary>
    /// Player executable path used by the launch request.
    /// </summary>
    const string PlayerExecutablePath = @"C:\editor\player\helengine.player.exe";

    /// <summary>
    /// Ensures every request argument becomes exactly one entry of the start info's argument list, in order, including
    /// arguments with spaces and quotes, so no command-line string is ever concatenated.
    /// </summary>
    [Fact]
    public void CreateStartInfo_CopiesArgumentsVerbatimIntoArgumentList() {
        List<string> arguments = new List<string> {
            "--content-root", @"C:\my project\cache\play\ps2 staging",
            "--scene-id", "cube_test",
            "--adapter-type", "helengine.ps2.player.Ps2PlayerAdapter",
            "--wait-for-debugger",
            "quoted \"value\""
        };

        ProcessStartInfo startInfo = EditorPlayProcessLauncher.CreateStartInfo(new EditorPlayLaunchRequest(PlayerExecutablePath, arguments));

        Assert.Equal(arguments, startInfo.ArgumentList);
        Assert.Equal(string.Empty, startInfo.Arguments);
    }

    /// <summary>
    /// Ensures the start info launches the requested executable directly with redirected console streams.
    /// </summary>
    [Fact]
    public void CreateStartInfo_RedirectsConsoleWithoutShellExecute() {
        ProcessStartInfo startInfo = EditorPlayProcessLauncher.CreateStartInfo(new EditorPlayLaunchRequest(PlayerExecutablePath, new List<string>()));

        Assert.Equal(PlayerExecutablePath, startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.Equal(@"C:\editor\player", startInfo.WorkingDirectory);
    }

    /// <summary>
    /// Ensures a null request is rejected instead of producing an empty start description.
    /// </summary>
    [Fact]
    public void CreateStartInfo_WhenRequestIsNull_Throws() {
        Assert.Throws<ArgumentNullException>(() => EditorPlayProcessLauncher.CreateStartInfo(null));
    }

    /// <summary>
    /// Ensures the launcher requires a foreground grant, so no player is ever started without the step that lets its
    /// window come to the front.
    /// </summary>
    [Fact]
    public void Constructor_WhenForegroundPermissionIsNull_Throws() {
        Assert.Throws<ArgumentNullException>(() => new EditorPlayProcessLauncher(new EditorMainThreadActionQueue(), null));
    }
}
