namespace helengine.video {
    /// <summary>
    /// Defines the kinetic typography templates that ship with the engine. Each is an ordinary
    /// <see cref="GraphicTemplateAsset"/>, exactly like a project <c>.hgraphic</c>. The motion follows one house style:
    /// short staggered pops (opacity in about 0.18 s, scale from 0.85 with a slight back-out overshoot), separators that
    /// pop just after the item before them, optional dimming of earlier items and an accent color that follows the text
    /// style's highlight color unless the edit overrides it. Dimming never makes an item see-through: a copy in a neutral
    /// dim color with the style's full outline and shadow appears under the item, which then fades partly away, so a dimmed
    /// item reads as a muted version of itself on light and dark pictures alike.
    /// </summary>
    public static class BuiltInGraphicTemplates {
        /// <summary>
        /// Name of the accent color parameter shared by the built-ins.
        /// </summary>
        const string AccentColor = "accent_color";

        /// <summary>
        /// Name of the dim switch shared by the built-ins.
        /// </summary>
        const string DimPrevious = "dim_previous";

        /// <summary>
        /// Name of the dim opacity parameter shared by the built-ins.
        /// </summary>
        const string DimOpacity = "dim_opacity";

        /// <summary>
        /// Name of the dim color parameter shared by the built-ins.
        /// </summary>
        const string DimColor = "dim_color";

        /// <summary>
        /// Seconds an item takes to fade in.
        /// </summary>
        const float FadeSeconds = 0.18f;

        /// <summary>
        /// Seconds an item takes to settle its pop scale.
        /// </summary>
        const float PopSeconds = 0.24f;

        /// <summary>
        /// Seconds an earlier item takes to dim once the next item appears.
        /// </summary>
        const float DimSeconds = 0.25f;

        /// <summary>
        /// Creates every built-in template.
        /// </summary>
        /// <returns>Fresh template instances in catalog order.</returns>
        public static GraphicTemplateAsset[] All() {
            return new[] { ContrastChain(), ListBuild(), HighlightWord(), StrikeReplace() };
        }

        /// <summary>
        /// Terms that must not be confused (A ≠ B ≠ C): each term pops in as it is spoken, the separator pops just after
        /// the term before it in the accent color, and earlier terms dim so the newest one leads.
        /// </summary>
        /// <returns>The <c>contrast_chain</c> template.</returns>
        public static GraphicTemplateAsset ContrastChain() {
            return new GraphicTemplateAsset {
                TemplateId = "contrast_chain",
                DisplayName = "Contrast chain",
                Description = "Two to four short terms that must not be confused, built up one by one with a separator (default ≠) popping between them: A ≠ B ≠ C. Use when the speaker distinguishes or contrasts terms; anchor each item on the word where it is spoken.",
                Slots = new[] {
                    Items(2, 4, 28, "The terms in spoken order, each one to three words."),
                    new GraphicTemplateSlotAsset { Name = GraphicTemplateValidator.SeparatorSlot, Kind = GraphicTemplateSlotKind.Text, MaxChars = 3, DefaultText = "≠", Description = "Symbol drawn between terms, such as ≠, vs or →." }
                },
                Parameters = new[] { Accent("Separator color; when omitted the text style highlight color is used."), DimSwitch(true), DimLevel(0.45f), DimTint() },
                Layouts = Layouts(-0.2f, 0.32f),
                Elements = new[] {
                    new GraphicTemplateElementAsset { Name = "item", Order = 1, Tracks = Entrance(0f, 0.85f).Append(Dim()).ToArray() },
                    new GraphicTemplateElementAsset {
                        Name = "separator", Repeat = GraphicElementRepeat.BetweenItems, Content = GraphicElementContent.SeparatorText, Color = GraphicElementColor.Accent, ColorParameter = AccentColor,
                        FontScale = 0.8f, Order = 2, Tracks = Entrance(0.12f, 0.6f)
                    },
                    Dimmed("item_dimmed", GraphicElementRepeat.EachItemExceptLast, DimPrevious)
                }
            };
        }

