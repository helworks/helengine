#ifdef DrawText
#undef DrawText
#endif
#include "SceneComponentAssetRecord.hpp"
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
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
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

SceneComponentAssetRecord::SceneComponentAssetRecord() : ComponentIndex(0), ComponentTypeId(), Payload(Array<uint8_t>::Empty())
{
}

int32_t SceneComponentAssetRecord::get_ComponentIndex()
{
return this->ComponentIndex;
}

void SceneComponentAssetRecord::set_ComponentIndex(int32_t value)
{
this->ComponentIndex = value;
}

std::string SceneComponentAssetRecord::get_ComponentTypeId()
{
return this->ComponentTypeId;
}

void SceneComponentAssetRecord::set_ComponentTypeId(std::string value)
{
this->ComponentTypeId = value;
}

Array<uint8_t>* SceneComponentAssetRecord::get_Payload()
{
return this->Payload;
}

void SceneComponentAssetRecord::set_Payload(Array<uint8_t>* value)
{
this->Payload = value;
}

