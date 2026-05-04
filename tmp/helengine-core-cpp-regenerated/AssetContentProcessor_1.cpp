#ifdef DrawText
#undef DrawText
#endif
#include "AssetContentProcessor_1.hpp"
#include "runtime/native_exceptions.hpp"
#include "Asset.hpp"
#include "AssetSerializer.hpp"
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

template <typename TAsset>
Type* AssetContentProcessor_1<TAsset>::get_OutputType()
{
return he_cpp_type_of<TAsset>("TAsset");
}

template <typename TAsset>
TAsset AssetContentProcessor_1<TAsset>::Read(::Stream* stream)
{
    if (stream == nullptr)
    {
throw new ArgumentNullException("stream");
    }
::Asset *asset = AssetSerializer::Deserialize(stream);
    TAsset typedAsset = he_cpp_try_cast<TAsset>(asset);
    if (typedAsset != nullptr)
    {
return typedAsset;    }
throw new InvalidOperationException(std::string("Serialized asset did not contain '") + he_cpp_type_of<TAsset>("TAsset")->get_Name() + std::string("'."));
}

template <typename TAsset>
void* AssetContentProcessor_1<TAsset>::ReadObject(::Stream* stream)
{
return this->Read(stream);}

