namespace helengine {
    /// <summary>
    /// Keeps an entity pinned to a parent layout bounds provider and updates its local position when that layout changes.
    /// </summary>
    public class AnchorComponent : Component {
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
        /// Stores the active anchor edge flags.
        /// </summary>
        public byte AnchorFlags { get; set; }

        /// <summary>
        /// Stores the active anchor distances in left, right, top, bottom order.
        /// </summary>
        public float4 AnchorDistances { get; set; }

        /// <summary>
        /// Tracks the current ancestor bounds provider used to resolve parent-relative anchors.
        /// </summary>
        IAnchorBoundsProvider anchorBoundsProvider;

        /// <summary>
        /// Tracks whether the component is currently subscribed to the window resize fallback.
        /// </summary>
        bool IsSubscribedToWindowResize;

        /// <summary>
        /// Gets a value indicating whether anchoring is currently enabled.
        /// </summary>
        public bool IsAnchored => AnchorFlags != 0;

        /// <summary>
        /// Enables anchoring with specific sides. The entity will maintain its distance from the resolved parent bounds.
        /// </summary>
        /// <param name="left">Anchor to the left edge of the parent bounds.</param>
        /// <param name="right">Anchor to the right edge of the parent bounds.</param>
        /// <param name="top">Anchor to the top edge of the parent bounds.</param>
        /// <param name="bottom">Anchor to the bottom edge of the parent bounds.</param>
        public void EnableAnchoring(bool left = false, bool right = false, bool top = false, bool bottom = false) {
            if (!left && !right && !top && !bottom) {
                DisableAnchoring();
                return;
            }

            if (Parent == null) {
                throw new InvalidOperationException("AnchorComponent must be attached before anchoring can be enabled.");
            }

            RefreshSubscriptions();
            int2 anchorBounds = GetAnchorBounds();
            int2 anchoredSize = GetAnchorSize();
            float3 localPosition = Parent.LocalPosition;

            byte anchorFlags = 0;
            float4 anchorDistances = new float4(0f, 0f, 0f, 0f);

            if (left) {
                anchorFlags |= LeftAnchorFlag;
                anchorDistances.X = localPosition.X;
            }
            if (right) {
                anchorFlags |= RightAnchorFlag;
                anchorDistances.Y = anchorBounds.X - localPosition.X - anchoredSize.X;
            }
            if (top) {
                anchorFlags |= TopAnchorFlag;
                anchorDistances.Z = localPosition.Y;
            }
            if (bottom) {
                anchorFlags |= BottomAnchorFlag;
                anchorDistances.W = anchorBounds.Y - localPosition.Y - anchoredSize.Y;
            }

            AnchorFlags = anchorFlags;
            AnchorDistances = anchorDistances;

            if (Parent != null && Core.Instance != null && Core.Instance.RenderManager3D != null) {
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
        }

        /// <summary>
        /// Sets anchor distances manually using the resolved parent bounds as the reference frame.
        /// </summary>
        /// <param name="left">Distance from the left edge of the parent bounds.</param>
        /// <param name="right">Distance from the right edge of the parent bounds.</param>
        /// <param name="top">Distance from the top edge of the parent bounds.</param>
        /// <param name="bottom">Distance from the bottom edge of the parent bounds.</param>
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
        /// Refreshes the anchored position from the current bounds provider and stored distances.
        /// </summary>
        public void RefreshAnchoring() {
            if (!IsAnchored || Parent == null) {
                return;
            }

            RefreshSubscriptions();

            int2 anchorBounds = GetAnchorBounds();
            int2 anchorSize = GetAnchorSize();
            float3 localPosition = Parent.LocalPosition;

            if ((AnchorFlags & LeftAnchorFlag) != 0) {
                localPosition.X = AnchorDistances.X;
            } else if ((AnchorFlags & RightAnchorFlag) != 0) {
                localPosition.X = anchorBounds.X - AnchorDistances.Y - anchorSize.X;
            }

            if ((AnchorFlags & TopAnchorFlag) != 0) {
                localPosition.Y = AnchorDistances.Z;
            } else if ((AnchorFlags & BottomAnchorFlag) != 0) {
                localPosition.Y = anchorBounds.Y - AnchorDistances.W - anchorSize.Y;
            }

            Parent.LocalPosition = localPosition;
        }

        /// <summary>
        /// Rebinds to the nearest parent bounds provider when the component is attached to an entity.
        /// </summary>
        /// <param name="entity">Entity receiving the component.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            if (IsAnchored) {
                RefreshSubscriptions();
                RefreshAnchoring();
            }
        }

