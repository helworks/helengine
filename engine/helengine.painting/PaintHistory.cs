namespace helengine.painting {
    /// <summary>Owns the chronological stroke history and its content revisions, without document metadata.</summary>
    public sealed class PaintHistory : IDisposable {
        /// <summary>Owned chronological transitions, including an optional undone suffix.</summary>
        [NativeOwnedMember] List<PaintTransition> Items;
        /// <summary>Index immediately after the currently applied prefix.</summary>
        int Cursor;
        /// <summary>First transition still allowed to be undone.</summary>
        int FirstUndo;
        /// <summary>Maximum retained undo depth chosen by the consumer.</summary>
        readonly int Limit;
        /// <summary>Unique next token, never reused after a discarded branch.</summary>
        long NextToken=1;
        /// <summary>Revision of current content, stable across visits through undo/redo.</summary>
        long CurrentRevision;
        /// <summary>Constructs a bounded undo history with an explicit positive retention limit.</summary>
        public PaintHistory(int limit) {
            if (limit<=0 || limit>10000) throw new ArgumentOutOfRangeException("limit");
            Limit=limit; Items=new List<PaintTransition>(10000);
        }
        /// <summary>Number of currently applied strokes, including the irreversible base.</summary>
        public int StrokeCount { get { return Cursor; } }
        /// <summary>Stable identifier of the current content, restored by undo and redo.</summary>
        public long Revision { get { return CurrentRevision; } }
        /// <summary>Whether an applied transition remains reversible.</summary>
        public bool CanUndo { get { return Items!=null && Cursor>FirstUndo; } }
        /// <summary>Whether a retained forward branch can be reapplied.</summary>
        public bool CanRedo { get { return Items!=null && Cursor<Items.Count; } }
        /// <summary>Sample cost of the next retained redo record, without copying its owned sample storage.</summary>
        public int RedoSampleCount {
            get { CheckAlive(); if(!CanRedo) throw new InvalidOperationException("No retained redo stroke"); return Items[Cursor].SampleCount; }
        }
        /// <summary>Conservative mark cost of the next retained redo record, without moving the history cursor.</summary>
        public double RedoStampBudget {
            get { CheckAlive(); if(!CanRedo) throw new InvalidOperationException("No retained redo stroke"); return Items[Cursor].StampBudget; }
        }
        /// <summary>Total samples in applied content.</summary>
        public int SampleCount { get { CheckAlive(); int count=0; for(int i=0;i<Cursor;i++) count+=Items[i].SampleCount; return count; } }
        /// <summary>Conservative mark cost in applied content.</summary>
        public double StampBudget { get { CheckAlive(); double count=0; for(int i=0;i<Cursor;i++) count+=Items[i].StampBudget; return count; } }
        /// <summary>Checks applied identities without allocating copies.</summary>
        public bool ContainsId(string id) {
            CheckAlive();
            for(int i=0;i<Cursor;i++) if(Items[i].StrokeId==id) return true;
            return false;
        }
        /// <summary>Establishes loaded content as an irreversible base before host history starts.</summary>
        public void SealBase() { CheckAlive(); DiscardRedo(); FirstUndo=Cursor; CurrentRevision=0; }
        /// <summary>Copies a new stroke and returns its transition token after truncating redo.</summary>
        public long Append([NativeNoEscape] PaintStrokeRecord stroke) {
            CheckAlive();
            if(Cursor>=10000) throw new InvalidOperationException("Stroke limit exceeded");
            PaintTransition transition=new PaintTransition(NextToken,CurrentRevision,stroke);
            DiscardRedo();
            Items.Add(transition); Cursor++;
            CurrentRevision=NextToken; NextToken++;
            FirstUndo=Math.Max(FirstUndo,Cursor-Limit);
            return CurrentRevision;
        }
        /// <summary>Undoes only the expected latest painting transition.</summary>
        public void Undo(long transition) {
            CheckAlive();
            if(!CanUndo || Items[Cursor-1].Token!=transition) throw new InvalidOperationException("Painting undo order mismatch");
            CurrentRevision=Items[Cursor-1].Before; Cursor--;
        }
        /// <summary>Redoes only the expected next painting transition.</summary>
        public void Redo(long transition) {
            CheckAlive();
            if(!CanRedo || Items[Cursor].Token!=transition) throw new InvalidOperationException("Painting redo order mismatch");
            CurrentRevision=Items[Cursor].Token; Cursor++;
        }
        /// <summary>Discards only undone records, retaining all applied artwork.</summary>
        public void DiscardRedo() {
            CheckAlive();
            while(Items.Count>Cursor) {
                Items[Items.Count-1].Dispose(); Items.RemoveAt(Items.Count-1);
            }
        }
        /// <summary>Forgets the ability to undo an applied prefix without deleting its artwork.</summary>
        public void ForgetTransition(long transition) {
            CheckAlive();
            for(int i=0;i<Cursor;i++) {
                if(Items[i].Token==transition) { FirstUndo=Math.Max(FirstUndo,i+1); return; }
            }
            throw new InvalidOperationException("Cannot forget absent or unapplied transition");
        }
        /// <summary>Copies an applied record for a consumer without exposing mutable history state.</summary>
        [NativeOwnedReturn]
        public PaintStrokeRecord GetStroke(int index) {
            CheckAlive();
            if(index<0 || index>=Cursor) throw new ArgumentOutOfRangeException("index");
            return Items[index].CopyStroke();
        }
        /// <summary>Releases retained records and their sample storage.</summary>
        public void Dispose() {
            if(Items!=null) {
                for(int i=0;i<Items.Count;i++) Items[i].Dispose();
            }
            NativeOwnership.Release(ref Items);
        }

        /// <summary>Rejects operations before dereferencing a released native history.</summary>
        void CheckAlive() { if(Items==null) throw new ObjectDisposedException("PaintHistory"); }
    }
}
