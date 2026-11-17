using System;

namespace helengine.ui.format {
    /// <summary>A renderer-independent logical rectangle with stable identity and typed components.</summary>
    public sealed class UiNode : IDisposable {
        /// <summary>Positive identity unique within the template.</summary>
        public int Id = 0;
        /// <summary>Earlier parent identity, or minus one for a template root.</summary>
        public int ParentId = -1;
        /// <summary>Unique nonempty authoring name within this template.</summary>
        public string Name = "";
        /// <summary>Local logical horizontal position.</summary>
        public float X = 0;
        /// <summary>Local logical vertical position.</summary>
        public float Y = 0;
        /// <summary>Local ordering depth; does not encode native depth-buffer units.</summary>
        public float Z = 0;
        /// <summary>Nonnegative logical width.</summary>
        public float Width = 0;
        /// <summary>Nonnegative logical height.</summary>
        public float Height = 0;
        /// <summary>Local visibility, composed with the ancestors by the renderer.</summary>
        public bool Visible = true;
        /// <summary>At most one component of each kind on this node.</summary>
        [NativeOwnedMember] UiComponent[] ComponentsValue = new UiComponent[0];
        /// <summary>Transfers exclusive ownership of the array and its elements; returned storage is borrowed.</summary>
        [NativeBorrowedReturn] public UiComponent[] Components {
            get { return ComponentsValue; }
            [param: NativeTakesOwnership] set {
                if (ReferenceEquals(ComponentsValue, value)) { return; }
                NativeOwnership.DisposeItemsAndRelease(ref ComponentsValue); ComponentsValue = value;
            }
        }
        /// <summary>Releases the complete owned subtree, including partially populated arrays; repeated disposal is safe.</summary>
        public void Dispose() { NativeOwnership.DisposeItemsAndRelease(ref ComponentsValue); }
    }
}
