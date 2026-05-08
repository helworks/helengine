using helengine;
using System.Reflection;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the dedicated tab wrapper keeps tab-style defaults centralized.
    /// </summary>
    public sealed class TabComponentTests {
        /// <summary>
        /// Ensures the tab wrapper starts with top corners and updates its selected state explicitly.
        /// </summary>
        [Fact]
        public void Constructor_UsesTopCornersAndTracksSelectionState() {
            TabComponent tab = new TabComponent("Windows", new int2(96, 24), null, null);

            Assert.Equal(RoundedRectCorners.TopLeft | RoundedRectCorners.TopRight, tab.Corners);
            Assert.False(tab.IsSelected);
            Assert.False(tab.IsKeyboardFocused);
            Assert.Equal(ThemeManager.Colors.SurfacePrimary, GetPrivateField<byte4>(tab, "IdleFillColor"));
            Assert.Equal(ThemeManager.Colors.SurfaceInput, GetPrivateField<byte4>(tab, "FocusedFillColor"));
            Assert.Equal(ThemeManager.Colors.AccentTertiary, GetPrivateField<byte4>(tab, "IdleBorderColor"));
            Assert.Equal(ThemeManager.Colors.AccentTertiary, GetPrivateField<byte4>(tab, "FocusedBorderColor"));
            Assert.Equal(7.2f, GetPrivateField<float>(tab, "CornerRadius"));

            tab.SetSelected(true);

            Assert.True(tab.IsSelected);
            Assert.True(tab.IsKeyboardFocused);
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().BaseType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }
    }
}
