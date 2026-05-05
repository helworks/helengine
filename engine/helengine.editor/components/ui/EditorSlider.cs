namespace helengine.editor {
    /// <summary>
    /// Reusable editor slider that supports pointer dragging and keyboard adjustment.
    /// </summary>
    public class EditorSlider : EditorEntity {
        /// <summary>
        /// Smallest allowed keyboard step when the caller does not provide an explicit value.
        /// </summary>
        const double DefaultKeyboardStep = 0.01;
        /// <summary>
        /// Comparison epsilon used when deciding whether one authored value changed materially.
        /// </summary>
        const double ValueEpsilon = 0.000001;

        /// <summary>
        /// Minimum authored value supported by this slider.
        /// </summary>
        readonly double MinimumValue;
        /// <summary>
        /// Maximum authored value supported by this slider.
        /// </summary>
        readonly double MaximumValue;
        /// <summary>
        /// Mapping mode used when converting between normalized and authored values.
        /// </summary>
        readonly EditorSliderScaleMode ScaleMode;
        /// <summary>
        /// Total control width in pixels.
        /// </summary>
        readonly int SliderWidth;
        /// <summary>
        /// Total control height in pixels.
        /// </summary>
        readonly int SliderHeight;
        /// <summary>
        /// Pixel height of the visible track surface.
        /// </summary>
        readonly int TrackHeight;
        /// <summary>
        /// Pixel width of the draggable thumb surface.
        /// </summary>
        readonly int ThumbWidth;
        /// <summary>
        /// Pixel height of the draggable thumb surface.
        /// </summary>
        readonly int ThumbHeight;
        /// <summary>
        /// Root entity for the visible slider track.
        /// </summary>
        readonly EditorEntity TrackHost;
        /// <summary>
        /// Background surface spanning the full slider track.
        /// </summary>
        readonly RoundedRectComponent TrackBackground;
        /// <summary>
        /// Filled surface showing the current normalized amount.
        /// </summary>
        readonly RoundedRectComponent TrackFill;
        /// <summary>
        /// Root entity for the draggable thumb.
        /// </summary>
        readonly EditorEntity ThumbHost;
        /// <summary>
        /// Surface used to render the draggable thumb.
        /// </summary>
        readonly RoundedRectComponent Thumb;
        /// <summary>
        /// Interactable region used to receive pointer input across the slider bounds.
        /// </summary>
        readonly InteractableComponent TrackInteractable;
        /// <summary>
        /// Tracks whether the pointer is currently dragging the slider thumb.
        /// </summary>
        bool IsDragging;
        /// <summary>
        /// Tracks whether the pointer is currently hovering the slider hit area.
        /// </summary>
        bool IsHovered;
        /// <summary>
        /// Tracks whether keyboard focus currently targets the slider.
        /// </summary>
        bool IsKeyboardFocused;

        /// <summary>
        /// Initializes one slider with authored range metadata and immediate visual children.
        /// </summary>
        /// <param name="minimumValue">Smallest authored value supported by the slider.</param>
        /// <param name="maximumValue">Largest authored value supported by the slider.</param>
        /// <param name="initialValue">Initial authored value applied to the slider.</param>
        /// <param name="scaleMode">Mapping mode used for normalized positions.</param>
        /// <param name="width">Total slider width in pixels.</param>
        /// <param name="height">Total slider height in pixels.</param>
        public EditorSlider(double minimumValue, double maximumValue, double initialValue, EditorSliderScaleMode scaleMode, int width, int height) {
            if (maximumValue <= minimumValue) {
                throw new ArgumentOutOfRangeException(nameof(maximumValue), "Maximum slider value must be greater than the minimum value.");
            }
            if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width), "Slider width must be greater than zero.");
            }
            if (height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(height), "Slider height must be greater than zero.");
            }
            if (scaleMode == EditorSliderScaleMode.Logarithmic && minimumValue <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(minimumValue), "Logarithmic sliders require a strictly positive minimum value.");
            }

            MinimumValue = minimumValue;
            MaximumValue = maximumValue;
            ScaleMode = scaleMode;
            SliderWidth = width;
            SliderHeight = height;
            TrackHeight = Math.Max(4, (int)Math.Round(height * 0.4));
            ThumbWidth = Math.Max(10, height);
            ThumbHeight = height;
            KeyboardStep = DefaultKeyboardStep;
            InternalEntity = true;
            Enabled = true;
            LayerMask = EditorLayerMasks.EditorUi;

            TrackHost = new EditorEntity {
                InternalEntity = true,
                LayerMask = LayerMask,
                Position = new float3(0f, (float)Math.Round((SliderHeight - TrackHeight) * 0.5), 0f)
            };
            AddChild(TrackHost);

            TrackBackground = new RoundedRectComponent {
                Size = new int2(SliderWidth, TrackHeight),
                Radius = TrackHeight * 0.5f,
                BorderThickness = 1f,
                FillColor = ThemeManager.Colors.SurfaceInput,
                BorderColor = ThemeManager.Colors.SurfacePrimary,
                RenderOrder2D = RenderOrder2D.PanelSurface
            };
            TrackHost.AddComponent(TrackBackground);

            TrackFill = new RoundedRectComponent {
                Size = new int2(1, TrackHeight),
                Radius = TrackHeight * 0.5f,
                BorderThickness = 0f,
                FillColor = ThemeManager.Colors.AccentPrimary,
                BorderColor = ThemeManager.Colors.AccentPrimary,
                RenderOrder2D = RenderOrder2D.PanelForeground
            };
            TrackHost.AddComponent(TrackFill);

            ThumbHost = new EditorEntity {
                InternalEntity = true,
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            AddChild(ThumbHost);

            Thumb = new RoundedRectComponent {
                Size = new int2(ThumbWidth, ThumbHeight),
                Radius = Math.Max(2f, ThumbHeight * 0.35f),
                BorderThickness = 1f,
                FillColor = ThemeManager.Colors.AccentPrimary,
                BorderColor = ThemeManager.Colors.AccentPrimary,
                RenderOrder2D = RenderOrder2D.PanelForeground
            };
            ThumbHost.AddComponent(Thumb);

            TrackInteractable = new InteractableComponent {
                Size = new int2(SliderWidth, SliderHeight)
            };
            TrackInteractable.CursorEvent += HandleTrackCursor;
            AddComponent(TrackInteractable);

            InitializeValue(initialValue);
            UpdateVisuals();
        }

        /// <summary>
        /// Raised whenever the authored slider value changes.
        /// </summary>
        public event Action<double> ValueChanged;

        /// <summary>
        /// Gets the current authored slider value.
        /// </summary>
        public double Value { get; private set; }

        /// <summary>
        /// Gets the overall slider bounds in pixels.
        /// </summary>
        public int2 ControlSize => new int2(SliderWidth, SliderHeight);

        /// <summary>
        /// Gets or sets the keyboard step applied by left and right arrow adjustments.
        /// </summary>
        public double KeyboardStep { get; set; }

        /// <summary>
        /// Applies one layer mask to the slider root and all visual child entities.
        /// </summary>
        /// <param name="layerMask">Layer mask that should be applied to the slider hierarchy.</param>
        public void ApplyLayerMask(ushort layerMask) {
            LayerMask = layerMask;
            TrackHost.LayerMask = layerMask;
            ThumbHost.LayerMask = layerMask;
        }

        /// <summary>
        /// Applies render orders to the track surface and the active foreground visuals.
        /// </summary>
        /// <param name="surfaceOrder">Render order used by the background track.</param>
        /// <param name="foregroundOrder">Render order used by the fill and thumb visuals.</param>
        public void SetRenderOrders(byte surfaceOrder, byte foregroundOrder) {
            TrackBackground.RenderOrder2D = surfaceOrder;
            TrackFill.RenderOrder2D = foregroundOrder;
            Thumb.RenderOrder2D = foregroundOrder;
        }

        /// <summary>
        /// Sets whether keyboard focus currently targets the slider so focused visuals can be shown.
        /// </summary>
        /// <param name="isFocused">True when keyboard focus currently targets the slider.</param>
        public void SetKeyboardFocused(bool isFocused) {
            IsKeyboardFocused = isFocused;
            UpdateVisuals();
        }

        /// <summary>
        /// Sets the authored slider value directly after clamping it into the legal range.
        /// </summary>
        /// <param name="value">Authored slider value to apply.</param>
        public void SetValue(double value) {
            double clampedValue = Math.Clamp(value, MinimumValue, MaximumValue);
            if (Math.Abs(clampedValue - Value) <= ValueEpsilon) {
                return;
            }

            Value = clampedValue;
            UpdateVisuals();
            if (ValueChanged != null) {
                ValueChanged(Value);
            }
        }

        /// <summary>
        /// Sets the slider value from a normalized track position.
        /// </summary>
        /// <param name="normalizedValue">Track position from 0 to 1.</param>
        public void SetNormalizedValue(double normalizedValue) {
            double clampedNormalizedValue = Math.Clamp(normalizedValue, 0.0, 1.0);
            double nextValue = MapNormalizedValue(clampedNormalizedValue);
            SetValue(nextValue);
        }

        /// <summary>
        /// Applies one keyboard adjustment for the provided key.
        /// </summary>
        /// <param name="key">Adjustment key routed by the focus target.</param>
        public void AdjustFromKey(Keys key) {
            if (KeyboardStep <= 0.0) {
                throw new InvalidOperationException("Keyboard step must be greater than zero before keyboard adjustment can occur.");
            }

            if (key == Keys.Right) {
                SetValue(Value + KeyboardStep);
            } else if (key == Keys.Left) {
                SetValue(Value - KeyboardStep);
            }
        }

        /// <summary>
        /// Handles one pointer interaction routed from the slider hit area.
        /// </summary>
        /// <param name="position">Pointer position relative to the slider bounds.</param>
        /// <param name="delta">Pointer delta supplied by the input system.</param>
        /// <param name="interaction">Pointer interaction state.</param>
        public void HandleTrackCursor(int2 position, int2 delta, PointerInteraction interaction) {
            if (interaction == PointerInteraction.Hover) {
                IsHovered = true;
                if (IsDragging) {
                    SetNormalizedValue(ResolveNormalizedPosition(position));
                }
            } else if (interaction == PointerInteraction.Press) {
                IsHovered = true;
                IsDragging = true;
                SetNormalizedValue(ResolveNormalizedPosition(position));
            } else if (interaction == PointerInteraction.Release) {
                if (IsDragging) {
                    SetNormalizedValue(ResolveNormalizedPosition(position));
                }

                IsDragging = false;
            } else if (interaction == PointerInteraction.Leave) {
                IsHovered = false;
                IsDragging = false;
            }

            UpdateVisuals();
        }

        /// <summary>
        /// Initializes the starting slider value without notifying listeners.
        /// </summary>
        /// <param name="initialValue">Initial authored slider value.</param>
        void InitializeValue(double initialValue) {
            Value = Math.Clamp(initialValue, MinimumValue, MaximumValue);
        }

        /// <summary>
        /// Maps one normalized track position into the authored slider range.
        /// </summary>
        /// <param name="normalizedValue">Normalized position from 0 to 1.</param>
        /// <returns>Mapped authored value.</returns>
        double MapNormalizedValue(double normalizedValue) {
            if (ScaleMode == EditorSliderScaleMode.Logarithmic) {
                double minimumLog = Math.Log(MinimumValue);
                double maximumLog = Math.Log(MaximumValue);
                return Math.Exp(minimumLog + ((maximumLog - minimumLog) * normalizedValue));
            }

            return MinimumValue + ((MaximumValue - MinimumValue) * normalizedValue);
        }

        /// <summary>
        /// Maps the current authored value into the normalized 0-to-1 slider domain.
        /// </summary>
        /// <returns>Normalized representation of the current authored value.</returns>
        double MapValueToNormalized() {
            if (Math.Abs(MaximumValue - MinimumValue) <= ValueEpsilon) {
                return 0.0;
            }

            if (ScaleMode == EditorSliderScaleMode.Logarithmic) {
                double minimumLog = Math.Log(MinimumValue);
                double maximumLog = Math.Log(MaximumValue);
                return (Math.Log(Value) - minimumLog) / (maximumLog - minimumLog);
            }

            return (Value - MinimumValue) / (MaximumValue - MinimumValue);
        }

        /// <summary>
        /// Resolves one normalized position from a pointer X coordinate inside the slider bounds.
        /// </summary>
        /// <param name="position">Pointer position relative to the slider.</param>
        /// <returns>Normalized position from 0 to 1.</returns>
        double ResolveNormalizedPosition(int2 position) {
            if (SliderWidth <= 1) {
                return 0.0;
            }

            return Math.Clamp(position.X / (double)SliderWidth, 0.0, 1.0);
        }

        /// <summary>
        /// Refreshes the fill width, thumb position, and color styling from current state.
        /// </summary>
        void UpdateVisuals() {
            double normalizedValue = Math.Clamp(MapValueToNormalized(), 0.0, 1.0);
            int fillWidth = Math.Max(1, (int)Math.Round(SliderWidth * normalizedValue));
            int thumbTravelWidth = Math.Max(0, SliderWidth - ThumbWidth);
            int thumbX = (int)Math.Round(thumbTravelWidth * normalizedValue);

            TrackFill.Size = new int2(fillWidth, TrackHeight);
            ThumbHost.Position = new float3(thumbX, 0f, 0.1f);

            if (IsDragging) {
                Thumb.FillColor = ThemeManager.Colors.AccentTertiary;
                Thumb.BorderColor = ThemeManager.Colors.AccentTertiary;
                TrackFill.FillColor = ThemeManager.Colors.AccentTertiary;
                TrackFill.BorderColor = ThemeManager.Colors.AccentTertiary;
            } else if (IsKeyboardFocused || IsHovered) {
                Thumb.FillColor = ThemeManager.Colors.AccentSecondary;
                Thumb.BorderColor = ThemeManager.Colors.AccentSecondary;
                TrackFill.FillColor = ThemeManager.Colors.AccentSecondary;
                TrackFill.BorderColor = ThemeManager.Colors.AccentSecondary;
            } else {
                Thumb.FillColor = ThemeManager.Colors.AccentPrimary;
                Thumb.BorderColor = ThemeManager.Colors.AccentPrimary;
                TrackFill.FillColor = ThemeManager.Colors.AccentPrimary;
                TrackFill.BorderColor = ThemeManager.Colors.AccentPrimary;
            }
        }
    }
}
