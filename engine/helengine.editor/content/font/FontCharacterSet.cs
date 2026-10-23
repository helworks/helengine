namespace helengine.editor {
    /// <summary>Describes an immutable, sorted selection of Unicode scalars for offline font generation, independently of the rasterizer and target platform.</summary>
    public sealed class FontCharacterSet : IEquatable<FontCharacterSet> {
        /// <summary>Canonical scalar values owned exclusively by this selection.</summary>
        readonly int[] CodePoints;

        /// <summary>Creates a selection from already validated canonical values owned by the caller.</summary>
        /// <param name="codePoints">Sorted unique Unicode scalar values.</param>
        FontCharacterSet(int[] codePoints) { CodePoints = codePoints; }

        /// <summary>Gets printable Basic Latin U+0020 through U+007E; accented characters require an explicit additional selection.</summary>
        public static FontCharacterSet BasicLatin { get; } = FromCodePoints(Enumerable.Range(32, 95));

        /// <summary>Gets uppercase and lowercase Portuguese accented letters, including the historical diaeresis, without adding Basic Latin.</summary>
        public static FontCharacterSet PortugueseAccents { get; } = FromText("\u00c0\u00c1\u00c2\u00c3\u00c7\u00c9\u00ca\u00cd\u00d3\u00d4\u00d5\u00da\u00dc\u00e0\u00e1\u00e2\u00e3\u00e7\u00e9\u00ea\u00ed\u00f3\u00f4\u00f5\u00fa\u00fc");

        /// <summary>Gets the previous importers' combined implicit coverage, used only when migrating persisted requests without a character list.</summary>
        internal static FontCharacterSet Legacy { get; } = BasicLatin.Union(FromText("\u00e0\u00e1\u00e3\u00e2\u00e4\u00c0\u00c1\u00c3\u00c2\u00c4\u00e9\u00ea\u00e8\u00eb\u00c9\u00ca\u00c8\u00cb\u00ed\u00ec\u00ee\u00ef\u00cd\u00cc\u00ce\u00cf\u00f3\u00f4\u00f5\u00f2\u00f6\u00d3\u00d4\u00d5\u00d2\u00d6\u00fa\u00f9\u00fb\u00fc\u00da\u00d9\u00db\u00dc\u00e7\u00c7"));

        /// <summary>Gets the number of distinct requested Unicode scalars.</summary>
        public int Count => CodePoints.Length;

        /// <summary>Creates a canonical selection and rejects values outside Unicode or within the UTF-16 surrogate range.</summary>
        /// <param name="codePoints">Scalar values; duplicates and input ordering do not affect identity.</param>
        /// <returns>An immutable selection owning a copy of the canonical values.</returns>
        public static FontCharacterSet FromCodePoints(IEnumerable<int> codePoints) {
            ArgumentNullException.ThrowIfNull(codePoints);
            SortedSet<int> values = new();
            foreach (int value in codePoints) {
                if (value < 0 || value > 0x10ffff || value >= 0xd800 && value <= 0xdfff) {
                    throw new ArgumentOutOfRangeException(nameof(codePoints), value, "Font characters must be valid Unicode scalar values.");
                }
                values.Add(value);
            }
            return new FontCharacterSet(values.ToArray());
        }

        /// <summary>Selects scalars appearing in valid UTF-16 text. CR, LF and TAB are layout commands and do not request atlas glyphs; combining marks remain explicit scalars.</summary>
        /// <param name="text">Text from which to collect glyph coverage without normalization or shaping.</param>
        /// <returns>The canonical selection represented by the text.</returns>
        public static FontCharacterSet FromText(string text) {
            ArgumentNullException.ThrowIfNull(text);
            List<int> values = new();
            for (int index = 0; index < text.Length; index++) {
                char character = text[index];
                if (character == '\r' || character == '\n' || character == '\t') { continue; }
                if (char.IsHighSurrogate(character)) {
                    if (index + 1 >= text.Length || !char.IsLowSurrogate(text[index + 1])) { throw new ArgumentException("Font text contains an unpaired UTF-16 surrogate.", nameof(text)); }
                    values.Add(char.ConvertToUtf32(character, text[++index]));
                } else if (char.IsLowSurrogate(character)) {
                    throw new ArgumentException("Font text contains an unpaired UTF-16 surrogate.", nameof(text));
                } else { values.Add(character); }
            }
            return FromCodePoints(values);
        }

        /// <summary>Combines two selections without mutating either input or changing canonical ordering.</summary>
        /// <param name="other">Additional scalar coverage.</param>
        /// <returns>The union of both requests.</returns>
        public FontCharacterSet Union(FontCharacterSet other) {
            ArgumentNullException.ThrowIfNull(other);
            return FromCodePoints(CodePoints.Concat(other.CodePoints));
        }

        /// <summary>Copies the canonical scalars so callers cannot mutate this selection.</summary>
        /// <returns>A caller-owned array in ascending scalar order.</returns>
        public int[] ToArray() => (int[])CodePoints.Clone();

        /// <summary>Adapts a nonempty selection to the current BMP-only cooked font contract, rejecting supplementary scalars instead of truncating them.</summary>
        /// <returns>Exactly the requested BMP characters, ready for rasterization.</returns>
        public char[] RequireBmpCharacters() {
            if (Count == 0) { throw new InvalidOperationException("Font generation requires at least one character."); }
            char[] characters = new char[Count];
            for (int index = 0; index < Count; index++) {
                if (CodePoints[index] > char.MaxValue) { throw new NotSupportedException($"The current cooked font format cannot represent U+{CodePoints[index]:X}; supplementary Unicode scalars require an extended runtime format."); }
                characters[index] = (char)CodePoints[index];
            }
            return characters;
        }

        /// <summary>Compares canonical scalar coverage rather than object identity.</summary>
        /// <param name="other">Selection to compare.</param>
        /// <returns>Whether both selections contain exactly the same scalars.</returns>
        public bool Equals(FontCharacterSet other) => other != null && CodePoints.SequenceEqual(other.CodePoints);

        /// <summary>Compares this selection with another character-selection value.</summary>
        /// <param name="obj">Value to compare.</param>
        /// <returns>Whether the value represents identical coverage.</returns>
        public override bool Equals(object obj) => obj is FontCharacterSet other && Equals(other);

        /// <summary>Hashes the canonical scalar sequence for in-process collections; persisted identities serialize the scalars themselves.</summary>
        /// <returns>A hash consistent with character-selection equality.</returns>
        public override int GetHashCode() {
            HashCode hash = new();
            foreach (int value in CodePoints) { hash.Add(value); }
            return hash.ToHashCode();
        }
    }
}