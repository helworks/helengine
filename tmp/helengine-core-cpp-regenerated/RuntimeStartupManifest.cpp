#ifdef DrawText
#undef DrawText
#endif
#include "RuntimeStartupManifest.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/stream-reader.hpp"
#include "system/text/encoding.hpp"
#include "RuntimeStartupManifest.hpp"
#include "RuntimeManifestJsonReader.hpp"
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
#include "system/diagnostics/stopwatch.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream-reader.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

std::string RuntimeStartupManifest::get_StartupSceneId()
{
return this->StartupSceneId;
}

::RuntimeStorageProfileId* RuntimeStartupManifest::get_StorageProfileId()
{
return this->StorageProfileId;
}

::RuntimeStartupManifest* RuntimeStartupManifest::ReadFromFile(std::string startupManifestPath)
{
    if (String::IsNullOrWhiteSpace(startupManifestPath))
    {
throw ([&]() {
auto __ctor_arg_000000D4 = "Runtime startup manifest path is required.";
auto __ctor_arg_000000D5 = "startupManifestPath";
return new ArgumentException(__ctor_arg_000000D4, __ctor_arg_000000D5);
})();
    }
    if (!File::Exists(startupManifestPath))
    {
throw new FileNotFoundException(std::string("Runtime startup manifest '") + startupManifestPath + std::string("' was not found."), startupManifestPath);
    }
::FileStream *fileStream = File::OpenRead(startupManifestPath);
StreamReader *reader = new StreamReader(fileStream, Encoding::UTF8, false, 1024, true);
const std::string json = reader->ReadToEnd();
reader->Dispose();
fileStream->Dispose();
return RuntimeManifestJsonReader::ReadRuntimeStartupManifest(json);}

RuntimeStartupManifest::RuntimeStartupManifest(std::string startupSceneId, ::RuntimeStorageProfileId* storageProfileId) : StartupSceneId(), StorageProfileId()
{
    if (String::IsNullOrWhiteSpace(startupSceneId))
    {
throw ([&]() {
auto __ctor_arg_000000D6 = "Startup scene id is required.";
auto __ctor_arg_000000D7 = "startupSceneId";
return new ArgumentException(__ctor_arg_000000D6, __ctor_arg_000000D7);
})();
    }
    if (storageProfileId == nullptr)
    {
throw new ArgumentNullException("storageProfileId");
    }
this->StartupSceneId = startupSceneId;
this->StorageProfileId = storageProfileId;
}

