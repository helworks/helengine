using helengine;
using helengine.files;
using Xunit;

namespace helengine.files.tests.assets {
    /// <summary>
    /// Verifies that a kinetic typography template survives the HELE editor asset format intact, so built-in and
    /// project-authored <c>.hgraphic</c> files load with the same slots, parameters, layouts, elements and keyframes.
    /// </summary>
    public class GraphicTemplateAssetPayloadSerializerTests {
        /// <summary>
        /// Every nested value of a template written and read back matches the original.
        /// </summary>
        [Fact]
        public void Serialize_thenDeserialize_preservesSlotsParametersLayoutsAndElements() {
            GraphicTemplateAsset original = new GraphicTemplateAsset {
                TemplateId = "contrast_chain",
                DisplayName = "Contrast chain",
                Description = "A differs from B.",
                TemplateVersion = 2,
                ExitSeconds = 0.25f,
                Slots = new[] {
                    new GraphicTemplateSlotAsset { Name = "items", Kind = GraphicTemplateSlotKind.TextList, Required = true, MinCount = 2, MaxCount = 4, MaxChars = 24 },
                    new GraphicTemplateSlotAsset { Name = "separator", Kind = GraphicTemplateSlotKind.Text, MaxChars = 3, DefaultText = "≠" }
                },
                Parameters = new[] {
                    new EffectParameterAsset { Name = "accent_color", Type = EffectParameterType.Color, DefaultValue = new float4(1, 0.9f, 0, 1), Minimum = 0, Maximum = 1 }
                },
                Layouts = new[] {
                    new GraphicTemplateLayoutAsset { Direction = GraphicLayoutDirection.Vertical, Gap = -0.2f },
                    new GraphicTemplateLayoutAsset { Direction = GraphicLayoutDirection.Horizontal, Gap = 0.35f }
                },
                Elements = new[] {
                    new GraphicTemplateElementAsset {
                        Name = "separator", Repeat = GraphicElementRepeat.BetweenItems, Content = GraphicElementContent.SeparatorText,
                        Color = GraphicElementColor.Accent, ColorParameter = "accent_color", FontScale = 0.8f, OffsetY = -0.1f, BarThickness = 0.1f, Order = 2, EnabledParameter = "show",
                        Tracks = new[] {
                            new GraphicTemplateTrackAsset {
                                Property = GraphicAnimatedProperty.Scale, Anchor = GraphicTimeAnchor.NextItem, EnabledParameter = "pop",
                                Keyframes = new[] {
                                    new GraphicTemplateKeyframeAsset { OffsetSeconds = -0.1f, Value = 0.6f, Curve = "ease_out_back.v1" },
                                    new GraphicTemplateKeyframeAsset { OffsetSeconds = 0.12f, ValueParameter = "size" }
                                }
                            }
                        }
                    }
                }
            };

            using MemoryStream stream = new MemoryStream();
            EditorAssetBinarySerializer.Serialize(stream, original);
            stream.Position = 0;
            GraphicTemplateAsset restored = Assert.IsType<GraphicTemplateAsset>(EditorAssetBinarySerializer.Deserialize(stream));

            Assert.Equal("contrast_chain", restored.TemplateId);
            Assert.Equal("Contrast chain", restored.DisplayName);
            Assert.Equal("A differs from B.", restored.Description);
            Assert.Equal(2, restored.TemplateVersion);
            Assert.Equal(0.25f, restored.ExitSeconds);
            Assert.Equal(2, restored.Slots.Length);
            Assert.True(restored.Slots[0].Required);
            Assert.Equal(4, restored.Slots[0].MaxCount);
            Assert.Equal("≠", restored.Slots[1].DefaultText);
            Assert.Equal(GraphicTemplateSlotKind.Text, restored.Slots[1].Kind);
            Assert.Equal(0.9f, Assert.Single(restored.Parameters).DefaultValue.Y);
            Assert.Equal(GraphicLayoutDirection.Horizontal, restored.Layouts[1].Direction);
            Assert.Equal(-0.2f, restored.Layouts[0].Gap);
            GraphicTemplateElementAsset element = Assert.Single(restored.Elements);
            Assert.Equal(GraphicElementRepeat.BetweenItems, element.Repeat);
            Assert.Equal(GraphicElementContent.SeparatorText, element.Content);
            Assert.Equal(GraphicElementColor.Accent, element.Color);
            Assert.Equal("accent_color", element.ColorParameter);
            Assert.Equal(0.8f, element.FontScale);
            Assert.Equal(-0.1f, element.OffsetY);
            Assert.Equal(0.1f, element.BarThickness);
            Assert.Equal(2, element.Order);
            Assert.Equal("show", element.EnabledParameter);
            GraphicTemplateTrackAsset track = Assert.Single(element.Tracks);
            Assert.Equal(GraphicAnimatedProperty.Scale, track.Property);
            Assert.Equal(GraphicTimeAnchor.NextItem, track.Anchor);
            Assert.Equal("pop", track.EnabledParameter);
            Assert.Equal(-0.1f, track.Keyframes[0].OffsetSeconds);
            Assert.Equal("ease_out_back.v1", track.Keyframes[0].Curve);
            Assert.Equal("size", track.Keyframes[1].ValueParameter);
        }

        /// <summary>
        /// A template without an id is rejected before any byte reaches the stream.
        /// </summary>
        [Fact]
        public void Serialize_whenTemplateIdIsMissing_throwsBeforeWriting() {
            using MemoryStream stream = new MemoryStream();
            Assert.Throws<InvalidOperationException>(() => EditorAssetBinarySerializer.Serialize(stream, new GraphicTemplateAsset()));
            Assert.Equal(0, stream.Length);
        }
    }
}
