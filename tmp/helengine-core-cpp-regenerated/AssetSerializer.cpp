#ifdef DrawText
#undef DrawText
#endif
#include "AssetSerializer.hpp"
#include "runtime/native_exceptions.hpp"
#include "EditorAssetBinarySerializer.hpp"
#include "EngineBinaryHeader.hpp"
#include "EngineBinaryHeaderSerializer.hpp"
#include "Asset.hpp"
#include "system/io/memory-stream.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
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
#include "system/bit_converter.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/text/encoding.hpp"

::Asset* AssetSerializer::Deserialize(::Stream* stream)
{
    if (stream == nullptr)
    {
throw new ArgumentNullException("stream");
    }
::EngineBinaryHeader *header = EngineBinaryHeaderSerializer::Read(stream);
    if (header->get_FormatId() == EditorAssetBinarySerializer::FormatId)
    {
return EditorAssetBinarySerializer::Deserialize(stream, header);    }
throw new InvalidOperationException(std::string("Unsupported asset binary format id '") + std::to_string(header->get_FormatId()) + std::string("'."));
}

::Asset* AssetSerializer::DeserializeFromBytes(Array<uint8_t>* data)
{
    if (data == nullptr)
    {
throw new ArgumentNullException("data");
    }
{
MemoryStream *stream = new MemoryStream(data, false);
return Deserialize(stream);}
}

void AssetSerializer::Serialize(::Stream* stream, ::Asset* asset)
{
    if (stream == nullptr)
    {
throw new ArgumentNullException("stream");
    }
else     if (asset == nullptr)
    {
throw new ArgumentNullException("asset");
    }
EditorAssetBinarySerializer::Serialize(stream, asset);
}

Array<uint8_t>* AssetSerializer::SerializeToBytes(::Asset* asset)
{
    if (asset == nullptr)
    {
throw new ArgumentNullException("asset");
    }
{
MemoryStream *stream = new MemoryStream();
Serialize(stream, asset);
return stream->ToArray();}
}

