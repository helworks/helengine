#ifdef DrawText
#undef DrawText
#endif
#include "RuntimeCodeModuleManifest.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/stream-reader.hpp"
#include "system/text/encoding.hpp"
#include "RuntimeCodeModuleManifest.hpp"
#include "runtime/array.hpp"
#include "RuntimeCodeModuleLoadState.hpp"
#include "RuntimeCodeModuleManifestEntry.hpp"
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

Array<::RuntimeCodeModuleManifestEntry*>* RuntimeCodeModuleManifest::get_Entries()
{
return this->Entries;
}

bool RuntimeCodeModuleManifest::CanUnloadModule(std::string moduleId)
{
return this->GetLoadState(moduleId) != RuntimeCodeModuleLoadState::ResidentAtStartup;}

Array<std::string>* RuntimeCodeModuleManifest::GetResidentModuleIds()
{
Array<std::string> *residentModuleIds = new Array<std::string>(this->Entries->Length);
int32_t residentModuleCount = 0;
for (int32_t index = 0; index < this->Entries->Length; index++) {
::RuntimeCodeModuleManifestEntry *entry = (*this->Entries)[index];
    if (entry->get_LoadState() != RuntimeCodeModuleLoadState::Unloadable)
    {
(*residentModuleIds)[residentModuleCount] = entry->get_ModuleId();
residentModuleCount++;
    }
}
    if (residentModuleCount == residentModuleIds->Length)
    {
return residentModuleIds;    }
Array<std::string> *exactResidentModuleIds = new Array<std::string>(residentModuleCount);
for (int32_t index = 0; index < residentModuleCount; index++) {
(*exactResidentModuleIds)[index] = (*residentModuleIds)[index];
}
return exactResidentModuleIds;}

Array<std::string>* RuntimeCodeModuleManifest::GetUnloadableModuleIds()
{
Array<std::string> *unloadableModuleIds = new Array<std::string>(this->Entries->Length);
int32_t unloadableModuleCount = 0;
for (int32_t index = 0; index < this->Entries->Length; index++) {
::RuntimeCodeModuleManifestEntry *entry = (*this->Entries)[index];
    if (entry->get_LoadState() == RuntimeCodeModuleLoadState::Unloadable)
    {
(*unloadableModuleIds)[unloadableModuleCount] = entry->get_ModuleId();
unloadableModuleCount++;
    }
}
    if (unloadableModuleCount == unloadableModuleIds->Length)
    {
return unloadableModuleIds;    }
Array<std::string> *exactUnloadableModuleIds = new Array<std::string>(unloadableModuleCount);
for (int32_t index = 0; index < unloadableModuleCount; index++) {
(*exactUnloadableModuleIds)[index] = (*unloadableModuleIds)[index];
}
return exactUnloadableModuleIds;}

::RuntimeCodeModuleManifest* RuntimeCodeModuleManifest::ReadFromFile(std::string manifestPath)
{
    if (String::IsNullOrWhiteSpace(manifestPath))
    {
throw ([&]() {
auto __ctor_arg_000000B6 = "Runtime code-module manifest path is required.";
auto __ctor_arg_000000B7 = "manifestPath";
return new ArgumentException(__ctor_arg_000000B6, __ctor_arg_000000B7);
})();
    }
    if (!File::Exists(manifestPath))
    {
throw new FileNotFoundException(std::string("Runtime code-module manifest '") + manifestPath + std::string("' was not found."), manifestPath);
    }
::FileStream *fileStream = File::OpenRead(manifestPath);
StreamReader *reader = new StreamReader(fileStream, Encoding::UTF8, false, 1024, true);
const std::string json = reader->ReadToEnd();
reader->Dispose();
fileStream->Dispose();
return RuntimeManifestJsonReader::ReadRuntimeCodeModuleManifest(json);}

RuntimeCodeModuleManifest::RuntimeCodeModuleManifest(Array<::RuntimeCodeModuleManifestEntry*>* entries) : Entries()
{
    if (entries == nullptr)
    {
throw new ArgumentNullException("entries");
    }
this->Entries = entries;
}

::RuntimeCodeModuleLoadState RuntimeCodeModuleManifest::GetLoadState(std::string moduleId)
{
    if (String::IsNullOrWhiteSpace(moduleId))
    {
throw ([&]() {
auto __ctor_arg_000000B8 = "Module id is required.";
auto __ctor_arg_000000B9 = "moduleId";
return new ArgumentException(__ctor_arg_000000B8, __ctor_arg_000000B9);
})();
    }
for (int32_t index = 0; index < this->Entries->Length; index++) {
::RuntimeCodeModuleManifestEntry *entry = (*this->Entries)[index];
    if (String::Equals(entry->get_ModuleId(), moduleId, StringComparison::OrdinalIgnoreCase))
    {
return entry->get_LoadState();    }
}
throw new InvalidOperationException(std::string("Runtime code module '") + moduleId + std::string("' was not found in the residency manifest."));
}