        /// <summary>
        /// Cleans up anchoring resources when the component is removed from its parent entity.
        /// </summary>
        /// <param name="entity">Entity the component was attached to.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            DisableAnchoring();
        }

        /// <summary>
        /// Rebinds the current provider subscriptions when the parent entity changes enabled state.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (!IsAnchored) {
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
        /// Handles parent bounds changes to reposition anchored entities.
        /// </summary>
        void HandleAnchorBoundsChanged() {
            RefreshAnchoring();
        }

        /// <summary>
        /// Handles window resize fallback events to reposition anchored entities when no parent provider exists.
        /// </summary>
        /// <param name="handle">Window handle reported by the render manager.</param>
        /// <param name="newWidth">Updated host width.</param>
        /// <param name="newHeight">Updated host height.</param>
        void HandleWindowResized(IntPtr handle, int newWidth, int newHeight) {
            RefreshAnchoring();
        }

        /// <summary>
        /// Rebinds the current provider subscriptions to the nearest ancestor layout bounds provider.
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

            if (anchorBoundsProvider == null) {
                AttachToWindowResize();
            } else {
                DetachFromWindowResize();
            }
        }

        /// <summary>
        /// Disconnects the current bounds provider subscription.
        /// </summary>
        void DetachFromBoundsProvider() {
            if (anchorBoundsProvider != null) {
                anchorBoundsProvider.AnchorBoundsChanged -= HandleAnchorBoundsChanged;
                anchorBoundsProvider = null;
            }
        }

        /// <summary>
        /// Connects the fallback window resize subscription if it is not already active.
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
        /// Disconnects the fallback window resize subscription if it is active.
        /// </summary>
        void DetachFromWindowResize() {
            if (!IsSubscribedToWindowResize) {
                return;
            }

            Core.Instance.RenderManager3D.WindowResized -= HandleWindowResized;
            IsSubscribedToWindowResize = false;
        }

        /// <summary>
        /// Resolves the nearest ancestor bounds provider in the entity chain.
        /// </summary>
        /// <returns>Nearest layout bounds provider when one exists; otherwise null.</returns>
        IAnchorBoundsProvider ResolveAnchorBoundsProvider() {
            Entity current = Parent;

            while (current != null) {
                if (current is IAnchorBoundsProvider provider) {
                    return provider;
                }

                if (current.Components != null) {
                    for (int i = 0; i < current.Components.Count; i++) {
                        if (current.Components[i] is IAnchorBoundsProvider componentProvider) {
                            return componentProvider;
                        }
                    }
                }

                current = current.Parent;
            }

            return null;
        }

        /// <summary>
        /// Resolves the bounds that should be treated as the active anchor space.
        /// </summary>
        /// <returns>Anchor bounds in local pixels.</returns>
        int2 GetAnchorBounds() {
            if (anchorBoundsProvider != null) {
                return anchorBoundsProvider.AnchorBounds;
            }

            return Core.Instance.RenderManager3D.MainWindowSize;
        }

        /// <summary>
        /// Resolves the current size of the anchored entity when it exposes one.
        /// </summary>
        /// <returns>Anchored size in local pixels, or zero when no size provider is available.</returns>
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

            for (int i = 0; i < Parent.Components.Count; i++) {
                if (Parent.Components[i] is IAnchorSizeProvider sizeProvider) {
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
        /// Computes the area of one size provider result for comparison purposes.
        /// </summary>
        /// <param name="size">Size used to compare providers.</param>
        /// <returns>Signed area used to select the most specific provider.</returns>
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

            var info = "Anchored to: ";
            var anchors = new List<string>();

            if ((AnchorFlags & LeftAnchorFlag) != 0) anchors.Add($"Left ({AnchorDistances.X:F1}px)");
            if ((AnchorFlags & RightAnchorFlag) != 0) anchors.Add($"Right ({AnchorDistances.Y:F1}px)");
            if ((AnchorFlags & TopAnchorFlag) != 0) anchors.Add($"Top ({AnchorDistances.Z:F1}px)");
            if ((AnchorFlags & BottomAnchorFlag) != 0) anchors.Add($"Bottom ({AnchorDistances.W:F1}px)");

            return info + string.Join(", ", anchors);
        }
    }
}
