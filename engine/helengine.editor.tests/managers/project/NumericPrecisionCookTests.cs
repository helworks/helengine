using helengine.editor;

namespace helengine.editor.tests {
    /// <summary>Checks explicit authored numeric metadata independently of selected native cook precision.</summary>
    public sealed class NumericPrecisionCookTests {
        /// <summary>The editor's complete managed graph preserves authoring values in 64-bit storage and arithmetic.</summary>
        [Fact]
        public void AuthoringHostUses64BitStorage() {
            Assert.Equal(64, RealRepresentation.StorageBits);
            Assert.Equal(64, RealRepresentation.ArithmeticBits);
        }

        /// <summary>New named-field containers declare their 64-bit numeric encoding before field payloads.</summary>
        [Fact]
        public void AuthoredRealFieldRetainsDoublePrecision() {
            EditorTaggedSceneComponentFieldWriter writer = new();
            writer.WriteField("Value", field => field.WriteReal(Real.FromDouble(16777217d)));
            byte[] payload = writer.BuildPayload();
            Assert.Equal(new byte[] { 2, 1, 64, 64 }, payload.Take(4).ToArray());
            EditorTaggedSceneComponentFieldReader reader = new(payload);
            Assert.True(reader.TryGetFieldReader("Value", out EngineBinaryReader field));
            using (field) {
                Assert.Equal("real-v1-s64-a64", field.NumericPolicy.Identity);
                Assert.Equal(16777217d, field.ReadReal().ToDouble());
            }
        }

