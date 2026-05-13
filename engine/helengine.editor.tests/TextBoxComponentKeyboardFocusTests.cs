using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies keyboard-focus behavior for text boxes.
    /// </summary>
    public class TextBoxComponentKeyboardFocusTests {
        /// <summary>
        /// Ensures text-box focus reuses the existing text-entry focus semantics and does not treat Space as activation.
        /// </summary>
        [Fact]
        public void TextBoxComponent_SetTargetFocused_UsesExistingTextFocusSemanticsWithoutSpaceActivation() {
            InitializeCore();
            TestFocusGroup focusGroup = new TestFocusGroup(null, 0, 0, 0, 240, 40);
            EditorEntity entity = new EditorEntity();
            TextBoxComponent textBox = new TextBoxComponent(new int2(180, 28), CreateFont(), "Name");
            int submitCount = 0;
            textBox.FocusGroup = focusGroup.FocusGroup;
            textBox.Submitted += submitted => submitCount++;
            entity.AddComponent(textBox);
            IFocusTarget focusTarget = textBox;

            focusTarget.SetTargetFocused(true);

            Assert.True(textBox.IsFocused);
            Assert.False(focusTarget.CanActivateWithKey(Keys.Space));

            focusTarget.ActivateFromKey(Keys.Enter);

            Assert.Equal(1, submitCount);
            Assert.False(textBox.IsFocused);
        }

        /// <summary>
        /// Ensures text-box removal clears both the static text-focus reference and the local focus state.
        /// </summary>
        [Fact]
        public void TextBoxComponent_ComponentRemoved_ClearsStaticTextFocusAndItsKeyboardState() {
            InitializeCore();
            TestFocusGroup focusGroup = new TestFocusGroup(null, 0, 0, 0, 240, 40);
            EditorEntity entity = new EditorEntity();
            TextBoxComponent textBox = new TextBoxComponent(new int2(180, 28), CreateFont(), "Name");
            textBox.FocusGroup = focusGroup.FocusGroup;
            entity.AddComponent(textBox);
            IFocusTarget focusTarget = textBox;

            focusTarget.SetTargetFocused(true);

            textBox.ComponentRemoved(entity);

            Assert.Null(GetFocusedTextBox());
            Assert.False(textBox.IsFocused);
        }

        /// <summary>
        /// Ensures the textbox creates a centered child text host instead of leaving its label flush to the top edge.
        /// </summary>
        [Fact]
        public void TextBoxComponent_ComponentAdded_CentersItsTextHostVertically() {
            InitializeCore();
            EditorEntity entity = new EditorEntity();
            TextBoxComponent textBox = new TextBoxComponent(new int2(180, 28), CreateFont(), "Name");
            entity.AddComponent(textBox);

            Entity textHost = Assert.Single(entity.Children, child => child.Components.OfType<TextComponent>().Any());

            Assert.Equal(6f, textHost.LocalPosition.Y);
            Assert.Equal(0.1f, textHost.LocalPosition.Z);
        }

        /// <summary>
        /// Ensures the textbox requests the text-edit cursor when hovered so the host window can present an I-beam.
        /// </summary>
        [Fact]
        public void TextBoxComponent_ComponentAdded_RequestsTextHoverCursor() {
            InitializeCore();
            EditorEntity entity = new EditorEntity();
            TextBoxComponent textBox = new TextBoxComponent(new int2(180, 28), CreateFont(), "Name");
            entity.AddComponent(textBox);

            InteractableComponent interactable = Assert.Single(entity.Components.OfType<InteractableComponent>());

            Assert.Equal(PointerCursorKind.Text, interactable.HoverCursor);
        }

        /// <summary>
        /// Ensures the textbox clamps negative cursor positions before it renders the caret.
        /// </summary>
        [Fact]
        public void TextBoxComponent_UpdateTextDisplay_WhenCursorIsNegative_ClampsTheCaretIndex() {
            InitializeCore();
            EditorEntity entity = new EditorEntity();
            TextBoxComponent textBox = new TextBoxComponent(new int2(180, 28), CreateFont(), "Name");
            entity.AddComponent(textBox);
            textBox.Text = "abc";

            TextBoxEditState editState = GetPrivateField<TextBoxEditState>(textBox, "EditState");
            editState.CursorPosition = -1;
            SetPrivateField(textBox, "isFocused", true);
            SetPrivateField(textBox, "cursorVisible", true);

            InvokePrivate(textBox, "UpdateTextDisplay");

            TextComponent textComponent = GetPrivateField<TextComponent>(textBox, "textComponent");

            Assert.Equal("|abc", textComponent.Text);
        }

        /// <summary>
        /// Ensures the textbox can accept typing and backspace edits through the normal engine update loop.
        /// </summary>
        [Fact]
        public void TextBoxComponent_WhenFocused_ProcessesTypingAndBackspaceEdits() {
            InitializeCore();
            EditorEntity entity = new EditorEntity();
            TextBoxComponent textBox = new TextBoxComponent(new int2(180, 28), CreateFont(), "Name");
            entity.AddComponent(textBox);

            textBox.IsFocused = true;

            ((TestInputBackend)Core.Instance.InputSystem.Backend).SetKeyboardState(new KeyboardState(Keys.A));
            Core.Instance.Update();
            Assert.Equal("a", textBox.Text);

            ((TestInputBackend)Core.Instance.InputSystem.Backend).SetKeyboardState(new KeyboardState(Keys.B));
            Core.Instance.Update();
            Assert.Equal("ab", textBox.Text);

            ((TestInputBackend)Core.Instance.InputSystem.Backend).SetKeyboardState(new KeyboardState(Keys.Back));
            Core.Instance.Update();
            Assert.Equal("a", textBox.Text);
        }

        /// <summary>
        /// Ensures clicking within the visible text moves the caret to the clicked character position instead of the end.
        /// </summary>
        [Fact]
        public void TextBoxComponent_OnCursorEvent_WhenClickedInsideText_MovesCaretToTheClickedPosition() {
            InitializeCore();
            EditorEntity entity = new EditorEntity();
            TextBoxComponent textBox = new TextBoxComponent(new int2(180, 28), CreateFont(), "Name");
            entity.AddComponent(textBox);
            textBox.Text = "Name";

            InvokePrivate(textBox, "OnCursorEvent", new int2(18, 10), new int2(0, 0), PointerInteraction.Press);

            TextBoxEditState editState = GetPrivateField<TextBoxEditState>(textBox, "EditState");

            Assert.True(textBox.IsFocused);
            Assert.Equal(1, editState.CursorPosition);
        }

        /// <summary>
        /// Ensures dragging the mouse across text creates a live selection range.
        /// </summary>
        [Fact]
        public void TextBoxComponent_OnCursorEvent_WhenDragged_SelectsTextRange() {
            InitializeCore();
            EditorEntity entity = new EditorEntity();
            TextBoxComponent textBox = new TextBoxComponent(new int2(180, 28), CreateFont(), "Name");
            entity.AddComponent(textBox);
            textBox.Text = "Name";

            InvokePrivate(textBox, "OnCursorEvent", new int2(18, 10), new int2(0, 0), PointerInteraction.Press);
            InvokePrivate(textBox, "OnCursorEvent", new int2(42, 10), new int2(24, 0), PointerInteraction.Hover);

            TextBoxEditState editState = GetPrivateField<TextBoxEditState>(textBox, "EditState");

            Assert.True(editState.HasSelection);
            Assert.Equal(1, editState.SelectionStart);
            Assert.Equal(4, editState.SelectionEnd);
        }

        /// <summary>
        /// Ensures typing while text is selected replaces the selected text instead of inserting beside it.
        /// </summary>
        [Fact]
        public void TextBoxComponent_WhenSelectionExists_TypingReplacesSelectedText() {
            InitializeCore();
            EditorEntity entity = new EditorEntity();
            TextBoxComponent textBox = new TextBoxComponent(new int2(180, 28), CreateFont(), "Name");
            entity.AddComponent(textBox);
            textBox.Text = "Name";
            textBox.IsFocused = true;

            InvokePrivate(textBox, "OnCursorEvent", new int2(18, 10), new int2(0, 0), PointerInteraction.Press);
            InvokePrivate(textBox, "OnCursorEvent", new int2(42, 10), new int2(24, 0), PointerInteraction.Hover);

            ((TestInputBackend)Core.Instance.InputSystem.Backend).SetKeyboardState(new KeyboardState(Keys.B));
            Core.Instance.Update();

            Assert.Equal("Nb", textBox.Text);
        }

        /// <summary>
        /// Ensures the textbox selects all text when the registered select-all shortcut is pressed.
        /// </summary>
        [Fact]
        public void TextBoxComponent_WhenSelectAllShortcutIsPressed_SelectsAllText() {
            InitializeCore();
            EditorEntity entity = new EditorEntity();
            TextBoxComponent textBox = new TextBoxComponent(new int2(180, 28), CreateFont(), "Name");
            entity.AddComponent(textBox);
            textBox.Text = "Name";
            textBox.IsFocused = true;

            ((TestInputBackend)Core.Instance.InputSystem.Backend).SetKeyboardState(new KeyboardState(Keys.LeftControl, Keys.A));
            Core.Instance.Update();

            TextBoxEditState editState = GetPrivateField<TextBoxEditState>(textBox, "EditState");
            Assert.True(editState.HasSelection);
            Assert.Equal(0, editState.SelectionStart);
            Assert.Equal(4, editState.SelectionEnd);
        }

        /// <summary>
        /// Ensures copy and paste shortcuts use the configured clipboard service instead of inserting shortcut characters.
        /// </summary>
        [Fact]
        public void TextBoxComponent_WhenCopyAndPasteShortcutsArePressed_UsesTheClipboardService() {
            InitializeCore();
            TestTextClipboardService clipboardService = new TestTextClipboardService();
            Core.Instance.SetTextClipboardService(clipboardService);
            EditorEntity entity = new EditorEntity();
            TextBoxComponent textBox = new TextBoxComponent(new int2(180, 28), CreateFont(), "Name");
            entity.AddComponent(textBox);
            textBox.Text = "Name";
            textBox.IsFocused = true;

            InvokePrivate(textBox, "OnCursorEvent", new int2(18, 10), new int2(0, 0), PointerInteraction.Press);
            InvokePrivate(textBox, "OnCursorEvent", new int2(42, 10), new int2(24, 0), PointerInteraction.Hover);

            ((TestInputBackend)Core.Instance.InputSystem.Backend).SetKeyboardState(new KeyboardState(Keys.LeftControl, Keys.C));
            Core.Instance.Update();

            Assert.Equal("ame", clipboardService.Text);

            textBox.Text = string.Empty;
            textBox.IsFocused = true;

            ((TestInputBackend)Core.Instance.InputSystem.Backend).SetKeyboardState(new KeyboardState(Keys.LeftControl, Keys.V));
            Core.Instance.Update();

            Assert.Equal("ame", textBox.Text);
        }

        /// <summary>
        /// Ensures the textbox honors a custom select-all shortcut registered through the textbox shortcut registry.
        /// </summary>
        [Fact]
        public void TextBoxComponent_WhenShortcutRegistryOverridesSelectAllKey_UsesTheRegisteredShortcut() {
            InitializeCore();
            Core.Instance.TextBoxShortcutRegistry.SelectAllShortcut = new TextBoxShortcutBinding(Keys.F2, false, false, false);
            EditorEntity entity = new EditorEntity();
            TextBoxComponent textBox = new TextBoxComponent(new int2(180, 28), CreateFont(), "Name");
            entity.AddComponent(textBox);
            textBox.Text = "Name";
            textBox.IsFocused = true;

            ((TestInputBackend)Core.Instance.InputSystem.Backend).SetKeyboardState(new KeyboardState(Keys.F2));
            Core.Instance.Update();

            TextBoxEditState editState = GetPrivateField<TextBoxEditState>(textBox, "EditState");
            Assert.True(editState.HasSelection);
            Assert.Equal(0, editState.SelectionStart);
            Assert.Equal(4, editState.SelectionEnd);
        }

        /// <summary>
        /// Initializes the core services required by keyboard-focus tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Reads the static focused text-box field.
        /// </summary>
        /// <returns>Currently focused text box, or null when none are focused.</returns>
        TextBoxComponent GetFocusedTextBox() {
            FieldInfo field = typeof(TextBoxComponent).GetField("focusedTextBox", BindingFlags.Static | BindingFlags.NonPublic);
            object value = field.GetValue(null);
            if (value == null) {
                return null;
            }

            return Assert.IsType<TextBoxComponent>(value);
        }

        /// <summary>
        /// Creates a deterministic font asset for text-box layout tests.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['N'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = FindPrivateField(target.GetType(), fieldName);
            return Assert.IsAssignableFrom<T>(field.GetValue(target));
        }

        /// <summary>
        /// Writes one non-public instance field.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to write.</param>
        /// <param name="value">Value to assign.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = FindPrivateField(target.GetType(), fieldName);
            field.SetValue(target, value);
        }

        /// <summary>
        /// Invokes one non-public instance method.
        /// </summary>
        /// <param name="target">Object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="arguments">Arguments passed to the method.</param>
        void InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) {
                throw new InvalidOperationException($"Could not find private method '{methodName}'.");
            }

            method.Invoke(target, arguments);
        }

        /// <summary>
        /// Finds one non-public instance field on the supplied type hierarchy.
        /// </summary>
        /// <param name="type">Type to inspect.</param>
        /// <param name="fieldName">Field name to locate.</param>
        /// <returns>Matching field definition.</returns>
        FieldInfo FindPrivateField(Type type, string fieldName) {
            for (Type current = type; current != null; current = current.BaseType) {
                FieldInfo field = current.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null) {
                    return field;
                }
            }

            throw new InvalidOperationException($"Could not find private field '{fieldName}'.");
        }
    }
}

