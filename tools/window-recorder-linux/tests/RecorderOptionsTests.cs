namespace helengine.tools.windowrecorderlinux.tests;

/// <summary>Protects the explicit window/PID boundary and exact thirty-second output contract without native capture.</summary>
public sealed class RecorderOptionsTests {
    /// <summary>Requires no desktop inference and establishes exactly 1,800 silent CFR60 slots by default.</summary>
    [Fact]
    public void DefaultsRequireExplicitWindowOwnerAndThirtySeconds() {
        var options = RecorderOptions.Parse(["--window", "0x20A", "--pid", "123"]);
        Assert.Equal(522ul, options.WindowId);
        Assert.Equal(123, options.ProcessId);
        Assert.Equal(1800, options.FrameCount);
        Assert.Equal(30, options.DurationSeconds);
        Assert.Equal("menu.mp4", Path.GetFileName(options.Output));
        Assert.True(Path.IsPathFullyQualified(options.Output));
    }

    /// <summary>Rejects absent ownership, malformed identifiers, ambiguous flags and non-frame-aligned duration before native calls.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("--window 0 --pid 1")]
    [InlineData("--window 1 --pid 0")]
    [InlineData("--window 1 --pid -1")]
    [InlineData("--window 0xZZ --pid 1")]
    [InlineData("--window 1 --pid 1 --duration 0.01")]
    [InlineData("--window 1 --pid 1 --duration 301")]
    [InlineData("--window 1 --pid 1 --duration 0")]
    [InlineData("--window 1 --pid 1 --output menu.mp4")]
    [InlineData("--window 1 --pid 1 --desktop 1")]
    [InlineData("--window 1 --pid 1 --pid 2")]
    [InlineData("--window 1 --pid")]
    public void RejectsUnsafeOrAmbiguousArguments(string arguments) {
        Assert.Throws<ArgumentException>(() => RecorderOptions.Parse(arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    /// <summary>Allows a short exact frame-aligned native smoke recording while preserving the fixed filename.</summary>
    [Fact]
    public void AcceptsExplicitOutputAndExactShortDuration() {
        string output = OperatingSystem.IsWindows() ? @"C:\temp\helengine\linux\menu.mp4" : "/tmp/helengine/linux/menu.mp4";
        var options = RecorderOptions.Parse(["--window", "100", "--pid", "12", "--duration", "0.5", "--output", output, "--ffmpeg", "/usr/bin/ffmpeg"]);
        Assert.Equal(30, options.FrameCount);
        Assert.Equal(Path.GetFullPath(output), options.Output);
        Assert.Equal("/usr/bin/ffmpeg", options.Ffmpeg);
    }

    /// <summary>Allows usage inspection on a non-Linux host without opening capture or writing artifacts.</summary>
    [Fact]
    public async Task HelpDoesNotOpenNativeCapture() {
        using var output = new StringWriter();
        using var errors = new StringWriter();
        Assert.Equal(0, await new RecorderApplication().RunAsync(["--help"], output, errors, CancellationToken.None));
        Assert.Contains("libx265", output.ToString());
        Assert.Contains("does not measure game FPS", output.ToString());
        Assert.Empty(errors.ToString());
    }
}
