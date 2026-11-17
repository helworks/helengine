namespace helengine.painting {
    /// <summary>Owns an immutable copy of committed painting data for serialization or transfer.</summary>
    public sealed class PaintSnapshot : IDisposable {
        /// <summary>Owned immutable records in original drawing order.</summary>
        [NativeOwnedMember] List<PaintStrokeRecord> Records;
        /// <summary>Content identity inside the producing painting session.</summary>
        public long Revision { get; }
        /// <summary>Number of committed records in this snapshot.</summary>
        public int StrokeCount { get { return Records.Count; } }
        /// <summary>Copies the applied history without borrowing its lifetime.</summary>
        public PaintSnapshot([NativeNoEscape] PaintHistory history) {
            if(history==null) throw new ArgumentException("Missing snapshot history");
            Records=new List<PaintStrokeRecord>(history.StrokeCount);
            for(int i=0;i<history.StrokeCount;i++) Records.Add(history.GetStroke(i));
            Revision=history.Revision;
        }
        /// <summary>Returns an independently owned record for callers that outlive this snapshot.</summary>
        [NativeOwnedReturn]
        public PaintStrokeRecord GetStroke(int index) { return Records[index].Copy(); }
        /// <summary>Releases records before deleting their native owning container.</summary>
        public void Dispose() {
            if(Records!=null) for(int i=0;i<Records.Count;i++) Records[i].Dispose();
            NativeOwnership.Release(ref Records);
        }
    }
}
