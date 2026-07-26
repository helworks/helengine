namespace helengine.editor.tests {
    /// <summary>
    /// Guards the runtime model against reintroducing unused per-drawable camera-layer properties.
    /// </summary>
    public sealed class Drawable2DLayerMaskRemovalTests {
        /// <summary>
        /// Verifies that camera filtering remains an entity and camera responsibility rather than a 2D component responsibility.
        /// </summary>
        [Fact]
        public void Drawable2D_components_do_not_expose_layer_masks() {
            Assert.Null(typeof(SpriteComponent).GetProperty("LayerMask"));
            Assert.Null(typeof(TextComponent).GetProperty("LayerMask"));
            Assert.Null(typeof(RoundedRectComponent).GetProperty("LayerMask"));
        }
    }
}
