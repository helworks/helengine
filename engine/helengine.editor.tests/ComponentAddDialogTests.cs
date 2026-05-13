using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies standalone modal behavior for the add-component dialog.
    /// </summary>
    public sealed class ComponentAddDialogTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the dialog tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the dialog tests.
        /// </summary>
        public ComponentAddDialogTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-component-add-dialog-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
            EditorInputCaptureService.Reset();

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
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
        /// Ensures the search field, scroll list, and footer are positioned immediately during Show.
        /// </summary>
        [Fact]
        public void Show_WhenOpened_PositionsSearchListAndFooterImmediately() {
            ComponentAddDialog dialog = new ComponentAddDialog(CreateFont());
            EditorEntity targetEntity = new EditorEntity {
                Name = "Cube"
            };

            dialog.Show(targetEntity);

            EditorEntity dialogContentRoot = GetProtectedProperty<EditorEntity>(dialog, "DialogContentRoot");
            EditorEntity searchFieldHost = GetPrivateField<EditorEntity>(dialog, "SearchFieldHost");
            EditorEntity listHost = GetPrivateField<EditorEntity>(dialog, "ListHost");
            EditorEntity footerHost = GetPrivateField<EditorEntity>(dialog, "FooterHost");
            ScrollComponent listScrollComponent = GetPrivateField<ScrollComponent>(dialog, "ListScrollComponent");

            Assert.Same(dialogContentRoot, searchFieldHost.Parent);
            Assert.Same(dialogContentRoot, listHost.Parent);
            Assert.Same(dialogContentRoot, footerHost.Parent);
            Assert.NotEqual(float3.Zero, searchFieldHost.LocalPosition);
            Assert.NotEqual(float3.Zero, listHost.LocalPosition);
            Assert.NotEqual(float3.Zero, footerHost.LocalPosition);
            Assert.True(listScrollComponent.Size.X > 0);
            Assert.True(listScrollComponent.Size.Y > 0);
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
        /// Creates a deterministic font asset for the add-component dialog layout.
        /// </summary>
        /// <returns>Font asset with the glyphs needed by the dialog labels and search field.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            string glyphs = "ACMSTabcdefghilmnoprstuvwy ";
            for (int index = 0; index < glyphs.Length; index++) {
                char glyph = glyphs[index];
                if (characters.ContainsKey(glyph)) {
                    continue;
                }

                double advance = glyph == 'M' || glyph == 'w' ? 10d :
                    glyph == 'i' || glyph == 'l' ? 4d : 8d;
                if (glyph == ' ') {
                    advance = 4d;
                }

                characters[glyph] = new FontChar(new float4(0f, 0f, (float)advance, 12f), 0f, (float)advance, 0f, 0f);
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
