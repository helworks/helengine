namespace helengine.editor.tests {
    /// <summary>Exercises direct UI references in reflected component fields and arrays during packaging.</summary>
    public sealed class CookedUiReferenceComponent : Component {
        /// <summary>Single interface document required by the runtime component.</summary>
        public SceneAssetReference InterfaceAsset;
        /// <summary>Additional interface documents required through an array-valued member.</summary>
        public SceneAssetReference[] Interfaces;
    }
}
