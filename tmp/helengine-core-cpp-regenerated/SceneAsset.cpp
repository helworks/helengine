#ifdef DrawText
#undef DrawText
#endif
#include "SceneAsset.hpp"
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

SceneAsset::SceneAsset() : AssetReferences(Array<SceneAssetReference*>::Empty()), RootEntities(Array<SceneEntityAsset*>::Empty())
{
}

std::string SceneAsset::FileExtension = ".helen";

Array<::SceneAssetReference*>* SceneAsset::get_AssetReferences()
{
return this->AssetReferences;
}

void SceneAsset::set_AssetReferences(Array<::SceneAssetReference*>* value)
{
this->AssetReferences = value;
}

Array<::SceneEntityAsset*>* SceneAsset::get_RootEntities()
{
return this->RootEntities;
}

void SceneAsset::set_RootEntities(Array<::SceneEntityAsset*>* value)
{
this->RootEntities = value;
}

std::string SceneAsset::get_Id()
{
return this->Asset::get_Id();
}

void SceneAsset::set_Id(std::string value)
{
this->Asset::set_Id(value);
}

