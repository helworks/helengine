namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-only per-platform MeshComponent tessellation metadata.
    /// </summary>
    public sealed class MeshComponentTessellationSettingsServiceTests {
        /// <summary>
        /// Stores and resolves detached MeshComponent tessellation settings for each test.
        /// </summary>
        readonly MeshComponentTessellationSettingsService Service;

        /// <summary>
        /// Initializes a fresh settings service for each test.
        /// </summary>
        public MeshComponentTessellationSettingsServiceTests() {
            Service = new MeshComponentTessellationSettingsService();
        }

        /// <summary>
        /// Ensures missing platform metadata uses the disabled editor default without allocating an override.
        /// </summary>
        [Fact]
        public void GetForPlatform_WhenNoOverrideExists_ReturnsDisabledDefault() {
            EntityComponentSaveState state = new EntityComponentSaveState();

            MeshComponentTessellationSettings settings = Service.GetForPlatform(state, "ps2");

            Assert.False(settings.Tessellate);
            Assert.Equal(1.0d, settings.TessellationMaxEdgeLength);
            Assert.False(state.HasPlatformOverride("ps2"));
        }

        /// <summary>
        /// Ensures each platform retains independent detached tessellation values.
        /// </summary>
        [Fact]
        public void SetForPlatform_WhenPlatformsDiffer_PreservesBothValues() {
            EntityComponentSaveState state = new EntityComponentSaveState();

            Service.SetForPlatform(state, "ps2", new MeshComponentTessellationSettings(true, 0.25d));
            Service.SetForPlatform(state, "windows", new MeshComponentTessellationSettings(false, 1.0d));

            Assert.True(Service.GetForPlatform(state, "ps2").Tessellate);
            Assert.Equal(0.25d, Service.GetForPlatform(state, "ps2").TessellationMaxEdgeLength);
            Assert.False(Service.GetForPlatform(state, "windows").Tessellate);
            Assert.Equal(1.0d, Service.GetForPlatform(state, "windows").TessellationMaxEdgeLength);
        }

        /// <summary>
        /// Ensures returned settings remain detached from later mutations to the stored platform metadata.
        /// </summary>
        [Fact]
        public void GetForPlatform_WhenStoredValuesChange_ReturnsIndependentSettings() {
            EntityComponentSaveState state = new EntityComponentSaveState();
            Service.SetForPlatform(state, "ps2", new MeshComponentTessellationSettings(true, 0.5d));
            MeshComponentTessellationSettings originalSettings = Service.GetForPlatform(state, "ps2");

            Service.SetForPlatform(state, "ps2", new MeshComponentTessellationSettings(false, 2.0d));

            Assert.True(originalSettings.Tessellate);
            Assert.Equal(0.5d, originalSettings.TessellationMaxEdgeLength);
        }

        /// <summary>
        /// Ensures the generated-variant key is invariant across the current UI culture.
        /// </summary>
        [Fact]
        public void BuildVariantIdentity_WhenValuesContainFractions_UsesInvariantRoundTripValues() {
            MeshComponentTessellationSettings settings = new MeshComponentTessellationSettings(true, 0.125d);

            string identity = Service.BuildVariantIdentity("models/cube.hasset", "ps2", settings, new float3(2.5f, 1f, -0.5f));

            Assert.Equal("SourceModelReference=models/cube.hasset\nPlatformId=ps2\nTessellate=True\nTessellationMaxEdgeLength=0.125\nWorldScaleX=2.5\nWorldScaleY=1\nWorldScaleZ=-0.5", identity);
        }

        /// <summary>
        /// Ensures invalid editor values and invalid scale values are rejected before cooking begins.
        /// </summary>
        [Fact]
        public void SettingsAndIdentity_WhenValuesAreInvalid_ThrowArgumentExceptions() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeshComponentTessellationSettings(true, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeshComponentTessellationSettings(true, double.NaN));
            Assert.Throws<ArgumentException>(() => Service.BuildVariantIdentity("models/cube.hasset", "ps2", new MeshComponentTessellationSettings(true, 1d), new float3(0f, 1f, 1f)));
        }
    }
}
