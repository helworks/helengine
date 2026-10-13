using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that the mesh modifier picker modal can be constructed with one entry button per modifier kind.
    /// </summary>
    public class MeshModifierPickerModalTests : IDisposable {
        /// <summary>
        /// Workspace-owned scratch project root used as the core content root for the modal tests.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes the core services required by the modifier picker modal.
        /// </summary>
        public MeshModifierPickerModalTests() {
            ProjectRootPath = Path.Combine("C:\\dev\\helworks\\builds\\helengine\\editor-tests", "mesh-modifier-picker-modal", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(ProjectRootPath)
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Deletes the scratch project root after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures every modifier kind, including Reduce, gets a labeled entry button so editor boot does not fail while building the modal.
        /// </summary>
        [Fact]
        public void Constructor_CreatesOneLabeledEntryButtonPerModifierKind() {
            MeshModifierPickerModal modal = new MeshModifierPickerModal(Core.Instance, new EditorSessionInteractionServices(), CreateFont(), new EditorUiMetrics(1d));

            ButtonComponent[] entryButtons = GetPrivateField<ButtonComponent[]>(modal, "EntryButtons");

            Assert.Equal(3, entryButtons.Length);
            Assert.All(entryButtons, button => Assert.NotNull(button));
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            } else if (string.IsNullOrWhiteSpace(fieldName)) {
                throw new ArgumentException("Field name must be provided.", nameof(fieldName));
            }

            Type currentType = target.GetType();
            while (currentType != null) {
                FieldInfo field = currentType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null) {
                    return Assert.IsType<T>(field.GetValue(target));
                }

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"Field '{fieldName}' was not found on '{target.GetType().FullName}' or its base types.");
        }

        /// <summary>
        /// Creates a small font asset with enough glyphs for the modal's labels.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            foreach (char character in "AaBbCcDdEeFfGgHhIiJjKkLlMmNnOoPpQqRrSsTtUuVvWwXxYyZz ") {
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
