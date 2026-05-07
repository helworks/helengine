using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies the automatic reflected editor persistence fallback for scripted components.
    /// </summary>
    public sealed class AutomaticScriptComponentPersistenceDescriptorTests {
        /// <summary>
        /// Ensures supported scripted-component members serialize through the reflected fallback and round-trip successfully without warning noise.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenScriptComponentHasSupportedMembers_UsesAutomaticFallbackWithoutWarnings() {
            ScriptComponentReflectionSchemaBuilder schemaBuilder = new ScriptComponentReflectionSchemaBuilder();
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(schemaBuilder);
            TestScriptSerializableComponent component = new TestScriptSerializableComponent {
                DisplayName = "Menu Row",
                Visible = true,
                SortOrder = 7
            };
            List<LogEntry> warnings = new List<LogEntry>();
            Logger.WarningLogged += warnings.Add;

            try {
                SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, new EntityComponentSaveState());
                TestScriptSerializableComponent deserialized = Assert.IsType<TestScriptSerializableComponent>(
                    descriptor.DeserializeComponent(record, null, null));

                Assert.Equal(AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(TestScriptSerializableComponent)), record.ComponentTypeId);
                Assert.Equal("Menu Row", deserialized.DisplayName);
                Assert.True(deserialized.Visible);
                Assert.Equal(7, deserialized.SortOrder);
                Assert.Empty(warnings);
            } finally {
                Logger.WarningLogged -= warnings.Add;
            }
        }

        /// <summary>
        /// Ensures unsupported reflected member types fail clearly instead of being silently skipped.
        /// </summary>
        [Fact]
        public void SerializeComponent_WhenMemberTypeIsUnsupported_Throws() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            UnsupportedScriptComponent component = new UnsupportedScriptComponent();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => descriptor.SerializeComponent(component, 0, new EntityComponentSaveState()));

            Assert.Contains(typeof(Entity).FullName, exception.Message, StringComparison.Ordinal);
        }
    }
}
