using System.Reflection;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the unsaved-changes confirmation dialog.
    /// </summary>
    public class UnsavedChangesDialogTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the dialog tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the dialog.
        /// </summary>
        public UnsavedChangesDialogTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-unsaved-changes-dialog-tests", Guid.NewGuid().ToString("N"));
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
        /// Ensures the unsaved-changes dialog raises the Save action.
        /// </summary>
        [Fact]
        public void HandleSaveClicked_RaisesSaveRequested() {
            UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());
            bool raised = false;
            dialog.SaveRequested += () => raised = true;
            dialog.Show();
            dialog.UpdateLayout(1280, 720);

            InvokePrivate(dialog, "HandleSaveClicked");

            Assert.True(raised);
        }

        /// <summary>
        /// Ensures the unsaved-changes dialog raises the Don't Save action.
        /// </summary>
        [Fact]
        public void HandleDontSaveClicked_RaisesDontSaveRequested() {
            UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());
            bool raised = false;
            dialog.DontSaveRequested += () => raised = true;
            dialog.Show();
            dialog.UpdateLayout(1280, 720);

            InvokePrivate(dialog, "HandleDontSaveClicked");

            Assert.True(raised);
        }

        /// <summary>
        /// Ensures the unsaved-changes dialog occupies the modal band above overlay menus.
        /// </summary>
        [Fact]
        public void Constructor_UsesModalBand() {
            UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());
            RoundedRectComponent panelBackground = GetPrivateField<RoundedRectComponent>(dialog, "PanelBackground");

            Assert.Equal(RenderOrder2D.ModalBackground, panelBackground.RenderOrder2D);
        }

        /// <summary>
        /// Ensures the dialog header uses a dedicated title-bar color instead of the panel fill color.
        /// </summary>
        [Fact]
        public void Constructor_UsesDistinctHeaderColor() {
            UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());
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
            UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());
            dialog.Show();
            dialog.UpdateLayout(1280, 720);

            EditorEntity headerRoot = GetPrivateField<EditorEntity>(dialog, "HeaderRoot");
            SpriteComponent headerBackground = GetPrivateField<SpriteComponent>(dialog, "HeaderBackground");

            Assert.Equal(0f, headerRoot.LocalPosition.X);
            Assert.Equal(0f, headerRoot.LocalPosition.Y);
            Assert.Equal(UnsavedChangesDialog.PanelWidth, headerBackground.Size.X);
            Assert.Equal(UnsavedChangesDialog.HeaderHeight, headerBackground.Size.Y);
        }

        /// <summary>
        /// Ensures the message block and footer buttons are anchored to the dialog shell and stay aligned after a resize.
        /// </summary>
        [Fact]
        public void UpdateLayout_AnchorsMessageAndFooterButtonsToResizablePanelEdges() {
            UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());
            dialog.Show();
            dialog.UpdateLayout(1280, 720);

            EditorEntity panelRoot = GetPrivateField<EditorEntity>(dialog, "PanelRoot");
            RoundedRectComponent panelBackground = GetPrivateField<RoundedRectComponent>(dialog, "PanelBackground");
            EditorEntity messageHost = GetPrivateField<EditorEntity>(dialog, "MessageHost");
            TextComponent messageText = GetPrivateField<TextComponent>(dialog, "MessageText");
            EditorEntity footerHost = GetPrivateField<EditorEntity>(dialog, "FooterHost");
            EditorEntity saveButtonHost = GetPrivateField<EditorEntity>(dialog, "SaveButtonHost");
            EditorEntity dontSaveButtonHost = GetPrivateField<EditorEntity>(dialog, "DontSaveButtonHost");
            EditorEntity cancelButtonHost = GetPrivateField<EditorEntity>(dialog, "CancelButtonHost");

            ButtonComponent saveButton = saveButtonHost.Components.OfType<ButtonComponent>().Single();
            ButtonComponent dontSaveButton = dontSaveButtonHost.Components.OfType<ButtonComponent>().Single();
            ButtonComponent cancelButton = cancelButtonHost.Components.OfType<ButtonComponent>().Single();

            int footerWidth = saveButton.Size.X + dontSaveButton.Size.X + cancelButton.Size.X + 16;
            int footerTop = UnsavedChangesDialog.PanelHeight - UnsavedChangesDialog.PanelPadding - UnsavedChangesDialog.FooterHeight;
            int saveButtonY = (UnsavedChangesDialog.FooterHeight - saveButton.Size.Y) / 2;
            int dontSaveButtonY = (UnsavedChangesDialog.FooterHeight - dontSaveButton.Size.Y) / 2;
            int cancelButtonY = (UnsavedChangesDialog.FooterHeight - cancelButton.Size.Y) / 2;
            int cancelButtonX = footerWidth - cancelButton.Size.X;
            int dontSaveButtonX = cancelButtonX - 8 - dontSaveButton.Size.X;
            int saveButtonX = dontSaveButtonX - 8 - saveButton.Size.X;

            Assert.Equal(UnsavedChangesDialog.PanelPadding, (int)Math.Round(messageHost.LocalPosition.X));
            Assert.Equal(UnsavedChangesDialog.PanelPadding + UnsavedChangesDialog.HeaderHeight + UnsavedChangesDialog.SectionSpacing, (int)Math.Round(messageHost.LocalPosition.Y));
            Assert.Equal(UnsavedChangesDialog.PanelWidth - UnsavedChangesDialog.PanelPadding - footerWidth, (int)Math.Round(footerHost.LocalPosition.X));
            Assert.Equal(footerTop, (int)Math.Round(footerHost.LocalPosition.Y));
            Assert.Equal(saveButtonX, (int)Math.Round(saveButtonHost.LocalPosition.X));
            Assert.Equal(dontSaveButtonX, (int)Math.Round(dontSaveButtonHost.LocalPosition.X));
            Assert.Equal(cancelButtonX, (int)Math.Round(cancelButtonHost.LocalPosition.X));
            Assert.Equal(saveButtonY, (int)Math.Round(saveButtonHost.LocalPosition.Y));
            Assert.Equal(dontSaveButtonY, (int)Math.Round(dontSaveButtonHost.LocalPosition.Y));
            Assert.Equal(cancelButtonY, (int)Math.Round(cancelButtonHost.LocalPosition.Y));
            Assert.Equal(UnsavedChangesDialog.PanelWidth - UnsavedChangesDialog.PanelPadding * 2, messageText.Size.X);

            EditorEntity topLeftGrip = Assert.IsType<EditorEntity>(Assert.Single(panelRoot.Children, child => string.Equals(((EditorEntity)child).Name, "ResizeTopLeftGrip", StringComparison.Ordinal)));
            InteractableComponent topLeftInteractable = topLeftGrip.Components.OfType<InteractableComponent>().Single();
            topLeftInteractable.OnCursor(new int2(8, 8), new int2(0, 0), PointerInteraction.Press);
            topLeftInteractable.OnCursor(new int2(8, 8), new int2(40, 24), PointerInteraction.Hover);
            topLeftInteractable.OnCursor(new int2(8, 8), new int2(0, 0), PointerInteraction.Release);
            dialog.UpdateLayout(1280, 720);

            int resizedFooterTop = panelBackground.Size.Y - UnsavedChangesDialog.PanelPadding - UnsavedChangesDialog.FooterHeight;

            Assert.Equal(UnsavedChangesDialog.PanelPadding, (int)Math.Round(messageHost.LocalPosition.X));
            Assert.Equal(UnsavedChangesDialog.PanelPadding + UnsavedChangesDialog.HeaderHeight + UnsavedChangesDialog.SectionSpacing, (int)Math.Round(messageHost.LocalPosition.Y));
            Assert.Equal(panelBackground.Size.X - UnsavedChangesDialog.PanelPadding - footerWidth, (int)Math.Round(footerHost.LocalPosition.X));
            Assert.Equal(resizedFooterTop, (int)Math.Round(footerHost.LocalPosition.Y));
            Assert.Equal(saveButtonX, (int)Math.Round(saveButtonHost.LocalPosition.X));
            Assert.Equal(dontSaveButtonX, (int)Math.Round(dontSaveButtonHost.LocalPosition.X));
            Assert.Equal(cancelButtonX, (int)Math.Round(cancelButtonHost.LocalPosition.X));
            Assert.Equal(saveButtonY, (int)Math.Round(saveButtonHost.LocalPosition.Y));
            Assert.Equal(dontSaveButtonY, (int)Math.Round(dontSaveButtonHost.LocalPosition.Y));
            Assert.Equal(cancelButtonY, (int)Math.Round(cancelButtonHost.LocalPosition.Y));
            Assert.Equal(panelBackground.Size.X - UnsavedChangesDialog.PanelPadding * 2, messageText.Size.X);
        }

        /// <summary>
        /// Ensures the close button fills the title-bar height and touches the right edge.
        /// </summary>
        [Fact]
        public void UpdateLayout_PositionsCloseButtonAsFullHeightRightEdgeChrome() {
            UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());
            dialog.Show();
            dialog.UpdateLayout(1280, 720);

            EditorEntity closeButtonHost = GetPrivateField<EditorEntity>(dialog, "CloseButtonHost");
            RoundedRectComponent closeButtonBackground = closeButtonHost.Components.OfType<RoundedRectComponent>().Single();
            Assert.Equal(UnsavedChangesDialog.PanelWidth - closeButtonBackground.Size.X, closeButtonHost.LocalPosition.X);
            Assert.Equal(0f, closeButtonHost.LocalPosition.Y);
            Assert.Equal(UnsavedChangesDialog.HeaderHeight, closeButtonBackground.Size.Y);
        }

        /// <summary>
        /// Ensures the shared dialog shell exposes resize grips in the three configured corners.
        /// </summary>
        [Fact]
        public void UpdateLayout_ExposesCornerResizeGripsByDefault() {
            UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());
            dialog.Show();
            dialog.UpdateLayout(1280, 720);

            EditorEntity panelRoot = GetPrivateField<EditorEntity>(dialog, "PanelRoot");
            RoundedRectComponent panelBackground = GetPrivateField<RoundedRectComponent>(dialog, "PanelBackground");

            EditorEntity topLeftGrip = Assert.IsType<EditorEntity>(Assert.Single(panelRoot.Children, child => string.Equals(((EditorEntity)child).Name, "ResizeTopLeftGrip", StringComparison.Ordinal)));
            EditorEntity bottomLeftGrip = Assert.IsType<EditorEntity>(Assert.Single(panelRoot.Children, child => string.Equals(((EditorEntity)child).Name, "ResizeBottomLeftGrip", StringComparison.Ordinal)));
            EditorEntity bottomRightGrip = Assert.IsType<EditorEntity>(Assert.Single(panelRoot.Children, child => string.Equals(((EditorEntity)child).Name, "ResizeBottomRightGrip", StringComparison.Ordinal)));

            InteractableComponent topLeftInteractable = topLeftGrip.Components.OfType<InteractableComponent>().Single();
            InteractableComponent bottomLeftInteractable = bottomLeftGrip.Components.OfType<InteractableComponent>().Single();
            InteractableComponent bottomRightInteractable = bottomRightGrip.Components.OfType<InteractableComponent>().Single();

            Assert.Equal(new int2(EditorDialogBase.ResizeGripSize, EditorDialogBase.ResizeGripSize), topLeftInteractable.Size);
            Assert.Equal(new int2(EditorDialogBase.ResizeGripSize, EditorDialogBase.ResizeGripSize), bottomLeftInteractable.Size);
            Assert.Equal(new int2(EditorDialogBase.ResizeGripSize, EditorDialogBase.ResizeGripSize), bottomRightInteractable.Size);
            Assert.Equal(PointerCursorKind.ResizeNorthWestSouthEast, topLeftInteractable.HoverCursor);
            Assert.Equal(PointerCursorKind.ResizeNorthEastSouthWest, bottomLeftInteractable.HoverCursor);
            Assert.Equal(PointerCursorKind.ResizeNorthWestSouthEast, bottomRightInteractable.HoverCursor);
            Assert.Equal(0f, topLeftGrip.LocalPosition.X);
            Assert.Equal(0f, topLeftGrip.LocalPosition.Y);
            Assert.Equal(0f, bottomLeftGrip.LocalPosition.X);
            Assert.Equal(panelBackground.Size.Y - EditorDialogBase.ResizeGripSize, bottomLeftGrip.LocalPosition.Y);
            Assert.Equal(panelBackground.Size.X - EditorDialogBase.ResizeGripSize, bottomRightGrip.LocalPosition.X);
            Assert.Equal(panelBackground.Size.Y - EditorDialogBase.ResizeGripSize, bottomRightGrip.LocalPosition.Y);
        }

        /// <summary>
        /// Ensures the close button owns the same left separator used by the editor window chrome.
        /// </summary>
        [Fact]
        public void Constructor_CreatesCloseButtonLeftSeparator() {
            UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());
            SpriteComponent closeButtonSeparator = GetPrivateField<SpriteComponent>(dialog, "CloseButtonSeparator");

            Assert.Equal(TextureUtils.PixelTexture, closeButtonSeparator.Texture);
            Assert.Equal(ThemeManager.Colors.AccentQuaternary, closeButtonSeparator.Color);
        }

        /// <summary>
        /// Ensures dragging the title bar moves the dialog panel.
        /// </summary>
        [Fact]
        public void HandleHeaderCursor_WhenDragged_MovesPanelPosition() {
            UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());
            dialog.Show();
            dialog.UpdateLayout(1280, 720);

            EditorEntity panelRoot = GetPrivateField<EditorEntity>(dialog, "PanelRoot");
            float3 initialPosition = panelRoot.Position;

            InvokePrivate(dialog, "HandleHeaderCursor", new int2(16, 16), new int2(0, 0), PointerInteraction.Press);
            InvokePrivate(dialog, "HandleHeaderCursor", new int2(36, 28), new int2(20, 12), PointerInteraction.Hover);
            InvokePrivate(dialog, "HandleHeaderCursor", new int2(36, 28), new int2(0, 0), PointerInteraction.Release);

            Assert.Equal(initialPosition.X + 20, panelRoot.Position.X);
            Assert.Equal(initialPosition.Y + 12, panelRoot.Position.Y);
        }

        /// <summary>
        /// Ensures dragging the top-left resize grip changes both the panel size and its origin.
        /// </summary>
        [Fact]
        public void HandleTopLeftResizeGrip_WhenDragged_ResizesAndRepositionsThePanel() {
            UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());
            dialog.Show();
            dialog.UpdateLayout(1280, 720);

            EditorEntity panelRoot = GetPrivateField<EditorEntity>(dialog, "PanelRoot");
            RoundedRectComponent panelBackground = GetPrivateField<RoundedRectComponent>(dialog, "PanelBackground");
            EditorEntity topLeftGrip = Assert.IsType<EditorEntity>(panelRoot.Children.Single(child => string.Equals(((EditorEntity)child).Name, "ResizeTopLeftGrip", StringComparison.Ordinal)));
            InteractableComponent topLeftInteractable = topLeftGrip.Components.OfType<InteractableComponent>().Single();

            float3 initialPosition = panelRoot.Position;
            int2 initialSize = panelBackground.Size;

            topLeftInteractable.OnCursor(new int2(8, 8), new int2(0, 0), PointerInteraction.Press);
            topLeftInteractable.OnCursor(new int2(8, 8), new int2(20, 12), PointerInteraction.Hover);
            topLeftInteractable.OnCursor(new int2(8, 8), new int2(0, 0), PointerInteraction.Release);

            Assert.Equal(initialPosition.X + 20, panelRoot.Position.X);
            Assert.Equal(initialPosition.Y + 12, panelRoot.Position.Y);
            Assert.Equal(initialSize.X - 20, panelBackground.Size.X);
            Assert.Equal(initialSize.Y - 12, panelBackground.Size.Y);
        }

        /// <summary>
        /// Ensures the unsaved-changes dialog cannot be resized below its designed starting size.
        /// </summary>
        [Fact]
        public void HandleTopLeftResizeGrip_WhenDraggedSmaller_ClampsToThePanelMinimumSize() {
            UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());
            dialog.Show();
            dialog.UpdateLayout(1280, 720);

            EditorEntity panelRoot = GetPrivateField<EditorEntity>(dialog, "PanelRoot");
            RoundedRectComponent panelBackground = GetPrivateField<RoundedRectComponent>(dialog, "PanelBackground");
            EditorEntity topLeftGrip = Assert.IsType<EditorEntity>(panelRoot.Children.Single(child => string.Equals(((EditorEntity)child).Name, "ResizeTopLeftGrip", StringComparison.Ordinal)));
            InteractableComponent topLeftInteractable = topLeftGrip.Components.OfType<InteractableComponent>().Single();

            topLeftInteractable.OnCursor(new int2(8, 8), new int2(0, 0), PointerInteraction.Press);
            topLeftInteractable.OnCursor(new int2(8, 8), new int2(-1000, -1000), PointerInteraction.Hover);
            topLeftInteractable.OnCursor(new int2(8, 8), new int2(0, 0), PointerInteraction.Release);

            Assert.Equal(UnsavedChangesDialog.PanelWidth, panelBackground.Size.X);
            Assert.Equal(UnsavedChangesDialog.PanelHeight, panelBackground.Size.Y);
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
        /// Creates a small font asset that can satisfy the layout requirements of the dialog.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['D'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['\''] = new FontChar(new float4(0f, 0f, 2f, 12f), 0f, 2f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['?'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f)
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
