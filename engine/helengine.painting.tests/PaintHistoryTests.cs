namespace helengine.painting.tests {
    /// <summary>Checks that standalone library consumers get correct stroke undo, branching and retention.</summary>
    public sealed class PaintHistoryTests {
        /// <summary>Builds a one-sample stroke with a stable identity and explicit brush parameters.</summary>
        static PaintStrokeRecord Stroke(string id) {
            return new PaintStrokeRecord(id,new PaintBrushDefinition { Version=1,Id="cozy.round",Diameter=2,Opacity=1 },
                new PaintSample[] { new PaintSample { X=1,Y=2,Pressure=1 } });
        }
        /// <summary>Returning to saved content restores its exact revision, not merely its pixel appearance.</summary>
        [Fact]
        public void UndoAndRedoRestoreContentRevision() {
            using PaintHistory history=new(100);
            long a=history.Append(Stroke("a")); long saved=history.Revision;
            long b=history.Append(Stroke("b"));
            Assert.Equal(2,history.StrokeCount);
            history.Undo(b); Assert.Equal(saved,history.Revision); Assert.Equal(1,history.StrokeCount);
            history.Undo(a); Assert.Equal(0,history.Revision); Assert.False(history.CanUndo);
            history.Redo(a); Assert.Equal(saved,history.Revision);
            history.Redo(b); Assert.Equal("b",history.GetStroke(1).Id);
        }
        /// <summary>A new branch cannot replay a discarded future stroke.</summary>
        [Fact]
        public void NewStrokeDropsRedo() {
            using PaintHistory history=new(100);
            history.Append(Stroke("a")); long b=history.Append(Stroke("b")); history.Undo(b);
            history.Append(Stroke("c"));
            Assert.False(history.CanRedo); Assert.Equal("c",history.GetStroke(1).Id);
            Assert.Throws<InvalidOperationException>(()=>history.Redo(b));
        }
        /// <summary>A mismatched host timeline cannot move the library cursor.</summary>
        [Fact]
        public void WrongTokenDoesNotMutateHistory() {
            using PaintHistory history=new(100);
            long a=history.Append(Stroke("a")); history.Append(Stroke("b"));
            Assert.Throws<InvalidOperationException>(()=>history.Undo(a));
            Assert.Equal(2,history.StrokeCount); Assert.False(history.CanRedo);
        }
        /// <summary>Evicting undo data keeps the permanent drawing, while limiting reversible actions.</summary>
        [Fact]
        public void ForgetOldTransitionKeepsCurrentArtwork() {
            using PaintHistory history=new(2);
            long a=history.Append(Stroke("a")); long b=history.Append(Stroke("b")); long c=history.Append(Stroke("c"));
            history.ForgetTransition(a);
            history.Undo(c); history.Undo(b);
            Assert.False(history.CanUndo); Assert.Equal(1,history.StrokeCount); Assert.Equal("a",history.GetStroke(0).Id);
        }
        /// <summary>Exactly one hundred actions remain reversible while all older artwork survives.</summary>
        [Fact]
        public void HundredActionWindowRetainsTheBase() {
            using PaintHistory history=new(100);
            long[] tokens=new long[125];
            for(int i=0;i<tokens.Length;i++) tokens[i]=history.Append(Stroke("stroke"+i));
            for(int i=124;i>=25;i--) history.Undo(tokens[i]);
            Assert.False(history.CanUndo); Assert.Equal(25,history.StrokeCount);
            for(int i=25;i<125;i++) history.Redo(tokens[i]);
            Assert.False(history.CanRedo); Assert.Equal(125,history.StrokeCount);
        }
    }
}
