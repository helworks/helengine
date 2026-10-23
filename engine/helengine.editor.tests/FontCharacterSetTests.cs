namespace helengine.editor.tests {
    /// <summary>Checks backend-independent font selection and the actual settings codec without generating fonts or opening windows.</summary>
    public sealed class FontCharacterSetTests {
        /// <summary>Requires the public request type rather than a backend-specific implicit alphabet.</summary>
        static Type SelectionType {
            get {
                Type type = typeof(FontAssetProcessorSettings).Assembly.GetType("helengine.editor.FontCharacterSet");
                Assert.NotNull(type); return type;
            }
        }
        /// <summary>New requests contain printable ASCII exactly and do not implicitly include accented letters.</summary>
        [Fact]
        public void NewSettingsUseOnlyPrintableBasicLatin() {
            int[] values = Points(Preset("BasicLatin"));
            Assert.Equal(Enumerable.Range(32, 95).ToArray(), values);
            Assert.DoesNotContain(0x00e3, values);
            object current = Characters().GetValue(new FontAssetProcessorSettings());
            Assert.Equal(values, Points(current));
        }
        /// <summary>Portuguese glyphs appear only when explicitly combined with the base request.</summary>
        [Fact]
        public void PortugueseAccentsAreAnExplicitUnion() {
            object accented = Preset("PortugueseAccents");
            Assert.Contains(0x00e3, Points(accented)); Assert.Contains(0x00e7, Points(accented));
            Assert.DoesNotContain((int)'A', Points(accented));
            int[] combined = Points(Call(Preset("BasicLatin"), "Union", accented));
            Assert.Contains((int)'A', combined); Assert.Contains(0x00e3, combined);
        }
        /// <summary>Equivalent requests have stable order and callers cannot mutate the selection through source or returned arrays.</summary>
        [Fact]
        public void SelectionCanonicalizesAndOwnsItsStorage() {
            int[] input = { 0x00e3, 65, 65, 48 };
            object set = Create(input); input[0] = 90;
            Assert.Equal(new[] { 48, 65, 0x00e3 }, Points(set));
            int[] exported = Points(set); exported[0] = 90;
            Assert.Equal(48, Points(set)[0]);
            Assert.Equal(Points(Create(new[] { 65, 48, 0x00e3 })), Points(set));
        }
        /// <summary>Text-derived requests preserve supplementary scalars and combining marks without splitting surrogate pairs.</summary>
        [Fact]
        public void TextSelectionPreservesUnicodeScalars() {
            object set = SelectionType.GetMethod("FromText").Invoke(null, new object[] { "A\u00e3a\u0303\U0001f600\r\n\tA" });
            Assert.Equal(new[] { 65, 97, 0x00e3, 0x0303, 0x1f600 }, Points(set));
            System.Reflection.TargetInvocationException failure = Assert.Throws<System.Reflection.TargetInvocationException>(() => Call(set, "RequireBmpCharacters"));
            Assert.IsType<NotSupportedException>(failure.InnerException);
        }
        /// <summary>Invalid scalars are rejected instead of becoming truncated or substitute glyphs.</summary>
        /// <param name="scalar">An integer that cannot identify a Unicode scalar.</param>
        [Theory]
        [InlineData(-1)]
        [InlineData(0xd800)]
        [InlineData(0xdfff)]
        [InlineData(0x110000)]
        public void InvalidScalarsFailExplicitly(int scalar) {
            System.Reflection.TargetInvocationException failure = Assert.Throws<System.Reflection.TargetInvocationException>(() => Create(new[] { scalar }));
            Assert.IsType<ArgumentOutOfRangeException>(failure.InnerException);
        }
        /// <summary>Unpaired UTF-16 input fails instead of being converted into an apparently valid character request.</summary>
        [Fact]
        public void InvalidUtf16FailsExplicitly() {
            Type type = SelectionType;
            System.Reflection.TargetInvocationException failure = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                type.GetMethod("FromText").Invoke(null, new object[] { "\ud800A" }));
            Assert.IsType<ArgumentException>(failure.InnerException);
        }
        /// <summary>Serialization and clone preserve the explicit selection, and settings comparison includes it.</summary>
        [Fact]
        public void SettingsRoundTripAndCompareCharacterSelection() {
            FontAssetProcessorSettings settings = new() { PixelSize = 24, SnapHorizontalMetricsToPixels = true };
            Characters().SetValue(settings, Create(new[] { 65, 0x00e3 }));
            FontAssetPlatformSettingsSectionDefinition codec = new();
            using MemoryStream stream = new();
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian)) { codec.Serialize(writer, settings); }
            using MemoryStream input = new(stream.ToArray());
            using EngineBinaryReader reader = EngineBinaryReader.Create(input, EngineBinaryEndianness.LittleEndian);
            FontAssetProcessorSettings restored = (FontAssetProcessorSettings)codec.Deserialize(reader);
            Assert.Equal(input.Length, input.Position);
            Assert.Equal(new[] { 65, 0x00e3 }, Points(Characters().GetValue(restored)));
            FontAssetProcessorSettings clone = (FontAssetProcessorSettings)codec.CloneSettings(restored);
            Assert.True(codec.SettingsEqual(settings, clone));
            Characters().SetValue(clone, Preset("BasicLatin"));
            Assert.False(codec.SettingsEqual(settings, clone));
        }
        /// <summary>Legacy requests migrate their old implicit accent coverage rather than losing glyphs on recook.</summary>
        [Fact]
        public void LegacySettingsMakeTheirPreviousCoverageExplicit() {
            using MemoryStream input = new(BitConverter.GetBytes(24));
            using EngineBinaryReader reader = EngineBinaryReader.Create(input, EngineBinaryEndianness.LittleEndian);
            FontAssetProcessorSettings restored = (FontAssetProcessorSettings)new FontAssetPlatformSettingsSectionDefinition().Deserialize(reader);
            int[] values = Points(Characters().GetValue(restored));
            Assert.Contains((int)'A', values); Assert.Contains(0x00e3, values); Assert.Contains(0x00e7, values);
        }
        /// <summary>Empty requests are representable for collection but fail before generation, while valid BMP selections adapt without adding glyphs.</summary>
        [Fact]
        public void BmpAdapterPreservesExactCoverageAndRejectsEmptyRequests() {
            FontCharacterSet digits = FontCharacterSet.FromText("012210");
            Assert.Equal(3, digits.Count);
            Assert.Equal(new[] { '0', '1', '2' }, digits.RequireBmpCharacters());
            Assert.Equal(digits, FontCharacterSet.FromCodePoints(new[] { 50, 48, 49 }));
            Assert.Throws<InvalidOperationException>(() => FontCharacterSet.FromText("").RequireBmpCharacters());
            Assert.Throws<ArgumentNullException>(() => FontCharacterSet.FromText(null));
            Assert.Throws<ArgumentNullException>(() => FontCharacterSet.FromCodePoints(null));
            Assert.Throws<ArgumentNullException>(() => digits.Union(null));
            Assert.Throws<ArgumentNullException>(() => new FontAssetProcessorSettings().Characters = null);
        }

        /// <summary>Historical horizontal-policy payloads retain both their metric choice and the old implicit accented coverage.</summary>
        [Fact]
        public void VersionOneSettingsRetainTheirLegacyCoverage() {
            using MemoryStream stream = new();
            using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true)) { writer.Write(-1); writer.Write((byte)1); writer.Write(24); writer.Write((byte)1); }
            stream.Position = 0;
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            FontAssetProcessorSettings restored = (FontAssetProcessorSettings)new FontAssetPlatformSettingsSectionDefinition().Deserialize(reader);
            Assert.True(restored.SnapHorizontalMetricsToPixels);
            Assert.Contains('\u00e3', restored.Characters.RequireBmpCharacters());
            Assert.Contains('\u00e4', restored.Characters.RequireBmpCharacters());
            Assert.Equal(stream.Length, stream.Position);
        }

        /// <summary>Malformed counts and noncanonical scalar lists cannot consume a neighboring settings section as valid font data.</summary>
        /// <param name="count">Declared scalar count.</param>
        /// <param name="first">First encoded value.</param>
        /// <param name="second">Second encoded value.</param>
        [Theory]
        [InlineData(-1, 65, 66)]
        [InlineData(0x110000, 65, 66)]
        [InlineData(2, 65, 65)]
        [InlineData(2, 66, 65)]
        [InlineData(2, 65, 0xd800)]
        [InlineData(2, -1, 66)]
        [InlineData(2, 65, 0x110000)]
        public void InvalidPersistedCharacterListsFailExplicitly(int count, int first, int second) {
            using MemoryStream stream = new();
            using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true)) {
                writer.Write(-1); writer.Write((byte)2); writer.Write(24); writer.Write((byte)0);
                writer.Write(count); writer.Write(first); writer.Write(second);
            }
            stream.Position = 0;
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            Assert.Throws<InvalidOperationException>(() => new FontAssetPlatformSettingsSectionDefinition().Deserialize(reader));
        }
        /// <summary>Finds the shared request property while making its absence a useful test failure.</summary>
        static System.Reflection.PropertyInfo Characters() {
            System.Reflection.PropertyInfo property = typeof(FontAssetProcessorSettings).GetProperty("Characters");
            Assert.NotNull(property); return property;
        }
        /// <summary>Reads one documented immutable preset.</summary>
        static object Preset(string name) => SelectionType.GetProperty(name).GetValue(null);
        /// <summary>Creates a selection through its public input-validation path.</summary>
        static object Create(int[] values) => SelectionType.GetMethod("FromCodePoints").Invoke(null, new object[] { values });
        /// <summary>Copies the selected scalar values through the public read API.</summary>
        static int[] Points(object set) => (int[])Call(set, "ToArray");
        /// <summary>Calls selection behavior without compiling against a feature that is intentionally absent during the red run.</summary>
        static object Call(object target, string name, params object[] arguments) => target.GetType().GetMethod(name).Invoke(target, arguments);
    }
}
