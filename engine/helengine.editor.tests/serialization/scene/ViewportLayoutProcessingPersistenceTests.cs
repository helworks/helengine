using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>Exercises persisted viewport policies through the actual component registry.</summary>
    public sealed class ViewportLayoutProcessingPersistenceTests {
        /// <summary>Ensures missing policy data preserves legacy dynamic behavior.</summary>
        [Fact]
        public void OldPayload_DefaultsToDynamic() {
            PropertyInfo mode = typeof(ViewportComponent).GetProperty("LayoutProcessingMode");
            Assert.NotNull(mode);
            Assert.Equal((byte)0, Convert.ToByte(mode.GetValue(new ViewportComponent())));
            Assert.Equal(typeof(byte), Enum.GetUnderlyingType(mode.PropertyType));
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("ScalingMode", field => field.WriteByte(0));
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            IComponentPersistenceDescriptor descriptor = registry.GetDescriptor(new ViewportComponent());
            ViewportComponent old = Assert.IsType<ViewportComponent>(descriptor.DeserializeComponent(new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.ViewportComponent", Payload = writer.BuildPayload()
            }, null, null));
            Assert.Equal((byte)0, Convert.ToByte(mode.GetValue(old)));
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
        public void PolicyAndCookMetadata_AreOptionalSuffix() {
            ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(typeof(ViewportComponent));
            Assert.Equal("LayoutProcessingMode", schema.Members[schema.Members.Count - 2].Name);
            Assert.Equal("CookedLayout", schema.Members[schema.Members.Count - 1].Name);
            Assert.Null(typeof(ViewportComponent).GetProperty("CookedLayout").GetValue(new ViewportComponent()));
        }
    }
}
