using System.Reflection;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the Project Settings dialog shows the stored identity, validates the name and reports edits.
    /// </summary>
    public class ProjectSettingsDialogTests {
        /// <summary>
        /// Ensures Show copies the name and description into the fields.
        /// </summary>
        [Fact]
        public void Show_PopulatesNameAndDescriptionFields() {
            ProjectSettingsDialog dialog = CreateDialog();

            dialog.Show(new EditorProjectSettings { Name = "city", Description = "twelve consoles" });

            Assert.Equal("city", GetNonPublicField<TextBoxComponent>(dialog, "NameField").Text);
            Assert.Equal("twelve consoles", GetNonPublicField<TextBoxComponent>(dialog, "DescriptionField").Text);
        }

        /// <summary>
        /// Ensures Save with a blank name shows a message and raises nothing.
        /// </summary>
        [Fact]
        public void Save_WhenNameIsBlank_ShowsStatusAndDoesNotConfirm() {
            ProjectSettingsDialog dialog = CreateDialog();
            int confirmCount = 0;
            dialog.ConfirmRequested += settings => confirmCount++;
            dialog.Show(new EditorProjectSettings { Name = "city", Description = string.Empty });
            GetNonPublicField<TextBoxComponent>(dialog, "NameField").Text = "   ";

            InvokeNonPublicMethod(dialog, "HandleSaveClicked");

            Assert.Equal(0, confirmCount);
            Assert.Equal("Project name must be provided.", GetNonPublicField<TextComponent>(dialog, "StatusText").Text);
        }

        /// <summary>
        /// Ensures Save raises the confirm event with trimmed values and clears the status line.
        /// </summary>
        [Fact]
        public void Save_WhenNameIsPresent_ConfirmsTrimmedSettings() {
            ProjectSettingsDialog dialog = CreateDialog();
            EditorProjectSettings confirmed = null;
            dialog.ConfirmRequested += settings => confirmed = settings;
            dialog.Show(new EditorProjectSettings { Name = "city", Description = string.Empty });
            GetNonPublicField<TextBoxComponent>(dialog, "NameField").Text = "  Demo Disc ";
            GetNonPublicField<TextBoxComponent>(dialog, "DescriptionField").Text = " one disc ";

            InvokeNonPublicMethod(dialog, "HandleSaveClicked");

            Assert.NotNull(confirmed);
            Assert.Equal("Demo Disc", confirmed.Name);
            Assert.Equal("one disc", confirmed.Description);
            Assert.Equal(string.Empty, GetNonPublicField<TextComponent>(dialog, "StatusText").Text);
        }

        /// <summary>
        /// Ensures the header close request behaves as Cancel.
        /// </summary>
        [Fact]
        public void CloseRequested_RaisesCancel() {
            ProjectSettingsDialog dialog = CreateDialog();
            int cancelCount = 0;
            dialog.CancelRequested += () => cancelCount++;

            InvokeNonPublicMethod(dialog, "OnCloseRequested");

            Assert.Equal(1, cancelCount);
        }

        ProjectSettingsDialog CreateDialog() {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            return new ProjectSettingsDialog(Core.Instance, new EditorSessionInteractionServices(), CreateFont(), EditorUiMetrics.Default);
        }

        static T GetNonPublicField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return Assert.IsType<T>(field.GetValue(target));
        }

        static void InvokeNonPublicMethod(object target, string methodName) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(target, null);
        }

        static FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            foreach (char character in "NameDescriptionSaveCancelProjectSettings ") {
                characters[character] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f);
            }

            return new FontAsset(
                new FontInfo("Dialog", 14, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                14f,
                64,
                64);
        }
    }
}
