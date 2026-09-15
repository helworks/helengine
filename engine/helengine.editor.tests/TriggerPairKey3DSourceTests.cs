namespace helengine.editor.tests;

/// <summary>
/// Verifies the shared trigger-pair key source keeps the hash-code shape the hash-based trigger-pair set relies on while avoiding constructs the generated C++ path does not support.
/// </summary>
public sealed class TriggerPairKey3DSourceTests {
    /// <summary>
    /// Ensures the shared trigger-pair key overrides `GetHashCode` with the multiply-and-xor combination used by the other transpiled key structs, and never through `HashCode.Combine`, because BepuTriggerPairSet3D keys a hash set with it.
    /// </summary>
    [Fact]
    public void Trigger_pair_key_3d_source_overrides_hash_code_without_hashcode_combine() {
        string sourcePath = Path.Combine(TestSourceRepositoryLocator.ResolveHelEngineRootPath(), "engine", "helengine.physics", "TriggerPairKey3D.cs");
        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("HashCode.Combine(", source, StringComparison.Ordinal);
        Assert.Contains("public override int GetHashCode()", source, StringComparison.Ordinal);
        Assert.Contains("* 397) ^", source, StringComparison.Ordinal);
    }
}
