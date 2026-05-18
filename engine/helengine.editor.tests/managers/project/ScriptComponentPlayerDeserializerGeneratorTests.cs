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
            Assert.Contains("helengine.ClipRectComponent, helengine.core", source, StringComparison.Ordinal);
            Assert.Contains("component->set_Size(reader->ReadInt2());", source, StringComparison.Ordinal);
            Assert.Contains("int32_t GeneratedRuntimeClipRectComponentDeserializer::MemberCount = 1;", source, StringComparison.Ordinal);
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
    }
}
