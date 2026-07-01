using helengine.editor;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies automatic reflected component persistence rejects raw binary blob members.
    /// </summary>
    public sealed class AutomaticScriptComponentBinaryPayloadGuardTests {
        /// <summary>
        /// Ensures raw `byte[]` members are rejected so plugin components must use engine-managed binary payload types instead of prepacked blobs.
        /// </summary>
        [Fact]
        public void SerializeComponent_WhenAutomaticComponentContainsByteArrayMember_ThrowsInvalidOperationException() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            AutomaticScriptComponentBinaryPayloadGuardTestComponent component = new AutomaticScriptComponentBinaryPayloadGuardTestComponent {
                RawPayload = [0x01, 0x02, 0x03]
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => descriptor.SerializeComponent(component, 0, new EntitySaveComponent().GetOrCreateComponentState(component)));

            Assert.Contains("byte[]", exception.Message, StringComparison.Ordinal);
            Assert.Contains("engine-managed binary payload", exception.Message, StringComparison.Ordinal);
        }
    }
}
