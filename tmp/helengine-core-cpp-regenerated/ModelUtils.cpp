#ifdef DrawText
#undef DrawText
#endif
#include "ModelUtils.hpp"
#include "ModelAsset.hpp"
#include "runtime/array.hpp"
#include "float3.hpp"
#include "float2.hpp"
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

::ModelAsset* ModelUtils::GenerateCubeMesh(::float3 position, ::float3 scale)
{
::ModelAsset *modelData = new ::ModelAsset();
modelData->set_Id(std::string("00000000-0000-0000-0000-000000000000"));
Array<::float3> *positions = new Array<float3>({ ::float3(-1, -1, -1), ::float3(1, -1, -1), ::float3(1, 1, -1), ::float3(-1, 1, -1), ::float3(-1, -1, 1), ::float3(1, -1, 1), ::float3(1, 1, 1), ::float3(-1, 1, 1), ::float3(1, -1, -1), ::float3(1, -1, 1), ::float3(1, 1, 1), ::float3(1, 1, -1), ::float3(-1, -1, -1), ::float3(-1, -1, 1), ::float3(-1, 1, 1), ::float3(-1, 1, -1), ::float3(-1, 1, -1), ::float3(1, 1, -1), ::float3(1, 1, 1), ::float3(-1, 1, 1), ::float3(-1, -1, -1), ::float3(1, -1, -1), ::float3(1, -1, 1), ::float3(-1, -1, 1) });
Array<::float3> *normals = new Array<float3>({ ::float3(0, 0, -1), ::float3(0, 0, -1), ::float3(0, 0, -1), ::float3(0, 0, -1), ::float3(0, 0, 1), ::float3(0, 0, 1), ::float3(0, 0, 1), ::float3(0, 0, 1), ::float3(1, 0, 0), ::float3(1, 0, 0), ::float3(1, 0, 0), ::float3(1, 0, 0), ::float3(-1, 0, 0), ::float3(-1, 0, 0), ::float3(-1, 0, 0), ::float3(-1, 0, 0), ::float3(0, 1, 0), ::float3(0, 1, 0), ::float3(0, 1, 0), ::float3(0, 1, 0), ::float3(0, -1, 0), ::float3(0, -1, 0), ::float3(0, -1, 0), ::float3(0, -1, 0) });
Array<::float2> *texCoords = new Array<float2>({ ::float2(0, 0), ::float2(1, 0), ::float2(1, 1), ::float2(0, 1), ::float2(0, 0), ::float2(1, 0), ::float2(1, 1), ::float2(0, 1), ::float2(0, 0), ::float2(1, 0), ::float2(1, 1), ::float2(0, 1), ::float2(0, 0), ::float2(1, 0), ::float2(1, 1), ::float2(0, 1), ::float2(0, 0), ::float2(1, 0), ::float2(1, 1), ::float2(0, 1), ::float2(0, 0), ::float2(1, 0), ::float2(1, 1), ::float2(0, 1) });
Array<uint16_t> *indices = new Array<uint16_t>({ 0, 1, 2, 2, 3, 0, 6, 5, 4, 4, 7, 6, 8, 9, 10, 10, 11, 8, 14, 13, 12, 12, 15, 14, 16, 17, 18, 18, 19, 16, 22, 21, 20, 20, 23, 22 });
for (int32_t i = 0; i < 24; i++) {
(*positions)[i] = (*positions)[i] * scale + position;
}
modelData->Positions = positions;
modelData->TexCoords = texCoords;
modelData->Normals = normals;
modelData->Indices16 = indices;
return modelData;}

::ModelAsset* ModelUtils::GeneratePlaneMesh(::float3 position, ::float3 scale)
{
::ModelAsset *modelData = new ::ModelAsset();
modelData->set_Id(std::string("00000000-0000-0000-0000-000000000000"));
Array<::float3> *positions = new Array<float3>({ ::float3(-1, -1, -1), ::float3(1, -1, -1), ::float3(1, -1, 1), ::float3(-1, -1, 1) });
Array<::float3> *normals = new Array<float3>({ ::float3(0, -1, 0), ::float3(0, -1, 0), ::float3(0, -1, 0), ::float3(0, -1, 0) });
Array<::float2> *texCoords = new Array<float2>({ ::float2(0, 0), ::float2(1, 0), ::float2(1, 1), ::float2(0, 1) });
Array<uint16_t> *indices = new Array<uint16_t>({ 0, 1, 2, 2, 3, 0 });
for (int32_t i = 0; i < positions->Length; i++) {
(*positions)[i] = (*positions)[i] * scale + position;
}
modelData->Positions = positions;
modelData->TexCoords = texCoords;
modelData->Normals = normals;
modelData->Indices16 = indices;
return modelData;}

