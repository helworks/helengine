using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies checkbox interaction behavior for shared editor controls.
    /// </summary>
    public class CheckBoxComponentTests {
        /// <summary>
        /// Ensures clicking the checkbox toggles its checked state and raises one change event.
        /// </summary>
        [Fact]
        public void CheckBoxComponent_WhenClicked_TogglesCheckedStateAndRaisesChangedEvent() {
            InitializeCore();
            EditorEntity entity = new EditorEntity();
            CheckBoxComponent checkBox = new CheckBoxComponent(new int2(20, 20), CreateFont(), false);
            bool raisedValue = false;
            int raisedCount = 0;
            checkBox.CheckedChanged += (component, isChecked) => {
                raisedCount++;
                raisedValue = isChecked;
            };
            entity.AddComponent(checkBox);

            InteractableComponent interactable = GetPrivateField<InteractableComponent>(checkBox, "Interactable");

            interactable.OnCursor(new int2(10, 10), new int2(0, 0), PointerInteraction.Hover);
            interactable.OnCursor(new int2(10, 10), new int2(0, 0), PointerInteraction.Press);
            interactable.OnCursor(new int2(10, 10), new int2(0, 0), PointerInteraction.Release);

            Assert.True(checkBox.IsChecked);
            Assert.True(raisedValue);
            Assert.Equal(1, raisedCount);
            Assert.Equal(PointerCursorKind.Hand, interactable.HoverCursor);
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Field name to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Initializes the core services required by checkbox interaction tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Creates a deterministic font asset for checkbox glyph layout.
        /// </summary>
        /// <returns>Font asset with a basic uppercase X glyph.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['X'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
    }
}
