namespace Nucleus.Diagnostics {
    public struct MeasureData {
        public object Parent { get; private set; }
        public string Key { get; private set; }
        public double RecordedValue { get; private set; }
        public int ThreadID { get; private set; }

        public MeasureData(object parent, string key, double val, int threadID) {
            Parent = parent;
            Key = key;
            RecordedValue = val;
            ThreadID = threadID;
        }

        public MeasureData(MeasureKey key, double val, int threadID) {
            Parent = key.Parent;
            Key = key.Key;
            RecordedValue = val;
            ThreadID = threadID;
        }
    }
}
