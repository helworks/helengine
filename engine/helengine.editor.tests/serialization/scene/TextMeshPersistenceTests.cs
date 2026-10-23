using helengine;
using helengine.editor;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>Verifies authored text persists without reading or saving its transient GPU model.</summary>
    public sealed class TextMeshPersistenceTests {
        /// <summary>Serializing attached text does not request a model even when runtime atlas bindings are deliberately unavailable.</summary>
        [Fact]
        public void SceneRoundTripIgnoresGeneratedGeometry() {
            using Core core=new Core(new CoreInitializationOptions{ContentStreamSource=new HostFileSystemContentStreamSource(AppContext.BaseDirectory)});
            core.Initialize(null,null,null,new PlatformInfo("test","test"));
            using Entity entity=new Entity(core); entity.InitComponents();
            TextMeshComponent text=new TextMeshComponent{Text="Relógio",RenderOrder3D=2}; entity.AddComponent(text);
            Assert.Throws<InvalidOperationException>(()=>text.Model);
            AutomaticScriptComponentPersistenceDescriptor descriptor=new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            SceneComponentAssetRecord record=descriptor.SerializeComponent(text,0,new EntityComponentSaveState());
            using TextMeshComponent restored=Assert.IsType<TextMeshComponent>(descriptor.DeserializeComponent(record,null,null));
            Assert.Equal("Relógio",restored.Text); Assert.Equal((byte)2,restored.RenderOrder3D);
            Assert.Null(restored.Font); Assert.Null(restored.Model); Assert.Empty(restored.Materials);
        }
    }
}