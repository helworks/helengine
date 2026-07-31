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
        string sourcePath = Path.Combine(ResolveRepositoryRootPath(), "engine", "helengine.physics", "TriggerPairKey3D.cs");
        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("HashCode.Combine(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public override int GetHashCode()", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures each generated trigger scan copies a read-only-list value before invoking the key's mutable C++ instance method surface.
    /// </summary>
    [Fact]
    public void Trigger_pair_scans_copy_list_values_before_equality() {
        AssertTriggerPairScanCopiesListValue("helengine.physics3d", "PhysicsWorld3D.cs");
        AssertTriggerPairScanCopiesListValue("helengine.bepu", "BepuPhysicsWorld3D.cs");
    }

    /// <summary>
    /// Verifies one runtime's linear trigger-pair scan does not invoke equality through a const native list reference.
    /// </summary>
    /// <param name="projectDirectoryName">Physics project directory beneath the engine root.</param>
    /// <param name="fileName">Runtime source file containing the trigger-pair scan.</param>
    static void AssertTriggerPairScanCopiesListValue(string projectDirectoryName, string fileName) {
        string sourcePath = Path.Combine(ResolveRepositoryRootPath(), "engine", projectDirectoryName, fileName);
        string source = File.ReadAllText(sourcePath).ReplaceLineEndings("\n");
        Assert.Contains("TriggerPairKey3D existingPair = pairs[index];\n                if (existingPair.Equals(pairKey))", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves the isolated HelEngine repository root from the editor test assembly output directory.
    /// </summary>
    /// <returns>Absolute repository root containing the engine and editor solution trees.</returns>
    static string ResolveRepositoryRootPath() {
        string currentDirectory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(currentDirectory)) {
            if (Directory.Exists(Path.Combine(currentDirectory, "engine"))
                && Directory.Exists(Path.Combine(currentDirectory, "helengine.ui"))
                && File.Exists(Path.Combine(currentDirectory, "helengine.ui", "helengine.sln"))) {
                return currentDirectory;
            }

            currentDirectory = Path.GetDirectoryName(currentDirectory);
        }

        throw new InvalidOperationException("Unable to resolve the HelEngine repository root from the editor test assembly.");
    }
}
