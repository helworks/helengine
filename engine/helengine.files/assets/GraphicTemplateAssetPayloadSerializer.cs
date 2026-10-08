using helengine;

namespace helengine.files {
    /// <summary>
    /// Serializes <see cref="GraphicTemplateAsset"/> payloads: the template identity, its slots, typed parameters, layouts
    /// and elements with their animation tracks, in declaration order so element draw order and keyframe order survive a
    /// round trip byte for byte. Parameters share the effect parameter encoding.
    /// </summary>
    public sealed class GraphicTemplateAssetPayloadSerializer : IEditorAssetPayloadSerializer {
        /// <summary>
        /// Gets the value kind stamped into the HELE header of graphic template payloads.
        /// </summary>
        public EditorAssetBinaryValueKind ValueKind {
            get {
                return EditorAssetBinaryValueKind.GraphicTemplateAsset;
            }
        }

        /// <summary>
        /// Reports whether the supplied asset is a graphic template.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        /// <returns>True for <see cref="GraphicTemplateAsset"/> instances.</returns>
        public bool Handles(Asset asset) {
            return asset is GraphicTemplateAsset;
        }

        /// <summary>
        /// Rejects templates whose identity or nested lists are missing before any byte is written.
        /// </summary>
        /// <param name="asset">Template about to be serialized.</param>
        public void Validate(Asset asset) {
            GraphicTemplateAsset template = (GraphicTemplateAsset)asset;
            if (string.IsNullOrWhiteSpace(template.TemplateId)) {
                throw new InvalidOperationException("Graphic template assets require a template id.");
            }
            if (template.Slots == null || template.Parameters == null || template.Layouts == null || template.Elements == null) {
                throw new InvalidOperationException($"Graphic template '{template.TemplateId}' has a missing slot, parameter, layout or element list.");
            }
            foreach (GraphicTemplateElementAsset element in template.Elements) {
                if (element == null || element.Tracks == null || element.Tracks.Any(track => track == null || track.Keyframes == null)) {
                    throw new InvalidOperationException($"Graphic template '{template.TemplateId}' has an element with a missing track or keyframe list.");
                }
            }
        }

        /// <summary>
        /// Writes the template payload after the shared HELE header.
        /// </summary>
        /// <param name="writer">Destination writer positioned after the header.</param>
        /// <param name="asset">Template to serialize.</param>
        public void Write(EngineBinaryWriter writer, Asset asset) {
            GraphicTemplateAsset template = (GraphicTemplateAsset)asset;
            EditorAssetPayloadPrimitives.EnsureRuntimeAssetIdentity(template);
            EditorAssetPayloadPrimitives.WriteAssetIdentity(writer, template);
            writer.WriteString(template.TemplateId);
            writer.WriteString(template.DisplayName ?? string.Empty);
            writer.WriteString(template.Description ?? string.Empty);
            writer.WriteInt32(template.TemplateVersion);
            writer.WriteSingle(template.ExitSeconds);
            writer.WriteArray(template.Slots, WriteSlot);
            writer.WriteArray(template.Parameters, EffectAssetPayloadSerializer.WriteParameter);
            writer.WriteArray(template.Layouts, WriteLayout);
            writer.WriteArray(template.Elements, WriteElement);
        }

        /// <summary>
        /// Reads one template payload produced by <see cref="Write"/>.
        /// </summary>
        /// <param name="reader">Source reader positioned after the header.</param>
        /// <returns>Deserialized graphic template.</returns>
        public Asset Read(EngineBinaryReader reader) {
            GraphicTemplateAsset template = new GraphicTemplateAsset();
            EditorAssetPayloadPrimitives.ReadAssetIdentity(reader, template);
            template.TemplateId = reader.ReadString();
            template.DisplayName = reader.ReadString();
            template.Description = reader.ReadString();
            template.TemplateVersion = reader.ReadInt32();
            template.ExitSeconds = reader.ReadSingle();
            template.Slots = reader.ReadArray(ReadSlot) ?? Array.Empty<GraphicTemplateSlotAsset>();
            template.Parameters = reader.ReadArray(EffectAssetPayloadSerializer.ReadParameter) ?? Array.Empty<EffectParameterAsset>();
            template.Layouts = reader.ReadArray(ReadLayout) ?? Array.Empty<GraphicTemplateLayoutAsset>();
            template.Elements = reader.ReadArray(ReadElement) ?? Array.Empty<GraphicTemplateElementAsset>();
            return template;
        }

        /// <summary>
        /// Writes one slot declaration.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="slot">Slot to write.</param>
        static void WriteSlot(EngineBinaryWriter writer, GraphicTemplateSlotAsset slot) {
            writer.WriteString(slot.Name);
            writer.WriteString(slot.Description ?? string.Empty);
            writer.WriteInt32((int)slot.Kind);
            writer.WriteByte(slot.Required ? (byte)1 : (byte)0);
            writer.WriteInt32(slot.MinCount);
            writer.WriteInt32(slot.MaxCount);
            writer.WriteInt32(slot.MaxChars);
            writer.WriteString(slot.DefaultText ?? string.Empty);
        }