        /// <summary>
        /// A short list that builds item by item: each item rises into place as it is spoken and the newest one is drawn in
        /// the accent color until the next arrives.
        /// </summary>
        /// <returns>The <c>list_build</c> template.</returns>
        public static GraphicTemplateAsset ListBuild() {
            return new GraphicTemplateAsset {
                TemplateId = "list_build",
                DisplayName = "List build",
                Description = "Two to six short points that appear one by one as they are spoken, stacked, with the newest point in the accent color. Use for enumerations; anchor each item on the word where it is spoken.",
                Slots = new[] { Items(2, 6, 32, "The points in spoken order, each one to four words.") },
                Parameters = new[] {
                    Accent("Color of the newest item; when omitted the text style highlight color is used."),
                    new EffectParameterAsset { Name = "highlight_latest", Type = EffectParameterType.Bool, DefaultValue = new float4(1, 0, 0, 0), Minimum = 0, Maximum = 1, Description = "Draw the newest item in the accent color until the next one appears." },
                    DimSwitch(false),
                    DimLevel(0.5f),
                    DimTint()
                },
                Layouts = Layouts(-0.14f, 0.5f),
                Elements = new[] {
                    new GraphicTemplateElementAsset { Name = "item", Order = 1, Tracks = Rise().Append(Dim()).ToArray() },
                    new GraphicTemplateElementAsset {
                        Name = "latest", Color = GraphicElementColor.Accent, ColorParameter = AccentColor, Order = 2, EnabledParameter = "highlight_latest",
                        Tracks = Rise().Append(Track(GraphicAnimatedProperty.Opacity, GraphicTimeAnchor.NextItem, "", Key(0f, 1f, "smoothstep.v1"), Key(0.3f, 0f, "linear.v1"))).ToArray()
                    },
                    Dimmed("item_dimmed", GraphicElementRepeat.EachItemExceptLast, DimPrevious)
                }
            };
        }

        /// <summary>
        /// One phrase split into words or chunks that pop in as spoken, with the key item drawn larger in the accent color
        /// and a stronger pop.
        /// </summary>
        /// <returns>The <c>highlight_word</c> template.</returns>
        public static GraphicTemplateAsset HighlightWord() {
            return new GraphicTemplateAsset {
                TemplateId = "highlight_word",
                DisplayName = "Highlight word",
                Description = "One short phrase split into words or chunks that appear as they are spoken, with one key item emphasized in the accent color. Set accent_item to the index of the key item; give items the same moment to show the phrase at once.",
                Slots = new[] {
                    Items(1, 8, 24, "The phrase split into words or short chunks, in reading order."),
                    new GraphicTemplateSlotAsset { Name = GraphicTemplateValidator.AccentItemSlot, Kind = GraphicTemplateSlotKind.ItemIndex, Required = true, Description = "Zero-based index of the item to emphasize." }
                },
                Parameters = new[] { Accent("Color of the emphasized item; when omitted the text style highlight color is used.") },
                Layouts = Layouts(-0.2f, 0.26f),
                Elements = new[] {
                    new GraphicTemplateElementAsset { Name = "word", Repeat = GraphicElementRepeat.EachItemExceptAccent, Order = 1, Tracks = Entrance(0f, 0.88f) },
                    new GraphicTemplateElementAsset {
                        Name = "accent", Repeat = GraphicElementRepeat.AccentItem, Color = GraphicElementColor.Accent, ColorParameter = AccentColor, FontScale = 1.15f, Order = 2,
                        Tracks = new[] {
                            Track(GraphicAnimatedProperty.Opacity, GraphicTimeAnchor.Item, "", Key(0f, 0f, "smoothstep.v1"), Key(0.14f, 1f, "linear.v1")),
                            Track(GraphicAnimatedProperty.Scale, GraphicTimeAnchor.Item, "", Key(0f, 0.7f, "ease_out_back.v1"), Key(0.3f, 1f, "linear.v1"))
                        }
                    }
                }
            };
        }