        /// <summary>Old named-field payloads retain their original explicit IEEE32 interpretation in the wide editor.</summary>
        [Fact]
        public void OldAuthoringWidthIsReadExplicitly() {
            using MemoryStream stream = new();
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian)) {
                writer.WriteByte(1);
                writer.WriteInt32(1);
                writer.WriteString("Value");
                writer.WriteByteArray(BitConverter.GetBytes(1.25f));
            }
            byte[] source = stream.ToArray();
            EditorTaggedSceneComponentFieldReader reader = new(source);
            Assert.True(reader.TryGetFieldReader("Value", out EngineBinaryReader field));
            using (field) {
                Assert.Equal("real-v1-s32-a32", field.NumericPolicy.Identity);
                Assert.Equal(1.25d, field.ReadReal().ToDouble());
            }
            Assert.Equal(source, stream.ToArray());
        }

        /// <summary>Reflected members use the container width instead of silently narrowing or walking Real internals.</summary>
        [Theory]
        [InlineData(32)]
        [InlineData(64)]
        public void ReflectedNumericMembersUseDeclaredWidth(int bits) {
            NumericPrecisionPolicy policy = new(bits, bits);
            object[] values = { Real.FromDouble(16777217d), new float2(16777217d, 1.25d), new float3(16777217d, 1.25d, -2.5d), new float4(16777217d, 1.25d, -2.5d, 4d) };
            int[] scalarCounts = { 1, 2, 3, 4 };
            for (int i = 0; i < values.Length; i++) {
                using MemoryStream stream = new();
                using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian)) {
                    writer.NumericPolicy = policy;
                    AutomaticScriptComponentPersistenceDescriptor.WriteSupportedValue(writer, values[i].GetType(), values[i]);
                }
                Assert.Equal(scalarCounts[i] * bits / 8, stream.Length);
                stream.Position = 0;
                using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
                reader.NumericPolicy = policy;
                object restored = AutomaticScriptComponentPersistenceDescriptor.ReadSupportedValue(reader, values[i].GetType());
                double first = restored switch { Real scalar => scalar.ToDouble(), float2 v => v.X.ToDouble(), float3 v => v.X.ToDouble(), float4 v => v.X.ToDouble(), _ => throw new InvalidOperationException() };
                Assert.Equal(bits == 32 ? 16777216d : 16777217d, first);
                Assert.Equal(stream.Length, stream.Position);
            }
        }

        /// <summary>The same authoring object cooks to either declared storage width without changing its source.</summary>
        [Theory]
        [InlineData(32)]
        [InlineData(64)]
        public void CookedModelRecordsPolicyAndRetainsSelectedPrecision(int bits) {
            ModelAsset source = new() { Positions = new[] { new float3(16777217d, 1.25d, -2.5d) } };
            NumericPrecisionPolicy policy = new(bits, bits);
            using MemoryStream stream = new();
            helengine.files.EditorAssetBinarySerializer.Serialize(stream, source, policy);
            byte[] payload = stream.ToArray();
            Assert.Equal(new byte[] { 1, (byte)bits, (byte)bits }, payload.Skip(12).Take(3).ToArray());
            stream.Position = 0;
            ModelAsset authored = (ModelAsset)helengine.files.EditorAssetBinarySerializer.Deserialize(stream);
            Assert.Equal(bits == 32 ? 16777216d : 16777217d, authored.Positions[0].X.ToDouble());
            stream.Position = 0;
            ModelAsset cooked = (ModelAsset)PackagedAssetBinarySerializer.Deserialize(stream, policy);
            Assert.Equal(authored.Positions[0], cooked.Positions[0]);
            Assert.Equal(16777217d, source.Positions[0].X.ToDouble());
            stream.Position = 0;
            NumericPrecisionPolicy other = new(bits == 32 ? 64 : 32, bits == 32 ? 64 : 32);
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => PackagedAssetBinarySerializer.Deserialize(stream, other));
            Assert.Contains("Numeric", error.Message);
            Assert.Equal(15, stream.Position);
        }

        /// <summary>Old asset bodies are read only under their declared legacy version and original storage width.</summary>
        [Fact]
        public void LegacyModelLoadsInWideEditorButIsRejectedByRuntime() {
            using MemoryStream stream = new();
            EngineBinaryHeaderSerializer.Write(stream, new EngineBinaryHeader(EngineBinaryEndianness.LittleEndian, 25, 1, (ushort)EditorBinaryRecordKind.Asset, (ushort)EditorAssetBinaryValueKind.ModelAsset));
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian)) {
                writer.WriteString("legacy-model");
                writer.WriteInt64(123);
                writer.WriteString("legacy-source");
                writer.WriteInt32(0);
                writer.WriteInt32(1);
                writer.WriteFloat3(new float3(1.25, 2.5, 3.75));
                for (int i = 0; i < 5; i++) { writer.WriteInt32(-1); }
            }
            stream.Position = 0;
            ModelAsset result = (ModelAsset)helengine.files.EditorAssetBinarySerializer.Deserialize(stream);
            Assert.Equal(new float3(1.25, 2.5, 3.75), result.Positions[0]);
            stream.Position = 0;
            Assert.Throws<InvalidOperationException>(() => PackagedAssetBinarySerializer.Deserialize(stream));
        }

        /// <summary>Scene transforms and animation tracks follow the same selected width as model geometry.</summary>
        [Theory]
        [InlineData(32)]
        [InlineData(64)]
        public void SceneAndAnimationValuesUseAssetPolicy(int bits) {
            NumericPrecisionPolicy policy = new(bits, bits);
            SceneAsset scene = new() { RootEntities = new[] { new SceneEntityAsset { Id = 1, LocalPosition = new float3(16777217d, 2d, 3d) } } };
            AnimationClipAsset clip = new() { Duration = 16777217d, PositionTracks = new[] { new PositionKeyframeTrackAsset { Keyframes = new[] { new PositionKeyframeAsset { Time = 16777217d, Value = new float3(16777217d, 2d, 3d) } } } } };
            foreach (Asset source in new Asset[] { scene, clip }) {
                using MemoryStream stream = new();
                helengine.files.EditorAssetBinarySerializer.Serialize(stream, source, policy);
                stream.Position = 0;
                Asset restored = PackagedAssetBinarySerializer.Deserialize(stream, policy);
                double expected = bits == 32 ? 16777216d : 16777217d;
                if (restored is SceneAsset loadedScene) { Assert.Equal(expected, loadedScene.RootEntities[0].LocalPosition.X.ToDouble()); }
                else {
                    AnimationClipAsset loadedClip = (AnimationClipAsset)restored;
                    Assert.Equal(expected, loadedClip.Duration.ToDouble());
                    Assert.Equal(expected, loadedClip.PositionTracks[0].Keyframes[0].Time.ToDouble());
                    Assert.Equal(expected, loadedClip.PositionTracks[0].Keyframes[0].Value.X.ToDouble());
                }
            }
        }

        /// <summary>Cook math rounds inputs and primitive operations under the target contract instead of the authoring host.</summary>
        [Theory]
        [InlineData(32)]
        [InlineData(64)]
        public void LayoutCookUsesTargetArithmeticAndStorage(int bits) {
            NumericPrecisionPolicy policy = new(bits, bits);
            ViewportLayoutMetrics metrics = ViewportLayoutMath.Resolve(ViewportComponent.DensityScalingMode, new int2(1, 1), new int2(320, 240), 1d, 1d, 16777217d, 16777216d, policy);
            Assert.Equal(bits == 32 ? 1d : 16777217d / 16777216d, metrics.ScaleX.ToDouble());
            ViewportLayoutMetrics scale = new() { ScaleX = 3d, ScaleY = 1d, GlyphScale = 1d };
            float3 position = ViewportLayoutApplier.ResolvePosition(new float3(16777217d, 2d, 3d), scale, policy);
            Assert.Equal(bits == 32 ? 50331648d : 50331651d, position.X.ToDouble());
            Assert.Equal(bits == 32 ? 16777216 : 16777217, ViewportLayoutApplier.ScaleInteger(16777217, 1d, policy));
        }

        /// <summary>Workspace JSON uses canonical numeric transport without narrowing authoring poses to single precision.</summary>
        [Fact]
        public void WorkspaceNumericTransportRetainsAuthoringPrecision() {
            ViewportWorkspacePanelStateDocument source = new() { CameraPositionX = 16777217d, OrbitPivotX = 16777217d, FarPlaneDistance = 16777217d };
            string json = System.Text.Json.JsonSerializer.Serialize(source);
            ViewportWorkspacePanelStateDocument restored = System.Text.Json.JsonSerializer.Deserialize<ViewportWorkspacePanelStateDocument>(json)!;
            Assert.Equal(16777217d, restored.CameraPositionX);
            Assert.Equal(16777217d, restored.OrbitPivotX);
            Assert.Equal(16777217d, restored.FarPlaneDistance);
        }

        /// <summary>Cooked world-transform intermediates use the target storage boundaries and primitive arithmetic.</summary>
        [Theory]
        [InlineData(32)]
        [InlineData(64)]
        public void CookWorldTransformsUseTargetNumericContract(int bits) {
            CookNumericOperations math = new(new NumericPrecisionPolicy(bits, bits));
            float3 scaled = TransformNumericKernel<double, CookNumericOperations>.Multiply(new float3(16777217d, 2d, 3d), new float3(3d, 4d, 5d), math);
            Assert.Equal(bits == 32 ? 50331648d : 50331651d, scaled.X.ToDouble());
            float3 rotated = TransformNumericKernel<double, CookNumericOperations>.Rotate(new float3(16777217d, 2d, 3d), float4.Identity, math);
            Assert.Equal(bits == 32 ? 16777216d : 16777217d, rotated.X.ToDouble());
            float4 normalized = TransformNumericKernel<double, CookNumericOperations>.Normalize(new float4(1d, 2d, 3d, 4d), math);
            double inverse = bits == 32 ? (double)(1f / MathF.Sqrt(30f)) : 1d / Math.Sqrt(30d);
            Assert.Equal(bits == 32 ? (double)(float)(4f * (float)inverse) : 4d * inverse, normalized.W.ToDouble());
        }

        /// <summary>Actual asset body codecs honor both declared byte orders and storage widths.</summary>
        [Theory]
        [InlineData(32, EngineBinaryEndianness.LittleEndian)]
        [InlineData(32, EngineBinaryEndianness.BigEndian)]
        [InlineData(64, EngineBinaryEndianness.LittleEndian)]
        [InlineData(64, EngineBinaryEndianness.BigEndian)]
        public void AssetBodiesRoundTripBothEndiannessesAndWidths(int bits, EngineBinaryEndianness endian) {
            NumericPrecisionPolicy policy = new(bits, bits);
            Asset[] assets = {
                new ModelAsset { Positions = [new float3(16777217d, 2d, 3d)] },
                new SceneAsset { RootEntities = [new SceneEntityAsset { Id = 1, LocalPosition = new float3(16777217d, 2d, 3d) }] },
                new AnimationClipAsset { Duration = 16777217d, PositionTracks = [new PositionKeyframeTrackAsset { Keyframes = [new PositionKeyframeAsset { Time = 16777217d, Value = new float3(16777217d, 2d, 3d) }] }] }
            };
            helengine.files.IEditorAssetPayloadSerializer[] serializers = { new helengine.files.ModelAssetPayloadSerializer(), new helengine.files.SceneAssetPayloadSerializer(), new helengine.files.AnimationClipAssetPayloadSerializer() };
            for (int index = 0; index < assets.Length; index++) {
                using MemoryStream stream = new();
                EngineBinaryHeaderSerializer.Write(stream, new EngineBinaryHeader(endian, PackagedAssetBinarySerializer.CurrentVersion, PackagedAssetBinarySerializer.FormatId, (ushort)PackagedAssetBinarySerializer.RecordKind, (ushort)serializers[index].ValueKind));
                using (helengine.files.EngineBinaryWriter writer = helengine.files.EngineBinaryWriter.Create(stream, endian)) {
                    writer.NumericPolicy = policy;
                    writer.WriteNumericPolicy();
                    serializers[index].Write(writer, assets[index]);
                }
                stream.Position = 0;
                Asset restored = PackagedAssetBinarySerializer.Deserialize(stream, policy);
                double first = restored switch { ModelAsset model => model.Positions[0].X.ToDouble(), SceneAsset scene => scene.RootEntities[0].LocalPosition.X.ToDouble(), AnimationClipAsset clip => clip.PositionTracks[0].Keyframes[0].Value.X.ToDouble(), _ => throw new InvalidOperationException() };
                Assert.Equal(bits == 32 ? 16777216d : 16777217d, first);
                Assert.Equal(stream.Length, stream.Position);
            }
        }

        /// <summary>A numeric representation version mismatch is reported before any numeric field can be read.</summary>
        [Fact]
        public void RejectsUnknownNumericVersion() {
            using MemoryStream stream = new();
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian)) {
                writer.WriteByte(2);
                writer.WriteByte(99);
                writer.WriteByte(64);
                writer.WriteByte(64);
                writer.WriteInt32(0);
            }
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new EditorTaggedSceneComponentFieldReader(stream.ToArray()));
            Assert.Contains("Numeric", error.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
