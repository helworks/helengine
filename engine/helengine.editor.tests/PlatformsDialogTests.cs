using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the project-platform selection dialog behavior.
    /// </summary>
    public sealed class PlatformsDialogTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the dialog tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the dialog tests.
        /// </summary>
        public PlatformsDialogTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-platforms-dialog-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
            EditorInputCaptureService.Reset();

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary test state after each test.
        /// </summary>
        public void Dispose() {
            EditorInputCaptureService.Reset();
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the dialog renders enabled-platform checkboxes and an active-platform dropdown constrained to enabled entries.
        /// </summary>
        [Fact]
        public void Show_WhenOpened_PopulatesCheckboxesAndActivePlatformDropdown() {
            PlatformsDialog dialog = new PlatformsDialog(CreateFont());

            dialog.Show(
                new[] { "windows", "ps2", "linux" },
                new[] { "windows", "ps2" },
                "ps2");

            ComboBoxComponent activePlatformComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ActivePlatformComboBox");
            List<CheckBoxComponent> platformCheckBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "PlatformCheckBoxes");

            Assert.Equal("ps2", activePlatformComboBox.SelectedItem);
            Assert.Equal(2, activePlatformComboBox.Items.Count);
            Assert.Equal(3, platformCheckBoxes.Count);
        }

        /// <summary>
        /// Ensures saving is blocked when the active platform is no longer one of the enabled platforms.
        /// </summary>
        [Fact]
        public void HandleSaveClicked_WhenActivePlatformIsNotEnabled_LeavesDialogOpenAndShowsValidation() {
            PlatformsDialog dialog = new PlatformsDialog(CreateFont());
            dialog.Show(new[] { "windows", "ps2" }, new[] { "windows", "ps2" }, "ps2");

            List<CheckBoxComponent> checkBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "PlatformCheckBoxes");
            checkBoxes[1].IsChecked = false;

            InvokePrivate(dialog, "HandleSaveClicked");

            TextComponent statusText = GetPrivateField<TextComponent>(dialog, "StatusText");
            Assert.Contains("active platform", statusText.Text, StringComparison.OrdinalIgnoreCase);
            Assert.True(dialog.Enabled);
        }

        /// <summary>
        /// Ensures platform rows are parented under the modal content root and positioned immediately during Show.
        /// </summary>
        [Fact]
        public void Show_WhenOpened_ParentsPlatformRowsUnderDialogContentRootAndLaysThemOutImmediately() {
            PlatformsDialog dialog = new PlatformsDialog(CreateFont());

            dialog.Show(
                new[] { "windows", "ps2", "linux" },
                new[] { "windows", "ps2" },
                "ps2");

            EditorEntity dialogContentRoot = GetProtectedProperty<EditorEntity>(dialog, "DialogContentRoot");
            List<EditorEntity> platformCheckBoxHosts = GetPrivateField<List<EditorEntity>>(dialog, "PlatformCheckBoxHosts");
            List<EditorEntity> platformLabelHosts = GetPrivateField<List<EditorEntity>>(dialog, "PlatformLabelHosts");
            EditorEntity platformsLabelHost = GetPrivateField<EditorEntity>(dialog, "PlatformsLabelHost");
            EditorEntity activePlatformComboBoxHost = GetPrivateField<EditorEntity>(dialog, "ActivePlatformComboBoxHost");

            Assert.Equal(3, platformCheckBoxHosts.Count);
            Assert.Equal(3, platformLabelHosts.Count);
            Assert.Same(dialogContentRoot, platformsLabelHost.Parent);
            Assert.Same(dialogContentRoot, activePlatformComboBoxHost.Parent);
            Assert.All(platformCheckBoxHosts, host => Assert.Same(dialogContentRoot, host.Parent));
            Assert.All(platformLabelHosts, host => Assert.Same(dialogContentRoot, host.Parent));
            Assert.All(platformCheckBoxHosts, host => Assert.True(host.LocalPosition.Y > 0f));
            Assert.All(platformLabelHosts, host => Assert.True(host.LocalPosition.Y > 0f));
        }

        /// <summary>
        /// Ensures platform rows do not require a later UpdateLayout pass to leave origin coordinates.
        /// </summary>
        [Fact]
        public void Show_WhenOpened_DoesNotLeavePlatformRowsAtDefaultOriginUntilLaterLayout() {
            PlatformsDialog dialog = new PlatformsDialog(CreateFont());

            dialog.Show(
                new[] { "windows", "ps2" },
                new[] { "windows" },
                "windows");

            List<EditorEntity> platformCheckBoxHosts = GetPrivateField<List<EditorEntity>>(dialog, "PlatformCheckBoxHosts");
            List<EditorEntity> platformLabelHosts = GetPrivateField<List<EditorEntity>>(dialog, "PlatformLabelHosts");

            Assert.All(platformCheckBoxHosts, host => Assert.NotEqual(float3.Zero, host.LocalPosition));
            Assert.All(platformLabelHosts, host => Assert.NotEqual(float3.Zero, host.LocalPosition));
        }

        /// <summary>
        /// Ensures hiding the dialog unregisters dynamic platform rows instead of leaving orphaned checkbox and label entities in the main UI graph.
        /// </summary>
        [Fact]
        public void Hide_WhenClosed_UnregistersDynamicPlatformRowsWithoutLeavingTopLevelUiEntities() {
            PlatformsDialog dialog = new PlatformsDialog(CreateFont());

            dialog.Show(
                new[] { "windows", "ps2" },
                new[] { "windows", "ps2" },
                "windows");

            List<EditorEntity> platformCheckBoxHosts = new List<EditorEntity>(GetPrivateField<List<EditorEntity>>(dialog, "PlatformCheckBoxHosts"));
            List<EditorEntity> platformLabelHosts = new List<EditorEntity>(GetPrivateField<List<EditorEntity>>(dialog, "PlatformLabelHosts"));

            dialog.Hide();

            Assert.Empty(GetPrivateField<List<EditorEntity>>(dialog, "PlatformCheckBoxHosts"));
            Assert.Empty(GetPrivateField<List<EditorEntity>>(dialog, "PlatformLabelHosts"));
            Assert.All(platformCheckBoxHosts, host => Assert.DoesNotContain(host, Core.Instance.ObjectManager.Entities));
            Assert.All(platformLabelHosts, host => Assert.DoesNotContain(host, Core.Instance.ObjectManager.Entities));
            Assert.All(platformCheckBoxHosts, host => Assert.Empty(host.Components));
            Assert.All(platformLabelHosts, host => Assert.Empty(host.Components));
            Assert.All(platformCheckBoxHosts, host => Assert.DoesNotContain(Core.Instance.ObjectManager.Drawables2D, drawable => ReferenceEquals(drawable.Parent, host)));
            Assert.All(platformCheckBoxHosts, host => Assert.DoesNotContain(Core.Instance.ObjectManager.Interactables, interactable => ReferenceEquals(interactable.Parent, host)));
            Assert.All(platformLabelHosts, host => Assert.DoesNotContain(Core.Instance.ObjectManager.Drawables2D, drawable => ReferenceEquals(drawable.Parent, host)));
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Reads one inherited non-public or protected instance property and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected property type.</typeparam>
        /// <param name="target">Object that owns the property.</param>
        /// <param name="propertyName">Name of the property to read.</param>
        /// <returns>Property value cast to the requested type.</returns>
        T GetProtectedProperty<T>(object target, string propertyName) {
            Type currentType = target.GetType();

            while (currentType != null) {
                PropertyInfo property = currentType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (property != null) {
                    return Assert.IsType<T>(property.GetValue(target));
                }

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"Property '{propertyName}' was not found on type '{target.GetType().FullName}'.");
        }

        /// <summary>
        /// Invokes one non-public instance method.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        void InvokePrivate(object target, string methodName) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, Array.Empty<object>());
        }

        /// <summary>
        /// Creates a small font asset suitable for modal tests.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['f'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 11f, 12f), 0f, 11f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 11f, 12f), 0f, 11f, 0f, 0f),
                ['x'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
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
