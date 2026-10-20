using helengine.baseplatform.Descriptors;
using helengine.baseplatform.Manifest;

namespace helengine.editor.tests;

/// <summary>Guards content/native identity and early module ABI checks across numeric selections.</summary>
public sealed class NumericPrecisionCacheTests {
    /// <summary>Changing only numeric policy invalidates the asset key, and returning to 32 reuses its identity.</summary>
    [Fact]
    public void CookKeyChangesWithNumericPolicy() {
        EditorAssetCookNode node = CreateNode();
        string first = EditorAssetCookKeyBuilder.Build(node, "1", "windows", "debug", null, [], new(32, 32)).Value;
        string second = EditorAssetCookKeyBuilder.Build(node, "1", "windows", "debug", null, [], new(64, 64)).Value;
        string third = EditorAssetCookKeyBuilder.Build(node, "1", "windows", "debug", null, [], new(32, 32)).Value;
        Assert.NotEqual(first, second);
        Assert.Equal(first, third);
    }

    /// <summary>The native cache uses the captured policy even when otherwise identical options omit it.</summary>
    [Fact]
    public void NativeKeyChangesWithNumericPolicy() {
        EditorBuildQueueItemDocument queue = new() { NumericPolicy = new(32, 32) };
        string first = ComputeNativeKey(queue, new Dictionary<string, string>());
        queue.NumericPolicy = new(64, 64);
        string second = ComputeNativeKey(queue, new Dictionary<string, string>());
        queue.NumericPolicy = new(32, 32);
        Assert.NotEqual(first, second);
        Assert.Equal(first, ComputeNativeKey(queue, new Dictionary<string, string>()));
    }

    /// <summary>Equivalent numeric spellings and insertion orders produce a single cache identity.</summary>
    [Fact]
    public void EquivalentPolicyOptionsHaveSameKey() {
        Dictionary<string, string> first = new() { ["REAL-STORAGE-BITS"] = " 032 ", ["real-arithmetic-bits"] = "32", ["z"] = "1" };
        Dictionary<string, string> second = new() { ["z"] = "1", ["real-arithmetic-bits"] = "032", ["real-storage-bits"] = "32" };
        EditorBuildQueueItemDocument queue = new() { NumericPolicy = new(32, 32) };
        Assert.Equal(ComputeNativeKey(queue, first), ComputeNativeKey(queue, second));
        Assert.Equal(EditorAssetCookKeyBuilder.Build(CreateNode(), "1", "windows", "debug", first, [], queue.NumericPolicy),
            EditorAssetCookKeyBuilder.Build(CreateNode(), "1", "windows", "debug", second, [], queue.NumericPolicy));
        Assert.True(first.ContainsKey("REAL-STORAGE-BITS"));
    }

    /// <summary>A module with only its numeric identity changed is rejected before any backend can load it.</summary>
    [Fact]
    public void RejectsModulePolicyBeforeLoad() {
        PlatformBuildCodeModule module = new("game", "code/game", "test", [], [], new(64, 64));
        Assert.Throws<InvalidOperationException>(() => module.RequireNumericPolicy(new(32, 32)));
        module.RequireNumericPolicy(new(64, 64));
    }

    /// <summary>Process handoff preserves immutable policy and rejects a forged module before exposing a manifest.</summary>
    [Fact]
    public void SerializedManifestRetainsAndValidatesModulePolicy() {
        PlatformBuildCodeModule module = new("game", "code/game", "test", [], [], new(64, 64));
        PlatformBuildManifest manifest = new(2, "game", "1", "1", "windows", "1", "", [], [], [], [module], [],
            new("", []), [], PlatformBuildRuntimeFeatureManifest.Empty, new(64, 64));
        string json = System.Text.Json.JsonSerializer.Serialize(manifest);
        PlatformBuildManifest decoded = System.Text.Json.JsonSerializer.Deserialize<PlatformBuildManifest>(json);
        Assert.Equal("real-v1-s64-a64", decoded.NumericPolicy.Identity);
        Assert.Equal("real-v1-s64-a64", decoded.CodeModules[0].NumericPolicy.Identity);
        System.Text.Json.Nodes.JsonObject forged = System.Text.Json.Nodes.JsonNode.Parse(json).AsObject();
        forged["CodeModules"][0]["NumericPolicy"]["StorageBits"] = 32;
        Assert.Throws<InvalidOperationException>(() => System.Text.Json.JsonSerializer.Deserialize<PlatformBuildManifest>(forged.ToJsonString()));
    }

    /// <summary>CLI precision is an invocation override and leaves persisted project selections unchanged.</summary>
    [Theory]
    [InlineData("32", "32")]
    [InlineData("64", "64")]
    public void CommandLineNumericSelectionDoesNotRewriteProject(string storage, string arithmetic) {
        Assert.True(EditorCliArgumentParser.TryParseBuildOptions(["--project=C:/project", "--build=windows", "--output=C:/output",
            "--real-storage-bits=" + storage, "--real-arithmetic-bits", arithmetic], out EditorCliBuildOptions options, out string error), error);
        Dictionary<string, string> original = new() { ["real-storage-bits"] = "16", ["other"] = "kept" };
        EditorBuildPlatformConfigDocument config = new() { SelectedCodegenOptionValues = original };
        EditorCliBuildPlatformConfigOverlayService.ApplyNumericOptions(config, options);
        Assert.Equal(storage, config.SelectedCodegenOptionValues["real-storage-bits"]);
        Assert.Equal(arithmetic, config.SelectedCodegenOptionValues["real-arithmetic-bits"]);
        Assert.Equal("kept", config.SelectedCodegenOptionValues["other"]);
        Assert.Equal("16", original["real-storage-bits"]);
    }

    /// <summary>Malformed or duplicated precision switches fail before any build phase starts.</summary>
    [Theory]
    [InlineData("--real-storage-bits=128", "--real-arithmetic-bits=32")]
    [InlineData("--real-storage-bits=32", "--real-storage-bits=64")]
    public void CommandLineRejectsInvalidNumericSelection(string first, string second) {
        Assert.False(EditorCliArgumentParser.TryParseBuildOptions(["--project=C:/project", "--build=windows", "--output=C:/output", first, second], out _, out _));
    }

    /// <summary>Invokes the same deterministic function used by the native graph phase.</summary>
    static string ComputeNativeKey(EditorBuildQueueItemDocument queue, IReadOnlyDictionary<string, string> options) {
        PlatformBuilderDescriptor descriptor = new("test", "1", "windows", new("1.0.0", "1.0.0"), new(1, 2), ["test"], ["test"]);
        return EditorPlatformBuildGraphRunner.ComputeNativeCacheFingerprint("windows", queue, "debug", "test", "cpp", "", descriptor, options);
    }

    /// <summary>Creates one stable source node without using project files or mutable importer settings.</summary>
    static EditorAssetCookNode CreateNode() => new(new("source:model"),
        global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference("0123456789abcdef0123456789abcdef", "model", "sha256:" + new string('a', 64)),
        AssetEntryKind.Model, [], "model");
}
