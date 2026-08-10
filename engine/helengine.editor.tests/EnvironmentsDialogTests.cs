using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the project-environment registry dialog behavior.
    /// </summary>
    public sealed class EnvironmentsDialogTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the dialog tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the dialog.
        /// </summary>
        public EnvironmentsDialogTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-environments-dialog-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
            EditorInputCaptureService.Reset();

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
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
        /// Ensures opening the dialog displays the protected built-in environments.
        /// </summary>
        [Fact]
        public void Show_WhenOpened_PopulatesProtectedEnvironmentRows() {
            EnvironmentsDialog dialog = new EnvironmentsDialog(CreateFont());

            dialog.Show(new EditorProjectEnvironmentsDocument {
                Environments = [
                    new EditorProjectEnvironmentDefinition { Id = "debug", IsProtected = true },
                    new EditorProjectEnvironmentDefinition { Id = "release", IsProtected = true },
                    new EditorProjectEnvironmentDefinition { Id = "QA", IsProtected = false }
                ]
            });

            List<EnvironmentsDialogRow> rows = GetPrivateField<List<EnvironmentsDialogRow>>(dialog, "EnvironmentRows");

            Assert.Equal(3, rows.Count(row => row.EnvironmentIndex >= 0));
            Assert.True(rows.Single(row => row.EnvironmentId == "debug").IsProtected);
            Assert.True(rows.Single(row => row.EnvironmentId == "release").IsProtected);
            Assert.False(rows.Single(row => row.EnvironmentId == "QA").IsProtected);
        }

        /// <summary>
        /// Ensures adding a custom environment updates the working dialog document.
        /// </summary>
        [Fact]
        public void HandleAddClicked_WhenIdIsValid_AddsCustomEnvironment() {
            EnvironmentsDialog dialog = new EnvironmentsDialog(CreateFont());
            dialog.Show(new EditorProjectEnvironmentsService(Path.Combine(TempRootPath, "project")).Load());
            TextBoxComponent idTextBox = GetPrivateField<TextBoxComponent>(dialog, "EnvironmentIdTextBox");
            idTextBox.Text = "QA";

            InvokePrivate(dialog, "HandleAddClicked");

            List<EnvironmentsDialogRow> rows = GetPrivateField<List<EnvironmentsDialogRow>>(dialog, "EnvironmentRows");
            Assert.Contains(rows, row => row.EnvironmentId == "QA");
            Assert.True(dialog.Enabled);
        }

        /// <summary>
        /// Ensures protected built-ins cannot be renamed through the dialog controls.
        /// </summary>
        [Fact]
        public void HandleRenameClicked_WhenSelectedEnvironmentIsProtected_ShowsValidation() {
            EnvironmentsDialog dialog = new EnvironmentsDialog(CreateFont());
            dialog.Show(new EditorProjectEnvironmentsService(Path.Combine(TempRootPath, "project")).Load());
            EnvironmentsDialogRow debugRow = FindRow(dialog, "debug");
            InvokePrivate(dialog, "HandleEnvironmentRowClicked", debugRow.SelectButton);
            TextBoxComponent idTextBox = GetPrivateField<TextBoxComponent>(dialog, "EnvironmentIdTextBox");
            idTextBox.Text = "development";

            InvokePrivate(dialog, "HandleRenameClicked");

            TextComponent statusText = GetPrivateField<TextComponent>(dialog, "StatusText");
            Assert.Contains("protected", statusText.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(GetPrivateField<List<EnvironmentsDialogRow>>(dialog, "EnvironmentRows"), row => row.EnvironmentId == "debug");
        }

        /// <summary>
        /// Finds the row currently bound to one environment identifier.
        /// </summary>
        /// <param name="dialog">Dialog under test.</param>
        /// <param name="environmentId">Identifier to locate.</param>
        /// <returns>Matching environment row.</returns>
        EnvironmentsDialogRow FindRow(EnvironmentsDialog dialog, string environmentId) {
            return GetPrivateField<List<EnvironmentsDialogRow>>(dialog, "EnvironmentRows")
                .Single(row => string.Equals(row.EnvironmentId, environmentId, StringComparison.OrdinalIgnoreCase));
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
        /// <param name="args">Arguments passed to the method.</param>
        void InvokePrivate(object target, string methodName, params object[] args) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, args);
        }

        /// <summary>
        /// Creates a small font asset suitable for modal tests.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            foreach (char character in "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz .") {
                characters[character] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f);
            }

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
