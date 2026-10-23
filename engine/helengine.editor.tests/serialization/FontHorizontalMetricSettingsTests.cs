namespace helengine.editor.tests.serialization {
    /// <summary>Verifies the opt-in font section extends old positive-size payloads while preserving legacy coverage and neighboring section framing.</summary>
    public sealed class FontHorizontalMetricSettingsTests {
        /// <summary>Legacy four-byte sections migrate to explicit character coverage without enabling the metric policy.</summary>
        [Fact]
        public void LegacyFontSectionMigratesItsImplicitCoverage() {
            byte[] legacy = BitConverter.GetBytes(34);
            FontAssetProcessorSettings settings = ReadFontSection(legacy);
            Assert.Equal(34, settings.PixelSize);
            Assert.False(settings.SnapHorizontalMetricsToPixels);
            Assert.Contains('ã', settings.Characters.RequireBmpCharacters());
            Assert.Equal(settings.Characters, ReadFontSection(WriteFontSection(settings)).Characters);
        }

        /// <summary>Enabled settings round-trip through the explicit extension and participate in real registry clone/equality semantics.</summary>
        [Fact]
        public void OptInSectionClonesAndComparesItsPolicy() {
            FontAssetProcessorSettings original = new FontAssetProcessorSettings { PixelSize = 34, SnapHorizontalMetricsToPixels = true };
            byte[] bytes = WriteFontSection(original);
            Assert.Equal(14 + 4 * FontCharacterSet.BasicLatin.Count, bytes.Length);
            Assert.Equal(2, bytes[4]);
            Assert.Equal(-1, BitConverter.ToInt32(bytes, 0));
            FontAssetProcessorSettings restored = ReadFontSection(bytes);
            Assert.Equal(34, restored.PixelSize);
            Assert.True(restored.SnapHorizontalMetricsToPixels);
            FontAssetPlatformSettingsSectionDefinition definition = new FontAssetPlatformSettingsSectionDefinition();
            FontAssetProcessorSettings clone = (FontAssetProcessorSettings)definition.CloneSettings(restored);
            Assert.NotSame(restored, clone);
            Assert.True(clone.SnapHorizontalMetricsToPixels);
            Assert.True(definition.SettingsEqual(original, clone));
            clone.SnapHorizontalMetricsToPixels = false;
            Assert.False(definition.SettingsEqual(original, clone));
        }

        /// <summary>A mixed complete sidecar preserves its texture, platform and environment sections under the one current header, and restores its exact disabled-policy bytes when the policy is turned off again.</summary>
        [Fact]
        public void MultipleSectionsProfilesAndEnvironmentsKeepTheirFraming() {
            AssetImportSettings settings = new AssetImportSettings();
            settings.Importer.ImporterId = "gdi-font";
            AssetPlatformProcessorSettings ps2 = new AssetPlatformProcessorSettings();
            ps2.Font.PixelSize = 34;
            ps2.FontAtlasTexture.MaxResolution = 512;
            ps2.FontAtlasTexture.ColorFormat = TextureAssetColorFormat.Indexed8;
            ps2.Environments.Add("preview", new AssetPlatformProcessorSettings { Font = new FontAssetProcessorSettings { PixelSize = 20 } });
            settings.Processor.Platforms.Add("ps2", ps2);
            settings.Processor.Platforms.Add("n64", new AssetPlatformProcessorSettings { Font = new FontAssetProcessorSettings { PixelSize = 13 } });
            byte[] disabled = WriteSidecar(settings);
            using (MemoryStream headerStream = new MemoryStream(disabled)) { Assert.Equal(SectionedAssetImportSettingsBinarySerializer.CurrentVersion, EngineBinaryHeaderSerializer.Read(headerStream).Version); }
            using (MemoryStream stream = new MemoryStream(disabled)) {
                AssetImportSettings disabledSettings = SectionedAssetImportSettingsBinarySerializer.Deserialize(stream);
                Assert.False(disabledSettings.Processor.Platforms["ps2"].Font.SnapHorizontalMetricsToPixels);
                Assert.Equal(stream.Length, stream.Position);
            }
            ps2.Font.SnapHorizontalMetricsToPixels = true;
            byte[] extended = WriteSidecar(settings);
            using (MemoryStream headerStream = new MemoryStream(extended)) { Assert.Equal(SectionedAssetImportSettingsBinarySerializer.CurrentVersion, EngineBinaryHeaderSerializer.Read(headerStream).Version); }
            Assert.Equal(disabled.Length, extended.Length);
            Assert.NotEqual(disabled, extended);
            using MemoryStream updatedStream = new MemoryStream(extended);
            AssetImportSettings restored = SectionedAssetImportSettingsBinarySerializer.Deserialize(updatedStream);
            Assert.Equal(updatedStream.Length, updatedStream.Position);
            Assert.True(restored.Processor.Platforms["ps2"].Font.SnapHorizontalMetricsToPixels);
            Assert.False(restored.Processor.Platforms["n64"].Font.SnapHorizontalMetricsToPixels);
            Assert.False(restored.Processor.Platforms["ps2"].Environments["preview"].Font.SnapHorizontalMetricsToPixels);
            Assert.Equal(512, restored.Processor.Platforms["ps2"].FontAtlasTexture.MaxResolution);
            Assert.Equal(TextureAssetColorFormat.Indexed8, restored.Processor.Platforms["ps2"].FontAtlasTexture.ColorFormat);
            restored.Processor.Platforms["ps2"].Font.SnapHorizontalMetricsToPixels = false;
            Assert.Equal(disabled, WriteSidecar(restored));
        }

        /// <summary>A sidecar written under the previous header version is rejected outright, so its sections are never framed by the current reader.</summary>
        [Fact]
        public void PreviousHeaderVersionIsRejected() {
            AssetImportSettings settings = new AssetImportSettings();
            settings.Processor.Platforms.Add("ps2", new AssetPlatformProcessorSettings {
                Font = new FontAssetProcessorSettings { PixelSize = 34, SnapHorizontalMetricsToPixels = true }
            });
            byte[] bytes = WriteSidecar(settings);
            using MemoryStream stream = new MemoryStream(bytes, true);
            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            stream.Position = 0;
            EngineBinaryHeaderSerializer.Write(stream, new EngineBinaryHeader(header.Endianness, (byte)(SectionedAssetImportSettingsBinarySerializer.CurrentVersion - 1), header.FormatId, header.RecordKind, header.ValueKind));
            stream.Position = 0;
            Assert.Throws<InvalidOperationException>(() => SectionedAssetImportSettingsBinarySerializer.Deserialize(stream));
        }

        /// <summary>Invalid extension versions, nonboolean flags and invalid sizes fail explicitly instead of shifting following section bytes.</summary>
        /// <param name="version">Extension format version.</param>
        /// <param name="pixels">Declared positive pixel size.</param>
        /// <param name="flag">Declared boolean policy byte.</param>
        [Theory]
        [InlineData(3, 34, 1)]
        [InlineData(1, 0, 1)]
        [InlineData(1, 34, 2)]
        public void MalformedExtensionsFailExplicitly(byte version, int pixels, byte flag) {
            using MemoryStream stream = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) {
                writer.Write(-1); writer.Write(version); writer.Write(pixels); writer.Write(flag);
            }
            Assert.Throws<InvalidOperationException>(() => ReadFontSection(stream.ToArray()));
        }

        /// <summary>A truncated opt-in payload cannot masquerade as a legacy font section.</summary>
        [Fact]
        public void TruncatedExtensionFails() {
            byte[] bytes = WriteFontSection(new FontAssetProcessorSettings { PixelSize = 34, SnapHorizontalMetricsToPixels = true });
            Assert.Throws<EndOfStreamException>(() => ReadFontSection(bytes[..^1]));
        }

        /// <summary>Reads the actual font-section codec and requires that it consumed the entire isolated payload.</summary>
        /// <param name="bytes">Section payload bytes.</param>
        /// <returns>The decoded font settings.</returns>
        static FontAssetProcessorSettings ReadFontSection(byte[] bytes) {
            using MemoryStream stream = new MemoryStream(bytes);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            FontAssetProcessorSettings result = (FontAssetProcessorSettings)new FontAssetPlatformSettingsSectionDefinition().Deserialize(reader);
            Assert.Equal(stream.Length, stream.Position);
            return result;
        }
        /// <summary>Writes the actual font-section codec, using its versioned explicit character request.</summary>
        /// <param name="settings">Explicit font settings.</param>
        /// <returns>The production section bytes.</returns>
        static byte[] WriteFontSection(FontAssetProcessorSettings settings) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            new FontAssetPlatformSettingsSectionDefinition().Serialize(writer, settings);
            return stream.ToArray();
        }
        /// <summary>Serializes a complete sidecar with the production registered section codec.</summary>
        /// <param name="settings">Complete import settings.</param>
        /// <returns>The official sectioned sidecar bytes.</returns>
        static byte[] WriteSidecar(AssetImportSettings settings) {
            using MemoryStream stream = new MemoryStream();
            SectionedAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            return stream.ToArray();
        }
    }
}
