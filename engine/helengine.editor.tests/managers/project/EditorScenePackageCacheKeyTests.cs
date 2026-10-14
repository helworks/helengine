using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the scene-package cache key computed through the input stamp memo always equals a full re-hash of the
/// same inputs, except for the documented same-length, same-stamp edit the memo trusts by design.
/// </summary>
public sealed class EditorScenePackageCacheKeyTests : IDisposable {
    /// <summary>
    /// Temporary project root of the current test.
    /// </summary>
    readonly string ProjectRootPath;

    /// <summary>
    /// Builder whose platform definition and type feed the key.
    /// </summary>
    readonly TestPlatformMaterialAssetBuilder Builder = new();

    /// <summary>
    /// Last-write time given to every seeded input, old enough for the memo to remember it.
    /// </summary>
    static readonly DateTime SeededWriteTimeUtc = DateTime.UtcNow.AddHours(-1);

    /// <summary>
    /// Creates a project with a scene, nested assets, settings, and a project file, all stamped in the past.
    /// </summary>
    public EditorScenePackageCacheKeyTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-package-key-tests", Guid.NewGuid().ToString("N"));
        WriteInput("assets/Scenes/MainMenu.helen", "scene payload");
        WriteInput("assets/textures/logo.png", "png bytes");
        WriteInput("assets/textures/logo.png.hasset", "import settings");
        WriteInput("assets/models/deep/nested/cube.obj", "o cube");
        WriteInput("settings/build_config.json", "{}");
        WriteInput("project.heproj", "{ \"name\": \"keytest\" }");
    }

    /// <summary>
    /// Deletes the temporary project.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    /// <summary>
    /// Ensures the memo-backed key equals a full re-hash after every kind of input change, and that the memo pass
    /// hashes only the files that are new or whose stamp changed.
    /// </summary>
    [Fact]
    public void ComputeKey_WithStampMemo_EqualsAFullRehashAfterAddRemoveAndModify() {
        EditorScenePackageCache cache = new(ProjectRootPath);
        string initialFullKey = ComputeFullKey(cache);
        Assert.Equal(6, cache.LastHashedInputCount);

        string unchangedMemoKey = ComputeKey(cache);
        Assert.Equal(initialFullKey, unchangedMemoKey);
        Assert.Equal(0, cache.LastHashedInputCount);
        Assert.Equal(6, cache.LastReusedInputCount);

        WriteInput("assets/textures/new.png", "new texture", SeededWriteTimeUtc.AddMinutes(1));
        AssertMemoKeyEqualsFullKey(cache, expectedHashedCount: 1, previousKey: initialFullKey);

        File.Delete(InputPath("assets/models/deep/nested/cube.obj"));
        AssertMemoKeyEqualsFullKey(cache, expectedHashedCount: 0, previousKey: initialFullKey);

        WriteInput("assets/textures/logo.png", "png bytes, now longer", SeededWriteTimeUtc.AddMinutes(2));
        AssertMemoKeyEqualsFullKey(cache, expectedHashedCount: 1, previousKey: initialFullKey);

        WriteInput("settings/build_config.json", "[]", SeededWriteTimeUtc.AddMinutes(3));
        AssertMemoKeyEqualsFullKey(cache, expectedHashedCount: 1, previousKey: initialFullKey);

        WriteInput("project.heproj", "{ \"name\": \"keytest2\" }", SeededWriteTimeUtc.AddMinutes(4));
        AssertMemoKeyEqualsFullKey(cache, expectedHashedCount: 1, previousKey: initialFullKey);
    }

    /// <summary>
    /// Documents the memo's trust model: an edit that keeps the file's length and last-write time is not noticed,
    /// exactly like the asset identity snapshot and the asset hash cache. A full re-hash does notice it.
    /// </summary>
    [Fact]
    public void ComputeKey_WhenContentChangesWithTheSameLengthAndStamp_TrustsTheRememberedHash() {
        EditorScenePackageCache cache = new(ProjectRootPath);
        string originalKey = ComputeFullKey(cache);

        WriteInput("assets/textures/logo.png", "PNG BYTES", SeededWriteTimeUtc);

        Assert.Equal(originalKey, ComputeKey(cache));
        Assert.Equal(0, cache.LastHashedInputCount);
        Assert.NotEqual(originalKey, ComputeFullKey(cache));
    }

    /// <summary>
    /// Ensures a file written just before the pass is hashed again on the next pass instead of being trusted, so a
    /// second write inside the stamp's resolution cannot be missed.
    /// </summary>
    [Fact]
    public void ComputeKey_DoesNotRememberAFileWrittenJustBeforeThePass() {
        EditorScenePackageCache cache = new(ProjectRootPath);
        File.WriteAllText(InputPath("assets/textures/fresh.png"), "fresh");
        string firstKey = ComputeFullKey(cache);

        Assert.Equal(firstKey, ComputeKey(cache));
        Assert.Equal(1, cache.LastHashedInputCount);
    }

    /// <summary>
    /// Ensures an unreadable memo only costs a full re-hash.
    /// </summary>
    [Fact]
    public void ComputeKey_WhenTheMemoIsCorrupt_HashesEveryInput() {
        EditorScenePackageCache cache = new(ProjectRootPath);
        string fullKey = ComputeFullKey(cache);
        File.WriteAllText(MemoPath, "{ not json");

        Assert.Equal(fullKey, ComputeKey(cache));
        Assert.Equal(6, cache.LastHashedInputCount);
        Assert.Equal(fullKey, ComputeKey(cache));
        Assert.Equal(0, cache.LastHashedInputCount);
    }

    /// <summary>
    /// Ensures a reparse point under the hashed tree still fails the key whether or not the memo remembers the
    /// files, matching the verified read path's refusal to traverse reparse points.
    /// </summary>
    [Fact]
    public void ComputeKey_WhenAnInputDirectoryIsAJunction_RejectsTheKey() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        string outsidePath = Path.Combine(ProjectRootPath + "-outside");
        Directory.CreateDirectory(outsidePath);
        try {
            File.WriteAllText(Path.Combine(outsidePath, "elsewhere.png"), "outside bytes");
            EditorScenePackageCache cache = new(ProjectRootPath);
            ComputeFullKey(cache);
            CreateJunction(InputPath("assets/linked"), outsidePath);

            Assert.Throws<InvalidDataException>(() => ComputeKey(cache));
        } finally {
            string junctionPath = InputPath("assets/linked");
            if (Directory.Exists(junctionPath)) {
                Directory.Delete(junctionPath);
            }
            Directory.Delete(outsidePath, true);
        }
    }

    /// <summary>
    /// Ensures a post-packaging key is accepted only when packaging created identity sidecars of unchanged sources;
    /// any rewritten or created import-settings sidecar, and any other difference, is rejected.
    /// </summary>
    [Fact]
    public void DiffersOnlyByCreatedIdentitySidecars_AcceptsOnlyCreatedIdentitySidecarsOfUnchangedSources() {
        EditorScenePackageKey before = CreateKey("a", "assets/t/logo.png", "1", "assets/t/logo.png.hasset", "2", "assets/m/mat.hasset", "3");
        EditorScenePackageKey withoutSidecars = CreateKey("a", "assets/t/logo.png", "1", "assets/m/mat.hasset", "3");

        // An identity sidecar created for an unchanged source.
        Assert.True(before.DiffersOnlyByCreatedIdentitySidecars(
            CreateKey("b", "assets/t/logo.png", "1", "assets/t/logo.png.hmeta", "5", "assets/t/logo.png.hasset", "2", "assets/m/mat.hasset", "3")));

        // A rewritten import-settings sidecar (with or without a created identity sidecar), or one created during packaging.
        Assert.False(before.DiffersOnlyByCreatedIdentitySidecars(
            CreateKey("b", "assets/t/logo.png", "1", "assets/t/logo.png.hasset", "9", "assets/m/mat.hasset", "3")));
        Assert.False(before.DiffersOnlyByCreatedIdentitySidecars(
            CreateKey("b", "assets/t/logo.png", "1", "assets/t/logo.png.hmeta", "5", "assets/t/logo.png.hasset", "9", "assets/m/mat.hasset", "3")));
        Assert.False(withoutSidecars.DiffersOnlyByCreatedIdentitySidecars(
            CreateKey("b", "assets/t/logo.png", "1", "assets/t/logo.png.hasset", "2", "assets/m/mat.hasset", "3")));

        // A rewritten identity sidecar, the source itself, another asset, a removed file, another input, or an
        // identity sidecar of a sidecar.
        EditorScenePackageKey withIdentity = CreateKey("a", "assets/t/logo.png", "1", "assets/t/logo.png.hmeta", "5", "assets/m/mat.hasset", "3");
        Assert.False(withIdentity.DiffersOnlyByCreatedIdentitySidecars(
            CreateKey("b", "assets/t/logo.png", "1", "assets/t/logo.png.hmeta", "6", "assets/m/mat.hasset", "3")));
        Assert.False(before.DiffersOnlyByCreatedIdentitySidecars(
            CreateKey("b", "assets/t/logo.png", "5", "assets/t/logo.png.hmeta", "5", "assets/t/logo.png.hasset", "2", "assets/m/mat.hasset", "3")));
        Assert.False(before.DiffersOnlyByCreatedIdentitySidecars(
            CreateKey("b", "assets/t/logo.png", "1", "assets/t/logo.png.hasset", "2", "assets/m/mat.hasset", "8")));
        Assert.False(before.DiffersOnlyByCreatedIdentitySidecars(
            CreateKey("b", "assets/t/logo.png", "1", "assets/m/mat.hasset", "3")));
        Assert.False(before.DiffersOnlyByCreatedIdentitySidecars(
            CreateKey("b", "assets/t/logo.png", "1", "assets/t/logo.png.hasset", "2", "assets/m/mat.hasset", "3", "settings/build_config.json", "7")));
        Assert.False(before.DiffersOnlyByCreatedIdentitySidecars(
            CreateKey("b", "assets/t/logo.png", "1", "assets/t/logo.png.hasset", "2", "assets/m/mat.hasset", "3", "assets/t/logo.png.hasset.hmeta", "6")));

        // The keys are equal, or differ while every covered file is identical.
        Assert.False(before.DiffersOnlyByCreatedIdentitySidecars(
            CreateKey("a", "assets/t/logo.png", "1", "assets/t/logo.png.hmeta", "5", "assets/t/logo.png.hasset", "2", "assets/m/mat.hasset", "3")));
        Assert.False(before.DiffersOnlyByCreatedIdentitySidecars(
            CreateKey("b", "assets/t/logo.png", "1", "assets/t/logo.png.hasset", "2", "assets/m/mat.hasset", "3")));
    }

    /// <summary>
    /// Builds a key over explicit file hashes.
    /// </summary>
    /// <param name="key">Key text.</param>
    /// <param name="pathsAndHashes">Alternating project-relative paths and content hashes.</param>
    /// <returns>Key covering exactly those files.</returns>
    static EditorScenePackageKey CreateKey(string key, params string[] pathsAndHashes) {
        Dictionary<string, string> inputHashes = new(StringComparer.Ordinal);
        for (int index = 0; index < pathsAndHashes.Length; index += 2) {
            inputHashes.Add(pathsAndHashes[index], pathsAndHashes[index + 1]);
        }

        return new EditorScenePackageKey(key, inputHashes);
    }

    /// <summary>
    /// Computes the key through the memo and compares it with a full re-hash of the same inputs.
    /// </summary>
    /// <param name="cache">Cache under test.</param>
    /// <param name="expectedHashedCount">Files the memo pass must hash.</param>
    /// <param name="previousKey">Key before the change, which the new key must differ from.</param>
    void AssertMemoKeyEqualsFullKey(EditorScenePackageCache cache, int expectedHashedCount, string previousKey) {
        string memoKey = ComputeKey(cache);
        Assert.Equal(expectedHashedCount, cache.LastHashedInputCount);
        Assert.Equal(ComputeFullKey(cache), memoKey);
        Assert.NotEqual(previousKey, memoKey);
    }

    /// <summary>
    /// Computes the key with no memo, so every input is hashed; the pass then leaves a fresh memo behind.
    /// </summary>
    /// <param name="cache">Cache under test.</param>
    /// <returns>Key of a full re-hash.</returns>
    string ComputeFullKey(EditorScenePackageCache cache) {
        if (File.Exists(MemoPath)) {
            File.Delete(MemoPath);
        }

        return ComputeKey(cache);
    }

    /// <summary>
    /// Computes the key for one fixed scene and fixed options.
    /// </summary>
    /// <param name="cache">Cache under test.</param>
    /// <returns>Scene-package key.</returns>
    string ComputeKey(EditorScenePackageCache cache) {
        return cache.ComputeKey(
            Builder.Definition,
            Builder,
            "debug",
            "default",
            "release",
            ["MainMenu"],
            ["Scenes/MainMenu.helen"],
            Array.Empty<IAssetImporterRegistration>(),
            null,
            null,
            null).Key;
    }

    /// <summary>
    /// Writes one project input and stamps it with a past last-write time.
    /// </summary>
    /// <param name="relativePath">Project-relative path with forward slashes.</param>
    /// <param name="content">Text content.</param>
    void WriteInput(string relativePath, string content) {
        WriteInput(relativePath, content, SeededWriteTimeUtc);
    }

    /// <summary>
    /// Writes one project input and stamps it with an explicit last-write time.
    /// </summary>
    /// <param name="relativePath">Project-relative path with forward slashes.</param>
    /// <param name="content">Text content.</param>
    /// <param name="lastWriteTimeUtc">Last-write time to apply.</param>
    void WriteInput(string relativePath, string content, DateTime lastWriteTimeUtc) {
        string path = InputPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
    }

    /// <summary>
    /// Resolves a project-relative path.
    /// </summary>
    /// <param name="relativePath">Project-relative path with forward slashes.</param>
    /// <returns>Absolute path.</returns>
    string InputPath(string relativePath) {
        return Path.Combine(ProjectRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// Absolute path of the input stamp memo.
    /// </summary>
    string MemoPath => Path.Combine(ProjectRootPath, "cache", "build", "scene-package", EditorScenePackageInputStampMemo.FileName);

    /// <summary>
    /// Creates a directory junction, which needs no elevation on Windows.
    /// </summary>
    /// <param name="junctionPath">Junction to create.</param>
    /// <param name="targetPath">Existing directory the junction points to.</param>
    static void CreateJunction(string junctionPath, string targetPath) {
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junctionPath}\" \"{targetPath}\"") {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true
        })!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }
}
