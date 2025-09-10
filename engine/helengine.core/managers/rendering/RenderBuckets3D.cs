namespace helengine {
    public struct Index3D {
        public int Variant;
        public int Bucket;
        public int Bin;
        public int Pos;
        public Index3D(int variant, int bucket, int bin, int pos) { Variant = variant; Bucket = bucket; Bin = bin; Pos = pos; }
    }

    public sealed class RenderBucket3D {
        public IDrawable3D[] Items;
        public int Count;

        public RenderBucket3D(int initialCapacity = 32) {
            if (initialCapacity < 1) initialCapacity = 1;
            Items = new IDrawable3D[initialCapacity];
            Count = 0;
        }

        public void Clear() { Count = 0; }

        public void Add(IDrawable3D item, out int pos) {
            if (Count == Items.Length) {
                int newCap = Items.Length << 1;
                Array.Resize(ref Items, newCap);
            }
            pos = Count;
            Items[Count++] = item;
        }

        public IDrawable3D? RemoveSwapAt(int pos) {
            int last = Count - 1;
            if (pos < 0 || pos > last) return null;
            IDrawable3D swapped = Items[last];
            if (pos != last) {
                Items[pos] = swapped;
            }
            Items[last] = default!;
            Count = last;
            return pos == last ? null : swapped;
        }
    }

    public sealed class Camera3DRegistry {
        public readonly RenderBucket3D[][][] Buckets; // [variant][bucket][bin]
        public readonly Dictionary<IDrawable3D, Index3D> Map;

        public readonly int BinsPerBucket;

        public Camera3DRegistry(int variants, int bucketsPerVariant, int binsPerBucket, int initialBucketCapacity = 32, int mapCapacity = 256) {
            BinsPerBucket = binsPerBucket;
            Buckets = new RenderBucket3D[variants][][];
            for (int v = 0; v < variants; v++) {
                Buckets[v] = new RenderBucket3D[bucketsPerVariant][];
                for (int b = 0; b < bucketsPerVariant; b++) {
                    Buckets[v][b] = new RenderBucket3D[binsPerBucket];
                    for (int bin = 0; bin < binsPerBucket; bin++) {
                        Buckets[v][b][bin] = new RenderBucket3D(initialBucketCapacity);
                    }
                }
            }
            Map = new Dictionary<IDrawable3D, Index3D>(mapCapacity, ReferenceEqualityComparer<IDrawable3D>.Instance);
        }
    }
}
