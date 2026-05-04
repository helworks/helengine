#ifdef DrawText
#undef DrawText
#endif
#include "RuntimeCodeModuleManifestEntry.hpp"
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

Array<std::string>* RuntimeCodeModuleManifestEntry::get_DependencyModuleIds()
{
return this->DependencyModuleIds;
}

::RuntimeCodeModuleLoadState RuntimeCodeModuleManifestEntry::get_LoadState()
{
return this->LoadState;
}

std::string RuntimeCodeModuleManifestEntry::get_ModuleId()
{
return this->ModuleId;
}

std::string RuntimeCodeModuleManifestEntry::get_RuntimeSpecializationId()
{
return this->RuntimeSpecializationId;
}

RuntimeCodeModuleManifestEntry::RuntimeCodeModuleManifestEntry(std::string moduleId, std::string runtimeSpecializationId, ::RuntimeCodeModuleLoadState loadState, Array<std::string>* dependencyModuleIds) : DependencyModuleIds(), LoadState(), ModuleId(), RuntimeSpecializationId()
{
    if (String::IsNullOrWhiteSpace(moduleId))
    {
throw ([&]() {
auto __ctor_arg_000000BA = "Runtime code module id is required.";
auto __ctor_arg_000000BB = "moduleId";
return new ArgumentException(__ctor_arg_000000BA, __ctor_arg_000000BB);
})();
    }
    if (String::IsNullOrWhiteSpace(runtimeSpecializationId))
    {
throw ([&]() {
auto __ctor_arg_000000BC = "Runtime code module specialization id is required.";
auto __ctor_arg_000000BD = "runtimeSpecializationId";
return new ArgumentException(__ctor_arg_000000BC, __ctor_arg_000000BD);
})();
    }
    if (dependencyModuleIds == nullptr)
    {
throw new ArgumentNullException("dependencyModuleIds");
    }
for (int32_t index = 0; index < dependencyModuleIds->Length; index++) {
const std::string dependencyModuleId = (*dependencyModuleIds)[index];
    if (String::IsNullOrWhiteSpace(dependencyModuleId))
    {
throw ([&]() {
auto __ctor_arg_000000BE = "Runtime code module dependencies cannot contain blank entries.";
auto __ctor_arg_000000BF = "dependencyModuleIds";
return new ArgumentException(__ctor_arg_000000BE, __ctor_arg_000000BF);
})();
    }
}
this->ModuleId = moduleId;
this->RuntimeSpecializationId = runtimeSpecializationId;
this->LoadState = loadState;
this->DependencyModuleIds = dependencyModuleIds;
}

