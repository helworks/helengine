namespace Nucleus.Diagnostics {
    public struct MeasureKey {
        public object Parent { get; private set; }
        public string Key { get; private set; }

        public MeasureKey(object parent, string key) {
            Parent = parent;
            Key = key;
        }

        public override string ToString() {
            return $"{Parent}.{Key}";
        }

        public override int GetHashCode() {
            return base.GetHashCode();
        }

        public override bool Equals(object obj) {
            if (obj is MeasureKey) {
                MeasureKey other = (MeasureKey)obj;
                if (other.Key == Key &&
                    other.Parent == Parent) {
                    return true;
                }
            }
            return false;
        }
    }
}
