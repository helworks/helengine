#ifdef DrawText
#undef DrawText
#endif
#include "SceneEntityAsset.hpp"
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

SceneEntityAsset::SceneEntityAsset() : Children(Array<SceneEntityAsset*>::Empty()), Components(Array<SceneComponentAssetRecord*>::Empty()), LocalOrientation(), LocalPosition(), LocalScale(), Name()
{
}

Array<::SceneEntityAsset*>* SceneEntityAsset::get_Children()
{
return this->Children;
}

void SceneEntityAsset::set_Children(Array<::SceneEntityAsset*>* value)
{
this->Children = value;
}

Array<::SceneComponentAssetRecord*>* SceneEntityAsset::get_Components()
{
return this->Components;
}

void SceneEntityAsset::set_Components(Array<::SceneComponentAssetRecord*>* value)
{
this->Components = value;
}

::float4 SceneEntityAsset::get_LocalOrientation()
{
return this->LocalOrientation;
}

void SceneEntityAsset::set_LocalOrientation(::float4 value)
{
this->LocalOrientation = value;
}

::float3 SceneEntityAsset::get_LocalPosition()
{
return this->LocalPosition;
}

void SceneEntityAsset::set_LocalPosition(::float3 value)
{
this->LocalPosition = value;
}

::float3 SceneEntityAsset::get_LocalScale()
{
return this->LocalScale;
}

void SceneEntityAsset::set_LocalScale(::float3 value)
{
this->LocalScale = value;
}

std::string SceneEntityAsset::get_Name()
{
return this->Name;
}

void SceneEntityAsset::set_Name(std::string value)
{
this->Name = value;
}

