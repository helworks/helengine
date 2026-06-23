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
    }
}
