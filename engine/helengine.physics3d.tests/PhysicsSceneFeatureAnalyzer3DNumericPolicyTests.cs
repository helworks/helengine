namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies serialized physics analysis follows its caller's exact cook policy without weakening payload validation.
    /// </summary>
    public sealed class PhysicsSceneFeatureAnalyzer3DNumericPolicyTests {
        /// <summary>
        /// Ensures all five payload readers and recursive entity analysis accept the selected width and preserve feature flags.
        /// </summary>
        [Fact]
        public void Analyze_WithSelectedPolicy_PreservesFeaturesAcross32And64() {
            NumericPrecisionPolicy policy32 = new NumericPrecisionPolicy(32, 32);
            NumericPrecisionPolicy policy64 = new NumericPrecisionPolicy(64, 64);
            SceneAsset scene32 = CreateScene(policy32);
            SceneAsset scene64 = CreateScene(policy64);
            PhysicsSceneFeatureFlags3D flags32 = PhysicsSceneFeatureAnalyzer3D.Analyze(scene32, policy32);
            PhysicsSceneFeatureFlags3D flags64 = PhysicsSceneFeatureAnalyzer3D.Analyze(scene64, policy64);
            Assert.NotEqual((PhysicsSceneFeatureFlags3D)0, flags32);
            Assert.Equal(flags64, flags32);
            Assert.Equal(flags32, PhysicsSceneFeatureAnalyzer3D.Analyze(scene32.RootEntities, policy32));
            Assert.Equal(flags64, PhysicsSceneFeatureAnalyzer3D.Analyze(scene64.RootEntities, policy64));
        }

        /// <summary>
        /// Requires each payload reader to reject an explicitly mismatched expected policy in both width directions.
        /// </summary>
        [Fact]
        public void Analyze_WithWrongExpectedPolicy_RejectsEveryPhysicsPayload() {
            for (int kind = 0; kind < 5; kind++) {
                Assert.Throws<InvalidOperationException>(() => PhysicsSceneFeatureAnalyzer3D.Analyze(
                    CreateSingleRecordScene(CreateRecord(kind, new NumericPrecisionPolicy(32, 32))), new NumericPrecisionPolicy(64, 64)));
                Assert.Throws<InvalidOperationException>(() => PhysicsSceneFeatureAnalyzer3D.Analyze(
                    CreateSingleRecordScene(CreateRecord(kind, new NumericPrecisionPolicy(64, 64))), new NumericPrecisionPolicy(32, 32)));
            }
        }

        /// <summary>
        /// Ensures the default overload retains the native policy and nested records cannot introduce another policy.
        /// </summary>
        [Fact]
        public void Analyze_DefaultAndNestedPayloads_RemainStrict() {
            Assert.Equal(PhysicsSceneFeatureAnalyzer3D.Analyze(CreateScene(RealRepresentation.Policy), RealRepresentation.Policy),
                PhysicsSceneFeatureAnalyzer3D.Analyze(CreateScene(RealRepresentation.Policy)));
            NumericPrecisionPolicy otherPolicy = RealRepresentation.Policy.StorageBits == 64
                ? new NumericPrecisionPolicy(32, 32) : new NumericPrecisionPolicy(64, 64);
            Assert.Throws<InvalidOperationException>(() => PhysicsSceneFeatureAnalyzer3D.Analyze(CreateScene(otherPolicy)));
            Assert.Throws<InvalidOperationException>(() => PhysicsSceneFeatureAnalyzer3D.Analyze(CreateScene(otherPolicy).RootEntities));
            SceneAsset mixed = CreateScene(new NumericPrecisionPolicy(32, 32));
            mixed.RootEntities[0].Children[0].Components[0] = CreateRecord(0, new NumericPrecisionPolicy(64, 64));
            Assert.Throws<InvalidOperationException>(() => PhysicsSceneFeatureAnalyzer3D.Analyze(mixed, new NumericPrecisionPolicy(32, 32)));
        }

        /// <summary>
        /// Keeps version, member-count, truncation, trailing-data and null-policy validation strict for the selected width.
        /// </summary>
        [Fact]
        public void Analyze_WithSelectedPolicy_RejectsMalformedPayloads() {
            NumericPrecisionPolicy policy = new NumericPrecisionPolicy(32, 32);
            for (int kind = 0; kind < 5; kind++) {
                SceneComponentAssetRecord version = CreateRecord(kind, policy);
                version.Payload[0] = 0;
                Assert.Throws<InvalidOperationException>(() => PhysicsSceneFeatureAnalyzer3D.Analyze(CreateSingleRecordScene(version), policy));
                SceneComponentAssetRecord count = CreateRecord(kind, policy);
                count.Payload[4] = 99;
                Assert.Throws<InvalidOperationException>(() => PhysicsSceneFeatureAnalyzer3D.Analyze(CreateSingleRecordScene(count), policy));
                SceneComponentAssetRecord truncated = CreateRecord(kind, policy);
                truncated.Payload = truncated.Payload.Take(truncated.Payload.Length - 1).ToArray();
                Assert.Throws<InvalidOperationException>(() => PhysicsSceneFeatureAnalyzer3D.Analyze(CreateSingleRecordScene(truncated), policy));
                SceneComponentAssetRecord trailing = CreateRecord(kind, policy);
                trailing.Payload = trailing.Payload.Concat(new byte[] { 42 }).ToArray();
                Assert.Throws<InvalidOperationException>(() => PhysicsSceneFeatureAnalyzer3D.Analyze(CreateSingleRecordScene(trailing), policy));
            }
            Assert.Throws<ArgumentNullException>(() => PhysicsSceneFeatureAnalyzer3D.Analyze(CreateScene(policy), null));
        }

        /// <summary>
        /// Creates a recursive hierarchy containing every supported serialized physics reader.
        /// </summary>
        /// <param name="policy">Numeric policy used by the official payload writer.</param>
        /// <returns>Scene with a rigid body and four collider kinds across parent and child entities.</returns>
        static SceneAsset CreateScene(NumericPrecisionPolicy policy) {
            SceneAsset scene = CreateSingleRecordScene(CreateRecord(0, policy));
            SceneEntityAsset child = new SceneEntityAsset {
                Components = new[] { CreateRecord(0, policy), CreateRecord(1, policy), CreateRecord(2, policy), CreateRecord(3, policy), CreateRecord(4, policy) },
                Children = Array.Empty<SceneEntityAsset>()
            };
            scene.RootEntities[0].Components = new[] { CreateRecord(0, policy), CreateRecord(1, policy) };
            scene.RootEntities[0].Children = new[] { child };
            return scene;
        }

        /// <summary>
        /// Wraps a serialized record in one otherwise empty scene hierarchy.
        /// </summary>
        /// <param name="record">Component payload to analyze.</param>
        /// <returns>Scene containing exactly the supplied component.</returns>
        static SceneAsset CreateSingleRecordScene(SceneComponentAssetRecord record) {
            return new SceneAsset {
                RootEntities = new[] { new SceneEntityAsset {
                    Components = new[] { record }, Children = Array.Empty<SceneEntityAsset>()
                } }
            };
        }

        /// <summary>
        /// Writes current reflected component schemas with the official engine writer and an explicit numeric policy.
        /// </summary>
        /// <param name="kind">Rigid body, box, sphere, capsule or static mesh selector.</param>
        /// <param name="policy">Required payload policy.</param>
        /// <returns>Current serialized component record.</returns>
        static SceneComponentAssetRecord CreateRecord(int kind, NumericPrecisionPolicy policy) {
            string[] types = { "RigidBody3DComponent", "BoxCollider3DComponent", "SphereCollider3DComponent", "CapsuleCollider3DComponent", "StaticMeshCollider3DComponent" };
            int[] counts = { 8, 7, 7, 8, 8 };
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.NumericPolicy = policy;
            writer.WriteByte(AutomaticScriptComponentPayloadFormat.CurrentVersion);
            RealBinaryCodec.WritePolicy(writer, policy);
            writer.WriteInt32(counts[kind]);
            if (kind == 0) {
                writer.WriteReal3(float3.Zero);
                writer.WriteInt32((int)BodyKind3D.Dynamic);
                writer.WriteDouble(1d);
                writer.WriteReal3(float3.Zero);
                writer.WriteDouble(1d);
                writer.WriteByte(1);
                writer.WriteDouble(0.5d);
                writer.WriteInt32(10);
            } else {
                if (kind == 4) {
                    writer.WriteByte(0);
                }
                writer.WriteUInt16(1);
                writer.WriteUInt16(ushort.MaxValue);
                if (kind == 4) {
                    writer.WriteByte(0);
                }
                writer.WriteDouble(0.4d);
                if (kind == 3) {
                    writer.WriteSingle(2f);
                }
                writer.WriteByte(0);
                if (kind == 2 || kind == 3) {
                    writer.WriteSingle(0.5f);
                }
                writer.WriteDouble(0d);
                if (kind == 1) {
                    writer.WriteReal3(float3.One);
                }
                writer.WriteDouble(0.6d);
            }
            return new SceneComponentAssetRecord { ComponentTypeId = "helengine." + types[kind], Payload = stream.ToArray() };
        }
    }
}
