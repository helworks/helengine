namespace helengine {
    /// <summary>
    /// Resolves WinForms-style anchored layout against a selectable layout space and can expose the entity's own bounds to child layout components.
    /// </summary>
    public class LayoutComponent : Component, IAnchorBoundsProvider {
        /// <summary>
        /// Bit flag used to mark a left-edge anchor.
        /// </summary>
        public const byte LeftAnchorFlag = 1;

        /// <summary>
        /// Bit flag used to mark a right-edge anchor.
        /// </summary>
        public const byte RightAnchorFlag = 2;

        /// <summary>
        /// Bit flag used to mark a top-edge anchor.
        /// </summary>
        public const byte TopAnchorFlag = 4;

        /// <summary>
        /// Bit flag used to mark a bottom-edge anchor.
        /// </summary>
        public const byte BottomAnchorFlag = 8;

        /// <summary>
        /// Layout space mode that preserves the legacy nearest-provider behavior.
        /// </summary>
        public const byte InheritedLayoutSpace = 0;

        /// <summary>
        /// Layout space mode that resolves against the immediate parent layout rect.
        /// </summary>
        public const byte ParentLayoutRectSpace = 1;

        /// <summary>
        /// Layout space mode that resolves against the nearest reference-canvas fit provider.
        /// </summary>
        public const byte ReferenceCanvasLayoutSpace = 2;

        /// <summary>
        /// Layout space mode that resolves against the nearest viewport provider.
        /// </summary>
        public const byte CameraViewportLayoutSpace = 3;

        /// <summary>
        /// Stores the selected anchor edge flags.
        /// </summary>
        public byte AnchorFlags { get; set; }

        /// <summary>
        /// Stores the active anchor distances in left, right, top, bottom order.
        /// </summary>
        public float4 AnchorDistances { get; set; }

        /// <summary>
        /// Stores which layout space this component should answer to.
        /// </summary>
        byte LayoutSpaceValue;

        /// <summary>
        /// Tracks the currently subscribed ancestor bounds provider.
        /// </summary>
        IAnchorBoundsProvider anchorBoundsProvider;

        /// <summary>
        /// Tracks whether the component is currently subscribed to the fallback window resize event.
        /// </summary>
        bool IsSubscribedToWindowResize;

        /// <summary>
        /// Reused fallback anchor space returned when no explicit provider exists.
        /// </summary>
        readonly AnchorSpace FallbackAnchorSpaceValue;

        /// <summary>
        /// Reused direct-parent anchor space returned when no parent layout component exists but the parent exposes a concrete size.
        /// </summary>
        readonly AnchorSpace ParentAnchorSpaceValue;

        /// <summary>
        /// Reused child-facing anchor space that reports this entity's resolved layout rect.
        /// </summary>
        readonly AnchorSpace ChildAnchorSpaceValue;

        /// <summary>
        /// Tracks the last child-facing anchor size published by this layout component.
        /// </summary>
        int2 LastChildAnchorSizeValue;

        /// <summary>
        /// Raised when the resolved child-facing layout bounds change.
        /// </summary>
        public event Action AnchorBoundsChanged;

        /// <summary>
        /// Initializes a layout component with reusable anchor-space records.
        /// </summary>
        public LayoutComponent() {
            LayoutSpaceValue = InheritedLayoutSpace;
            FallbackAnchorSpaceValue = new AnchorSpace(new int2(0, 0), new float2(0f, 0f));
            ParentAnchorSpaceValue = new AnchorSpace(new int2(0, 0), new float2(0f, 0f));
            ChildAnchorSpaceValue = new AnchorSpace(new int2(0, 0), new float2(0f, 0f));
            LastChildAnchorSizeValue = new int2(-1, -1);
        }

        /// <summary>
        /// Gets or sets which layout space this component should answer to.
        /// </summary>
        public byte LayoutSpace {
            get { return LayoutSpaceValue; }
            set {
                if (LayoutSpaceValue != value) {
                    LayoutSpaceValue = value;
                    if (Parent != null) {
                        RefreshSubscriptions();
                        RefreshAnchoring();
                    }
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether any anchors are currently active.
        /// </summary>
        public bool IsAnchored => AnchorFlags != 0;

        /// <summary>
        /// Gets the child-facing layout bounds published by this component.
        /// </summary>
        public AnchorSpace AnchorSpace {
            get {
                int2 currentSize = GetAnchorSize();
                ChildAnchorSpaceValue.Update(currentSize, new float2(0f, 0f));
                return ChildAnchorSpaceValue;
            }
        }

        /// <summary>
        /// Enables anchoring with specific sides and captures the current distances from the selected layout space.
        /// </summary>
        /// <param name="left">Anchor to the left edge of the layout space.</param>
        /// <param name="right">Anchor to the right edge of the layout space.</param>
        /// <param name="top">Anchor to the top edge of the layout space.</param>
        /// <param name="bottom">Anchor to the bottom edge of the layout space.</param>
        public void EnableAnchoring(bool left = false, bool right = false, bool top = false, bool bottom = false) {
            if (!left && !right && !top && !bottom) {
                DisableAnchoring();
                return;
            }

            if (Parent == null) {
                throw new InvalidOperationException("LayoutComponent must be attached before anchoring can be enabled.");
            }

            RefreshSubscriptions();
            AnchorSpace anchorSpace = GetAnchorSpace();
            int2 anchoredSize = GetAnchorSize();
            float3 localPosition = Parent.LocalPosition;

            byte anchorFlags = 0;
            float4 anchorDistances = new float4(0f, 0f, 0f, 0f);

            if (left) {
                anchorFlags |= LeftAnchorFlag;
                anchorDistances.X = localPosition.X - anchorSpace.Origin.X;
            }
            if (right) {
                anchorFlags |= RightAnchorFlag;
                anchorDistances.Y = anchorSpace.Size.X - (localPosition.X - anchorSpace.Origin.X) - anchoredSize.X;
            }
            if (top) {
                anchorFlags |= TopAnchorFlag;
                anchorDistances.Z = localPosition.Y - anchorSpace.Origin.Y;
            }
            if (bottom) {
                anchorFlags |= BottomAnchorFlag;
                anchorDistances.W = anchorSpace.Size.Y - (localPosition.Y - anchorSpace.Origin.Y) - anchoredSize.Y;
            }

            AnchorFlags = anchorFlags;
            AnchorDistances = anchorDistances;

            if (Core.Instance != null && Core.Instance.RenderManager3D != null) {
                RefreshSubscriptions();
                RefreshAnchoring();
            }
        }

        /// <summary>
        /// Disables anchoring and stops responding to layout changes.
        /// </summary>
        public void DisableAnchoring() {
            DetachFromBoundsProvider();
            DetachFromWindowResize();
            AnchorFlags = 0;
            AnchorDistances = new float4(0f, 0f, 0f, 0f);
            PublishOwnAnchorBoundsIfNeeded();
        }

        /// <summary>
        /// Sets anchor distances manually using the selected layout space as the reference frame.
        /// </summary>
        /// <param name="left">Distance from the left edge of the layout space.</param>
        /// <param name="right">Distance from the right edge of the layout space.</param>
        /// <param name="top">Distance from the top edge of the layout space.</param>
        /// <param name="bottom">Distance from the bottom edge of the layout space.</param>
        public void SetAnchorDistances(Nullable<float> left = null, Nullable<float> right = null, Nullable<float> top = null, Nullable<float> bottom = null) {
            if (!left.HasValue && !right.HasValue && !top.HasValue && !bottom.HasValue) {
                DisableAnchoring();
                return;
            }

            byte anchorFlags = 0;
            float4 anchorDistances = new float4(0f, 0f, 0f, 0f);
            if (left.HasValue) {
                anchorFlags |= LeftAnchorFlag;
                anchorDistances.X = left.Value;
            }
            if (right.HasValue) {
                anchorFlags |= RightAnchorFlag;
                anchorDistances.Y = right.Value;
            }
            if (top.HasValue) {
                anchorFlags |= TopAnchorFlag;
                anchorDistances.Z = top.Value;
            }
            if (bottom.HasValue) {
                anchorFlags |= BottomAnchorFlag;
                anchorDistances.W = bottom.Value;
            }

            AnchorFlags = anchorFlags;
            AnchorDistances = anchorDistances;

            if (Parent != null && Core.Instance != null && Core.Instance.RenderManager3D != null) {
                RefreshSubscriptions();
                RefreshAnchoring();
            }
        }

        /// <summary>
        /// Refreshes the anchored position and any stretch-driven size changes from the current layout space.
        /// </summary>
        public void RefreshAnchoring() {
            if (!IsAnchored || Parent == null) {
                PublishOwnAnchorBoundsIfNeeded();
                return;
            }

            RefreshSubscriptions();

            AnchorSpace anchorSpace = GetAnchorSpace();
            int2 currentSize = GetAnchorSize();
            int targetWidth = currentSize.X;
            int targetHeight = currentSize.Y;
            float3 localPosition = Parent.LocalPosition;

            bool hasLeft = (AnchorFlags & LeftAnchorFlag) != 0;
            bool hasRight = (AnchorFlags & RightAnchorFlag) != 0;
            bool hasTop = (AnchorFlags & TopAnchorFlag) != 0;
            bool hasBottom = (AnchorFlags & BottomAnchorFlag) != 0;

            if (hasLeft && hasRight) {
                targetWidth = Math.Max(0, (int)Math.Round(anchorSpace.Size.X - AnchorDistances.X - AnchorDistances.Y));
                localPosition.X = anchorSpace.Origin.X + AnchorDistances.X;
            } else if (hasLeft) {
                localPosition.X = anchorSpace.Origin.X + AnchorDistances.X;
            } else if (hasRight) {
                localPosition.X = anchorSpace.Origin.X + anchorSpace.Size.X - AnchorDistances.Y - currentSize.X;
            }

            if (hasTop && hasBottom) {
                targetHeight = Math.Max(0, (int)Math.Round(anchorSpace.Size.Y - AnchorDistances.Z - AnchorDistances.W));
                localPosition.Y = anchorSpace.Origin.Y + AnchorDistances.Z;
            } else if (hasTop) {
                localPosition.Y = anchorSpace.Origin.Y + AnchorDistances.Z;
            } else if (hasBottom) {
                localPosition.Y = anchorSpace.Origin.Y + anchorSpace.Size.Y - AnchorDistances.W - currentSize.Y;
            }

            ApplyResolvedSize(new int2(targetWidth, targetHeight));
            Parent.LocalPosition = localPosition;
            PublishOwnAnchorBoundsIfNeeded();
        }

        /// <summary>
        /// Rebinds to the selected layout-space provider when the component is attached to an entity.
        /// </summary>
        /// <param name="entity">Entity receiving the component.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            PublishOwnAnchorBoundsIfNeeded();
            if (IsAnchored) {
                RefreshSubscriptions();
                RefreshAnchoring();
            }
        }

        /// <summary>
        /// Cleans up layout subscriptions when the component is removed from its parent entity.
        /// </summary>
        /// <param name="entity">Entity the component was attached to.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            DisableAnchoring();
        }

        /// <summary>
        /// Releases the reusable anchor-space records owned by this component.
        /// </summary>
        public override void Dispose() {
            NativeOwnership.Delete(FallbackAnchorSpaceValue);
            NativeOwnership.Delete(ParentAnchorSpaceValue);
            NativeOwnership.Delete(ChildAnchorSpaceValue);
            base.Dispose();
        }

        /// <summary>
        /// Rebinds subscriptions when the parent entity changes enabled state.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (!IsAnchored) {
                PublishOwnAnchorBoundsIfNeeded();
                return;
            }

            if (newEnabled) {
                RefreshSubscriptions();
                RefreshAnchoring();
            } else {
                DetachFromBoundsProvider();
                DetachFromWindowResize();
            }
        }

        /// <summary>
        /// Handles upstream layout-bounds changes by recomputing this entity's anchored rect.
        /// </summary>
        void HandleAnchorBoundsChanged() {
            RefreshAnchoring();
        }

        /// <summary>
        /// Handles fallback window resize notifications when no explicit provider exists.
        /// </summary>
        /// <param name="handle">Window handle reported by the render manager.</param>
        /// <param name="newWidth">Updated host width.</param>
        /// <param name="newHeight">Updated host height.</param>
        void HandleWindowResized(IntPtr handle, int newWidth, int newHeight) {
            RefreshAnchoring();
        }

        /// <summary>
        /// Rebinds the current provider subscriptions for the selected layout space.
        /// </summary>
        void RefreshSubscriptions() {
            IAnchorBoundsProvider newProvider = ResolveAnchorBoundsProvider();

            if (!ReferenceEquals(anchorBoundsProvider, newProvider)) {
                DetachFromBoundsProvider();
                anchorBoundsProvider = newProvider;

                if (anchorBoundsProvider != null) {
                    anchorBoundsProvider.AnchorBoundsChanged += HandleAnchorBoundsChanged;
                }
            }

            if (anchorBoundsProvider == null && LayoutSpaceValue != ParentLayoutRectSpace) {
                AttachToWindowResize();
            } else {
                DetachFromWindowResize();
            }
        }

        /// <summary>
        /// Disconnects the current bounds-provider subscription.
        /// </summary>
        void DetachFromBoundsProvider() {
            if (anchorBoundsProvider != null) {
                anchorBoundsProvider.AnchorBoundsChanged -= HandleAnchorBoundsChanged;
                anchorBoundsProvider = null;
            }
        }

        /// <summary>
        /// Connects the fallback window-resize subscription when it is not already active.
        /// </summary>
        void AttachToWindowResize() {
            if (IsSubscribedToWindowResize) {
                return;
            }
            if (Core.Instance == null || Core.Instance.RenderManager3D == null) {
                return;
            }

            Core.Instance.RenderManager3D.WindowResized += HandleWindowResized;
            IsSubscribedToWindowResize = true;
        }

        /// <summary>
        /// Disconnects the fallback window-resize subscription when it is active.
        /// </summary>
        void DetachFromWindowResize() {
            if (!IsSubscribedToWindowResize) {
                return;
            }

            Core.Instance.RenderManager3D.WindowResized -= HandleWindowResized;
            IsSubscribedToWindowResize = false;
        }

        /// <summary>
        /// Resolves the selected ancestor bounds provider for the configured layout-space mode.
        /// </summary>
        /// <returns>Resolved provider when one exists; otherwise null.</returns>
        IAnchorBoundsProvider ResolveAnchorBoundsProvider() {
            if (Parent == null) {
                return null;
            }

            if (LayoutSpaceValue == ParentLayoutRectSpace) {
                return ResolveImmediateParentLayoutProvider();
            }
            if (LayoutSpaceValue == ReferenceCanvasLayoutSpace) {
                return ResolveAncestorComponentProvider(typeof(ReferenceCanvasFitComponent));
            }
            if (LayoutSpaceValue == CameraViewportLayoutSpace) {
                return ResolveAncestorComponentProvider(typeof(ViewportComponent));
            }

            return ResolveInheritedBoundsProvider();
        }

        /// <summary>
        /// Resolves the active layout space in local pixels.
        /// </summary>
        /// <returns>Resolved anchor space in local pixels.</returns>
        AnchorSpace GetAnchorSpace() {
            if (LayoutSpaceValue == ParentLayoutRectSpace) {
                if (anchorBoundsProvider != null) {
                    return anchorBoundsProvider.AnchorSpace;
                }

                int2 parentSize = ResolveImmediateParentAnchorSize();
                ParentAnchorSpaceValue.Update(parentSize, new float2(0f, 0f));
                return ParentAnchorSpaceValue;
            }

            if (anchorBoundsProvider != null) {
                return anchorBoundsProvider.AnchorSpace;
            }

            FallbackAnchorSpaceValue.Update(Core.Instance.RenderManager3D.MainWindowSize, new float2(0f, 0f));
            return FallbackAnchorSpaceValue;
        }

        /// <summary>
        /// Resolves the nearest provider in the legacy inherited mode.
        /// </summary>
        /// <returns>Nearest ancestor bounds provider when one exists; otherwise null.</returns>
        IAnchorBoundsProvider ResolveInheritedBoundsProvider() {
            Entity current = Parent != null ? Parent.Parent : null;

            while (current != null) {
                if (current.Components != null) {
                    for (int componentIndex = current.Components.Count - 1; componentIndex >= 0; componentIndex--) {
                        if (current.Components[componentIndex] is IAnchorBoundsProvider componentProvider) {
                            return componentProvider;
                        }
                    }
                }

                if (current is IAnchorBoundsProvider provider) {
                    return provider;
                }

                current = current.Parent;
            }

            return null;
        }

        /// <summary>
        /// Resolves the first matching provider component from the current ancestor chain.
        /// </summary>
        /// <param name="providerType">Provider type that should be found.</param>
        /// <returns>Resolved provider when found; otherwise null.</returns>
        IAnchorBoundsProvider ResolveAncestorComponentProvider(Type providerType) {
            if (providerType == null) {
                throw new ArgumentNullException(nameof(providerType));
            }

            Entity current = Parent != null ? Parent.Parent : null;
            while (current != null) {
                if (current.Components != null) {
                    for (int componentIndex = 0; componentIndex < current.Components.Count; componentIndex++) {
                        Component component = current.Components[componentIndex];
                        if (providerType.IsInstanceOfType(component) && component is IAnchorBoundsProvider provider) {
                            return provider;
                        }
                    }
                }

                current = current.Parent;
            }

            return null;
        }

        /// <summary>
        /// Resolves the immediate parent layout provider when one exists.
        /// </summary>
        /// <returns>Layout provider attached to the immediate parent entity when found; otherwise null.</returns>
        IAnchorBoundsProvider ResolveImmediateParentLayoutProvider() {
            Entity parentEntity = Parent != null ? Parent.Parent : null;
            if (parentEntity == null || parentEntity.Components == null) {
                return null;
            }

            for (int componentIndex = 0; componentIndex < parentEntity.Components.Count; componentIndex++) {
                if (parentEntity.Components[componentIndex] is LayoutComponent layoutComponent) {
                    return layoutComponent;
                }
            }

            if (parentEntity is IAnchorBoundsProvider provider) {
                return provider;
            }

            return null;
        }

        /// <summary>
        /// Resolves the current size of the layout host when it exposes one.
        /// </summary>
        /// <returns>Current size in local pixels, or zero when no size provider exists.</returns>
        int2 GetAnchorSize() {
            if (Parent == null) {
                return new int2(0, 0);
            }

            IAnchorSizeProvider bestProvider = null;
            int bestArea = -1;

            if (Parent is IAnchorSizeProvider parentProvider) {
                bestProvider = parentProvider;
                bestArea = GetAnchorArea(parentProvider.AnchorSize);
            }

            for (int componentIndex = 0; componentIndex < Parent.Components.Count; componentIndex++) {
                if (Parent.Components[componentIndex] is IAnchorSizeProvider sizeProvider) {
                    int area = GetAnchorArea(sizeProvider.AnchorSize);
                    if (area > bestArea) {
                        bestProvider = sizeProvider;
                        bestArea = area;
                    }
                }
            }

            if (bestProvider == null) {
                return new int2(0, 0);
            }

            return bestProvider.AnchorSize;
        }

        /// <summary>
        /// Resolves the immediate parent size that should be treated as the parent layout rect when no parent layout provider exists.
        /// </summary>
        /// <returns>Immediate parent size in local pixels, or zero when no size provider exists.</returns>
        int2 ResolveImmediateParentAnchorSize() {
            Entity parentEntity = Parent != null ? Parent.Parent : null;
            if (parentEntity == null) {
                return new int2(0, 0);
            }

            if (parentEntity is IAnchorSizeProvider parentProvider) {
                return parentProvider.AnchorSize;
            }

            if (parentEntity.Components == null) {
                return new int2(0, 0);
            }

            IAnchorSizeProvider bestProvider = null;
            int bestArea = -1;
            for (int componentIndex = 0; componentIndex < parentEntity.Components.Count; componentIndex++) {
                if (parentEntity.Components[componentIndex] is IAnchorSizeProvider sizeProvider) {
                    int area = GetAnchorArea(sizeProvider.AnchorSize);
                    if (area > bestArea) {
                        bestProvider = sizeProvider;
                        bestArea = area;
                    }
                }
            }

            return bestProvider != null ? bestProvider.AnchorSize : new int2(0, 0);
        }

        /// <summary>
        /// Applies one resolved size to supported 2D components hosted on the same entity.
        /// </summary>
        /// <param name="resolvedSize">Resolved layout size in local pixels.</param>
        void ApplyResolvedSize(int2 resolvedSize) {
            if (Parent == null || Parent.Components == null) {
                return;
            }

            for (int componentIndex = 0; componentIndex < Parent.Components.Count; componentIndex++) {
                Component component = Parent.Components[componentIndex];
                if (component is RoundedRectComponent roundedRectComponent) {
                    roundedRectComponent.Size = resolvedSize;
                } else if (component is SpriteComponent spriteComponent) {
                    spriteComponent.Size = resolvedSize;
                } else if (component is TextComponent textComponent) {
                    textComponent.Size = resolvedSize;
                } else if (component is ClipRectComponent clipRectComponent) {
                    clipRectComponent.Size = resolvedSize;
                } else if (component is InteractableComponent interactableComponent) {
                    interactableComponent.Size = resolvedSize;
                } else if (component is ScrollComponent scrollComponent) {
                    scrollComponent.Size = resolvedSize;
                } else if (component is ComboBoxComponent comboBoxComponent) {
                    comboBoxComponent.Size = resolvedSize;
                } else if (component is TextBoxComponent textBoxComponent) {
                    textBoxComponent.Size = resolvedSize;
                } else if (component is CheckBoxComponent checkBoxComponent) {
                    checkBoxComponent.Size = resolvedSize;
                }
            }
        }

        /// <summary>
        /// Raises child-layout bounds changes only when the entity's resolved size changed.
        /// </summary>
        void PublishOwnAnchorBoundsIfNeeded() {
            int2 currentSize = GetAnchorSize();
            if (currentSize.X == LastChildAnchorSizeValue.X && currentSize.Y == LastChildAnchorSizeValue.Y) {
                return;
            }

            LastChildAnchorSizeValue = currentSize;
            ChildAnchorSpaceValue.Update(currentSize, new float2(0f, 0f));
            if (AnchorBoundsChanged != null) {
                AnchorBoundsChanged();
            }
        }

        /// <summary>
        /// Computes one size area used to select the most specific size provider.
        /// </summary>
        /// <param name="size">Size used to compare providers.</param>
        /// <returns>Signed area used to compare size providers.</returns>
        int GetAnchorArea(int2 size) {
            if (size.X < 0 || size.Y < 0) {
                return -1;
            }

            return size.X * size.Y;
        }

        /// <summary>
        /// Gets current anchor configuration as a readable string.
        /// </summary>
        /// <returns>Readable description of the active anchor distances.</returns>
        public string GetAnchorInfo() {
            if (!IsAnchored) {
                return "Not anchored";
            }

            List<string> anchors = new List<string>();
            if ((AnchorFlags & LeftAnchorFlag) != 0) {
                anchors.Add($"Left ({AnchorDistances.X:F1}px)");
            }
            if ((AnchorFlags & RightAnchorFlag) != 0) {
                anchors.Add($"Right ({AnchorDistances.Y:F1}px)");
            }
            if ((AnchorFlags & TopAnchorFlag) != 0) {
                anchors.Add($"Top ({AnchorDistances.Z:F1}px)");
            }
            if ((AnchorFlags & BottomAnchorFlag) != 0) {
                anchors.Add($"Bottom ({AnchorDistances.W:F1}px)");
            }

            return "Anchored to: " + string.Join(", ", anchors);
        }
    }
}