        /// <summary>
        /// A wrong claim appears, gets struck through just before the correcting word, dims, and the correction pops in
        /// in the accent color.
        /// </summary>
        /// <returns>The <c>strike_replace</c> template.</returns>
        public static GraphicTemplateAsset StrikeReplace() {
            return new GraphicTemplateAsset {
                TemplateId = "strike_replace",
                DisplayName = "Strike and replace",
                Description = "Exactly two items: a wrong claim that appears, is struck through and dims, then its correction popping in the accent color. Anchor the first item on the claim and the second on the correcting word.",
                Slots = new[] { Items(2, 2, 28, "The claim, then its correction.") },
                Parameters = new[] {
                    Accent("Color of the correction; when omitted the text style highlight color is used."),
                    new EffectParameterAsset { Name = "strike_color", Type = EffectParameterType.Color, DefaultValue = new float4(1f, 0.231f, 0.188f, 1f), Minimum = 0, Maximum = 1, Description = "Color of the strike-through line." },
                    new EffectParameterAsset { Name = "dim_struck", Type = EffectParameterType.Float, DefaultValue = new float4(0.55f, 0, 0, 0), Minimum = 0.1f, Maximum = 1, Description = "Share of its own color the struck claim keeps once the correction appears; the rest shows the dim color, with outline and shadow at full strength." },
                    DimTint()
                },
                Layouts = Layouts(-0.12f, 0.5f),
                Elements = new[] {
                    new GraphicTemplateElementAsset {
                        Name = "claim", Repeat = GraphicElementRepeat.FirstItem, Order = 1,
                        Tracks = new[] {
                            Track(GraphicAnimatedProperty.Opacity, GraphicTimeAnchor.Item, "", Key(0f, 0f, "smoothstep.v1"), Key(FadeSeconds, 1f, "linear.v1")),
                            Track(GraphicAnimatedProperty.Scale, GraphicTimeAnchor.Item, "", Key(0f, 0.85f, "ease_out_back.v1"), Key(PopSeconds, 1f, "linear.v1")),
                            Track(GraphicAnimatedProperty.Opacity, GraphicTimeAnchor.NextItem, "", Key(0f, 1f, "smoothstep.v1"), new GraphicTemplateKeyframeAsset { OffsetSeconds = DimSeconds, ValueParameter = "dim_struck" })
                        }
                    },
                    new GraphicTemplateElementAsset {
                        Name = "strike", Repeat = GraphicElementRepeat.FirstItem, Content = GraphicElementContent.Bar, Color = GraphicElementColor.Parameter, ColorParameter = "strike_color",
                        OffsetY = -0.1f, BarThickness = 0.085f, Order = 3,
                        Tracks = new[] {
                            Track(GraphicAnimatedProperty.Reveal, GraphicTimeAnchor.NextItem, "", Key(-0.2f, 0f, "ease_out_cubic.v1"), Key(0.02f, 1f, "linear.v1"))
                        }
                    },
                    new GraphicTemplateElementAsset {
                        Name = "correction", Repeat = GraphicElementRepeat.LastItem, Color = GraphicElementColor.Accent, ColorParameter = AccentColor, Order = 2,
                        Tracks = Entrance(0.06f, 0.8f)
                    },
                    Dimmed("claim_dimmed", GraphicElementRepeat.FirstItem, "")
                }
            };
        }

        /// <summary>
        /// Builds the required item list slot.
        /// </summary>
        /// <param name="minimum">Fewest items.</param>
        /// <param name="maximum">Most items.</param>
        /// <param name="maxChars">Longest item text.</param>
        /// <param name="description">Explanation for planners.</param>
        /// <returns>Item slot.</returns>
        static GraphicTemplateSlotAsset Items(int minimum, int maximum, int maxChars, string description) {
            return new GraphicTemplateSlotAsset { Name = GraphicTemplateValidator.ItemsSlot, Kind = GraphicTemplateSlotKind.TextList, Required = true, MinCount = minimum, MaxCount = maximum, MaxChars = maxChars, Description = description };
        }

