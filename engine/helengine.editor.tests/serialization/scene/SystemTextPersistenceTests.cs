using helengine.editor;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>Verifies append-compatible system style authoring through the existing tagged and nested scene serializer.</summary>
    public sealed class SystemTextPersistenceTests {
        /// <summary>Older authored text fields omit system styles and continue to select their existing bitmap-font path.</summary>
        [Fact]
        public void LegacyAuthoredTextKeepsBitmapDefaults() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField(nameof(TextComponent.Text), field => field.WriteString("Legacy text"));
            writer.WriteField(nameof(TextComponent.Size), field => field.WriteInt2(new int2(130, 30)));
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(TextComponent)),
                Payload = writer.BuildPayload()
            };
            TextComponent restored = Assert.IsType<TextComponent>(descriptor.DeserializeComponent(record, null, null));
            Assert.Equal("Legacy text", restored.Text);
            Assert.Equal(new int2(130, 30), restored.Size);
            Assert.Null(restored.SystemTextStyle);
            Assert.Null(restored.PersistedSystemTextStyle);
            Assert.False(restored.ClipSystemTextToSize);
        }
        /// <summary>Immutable system styles round-trip all choices without changing the older native-font-pixels suffix ordinal.</summary>
        [Fact]
        public void AuthoredSystemStyleRoundTripsUsingExistingAppendSuffix() {
            ScriptComponentReflectionSchemaBuilder schemas = new ScriptComponentReflectionSchemaBuilder();
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(schemas);
            TextComponent text = new TextComponent {
                Text = "Nome 😀 é אבג",
                SystemTextStyle = new SystemTextStyle(SystemTextRole.Title, SystemTextWeight.Semibold, true, 23.5f),
                Size = new int2(220, 60),
                Alignment = TextAlignment.Right,
                FontScale = 1.5f,
                ClipSystemTextToSize = true
            };
            SceneComponentAssetRecord record = descriptor.SerializeComponent(text, 0, new EntityComponentSaveState());
            TextComponent restored = Assert.IsType<TextComponent>(descriptor.DeserializeComponent(record, null, null));
            Assert.Equal(text.Text, restored.Text);
            Assert.Equal(SystemTextRole.Title, restored.SystemTextStyle.Role);
            Assert.Equal(SystemTextWeight.Semibold, restored.SystemTextStyle.Weight);
            Assert.True(restored.SystemTextStyle.Rounded);
            Assert.Equal(23.5f, restored.SystemTextStyle.PointSize);
            Assert.Equal(text.FontScale, restored.FontScale);
            Assert.Equal(text.Alignment, restored.Alignment);
            Assert.True(restored.ClipSystemTextToSize);
            ScriptComponentReflectionSchema schema = schemas.Build(typeof(TextComponent));
            Assert.Equal(nameof(TextComponent.NativeFontPixels), schema.Members[schema.Members.Count - 3].Name);
            Assert.Equal(nameof(TextComponent.PersistedSystemTextStyle), schema.Members[schema.Members.Count - 2].Name);
            Assert.Equal(nameof(TextComponent.ClipSystemTextToSize), schema.Members[schema.Members.Count - 1].Name);
        }
        /// <summary>Invalid persisted values fail before replacing a previously valid style, and mutable snapshots cannot alter live typography.</summary>
        [Fact]
        public void PersistedSnapshotsAreValidatedAndIndependent() {
            TextComponent text = new TextComponent { SystemTextStyle = new SystemTextStyle(SystemTextRole.Body) };
            SystemTextSettings snapshot = text.PersistedSystemTextStyle;
            snapshot.PointSize = 99f;
            Assert.Equal(0f, text.SystemTextStyle.PointSize);
            Assert.Throws<ArgumentOutOfRangeException>(() => text.PersistedSystemTextStyle = new SystemTextSettings { Weight = (SystemTextWeight)99 });
            Assert.Equal(SystemTextRole.Body, text.SystemTextStyle.Role);
            text.PersistedSystemTextStyle = null;
            Assert.Null(text.SystemTextStyle);
        }
    }
}
