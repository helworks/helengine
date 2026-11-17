using System;

namespace helengine.ui.format {
    /// <summary>A tagged property value; only the field selected by Kind is serialized.</summary>
    public sealed class UiProperty {
        /// <summary>Stable property identifier.</summary>
        public UiPropertyId Id = UiPropertyId.Fill;
        /// <summary>Selects Number, Integer, Boolean, Color or Text.</summary>
        public UiValueKind Kind = UiValueKind.Color;
        /// <summary>Finite float32 payload for Number.</summary>
        public float Number = 0;
        /// <summary>Signed 32-bit payload for Integer.</summary>
        public int Integer = 0;
        /// <summary>Boolean payload encoded as exactly zero or one.</summary>
        public bool Boolean = false;
        /// <summary>Packed RRGGBBAA color for Color, written as a little-endian uint32.</summary>
        public uint Color = 0;
        /// <summary>UTF-8 literal or exact binding key for text-like kinds.</summary>
        public string Text = "";
    }
}
