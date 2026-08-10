using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-session integration for project environments.
    /// </summary>
    public sealed class EditorSessionEnvironmentsTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the session environment tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes a temporary content root and editor core host.
        /// </summary>
        public EditorSessionEnvironmentsTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-session-environments-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempProjectRootPath);
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempProjectRootPath)
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Deletes temporary test state.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the title-bar environment command opens the session-owned dialog with the project registry.
        /// </summary>
        [Fact]
        public void HandleEnvironmentsRequested_ShowsSessionEnvironmentDialog() {
            EditorSession session = CreateSession();
            EnvironmentsDialog dialog = GetPrivateField<EnvironmentsDialog>(session, "environmentsDialog");

            InvokePrivate(session, "HandleEnvironmentsRequested");

            Assert.True(dialog.Enabled);
            Assert.Contains(GetPrivateField<List<EnvironmentsDialogRow>>(dialog, "EnvironmentRows"), row => row.EnvironmentId == "debug");
        }

        /// <summary>
        /// Ensures a confirmed environment document is persisted and closes the session-owned dialog.
        /// </summary>
        [Fact]
        public void HandleEnvironmentsDialogConfirmed_PersistsDocumentAndHidesDialog() {
            EditorSession session = CreateSession();
            EnvironmentsDialog dialog = GetPrivateField<EnvironmentsDialog>(session, "environmentsDialog");
            EditorProjectEnvironmentsDocument document = new EditorProjectEnvironmentsService(TempProjectRootPath).Load();
            document.Environments.Add(new EditorProjectEnvironmentDefinition { Id = "QA", IsProtected = false });

            InvokePrivate(session, "HandleEnvironmentsDialogConfirmed", new EnvironmentsDialogSelection(document));

            EditorProjectEnvironmentsDocument reloaded = new EditorProjectEnvironmentsService(TempProjectRootPath).Load();
            Assert.Contains(reloaded.Environments, environment => environment.Id == "QA");
            Assert.False(dialog.Enabled);
        }

        /// <summary>
        /// Creates a minimally initialized session containing the environment collaborators under test.
        /// </summary>
        /// <returns>Editor session configured for environment command tests.</returns>
        EditorSession CreateSession() {
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            SetPrivateField(session, "projectPath", TempProjectRootPath);
            SetPrivateField(session, "projectEnvironmentsService", new EditorProjectEnvironmentsService(TempProjectRootPath));
            SetPrivateField(session, "environmentsDialog", new EnvironmentsDialog(CreateFont()));
            return session;
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
        /// Assigns one non-public instance field.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to assign.</param>
        /// <param name="value">Value to assign.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
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
        /// Creates a small font asset suitable for the dialog.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics.</returns>
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
