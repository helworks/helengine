using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that combo-box popups cover controls nested at greater local depth.
    /// </summary>
    public class ComboBoxPopupDepthTests {
        /// <summary>
        /// Ensures the open list has enough physical depth to cover neighboring nested controls.
        /// </summary>
        [Fact]
        public void ComboBoxPopup_RendersAboveNestedSiblingControl() {
            InitializeCore();

            EditorEntity root = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                Position = new float3(0f, 0f, 32f)
            };
            EditorEntity comboHost = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                Position = new float3(0f, 0f, 0.4f)
            };
            root.AddChild(comboHost);
            ComboBoxComponent comboBox = new ComboBoxComponent(new int2(180, 28), CreateFont(), new[] { "One", "Two" }, 0);
            comboHost.AddComponent(comboBox);

            EditorEntity siblingHost = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                Position = new float3(0f, 0f, 0.5f)
            };
            root.AddChild(siblingHost);
            EditorEntity siblingLabelHost = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                Position = new float3(0f, 0f, 0.3f)
            };
            siblingHost.AddChild(siblingLabelHost);
            TextComponent siblingLabel = new TextComponent {
                Font = CreateFont(),
                Text = "Neighbor"
            };
            siblingLabelHost.AddComponent(siblingLabel);

            comboBox.IsOpen = true;
            RoundedRectComponent popup = GetPrivateField<RoundedRectComponent>(comboBox, "ListBackground");

            Assert.True(RenderDepthOrder2D.CompareDrawables(siblingLabel, popup) < 0);
        }

        /// <summary>
        /// Initializes the engine services required by the popup depth test.
        /// </summary>
        void InitializeCore() {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            core.Initialize(null, new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>The requested private field value.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            System.Reflection.FieldInfo field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Creates a deterministic font asset for popup text.
        /// </summary>
        /// <returns>A small test font.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['O'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['T'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['N'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture { Width = 64, Height = 64 },
                characters,
                16f,
                64,
                64);
        }
    }
}
