namespace helengine.painting.tests {
    /// <summary>Test surface that retains immutable mark values and can exercise a real session's error boundary.</summary>
    public sealed class RecordingPaintBackend : IPaintSurfaceBackend {
        /// <summary>Marks admitted since the last transparent reset.</summary>
        public readonly List<PaintStamp> Stamps=new();
        /// <summary>Enables a synchronous admission failure without simulating a successful GPU operation.</summary>
        public bool FailAppend;
        /// <summary>Records explicit activation without transforming authored test marks.</summary>
        public void Reconfigure(PaintCanvasDescriptor canvas) { Stamps.Clear(); }
        /// <summary>Models release of all queued GPU marks.</summary>
        public void Suspend() { Stamps.Clear(); }
        /// <summary>Clears the in-memory surface state for a replay.</summary>
        public void Reset(long revision) { Stamps.Clear(); }
        /// <summary>Copies the supplied values or explicitly rejects a failed surface.</summary>
        public void Append(PaintStamp[] stamps,int count,PaintBlendMode mode,long revision) {
            if(FailAppend) throw new InvalidOperationException("Surface failure");
            for(int i=0;i<count;i++) Stamps.Add(stamps[i]);
        }
        /// <summary>The in-memory fixture completes immediately.</summary>
        public long Submit() { return 1; }
        /// <summary>Reports the fixture's synchronous completion.</summary>
        public PaintSubmissionStatus GetSubmissionStatus(long ticket) { return PaintSubmissionStatus.Completed; }
        /// <summary>No GPU ticket allocation exists in this fixture.</summary>
        public void ReleaseSubmission(long ticket) { }
        /// <summary>No GPU texture exists in the CPU-only fixture.</summary>
        public long AcquireSnapshot(long revision,bool mipmapped) { return revision+1; }
        /// <summary>Reports fixture snapshot completion.</summary>
        public PaintSubmissionStatus GetSnapshotStatus(long snapshot) { return PaintSubmissionStatus.Completed; }
        /// <summary>No GPU snapshot allocation exists in this fixture.</summary>
        public void ReleaseSnapshot(long snapshot) { }
    }
}
