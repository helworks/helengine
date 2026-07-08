using helengine.baseplatform.Definitions;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Verifies ordinal runtime deserializer source generation for reflected scripted component schemas.
    /// </summary>
    public sealed class ScriptComponentPlayerDeserializerGeneratorTests {
        /// <summary>
        /// Ensures supported reflected member schemas emit ordinal reader source without editor tagged-field lookup.
        /// </summary>
        [Fact]
        public void Generate_WhenSchemaContainsSupportedMembers_EmitsOrdinalDeserializerSource() {
            ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(typeof(TestScriptSerializableComponent));
            ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator();

            string source = generator.Generate(schema);

            Assert.Contains("reader.ReadString()", source, StringComparison.Ordinal);
            Assert.Contains("reader.ReadByte() != 0", source, StringComparison.Ordinal);
            Assert.Contains("reader.ReadInt32()", source, StringComparison.Ordinal);
            Assert.DoesNotContain("TryGetFieldReader", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures eligible engine-owned automatic components can emit native runtime deserializer classes for player builds.
        /// </summary>
        [Fact]
        public void GenerateNativeDeserializerSource_WhenSchemaContainsClipRectComponent_EmitsNativeDeserializerClass() {
            ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(typeof(ClipRectComponent));
            ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator();

            string header = generator.GenerateNativeDeserializerHeader(schema);
            string source = generator.GenerateNativeDeserializerSource(schema);

            Assert.Contains("class GeneratedRuntimeClipRectComponentDeserializer", header, StringComparison.Ordinal);
            Assert.Contains("helengine.ClipRectComponent", source, StringComparison.Ordinal);
            Assert.Contains("component->set_Size(reader->ReadInt2());", source, StringComparison.Ordinal);
            Assert.Contains("int32_t GeneratedRuntimeClipRectComponentDeserializer::MemberCount = 1;", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures generated native deserializers route numeric exception message formatting through the shared native string helper instead of pulling <c>std::to_string</c> into every packaged component deserializer.
        /// </summary>
        [Fact]
        public void GenerateNativeDeserializerSource_WhenSchemaContainsClipRectComponent_UsesNativeStringJoinFormattingForVersionAndMemberCounts() {
            ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(typeof(ClipRectComponent));
            ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator();

            string source = generator.GenerateNativeDeserializerSource(schema);

            Assert.Contains("String::ToJoinString(version)", source, StringComparison.Ordinal);
            Assert.Contains("String::ToJoinString(MemberCount)", source, StringComparison.Ordinal);
            Assert.Contains("String::ToJoinString(memberCount)", source, StringComparison.Ordinal);
            Assert.DoesNotContain("std::to_string(version)", source, StringComparison.Ordinal);
            Assert.DoesNotContain("std::to_string(MemberCount)", source, StringComparison.Ordinal);
            Assert.DoesNotContain("std::to_string(memberCount)", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures compact native exception message mode strips the hand-authored native deserializer version and member-count string formatting instead of bypassing the shared compact exception contract.
        /// </summary>
        [Fact]
        public void GenerateNativeDeserializerSource_WhenCompactNativeExceptionMessagesEnabled_OmitsVersionAndMemberCountFormatting() {
            ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(typeof(ClipRectComponent));
            ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator(useCompactNativeExceptionMessages: true);

            string source = generator.GenerateNativeDeserializerSource(schema);

            Assert.Contains("throw new InvalidOperationException();", source, StringComparison.Ordinal);
            Assert.DoesNotContain("String::ToJoinString(version)", source, StringComparison.Ordinal);
            Assert.DoesNotContain("String::ToJoinString(MemberCount)", source, StringComparison.Ordinal);
            Assert.DoesNotContain("String::ToJoinString(memberCount)", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures native runtime deserializers release the temporary stream and reader allocated while decoding component payloads.
        /// </summary>
        [Fact]
        public void GenerateNativeDeserializerSource_WhenSchemaContainsClipRectComponent_EmitsStreamAndReaderDisposeGuards() {
            ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(typeof(ClipRectComponent));
            ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator();

            string source = generator.GenerateNativeDeserializerSource(schema);

            Assert.Contains("#include \"runtime/finally.hpp\"", source, StringComparison.Ordinal);
            Assert.Contains("stream->Dispose();", source, StringComparison.Ordinal);
            Assert.Contains("delete stream;", source, StringComparison.Ordinal);
            Assert.Contains("reader->Dispose();", source, StringComparison.Ordinal);
            Assert.Contains("delete reader;", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures native runtime deserializer generation can emit nested array reader source for the planned memory-probe component shape.
        /// </summary>
        [Fact]
        public void GenerateNativeDeserializerSource_WhenSchemaContainsStepArray_EmitsNestedArrayReaderPath() {
            ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(typeof(TestSceneMemoryProbeSerializableComponent));
            ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator();

            Assert.True(generator.CanGenerateNativeDeserializer(schema));

            string source = generator.GenerateNativeDeserializerSource(schema);

            Assert.Contains("ReadDouble()", source, StringComparison.Ordinal);
            Assert.Contains("for (int32_t", source, StringComparison.Ordinal);
            Assert.Contains("new ::TestSceneMemoryProbeSerializableStep()", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures generated native deserializer component-type accessors return constant string references so the interface contract matches generated const-reference string getters.
        /// </summary>
        [Fact]
        public void GenerateNativeDeserializerSource_WhenSchemaContainsClipRectComponent_EmitsConstReferenceComponentTypeGetter() {
            ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(typeof(ClipRectComponent));
            ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator();

            string header = generator.GenerateNativeDeserializerHeader(schema);
            string source = generator.GenerateNativeDeserializerSource(schema);

            Assert.Contains("const std::string& get_ComponentTypeId();", header, StringComparison.Ordinal);
            Assert.Contains("const std::string& GeneratedRuntimeClipRectComponentDeserializer::get_ComponentTypeId()", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures generated native scripted-component deserializers rebuild <see cref="byte4"/> values by assigning channels explicitly instead of relying on one native constructor ordering contract.
        /// </summary>
        [Fact]
        public void GenerateNativeDeserializerSource_WhenSchemaContainsByte4Members_EmitsExplicitChannelAssignments() {
            ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(typeof(RoundedRectComponent));
            ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator();

            string source = generator.GenerateNativeDeserializerSource(schema);

            Assert.Contains("X = reader->ReadByte();", source, StringComparison.Ordinal);
            Assert.Contains("Y = reader->ReadByte();", source, StringComparison.Ordinal);
            Assert.Contains("Z = reader->ReadByte();", source, StringComparison.Ordinal);
            Assert.Contains("W = reader->ReadByte();", source, StringComparison.Ordinal);
            Assert.DoesNotContain("::byte4(reader->ReadByte(), reader->ReadByte(), reader->ReadByte(), reader->ReadByte())", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures generated native deserializers for engine-owned text components rebuild authored layout state through the automatic reflected path.
        /// </summary>
        [Fact]
        public void GenerateNativeDeserializerSource_WhenSchemaContainsTextComponent_EmitsFontReferenceResolutionAndAlignmentRead() {
            ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(typeof(TextComponent));
            ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator();

            string header = generator.GenerateNativeDeserializerHeader(schema);
            string source = generator.GenerateNativeDeserializerSource(schema);

            Assert.Contains("SceneAssetReference", header, StringComparison.Ordinal);
            Assert.Contains("referenceResolver->ResolveFont(reference)", source, StringComparison.Ordinal);
            Assert.Contains("component->set_FontScale(reader->ReadSingle());", source, StringComparison.Ordinal);
            Assert.Contains("component->set_Alignment(static_cast<::TextAlignment>(reader->ReadInt32()));", source, StringComparison.Ordinal);
            Assert.DoesNotContain("component->set_Texture(", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures generated native scripted-component deserializers support animation-clip asset references through the shared runtime scene asset resolver.
        /// </summary>
        [Fact]
        public void GenerateNativeDeserializerSource_WhenSchemaContainsAnimationClipAssetMember_EmitsAnimationClipReferenceResolution() {
            ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(typeof(TestAnimationClipAssetScriptComponent));
            ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator();

            Assert.True(generator.CanGenerateNativeDeserializer(schema));

            string header = generator.GenerateNativeDeserializerHeader(schema);
            string source = generator.GenerateNativeDeserializerSource(schema);

            Assert.Contains("AnimationClipAsset", header, StringComparison.Ordinal);
            Assert.Contains("referenceResolver->ResolveAnimationClip(reference)", source, StringComparison.Ordinal);
            Assert.Contains("component->set_IdleClip(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ReadAnimationClipAsset(", header, StringComparison.Ordinal);
            Assert.DoesNotContain("\"UInt64.hpp\"", header, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures primitive reflected members do not emit synthetic native headers or helper methods when generated native deserializers already read those scalar payloads directly.
        /// </summary>
        [Fact]
        public void GenerateNativeDeserializerSource_WhenSchemaContainsPrimitiveMembers_DoesNotEmitSyntheticPrimitiveHeaders() {
            ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(typeof(AmbientLightComponent));
            ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator();

            string header = generator.GenerateNativeDeserializerHeader(schema);
            string source = generator.GenerateNativeDeserializerSource(schema);

            Assert.DoesNotContain("\"Boolean.hpp\"", header, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Byte.hpp\"", header, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Single.hpp\"", header, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Boolean.hpp\"", source, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Byte.hpp\"", source, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Single.hpp\"", source, StringComparison.Ordinal);
            Assert.DoesNotContain("static bool ReadBoolean", header, StringComparison.Ordinal);
            Assert.DoesNotContain("static uint8_t ReadByte", header, StringComparison.Ordinal);
            Assert.DoesNotContain("static float ReadSingle", header, StringComparison.Ordinal);
            Assert.DoesNotContain("static ::ShadowMapMode ReadShadowMapMode", header, StringComparison.Ordinal);
            Assert.DoesNotContain("value.value__", source, StringComparison.Ordinal);
            Assert.Contains("component->set_Intensity(reader->ReadSingle());", source, StringComparison.Ordinal);
            Assert.Contains("component->set_ShadowMapMode(static_cast<::ShadowMapMode>(reader->ReadByte()));", source, StringComparison.Ordinal);
            Assert.Contains("component->set_ShadowsEnabled(reader->ReadByte() != 0);", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures generated native deserializers include the current qualified engine math header path instead of the removed legacy leaf header name.
        /// </summary>
        [Fact]
        public void GenerateNativeDeserializerHeader_WhenSchemaContainsAmbientLightComponent_UsesGeneratedEngineMathHeaderNames() {
            ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(typeof(AmbientLightComponent));
            ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator();

            string header = generator.GenerateNativeDeserializerHeader(schema);

            Assert.Contains("#include \"float4.hpp\"", header, StringComparison.Ordinal);
            Assert.DoesNotContain("#include \"helengine_float4.hpp\"", header, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures generated native deserializers rebuild optional scene asset references through the sanctioned factory path instead of constructing mutable references inline.
        /// </summary>
        [Fact]
        public void GenerateNativeDeserializerSource_WhenSchemaContainsTextComponent_UsesSceneAssetReferenceFactoryReadPath() {
            ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(typeof(TextComponent));
            ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator();

            string source = generator.GenerateNativeDeserializerSource(schema);

            Assert.Contains("SceneAssetReferenceFactory::ReadOptionalReference(reader)", source, StringComparison.Ordinal);
            Assert.DoesNotContain("new ::SceneAssetReference()", source, StringComparison.Ordinal);
            Assert.DoesNotContain("value->set_SourceKind", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures platform-extended schemas emit native deserializer writes through the component synthetic-member store instead of assuming one reflected property exists on the component type.
        /// </summary>
        [Fact]
        public void GenerateNativeDeserializerSource_WhenSchemaContainsDsSyntheticTextMember_EmitsSyntheticMemberStoreWrite() {
            PlatformExtendedScriptComponentSchemaBuilder schemaBuilder = new PlatformExtendedScriptComponentSchemaBuilder();
            ScriptComponentReflectionSchema schema = schemaBuilder.Build(typeof(TextComponent), CreateDsSyntheticTextPlatformDefinition());
            ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator();

            string source = generator.GenerateNativeDeserializerSource(schema);

            Assert.Contains("component->SetSyntheticInt32Member(std::string(\"BGLayer\"), reader->ReadInt32());", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures generated native deserializers rebuild engine-owned serialized payload members through the shared restore helper instead of traversing one removed reflected byte-array contract.
        /// </summary>
        [Fact]
        public void GenerateNativeDeserializerSource_WhenSchemaContainsStaticMeshCookedRuntimePayload_EmitsEngineSerializedPayloadRestore() {
            ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(typeof(StaticMeshCollider3DComponent));
            ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator();

            Assert.True(generator.CanGenerateNativeDeserializer(schema));

            string source = generator.GenerateNativeDeserializerSource(schema);

            Assert.Contains("EngineSerializedPayload::Restore", source, StringComparison.Ordinal);
            Assert.Contains("value->set_Payload(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("value->set_Data(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("value->set_FormatId(", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Creates the minimal DS platform definition required by the synthetic text deserializer test.
        /// </summary>
        /// <returns>Minimal DS platform definition with one synthetic text member.</returns>
        static PlatformDefinition CreateDsSyntheticTextPlatformDefinition() {
            return new PlatformDefinition(
                "ds",
                "Nintendo DS",
                Array.Empty<PlatformBuildProfileDefinition>(),
                Array.Empty<PlatformGraphicsProfileDefinition>(),
                Array.Empty<PlatformAssetRequirementDefinition>(),
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                Array.Empty<PlatformComponentSupportRule>(),
                Array.Empty<PlatformCodegenProfileDefinition>(),
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>(),
                componentMemberDefinitions: [
                    new PlatformComponentMemberDefinition(
                        "helengine.TextComponent",
                        "BGLayer",
                        "BG Layer",
                        PlatformComponentMemberValueKind.Int32,
                        "0",
                        0)
                ]);
        }
    }
}
