using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>Exercises persisted viewport policies through the actual component registry.</summary>
    public sealed class ViewportLayoutProcessingPersistenceTests {
        /// <summary>Old ordinal assets lacking current layout members are rejected by both official readers.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void OldOrdinalPayload_IsRejected(bool managedRuntime) {
            ViewportComponent source = new ViewportComponent { ScalingMode = 2, ReferenceWidth = 640, ReferenceHeight = 480 };
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            IComponentPersistenceDescriptor descriptor = registry.GetDescriptor(source);
            SceneComponentAssetRecord named = descriptor.SerializeComponent(source, 0, new EntityComponentSaveState());
            ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(typeof(ViewportComponent));
            EditorTaggedSceneComponentFieldReader tagged = new EditorTaggedSceneComponentFieldReader(named.Payload);
            using MemoryStream stream = new MemoryStream();
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian)) {
                writer.WriteByte(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion);
                writer.WriteInt32(schema.Members.Count - 2);
                for (int index = 0; index < schema.Members.Count - 2; index++) {
                    Assert.True(tagged.TryGetFieldPayloadLength(schema.Members[index].Name, out int length));
                    Assert.True(tagged.TryGetFieldReader(schema.Members[index].Name, out EngineBinaryReader reader));
                    using (reader) {
                        for (int offset = 0; offset < length; offset++) { writer.WriteByte(reader.ReadByte()); }
                    }
                }
            }
            named.Payload = stream.ToArray();
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => {
                if (managedRuntime) {
                    new AutomaticScriptComponentRuntimeDeserializer(named.ComponentTypeId, typeof(ViewportComponent)).Deserialize(named, null);
                } else {
                    descriptor.DeserializeComponent(named, null, null);
                }
            });
            Assert.Contains("member count", error.Message);
            Assert.Contains("Regenerate", error.Message);
        }
        /// <summary>Named authoring payloads must explicitly carry the current layout fields, including nullable metadata.</summary>
        [Theory]
        [InlineData(false, "LayoutProcessingMode")]
        [InlineData(false, "CookedLayout")]
        [InlineData(true, "CookedLayout")]
        public void NamedPayload_MissingLayoutField_IsRejected(bool legacyFit, string missingField) {
            Component source = legacyFit ? new ReferenceCanvasFitComponent() : new ViewportComponent();
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            IComponentPersistenceDescriptor descriptor = registry.GetDescriptor(source);
            SceneComponentAssetRecord record = descriptor.SerializeComponent(source, 0, new EntityComponentSaveState());
            EditorTaggedSceneComponentFieldReader tagged = new EditorTaggedSceneComponentFieldReader(record.Payload);
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            foreach (string field in tagged.FieldNames) {
                if (field == missingField) { continue; }
                Assert.True(tagged.TryGetFieldPayloadLength(field, out int length));
                Assert.True(tagged.TryGetFieldReader(field, out EngineBinaryReader reader));
                using (reader) {
                    byte[] bytes = new byte[length];
                    for (int index = 0; index < length; index++) { bytes[index] = reader.ReadByte(); }
                    writer.WriteField(field, output => {
                        for (int index = 0; index < bytes.Length; index++) { output.WriteByte(bytes[index]); }
                    });
                }
            }
            record.Payload = writer.BuildPayload();
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => descriptor.DeserializeComponent(record, null, null));
            Assert.Contains(missingField, error.Message);
            Assert.Contains("Regenerate", error.Message);
        }

        /// <summary>Uses the existing property override mechanism without modifying Common.</summary>
        [Fact]
        public void PlatformOverride_ChangesOnlyTarget() {
            ViewportComponent common = new ViewportComponent();
            EntitySaveComponent save = new EntitySaveComponent();
            ComponentPlatformEditingService service = new ComponentPlatformEditingService();
            ViewportComponent editable = Assert.IsType<ViewportComponent>(service.EnsurePlatformOverrideComponent(common, save, "ps1"));
            editable.LayoutProcessingMode = ViewportLayoutProcessingMode.Cook;
            service.MarkPropertyOverride(common, save, "ps1", "LayoutProcessingMode");
            service.PersistPlatformOverride(common, editable, save, "ps1");
            Assert.Equal(ViewportLayoutProcessingMode.Dynamic, common.LayoutProcessingMode);
            Assert.Equal(ViewportLayoutProcessingMode.Cook,
                Assert.IsType<ViewportComponent>(service.ResolveEditableComponent(common, save, "ps1")).LayoutProcessingMode);
            Assert.Equal(ViewportLayoutProcessingMode.Dynamic,
                Assert.IsType<ViewportComponent>(service.ResolveEditableComponent(common, save, "windows")).LayoutProcessingMode);
        }

        /// <summary>Authoring serialization excludes derived cook output without losing the live metadata.</summary>
        [Fact]
        public void CookMetadata_DoesNotLeakIntoAuthoredSave() {
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test"));
            Entity root = new Entity(core);
            root.InitComponents();
            root.InitChildren();
            ViewportComponent viewport = new ViewportComponent { CookedLayout = new ViewportCookedLayout { AlgorithmVersion = 1 } };
            root.AddComponent(viewport);
            ViewportCookedLayout metadata = viewport.CookedLayout;
            IComponentPersistenceDescriptor descriptor = new ComponentPersistenceRegistry().GetDescriptor(viewport);
            SceneComponentAssetRecord record = SceneViewportAuthoredLayoutScope.ForEntity(root).Serialize(
                () => descriptor.SerializeComponent(viewport, 0, new EntityComponentSaveState()));
            Assert.Null(Assert.IsType<ViewportComponent>(descriptor.DeserializeComponent(record, null, null)).CookedLayout);
            Assert.Same(metadata, viewport.CookedLayout);
        }

        /// <summary>Round-trips each policy using the named scene payload.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void NamedPayload_PreservesMode(byte value) {
            PropertyInfo mode = typeof(ViewportComponent).GetProperty("LayoutProcessingMode");
            Assert.NotNull(mode);
            ViewportComponent source = new ViewportComponent();
            mode.SetValue(source, Enum.ToObject(mode.PropertyType, value));
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            IComponentPersistenceDescriptor descriptor = registry.GetDescriptor(source);
            SceneComponentAssetRecord record = descriptor.SerializeComponent(source, 0, new EntityComponentSaveState());
            ViewportComponent restored = Assert.IsType<ViewportComponent>(descriptor.DeserializeComponent(record, null, null));
            Assert.Equal(value, Convert.ToByte(mode.GetValue(restored)));
        }

        /// <summary>Leaves old ordinal members in their established positions.</summary>
        [Fact]
        public void PolicyAndCookMetadata_KeepAppendOrder() {
            ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(typeof(ViewportComponent));
            Assert.Equal("LayoutProcessingMode", schema.Members[schema.Members.Count - 2].Name);
            Assert.Equal("CookedLayout", schema.Members[schema.Members.Count - 1].Name);
            Assert.Null(typeof(ViewportComponent).GetProperty("CookedLayout").GetValue(new ViewportComponent()));
        }
    }
}
