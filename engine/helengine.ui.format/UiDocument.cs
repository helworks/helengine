using System;

namespace helengine.ui.format {
    /// <summary>Portable UI definitions and application-supplied logical metrics; contains no renderer objects.</summary>
    public sealed class UiDocument : IDisposable {
        /// <summary>Named logical metrics in deterministic authoring order.</summary>
        [NativeOwnedMember] UiMetric[] MetricsValue = new UiMetric[0];
        /// <summary>Transfers exclusive ownership of the array and its elements; returned storage is borrowed.</summary>
        [NativeBorrowedReturn] public UiMetric[] Metrics {
            get { return MetricsValue; }
            [param: NativeTakesOwnership] set {
                if (ReferenceEquals(MetricsValue, value)) { return; }
                NativeOwnership.DeleteItemsAndRelease(ref MetricsValue); MetricsValue = value;
            }
        }
        /// <summary>Reusable templates in deterministic authoring order.</summary>
        [NativeOwnedMember] UiTemplate[] TemplatesValue = new UiTemplate[0];
        /// <summary>Transfers exclusive ownership of the array and its elements; returned storage is borrowed.</summary>
        [NativeBorrowedReturn] public UiTemplate[] Templates {
            get { return TemplatesValue; }
            [param: NativeTakesOwnership] set {
                if (ReferenceEquals(TemplatesValue, value)) { return; }
                NativeOwnership.DisposeItemsAndRelease(ref TemplatesValue); TemplatesValue = value;
            }
        }
        /// <summary>Releases the complete owned subtree, including partially populated arrays; repeated disposal is safe.</summary>
        public void Dispose() { NativeOwnership.DeleteItemsAndRelease(ref MetricsValue); NativeOwnership.DisposeItemsAndRelease(ref TemplatesValue); }
    }
}
