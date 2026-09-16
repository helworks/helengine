namespace helengine.editor.tests;

/// <summary>
/// Verifies the shared shader target descriptor table stays complete and round-trips every compile target through its persisted name.
/// </summary>
public sealed class ShaderTargetDescriptorTests {
    /// <summary>Native callers borrow the shared descriptor instead of taking cleanup responsibility for it.</summary>
    [Fact]
    public void Get_declares_borrowed_return_for_shared_descriptors() {
        Assert.True(typeof(ShaderTargetDescriptors).GetMethod(nameof(ShaderTargetDescriptors.Get)).IsDefined(typeof(NativeBorrowedReturnAttribute), false));
        foreach (ShaderTargetDescriptor descriptor in ShaderTargetDescriptors.All) {
            Assert.Same(descriptor, ShaderTargetDescriptors.Get(descriptor.Target));
        }
    }
    /// <summary>
    /// Ensures every declared compile target has a descriptor so no target can silently fall through to an unsupported-target throw.
    /// </summary>
    [Fact]
    public void Every_shader_compile_target_has_a_descriptor() {
        Array targets = Enum.GetValues(typeof(ShaderCompileTarget));

        Assert.Equal(targets.Length, ShaderTargetDescriptors.All.Count);

        foreach (ShaderCompileTarget target in targets) {
            ShaderTargetDescriptor descriptor = ShaderTargetDescriptors.Get(target);

            Assert.Equal(target, descriptor.Target);
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Name));
            Assert.False(string.IsNullOrWhiteSpace(descriptor.DefineName));
            Assert.Equal(descriptor.Name, descriptor.Name.ToLowerInvariant());
            Assert.StartsWith("HEL_API_", descriptor.DefineName, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Ensures target names emitted into shader packages parse back into the same compile target.
    /// </summary>
    [Fact]
    public void Every_shader_compile_target_round_trips_through_its_target_name() {
        foreach (ShaderCompileTarget target in Enum.GetValues(typeof(ShaderCompileTarget))) {
            string name = ShaderTargetNames.GetTargetName(target);

            ShaderCompileTarget parsed;
            Assert.True(ShaderTargetNames.TryParseTarget(name, out parsed));
            Assert.Equal(target, parsed);
            Assert.Equal(target, ShaderTargetNames.ParseTarget(name.ToUpperInvariant()));
        }
    }

    /// <summary>
    /// Ensures descriptor names and API defines are unique so parsing and preprocessor branching stay unambiguous.
    /// </summary>
    [Fact]
    public void Shader_target_descriptor_names_and_defines_are_unique() {
        HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> defineNames = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < ShaderTargetDescriptors.All.Count; i++) {
            ShaderTargetDescriptor descriptor = ShaderTargetDescriptors.All[i];

            Assert.True(names.Add(descriptor.Name));
            Assert.True(defineNames.Add(descriptor.DefineName));
        }
    }

    /// <summary>
    /// Ensures unknown names are rejected instead of resolving to an arbitrary target.
    /// </summary>
    [Fact]
    public void Unknown_target_names_do_not_resolve_to_a_descriptor() {
        ShaderTargetDescriptor descriptor;

        Assert.False(ShaderTargetDescriptors.TryGetByName("not-a-target", out descriptor));
        Assert.Null(descriptor);
        Assert.False(ShaderTargetDescriptors.TryGetByName("   ", out descriptor));
        Assert.Null(descriptor);
    }
}
