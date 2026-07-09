namespace helengine.editor.tests;

/// <summary>
/// Verifies the shared trigger-pair key source avoids hash-code overrides that are unsupported and unnecessary for the current linear trigger-pair tracking path.
/// </summary>
public sealed class TriggerPairKey3DSourceTests {
    /// <summary>
    /// Ensures the shared trigger-pair key does not override `GetHashCode`, because the current physics trigger-pair tracking path only performs linear equality comparisons.
    /// </summary>
    [Fact]
    public void Trigger_pair_key_3d_source_does_not_override_hash_code() {
        string sourcePath = @"C:\dev\helworks\helengine\engine\helengine.physics\TriggerPairKey3D.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("HashCode.Combine(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public override int GetHashCode()", source, StringComparison.Ordinal);
    }
}
