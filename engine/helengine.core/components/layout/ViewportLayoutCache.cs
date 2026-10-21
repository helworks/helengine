#if !HELENGINE_RUNTIME_COOKED_VIEWPORT_LAYOUT
namespace helengine {
    /// <summary>Retains compact typed original values only for a dynamic owner or editor preview.</summary>
    public sealed class ViewportLayoutCache {
        /// <summary>Borrowed root whose layout records this cache contains.</summary>
        Entity Root;
        /// <summary>Dense original records for actual LayoutComponent participants.</summary>
        readonly List<ViewportAnchorState> AnchorStates = new List<ViewportAnchorState>();
        /// <summary>Dense original records for actual RoundedRectComponent participants.</summary>
        readonly List<ViewportRectangleState> RectangleStates = new List<ViewportRectangleState>();
        /// <summary>Dense original records for actual TextComponent participants.</summary>
        readonly List<ViewportTextState> TextStates = new List<ViewportTextState>();
        /// <summary>Dense original records for actual SpriteComponent participants.</summary>
        readonly List<ViewportSpriteState> SpriteStates = new List<ViewportSpriteState>();
        /// <summary>Dense original records for actual ClipRectComponent participants.</summary>
        readonly List<ViewportClipState> ClipStates = new List<ViewportClipState>();
        /// <summary>Dense original records for actual InteractableComponent participants.</summary>
        readonly List<ViewportInteractableState> InteractableStates = new List<ViewportInteractableState>();
        /// <summary>Dense original records for actual ScrollComponent participants.</summary>
        readonly List<ViewportScrollState> ScrollStates = new List<ViewportScrollState>();
        /// <summary>Dense original records for actual FPSComponent participants.</summary>
        readonly List<ViewportFpsState> FpsStates = new List<ViewportFpsState>();
        /// <summary>Dense original local positions for the UI transform channel.</summary>
        readonly List<ViewportPositionState> Positions = new List<ViewportPositionState>();
        /// <summary>Creates a cache only through explicit capture.</summary>
        ViewportLayoutCache() { }
        /// <summary>Gets the number of retained transform participants.</summary>
        public int PositionCount => Positions.Count;
        /// <summary>Returns a borrowed transform participant during editor-only authoring restoration.</summary>
        [NativeBorrowedReturn]
        internal Entity GetPositionEntity(int index) { return Positions[index].Entity; }
        /// <summary>Captures one owner boundary; never called by Cook or OnLoad players.</summary>
        [NativeOwnedReturn]
        public static ViewportLayoutCache Capture([NativeRetainsBorrow] Entity owner) {
            ViewportLayoutCache cache = new ViewportLayoutCache();
            cache.Reconcile(owner);
            return cache;
        }
        /// <summary>Discovers newly attached participants while preserving originals of unchanged identities.</summary>
        public void Reconcile([NativeRetainsBorrow] Entity owner) {
            Root = owner;
            CaptureRecursive(owner);
        }
        /// <summary>Captures a subtree without crossing another layout owner boundary.</summary>
        void CaptureRecursive([NativeRetainsBorrow] Entity entity) {
            bool hasPosition = false;
            for (int index = 0; index < Positions.Count; index++) {
                if (ReferenceEquals(Positions[index].Entity, entity)) { hasPosition = true; break; }
            }
            if (!hasPosition) { Positions.Add(new ViewportPositionState(entity)); }
            if (entity.Components != null) {
                for (int index = 0; index < entity.Components.Count; index++) {
                    Component component = entity.Components[index];
                    if (component is LayoutComponent participantAnchor) {
                        CaptureAnchor(participantAnchor);
                    }
                    else if (component is RoundedRectComponent participantRectangle) {
                        CaptureRectangle(participantRectangle);
                    }
                    else if (component is TextComponent participantText) {
                        CaptureText(participantText);
                    }
                    else if (component is SpriteComponent participantSprite) {
                        CaptureSprite(participantSprite);
                    }
                    else if (component is ClipRectComponent participantClip) {
                        CaptureClip(participantClip);
                    }
                    else if (component is InteractableComponent participantInteractable) {
                        CaptureInteractable(participantInteractable);
                    }
                    else if (component is ScrollComponent participantScroll) {
                        CaptureScroll(participantScroll);
                    }
                    else if (component is FPSComponent participantFps) {
                        CaptureFps(participantFps);
                    }
                }
            }
            if (entity.Children != null) {
                for (int index = 0; index < entity.Children.Count; index++) {
                    if (ViewportLayoutTraversal.ShouldVisit(entity.Children[index])) { CaptureRecursive(entity.Children[index]); }
                }
            }
        }
        /// <summary>Captures a new LayoutComponent identity without replacing an existing authored baseline.</summary>
        void CaptureAnchor([NativeRetainsBorrow] LayoutComponent component) {
            for (int index = 0; index < AnchorStates.Count; index++) {
                if (ReferenceEquals(AnchorStates[index].Component, component)) { return; }
            }
            AnchorStates.Add(new ViewportAnchorState(component));
        }
        /// <summary>Captures a new RoundedRectComponent identity without replacing an existing authored baseline.</summary>
        void CaptureRectangle([NativeRetainsBorrow] RoundedRectComponent component) {
            for (int index = 0; index < RectangleStates.Count; index++) {
                if (ReferenceEquals(RectangleStates[index].Component, component)) { return; }
            }
            RectangleStates.Add(new ViewportRectangleState(component));
        }
        /// <summary>Captures a new TextComponent identity without replacing an existing authored baseline.</summary>
        void CaptureText([NativeRetainsBorrow] TextComponent component) {
            for (int index = 0; index < TextStates.Count; index++) {
                if (ReferenceEquals(TextStates[index].Component, component)) { return; }
            }
            TextStates.Add(new ViewportTextState(component));
        }
        /// <summary>Captures a new SpriteComponent identity without replacing an existing authored baseline.</summary>
        void CaptureSprite([NativeRetainsBorrow] SpriteComponent component) {
            for (int index = 0; index < SpriteStates.Count; index++) {
                if (ReferenceEquals(SpriteStates[index].Component, component)) { return; }
            }
            SpriteStates.Add(new ViewportSpriteState(component));
        }
        /// <summary>Captures a new ClipRectComponent identity without replacing an existing authored baseline.</summary>
        void CaptureClip([NativeRetainsBorrow] ClipRectComponent component) {
            for (int index = 0; index < ClipStates.Count; index++) {
                if (ReferenceEquals(ClipStates[index].Component, component)) { return; }
            }
            ClipStates.Add(new ViewportClipState(component));
        }
        /// <summary>Captures a new InteractableComponent identity without replacing an existing authored baseline.</summary>
        void CaptureInteractable([NativeRetainsBorrow] InteractableComponent component) {
            for (int index = 0; index < InteractableStates.Count; index++) {
                if (ReferenceEquals(InteractableStates[index].Component, component)) { return; }
            }
            InteractableStates.Add(new ViewportInteractableState(component));
        }
        /// <summary>Captures a new ScrollComponent identity without replacing an existing authored baseline.</summary>
        void CaptureScroll([NativeRetainsBorrow] ScrollComponent component) {
            for (int index = 0; index < ScrollStates.Count; index++) {
                if (ReferenceEquals(ScrollStates[index].Component, component)) { return; }
            }
            ScrollStates.Add(new ViewportScrollState(component));
        }
        /// <summary>Captures a new FPSComponent identity without replacing an existing authored baseline.</summary>
        void CaptureFps([NativeRetainsBorrow] FPSComponent component) {
            for (int index = 0; index < FpsStates.Count; index++) {
                if (ReferenceEquals(FpsStates[index].Component, component)) { return; }
            }
            FpsStates.Add(new ViewportFpsState(component));
        }
        /// <summary>Reapplies absolute scaling from retained originals without scene discovery.</summary>
        public void Apply(in ViewportLayoutMetrics metrics) {
            for (int index = 0; index < AnchorStates.Count; index++) {
                AnchorStates[index].Restore();
                ViewportLayoutApplier.ApplyComponent(AnchorStates[index].Component, metrics);
            }
            for (int index = 0; index < RectangleStates.Count; index++) {
                RectangleStates[index].Restore();
                ViewportLayoutApplier.ApplyComponent(RectangleStates[index].Component, metrics);
            }
            for (int index = 0; index < TextStates.Count; index++) {
                TextStates[index].Restore();
                ViewportLayoutApplier.ApplyComponent(TextStates[index].Component, metrics);
            }
            for (int index = 0; index < SpriteStates.Count; index++) {
                SpriteStates[index].Restore();
                ViewportLayoutApplier.ApplyComponent(SpriteStates[index].Component, metrics);
            }
            for (int index = 0; index < ClipStates.Count; index++) {
                ClipStates[index].Restore();
                ViewportLayoutApplier.ApplyComponent(ClipStates[index].Component, metrics);
            }
            for (int index = 0; index < InteractableStates.Count; index++) {
                InteractableStates[index].Restore();
                ViewportLayoutApplier.ApplyComponent(InteractableStates[index].Component, metrics);
            }
            for (int index = 0; index < ScrollStates.Count; index++) {
                ScrollStates[index].Restore();
                ViewportLayoutApplier.ApplyComponent(ScrollStates[index].Component, metrics);
            }
            for (int index = 0; index < FpsStates.Count; index++) {
                FpsStates[index].Restore();
                ViewportLayoutApplier.ApplyComponent(FpsStates[index].Component, metrics);
            }
            for (int index = 0; index < Positions.Count; index++) {
                ViewportPositionState state = Positions[index];
                ViewportLayoutMetrics localMetrics = metrics;
                if (!ReferenceEquals(state.Entity, Root)) { localMetrics.Origin = new float2(0f, 0f); }
                state.Entity.LocalPosition = ViewportLayoutApplier.ResolvePosition(state.LocalPosition, localMetrics);
            }
        }
        /// <summary>Restores original component values before original positions.</summary>
        public void RestoreAuthoredLayout() {
            for (int index = 0; index < AnchorStates.Count; index++) { AnchorStates[index].Restore(); }
            for (int index = 0; index < RectangleStates.Count; index++) { RectangleStates[index].Restore(); }
            for (int index = 0; index < TextStates.Count; index++) { TextStates[index].Restore(); }
            for (int index = 0; index < SpriteStates.Count; index++) { SpriteStates[index].Restore(); }
            for (int index = 0; index < ClipStates.Count; index++) { ClipStates[index].Restore(); }
            for (int index = 0; index < InteractableStates.Count; index++) { InteractableStates[index].Restore(); }
            for (int index = 0; index < ScrollStates.Count; index++) { ScrollStates[index].Restore(); }
            for (int index = 0; index < FpsStates.Count; index++) { FpsStates[index].Restore(); }
            for (int index = 0; index < Positions.Count; index++) { Positions[index].Restore(); }
        }
        /// <summary>Restores editor originals while absorbing edits relative to the last applied typed values.</summary>
        public void RestoreEdited(ViewportLayoutCache applied, in ViewportLayoutMetrics metrics) {
            for (int index = 0; index < AnchorStates.Count; index++) {
                ViewportAnchorState state = AnchorStates[index];
                for (int other = 0; other < applied.AnchorStates.Count; other++) {
                    if (ReferenceEquals(state.Component, applied.AnchorStates[other].Component)) {
                        state.RestoreEdited(applied.AnchorStates[other], metrics);
                        AnchorStates[index] = new ViewportAnchorState(state.Component);
                        break;
                    }
                }
            }
            for (int index = 0; index < RectangleStates.Count; index++) {
                ViewportRectangleState state = RectangleStates[index];
                for (int other = 0; other < applied.RectangleStates.Count; other++) {
                    if (ReferenceEquals(state.Component, applied.RectangleStates[other].Component)) {
                        state.RestoreEdited(applied.RectangleStates[other], metrics);
                        RectangleStates[index] = new ViewportRectangleState(state.Component);
                        break;
                    }
                }
            }
            for (int index = 0; index < TextStates.Count; index++) {
                ViewportTextState state = TextStates[index];
                for (int other = 0; other < applied.TextStates.Count; other++) {
                    if (ReferenceEquals(state.Component, applied.TextStates[other].Component)) {
                        state.RestoreEdited(applied.TextStates[other], metrics);
                        TextStates[index] = new ViewportTextState(state.Component);
                        break;
                    }
                }
            }
            for (int index = 0; index < SpriteStates.Count; index++) {
                ViewportSpriteState state = SpriteStates[index];
                for (int other = 0; other < applied.SpriteStates.Count; other++) {
                    if (ReferenceEquals(state.Component, applied.SpriteStates[other].Component)) {
                        state.RestoreEdited(applied.SpriteStates[other], metrics);
                        SpriteStates[index] = new ViewportSpriteState(state.Component);
                        break;
                    }
                }
            }
            for (int index = 0; index < ClipStates.Count; index++) {
                ViewportClipState state = ClipStates[index];
                for (int other = 0; other < applied.ClipStates.Count; other++) {
                    if (ReferenceEquals(state.Component, applied.ClipStates[other].Component)) {
                        state.RestoreEdited(applied.ClipStates[other], metrics);
                        ClipStates[index] = new ViewportClipState(state.Component);
                        break;
                    }
                }
            }
            for (int index = 0; index < InteractableStates.Count; index++) {
                ViewportInteractableState state = InteractableStates[index];
                for (int other = 0; other < applied.InteractableStates.Count; other++) {
                    if (ReferenceEquals(state.Component, applied.InteractableStates[other].Component)) {
                        state.RestoreEdited(applied.InteractableStates[other], metrics);
                        InteractableStates[index] = new ViewportInteractableState(state.Component);
                        break;
                    }
                }
            }
            for (int index = 0; index < ScrollStates.Count; index++) {
                ViewportScrollState state = ScrollStates[index];
                for (int other = 0; other < applied.ScrollStates.Count; other++) {
                    if (ReferenceEquals(state.Component, applied.ScrollStates[other].Component)) {
                        state.RestoreEdited(applied.ScrollStates[other], metrics);
                        ScrollStates[index] = new ViewportScrollState(state.Component);
                        break;
                    }
                }
            }
            for (int index = 0; index < FpsStates.Count; index++) {
                ViewportFpsState state = FpsStates[index];
                for (int other = 0; other < applied.FpsStates.Count; other++) {
                    if (ReferenceEquals(state.Component, applied.FpsStates[other].Component)) {
                        state.RestoreEdited(applied.FpsStates[other], metrics);
                        FpsStates[index] = new ViewportFpsState(state.Component);
                        break;
                    }
                }
            }
            for (int index = 0; index < Positions.Count; index++) {
                ViewportPositionState state = Positions[index];
                for (int other = 0; other < applied.Positions.Count; other++) {
                    if (ReferenceEquals(state.Entity, applied.Positions[other].Entity)) {
                        float2 origin = ReferenceEquals(state.Entity, Root) ? metrics.Origin : new float2(0f, 0f);
                        state.RestoreEdited(applied.Positions[other], metrics, origin);
                        Positions[index] = new ViewportPositionState(state.Entity);
                        break;
                    }
                }
            }
        }
        /// <summary>Removes and restores a borrowed participant before its native lifetime ends.</summary>
        public void Remove(Component component) {
            for (int index = AnchorStates.Count - 1; index >= 0; index--) {
                if (ReferenceEquals(AnchorStates[index].Component, component)) {
                    AnchorStates[index].Restore();
                    AnchorStates.RemoveAt(index);
                }
            }
            for (int index = RectangleStates.Count - 1; index >= 0; index--) {
                if (ReferenceEquals(RectangleStates[index].Component, component)) {
                    RectangleStates[index].Restore();
                    RectangleStates.RemoveAt(index);
                }
            }
            for (int index = TextStates.Count - 1; index >= 0; index--) {
                if (ReferenceEquals(TextStates[index].Component, component)) {
                    TextStates[index].Restore();
                    TextStates.RemoveAt(index);
                }
            }
            for (int index = SpriteStates.Count - 1; index >= 0; index--) {
                if (ReferenceEquals(SpriteStates[index].Component, component)) {
                    SpriteStates[index].Restore();
                    SpriteStates.RemoveAt(index);
                }
            }
            for (int index = ClipStates.Count - 1; index >= 0; index--) {
                if (ReferenceEquals(ClipStates[index].Component, component)) {
                    ClipStates[index].Restore();
                    ClipStates.RemoveAt(index);
                }
            }
            for (int index = InteractableStates.Count - 1; index >= 0; index--) {
                if (ReferenceEquals(InteractableStates[index].Component, component)) {
                    InteractableStates[index].Restore();
                    InteractableStates.RemoveAt(index);
                }
            }
            for (int index = ScrollStates.Count - 1; index >= 0; index--) {
                if (ReferenceEquals(ScrollStates[index].Component, component)) {
                    ScrollStates[index].Restore();
                    ScrollStates.RemoveAt(index);
                }
            }
            for (int index = FpsStates.Count - 1; index >= 0; index--) {
                if (ReferenceEquals(FpsStates[index].Component, component)) {
                    FpsStates[index].Restore();
                    FpsStates.RemoveAt(index);
                }
            }
        }
        /// <summary>Restores and drops a departing subtree before reparenting or native destruction.</summary>
        public void Remove(Entity subtree) {
            if (subtree.Components != null) {
                for (int index = 0; index < subtree.Components.Count; index++) { Remove(subtree.Components[index]); }
            }
            for (int index = Positions.Count - 1; index >= 0; index--) {
                if (ReferenceEquals(Positions[index].Entity, subtree)) { Positions[index].Restore(); Positions.RemoveAt(index); }
            }
            if (subtree.Children != null) {
                for (int index = 0; index < subtree.Children.Count; index++) { Remove(subtree.Children[index]); }
            }
        }
        /// <summary>Recaptures values after editor-only authored reanchoring without rediscovering participants.</summary>
        public void RecaptureValues() {
            for (int index = 0; index < AnchorStates.Count; index++) {
                AnchorStates[index] = new ViewportAnchorState(AnchorStates[index].Component);
            }
            for (int index = 0; index < RectangleStates.Count; index++) {
                RectangleStates[index] = new ViewportRectangleState(RectangleStates[index].Component);
            }
            for (int index = 0; index < TextStates.Count; index++) {
                TextStates[index] = new ViewportTextState(TextStates[index].Component);
            }
            for (int index = 0; index < SpriteStates.Count; index++) {
                SpriteStates[index] = new ViewportSpriteState(SpriteStates[index].Component);
            }
            for (int index = 0; index < ClipStates.Count; index++) {
                ClipStates[index] = new ViewportClipState(ClipStates[index].Component);
            }
            for (int index = 0; index < InteractableStates.Count; index++) {
                InteractableStates[index] = new ViewportInteractableState(InteractableStates[index].Component);
            }
            for (int index = 0; index < ScrollStates.Count; index++) {
                ScrollStates[index] = new ViewportScrollState(ScrollStates[index].Component);
            }
            for (int index = 0; index < FpsStates.Count; index++) {
                FpsStates[index] = new ViewportFpsState(FpsStates[index].Component);
            }
            for (int index = 0; index < Positions.Count; index++) { Positions[index] = new ViewportPositionState(Positions[index].Entity); }
        }

        /// <summary>Drops all borrowed references and native buffer storage without destroying participants.</summary>
        public void Release() {
            AnchorStates.Clear();
            NativeOwnership.Delete(AnchorStates);
            RectangleStates.Clear();
            NativeOwnership.Delete(RectangleStates);
            TextStates.Clear();
            NativeOwnership.Delete(TextStates);
            SpriteStates.Clear();
            NativeOwnership.Delete(SpriteStates);
            ClipStates.Clear();
            NativeOwnership.Delete(ClipStates);
            InteractableStates.Clear();
            NativeOwnership.Delete(InteractableStates);
            ScrollStates.Clear();
            NativeOwnership.Delete(ScrollStates);
            FpsStates.Clear();
            NativeOwnership.Delete(FpsStates);
            Positions.Clear();
            NativeOwnership.Delete(Positions);
            Root = null;
        }
    }
}
#endif
