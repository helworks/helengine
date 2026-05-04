#ifdef DrawText
#undef DrawText
#endif
#include "RuntimeFPSComponentDeserializer.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/memory-stream.hpp"
#include "EngineBinaryReader.hpp"
#include "SceneAssetReference.hpp"
#include "FPSComponent.hpp"
#include "Component.hpp"
#include "EngineBinaryEndianness.hpp"
#include "Core.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_cast.hpp"
#include "runtime/native_datetime.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_nullable.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_stack.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/binary_primitives.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/diagnostics/stopwatch.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream-reader.hpp"
#include "system/io/stream.hpp"
#include "system/io/string-reader.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/security/cryptography/sha256.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

std::string RuntimeFPSComponentDeserializer::get_ComponentTypeId()
{
return ComponentType;
}

::Component* RuntimeFPSComponentDeserializer::Deserialize(::SceneComponentAssetRecord* record, ::RuntimeSceneAssetReferenceResolver* referenceResolver)
{
    if (record == nullptr)
    {
throw new ArgumentNullException("record");
    }
    if (referenceResolver == nullptr)
    {
throw new ArgumentNullException("referenceResolver");
    }
    if (!String::Equals(record->get_ComponentTypeId(), ComponentType, StringComparison::OrdinalIgnoreCase))
    {
throw new InvalidOperationException(std::string("FPS component deserializer cannot deserialize '") + record->get_ComponentTypeId() + std::string("'."));
    }
{
::MemoryStream *stream = ([&]() {
auto __ctor_arg_00000190 = ([&]() {
Array<uint8_t>* __coalesce_00000191 = record->get_Payload();
return __coalesce_00000191 != nullptr ? __coalesce_00000191 : Array<uint8_t>::Empty();
})();
auto __ctor_arg_00000192 = false;
return new ::MemoryStream(__ctor_arg_00000190, __ctor_arg_00000192);
})();
{
::EngineBinaryReader *reader = EngineBinaryReader::Create(stream, EngineBinaryEndianness::LittleEndian, true);
const uint8_t version = reader->ReadByte();
    if (version != CurrentVersion && version != 1)
    {
throw new InvalidOperationException(std::string("Unsupported FPS component payload version '") + std::to_string(version) + std::string("'."));
    }
::SceneAssetReference *fontReference = version >= 2 ? this->ReadOptionalReference(reader) : nullptr;
::FPSComponent *fpsComponent = ([&]() {
auto __object_00000193 = new ::FPSComponent();
__object_00000193->set_RefreshIntervalSeconds(BitConverter::Int64BitsToDouble(reader->ReadInt64()));
__object_00000193->set_Padding(reader->ReadInt2());
__object_00000193->set_RenderOrder2D(reader->ReadByte());
return __object_00000193;
})();
    if (fontReference != nullptr)
    {
fpsComponent->set_Font(referenceResolver->ResolveFont(fontReference));
    }
else     if (version == 1 && Core::get_Instance() != nullptr && Core::get_Instance()->get_DefaultFontAsset() != nullptr)
    {
fpsComponent->set_Font(Core::get_Instance()->get_DefaultFontAsset());
    }
else {
throw new InvalidOperationException("FPSComponent requires a packaged font reference before deserialization.");
}
return fpsComponent;}
}
}

std::string RuntimeFPSComponentDeserializer::ComponentType = "helengine.FPSComponent";

uint8_t RuntimeFPSComponentDeserializer::CurrentVersion = 2;

::SceneAssetReference* RuntimeFPSComponentDeserializer::ReadOptionalReference(::EngineBinaryReader* reader)
{
    if (reader == nullptr)
    {
throw new ArgumentNullException("reader");
    }
    if (reader->ReadByte() == 0)
    {
return nullptr;    }
return ([&]() {
auto __object_00000194 = new ::SceneAssetReference();
__object_00000194->set_SourceKind(static_cast<SceneAssetReferenceSourceKind>(reader->ReadInt32()));
__object_00000194->set_RelativePath(reader->ReadString());
__object_00000194->set_ProviderId(reader->ReadString());
__object_00000194->set_AssetId(reader->ReadString());
return __object_00000194;
})();}

