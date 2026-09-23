using System;
using System.IO;
using helengine.baseplatform.Requests;
using Xunit;

namespace helengine.baseplatform.tests.Requests;

/// <summary>
/// Verifies resolved shader source snapshots retain optional host-side source path provenance.
/// </summary>
public sealed class PlatformShaderArtifactCookSourceTests {
    /// <summary>
    /// Ensures the original source-only constructor retains its values and represents path absence explicitly.
    /// </summary>
    [Fact]
    public void LegacyConstructor_whenSourceIsProvided_preservesSnapshotWithoutSourcePath() {
        PlatformShaderArtifactCookSource source = new("asset", "supplied-hash", "source snapshot");

        Assert.Equal("asset", source.ShaderAssetId);
        Assert.Equal("supplied-hash", source.SourceHash);
        Assert.Equal("source snapshot", source.SourceText);
        Assert.False(source.HasSourcePath);
        Assert.Null(source.SourcePath);
    }

    /// <summary>
    /// Ensures a fully qualified metadata-only path is retained without reading or requiring the source file.
    /// </summary>
    [Fact]
    public void PathConstructor_whenSourcePathIsFullyQualified_preservesSnapshotAndPath() {
        string path = Path.GetFullPath(Path.Combine("shader-source-tests", "shaders with spaces", "ação.hlsl"));
        PlatformShaderArtifactCookSource source = new("asset", "supplied-hash", "source snapshot", path);

        Assert.True(Path.IsPathFullyQualified(path));
        Assert.False(File.Exists(path));
        Assert.True(source.HasSourcePath);
        Assert.Equal(path, source.SourcePath);
        Assert.Equal("supplied-hash", source.SourceHash);
        Assert.Equal("source snapshot", source.SourceText);
    }

    /// <summary>
    /// Ensures null, empty, whitespace, and relative paths cannot be used as source path provenance.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative/main.hlsl")]
    public void PathConstructor_whenSourcePathIsMissingOrRelative_throwsArgumentException(string sourcePath) {
        Assert.Throws<ArgumentException>(() => new PlatformShaderArtifactCookSource("asset", "hash", "text", sourcePath));
    }

    /// <summary>
    /// Ensures Windows drive-relative and rooted-without-drive paths are rejected as not fully qualified.
    /// </summary>
    [Fact]
    public void PathConstructor_whenWindowsPathIsNotFullyQualified_throwsArgumentException() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        Assert.Throws<ArgumentException>(() => new PlatformShaderArtifactCookSource("asset", "hash", "text", "C:main.hlsl"));
        Assert.Throws<ArgumentException>(() => new PlatformShaderArtifactCookSource("asset", "hash", "text", "\\main.hlsl"));
    }

    /// <summary>
    /// Ensures required shader identity and source snapshot validation remains unchanged for both constructors.
    /// </summary>
    [Fact]
    public void Constructors_whenRequiredIdentityOrSourceTextIsInvalid_preserveExistingExceptions() {
        Assert.Throws<ArgumentException>(() => new PlatformShaderArtifactCookSource(" ", "hash", "text"));
        Assert.Throws<ArgumentException>(() => new PlatformShaderArtifactCookSource("asset", " ", "text"));
        Assert.Throws<ArgumentNullException>(() => new PlatformShaderArtifactCookSource("asset", "hash", null));

        Assert.Throws<ArgumentException>(() => new PlatformShaderArtifactCookSource(" ", "hash", "text", Path.GetFullPath("source.hlsl")));
        Assert.Throws<ArgumentException>(() => new PlatformShaderArtifactCookSource("asset", " ", "text", Path.GetFullPath("source.hlsl")));
        Assert.Throws<ArgumentNullException>(() => new PlatformShaderArtifactCookSource("asset", "hash", null, Path.GetFullPath("source.hlsl")));
    }
}
