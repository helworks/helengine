using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Rejects graphic template definitions the compiler could not expand deterministically: missing or misnamed slots,
    /// parameters of shapes templates cannot use, elements that reference unknown parameters or slots, and animation
    /// tracks with unknown curves, unordered keyframes or values outside their property range. Runs when a template is
    /// saved, loaded or registered, so a broken <c>.hgraphic</c> fails with a clear message before any edit uses it.
    /// </summary>
    public static class GraphicTemplateValidator {
        /// <summary>
        /// Name of the required item list slot.
        /// </summary>
        public const string ItemsSlot = "items";

        /// <summary>
        /// Name of the optional separator text slot.
        /// </summary>
        public const string SeparatorSlot = "separator";

        /// <summary>
        /// Name of the optional accent item index slot.
        /// </summary>
        public const string AccentItemSlot = "accent_item";

        /// <summary>
        /// Most items one graphic may show.
        /// </summary>
        public const int MaxItems = 16;

        /// <summary>
        /// Validates one template definition.
        /// </summary>
        /// <param name="template">Template to check.</param>
        /// <exception cref="InvalidDataException">The definition is inconsistent; the message names the problem.</exception>
        public static void Validate(GraphicTemplateAsset template) {
            if (template == null) {
                throw new ArgumentNullException(nameof(template));
            }
            string id = template.TemplateId;
            if (string.IsNullOrWhiteSpace(id)) {
                throw new InvalidDataException("Graphic template requires a template id.");
            }
            if (template.TemplateVersion < 1) {
                throw new InvalidDataException($"Graphic template '{id}' version must be at least 1.");
            }
            if (!float.IsFinite(template.ExitSeconds) || template.ExitSeconds < 0 || template.ExitSeconds > 2) {
                throw new InvalidDataException($"Graphic template '{id}' exit must be between 0 and 2 seconds.");
            }
            if (template.Slots == null || template.Parameters == null || template.Layouts == null || template.Elements == null) {
                throw new InvalidDataException($"Graphic template '{id}' has a missing slot, parameter, layout or element list.");
            }
            ValidateSlots(template, id);
            ValidateParameters(template, id);
            ValidateLayouts(template, id);
            if (template.Elements.Length == 0) {
                throw new InvalidDataException($"Graphic template '{id}' must declare at least one element.");
            }
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (GraphicTemplateElementAsset element in template.Elements) {
                if (element == null || string.IsNullOrWhiteSpace(element.Name) || !element.Name.All(character => char.IsAsciiLetterOrDigit(character) || character == '_') || !names.Add(element.Name)) {
                    throw new InvalidDataException($"Graphic template '{id}' elements need unique names made of letters, digits and underscores.");
                }
                ValidateElement(template, element, id);
            }
        }

        /// <summary>
        /// Finds a slot by name.
        /// </summary>
        /// <param name="template">Template.</param>
        /// <param name="name">Slot name.</param>
        /// <returns>Slot, or null when the template does not declare it.</returns>
        public static GraphicTemplateSlotAsset FindSlot(GraphicTemplateAsset template, string name) {
            return template.Slots.FirstOrDefault(slot => slot.Name == name);
        }

        /// <summary>
        /// Finds a parameter by name.
        /// </summary>
        /// <param name="template">Template.</param>
        /// <param name="name">Parameter name.</param>
        /// <returns>Parameter, or null when the template does not declare it.</returns>
        public static EffectParameterAsset FindParameter(GraphicTemplateAsset template, string name) {
            return template.Parameters.FirstOrDefault(parameter => parameter.Name == name);
        }

        /// <summary>
        /// Checks the slot list: exactly the known slot names with their fixed kinds and sane limits.
        /// </summary>
        /// <param name="template">Template.</param>
        /// <param name="id">Template id used in messages.</param>
        static void ValidateSlots(GraphicTemplateAsset template, string id) {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (GraphicTemplateSlotAsset slot in template.Slots) {
                if (slot == null || !names.Add(slot.Name ?? "")) {
                    throw new InvalidDataException($"Graphic template '{id}' slots must be present and unique.");
                }
                GraphicTemplateSlotKind expected = slot.Name switch {
                    ItemsSlot => GraphicTemplateSlotKind.TextList,
                    SeparatorSlot => GraphicTemplateSlotKind.Text,
                    AccentItemSlot => GraphicTemplateSlotKind.ItemIndex,
                    _ => throw new InvalidDataException($"Graphic template '{id}' slot '{slot.Name}' is unknown; use {ItemsSlot}, {SeparatorSlot} or {AccentItemSlot}.")
                };
                if (slot.Kind != expected) {
                    throw new InvalidDataException($"Graphic template '{id}' slot '{slot.Name}' must be of kind {expected}.");
                }
                if (slot.Kind != GraphicTemplateSlotKind.ItemIndex && (slot.MaxChars < 1 || slot.MaxChars > 200)) {
                    throw new InvalidDataException($"Graphic template '{id}' slot '{slot.Name}' needs a character limit between 1 and 200.");
                }
                if (slot.Kind == GraphicTemplateSlotKind.Text && (slot.DefaultText ?? "").Length > slot.MaxChars) {
                    throw new InvalidDataException($"Graphic template '{id}' slot '{slot.Name}' default is longer than its limit.");
                }
            }
            GraphicTemplateSlotAsset items = FindSlot(template, ItemsSlot);
            if (items == null || !items.Required || items.MinCount < 1 || items.MaxCount < items.MinCount || items.MaxCount > MaxItems) {
                throw new InvalidDataException($"Graphic template '{id}' needs a required '{ItemsSlot}' slot accepting between 1 and {MaxItems} entries.");
            }
            GraphicTemplateSlotAsset separator = FindSlot(template, SeparatorSlot);
            if (separator != null && separator.Required) {
                throw new InvalidDataException($"Graphic template '{id}' separator slot must be optional; its default applies when it is empty.");
            }
        }

        /// <summary>
        /// Checks the parameters: unique names and only the scalar, color, switch and choice shapes elements can use.
        /// </summary>
        /// <param name="template">Template.</param>
        /// <param name="id">Template id used in messages.</param>
        static void ValidateParameters(GraphicTemplateAsset template, string id) {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (EffectParameterAsset parameter in template.Parameters) {
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name) || !names.Add(parameter.Name)) {
                    throw new InvalidDataException($"Graphic template '{id}' parameters need unique names.");
                }
                if (parameter.Type is not (EffectParameterType.Float or EffectParameterType.Integer or EffectParameterType.Color or EffectParameterType.Bool or EffectParameterType.Enum)) {
                    throw new InvalidDataException($"Graphic template '{id}' parameter '{parameter.Name}' must be a float, integer, color, bool or enum.");
                }
                if (!float.IsFinite(parameter.Minimum) || !float.IsFinite(parameter.Maximum) || parameter.Minimum > parameter.Maximum) {
                    throw new InvalidDataException($"Graphic template '{id}' parameter '{parameter.Name}' has an invalid range.");
                }
                if (parameter.Type == EffectParameterType.Enum && (parameter.AllowedValues == null || parameter.AllowedValues.Length == 0 || parameter.DefaultValue.X < 0 || parameter.DefaultValue.X >= parameter.AllowedValues.Length)) {
                    throw new InvalidDataException($"Graphic template '{id}' enum parameter '{parameter.Name}' needs allowed values and a default index among them.");
                }
                if (parameter.Type is EffectParameterType.Float or EffectParameterType.Integer && (parameter.DefaultValue.X < parameter.Minimum || parameter.DefaultValue.X > parameter.Maximum)) {
                    throw new InvalidDataException($"Graphic template '{id}' parameter '{parameter.Name}' default is outside its range.");
                }
            }
        }

        /// <summary>
        /// Checks the layouts: one or two distinct directions with finite gaps.
        /// </summary>
        /// <param name="template">Template.</param>
        /// <param name="id">Template id used in messages.</param>
        static void ValidateLayouts(GraphicTemplateAsset template, string id) {
            if (template.Layouts.Length == 0 || template.Layouts.Any(layout => layout == null || !Enum.IsDefined(layout.Direction) || !float.IsFinite(layout.Gap) || layout.Gap < -1 || layout.Gap > 4)
                || template.Layouts.Select(layout => layout.Direction).Distinct().Count() != template.Layouts.Length) {
                throw new InvalidDataException($"Graphic template '{id}' needs one layout per direction with a gap between -1 and 4 ems.");
            }
        }

        /// <summary>
        /// Checks one element: its repeat, content and color references, static geometry and tracks.
        /// </summary>
        /// <param name="template">Template.</param>
        /// <param name="element">Element.</param>
        /// <param name="id">Template id used in messages.</param>
        static void ValidateElement(GraphicTemplateAsset template, GraphicTemplateElementAsset element, string id) {
            string name = $"Graphic template '{id}' element '{element.Name}'";
            if (!Enum.IsDefined(element.Repeat) || !Enum.IsDefined(element.Content) || !Enum.IsDefined(element.Color)) {
                throw new InvalidDataException($"{name} has an unknown repeat, content or color.");
            }
            bool between = element.Repeat == GraphicElementRepeat.BetweenItems, separator = element.Content == GraphicElementContent.SeparatorText;
            if (between != separator) {
                throw new InvalidDataException($"{name}: separator text is drawn between items, and only separator text is.");
            }
            if (separator && FindSlot(template, SeparatorSlot) == null) {
                throw new InvalidDataException($"{name} draws a separator but the template has no '{SeparatorSlot}' slot.");
            }
            if (element.Repeat is GraphicElementRepeat.AccentItem or GraphicElementRepeat.EachItemExceptAccent && FindSlot(template, AccentItemSlot) == null) {
                throw new InvalidDataException($"{name} depends on the accent item but the template has no '{AccentItemSlot}' slot.");
            }
            if (element.Color is GraphicElementColor.Parameter or GraphicElementColor.Accent) {
                EffectParameterAsset color = FindParameter(template, element.ColorParameter ?? "");
                if (color == null || color.Type != EffectParameterType.Color) {
                    throw new InvalidDataException($"{name} color parameter '{element.ColorParameter}' is not a color parameter.");
                }
            }
            Switch(template, element.EnabledParameter, name);
            if (!float.IsFinite(element.FontScale) || element.FontScale <= 0 || element.FontScale > 4 || !float.IsFinite(element.OffsetX) || Math.Abs(element.OffsetX) > 4
                || !float.IsFinite(element.OffsetY) || Math.Abs(element.OffsetY) > 4 || !float.IsFinite(element.BarThickness) || element.BarThickness <= 0 || element.BarThickness > 1) {
                throw new InvalidDataException($"{name} needs a font scale in (0, 4], offsets within 4 ems and a bar thickness in (0, 1] ems.");
            }
            if (element.Tracks == null) {
                throw new InvalidDataException($"{name} has a missing track list.");
            }
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (GraphicTemplateTrackAsset track in element.Tracks) {
                if (track == null || !Enum.IsDefined(track.Property) || !Enum.IsDefined(track.Anchor) || !seen.Add(track.Property + "/" + track.Anchor)) {
                    throw new InvalidDataException($"{name} tracks need a known property and anchor, at most one track per property and anchor.");
                }
                ValidateTrack(template, element, track, name);
            }
            HashSet<GraphicAnimatedProperty> properties = element.Tracks.Select(track => track.Property).ToHashSet();
            if (properties.Contains(GraphicAnimatedProperty.Reveal) && (element.Content != GraphicElementContent.Bar || properties.Contains(GraphicAnimatedProperty.OffsetX) || properties.Contains(GraphicAnimatedProperty.Scale))) {
                throw new InvalidDataException($"{name}: reveal animates bars only and cannot be combined with offset_x or scale.");
            }
            if (element.Content == GraphicElementContent.Bar && properties.Contains(GraphicAnimatedProperty.Scale)) {
                throw new InvalidDataException($"{name}: bars keep their thickness and cannot animate scale.");
            }
        }

        /// <summary>
        /// Checks one track: ordered finite offsets, catalog curves, and values or value parameters inside the property range.
        /// </summary>
        /// <param name="template">Template.</param>
        /// <param name="element">Owning element.</param>
        /// <param name="track">Track.</param>
        /// <param name="name">Element description used in messages.</param>
        static void ValidateTrack(GraphicTemplateAsset template, GraphicTemplateElementAsset element, GraphicTemplateTrackAsset track, string name) {
            Switch(template, track.EnabledParameter, name);
            if (track.Keyframes == null || track.Keyframes.Length == 0 || track.Keyframes.Length > 16) {
                throw new InvalidDataException($"{name} {track.Property} track needs between 1 and 16 keyframes.");
            }
            double minimum = Minimum(track.Property), maximum = Maximum(track.Property), previous = double.NegativeInfinity;
            foreach (GraphicTemplateKeyframeAsset keyframe in track.Keyframes) {
                if (keyframe == null || !float.IsFinite(keyframe.OffsetSeconds) || keyframe.OffsetSeconds <= previous || Math.Abs(keyframe.OffsetSeconds) > 5 || !MediaCurve.Supports(keyframe.Curve)) {
                    throw new InvalidDataException($"{name} {track.Property} keyframes need increasing offsets within 5 seconds and catalog curves.");
                }
                previous = keyframe.OffsetSeconds;
                if (string.IsNullOrEmpty(keyframe.ValueParameter)) {
                    if (!float.IsFinite(keyframe.Value) || keyframe.Value < minimum || keyframe.Value > maximum) {
                        throw new InvalidDataException($"{name} {track.Property} value {keyframe.Value} is outside {minimum}..{maximum}.");
                    }
                } else {
                    EffectParameterAsset parameter = FindParameter(template, keyframe.ValueParameter);
                    if (parameter == null || parameter.Type is not (EffectParameterType.Float or EffectParameterType.Integer) || parameter.Minimum < minimum || parameter.Maximum > maximum) {
                        throw new InvalidDataException($"{name} {track.Property} value parameter '{keyframe.ValueParameter}' must be a number whose range fits {minimum}..{maximum}.");
                    }
                }
            }
        }

        /// <summary>
        /// Checks that an enabling parameter name is empty or names a boolean parameter.
        /// </summary>
        /// <param name="template">Template.</param>
        /// <param name="parameter">Parameter name, possibly empty.</param>
        /// <param name="name">Element description used in messages.</param>
        static void Switch(GraphicTemplateAsset template, string parameter, string name) {
            if (!string.IsNullOrEmpty(parameter) && FindParameter(template, parameter)?.Type != EffectParameterType.Bool) {
                throw new InvalidDataException($"{name} enabling parameter '{parameter}' is not a bool parameter.");
            }
        }

        /// <summary>
        /// Lowest value a property may take in a template keyframe.
        /// </summary>
        /// <param name="property">Animated property.</param>
        /// <returns>Inclusive lower bound.</returns>
        public static double Minimum(GraphicAnimatedProperty property) {
            return property switch {
                GraphicAnimatedProperty.Scale => 0.05,
                GraphicAnimatedProperty.OffsetX or GraphicAnimatedProperty.OffsetY => -4,
                _ => 0
            };
        }

        /// <summary>
        /// Highest value a property may take in a template keyframe.
        /// </summary>
        /// <param name="property">Animated property.</param>
        /// <returns>Inclusive upper bound.</returns>
        public static double Maximum(GraphicAnimatedProperty property) {
            return property switch {
                GraphicAnimatedProperty.Scale => 4,
                GraphicAnimatedProperty.OffsetX or GraphicAnimatedProperty.OffsetY => 4,
                _ => 1
            };
        }
    }
}
