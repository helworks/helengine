namespace helengine {
    /// <summary>
    /// Renders a draggable vertical scrollbar bound to a <see cref="ScrollComponent"/>, hiding itself when nothing overflows.
    /// </summary>
    public class ScrollBarComponent : Component {
        /// <summary>
        /// Smallest thumb length allowed regardless of how small the visible proportion becomes.
        /// </summary>
        const int MinimumThumbLengthPixels = 20;

        /// <summary>
        /// Full track bounds in pixels; X is the bar thickness, Y is the track length.
        /// </summary>
        int2 size;
        /// <summary>
        /// Tracks whether custom render orders were supplied for the track and thumb visuals.
        /// </summary>
        bool HasRenderOrderOverrides;
        /// <summary>
        /// Render order override for the track background.
        /// </summary>
        byte TrackRenderOrder;
        /// <summary>
        /// Render order override for the draggable thumb.
        /// </summary>
        byte ThumbRenderOrder;

        /// <summary>
        /// Scroll controller this scrollbar reflects and drives.
        /// </summary>
        ScrollComponent target;
        /// <summary>
        /// Tracks whether the pointer is currently hovering the scrollbar.
        /// </summary>
        bool isHovering;
        /// <summary>
        /// Tracks whether the pointer is currently dragging the thumb.
        /// </summary>
        bool isDragging;

        // Child entities and components
        Entity visualsRoot;
        RoundedRectComponent track;
        InteractableComponent interactableComponent;
        Entity thumbHost;
        RoundedRectComponent thumb;

        /// <summary>
        /// Creates one vertical scrollbar with the supplied track bounds.
        /// </summary>
        /// <param name="size">Full track bounds; X is the bar thickness, Y is the track length.</param>
        public ScrollBarComponent(int2 size) {
            if (size.X < 1 || size.Y < 1) {
                throw new ArgumentOutOfRangeException(nameof(size), "Scrollbar size must be positive.");
            }

            this.size = size;
        }

        /// <summary>
        /// Gets or sets the full track bounds; X is the bar thickness, Y is the track length.
        /// </summary>
        public int2 Size {
            get { return size; }
            set {
                if (value.X < 1 || value.Y < 1) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Scrollbar size must be positive.");
                }

                size = value;

                if (track != null) {
                    track.Size = size;
                }

                if (interactableComponent != null) {
                    interactableComponent.Size = size;
                }

                Refresh();
            }
        }

        /// <summary>
        /// Gets or sets the scroll controller this scrollbar reflects and drives.
        /// </summary>
        public ScrollComponent Target {
            get { return target; }
            set {
                if (ReferenceEquals(target, value)) {
                    return;
                }

                if (target != null) {
                    target.ScrollOffsetChanged -= HandleTargetScrollOffsetChanged;
                }

                target = value;

                if (target != null) {
                    target.ScrollOffsetChanged += HandleTargetScrollOffsetChanged;
                }

                Refresh();
            }
        }

        /// <summary>
        /// Gets whether the track and thumb are currently rendered because the bound target overflows.
        /// </summary>
        public bool IsVisible => visualsRoot != null && visualsRoot.Enabled;

        /// <summary>
        /// Overrides the render order used for the track and thumb visuals.
        /// </summary>
        /// <param name="trackOrder">Render order for the track background.</param>
        /// <param name="thumbOrder">Render order for the draggable thumb.</param>
        public void SetRenderOrders(byte trackOrder, byte thumbOrder) {
            HasRenderOrderOverrides = true;
            TrackRenderOrder = trackOrder;
            ThumbRenderOrder = thumbOrder;

            if (track != null) {
                track.RenderOrder2D = trackOrder;
            }

            if (thumb != null) {
                thumb.RenderOrder2D = thumbOrder;
            }
        }

        /// <summary>
        /// Creates the track, thumb, and interactable region when added to an enabled entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (!entity.Enabled) {
                return;
            }

            byte trackOrder = RenderOrder2D.PanelSurface;
            byte thumbOrder = RenderOrder2D.PanelForeground;
            if (HasRenderOrderOverrides) {
                trackOrder = TrackRenderOrder;
                thumbOrder = ThumbRenderOrder;
            }

            visualsRoot = new Entity();
            visualsRoot.LayerMask = entity.LayerMask;
            visualsRoot.Enabled = true;
            visualsRoot.InitComponents();

            if (entity.Children == null) {
                entity.InitChildren();
            }
            entity.AddChild(visualsRoot);

            track = new RoundedRectComponent {
                Size = size,
                Radius = size.X * 0.5f,
                BorderThickness = 0f,
                FillColor = ThemeManager.Colors.SurfaceInput,
                BorderColor = ThemeManager.Colors.SurfaceInput,
                RenderOrder2D = trackOrder
            };
            visualsRoot.AddComponent(track);

            interactableComponent = new InteractableComponent {
                Size = size,
                HoverCursor = PointerCursorKind.Hand
            };
            interactableComponent.CursorEvent += HandleCursorEvent;
            visualsRoot.AddComponent(interactableComponent);

            thumbHost = new Entity();
            thumbHost.LayerMask = entity.LayerMask;
            thumbHost.Enabled = true;
            thumbHost.InitComponents();

            if (visualsRoot.Children == null) {
                visualsRoot.InitChildren();
            }
            visualsRoot.AddChild(thumbHost);

            thumb = new RoundedRectComponent {
                Size = new int2(size.X, MinimumThumbLengthPixels),
                Radius = size.X * 0.5f,
                BorderThickness = 0f,
                RenderOrder2D = thumbOrder
            };
            thumbHost.AddComponent(thumb);

            Refresh();
        }

        /// <summary>
        /// Clears transient hover and drag state when hierarchy enablement changes.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (!newEnabled) {
                isHovering = false;
                isDragging = false;
            }
        }

        /// <summary>
        /// Unsubscribes from the bound target when the scrollbar is removed from its entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);

            if (target != null) {
                target.ScrollOffsetChanged -= HandleTargetScrollOffsetChanged;
            }

            isHovering = false;
            isDragging = false;
        }

        /// <summary>
        /// Recomputes thumb size and position from the bound target, hiding the scrollbar when nothing overflows.
        /// </summary>
        public void Refresh() {
            if (visualsRoot == null) {
                return;
            }

            bool hasOverflow = target != null && target.MaximumScrollOffset > 0;
            visualsRoot.Enabled = hasOverflow;
            if (!hasOverflow) {
                isHovering = false;
                isDragging = false;
                return;
            }

            int thumbLength = ComputeThumbLengthPixels();
            int travel = Math.Max(0, size.Y - thumbLength);
            int thumbY = target.MaximumScrollOffset > 0
                ? (int)Math.Round(travel * (target.ScrollOffset / (double)target.MaximumScrollOffset))
                : 0;

            thumb.Size = new int2(size.X, thumbLength);
            thumbHost.Position = new float3(0f, thumbY, 0.1f);
            UpdateThumbColor();
        }

        /// <summary>
        /// Refreshes the scrollbar whenever the bound target's scroll offset changes.
        /// </summary>
        /// <param name="scrollComponent">Scroll controller that changed.</param>
        /// <param name="scrollOffset">New scroll offset.</param>
        void HandleTargetScrollOffsetChanged(ScrollComponent scrollComponent, int scrollOffset) {
            Refresh();
        }

        /// <summary>
        /// Handles pointer hover, press, and release to drag the thumb or jump to a clicked track position.
        /// </summary>
        /// <param name="relPos">Pointer position relative to the scrollbar.</param>
        /// <param name="delta">Pointer movement delta.</param>
        /// <param name="state">Pointer interaction state.</param>
        void HandleCursorEvent(int2 relPos, int2 delta, PointerInteraction state) {
            if (target == null) {
                return;
            }

            switch (state) {
                case PointerInteraction.Hover:
                    isHovering = true;
                    if (isDragging) {
                        ApplyNormalizedPosition(relPos.Y);
                    }
                    break;

                case PointerInteraction.Press:
                    isHovering = true;
                    isDragging = true;
                    ApplyNormalizedPosition(relPos.Y);
                    break;

                case PointerInteraction.Release:
                    if (isDragging) {
                        ApplyNormalizedPosition(relPos.Y);
                    }
                    isDragging = false;
                    break;

                case PointerInteraction.Leave:
                    isHovering = false;
                    isDragging = false;
                    break;

                case PointerInteraction.None:
                    break;
            }

            UpdateThumbColor();
        }

        /// <summary>
        /// Scrolls the bound target so the thumb center lands at the supplied track-relative Y position.
        /// </summary>
        /// <param name="pointerY">Pointer Y position relative to the scrollbar track.</param>
        void ApplyNormalizedPosition(int pointerY) {
            int maximumOffset = target.MaximumScrollOffset;
            if (maximumOffset <= 0) {
                return;
            }

            int thumbLength = ComputeThumbLengthPixels();
            int travel = Math.Max(1, size.Y - thumbLength);
            double normalizedCenter = (pointerY - (thumbLength * 0.5)) / travel;
            normalizedCenter = Math.Clamp(normalizedCenter, 0.0, 1.0);
            int scrollOffset = (int)Math.Round(normalizedCenter * maximumOffset);
            target.ScrollTo(scrollOffset);
        }

        /// <summary>
        /// Computes the current thumb length from the bound target's visible proportion.
        /// </summary>
        /// <returns>Thumb length in pixels, clamped to the track bounds.</returns>
        int ComputeThumbLengthPixels() {
            if (target == null || target.ItemCount <= 0) {
                return size.Y;
            }

            double proportion = Math.Clamp(target.VisibleItemCount / (double)target.ItemCount, 0.0, 1.0);
            int length = (int)Math.Round(size.Y * proportion);
            return Math.Clamp(length, Math.Min(MinimumThumbLengthPixels, size.Y), size.Y);
        }

        /// <summary>
        /// Updates the thumb fill and border color from the current hover and drag state.
        /// </summary>
        void UpdateThumbColor() {
            if (thumb == null) {
                return;
            }

            byte4 color;
            if (isDragging) {
                color = ThemeManager.Colors.AccentTertiary;
            } else if (isHovering) {
                color = ThemeManager.Colors.AccentSecondary;
            } else {
                color = ThemeManager.Colors.AccentPrimary;
            }

            thumb.FillColor = color;
            thumb.BorderColor = color;
        }
    }
}
