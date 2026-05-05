using System.Reflection;
using helengine.editor;
using helengine.editor.tests.testing;
using helengine.platforms;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the build-settings dialog platform-selection behavior.
    /// </summary>
    public class BuildSettingsDialogTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the dialog tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the dialog tests.
        /// </summary>
        public BuildSettingsDialogTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-build-settings-dialog-tests", Guid.NewGuid().ToString("N"));
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
        /// Ensures one checkbox row is created for each available platform.
        /// </summary>
        [Fact]
        public void Show_WhenAvailablePlatformsProvided_CreatesOneCheckboxRowPerPlatform() {
            BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont());

            dialog.Show(
                CreateAvailablePlatforms("windows", "linux", "android"),
                new List<string> {
                    "windows"
                });

            List<CheckBoxComponent> platformCheckBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "PlatformCheckBoxes");
            List<TextComponent> platformLabels = GetPrivateField<List<TextComponent>>(dialog, "PlatformLabelTexts");

            Assert.Equal(3, platformCheckBoxes.Count);
            Assert.Equal(3, platformLabels.Count);
            Assert.Collection(
                platformLabels,
                label => Assert.Equal("Windows", label.Text),
                label => Assert.Equal("Linux", label.Text),
                label => Assert.Equal("Android", label.Text));
        }

        /// <summary>
        /// Ensures each platform row renders a dedicated status column instead of embedding installation state in the platform name.
        /// </summary>
        [Fact]
        public void Show_WhenAvailablePlatformsIncludeMissingEntries_RendersDedicatedStatusColumn() {
            BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont());

            dialog.Show(
                new List<AvailablePlatformDescriptor> {
                    new AvailablePlatformDescriptor("windows", "Windows", isInstalled: true),
                    new AvailablePlatformDescriptor("linux", "Linux", isInstalled: false),
                    new AvailablePlatformDescriptor("android", "Android", isInstalled: true)
                },
                new List<string> {
                    "windows"
                });

            List<TextComponent> platformLabels = GetPrivateField<List<TextComponent>>(dialog, "PlatformLabelTexts");
            List<TextComponent> platformStatuses = GetPrivateField<List<TextComponent>>(dialog, "PlatformStatusTexts");

            Assert.Equal(3, platformLabels.Count);
            Assert.Equal(3, platformStatuses.Count);
            Assert.Equal("Windows", platformLabels[0].Text);
            Assert.Equal("Linux", platformLabels[1].Text);
            Assert.Equal("Android", platformLabels[2].Text);
            Assert.Equal("INSTALLED", platformStatuses[0].Text);
            Assert.Equal("MISSING", platformStatuses[1].Text);
            Assert.Equal("INSTALLED", platformStatuses[2].Text);
        }

        /// <summary>
        /// Ensures the initial checked state matches the currently supported platforms.
        /// </summary>
        [Fact]
        public void Show_WhenSupportedPlatformsProvided_ChecksMatchingPlatformRows() {
            BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont());

            dialog.Show(
                CreateAvailablePlatforms("windows", "linux", "android"),
                new List<string> {
                    "linux",
                    "android"
                });

            List<CheckBoxComponent> platformCheckBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "PlatformCheckBoxes");

            Assert.False(platformCheckBoxes[0].IsChecked);
            Assert.True(platformCheckBoxes[1].IsChecked);
            Assert.True(platformCheckBoxes[2].IsChecked);
        }

        /// <summary>
        /// Ensures dialog-owned row hosts stay internal so they never leak into the scene hierarchy.
        /// </summary>
        [Fact]
        public void Show_WhenPlatformRowsAreCreated_MarksDialogOwnedEntitiesAsInternal() {
            BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont());

            dialog.Show(
                CreateAvailablePlatforms("windows", "linux"),
                new List<string> {
                    "windows"
                });

            EditorEntity panelRoot = GetPrivateField<EditorEntity>(dialog, "PanelRoot");
            EditorEntity titleHost = GetPrivateField<EditorEntity>(dialog, "TitleHost");
            EditorEntity closeButtonHost = GetPrivateField<EditorEntity>(dialog, "CloseButtonHost");
            EditorEntity statusHost = GetPrivateField<EditorEntity>(dialog, "StatusHost");
            EditorEntity cancelButtonHost = GetPrivateField<EditorEntity>(dialog, "CancelButtonHost");
            EditorEntity saveButtonHost = GetPrivateField<EditorEntity>(dialog, "SaveButtonHost");
            List<EditorEntity> platformHeaderHosts = GetPrivateField<List<EditorEntity>>(dialog, "PlatformHeaderHosts");
            List<EditorEntity> platformLabelHosts = GetPrivateField<List<EditorEntity>>(dialog, "PlatformLabelHosts");
            List<EditorEntity> platformStatusHosts = GetPrivateField<List<EditorEntity>>(dialog, "PlatformStatusHosts");
            List<EditorEntity> platformCheckBoxHosts = GetPrivateField<List<EditorEntity>>(dialog, "PlatformCheckBoxHosts");

            Assert.True(panelRoot.InternalEntity);
            Assert.True(titleHost.InternalEntity);
            Assert.True(closeButtonHost.InternalEntity);
            Assert.True(statusHost.InternalEntity);
            Assert.True(cancelButtonHost.InternalEntity);
            Assert.True(saveButtonHost.InternalEntity);
            Assert.All(platformHeaderHosts, host => Assert.True(host.InternalEntity));
            Assert.All(platformLabelHosts, host => Assert.True(host.InternalEntity));
            Assert.All(platformStatusHosts, host => Assert.True(host.InternalEntity));
            Assert.All(platformCheckBoxHosts, host => Assert.True(host.InternalEntity));
        }

        /// <summary>
        /// Ensures the dialog exposes a header close action that raises cancel.
        /// </summary>
        [Fact]
        public void HandleCloseClicked_RaisesCancelRequested() {
            BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont());
            bool raised = false;
            dialog.CancelRequested += () => raised = true;
            dialog.Show(
                CreateAvailablePlatforms("windows"),
                new List<string> {
                    "windows"
                });
            dialog.UpdateLayout(1280, 720);

            InvokePrivate(dialog, "HandleCloseClicked");

            Assert.True(raised);
        }

        /// <summary>
        /// Ensures modal checkboxes render above the modal panel instead of using panel-surface render orders.
        /// </summary>
        [Fact]
        public void Show_WhenPlatformRowsAreCreated_UsesModalRenderOrdersForCheckBoxes() {
            BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont());

            dialog.Show(
                CreateAvailablePlatforms("windows"),
                new List<string> {
                    "windows"
                });

            List<CheckBoxComponent> platformCheckBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "PlatformCheckBoxes");
            RoundedRectComponent background = GetPrivateField<RoundedRectComponent>(platformCheckBoxes[0], "Background");
            TextComponent checkMark = GetPrivateField<TextComponent>(platformCheckBoxes[0], "CheckMark");

            Assert.Equal(RenderOrder2D.ModalForeground, background.RenderOrder2D);
            Assert.Equal(RenderOrder2D.ModalForeground, checkMark.RenderOrder2D);
        }

        /// <summary>
        /// Ensures the header uses a dedicated title-bar color instead of the panel fill color.
        /// </summary>
        [Fact]
        public void Constructor_UsesDistinctHeaderColor() {
            BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont());
            RoundedRectComponent panelBackground = GetPrivateField<RoundedRectComponent>(dialog, "PanelBackground");
            SpriteComponent headerBackground = GetPrivateField<SpriteComponent>(dialog, "HeaderBackground");

            Assert.Equal(ThemeManager.Colors.AccentSecondary, headerBackground.Color);
            Assert.NotEqual(panelBackground.FillColor, headerBackground.Color);
        }

        /// <summary>
        /// Ensures scaled metrics resize the shared dialog shell immediately during construction.
        /// </summary>
        [Fact]
        public void Constructor_WithScaledMetrics_UsesScaledHeaderAndPanelSize() {
            BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont(), new EditorUiMetrics(1.5));
            RoundedRectComponent panelBackground = GetPrivateField<RoundedRectComponent>(dialog, "PanelBackground");
            SpriteComponent headerBackground = GetPrivateField<SpriteComponent>(dialog, "HeaderBackground");

            Assert.Equal(new int2(630, 354), panelBackground.Size);
            Assert.Equal(48, headerBackground.Size.Y);
        }

        /// <summary>
        /// Ensures the title bar touches the panel borders instead of using inset chrome.
        /// </summary>
        [Fact]
        public void UpdateLayout_PositionsHeaderFlushToPanelEdges() {
            BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont());
            dialog.Show(
                CreateAvailablePlatforms("windows"),
                new List<string> {
                    "windows"
                });
            dialog.UpdateLayout(1280, 720);

            EditorEntity headerRoot = GetPrivateField<EditorEntity>(dialog, "HeaderRoot");
            SpriteComponent headerBackground = GetPrivateField<SpriteComponent>(dialog, "HeaderBackground");

            Assert.Equal(0f, headerRoot.LocalPosition.X);
            Assert.Equal(0f, headerRoot.LocalPosition.Y);
            Assert.Equal(BuildSettingsDialog.PanelWidth, headerBackground.Size.X);
            Assert.Equal(BuildSettingsDialog.HeaderHeight, headerBackground.Size.Y);
        }

        /// <summary>
        /// Ensures the close button fills the title-bar height and touches the right edge.
        /// </summary>
        [Fact]
        public void UpdateLayout_PositionsCloseButtonAsFullHeightRightEdgeChrome() {
            BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont());
            dialog.Show(
                CreateAvailablePlatforms("windows"),
                new List<string> {
                    "windows"
                });
            dialog.UpdateLayout(1280, 720);

            EditorEntity closeButtonHost = GetPrivateField<EditorEntity>(dialog, "CloseButtonHost");
            RoundedRectComponent closeButtonBackground = closeButtonHost.Components.OfType<RoundedRectComponent>().Single();
            Assert.Equal(BuildSettingsDialog.PanelWidth - closeButtonBackground.Size.X, closeButtonHost.LocalPosition.X);
            Assert.Equal(0f, closeButtonHost.LocalPosition.Y);
            Assert.Equal(BuildSettingsDialog.HeaderHeight, closeButtonBackground.Size.Y);
        }

        /// <summary>
        /// Ensures scaled metrics reposition footer chrome using scaled panel, padding, and button sizes.
        /// </summary>
        [Fact]
        public void UpdateLayout_WithScaledMetrics_UsesScaledDialogChrome() {
            BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont(), new EditorUiMetrics(1.5));
            dialog.Show(
                CreateAvailablePlatforms("windows"),
                new List<string> {
                    "windows"
                });
            dialog.UpdateLayout(1280, 720);

            EditorEntity cancelButtonHost = GetPrivateField<EditorEntity>(dialog, "CancelButtonHost");
            EditorEntity saveButtonHost = GetPrivateField<EditorEntity>(dialog, "SaveButtonHost");
            ButtonComponent cancelButton = cancelButtonHost.Components.OfType<ButtonComponent>().Single();
            ButtonComponent saveButton = saveButtonHost.Components.OfType<ButtonComponent>().Single();

            Assert.Equal(new int2(132, 33), cancelButton.Size);
            Assert.Equal(new int2(132, 33), saveButton.Size);
            Assert.Equal(327f, cancelButtonHost.LocalPosition.X);
            Assert.Equal(474f, saveButtonHost.LocalPosition.X);
            Assert.Equal(292f, cancelButtonHost.LocalPosition.Y);
            Assert.Equal(292f, saveButtonHost.LocalPosition.Y);
        }

        /// <summary>
        /// Ensures the close button owns the same left separator used by the editor window chrome.
        /// </summary>
        [Fact]
        public void Constructor_CreatesCloseButtonLeftSeparator() {
            BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont());
            SpriteComponent closeButtonSeparator = GetPrivateField<SpriteComponent>(dialog, "CloseButtonSeparator");

            Assert.Equal(TextureUtils.PixelTexture, closeButtonSeparator.Texture);
            Assert.Equal(ThemeManager.Colors.AccentQuaternary, closeButtonSeparator.Color);
        }

        /// <summary>
        /// Ensures dragging the title bar moves the dialog panel.
        /// </summary>
        [Fact]
        public void HandleHeaderCursor_WhenDragged_MovesPanelPosition() {
            BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont());
            dialog.Show(
                CreateAvailablePlatforms("windows"),
                new List<string> {
                    "windows"
                });
            dialog.UpdateLayout(1280, 720);

            EditorEntity panelRoot = GetPrivateField<EditorEntity>(dialog, "PanelRoot");
            float3 initialPosition = panelRoot.Position;

            InvokePrivate(dialog, "HandleHeaderCursor", new int2(16, 16), new int2(0, 0), PointerInteraction.Press);
            InvokePrivate(dialog, "HandleHeaderCursor", new int2(32, 28), new int2(24, 12), PointerInteraction.Hover);
            InvokePrivate(dialog, "HandleHeaderCursor", new int2(32, 28), new int2(0, 0), PointerInteraction.Release);

            Assert.Equal(initialPosition.X + 24, panelRoot.Position.X);
            Assert.Equal(initialPosition.Y + 12, panelRoot.Position.Y);
        }

        /// <summary>
        /// Ensures the footer and status rows stay attached to the dialog shell when it is resized.
        /// </summary>
        [Fact]
        public void HandleBottomRightResizeGrip_WhenDragged_RepositionsFooterAndStatusWithTheShell() {
            BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont());
            dialog.Show(
                CreateAvailablePlatforms("windows"),
                new List<string> {
                    "windows"
                });
            dialog.UpdateLayout(1280, 720);

            EditorEntity panelRoot = GetPrivateField<EditorEntity>(dialog, "PanelRoot");
            RoundedRectComponent panelBackground = GetPrivateField<RoundedRectComponent>(dialog, "PanelBackground");
            EditorEntity statusHost = GetPrivateField<EditorEntity>(dialog, "StatusHost");
            EditorEntity cancelButtonHost = GetPrivateField<EditorEntity>(dialog, "CancelButtonHost");
            EditorEntity saveButtonHost = GetPrivateField<EditorEntity>(dialog, "SaveButtonHost");
            ButtonComponent cancelButton = cancelButtonHost.Components.OfType<ButtonComponent>().Single();
            ButtonComponent saveButton = saveButtonHost.Components.OfType<ButtonComponent>().Single();
            EditorEntity bottomRightGrip = Assert.IsType<EditorEntity>(panelRoot.Children.Single(child => string.Equals(((EditorEntity)child).Name, "ResizeBottomRightGrip", StringComparison.Ordinal)));
            InteractableComponent bottomRightInteractable = bottomRightGrip.Components.OfType<InteractableComponent>().Single();
            int2 initialSize = panelBackground.Size;

            bottomRightInteractable.OnCursor(new int2(panelBackground.Size.X - 8, panelBackground.Size.Y - 8), new int2(0, 0), PointerInteraction.Press);
            bottomRightInteractable.OnCursor(new int2(panelBackground.Size.X - 8, panelBackground.Size.Y - 8), new int2(48, 32), PointerInteraction.Hover);
            bottomRightInteractable.OnCursor(new int2(panelBackground.Size.X - 8, panelBackground.Size.Y - 8), new int2(0, 0), PointerInteraction.Release);
            dialog.UpdateLayout(1280, 720);

            Assert.Equal(initialSize.X + 48, panelBackground.Size.X);
            Assert.Equal(initialSize.Y + 32, panelBackground.Size.Y);
            int expectedFooterTop = panelBackground.Size.Y - BuildSettingsDialog.PanelPadding - BuildSettingsDialog.FooterHeight;
            int expectedButtonY = expectedFooterTop + Math.Max(0, (BuildSettingsDialog.FooterHeight - saveButton.Size.Y) / 2);
            int expectedCancelX = panelBackground.Size.X - BuildSettingsDialog.PanelPadding - saveButton.Size.X - BuildSettingsDialog.SectionSpacing - cancelButton.Size.X;
            int expectedSaveX = panelBackground.Size.X - BuildSettingsDialog.PanelPadding - saveButton.Size.X;
            int expectedStatusY = panelBackground.Size.Y - BuildSettingsDialog.FooterHeight - 38;

            Assert.Equal(expectedStatusY, (int)Math.Round(statusHost.LocalPosition.Y));
            Assert.Equal(expectedCancelX, (int)Math.Round(cancelButtonHost.LocalPosition.X));
            Assert.Equal(expectedSaveX, (int)Math.Round(saveButtonHost.LocalPosition.X));
            Assert.Equal(expectedButtonY, (int)Math.Round(cancelButtonHost.LocalPosition.Y));
            Assert.Equal(expectedButtonY, (int)Math.Round(saveButtonHost.LocalPosition.Y));
        }

        /// <summary>
        /// Ensures the dialog rejects confirmation when every platform is unchecked.
        /// </summary>
        [Fact]
        public void HandleSaveClicked_WhenNoPlatformsRemainSelected_ShowsValidationErrorAndDoesNotRaiseConfirm() {
            BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont());
            bool raised = false;
            dialog.ConfirmRequested += selection => raised = true;

            dialog.Show(
                CreateAvailablePlatforms("windows", "linux"),
                new List<string> {
                    "windows"
                });

            List<CheckBoxComponent> platformCheckBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "PlatformCheckBoxes");
            TextComponent statusText = GetPrivateField<TextComponent>(dialog, "StatusText");

            platformCheckBoxes[0].IsChecked = false;
            InvokePrivate(dialog, "HandleSaveClicked");

            Assert.False(raised);
            Assert.Equal("Select at least one platform.", statusText.Text);
        }

        /// <summary>
        /// Ensures confirmation returns the selected platform ids in the same order as the available rows.
        /// </summary>
        [Fact]
        public void HandleSaveClicked_WhenPlatformsAreSelected_RaisesConfirmWithStablePlatformOrder() {
            BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont());
            BuildSettingsSelection raisedSelection = null;
            dialog.ConfirmRequested += selection => raisedSelection = selection;

            dialog.Show(
                CreateAvailablePlatforms("windows", "linux", "android"),
                new List<string> {
                    "android"
                });

            List<CheckBoxComponent> platformCheckBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "PlatformCheckBoxes");

            platformCheckBoxes[0].IsChecked = true;
            InvokePrivate(dialog, "HandleSaveClicked");

            Assert.NotNull(raisedSelection);
            Assert.Equal(
                new[] {
                    "windows",
                    "android"
                },
                raisedSelection.SelectedPlatformIds);
        }

        /// <summary>
        /// Creates one available-platform list from the provided ids.
        /// </summary>
        /// <param name="platformIds">Stable platform ids to expose in the dialog.</param>
        /// <returns>Available platform descriptors with readable display names.</returns>
        IReadOnlyList<AvailablePlatformDescriptor> CreateAvailablePlatforms(params string[] platformIds) {
            List<AvailablePlatformDescriptor> platforms = new List<AvailablePlatformDescriptor>(platformIds.Length);

            for (int index = 0; index < platformIds.Length; index++) {
                string platformId = platformIds[index];
                string displayName = platformId switch {
                    "windows" => "Windows",
                    "linux" => "Linux",
                    "android" => "Android",
                    _ => platformId
                };

                platforms.Add(new AvailablePlatformDescriptor(platformId, displayName));
            }

            return platforms;
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
            return Assert.IsType<T>(field.GetValue(target));
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
        /// Invokes one non-public instance method with parameters.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="arguments">Arguments passed to the invoked method.</param>
        void InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = FindPrivateMethod(target.GetType(), methodName);
            method.Invoke(target, arguments);
        }

        /// <summary>
        /// Finds one inherited non-public instance field by walking the type hierarchy.
        /// </summary>
        /// <param name="type">Type that starts the field lookup.</param>
        /// <param name="fieldName">Name of the field to locate.</param>
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
        /// <param name="methodName">Name of the method to locate.</param>
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
        /// Creates a small font asset that can satisfy the dialog layout requirements.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current tests.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['L'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['W'] = new FontChar(new float4(0f, 0f, 11f, 12f), 0f, 11f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['x'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['.'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
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
