namespace helengine {
    /// <summary>
    /// Owns fixed-count shader binding slots and versions every effective indexed replacement.
    /// Constructor arrays are copied but retained until disposal; callers may borrow them without altering slots.
    /// Binding objects transferred at construction or assignment remain owned until disposal, including displaced
    /// objects, so reordering and reusing entries never deletes a binding still borrowed by another slot.
    /// Ownership is exclusive to this collection; bindings must not be shared with other owning collections.
    /// </summary>
    public class MaterialLayoutBindings : IDisposable {
        /// <summary>Private slot storage that cannot be mutated through a constructor-array alias.</summary>
        MaterialLayoutBinding[] Bindings;
        /// <summary>Transferred input container retained for the lifetime of any existing caller alias.</summary>
        MaterialLayoutBinding[] SourceBindings;
        /// <summary>Unique transferred bindings, including displaced entries retained for safe reuse.</summary>
        List<MaterialLayoutBinding> OwnedBindings;
        /// <summary>Monotonic mutation revision that never wraps.</summary>
        ulong RevisionValue;
        /// <summary>Whether explicit disposal has released all owned native storage.</summary>
        bool Disposed;
        /// <summary>Whether the owning shared empty layout requires process-lifetime storage.</summary>
        bool SharedLifetime;

        /// <summary>Copies binding slots and assumes ownership of the supplied container and its original entries.</summary>
        /// <param name="bindings">Transferred binding array; null slots and repeated identities are allowed.</param>
        public MaterialLayoutBindings(MaterialLayoutBinding[] bindings) {
            if (bindings == null) {
                throw new ArgumentNullException(nameof(bindings));
            }
            Bindings = new MaterialLayoutBinding[bindings.Length];
            OwnedBindings = new List<MaterialLayoutBinding>();
            for (int index = 0; index < bindings.Length; index++) {
                Bindings[index] = bindings[index];
                RetainBinding(bindings[index]);
            }
            SourceBindings = bindings;
        }

        /// <summary>Gets the fixed slot count; disposed collections cannot be read.</summary>
        public int Length {
            get {
                ThrowIfDisposed();
                return Bindings.Length;
            }
        }

        /// <summary>Gets the mutation revision; disposed collections cannot validate a prepared cache entry.</summary>
        public ulong Revision {
            get {
                ThrowIfDisposed();
                return RevisionValue;
            }
        }

        /// <summary>Gets or replaces one binding slot, advancing the revision before an effective replacement.</summary>
        /// <param name="index">Zero-based binding slot.</param>
        public MaterialLayoutBinding this[int index] {
            get {
                ValidateIndex(index);
                return Bindings[index];
            }
            set {
                ValidateIndex(index);
                if (ReferenceEquals(Bindings[index], value)) {
                    return;
                }
                if (RevisionValue == ulong.MaxValue) {
                    throw new InvalidOperationException("Material binding revision exhausted.");
                }
                RevisionValue++;
                RetainBinding(value);
                Bindings[index] = value;
            }
        }

        /// <summary>Releases each transferred binding once and both containers without inspecting mutable input slots.</summary>
        public void Dispose() {
            if (Disposed || SharedLifetime) {
                return;
            }
            Disposed = true;
            NativeOwnership.DetachOwned(OwnedBindings);
            for (int index = 0; index < OwnedBindings.Count; index++) {
                NativeOwnership.Delete(OwnedBindings[index]);
            }
            NativeOwnership.Release(ref OwnedBindings);
            NativeOwnership.Release(ref Bindings);
            if (!ReferenceEquals(SourceBindings, Array.Empty<MaterialLayoutBinding>())) {
                NativeOwnership.Release(ref SourceBindings);
            }
            SourceBindings = null;
        }

        /// <summary>Protects the shared empty layout's collections from direct public disposal.</summary>
        internal void PreserveSharedLifetime() {
            SharedLifetime = true;
        }

        /// <summary>Retains each non-null transferred identity once, permitting duplicate slots and reorder operations.</summary>
        /// <param name="binding">Binding whose ownership transfers to this collection.</param>
        void RetainBinding(MaterialLayoutBinding binding) {
            if (binding == null) {
                return;
            }
            for (int index = 0; index < OwnedBindings.Count; index++) {
                if (ReferenceEquals(OwnedBindings[index], binding)) {
                    return;
                }
            }
            OwnedBindings.Add(binding);
        }

        /// <summary>Rejects disposed access and invalid slots before any mutation or ownership transfer occurs.</summary>
        /// <param name="index">Zero-based slot to validate.</param>
        void ValidateIndex(int index) {
            ThrowIfDisposed();
            if (index < 0 || index >= Bindings.Length) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        /// <summary>Prevents reads or writes through retained references after native resources have been released.</summary>
        void ThrowIfDisposed() {
            if (Disposed) {
                throw new InvalidOperationException("Material binding collection has been disposed.");
            }
        }
    }
}
