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

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary project state after each test.
        /// </summary>
        public void Dispose() {
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
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
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
