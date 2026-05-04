#ifdef DrawText
#undef DrawText
#endif
#include "FontAssetBinarySerializer.hpp"
#include "runtime/native_exceptions.hpp"
#include "EngineBinaryHeader.hpp"
#include "EngineBinaryHeaderSerializer.hpp"
#include "FontAsset.hpp"
#include "EngineBinaryReader.hpp"
#include "FontInfo.hpp"
#include "TextureAsset.hpp"
#include "runtime/native_dictionary.hpp"
#include "RuntimeTexture.hpp"
#include "Core.hpp"
#include "RenderManager2D.hpp"
#include "EditorBinaryRecordKind.hpp"
#include "FontChar.hpp"
#include "runtime/array.hpp"
#include "runtime/native_cast.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_nullable.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"

uint8_t FontAssetBinarySerializer::CurrentVersion = 1;

uint16_t FontAssetBinarySerializer::FormatId = 1;

::EditorBinaryRecordKind FontAssetBinarySerializer::RecordKind = EditorBinaryRecordKind::FontAsset;

::FontAsset* FontAssetBinarySerializer::Deserialize(::Stream* stream)
{
    if (stream == nullptr)
    {
throw new ArgumentNullException("stream");
    }
::EngineBinaryHeader *header = EngineBinaryHeaderSerializer::Read(stream);
return Deserialize(stream, header);}

::FontAsset* FontAssetBinarySerializer::Deserialize(::Stream* stream, ::EngineBinaryHeader* header)
{
    if (stream == nullptr)
    {
throw new ArgumentNullException("stream");
    }
    if (header == nullptr)
    {
throw new ArgumentNullException("header");
    }
ValidateHeader(header);
    if (Core::get_Instance() == nullptr || Core::get_Instance()->get_RenderManager2D() == nullptr)
    {
throw new InvalidOperationException("Font assets require an initialized core renderer before deserialization.");
    }
{
::EngineBinaryReader *reader = EngineBinaryReader::Create(stream, header->get_Endianness(), true);
::FontInfo *fontInfo = ([&]() {
auto __ctor_arg_00000059 = reader->ReadString();
auto __ctor_arg_0000005A = reader->ReadInt32();
auto __ctor_arg_0000005B = reader->ReadSingle();
return new ::FontInfo(__ctor_arg_00000059, __ctor_arg_0000005A, __ctor_arg_0000005B);
})();
const float lineHeight = reader->ReadSingle();
const int32_t atlasWidth = reader->ReadInt32();
const int32_t atlasHeight = reader->ReadInt32();
::TextureAsset *sourceTexture = ([&]() {
auto __object_0000005C = new ::TextureAsset();
__object_0000005C->Width = reader->ReadUInt16();
__object_0000005C->Height = reader->ReadUInt16();
__object_0000005C->Colors = reader->ReadByteArray();
return __object_0000005C;
})();
const int32_t characterCount = reader->ReadInt32();
Dictionary<char, ::FontChar> *characters = new Dictionary<char, ::FontChar>(characterCount);
for (int32_t index = 0; index < characterCount; index++) {
const char character = static_cast<char>(reader->ReadUInt16());
::FontChar fontChar = ([&]() {
auto __ctor_arg_0000005D = reader->ReadFloat4();
auto __ctor_arg_0000005E = reader->ReadSingle();
auto __ctor_arg_0000005F = reader->ReadSingle();
auto __ctor_arg_00000060 = reader->ReadSingle();
auto __ctor_arg_00000061 = reader->ReadSingle();
return ::FontChar(__ctor_arg_0000005D, __ctor_arg_0000005E, __ctor_arg_0000005F, __ctor_arg_00000060, __ctor_arg_00000061);
})();
characters->Add(character, fontChar);
}
::RuntimeTexture *texture = Core::get_Instance()->get_RenderManager2D()->BuildTextureFromRaw(sourceTexture);
::FontAsset *asset = ([&]() {
auto __object_00000062 = new ::FontAsset(fontInfo, texture, characters, lineHeight, atlasWidth, atlasHeight);
__object_00000062->set_SourceTextureAsset(sourceTexture);
return __object_00000062;
})();
return asset;}
}

void FontAssetBinarySerializer::ValidateHeader(::EngineBinaryHeader* header)
{
    if (header->get_FormatId() != FormatId)
    {
throw new InvalidOperationException(std::string("Unsupported font binary format id '") + std::to_string(header->get_FormatId()) + std::string("'."));
    }
    if (header->get_RecordKind() != static_cast<uint16_t>(RecordKind))
    {
throw new InvalidOperationException(std::string("Unexpected font record kind '") + std::to_string(header->get_RecordKind()) + std::string("'."));
    }
    if (header->get_Version() != CurrentVersion)
    {
throw new InvalidOperationException(std::string("Unsupported font binary version '") + std::to_string(header->get_Version()) + std::string("'."));
    }
}

