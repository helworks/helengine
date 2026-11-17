namespace helengine.painting {
    /// <summary>Owns one committed stroke and the content revision on each side of its history transition.</summary>
    public sealed class PaintTransition : IDisposable {
        /// <summary>Authored stroke storage owned by this transition.</summary>
        [NativeOwnedMember] PaintStrokeRecord Stroke;
        /// <summary>Unique transition and resulting content revision within the session.</summary>
        public long Token { get; }
        /// <summary>Content revision restored when this transition is undone.</summary>
        public long Before { get; }
        /// <summary>Stable identity without copying sample storage.</summary>
        public string StrokeId { get { return Stroke.Id; } }
        /// <summary>Authored sample count for admission accounting.</summary>
        public int SampleCount { get { return Stroke.SampleCount; } }
        /// <summary>Conservative mark cost of the owned stroke.</summary>
        public double StampBudget { get { return Stroke.StampBudget; } }
        /// <summary>Copies one validated record before its history cursor is mutated.</summary>
        public PaintTransition(long token,long before,[NativeNoEscape] PaintStrokeRecord stroke) {
            if (stroke==null) throw new ArgumentException("Missing history stroke");
            Token=token; Before=before; Stroke=stroke.Copy();
        }
        /// <summary>Returns an independent copy of this transition's committed data.</summary>
        [NativeOwnedReturn]
        public PaintStrokeRecord CopyStroke() { return Stroke.Copy(); }
        /// <summary>Releases the record before the owning history deletes this transition.</summary>
        public void Dispose() { NativeOwnership.DisposeAndRelease(ref Stroke); }
    }
}