        /// <summary>
        /// Builds the accent color parameter; its default mirrors the caption style's default highlight yellow.
        /// </summary>
        /// <param name="description">Explanation for planners.</param>
        /// <returns>Accent color parameter.</returns>
        static EffectParameterAsset Accent(string description) {
            return new EffectParameterAsset { Name = AccentColor, Type = EffectParameterType.Color, DefaultValue = new float4(1f, 0.902f, 0f, 1f), Minimum = 0, Maximum = 1, Description = description };
        }

        /// <summary>
        /// Builds the switch that dims earlier items when the next one appears.
        /// </summary>
        /// <param name="enabled">Default state.</param>
        /// <returns>Dim switch parameter.</returns>
        static EffectParameterAsset DimSwitch(bool enabled) {
            return new EffectParameterAsset { Name = DimPrevious, Type = EffectParameterType.Bool, DefaultValue = new float4(enabled ? 1 : 0, 0, 0, 0), Minimum = 0, Maximum = 1, Description = "Dim earlier items when the next one appears." };
        }

        /// <summary>
        /// Builds the opacity earlier items dim to.
        /// </summary>
        /// <param name="level">Default opacity.</param>
        /// <returns>Dim opacity parameter.</returns>
        static EffectParameterAsset DimLevel(float level) {
            return new EffectParameterAsset { Name = DimOpacity, Type = EffectParameterType.Float, DefaultValue = new float4(level, 0, 0, 0), Minimum = 0.1f, Maximum = 1, Description = "Share of its own color a dimmed item keeps; the rest shows the dim color, with outline and shadow at full strength." };
        }

        /// <summary>
        /// Builds the neutral color dimmed items fade toward. A mid gray keeps enough contrast against white pictures and
        /// against dark ones, whatever the text color.
        /// </summary>
        /// <returns>Dim color parameter.</returns>
        static EffectParameterAsset DimTint() {
            return new EffectParameterAsset { Name = DimColor, Type = EffectParameterType.Color, DefaultValue = new float4(0.5f, 0.5f, 0.5f, 1f), Minimum = 0, Maximum = 1, Description = "Color dimmed items fade toward." };
        }

        /// <summary>
        /// Builds the copy drawn under an item once the next item appears: the same text in the dim color with the style's
        /// outline and shadow, fully opaque, so the item fading over it settles on a muted, still legible mix instead of
        /// turning see-through.
        /// </summary>
        /// <param name="name">Element name.</param>
        /// <param name="repeat">Items that get a copy, matching the items that dim.</param>
        /// <param name="enabledParameter">Switch that enables dimming, or empty when the dim always happens.</param>
        /// <returns>Underlay element, ordered below every other element.</returns>
        static GraphicTemplateElementAsset Dimmed(string name, GraphicElementRepeat repeat, string enabledParameter) {
            return new GraphicTemplateElementAsset {
                Name = name, Repeat = repeat, Color = GraphicElementColor.Parameter, ColorParameter = DimColor, Order = 0, EnabledParameter = enabledParameter,
                Tracks = new[] { Track(GraphicAnimatedProperty.Opacity, GraphicTimeAnchor.NextItem, "", Key(0f, 1f, "linear.v1")) }
            };
        }

        /// <summary>
        /// Builds the vertical and horizontal layouts.
        /// </summary>
        /// <param name="vertical">Vertical gap in ems.</param>
        /// <param name="horizontal">Horizontal gap in ems.</param>
        /// <returns>Both layouts, vertical first.</returns>
        static GraphicTemplateLayoutAsset[] Layouts(float vertical, float horizontal) {
            return new[] {
                new GraphicTemplateLayoutAsset { Direction = GraphicLayoutDirection.Vertical, Gap = vertical },
                new GraphicTemplateLayoutAsset { Direction = GraphicLayoutDirection.Horizontal, Gap = horizontal }
            };
        }

