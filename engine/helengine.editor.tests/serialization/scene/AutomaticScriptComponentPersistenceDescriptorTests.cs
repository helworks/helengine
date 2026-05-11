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
        /// Ensures runtime-only scroll bindings are ignored while the remaining reflected members still round-trip.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenScrollComponentHasIgnoredEntityMember_SkipsRuntimeOnlyReference() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            ScrollComponent component = new ScrollComponent {
                Size = new int2(320, 180),
                ItemCount = 12,
                ItemExtent = 24,
                VisibleItemCount = 4,
                ScrollStepCount = 2,
                WheelNotchSize = 120,
                RequiresPointerInside = false
            };
            component.ContentRoot = new EditorEntity();

            SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, new EntityComponentSaveState());
            ScrollComponent deserialized = Assert.IsType<ScrollComponent>(descriptor.DeserializeComponent(record, null, null));

            Assert.Equal(new int2(320, 180), deserialized.Size);
            Assert.Equal(12, deserialized.ItemCount);
            Assert.Equal(24, deserialized.ItemExtent);
            Assert.Equal(4, deserialized.VisibleItemCount);
            Assert.Equal(2, deserialized.ScrollStepCount);
            Assert.Equal(120, deserialized.WheelNotchSize);
            Assert.False(deserialized.RequiresPointerInside);
            Assert.Null(deserialized.ContentRoot);
        }

        /// <summary>
        /// Ensures empty automatic-script payloads are rejected instead of being treated as legacy default instances.
        /// </summary>
        [Fact]
        public void DeserializeComponent_WhenAutomaticScriptPayloadIsEmpty_ThrowsUnsupportedPayloadVersion() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(TestUpdateOnlyScriptComponent)),
                ComponentIndex = 0,
                Payload = Array.Empty<byte>()
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => descriptor.DeserializeComponent(record, null, null));
            Assert.Contains("Automatic script component payload must use the current tagged scene payload format.", exception.Message);
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