        /// <summary>
        /// Reads one slot declaration.
        /// </summary>
        /// <param name="reader">Source reader.</param>
        /// <returns>Deserialized slot.</returns>
        static GraphicTemplateSlotAsset ReadSlot(EngineBinaryReader reader) {
            return new GraphicTemplateSlotAsset {
                Name = reader.ReadString(),
                Description = reader.ReadString(),
                Kind = (GraphicTemplateSlotKind)reader.ReadInt32(),
                Required = reader.ReadByte() != 0,
                MinCount = reader.ReadInt32(),
                MaxCount = reader.ReadInt32(),
                MaxChars = reader.ReadInt32(),
                DefaultText = reader.ReadString()
            };
        }

        /// <summary>
        /// Writes one supported layout.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="layout">Layout to write.</param>
        static void WriteLayout(EngineBinaryWriter writer, GraphicTemplateLayoutAsset layout) {
            writer.WriteInt32((int)layout.Direction);
            writer.WriteSingle(layout.Gap);
        }

        /// <summary>
        /// Reads one supported layout.
        /// </summary>
        /// <param name="reader">Source reader.</param>
        /// <returns>Deserialized layout.</returns>
        static GraphicTemplateLayoutAsset ReadLayout(EngineBinaryReader reader) {
            return new GraphicTemplateLayoutAsset {
                Direction = (GraphicLayoutDirection)reader.ReadInt32(),
                Gap = reader.ReadSingle()
            };
        }

        /// <summary>
        /// Writes one element with its tracks.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="element">Element to write.</param>
        static void WriteElement(EngineBinaryWriter writer, GraphicTemplateElementAsset element) {
            writer.WriteString(element.Name);
            writer.WriteInt32((int)element.Repeat);
            writer.WriteInt32((int)element.Content);
            writer.WriteInt32((int)element.Color);
            writer.WriteString(element.ColorParameter ?? string.Empty);
            writer.WriteSingle(element.FontScale);
            writer.WriteSingle(element.OffsetX);
            writer.WriteSingle(element.OffsetY);
            writer.WriteSingle(element.BarThickness);
            writer.WriteInt32(element.Order);
            writer.WriteString(element.EnabledParameter ?? string.Empty);
            writer.WriteArray(element.Tracks, WriteTrack);
        }

        /// <summary>
        /// Reads one element with its tracks.
        /// </summary>
        /// <param name="reader">Source reader.</param>
        /// <returns>Deserialized element.</returns>
        static GraphicTemplateElementAsset ReadElement(EngineBinaryReader reader) {
            return new GraphicTemplateElementAsset {
                Name = reader.ReadString(),
                Repeat = (GraphicElementRepeat)reader.ReadInt32(),
                Content = (GraphicElementContent)reader.ReadInt32(),
                Color = (GraphicElementColor)reader.ReadInt32(),
                ColorParameter = reader.ReadString(),
                FontScale = reader.ReadSingle(),
                OffsetX = reader.ReadSingle(),
                OffsetY = reader.ReadSingle(),
                BarThickness = reader.ReadSingle(),
                Order = reader.ReadInt32(),
                EnabledParameter = reader.ReadString(),
                Tracks = reader.ReadArray(ReadTrack) ?? Array.Empty<GraphicTemplateTrackAsset>()
            };
        }

        /// <summary>
        /// Writes one animation track.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="track">Track to write.</param>
        static void WriteTrack(EngineBinaryWriter writer, GraphicTemplateTrackAsset track) {
            writer.WriteInt32((int)track.Property);
            writer.WriteInt32((int)track.Anchor);
            writer.WriteString(track.EnabledParameter ?? string.Empty);
            writer.WriteArray(track.Keyframes, WriteKeyframe);
        }

        /// <summary>
        /// Reads one animation track.
        /// </summary>
        /// <param name="reader">Source reader.</param>
        /// <returns>Deserialized track.</returns>
        static GraphicTemplateTrackAsset ReadTrack(EngineBinaryReader reader) {
            return new GraphicTemplateTrackAsset {
                Property = (GraphicAnimatedProperty)reader.ReadInt32(),
                Anchor = (GraphicTimeAnchor)reader.ReadInt32(),
                EnabledParameter = reader.ReadString(),
                Keyframes = reader.ReadArray(ReadKeyframe) ?? Array.Empty<GraphicTemplateKeyframeAsset>()
            };
        }

        /// <summary>
        /// Writes one keyframe.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="keyframe">Keyframe to write.</param>
        static void WriteKeyframe(EngineBinaryWriter writer, GraphicTemplateKeyframeAsset keyframe) {
            writer.WriteSingle(keyframe.OffsetSeconds);
            writer.WriteSingle(keyframe.Value);
            writer.WriteString(keyframe.ValueParameter ?? string.Empty);
            writer.WriteString(keyframe.Curve ?? string.Empty);
        }

        /// <summary>
        /// Reads one keyframe.
        /// </summary>
        /// <param name="reader">Source reader.</param>
        /// <returns>Deserialized keyframe.</returns>
        static GraphicTemplateKeyframeAsset ReadKeyframe(EngineBinaryReader reader) {
            return new GraphicTemplateKeyframeAsset {
                OffsetSeconds = reader.ReadSingle(),
                Value = reader.ReadSingle(),
                ValueParameter = reader.ReadString(),
                Curve = reader.ReadString()
            };
        }
    }
}
