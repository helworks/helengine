using System;

namespace helengine.ui.format {
    /// <summary>One supported component with explicitly typed properties.</summary>
    public sealed class UiComponent : IDisposable {
        /// <summary>Stable component identifier, independent of C# type names.</summary>
        public UiComponentKind Kind = UiComponentKind.Rectangle;
        /// <summary>Unique property identifiers in deterministic authoring order.</summary>
        [NativeOwnedMember] UiProperty[] PropertiesValue = new UiProperty[0];
        /// <summary>Transfers exclusive ownership of the array and its elements; returned storage is borrowed.</summary>
        [NativeBorrowedReturn] public UiProperty[] Properties {
            get { return PropertiesValue; }
            [param: NativeTakesOwnership] set {
                if (ReferenceEquals(PropertiesValue, value)) { return; }
                NativeOwnership.DeleteItemsAndRelease(ref PropertiesValue); PropertiesValue = value;
            }
        }
        /// <summary>Releases the complete owned subtree, including partially populated arrays; repeated disposal is safe.</summary>
        public void Dispose() { NativeOwnership.DeleteItemsAndRelease(ref PropertiesValue); }
    }
}
