using System.Globalization;
namespace helengine.tools.windowrecorder.tests;
/// <summary>Checks CLI parsing independently of Windows APIs and codec availability.</summary>
public sealed class RecorderCommandLineTests {
    /// <summary>Both common handle representations identify exactly the same explicit window.</summary>
    [Fact] public void ParsesHwndHexAndDecimalEqually() {
        Assert.Equal((IntPtr)291, RecorderCommandLine.Parse(["record", "--hwnd", "0x123", "--output", "x.mp4"]).Options.WindowHandle);
        Assert.Equal((IntPtr)291, RecorderCommandLine.Parse(["record", "--hwnd", "291", "--output", "x.mp4"]).Options.WindowHandle);
    }
    /// <summary>System culture cannot change numeric syntax used by automation.</summary>
    [Fact] public void ParsesNumbersInvariantlyInPtBr() {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            var options = RecorderCommandLine.Parse(["record", "--hwnd", "1", "--output", "x.mp4", "--duration", "1.5", "--bitrate", "40M", "--max-bitrate", "60M"]).Options;
            Assert.Equal(TimeSpan.FromMilliseconds(1500), options.Duration); Assert.Equal(40_000_000, options.Bitrate); Assert.Equal(60_000_000, options.MaxBitrate);
            Assert.Throws<ArgumentException>(() => RecorderCommandLine.Parse(["record", "--hwnd", "1", "--output", "x.mp4", "--duration", "1,5"]));
        } finally { CultureInfo.CurrentCulture = previous; }
    }
    /// <summary>Arguments already separated by the shell retain all Unicode and spaces.</summary>
    [Fact] public void AcceptsUnicodeAndSpacedOutput() {
        var options = RecorderCommandLine.Parse(["record", "--hwnd", "1", "--output", "C:\\temp\\helengine-window-recording\\menu ação.mp4"]).Options;
        Assert.EndsWith("menu ação.mp4", options.OutputPath); Assert.Null(options.Duration);
    }
    /// <summary>Typos and unsupported modes cannot silently select a window or different frame rate.</summary>
    [Theory][InlineData("--fps", "30")][InlineData("--duration", "0")][InlineData("--fps", "60.0")][InlineData("--bitrate", "0")][InlineData("--unknown", "x")][InlineData("--hwnd", "2")]
    public void RejectsMissingDuplicateUnknownOrNon60Options(string name, string value) => Assert.Throws<ArgumentException>(() => RecorderCommandLine.Parse(["record", "--hwnd", "1", "--output", "x.mp4", name, value]));
    /// <summary>A missing handle/value or extra list argument cannot be treated as a successful command.</summary>
    [Fact] public void RejectsIncompleteCommands() {
        Assert.Throws<ArgumentException>(() => RecorderCommandLine.Parse(["record", "--output", "x.mp4"]));
        Assert.Throws<ArgumentException>(() => RecorderCommandLine.Parse(["record", "--hwnd"]));
        Assert.Throws<ArgumentException>(() => RecorderCommandLine.Parse(["windows", "extra"]));
    }
}
