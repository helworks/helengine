using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies standalone modal behavior for the reparent dialog.
    /// </summary>
    public class ReparentEntityDialogTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the dialog tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the dialog tests.
        /// </summary>
        public ReparentEntityDialogTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-reparent-dialog-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
            EditorInputCaptureService.Reset();

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
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
        /// Ensures the dialog header uses a dedicated title-bar color instead of the panel fill color.
        /// </summary>
        [Fact]
        public void Constructor_UsesDistinctHeaderColor() {
            ReparentEntityDialog dialog = new ReparentEntityDialog(CreateFont());
            RoundedRectComponent panelBackground = GetPrivateField<RoundedRectComponent>(dialog, "PanelBackground");
            SpriteComponent headerBackground = GetPrivateField<SpriteComponent>(dialog, "HeaderBackground");

            Assert.Equal(ThemeManager.Colors.AccentSecondary, headerBackground.Color);
            Assert.NotEqual(panelBackground.FillColor, headerBackground.Color);
        }

        /// <summary>
        /// Ensures the title bar touches the panel borders instead of using inset chrome.
        /// </summary>
        [Fact]
        public void UpdateLayout_PositionsHeaderFlushToPanelEdges() {
            ReparentEntityDialog dialog = new ReparentEntityDialog(CreateFont());
            EditorEntity root = new EditorEntity {
                Name = "Root"
            };
            EditorEntity child = new EditorEntity {
                Name = "Child"
            };
            root.AddChild(child);

            dialog.Show(child, new Entity[] { root, child });
            dialog.UpdateLayout(1280, 720);

            EditorEntity headerRoot = GetPrivateField<EditorEntity>(dialog, "HeaderRoot");
            SpriteComponent headerBackground = GetPrivateField<SpriteComponent>(dialog, "HeaderBackground");

            Assert.Equal(0f, headerRoot.LocalPosition.X);
            Assert.Equal(0f, headerRoot.LocalPosition.Y);
            Assert.Equal(ReparentEntityDialog.PanelWidth, headerBackground.Size.X);
            Assert.Equal(ReparentEntityDialog.HeaderHeight, headerBackground.Size.Y);
        }

        /// <summary>
        /// Ensures the close button fills the title-bar height and touches the right edge.
        /// </summary>
        [Fact]
        public void UpdateLayout_PositionsCloseButtonAsFullHeightRightEdgeChrome() {
            ReparentEntityDialog dialog = new ReparentEntityDialog(CreateFont());
            EditorEntity root = new EditorEntity {
                Name = "Root"
            };
            EditorEntity child = new EditorEntity {
                Name = "Child"
            };
            root.AddChild(child);

            dialog.Show(child, new Entity[] { root, child });
            dialog.UpdateLayout(1280, 720);

            EditorEntity closeButtonHost = GetPrivateField<EditorEntity>(dialog, "CloseButtonHost");
            RoundedRectComponent closeButtonBackground = closeButtonHost.Components.OfType<RoundedRectComponent>().Single();
            Assert.Equal(ReparentEntityDialog.PanelWidth - closeButtonBackground.Size.X, closeButtonHost.LocalPosition.X);
            Assert.Equal(0f, closeButtonHost.LocalPosition.Y);
            Assert.Equal(ReparentEntityDialog.HeaderHeight, closeButtonBackground.Size.Y);
        }

        /// <summary>
        /// Ensures scaled metrics resize the target row, hierarchy area, and footer buttons.
        /// </summary>
        [Fact]
        public void UpdateLayout_WithScaledMetrics_UsesScaledContentAndFooterLayout() {
            EditorUiMetrics metrics = new EditorUiMetrics(1.5d);
            ReparentEntityDialog dialog = new ReparentEntityDialog(CreateFont(), metrics);
            EditorEntity root = new EditorEntity {
                Name = "Root"
            };
            EditorEntity child = new EditorEntity {
                Name = "Child"
            };
            root.AddChild(child);

            dialog.Show(child, new Entity[] { root, child });
            dialog.UpdateLayout(1280, 720);

            EditorEntity targetHost = GetPrivateField<EditorEntity>(dialog, "TargetHost");
            EditorEntity cancelButtonHost = GetPrivateField<EditorEntity>(dialog, "CancelButtonHost");
            ButtonComponent cancelButton = GetPrivateField<ButtonComponent>(dialog, "CancelButton");
            ButtonComponent applyButton = GetPrivateField<ButtonComponent>(dialog, "ApplyButton");

            Assert.Equal(metrics.ScalePixels(ReparentEntityDialog.PanelPadding), (int)Math.Round(targetHost.LocalPosition.X));
            Assert.Equal(metrics.ScalePixels(ReparentEntityDialog.PanelPadding + ReparentEntityDialog.HeaderHeight + ReparentEntityDialog.SectionSpacing), (int)Math.Round(targetHost.LocalPosition.Y));
            Assert.Equal(new int2(metrics.ScalePixels(88), metrics.ScalePixels(22)), cancelButton.Size);
            Assert.Equal(new int2(metrics.ScalePixels(88), metrics.ScalePixels(22)), applyButton.Size);
            Assert.Equal(metrics.ScalePixels(ReparentEntityDialog.PanelWidth - ReparentEntityDialog.PanelPadding - 88), (int)Math.Round(cancelButtonHost.LocalPosition.X));
        }

        /// <summary>
        /// Ensures the close button owns the same left separator used by the editor window chrome.
        /// </summary>
        [Fact]
        public void Constructor_CreatesCloseButtonLeftSeparator() {
            ReparentEntityDialog dialog = new ReparentEntityDialog(CreateFont());
            SpriteComponent closeButtonSeparator = GetPrivateField<SpriteComponent>(dialog, "CloseButtonSeparator");

            Assert.Equal(TextureUtils.PixelTexture, closeButtonSeparator.Texture);
            Assert.Equal(ThemeManager.Colors.AccentQuaternary, closeButtonSeparator.Color);
        }

        /// <summary>
        /// Ensures dragging the title bar moves the dialog panel.
        /// </summary>
        [Fact]
        public void HandleHeaderCursor_WhenDragged_MovesPanelPosition() {
            ReparentEntityDialog dialog = new ReparentEntityDialog(CreateFont());
            EditorEntity root = new EditorEntity {
                Name = "Root"
            };
            EditorEntity child = new EditorEntity {
                Name = "Child"
            };
            root.AddChild(child);

            dialog.Show(child, new Entity[] { root, child });
            dialog.UpdateLayout(1280, 720);

            EditorEntity panelRoot = GetPrivateField<EditorEntity>(dialog, "PanelRoot");
            float3 initialPosition = panelRoot.Position;

            InvokePrivate(dialog, "HandleHeaderCursor", new int2(16, 16), new int2(0, 0), PointerInteraction.Press);
            InvokePrivate(dialog, "HandleHeaderCursor", new int2(40, 24), new int2(24, 8), PointerInteraction.Hover);
            InvokePrivate(dialog, "HandleHeaderCursor", new int2(40, 24), new int2(0, 0), PointerInteraction.Release);

            Assert.Equal(initialPosition.X + 24, panelRoot.Position.X);
            Assert.Equal(initialPosition.Y + 8, panelRoot.Position.Y);
        }

        /// <summary>
        /// Invokes one non-public instance method with explicit arguments.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="arguments">Arguments forwarded to the method.</param>
        void InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = FindPrivateMethod(target.GetType(), methodName);
            method.Invoke(target, arguments);
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
        /// Creates a deterministic font asset for the reparent dialog layout.
        /// </summary>
        /// <returns>Font asset with the glyphs needed by the dialog labels and buttons.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['R'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
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
