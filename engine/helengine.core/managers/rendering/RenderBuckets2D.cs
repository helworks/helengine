namespace helengine {
    public struct Index2D {
        public int Bucket;
        public int Pos;
        public Index2D(int bucket, int pos) { Bucket = bucket; Pos = pos; }
    }

    public sealed class RenderBucket2D {
        public IDrawable2D[] Items;
        public int Count;

        public RenderBucket2D(int initialCapacity = 32) {
            if (initialCapacity < 1) initialCapacity = 1;
            Items = new IDrawable2D[initialCapacity];
            Count = 0;
        }

        public void Clear() { Count = 0; }

        public void Add(IDrawable2D item, out int pos) {
            if (Count == Items.Length) {
                int newCap = Items.Length << 1;
                Array.Resize(ref Items, newCap);
            }
            pos = Count;
            Items[Count++] = item;
        }

        public IDrawable2D? RemoveSwapAt(int pos) {
            int last = Count - 1;
            if (pos < 0 || pos > last) return null;
            IDrawable2D swapped = Items[last];
            if (pos != last) {
                Items[pos] = swapped;
            }
            Items[last] = default!;
            Count = last;
            return pos == last ? null : swapped;
        }
    }

    public sealed class Camera2DRegistry {
        public readonly RenderBucket2D[] Buckets;
        public readonly Dictionary<IDrawable2D, Index2D> Map;

        public Camera2DRegistry(int bucketCount, int initialBucketCapacity = 32, int mapCapacity = 128) {
            Buckets = new RenderBucket2D[bucketCount];
            for (int i = 0; i < bucketCount; i++) {
                Buckets[i] = new RenderBucket2D(initialBucketCapacity);
            }
            Map = new Dictionary<IDrawable2D, Index2D>(mapCapacity, ReferenceEqualityComparer<IDrawable2D>.Instance);
        }
    }

    // Reference equality comparer to avoid overriding Equals on drawables
    internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class {
        public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();
        private ReferenceEqualityComparer() { }
        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
        public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}