        /// <summary>
        /// Builds the house pop: a fade in and a back-out scale from <paramref name="fromScale"/>, both starting
        /// <paramref name="delay"/> seconds after the item moment.
        /// </summary>
        /// <param name="delay">Seconds after the item moment.</param>
        /// <param name="fromScale">Starting scale.</param>
        /// <returns>Opacity and scale tracks.</returns>
        static GraphicTemplateTrackAsset[] Entrance(float delay, float fromScale) {
            return new[] {
                Track(GraphicAnimatedProperty.Opacity, GraphicTimeAnchor.Item, "", Key(delay, 0f, "smoothstep.v1"), Key(delay + FadeSeconds, 1f, "linear.v1")),
                Track(GraphicAnimatedProperty.Scale, GraphicTimeAnchor.Item, "", Key(delay, fromScale, "ease_out_back.v1"), Key(delay + PopSeconds, 1f, "linear.v1"))
            };
        }

        /// <summary>
        /// Builds the list entrance: a fade in, a short rise from below and a gentle back-out scale.
        /// </summary>
        /// <returns>Opacity, offset and scale tracks.</returns>
        static GraphicTemplateTrackAsset[] Rise() {
            return new[] {
                Track(GraphicAnimatedProperty.Opacity, GraphicTimeAnchor.Item, "", Key(0f, 0f, "smoothstep.v1"), Key(0.2f, 1f, "linear.v1")),
                Track(GraphicAnimatedProperty.OffsetY, GraphicTimeAnchor.Item, "", Key(0f, 0.3f, "ease_out_cubic.v1"), Key(0.26f, 0f, "linear.v1")),
                Track(GraphicAnimatedProperty.Scale, GraphicTimeAnchor.Item, "", Key(0f, 0.92f, "ease_out_back.v1"), Key(0.26f, 1f, "linear.v1"))
            };
        }

        /// <summary>
        /// Builds the dim applied to an item when the next item appears, enabled by the dim switch.
        /// </summary>
        /// <returns>Opacity track anchored on the next item.</returns>
        static GraphicTemplateTrackAsset Dim() {
            return Track(GraphicAnimatedProperty.Opacity, GraphicTimeAnchor.NextItem, DimPrevious,
                Key(0f, 1f, "smoothstep.v1"), new GraphicTemplateKeyframeAsset { OffsetSeconds = DimSeconds, ValueParameter = DimOpacity });
        }

        /// <summary>
        /// Builds one track.
        /// </summary>
        /// <param name="property">Animated property.</param>
        /// <param name="anchor">Anchor moment.</param>
        /// <param name="enabledParameter">Bool parameter enabling the track, or empty.</param>
        /// <param name="keyframes">Keyframes in offset order.</param>
        /// <returns>Track.</returns>
        static GraphicTemplateTrackAsset Track(GraphicAnimatedProperty property, GraphicTimeAnchor anchor, string enabledParameter, params GraphicTemplateKeyframeAsset[] keyframes) {
            return new GraphicTemplateTrackAsset { Property = property, Anchor = anchor, EnabledParameter = enabledParameter, Keyframes = keyframes };
        }

        /// <summary>
        /// Builds one literal keyframe.
        /// </summary>
        /// <param name="offset">Seconds from the anchor.</param>
        /// <param name="value">Property value.</param>
        /// <param name="curve">Curve toward the next keyframe.</param>
        /// <returns>Keyframe.</returns>
        static GraphicTemplateKeyframeAsset Key(float offset, float value, string curve) {
            return new GraphicTemplateKeyframeAsset { OffsetSeconds = offset, Value = value, Curve = curve };
        }
    }
}
