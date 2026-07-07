using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the remove-component confirmation dialog behavior.
    /// </summary>
    public class RemoveComponentDialogTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the dialog tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the dialog tests.
        /// </summary>
        public RemoveComponentDialogTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-remove-component-dialog-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
            EditorInputCaptureService.Reset();

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Deletes temporary project state after each test.
        /// </summary>
        public void Dispose() {
            EditorInputCaptureService.Reset();
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the dialog message includes both the component and entity names.
        /// </summary>
        [Fact]
        public void Show_WhenNamesProvided_UpdatesMessageText() {
            RemoveComponentDialog dialog = new RemoveComponentDialog(CreateFont());

            dialog.Show("Cube", "Mesh Component");

            TextComponent messageText = GetPrivateField<TextComponent>(dialog, "MessageText");

            Assert.Equal("Remove Mesh Component from Cube?", messageText.Text);
        }

        /// <summary>
        /// Ensures the remove action raises the confirm event.
        /// </summary>
        [Fact]
        public void HandleRemoveClicked_RaisesConfirmRequested() {
            RemoveComponentDialog dialog = new RemoveComponentDialog(CreateFont());
            bool raised = false;
            dialog.ConfirmRequested += () => raised = true;
            dialog.Show("Cube", "Mesh Component");
            dialog.UpdateLayout(1280, 720);

            InvokePrivate(dialog, "HandleRemoveClicked");

            Assert.True(raised);
        }

        /// <summary>
        /// Ensures the cancel action raises the cancel event.
        /// </summary>
        [Fact]
        public void HandleCancelClicked_RaisesCancelRequested() {
            RemoveComponentDialog dialog = new RemoveComponentDialog(CreateFont());
            bool raised = false;
            dialog.CancelRequested += () => raised = true;
            dialog.Show("Cube", "Mesh Component");
            dialog.UpdateLayout(1280, 720);

            InvokePrivate(dialog, "HandleCancelClicked");

            Assert.True(raised);
        }

        /// <summary>
        /// Ensures the header close button reuses the cancel path.
        /// </summary>
        [Fact]
        public void HandleCloseClicked_RaisesCancelRequested() {
            RemoveComponentDialog dialog = new RemoveComponentDialog(CreateFont());
            bool raised = false;
            dialog.CancelRequested += () => raised = true;
            dialog.Show("Cube", "Mesh Component");
            dialog.UpdateLayout(1280, 720);

            InvokePrivate(dialog, "HandleCloseClicked");

            Assert.True(raised);
        }

        /// <summary>
        /// Ensures the confirmation message and footer buttons are positioned immediately during Show.
        /// </summary>
        [Fact]
        public void Show_WhenOpened_PositionsMessageAndFooterImmediately() {
            RemoveComponentDialog dialog = new RemoveComponentDialog(CreateFont());

            dialog.Show("Cube", "Mesh Component");

            EditorEntity messageHost = GetPrivateField<EditorEntity>(dialog, "MessageHost");
            EditorEntity removeButtonHost = GetPrivateField<EditorEntity>(dialog, "RemoveButtonHost");

            Assert.NotEqual(float3.Zero, messageHost.LocalPosition);
            Assert.NotEqual(float3.Zero, removeButtonHost.LocalPosition);
        }

        /// <summary>
        /// Invokes one non-public instance method.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        void InvokePrivate(object target, string methodName) {
            MethodInfo method = FindPrivateMethod(target.GetType(), methodName);
            method.Invoke(target, Array.Empty<object>());
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Field name to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = FindPrivateField(target.GetType(), fieldName);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Finds one inherited non-public instance field by walking the type hierarchy.
        /// </summary>
        /// <param name="type">Type that starts the field lookup.</param>
        /// <param name="fieldName">Field name to locate.</param>
        /// <returns>Matching field metadata.</returns>
        FieldInfo FindPrivateField(Type type, string fieldName) {
            Type currentType = type;

            while (currentType != null) {
                FieldInfo field = currentType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

                if (field != null) {
                    return field;
                }

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"Field '{fieldName}' was not found on type '{type.FullName}'.");
        }

        /// <summary>
        /// Finds one inherited non-public instance method by walking the type hierarchy.
        /// </summary>
        /// <param name="type">Type that starts the method lookup.</param>
        /// <param name="methodName">Method name to locate.</param>
        /// <returns>Matching method metadata.</returns>
        MethodInfo FindPrivateMethod(Type type, string methodName) {
            Type currentType = type;

            while (currentType != null) {
                MethodInfo method = currentType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);

                if (method != null) {
                    return method;
                }

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"Method '{methodName}' was not found on type '{type.FullName}'.");
        }

        /// <summary>
        /// Creates one small deterministic font asset for the dialog layout.
        /// </summary>
        /// <returns>Font asset with the glyphs required by the dialog.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['R'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['f'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['x'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['?'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f)
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

