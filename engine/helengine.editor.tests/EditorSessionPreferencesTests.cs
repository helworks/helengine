using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor-session preference wiring for global UI scale changes.
    /// </summary>
    public class EditorSessionPreferencesTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the editor-session preferences tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the preferences dialog shell used in these tests.
        /// </summary>
        public EditorSessionPreferencesTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-session-preferences-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary test state after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures confirming the preferences dialog raises the session-level UI scale settings event.
        /// </summary>
        [Fact]
        public void HandlePreferencesDialogConfirmed_WhenInvoked_RaisesUiScaleSettingsChanged() {
            EditorSession session = CreateSessionForPreferences();
            EditorUiScaleSettings raisedSettings = null;
            session.UiScaleSettingsChanged += settings => raisedSettings = settings;

            InvokePrivate(session, "HandlePreferencesDialogConfirmed", new EditorUiScaleSettings(EditorUiScaleMode.Override, 175));

            Assert.NotNull(raisedSettings);
            Assert.Equal(EditorUiScaleMode.Override, raisedSettings.Mode);
            Assert.Equal(175, raisedSettings.OverridePercent);
        }

        /// <summary>
        /// Ensures applying one new UI scale updates the reusable title-bar and preferences-dialog chrome in place.
        /// </summary>
        [Fact]
        public void ApplyUiScale_WhenCalled_UpdatesScaledTitleBarAndPreferencesDialogChrome() {
            EditorUiMetrics initialMetrics = new EditorUiMetrics(1d);
            EditorUiMetrics scaledMetrics = new EditorUiMetrics(1.5d);
            EditorSession session = CreateSessionForPreferences(initialMetrics);

            InvokePrivate(
                session,
                "ApplyUiScale",
                new EditorUiScaleSettings(EditorUiScaleMode.Override, 150),
                scaledMetrics,
                CreateFont(18f),
                CreateFont(23f));

            EditorTitleBar titleBar = GetPrivateField<EditorTitleBar>(session, "titleBar");
            EditorPreferencesDialog preferencesDialog = GetPrivateField<EditorPreferencesDialog>(session, "preferencesDialog");

            Assert.Equal(54, titleBar.Height);
            Assert.Equal(new int2(540, 330), GetDialogMinimumSize(preferencesDialog));
        }

        /// <summary>
        /// Creates one partially initialized editor session containing only the collaborators used by the preferences flow.
        /// </summary>
        /// <returns>Editor session configured for isolated preferences tests.</returns>
        EditorSession CreateSessionForPreferences() {
            return CreateSessionForPreferences(EditorUiMetrics.Default);
        }

        /// <summary>
        /// Creates one partially initialized editor session containing only the collaborators used by the preferences flow.
        /// </summary>
        /// <param name="metrics">Scaled editor UI metrics used to construct the title bar and preferences dialog.</param>
        /// <returns>Editor session configured for isolated preferences tests.</returns>
        EditorSession CreateSessionForPreferences(EditorUiMetrics metrics) {
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            SetPrivateField(session, "titleBar", new EditorTitleBar(CreateFont(), metrics, 1280, 720, "Hel"));
            SetPrivateField(session, "preferencesDialog", new EditorPreferencesDialog(CreateFont(), metrics));
            SetPrivateField(session, "CurrentUiScaleSettings", new EditorUiScaleSettings(EditorUiScaleMode.Auto, 100));
            return session;
        }

        /// <summary>
        /// Invokes one non-public instance method with a single argument.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="argument">Argument passed to the invoked method.</param>
        void InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            method.Invoke(target, arguments);
        }

        /// <summary>
        /// Assigns one non-public instance field to the provided value.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to assign.</param>
        /// <param name="value">Value assigned to the field.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
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
        /// Reads the protected dialog minimum-size property from one dialog instance.
        /// </summary>
        /// <param name="dialog">Dialog whose minimum size should be inspected.</param>
        /// <returns>Current scaled dialog minimum size.</returns>
        int2 GetDialogMinimumSize(EditorDialogBase dialog) {
            PropertyInfo property = typeof(EditorDialogBase).GetProperty("DialogMinimumSize", BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<int2>(property.GetValue(dialog));
        }

        /// <summary>
        /// Creates a small font asset that can satisfy preferences dialog layout requirements.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            return CreateFont(16f);
        }

        /// <summary>
        /// Creates a small font asset that can satisfy preferences dialog layout requirements at the supplied line height.
        /// </summary>
        /// <param name="lineHeight">Line height to expose on the created test font.</param>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont(float lineHeight) {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['O'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['f'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['%'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['1'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['7'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['5'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                lineHeight,
                64,
                64);
        }
    }
}
