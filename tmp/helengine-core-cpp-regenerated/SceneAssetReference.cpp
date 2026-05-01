#ifdef DrawText
#undef DrawText
#endif
#include "SceneAssetReference.hpp"
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

SceneAssetReference::SceneAssetReference() : AssetId(), ProviderId(), RelativePath(), SourceKind()
{
}

std::string SceneAssetReference::get_AssetId()
{
return this->AssetId;
}

void SceneAssetReference::set_AssetId(std::string value)
{
this->AssetId = value;
}

std::string SceneAssetReference::get_ProviderId()
{
return this->ProviderId;
}

void SceneAssetReference::set_ProviderId(std::string value)
{
this->ProviderId = value;
}

std::string SceneAssetReference::get_RelativePath()
{
return this->RelativePath;
}

void SceneAssetReference::set_RelativePath(std::string value)
{
this->RelativePath = value;
}

::SceneAssetReferenceSourceKind SceneAssetReference::get_SourceKind()
{
return this->SourceKind;
}

void SceneAssetReference::set_SourceKind(::SceneAssetReferenceSourceKind value)
{
this->SourceKind = value;
}

