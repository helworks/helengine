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
        int2 SizeValue;
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
        ScrollComponent TargetValue;
        /// <summary>
        /// Tracks whether the pointer is currently hovering the scrollbar.
        /// </summary>
        bool IsHovering;
        /// <summary>
        /// Tracks whether the pointer is currently dragging the thumb.
        /// </summary>
        bool IsDragging;

        // Child entities and components
        Entity VisualsRoot;
        RoundedRectComponent Track;
        InteractableComponent InteractableComponent;
        Entity ThumbHost;
        RoundedRectComponent Thumb;

        /// <summary>
        /// Creates one vertical scrollbar with the supplied track bounds.
        /// </summary>
        /// <param name="size">Full track bounds; X is the bar thickness, Y is the track length.</param>
        public ScrollBarComponent(int2 size) {
            if (size.X < 1 || size.Y < 1) {
                throw new ArgumentOutOfRangeException(nameof(size), "Scrollbar size must be positive.");
            }

            this.SizeValue = size;
        }

        /// <summary>
        /// Gets or sets the full track bounds; X is the bar thickness, Y is the track length.
        /// </summary>
        public int2 Size {
            get { return SizeValue; }
            set {
                if (value.X < 1 || value.Y < 1) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Scrollbar size must be positive.");
                }

                SizeValue = value;

                if (Track != null) {
                    Track.Size = SizeValue;
                }

                if (InteractableComponent != null) {
                    InteractableComponent.Size = SizeValue;
                }

                Refresh();
            }
        }

        /// <summary>
        /// Gets or sets the scroll controller this scrollbar reflects and drives.
        /// </summary>
        public ScrollComponent Target {
            get { return TargetValue; }
            set {
                if (ReferenceEquals(TargetValue, value)) {
                    return;
                }

                if (TargetValue != null) {
                    TargetValue.ScrollOffsetChanged -= HandleTargetScrollOffsetChanged;
                }

                TargetValue = value;

                if (TargetValue != null) {
                    TargetValue.ScrollOffsetChanged += HandleTargetScrollOffsetChanged;
                }

                Refresh();
            }
        }

        /// <summary>
        /// Gets whether the track and thumb are currently rendered because the bound target overflows.
        /// </summary>
        public bool IsVisible => VisualsRoot != null && VisualsRoot.Enabled;

        /// <summary>
        /// Overrides the render order used for the track and thumb visuals.
        /// </summary>
        /// <param name="trackOrder">Render order for the track background.</param>
        /// <param name="thumbOrder">Render order for the draggable thumb.</param>
        public void SetRenderOrders(byte trackOrder, byte thumbOrder) {
            HasRenderOrderOverrides = true;
            TrackRenderOrder = trackOrder;
            ThumbRenderOrder = thumbOrder;

            if (Track != null) {
                Track.RenderOrder2D = trackOrder;
            }

            if (Thumb != null) {
                Thumb.RenderOrder2D = thumbOrder;
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

            VisualsRoot = new Entity(OwnerCore ?? throw new InvalidOperationException("Scroll-bar visuals require an owning core."));
            VisualsRoot.LayerMask = entity.LayerMask;
            VisualsRoot.Enabled = true;
            VisualsRoot.InitComponents();

            if (entity.Children == null) {
                entity.InitChildren();
            }
            entity.AddChild(VisualsRoot);

            Track = new RoundedRectComponent {
                Size = SizeValue,
                Radius = SizeValue.X * 0.5f,
                BorderThickness = 0f,
                FillColor = ThemeManager.Colors.SurfaceInput,
                BorderColor = ThemeManager.Colors.SurfaceInput,
                RenderOrder2D = trackOrder
            };
            VisualsRoot.AddComponent(Track);

            InteractableComponent = new InteractableComponent {
                Size = SizeValue,
                HoverCursor = PointerCursorKind.Hand
            };
            InteractableComponent.CursorEvent += HandleCursorEvent;
            VisualsRoot.AddComponent(InteractableComponent);

            ThumbHost = new Entity(OwnerCore ?? throw new InvalidOperationException("Scroll-bar visuals require an owning core."));
            ThumbHost.LayerMask = entity.LayerMask;
            ThumbHost.Enabled = true;
            ThumbHost.InitComponents();

            if (VisualsRoot.Children == null) {
                VisualsRoot.InitChildren();
            }
            VisualsRoot.AddChild(ThumbHost);

            Thumb = new RoundedRectComponent {
                Size = new int2(SizeValue.X, MinimumThumbLengthPixels),
                Radius = SizeValue.X * 0.5f,
                BorderThickness = 0f,
                RenderOrder2D = thumbOrder
            };
            ThumbHost.AddComponent(Thumb);

            Refresh();
        }

        /// <summary>
        /// Clears transient hover and drag state when hierarchy enablement changes.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (!newEnabled) {
                IsHovering = false;
                IsDragging = false;
            }
        }

        /// <summary>
        /// Unsubscribes from the bound target when the scrollbar is removed from its entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);

            if (TargetValue != null) {
                TargetValue.ScrollOffsetChanged -= HandleTargetScrollOffsetChanged;
            }

            IsHovering = false;
            IsDragging = false;
        }

        /// <summary>
        /// Recomputes thumb size and position from the bound target, hiding the scrollbar when nothing overflows.
        /// </summary>
        public void Refresh() {
            if (VisualsRoot == null) {
                return;
            }

            bool hasOverflow = TargetValue != null && TargetValue.MaximumScrollOffset > 0;
            VisualsRoot.Enabled = hasOverflow;
            if (!hasOverflow) {
                IsHovering = false;
                IsDragging = false;
                return;
            }

            int thumbLength = ComputeThumbLengthPixels();
            int travel = Math.Max(0, SizeValue.Y - thumbLength);
            int thumbY = TargetValue.MaximumScrollOffset > 0
                ? (int)Math.Round(travel * (TargetValue.ScrollOffset / (double)TargetValue.MaximumScrollOffset))
                : 0;

            Thumb.Size = new int2(SizeValue.X, thumbLength);
            ThumbHost.Position = new float3(0f, thumbY, 0.1f);
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
            if (TargetValue == null) {
                return;
            }

            switch (state) {
                case PointerInteraction.Hover:
                    IsHovering = true;
                    if (IsDragging) {
                        ApplyNormalizedPosition(relPos.Y);
                    }
                    break;

                case PointerInteraction.Press:
                    IsHovering = true;
                    IsDragging = true;
                    ApplyNormalizedPosition(relPos.Y);
                    break;

                case PointerInteraction.Release:
                    if (IsDragging) {
                        ApplyNormalizedPosition(relPos.Y);
                    }
                    IsDragging = false;
                    break;

                case PointerInteraction.Leave:
                    IsHovering = false;
                    IsDragging = false;
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
            int maximumOffset = TargetValue.MaximumScrollOffset;
            if (maximumOffset <= 0) {
                return;
            }

            int thumbLength = ComputeThumbLengthPixels();
            int travel = Math.Max(1, SizeValue.Y - thumbLength);
            double normalizedCenter = (pointerY - (thumbLength * 0.5)) / travel;
            normalizedCenter = Math.Clamp(normalizedCenter, 0.0, 1.0);
            int scrollOffset = (int)Math.Round(normalizedCenter * maximumOffset);
            TargetValue.ScrollTo(scrollOffset);
        }

        /// <summary>
        /// Computes the current thumb length from the bound target's visible proportion.
        /// </summary>
        /// <returns>Thumb length in pixels, clamped to the track bounds.</returns>
        int ComputeThumbLengthPixels() {
            if (TargetValue == null || TargetValue.ItemCount <= 0) {
                return SizeValue.Y;
            }

            double proportion = Math.Clamp(TargetValue.VisibleItemCount / (double)TargetValue.ItemCount, 0.0, 1.0);
            int length = (int)Math.Round(SizeValue.Y * proportion);
            return Math.Clamp(length, Math.Min(MinimumThumbLengthPixels, SizeValue.Y), SizeValue.Y);
        }

        /// <summary>
        /// Updates the thumb fill and border color from the current hover and drag state.
        /// </summary>
        void UpdateThumbColor() {
            if (Thumb == null) {
                return;
            }

            byte4 color;
            if (IsDragging) {
                color = ThemeManager.Colors.AccentTertiary;
            } else if (IsHovering) {
                color = ThemeManager.Colors.AccentSecondary;
            } else {
                color = ThemeManager.Colors.AccentPrimary;
            }

            Thumb.FillColor = color;
            Thumb.BorderColor = color;
        }
    }
}
