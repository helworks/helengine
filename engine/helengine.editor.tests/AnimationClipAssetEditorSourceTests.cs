using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Audits editor source so animation clips get a dedicated per-platform asset editor.
    /// </summary>
    public sealed class AnimationClipAssetEditorSourceTests {
        /// <summary>
        /// Ensures editor-session asset routing recognizes `.hanim` entries and forwards them to the dedicated animation clip settings view.
        /// </summary>
        [Fact]
        public void EditorSession_routes_hanim_assets_to_animation_clip_settings_view() {
            string source = File.ReadAllText(@"C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs");

            Assert.Contains("bool IsAnimationClipAssetEntry(AssetBrowserEntry entry)", source, StringComparison.Ordinal);
            Assert.Contains("propertiesPanels[index].ShowAnimationClipSettings(", source, StringComparison.Ordinal);
            Assert.Contains("entry.Extension", source, StringComparison.Ordinal);
            Assert.Contains(".hanim", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the properties panel owns one dedicated animation clip asset view and exposes a show method for it.
        /// </summary>
        [Fact]
        public void PropertiesPanel_owns_animation_clip_asset_view() {
            string source = File.ReadAllText(@"C:\dev\helworks\helengine\engine\helengine.editor\components\ui\PropertiesPanel.cs");

            Assert.Contains("readonly AnimationClipAssetView AnimationClipView;", source, StringComparison.Ordinal);
            Assert.Contains("public void ShowAnimationClipSettings(", source, StringComparison.Ordinal);
            Assert.Contains("AnimationClipView.Show(", source, StringComparison.Ordinal);
        }
    }
}
