using System;

namespace helengine.ui.format {
    /// <summary>A named reusable hierarchy whose parents precede their children.</summary>
    public sealed class UiTemplate : IDisposable {
        /// <summary>Unique nonempty template name.</summary>
        public string Name = "";
        /// <summary>Nodes in parent-before-child authoring order.</summary>
        [NativeOwnedMember] UiNode[] NodesValue = new UiNode[0];
        /// <summary>Transfers exclusive ownership of the array and its elements; returned storage is borrowed.</summary>
        [NativeBorrowedReturn] public UiNode[] Nodes {
            get { return NodesValue; }
            [param: NativeTakesOwnership] set {
                if (ReferenceEquals(NodesValue, value)) { return; }
                NativeOwnership.DisposeItemsAndRelease(ref NodesValue); NodesValue = value;
            }
        }
        /// <summary>Releases the complete owned subtree, including partially populated arrays; repeated disposal is safe.</summary>
        public void Dispose() { NativeOwnership.DisposeItemsAndRelease(ref NodesValue); }
    }
}
