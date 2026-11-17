namespace helengine {
    /// <summary>Owns material instances and explicit font-page pins for one visible text geometry run.</summary>
    public sealed class TextMeshPageBindings : IDisposable {
        /// <summary>Borrows the font that must outlive the visible text component.</summary>
        readonly FontAsset Font;
        /// <summary>Owns the material instances and their slot container.</summary>
        [NativeOwnedMember] RuntimeMaterial[] MaterialSlots;
        /// <summary>Owns the page indices corresponding to successfully retained material slots.</summary>
        [NativeOwnedMember] int[] Pages;
        /// <summary>Counts successful pins so interrupted construction can release exactly those pages.</summary>
        int RetainedCount;

        /// <summary>Allocates empty binding containers before any page uploads or material creation.</summary>
        TextMeshPageBindings(FontAsset font, int count) {
            Font = font; MaterialSlots = new RuntimeMaterial[count]; Pages = new int[count];
        }
        /// <summary>Gets borrowed material instances in the geometry's consecutive atlas-run order.</summary>
        [NativeBorrowedReturn]
        public RuntimeMaterial[] Materials => MaterialSlots;

        /// <summary>Creates every required page binding transactionally, releasing partial pins and instances on failure.</summary>
        [NativeOwnedReturn]
        public static TextMeshPageBindings Create(FontAsset font, RuntimeMaterial template, int[] pageIndices) {
            TextMeshPageBindings bindings = new TextMeshPageBindings(font, pageIndices.Length);
            try {
                for (int index = 0; index < pageIndices.Length; index++) {
                    RuntimeTexture texture = font.RetainPageTexture(pageIndices[index]);
                    bindings.Pages[index] = pageIndices[index]; bindings.RetainedCount++;
                    RuntimeMaterial material = template.CreateInstance();
                    if (material == null) { throw new InvalidOperationException("Text material template returned no instance."); }
                    bindings.MaterialSlots[index] = material; bindings.MaterialSlots[index].SetPrimaryTexture(texture);
                }
                return bindings;
            } catch {
                NativeOwnership.DisposeAndDelete(bindings); throw;
            }
        }
        /// <summary>Destroys local material instances before releasing pins that may immediately evict their borrowed textures.</summary>
        public void Dispose() {
            if (MaterialSlots != null) {
                for (int index = 0; index < MaterialSlots.Length; index++) { NativeOwnership.DisposeAndDelete(MaterialSlots[index]); MaterialSlots[index] = null; }
            }
            for (int index = 0; index < RetainedCount; index++) { Font.ReleasePageTexture(Pages[index]); }
            RetainedCount = 0; NativeOwnership.Release(ref MaterialSlots); NativeOwnership.Release(ref Pages);
        }
    }
}
