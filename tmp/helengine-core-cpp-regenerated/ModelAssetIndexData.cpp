#ifdef DrawText
#undef DrawText
#endif
#include "ModelAssetIndexData.hpp"
#include "runtime/native_exceptions.hpp"
#include "ModelAssetIndexData.hpp"
#include "runtime/array.hpp"
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
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"

int32_t ModelAssetIndexData::get_IndexCount()
{
return this->IndexCount;
}

Array<uint16_t>* ModelAssetIndexData::get_Indices16()
{
return this->Indices16;
}

Array<uint32_t>* ModelAssetIndexData::get_Indices32()
{
return this->Indices32;
}

bool ModelAssetIndexData::get_Uses32BitIndices()
{
return this->Uses32BitIndices;
}

::ModelAssetIndexData* ModelAssetIndexData::Resolve(::ModelAsset* asset)
{
    if (asset == nullptr)
    {
throw new ArgumentNullException("asset");
    }
const bool hasIndices16 = HasIndices(asset->Indices16);
const bool hasIndices32 = HasIndices(asset->Indices32);
    if (hasIndices16 && hasIndices32)
    {
throw new InvalidOperationException("Model assets cannot define both 16-bit and 32-bit index buffers.");
    }
else     if (hasIndices32)
    {
return new ::ModelAssetIndexData(true, asset->Indices32->Length, nullptr, asset->Indices32);    }
else     if (hasIndices16)
    {
return new ::ModelAssetIndexData(false, asset->Indices16->Length, asset->Indices16, nullptr);    }
return new ::ModelAssetIndexData(false, 0, asset->Indices16, asset->Indices32);}

bool ModelAssetIndexData::HasIndices(Array<uint16_t>* indices)
{
return indices != nullptr && indices->Length > 0;}

bool ModelAssetIndexData::HasIndices(Array<uint32_t>* indices)
{
return indices != nullptr && indices->Length > 0;}

ModelAssetIndexData::ModelAssetIndexData(bool uses32BitIndices, int32_t indexCount, Array<uint16_t>* indices16, Array<uint32_t>* indices32) : IndexCount(0), Indices16(), Indices32(), Uses32BitIndices()
{
this->Uses32BitIndices = uses32BitIndices;
this->IndexCount = indexCount;
this->Indices16 = indices16;
this->Indices32 = indices32;
}

