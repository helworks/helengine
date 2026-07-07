using helengine.editor.tests.testing;

namespace helengine.editor.tests.content;

/// <summary>
/// Verifies content stream sources can back content-manager reads without direct filesystem ownership in the manager itself.
/// </summary>
public sealed class ContentManagerStreamSourceTests : IDisposable {
    /// <summary>
    /// Temporary root used by the filesystem-backed source tests.
    /// </summary>
    readonly string RootPath;

    /// <summary>
    /// Initializes the temporary filesystem root used by the tests.
    /// </summary>
    public ContentManagerStreamSourceTests() {
        RootPath = Path.Combine(Path.GetTempPath(), "helengine-content-stream-source-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    /// <summary>
    /// Removes the temporary filesystem root after each test.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(RootPath)) {
            Directory.Delete(RootPath, true);
        }
    }

    /// <summary>
    /// Verifies one host-filesystem stream source can open relative asset paths beneath its configured root.
    /// </summary>
    [Fact]
    public void OpenRead_whenAssetPathIsRelative_opensFileBeneathConfiguredRoot() {
        string assetDirectoryPath = Path.Combine(RootPath, "nested");
        string assetPath = Path.Combine(assetDirectoryPath, "payload.bin");
        Directory.CreateDirectory(assetDirectoryPath);
        File.WriteAllBytes(assetPath, new byte[] { 4, 5, 6, 7 });
        HostFileSystemContentStreamSource source = new(RootPath);

        using Stream stream = source.OpenRead(Path.Combine("nested", "payload.bin"));
        using MemoryStream copy = new();
        stream.CopyTo(copy);

        Assert.Equal(new byte[] { 4, 5, 6, 7 }, copy.ToArray());
    }

    /// <summary>
    /// Verifies one content manager reads through the injected source instead of opening files directly.
    /// </summary>
    [Fact]
    public void Load_whenUsingFakeStreamSource_readsThroughInjectedSource() {
        FakeContentStreamSource streamSource = new();
        streamSource.Register("memory.bin", new byte[] { 1, 2, 3, 4 });
        ContentManager contentManager = new(streamSource);

        RawByteContent content = contentManager.Load<RawByteContent>("memory.bin");

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, content.Bytes);
        Assert.Equal(["memory.bin"], streamSource.RequestedAssetPaths);
    }
}
