using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.components.ui {
    /// <summary>
    /// Verifies the reusable editor color field control keeps its text and swatch synchronized.
    /// </summary>
    public sealed class EditorColorFieldControlTests {
        /// <summary>
        /// Ensures assigning one color updates the HTML textbox and the visible swatch color.
        /// </summary>
        [Fact]
        public void EditorColorFieldControl_SetValue_FormatsTheTextboxAndUpdatesTheSwatch() {
            InitializeCore();
            EditorEntity host = new EditorEntity();
            EditorColorFieldControl control = new EditorColorFieldControl(CreateFont(), 1);
            host.AddChild(control);

            control.SetValue(new byte4(0x33, 0x66, 0x99, 0xff));

            Assert.Equal("#336699", control.HexTextBoxControl.Text);

            RoundedRectComponent swatchBackground = GetPrivateField<RoundedRectComponent>(control.SwatchButtonControl, "roundedRect");
            Assert.Equal(new byte4(0x33, 0x66, 0x99, 0xff), swatchBackground.FillColor);
        }

        /// <summary>
        /// Ensures clicking the swatch opens the shared color picker beneath the field.
        /// </summary>
        [Fact]
        public void EditorColorFieldControl_WhenSwatchToggles_OpensTheOverlayBelowTheField() {
            InitializeCore();
            EditorEntity host = new EditorEntity();
            EditorColorFieldControl control = new EditorColorFieldControl(CreateFont(), 1);
            host.AddChild(control);

            bool requested = false;
            control.PickerRequested += () => requested = true;
            InteractableComponent interactable = GetPrivateField<InteractableComponent>(control.SwatchButtonControl, "interactableComponent");
            interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Hover);
            interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Press);
            interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Release);

            Assert.True(requested);
        }

        /// <summary>
        /// Ensures the shared color picker stays inside the window bounds when the swatch sits near the bottom edge.
        /// </summary>
        [Fact]
        public void EditorColorPickerOverlay_WhenOpenedNearTheBottom_ClampsIntoTheViewport() {
            InitializeCore();
            Core.Instance.RenderManager3D.AddWindow(IntPtr.Zero, 400, 300);

            EditorEntity host = new EditorEntity();
            EditorColorPickerOverlayComponent overlay = new EditorColorPickerOverlayComponent(CreateFont(), 1);
            host.AddChild(overlay);

            overlay.SetAnchorPosition(380f, 260f, 24);
            overlay.Open(new byte4(32, 64, 96, 255));
            overlay.UpdateLayout();

            Assert.True(overlay.IsOpen);
            Assert.Equal(4, overlay.OverlayPosition.X);
            Assert.Equal(4, overlay.OverlayPosition.Y);
        }

        /// <summary>
        /// Initializes the core services required by color field tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Creates a compact test font with the glyphs needed by the color field textbox.
        /// </summary>
        /// <returns>Font asset with basic glyph coverage.</returns>
        FontAsset CreateFont() {
        Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
            ['0'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['1'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
            ['2'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['#'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['3'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['4'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['5'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['6'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['7'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['8'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['9'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['c'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['f'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
        };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }

        /// <summary>
        /// Reads one private field from an object instance.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object instance whose private field should be read.</param>
        /// <param name="fieldName">Private field name.</param>
        /// <returns>Resolved field value.</returns>
        static T GetPrivateField<T>(object target, string fieldName) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            } else if (string.IsNullOrWhiteSpace(fieldName)) {
                throw new ArgumentException("Field name must be provided.", nameof(fieldName));
            }

            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Field '{fieldName}' was not found on '{target.GetType().FullName}'.");
            return (T)field.GetValue(target);
        }

    }
}
